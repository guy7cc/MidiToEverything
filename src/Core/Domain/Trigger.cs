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

    /// <summary>Inclusive input range [min,max] for Absolute mode.</summary>
    public int RangeMin { get; init; } = 0;
    public int RangeMax { get; init; } = 127;

    /// <summary>Dead zone applied around the range edges / center.</summary>
    public int Deadzone { get; init; } = 0;

    /// <summary>Invert the resulting magnitude.</summary>
    public bool Invert { get; init; } = false;

    /// <summary>Output sensitivity multiplier.</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>Signed encoding for Relative mode.</summary>
    public RelativeFormat RelativeFormat { get; init; } = RelativeFormat.TwosComplement;

    /// <summary>A plain edge trigger with default settings.</summary>
    public static Trigger Default { get; } = new();
}
