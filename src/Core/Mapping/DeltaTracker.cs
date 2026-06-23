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
        var key = new ControlKey(message.Device, message.Channel, message.Type, message.Number);
        var hasPrev = _last.TryGetValue(key, out var prev);
        _last[key] = message.Value;

        // The first value only establishes a baseline; we can't know a delta yet.
        if (!hasPrev)
        {
            return TriggerResult.None;
        }

        var delta = message.Value - prev;
        if (trigger.Wrap)
        {
            // A jump bigger than half the 7-bit range is read as a wrap of an endless knob.
            if (delta > 64)
            {
                delta -= 128;
            }
            else if (delta < -64)
            {
                delta += 128;
            }
        }

        return TriggerEvaluator.RelativeResult(trigger, delta);
    }

    /// <summary>Forget all tracked baselines (e.g. on profile reload).</summary>
    public void Reset() => _last.Clear();
}
