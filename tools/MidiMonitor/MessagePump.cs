using System.Runtime.InteropServices;

namespace MidiToEverything.Tools.MidiMonitor;

/// <summary>
/// Minimal Win32 message pump for the console monitor. DryWetMIDI's DevicesWatcher receives
/// physical hot-plug notifications via <c>WM_DEVICECHANGE</c>, which requires the thread that
/// started watching to pump messages. The WPF app gets this from its Dispatcher; here we pump
/// explicitly. <see cref="Run"/> blocks until <see cref="Quit"/> posts WM_QUIT.
/// </summary>
internal static class MessagePump
{
    private const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>Pumps the current thread's message queue until <see cref="Quit"/> is called.</summary>
    public static void Run()
    {
        while (true)
        {
            var result = GetMessage(out var msg, IntPtr.Zero, 0, 0);
            if (result == 0 || result == -1) // WM_QUIT or error
            {
                break;
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    /// <summary>Captures the calling thread's id so <see cref="Quit"/> can target its pump.</summary>
    public static uint CurrentThreadId() => GetCurrentThreadId();

    /// <summary>Posts WM_QUIT to the pump running on <paramref name="threadId"/>.</summary>
    public static void Quit(uint threadId) => PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
}
