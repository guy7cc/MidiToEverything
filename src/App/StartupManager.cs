using Microsoft.Win32;

namespace MidiToEverything.App;

/// <summary>
/// Toggles "launch at Windows startup" for the current user via the HKCU Run key
/// (no admin needed). This is the app-level control; the MSI installer offers a
/// separate all-users (HKLM) option. The app manages only its own HKCU entry.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MidiToEverything";

    /// <summary>True when an HKCU Run entry for this app exists.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    /// <summary>Adds or removes the HKCU Run entry pointing at the running executable.</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
