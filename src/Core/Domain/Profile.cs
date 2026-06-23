using System.Text.RegularExpressions;

namespace MidiToEverything.Core.Domain;

/// <summary>
/// Condition for auto-selecting a profile based on the foreground window
/// (docs/03_ProfileSchema.md §4). The base profile has no match rule.
///
/// A single regex (<see cref="Pattern"/>) is matched against a two-line target,
/// <c>"{processName}\n{windowTitle}"</c>, evaluated in multiline mode. This lets one pattern
/// reference the process (e.g. <c>^chrome\.exe$</c> on the first line) and/or the title
/// (e.g. <c>Google Chrome$</c> on the second), so the process name and title discrimination
/// live in one editable expression.
/// </summary>
public sealed record MatchRule
{
    public const string Separator = "\n";

    /// <summary>Regex matched against "process\ntitle". Empty matches nothing.</summary>
    public string Pattern { get; init; } = "";

    /// <summary>Tie-break priority when several profiles match (higher wins).</summary>
    public int Priority { get; init; }

    /// <summary>Builds the multiline match target the pattern is evaluated against.</summary>
    public static string BuildTarget(string processName, string windowTitle)
        => processName + Separator + windowTitle;

    /// <summary>
    /// True when the foreground window satisfies this rule. An empty or invalid pattern never
    /// matches (the editor allows free editing, so a transiently invalid regex must not throw).
    /// </summary>
    public bool Matches(string processName, string windowTitle)
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(BuildTarget(processName, windowTitle), Pattern, RegexOptions.Multiline);
        }
        catch (ArgumentException)
        {
            return false; // invalid regex
        }
    }
}

/// <summary>
/// A named set of <see cref="Binding"/>s. Used both for the base (global) profile and
/// for context/manual profiles (docs/03_ProfileSchema.md §4).
/// </summary>
public sealed record Profile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Auto-switch rule. Null for the base profile.</summary>
    public MatchRule? Match { get; init; }

    public IReadOnlyList<Binding> Bindings { get; init; } = Array.Empty<Binding>();

    /// <summary>
    /// Returns the most specific enabled binding whose signal matches the message,
    /// or null when this profile defines nothing for it. Ties broken by declaration order.
    /// </summary>
    public Binding? FindBestMatch(MidiMessage message)
    {
        var matches = FindAllMatches(message);
        return matches.Count > 0 ? matches[0] : null;
    }

    /// <summary>
    /// Every enabled binding that matches the message at the highest specificity present (ties
    /// included, in declaration order). A more specific binding still shadows less specific ones,
    /// but equally specific bindings all match — so one control can drive several actions, e.g. a
    /// relative knob's increase and decrease split across two bindings (docs/03_ProfileSchema.md §6).
    /// </summary>
    public IReadOnlyList<Binding> FindAllMatches(MidiMessage message)
    {
        var bestScore = int.MinValue;
        var matches = new List<Binding>();

        foreach (var binding in Bindings)
        {
            if (!binding.Enabled || !binding.Signal.Matches(message))
            {
                continue;
            }

            var score = binding.Signal.Specificity;
            if (score > bestScore)
            {
                bestScore = score;
                matches.Clear();
                matches.Add(binding);
            }
            else if (score == bestScore)
            {
                matches.Add(binding);
            }
        }

        return matches;
    }
}
