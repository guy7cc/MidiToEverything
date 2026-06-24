using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MidiToEverything.App;

/// <summary>
/// Keeps a borderless (WindowChrome) maximized window within the monitor's working area so it
/// does not cover the taskbar. Shared by the main and editor windows. Call <see cref="Apply"/>
/// from <c>OnSourceInitialized</c> after the handle exists.
/// </summary>
internal static class WindowChromeFix
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(Hook);
        RoundCorners(handle);
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    /// <summary>Ask DWM to round the window corners (Windows 11+; a no-op on older OSes).</summary>
    private static void RoundCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var preference = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int GWL_STYLE = -16;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    /// <summary>
    /// Remove the maximize capability while keeping the resize frame (WS_THICKFRAME) — so a dialog can
    /// still be DWM-rounded and resized, but caption double-click / Win+Up won't maximize it.
    /// </summary>
    public static void DisableMaximize(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            Constrain(hwnd, lParam);
        }

        return IntPtr.Zero;
    }

    private static void Constrain(IntPtr hwnd, IntPtr lParam)
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
        mmi.ptMinTrackSize = new POINT { x = 480, y = 360 };
        Marshal.StructureToPtr(mmi, lParam, true);
    }

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
}
