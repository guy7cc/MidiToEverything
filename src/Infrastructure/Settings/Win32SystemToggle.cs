using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Settings;

/// <summary>
/// Toggles Windows settings (docs/05 §5, Phase 2). Dark mode flips the per-user theme registry
/// values and broadcasts WM_SETTINGCHANGE so running apps re-read the theme.
/// </summary>
public sealed class Win32SystemToggle : ISystemToggle
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly ILogger<Win32SystemToggle> _logger;

    public Win32SystemToggle(ILogger<Win32SystemToggle>? logger = null)
        => _logger = logger ?? NullLogger<Win32SystemToggle>.Instance;

    public void Toggle(WindowsSetting setting)
    {
        try
        {
            switch (setting)
            {
                case WindowsSetting.DarkMode:
                    ToggleDarkMode();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Toggle {Setting} failed", setting);
        }
    }

    private static void ToggleDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(PersonalizeKey);

        var appsLight = key.GetValue("AppsUseLightTheme") is int v ? v : 1;
        var next = appsLight == 0 ? 1 : 0; // flip light <-> dark
        key.SetValue("AppsUseLightTheme", next, RegistryValueKind.DWord);
        key.SetValue("SystemUsesLightTheme", next, RegistryValueKind.DWord);

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet",
            SMTO_ABORTIFHUNG, 100, out _);
    }

    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);
}
