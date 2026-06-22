namespace MidiToEverything.Core.Domain;

/// <summary>
/// Associates a <see cref="Signal"/> pattern with one or more <see cref="InputAction"/>s,
/// interpreted through a <see cref="Trigger"/> (docs/03_ProfileSchema.md §4).
/// </summary>
public sealed record Binding
{
    public required Signal Signal { get; init; }

    public Trigger Trigger { get; init; } = Trigger.Default;

    public IReadOnlyList<InputAction> Actions { get; init; } = Array.Empty<InputAction>();

    public string? Label { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>
    /// True when this binding only blocks (its sole action is <see cref="NoneAction"/>).
    /// A blocking binding suppresses fallback to lower layers (FR-6.4).
    /// </summary>
    public bool IsBlock => Actions.Count == 1 && Actions[0] is NoneAction;
}
