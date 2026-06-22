using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>
/// Translates a resolved <see cref="Binding"/> plus its <see cref="TriggerResult"/> into
/// concrete <see cref="IInputSink"/> calls (docs/02_Architecture.md §3). Profile-switch
/// actions are surfaced as an event for ProfileManager rather than sent to the OS.
/// </summary>
public sealed class ActionExecutor
{
    private readonly IInputSink _sink;

    public ActionExecutor(IInputSink sink) => _sink = sink;

    /// <summary>Raised when a binding requests a profile switch (FR-5.4); handled by ProfileManager.</summary>
    public event EventHandler<SwitchProfileAction>? ProfileSwitchRequested;

    public void Execute(Binding binding, TriggerResult trigger, MidiMessage message)
    {
        foreach (var action in binding.Actions)
        {
            Execute(action, trigger, message);
        }
    }

    private void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        switch (action)
        {
            case KeyAction key:
                ExecuteKey(key, trigger);
                break;

            case MouseClickAction click:
                _sink.MouseClick(click.Button, click.Double);
                break;

            case CursorMoveAction move:
                ExecuteMove(move, trigger);
                break;

            case ScrollAction scroll:
                var amount = scroll.UseInputValue ? trigger.Magnitude : scroll.Amount;
                _sink.Scroll(scroll.Axis, amount);
                break;

            case SwitchProfileAction switchProfile:
                ProfileSwitchRequested?.Invoke(this, switchProfile);
                break;

            case NoneAction:
                // Reached only if a non-block binding contains a None; treat as no-op.
                break;
        }
    }

    private void ExecuteKey(KeyAction key, TriggerResult trigger)
    {
        if (key.Hold)
        {
            // Hold maps NoteOn->down, NoteOff->up via the trigger phase.
            switch (trigger.Phase)
            {
                case TriggerPhase.Press:
                    _sink.KeyDown(key.Keys);
                    break;
                case TriggerPhase.Release:
                    _sink.KeyUp(key.Keys);
                    break;
            }
        }
        else if (trigger.Phase is TriggerPhase.Press or TriggerPhase.Change)
        {
            _sink.KeyTap(key.Keys);
        }
    }

    private void ExecuteMove(CursorMoveAction move, TriggerResult trigger)
    {
        if (move.UseInputValue)
        {
            // Value-driven moves apply the magnitude to the horizontal axis by default.
            // Per-axis mapping is refined alongside the editor UI (M8).
            _sink.MoveCursor(move.Mode, trigger.Magnitude, 0);
        }
        else
        {
            _sink.MoveCursor(move.Mode, move.Dx, move.Dy);
        }
    }
}
