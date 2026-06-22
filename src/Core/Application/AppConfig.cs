using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application;

/// <summary>
/// The full persisted application configuration (docs/03_ProfileSchema.md §5): the base
/// profile plus all context/manual profiles and global settings. This is the in-memory
/// domain aggregate; the on-disk JSON shape lives in the persistence layer.
/// </summary>
public sealed record AppConfig
{
    /// <summary>Schema version for migrations (FR-7.5).</summary>
    public int Version { get; init; } = 1;

    public AppSettings Settings { get; init; } = new();

    /// <summary>Always-on base/global profile (FR-6.1).</summary>
    public required Profile BaseProfile { get; init; }

    /// <summary>Context/manual profiles.</summary>
    public IReadOnlyList<Profile> Profiles { get; init; } = Array.Empty<Profile>();

    /// <summary>Optional persisted runtime state (current/pinned profile).</summary>
    public ActiveContextState? ActiveContext { get; init; }
}

/// <summary>Global, non-profile settings (docs/03_ProfileSchema.md §5 "settings").</summary>
public sealed record AppSettings
{
    public bool StartWithWindows { get; init; }

    public string? EmergencyStopHotkey { get; init; } = "ctrl+alt+pause";

    /// <summary>Devices to watch; "*" means all.</summary>
    public IReadOnlyList<string> WatchedDevices { get; init; } = new[] { "*" };

    public MonitorSettings Monitor { get; init; } = new();
}

/// <summary>Input-monitor UI tuning (docs/03_ProfileSchema.md §5 "monitor").</summary>
public sealed record MonitorSettings
{
    public int MaxLogLines { get; init; } = 500;

    public int UiThrottleMs { get; init; } = 30;
}

/// <summary>Optional persisted runtime selection (docs/03_ProfileSchema.md §5 "activeContext").</summary>
public sealed record ActiveContextState
{
    /// <summary>Manually pinned profile id, disabling auto-switch while set (FR-5.5).</summary>
    public string? PinnedProfileId { get; init; }

    public string? CurrentProfileId { get; init; }
}
