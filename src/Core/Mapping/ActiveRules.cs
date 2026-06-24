using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>
/// The set of rules (profiles) active at a given moment: every rule whose match regex matches the
/// foreground window, plus the always-on base rule and any manually force-enabled rules.
///
/// Unlike the old layered model, there is no priority override between rules — for a given MIDI
/// message, the bindings of <em>all</em> active rules are evaluated and fire together (union). A
/// more specific binding still shadows a less specific one <em>within the same rule</em>
/// (see <see cref="Profile.FindAllMatches"/>); across rules they simply co-fire.
/// </summary>
public readonly record struct ActiveRules(IReadOnlyList<Profile> Rules)
{
    /// <summary>Convenience for a single always-on rule (e.g. just the base rule).</summary>
    public ActiveRules(Profile single) : this(new[] { single })
    {
    }
}
