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

    /// <summary>
    /// True when <paramref name="message"/> should drive this binding: a normal signal match, or —
    /// for a <see cref="TriggerMode.Hold"/> trigger on a NoteOn signal — the matching Note Off, so a
    /// held key is released on note-off even when the device sends an explicit Note Off (not NoteOn
    /// velocity 0). Without this a Hold "press-and-hold" binding would never see the release.
    /// </summary>
    public bool Matches(MidiMessage message)
        => Signal.Matches(message)
           || (Trigger.Mode == TriggerMode.Hold && Signal.MatchesNoteRelease(message));
}
