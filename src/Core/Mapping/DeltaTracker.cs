using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>
/// Turns an absolute controller (0..127) into a relative delta by tracking the change between
/// consecutive values per control — the stateful side of <see cref="TriggerMode.Relative"/> with
/// <see cref="RelativeFormat.AbsoluteDelta"/>. This lets a plain absolute knob/fader drive the same
/// relative actions as an endless encoder. The raw delta is then fed to the shared
/// <see cref="TriggerEvaluator.RelativeResult"/> (dead zone / scale / invert / output).
///
/// A bounded fader saturates at its ends — once pinned at 0 or 127 there is no further delta, so
/// it can't increment forever. For endless knobs that send absolute values and wrap
/// (…126, 127, 0, 1…), set <see cref="Trigger.Wrap"/> so a 127→0 step reads as +1 rather than −127.
///
/// Stateful and single-threaded (the pipeline worker); not thread-safe. State is keyed by physical
/// control, so it survives profile reloads and is bounded by the number of distinct controls seen.
/// </summary>
public sealed class DeltaTracker
{
    private readonly Dictionary<ControlKey, int> _last = new();

    public TriggerResult Evaluate(Trigger trigger, MidiMessage message)
    {
        var raw = Advance(message);
        return raw is { } delta
            ? TriggerEvaluator.RelativeResult(trigger, ApplyWrap(trigger, delta))
            : TriggerResult.None;
    }

    /// <summary>
    /// Advance the per-control baseline and return the raw signed delta (current − previous), or
    /// null on the first sample. Kept separate from <see cref="Evaluate"/> so several bindings on
    /// one control can share a single advance per message (each applying its own <see cref="Trigger.Wrap"/>),
    /// rather than consuming each other's delta.
    /// </summary>
    public int? Advance(MidiMessage message)
    {
        var key = new ControlKey(message.Device, message.Channel, message.Type, message.Number);
        var hasPrev = _last.TryGetValue(key, out var prev);
        _last[key] = message.Value;
        return hasPrev ? message.Value - prev : null;
    }

    /// <summary>Apply endless-knob wrap handling: a jump bigger than half the 7-bit range is a wrap.</summary>
    public static int ApplyWrap(Trigger trigger, int delta)
    {
        if (trigger.Wrap)
        {
            if (delta > 64)
            {
                delta -= 128;
            }
            else if (delta < -64)
            {
                delta += 128;
            }
        }

        return delta;
    }

    /// <summary>Forget all tracked baselines (e.g. on profile reload).</summary>
    public void Reset() => _last.Clear();
}
