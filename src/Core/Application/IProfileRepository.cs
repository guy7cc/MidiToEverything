namespace MidiToEverything.Core.Application;

/// <summary>
/// Port for loading and persisting the application configuration
/// (docs/02_Architecture.md §3.5). The JSON-on-disk adapter is
/// <c>JsonProfileRepository</c>; tests can substitute a fake.
/// </summary>
public interface IProfileRepository
{
    /// <summary>Absolute path of the backing config file.</summary>
    string ConfigPath { get; }

    /// <summary>Loads the config, throwing if it does not exist or is invalid.</summary>
    AppConfig Load();

    /// <summary>Loads the config, or creates and persists a default one on first run.</summary>
    AppConfig LoadOrCreateDefault();

    /// <summary>Persists the config atomically (temp file then replace).</summary>
    void Save(AppConfig config);
}
