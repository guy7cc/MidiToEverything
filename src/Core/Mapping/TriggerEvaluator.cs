using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>Phase of a trigger evaluation, driving how actions are emitted.</summary>
public enum TriggerPhase
{
    /// <summary>No emission (below threshold / inside dead zone).</summary>
    None,

    /// <summary>Discrete press (e.g. key down, click).</summary>
    Press,

    /// <summary>Release of a held press (key up).</summary>
    Release,

    /// <summary>Continuous change carrying a magnitude (absolute).</summary>
    Change,

    /// <summary>A relative (endless encoder) tick in the positive direction; magnitude &gt; 0.</summary>
    Increase,

    /// <summary>A relative (endless encoder) tick in the negative direction; magnitude &lt; 0.</summary>
    Decrease,
}

/// <summary>
/// Outcome of interpreting one <see cref="MidiMessage"/> through a <see cref="Trigger"/>.
/// </summary>
/// <param name="Phase">What kind of emission (if any) should happen.</param>
/// <param name="Magnitude">
/// Signed amount for value phases (post scale/invert): a normalized 0..1 for Absolute
/// (<see cref="TriggerPhase.Change"/>), or a signed delta for Relative
/// (<see cref="TriggerPhase.Increase"/>/<see cref="TriggerPhase.Decrease"/>). 0 for
/// Press/Release/None.
/// </param>
public readonly record struct TriggerResult(TriggerPhase Phase, double Magnitude)
{
    public bool ShouldFire => Phase != TriggerPhase.None;

    /// <summary>
    /// True for any value-carrying phase — absolute <see cref="TriggerPhase.Change"/> or a
    /// relative <see cref="TriggerPhase.Increase"/>/<see cref="TriggerPhase.Decrease"/> tick.
    /// Handlers that consume the magnitude (or fire once per change) use this rather than
    /// testing <see cref="TriggerPhase.Change"/> alone, so endless-encoder input drives them too.
    /// </summary>
    public bool IsChange => Phase is TriggerPhase.Change or TriggerPhase.Increase or TriggerPhase.Decrease;

    public static readonly TriggerResult None = new(TriggerPhase.None, 0);
}

/// <summary>
/// Pure, stateless interpretation of a raw MIDI value through a <see cref="Trigger"/>
/// (docs/02_Architecture.md §3.2, docs/03_ProfileSchema.md §2).
///
/// Note: edge detection for <see cref="TriggerMode.Trigger"/> on continuous controllers
/// (fire only when crossing the threshold) is a stateful concern handled by the worker
/// in a later milestone; here Trigger fires whenever value >= threshold.
/// </summary>
public static class TriggerEvaluator
{
    public static TriggerResult Evaluate(Trigger trigger, MidiMessage message)
    {
        return trigger.Mode switch
        {
            TriggerMode.Trigger => EvaluateTrigger(trigger, message),
            TriggerMode.Hold => EvaluateHold(trigger, message),
            TriggerMode.Absolute => EvaluateAbsolute(trigger, message),
            TriggerMode.Relative => EvaluateRelative(trigger, message),
            _ => TriggerResult.None,
        };
    }

    private static TriggerResult EvaluateTrigger(Trigger t, MidiMessage m)
        => m.Value >= t.Threshold ? new TriggerResult(TriggerPhase.Press, 0) : TriggerResult.None;

    private static TriggerResult EvaluateHold(Trigger t, MidiMessage m)
    {
        // NoteOff (or NoteOn with zero velocity, a common "note off" encoding) releases.
        var isRelease = m.Type == MidiMessageType.NoteOff ||
                        (m.Type == MidiMessageType.NoteOn && m.Value == 0);
        if (isRelease)
        {
            return new TriggerResult(TriggerPhase.Release, 0);
        }

        return m.Value >= t.Threshold
            ? new TriggerResult(TriggerPhase.Press, 0)
            : TriggerResult.None;
    }

