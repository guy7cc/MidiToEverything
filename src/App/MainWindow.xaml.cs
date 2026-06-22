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
    private const int WM_GETMINMAXINFO = 0x0024;
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

    // ── Caption buttons ───────────────────────────────────────────────────────

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreGlyph()
        => MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "" : "";

    // ── Win32 ─────────────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_HOTKEY when wParam.ToInt32() == HotkeyId:
                _viewModel.ToggleEmissionCommand.Execute(null);
                handled = true;
                break;

            case WM_GETMINMAXINFO:
                ConstrainMaximizedSize(hwnd, lParam);
                break;
        }

        return IntPtr.Zero;
    }

    // Keep a borderless maximized window within the monitor's working area (not over the taskbar).
    private static void ConstrainMaximizedSize(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var work = info.rcWork;
        var full = info.rcMonitor;
        mmi.ptMaxPosition = new POINT { x = work.left - full.left, y = work.top - full.top };
        mmi.ptMaxSize = new POINT { x = work.right - work.left, y = work.bottom - work.top };
        mmi.ptMinTrackSize = new POINT { x = 640, y = 420 };
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
