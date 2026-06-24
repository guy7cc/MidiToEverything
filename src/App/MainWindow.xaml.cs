using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MidiToEverything.App.ViewModels;

namespace MidiToEverything.App;

/// <summary>
/// Shell window: custom title bar, full-width header, device panel (with detection-mode toggle)
/// and an expanded input monitor (docs/04_Roadmap.md M7). Registers the global emergency-stop
/// hotkey (Ctrl+Alt+Pause) and keeps a borderless maximized window from covering the taskbar.
/// </summary>
public partial class MainWindow : Window
{
    private const int HotkeyId = 0xB001;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_PAUSE = 0x13;

    private readonly MainViewModel _viewModel;
    private readonly Func<ProfileEditorWindow> _editorFactory;
    private IntPtr _handle;

    public MainWindow(MainViewModel viewModel, Func<ProfileEditorWindow> editorFactory)
    {
        _viewModel = viewModel;
        _editorFactory = editorFactory;
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_handle)?.AddHook(WndProc);
        WindowChromeFix.Apply(this); // keep a maximized borderless window off the taskbar
        RegisterHotKey(_handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_PAUSE);
        UpdateMaxRestoreGlyph();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, HotkeyId);
            HwndSource.FromHwnd(_handle)?.RemoveHook(WndProc);
        }

        base.OnClosed(e);
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        var editor = _editorFactory();
        editor.Owner = this;
        editor.ShowDialog();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        // Reuse the same MainViewModel so changes persist exactly as the inline controls did.
        var settings = new SettingsWindow { Owner = this, DataContext = DataContext };
        settings.ShowDialog();
    }

    // ── Caption buttons ───────────────────────────────────────────────────────

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreGlyph()
        => MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";

    // ── Win32 ─────────────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _viewModel.ToggleEmissionCommand.Execute(null);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
