using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Handlers;

/// <summary>Runs a sequence of key chords with an optional delay between steps.</summary>
public sealed class MacroActionHandler : FireOnPressHandler
{
    private readonly IInputSink _sink;

    public MacroActionHandler(IInputSink sink) => _sink = sink;

    public override bool CanHandle(InputAction action) => action is MacroAction;

    protected override void Fire(InputAction action) => _ = RunAsync((MacroAction)action);

    private async Task RunAsync(MacroAction macro)
    {
        for (var i = 0; i < macro.Steps.Count; i++)
        {
            if (i > 0 && macro.StepDelayMs > 0)
            {
                await Task.Delay(macro.StepDelayMs).ConfigureAwait(false);
            }

            _sink.KeyTap(macro.Steps[i]);
        }
    }
}

/// <summary>
/// Alternates between two key chords on each press, tracking state in-process, and (optionally)
/// reflects the state on a controller LED via MIDI out — bidirectional feedback.
/// </summary>
public sealed class ToggleActionHandler : IActionHandler
{
    private readonly IInputSink _sink;
    private readonly IMidiOutput _midi;
    private readonly Dictionary<string, int> _state = new();
    private readonly object _lock = new();

    public ToggleActionHandler(IInputSink sink, IMidiOutput midi)
    {
        _sink = sink;
        _midi = midi;
    }

    public bool CanHandle(InputAction action) => action is ToggleAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        if (trigger.Phase != TriggerPhase.Press)
        {
            return; // toggle is a discrete, per-press action
        }

        var t = (ToggleAction)action;
        bool isA;
        var key = StateKey(t);
        lock (_lock)
        {
            var count = _state.GetValueOrDefault(key);
            isA = count % 2 == 0;
            _state[key] = count + 1;
        }

        _sink.KeyTap(isA ? t.KeysA : t.KeysB);

        if (!string.IsNullOrWhiteSpace(t.LedDevice))
        {
            // NoteOn velocity 127 = lit, 0 = off (common LED convention).
            _midi.Send(t.LedDevice, MidiOutKind.NoteOn, t.LedChannel, t.LedNote, isA ? 127 : 0);
        }
    }

    // Content-based key so the toggle state survives a config reload (records hold lists by reference).
    private static string StateKey(ToggleAction t) =>
        $"{string.Join('+', t.KeysA)}|{string.Join('+', t.KeysB)}|{t.LedDevice}|{t.LedChannel}|{t.LedNote}";
}
