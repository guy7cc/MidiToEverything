using System.Runtime.InteropServices;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Window;

/// <summary>Win32 adapter for <see cref="IWindowController"/> over the foreground window.</summary>
public sealed class Win32WindowController : IWindowController
{
    public void Apply(WindowOp op)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        switch (op)
        {
            case WindowOp.Minimize:
                ShowWindow(hwnd, SW_MINIMIZE);
                break;
            case WindowOp.Maximize:
                ShowWindow(hwnd, SW_MAXIMIZE);
                break;
            case WindowOp.Restore:
                ShowWindow(hwnd, SW_RESTORE);
                break;
            case WindowOp.Close:
                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                break;
            case WindowOp.ToggleTopMost:
                ToggleTopMost(hwnd);
                break;
        }
    }

    private static void ToggleTopMost(IntPtr hwnd)
    {
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        var isTopMost = (exStyle & WS_EX_TOPMOST) != 0;
        SetWindowPos(hwnd, isTopMost ? HWND_NOTOPMOST : HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // ── Win32 ─────────────────────────────────────────────────────────────────
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const uint WM_CLOSE = 0x0010;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x0008;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);
}
