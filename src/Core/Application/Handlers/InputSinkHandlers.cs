using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Handlers;

/// <summary>Sends a keyboard chord. <see cref="KeyAction.Hold"/> maps NoteOn→down, NoteOff→up.</summary>
public sealed class KeyActionHandler : IActionHandler
{
    private readonly IInputSink _sink;

    public KeyActionHandler(IInputSink sink) => _sink = sink;

    public bool CanHandle(InputAction action) => action is KeyAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        var key = (KeyAction)action;
        if (key.Hold)
        {
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
}

/// <summary>Emits a mouse click.</summary>
public sealed class MouseClickActionHandler : IActionHandler
{
    private readonly IInputSink _sink;

    public MouseClickActionHandler(IInputSink sink) => _sink = sink;

    public bool CanHandle(InputAction action) => action is MouseClickAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        var click = (MouseClickAction)action;
        _sink.MouseClick(click.Button, click.Double);
    }
}

/// <summary>Moves the cursor; value-driven moves apply the magnitude to the horizontal axis.</summary>
public sealed class CursorMoveActionHandler : IActionHandler
{
    private readonly IInputSink _sink;

    public CursorMoveActionHandler(IInputSink sink) => _sink = sink;

    public bool CanHandle(InputAction action) => action is CursorMoveAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        var move = (CursorMoveAction)action;
        if (move.UseInputValue)
        {
            _sink.MoveCursor(move.Mode, trigger.Magnitude, 0);
        }
        else
        {
            _sink.MoveCursor(move.Mode, move.Dx, move.Dy);
        }
    }
}

/// <summary>Scrolls the wheel; value-driven scrolls use the magnitude as the amount.</summary>
public sealed class ScrollActionHandler : IActionHandler
{
    private readonly IInputSink _sink;

    public ScrollActionHandler(IInputSink sink) => _sink = sink;

    public bool CanHandle(InputAction action) => action is ScrollAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        var scroll = (ScrollAction)action;
        var amount = scroll.UseInputValue ? trigger.Magnitude : scroll.Amount;
        _sink.Scroll(scroll.Axis, amount);
    }
}
