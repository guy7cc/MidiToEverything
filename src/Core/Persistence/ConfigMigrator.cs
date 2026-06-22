using System.Text.RegularExpressions;

namespace MidiToEverything.Core.Persistence;

/// <summary>
/// Upgrades a parsed <see cref="ConfigDto"/> to the current schema version (FR-7.5).
/// Each schema change adds one stepwise migration.
/// </summary>
internal static class ConfigMigrator
{
    public const int CurrentVersion = 2;

    /// <summary>
    /// Migrates the DTO in place to <see cref="CurrentVersion"/>.
    /// Throws if the file was written by a newer, unknown schema.
    /// </summary>
    public static ConfigDto Migrate(ConfigDto dto)
    {
        if (dto.Version > CurrentVersion)
        {
            throw new NotSupportedException(
                $"Config schema version {dto.Version} is newer than supported {CurrentVersion}. " +
                "Update the application.");
        }

        if (dto.Version < 2)
        {
            // v1 -> v2: fold processNames + titlePattern into a single match regex.
            MigrateMatch(dto.BaseProfile);
            foreach (var profile in dto.Profiles)
            {
                MigrateMatch(profile);
            }
        }

        dto.Version = CurrentVersion;
        return dto;
    }

    private static void MigrateMatch(ProfileDto profile)
    {
        var match = profile.Match;
        if (match is null || !string.IsNullOrEmpty(match.Pattern))
        {
            return;
        }

        var clauses = new List<string>();
        if (match.ProcessNames is { } names)
        {
            clauses.AddRange(names
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => "^" + Regex.Escape(n.Trim()) + "$"));
        }

        if (!string.IsNullOrWhiteSpace(match.TitlePattern))
        {
            clauses.Add(match.TitlePattern!);
        }

        match.Pattern = clauses.Count switch
        {
            0 => "",
            1 => clauses[0],
            _ => string.Join("|", clauses.Select(c => $"(?:{c})")),
        };
        match.ProcessNames = null;
        match.TitlePattern = null;
    }
}