    private static TriggerResult EvaluateAbsolute(Trigger t, MidiMessage m)
    {
        var min = Math.Min(t.RangeMin, t.RangeMax);
        var max = Math.Max(t.RangeMin, t.RangeMax);
        var span = max - min;
        if (span <= 0)
        {
            return TriggerResult.None;
        }

        // Gate mode fires only while the raw value sits inside the window; Clamp (default) keeps
        // the legacy behavior where out-of-range values snap to an edge and still fire.
        if (t.OutOfRange == OutOfRangeBehavior.Gate && (m.Value < min || m.Value > max))
        {
            return TriggerResult.None;
        }

        var clamped = Math.Clamp(m.Value, min, max);

        // Dead zone trims both ends of the usable span.
        if (t.Deadzone > 0)
        {
            if (clamped <= min + t.Deadzone || clamped >= max - t.Deadzone)
            {
                // Snap to the nearest edge rather than dropping the event entirely.
                clamped = clamped <= min + t.Deadzone ? min : max;
            }
        }

        var normalized = (double)(clamped - min) / span; // 0..1
        if (t.Invert)
        {
            normalized = 1.0 - normalized;
        }

        return new TriggerResult(TriggerPhase.Change, normalized * t.Scale);
    }

    private static TriggerResult EvaluateRelative(Trigger t, MidiMessage m)
    {
        // AbsoluteDelta is stateful (needs the previous value) and is handled by the pipeline's
        // DeltaTracker, not here. The encoder encodings are stateless and decoded directly.
        return t.RelativeFormat == RelativeFormat.AbsoluteDelta
            ? TriggerResult.None
            : RelativeResult(t, DecodeRelative(t.RelativeFormat, m.Value));
    }

    /// <summary>
    /// Shared relative post-processing: apply dead zone / scale / invert to a raw signed delta,
    /// then emit per <see cref="Trigger.RelativeOutput"/> — a signed Increase/Decrease amount, or a
    /// one-shot Press on the chosen direction. Used by both the stateless encoder formats and the
    /// stateful <see cref="DeltaTracker"/> (AbsoluteDelta).
    /// </summary>
    public static TriggerResult RelativeResult(Trigger t, int rawDelta)
    {
        if (Math.Abs(rawDelta) <= t.Deadzone)
        {
            return TriggerResult.None;
        }

        double magnitude = rawDelta * t.Scale;
        if (t.Invert)
        {
            magnitude = -magnitude;
        }

        var increasing = magnitude > 0;
        var decreasing = magnitude < 0;
        if (!increasing && !decreasing)
        {
            return TriggerResult.None; // exactly zero after scale (e.g. scale 0)
        }

        return t.RelativeOutput switch
        {
            RelativeOutput.FireOnIncrease => increasing ? new TriggerResult(TriggerPhase.Press, 0) : TriggerResult.None,
            RelativeOutput.FireOnDecrease => decreasing ? new TriggerResult(TriggerPhase.Press, 0) : TriggerResult.None,
            // Any non-zero change fires (zero was already filtered out above).
            RelativeOutput.FireOnEither => new TriggerResult(TriggerPhase.Press, 0),
            // Amount: signed magnitude carried as an Increase/Decrease tick.
            _ => new TriggerResult(increasing ? TriggerPhase.Increase : TriggerPhase.Decrease, magnitude),
        };
    }

    /// <summary>Decode a 7-bit value into a signed delta per the encoder format.</summary>
    public static int DecodeRelative(RelativeFormat format, int value) => format switch
    {
        // 1..63 => +1..+63, 65..127 => -63..-1, 0/64 => 0
        RelativeFormat.TwosComplement => value == 0 ? 0 : value < 64 ? value : value - 128,
        // bit 6 = sign, low 6 bits = magnitude
        RelativeFormat.SignedBit => (value & 0x40) != 0 ? -(value & 0x3F) : value & 0x3F,
        // 64 = no change
        RelativeFormat.BinaryOffset => value - 64,
        _ => 0,
    };
}
