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

    /// <summary>Opt-in for launch/command actions running external programs (docs/05 §6, Q5).</summary>
    public bool AllowExternalLaunch { get; init; }

    /// <summary>UI language code ("ja" / "en"). docs/07.</summary>
    public string Language { get; init; } = "ja";

    /// <summary>Check GitHub Releases for a newer version on startup and periodically.</summary>
    public bool AutoUpdate { get; init; } = true;

    /// <summary>Start hidden in the tray instead of showing the main window.</summary>
    public bool StartMinimized { get; init; }

    /// <summary>Close button hides to the tray (true) or exits the app (false).</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>Whether action emission (the safety gate) is enabled at startup.</summary>
    public bool StartEmissionEnabled { get; init; } = true;

    /// <summary>Detect MIDI devices by periodic polling (true) or only on manual rescan (false).</summary>
    public bool AutoDetectDevices { get; init; } = true;

    /// <summary>obs-websocket connection for OBS actions (docs/05 §5, Phase 3).</summary>
    public string ObsHost { get; init; } = "localhost";
    public int ObsPort { get; init; } = 4455;
    public string ObsPassword { get; init; } = "";

    /// <summary>Devices to watch; "*" means all.</summary>
    public IReadOnlyList<string> WatchedDevices { get; init; } = new[] { "*" };

    /// <summary>Serilog minimum level ("Verbose"/"Debug"/"Information"/"Warning"/"Error"/"Fatal").</summary>
    public string LogLevel { get; init; } = "Debug";

    /// <summary>Number of daily rolling log files to keep.</summary>
    public int LogRetentionDays { get; init; } = 7;

    /// <summary>Relaunch the app automatically after an unhandled crash.</summary>
    public bool CrashAutoRestart { get; init; } = true;

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
