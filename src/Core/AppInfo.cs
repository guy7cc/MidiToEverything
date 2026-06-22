using System.Reflection;

namespace MidiToEverything.Core;

/// <summary>
/// Application-wide metadata, resolvable without any UI or OS dependency.
/// Acts as the first unit-testable surface for the M0 milestone
/// (see docs/04_Roadmap.md).
/// </summary>
public static class AppInfo
{
    /// <summary>Product display name.</summary>
    public const string Name = "MidiToEverything";

    /// <summary>Assembly version (e.g. "0.1.0").</summary>
    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Per-user data directory: %APPDATA%\MidiToEverything.</summary>
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Name);
}
