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

    /// <summary>Continuous change carrying a magnitude (absolute/relative).</summary>
    Change,
}

/// <summary>
/// Outcome of interpreting one <see cref="MidiMessage"/> through a <see cref="Trigger"/>.
/// </summary>
/// <param name="Phase">What kind of emission (if any) should happen.</param>
/// <param name="Magnitude">
/// Signed amount for Change phases (post scale/invert): a normalized 0..1 for Absolute,
/// or a signed delta for Relative. 0 for Press/Release/None.
/// </param>
public readonly record struct TriggerResult(TriggerPhase Phase, double Magnitude)
{
    public bool ShouldFire => Phase != TriggerPhase.None;

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
        var delta = DecodeRelative(t.RelativeFormat, m.Value);
        if (Math.Abs(delta) <= t.Deadzone)
        {
            return TriggerResult.None;
        }

        double magnitude = delta * t.Scale;
        if (t.Invert)
        {
            magnitude = -magnitude;
        }

        return new TriggerResult(TriggerPhase.Change, magnitude);
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
