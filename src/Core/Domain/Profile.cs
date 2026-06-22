using System.Text.RegularExpressions;

namespace MidiToEverything.Core.Domain;

/// <summary>
/// Conditions for auto-selecting a profile based on the foreground window
/// (docs/03_ProfileSchema.md §4). The base profile has no match rule.
/// </summary>
public sealed record MatchRule
{
    /// <summary>Executable names (case-insensitive), e.g. "CLIPStudioPaint.exe".</summary>
    public IReadOnlyList<string> ProcessNames { get; init; } = Array.Empty<string>();

    /// <summary>Optional window-title regex.</summary>
    public string? TitlePattern { get; init; }

    /// <summary>Tie-break priority when several profiles match (higher wins).</summary>
    public int Priority { get; init; }

    /// <summary>True when the given foreground window satisfies this rule.</summary>
    public bool Matches(string processName, string windowTitle)
    {
        var processOk = ProcessNames.Count == 0 ||
            ProcessNames.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));
        if (!processOk)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(TitlePattern) &&
            !Regex.IsMatch(windowTitle, TitlePattern))
        {
            return false;
        }

        return true;
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
        Binding? best = null;
        var bestScore = int.MinValue;

        foreach (var binding in Bindings)
        {
            if (!binding.Enabled || !binding.Signal.Matches(message))
            {
                continue;
            }

            var score = binding.Signal.Specificity;
            if (score > bestScore)
            {
                best = binding;
                bestScore = score;
            }
        }

        return best;
    }
}
