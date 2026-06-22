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
            return Load();
        }

        _logger.LogInformation("No config at {Path}; creating default.", ConfigPath);
        var config = DefaultConfig.Create();
        Save(config);
        return config;
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
