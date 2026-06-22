using System.Text;
using System.Text.RegularExpressions;

namespace MidiToEverything.Core.Application;

/// <summary>
/// Suggests a robust <see cref="Domain.MatchRule.TitlePattern"/> regex from a sample window
/// title, so a profile keeps matching the same app on future launches even though the volatile
/// part of the title (document name, tab, etc.) changes.
///
/// Heuristic: Windows titles follow "document - App" (or "...| App"), so the trailing segment
/// is the stable app/brand name. We anchor that segment to the end of the title and tolerate a
/// trailing version number (e.g. "Obsidian 1.12.7" → matches "Obsidian 1.13.0"). Pure and
/// OS-free, so it is unit-tested.
/// </summary>
public static class TitlePatternSuggester
{
    private static readonly string[] Separators = { " - ", " — ", " – ", " | ", " · ", " : " };

    /// <summary>Returns a suggested title regex, or an empty string when no title is available.</summary>
    public static string Suggest(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var segment = StripVersion(LastSegment(title.Trim()));
        if (string.IsNullOrWhiteSpace(segment))
        {
            segment = title.Trim();
        }

        // Anchor the app name to the end, tolerating a trailing version like " 1.12.7" or " v2".
        return Escape(segment) + @"(?:\s+v?\d[\d.]*)?$";
    }

    private static string LastSegment(string title)
    {
        var parts = title.Split(Separators, StringSplitOptions.None);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var trimmed = parts[i].Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        return title;
    }

    private static string StripVersion(string segment)
        => Regex.Replace(segment, @"\s+v?\d+(?:\.\d+)*$", "").Trim();

    /// <summary>Escapes regex metacharacters but leaves spaces/letters literal for readability.</summary>
    private static string Escape(string value)
    {
        const string meta = @"\.^$|?*+()[]{}";
        var builder = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (meta.IndexOf(c) >= 0)
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
