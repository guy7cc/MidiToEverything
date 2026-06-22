using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>Result category of resolving a message against the profile layers.</summary>
public enum ResolutionOutcome
{
    /// <summary>No layer defined a binding for the message — emit nothing.</summary>
    NoMatch,

    /// <summary>A layer matched with an explicit block (NoneAction) — emit nothing, no fallback.</summary>
    Blocked,

    /// <summary>A binding was resolved and should be emitted.</summary>
    Resolved,
}

/// <summary>
/// Outcome of <see cref="MappingResolver.Resolve"/>: which binding (if any) wins and where
/// it came from. <see cref="Outcome"/> distinguishes "nothing matched" from "explicitly
/// blocked" for diagnostics and UI (FR-6.5), though both emit no action.
/// </summary>
/// <param name="Outcome">Resolution category.</param>
/// <param name="Binding">The winning binding when <see cref="ResolutionOutcome.Resolved"/>.</param>
/// <param name="SourceProfileId">Id of the layer that decided the outcome, if any.</param>
public readonly record struct MappingResolution(
    ResolutionOutcome Outcome,
    Binding? Binding,
    string? SourceProfileId)
{
    public static readonly MappingResolution NoMatch =
        new(ResolutionOutcome.NoMatch, null, null);

    public static MappingResolution Blocked(string profileId) =>
        new(ResolutionOutcome.Blocked, null, profileId);

    public static MappingResolution Resolved(Binding binding, string profileId) =>
        new(ResolutionOutcome.Resolved, binding, profileId);

    public bool ShouldEmit => Outcome == ResolutionOutcome.Resolved;
}
