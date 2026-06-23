namespace MidiToEverything.Core.Domain;

/// <summary>
/// Describes how an incoming value is interpreted before driving a binding's actions
/// (docs/03_ProfileSchema.md §2). Pure data; evaluation lives in
/// <see cref="Mapping.TriggerEvaluator"/>.
/// </summary>
public sealed record Trigger
{
    public TriggerMode Mode { get; init; } = TriggerMode.Trigger;

    /// <summary>Minimum value (velocity/CC) to count as ON for Trigger/Hold.</summary>
    public int Threshold { get; init; } = 1;

    /// <summary>Inclusive input window [min,max] for Absolute mode (normalized to 0..1).</summary>
    public int RangeMin { get; init; } = 0;
    public int RangeMax { get; init; } = 127;

    /// <summary>
    /// What Absolute does outside <see cref="RangeMin"/>..<see cref="RangeMax"/>: clamp to the
    /// edge and keep firing (default), or gate — fire only while the value is inside the window.
    /// </summary>
    public OutOfRangeBehavior OutOfRange { get; init; } = OutOfRangeBehavior.Clamp;

    /// <summary>Dead zone applied around the range edges / center.</summary>
    public int Deadzone { get; init; } = 0;

    /// <summary>Invert the resulting magnitude.</summary>
    public bool Invert { get; init; } = false;

    /// <summary>Output sensitivity multiplier.</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>How a Relative trigger decodes the delta (encoder encoding, or AbsoluteDelta).</summary>
    public RelativeFormat RelativeFormat { get; init; } = RelativeFormat.TwosComplement;

    /// <summary>What a Relative trigger does with the delta: send as amount, or fire on a direction.</summary>
    public RelativeOutput RelativeOutput { get; init; } = RelativeOutput.Amount;

    /// <summary>
    /// Rising-edge mode for value-gated triggers (<see cref="TriggerMode.Trigger"/> threshold /
    /// <see cref="TriggerMode.Absolute"/> gate): fire once when the control enters its active
    /// zone and not again until it leaves and re-enters. Stateful, applied by the pipeline's
    /// <see cref="Mapping.EdgeGate"/>; ignored for Hold/Relative (already event-driven).
    /// </summary>
    public bool Edge { get; init; } = false;

    /// <summary>
    /// For Relative + <see cref="RelativeFormat.AbsoluteDelta"/>: treat a large jump as a wrap-around
    /// (e.g. 127→0 reads as +1) so an endless knob that sends absolute values keeps incrementing.
    /// Leave off for bounded faders, where a big sweep is a real large delta, not a wrap.
    /// </summary>
    public bool Wrap { get; init; } = false;

    /// <summary>A plain edge trigger with default settings.</summary>
    public static Trigger Default { get; } = new();
}
