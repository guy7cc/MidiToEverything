using System.Text.RegularExpressions;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application;

/// <summary>Outcome of folding a process into a profile's match pattern.</summary>
public enum AddProcessStatus
{
    /// <summary>The process clause was added (OR-merged) into the pattern.</summary>
    Added,

    /// <summary>The pattern already contained this process clause; nothing changed.</summary>
    AlreadyPresent,

    /// <summary>No process name was provided.</summary>
    EmptyName,

    /// <summary>The merged result was not a valid regex (e.g. the existing pattern was broken).</summary>
    ReconstructionFailed,
}

/// <summary>Result of <see cref="MatchPatternBuilder.AddProcess"/>.</summary>
public readonly record struct AddProcessResult(AddProcessStatus Status, string Pattern)
{
    public bool Changed => Status == AddProcessStatus.Added;
}

/// <summary>
/// Builds and extends the unified match regex (docs/03_ProfileSchema.md §4). Adding a process
/// OR-merges a clause that anchors the process name to the first line of the match target
/// (<c>^name$</c> in multiline mode), so the profile matches that app on any future launch
/// regardless of its window title. Pure and unit-tested.
/// </summary>
public static class MatchPatternBuilder
{
    /// <summary>A clause matching the process-name line of the match target.</summary>
    public static string ProcessClause(string processName) => "^" + Regex.Escape(processName.Trim()) + "$";

    /// <summary>
    /// Returns a pattern that also matches <paramref name="processName"/>, OR-merged into
    /// <paramref name="existingPattern"/>. Reports failure (without throwing) when the name is
    /// empty or the merged result would be an invalid regex.
    /// </summary>
    public static AddProcessResult AddProcess(string? existingPattern, string? processName)
    {
        var name = (processName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return new AddProcessResult(AddProcessStatus.EmptyName, existingPattern ?? string.Empty);
        }

        var existing = (existingPattern ?? string.Empty).Trim();
        var clause = ProcessClause(name);

        if (existing.Contains(clause, StringComparison.Ordinal))
        {
            return new AddProcessResult(AddProcessStatus.AlreadyPresent, existing);
        }

        var candidate = existing.Length == 0 ? clause : $"(?:{existing})|(?:{clause})";
        if (!IsValidRegex(candidate))
        {
            return new AddProcessResult(AddProcessStatus.ReconstructionFailed, existing);
        }

        return new AddProcessResult(AddProcessStatus.Added, candidate);
    }

    private static bool IsValidRegex(string pattern)
    {
        try
        {
            _ = Regex.Match(string.Empty, pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
