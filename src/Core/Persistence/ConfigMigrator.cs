namespace MidiToEverything.Core.Persistence;

/// <summary>
/// Upgrades a parsed <see cref="ConfigDto"/> to the current schema version (FR-7.5).
/// Today there is a single version; the stepwise structure is in place so future schema
/// changes add one case each without touching callers.
/// </summary>
internal static class ConfigMigrator
{
    public const int CurrentVersion = 1;

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

        // Stepwise migrations would run here, e.g.:
        //   if (dto.Version < 2) { /* v1 -> v2 */ dto.Version = 2; }

        dto.Version = CurrentVersion;
        return dto;
    }
}
