using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application;

namespace MidiToEverything.Core.Persistence;

/// <summary>
/// JSON-file implementation of <see cref="IProfileRepository"/>. Stores a single
/// <c>config.json</c> under <c>%APPDATA%\MidiToEverything</c> by default and writes
/// atomically (temp file then replace) to avoid corruption on crash (docs/02_Architecture.md §3.5).
/// </summary>
public sealed class JsonProfileRepository : IProfileRepository
{
    private const string FileName = "config.json";

    private readonly ILogger<JsonProfileRepository> _logger;

    public JsonProfileRepository(string? baseDirectory = null, ILogger<JsonProfileRepository>? logger = null)
    {
        var dir = baseDirectory ?? DefaultDirectory();
        ConfigPath = Path.Combine(dir, FileName);
        _logger = logger ?? NullLogger<JsonProfileRepository>.Instance;
    }

    public string ConfigPath { get; }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException($"Config file not found: {ConfigPath}", ConfigPath);
        }

        var json = File.ReadAllText(ConfigPath);
        return ConfigSerializer.Deserialize(json);
    }

    public AppConfig LoadOrCreateDefault()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                return Load();
            }
            catch (Exception ex)
            {
                // A corrupt or partially-written config (bad hand-edit, crash mid-save, or a
                // file from a newer/unknown schema) must never brick startup. Preserve the bad
                // file for recovery, then fall back to a fresh default (FR-7.1 robustness).
                var backup = BackupCorruptFile();
                _logger.LogError(
                    ex,
                    "Config at {Path} could not be loaded; backed it up to {Backup} and recreating default.",
                    ConfigPath,
                    backup ?? "(backup failed)");
            }
        }
        else
        {
            _logger.LogInformation("No config at {Path}; creating default.", ConfigPath);
        }

        var config = DefaultConfig.Create();
        Save(config);
        return config;
    }

    /// <summary>
    /// Moves an unloadable config aside so the user can recover it, returning the backup path
    /// (or null if even the backup failed). Picks the first free <c>config.corrupt[.N].json</c>
    /// sibling so repeated failures don't clobber earlier copies.
    /// </summary>
    private string? BackupCorruptFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath) ?? ".";
            var backup = Path.Combine(dir, "config.corrupt.json");
            for (var i = 1; File.Exists(backup); i++)
            {
                backup = Path.Combine(dir, $"config.corrupt.{i}.json");
            }

            File.Move(ConfigPath, backup);
            return backup;
        }
        catch (Exception ex)
        {
            // Best effort: if we can't even rename it, Save() below will overwrite it. Log and move on.
            _logger.LogWarning(ex, "Failed to back up corrupt config at {Path}.", ConfigPath);
            return null;
        }
    }

    public void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = ConfigSerializer.Serialize(config);

        // Atomic-ish write: write to a sibling temp file, then move over the target.
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, ConfigPath, overwrite: true);

        _logger.LogDebug("Saved config to {Path}.", ConfigPath);
    }

    private static string DefaultDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MidiToEverything");
}
