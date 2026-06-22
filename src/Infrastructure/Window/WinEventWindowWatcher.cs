using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Window;

/// <summary>
/// <see cref="IWindowWatcher"/> using <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c>
/// (docs/02_Architecture.md §3.4). The hook is event-driven (no polling). With
/// WINEVENT_OUTOFCONTEXT the callback runs on the thread that installed the hook, so that
/// thread owns a dedicated Win32 message loop; <see cref="Stop"/> posts WM_QUIT to end it.
///
/// <see cref="ForegroundChanged"/> is raised on the watcher thread.
/// </summary>
public sealed class WinEventWindowWatcher : IWindowWatcher, IDisposable
{
    private readonly ILogger<WinEventWindowWatcher> _logger;
    private readonly ManualResetEventSlim _ready = new(false);

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private WinEventDelegate? _callback; // kept alive for the unmanaged hook

    public WinEventWindowWatcher(ILogger<WinEventWindowWatcher>? logger = null)
        => _logger = logger ?? NullLogger<WinEventWindowWatcher>.Instance;

    public event EventHandler<WindowContext>? ForegroundChanged;

    public WindowContext Current { get; private set; } = WindowContext.Unknown;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(ThreadProc) { IsBackground = true, Name = "WinEventWatcher" };
        _thread.Start();
        _ready.Wait();
    }

    public void Stop()
    {
        if (_thread is null)
        {
            return;
        }

        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose()
    {
        Stop();
        _ready.Dispose();
    }

    private void ThreadProc()
    {
        _threadId = GetCurrentThreadId();
        _callback = OnWinEvent;
        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (_hook == IntPtr.Zero)
        {
            _logger.LogError("SetWinEventHook failed; foreground monitoring is disabled.");
        }

        Update(GetForegroundWindow()); // seed Current before Start() returns
        _ready.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint eventThread, uint eventTime)
    {
        const int OBJID_WINDOW = 0;
        if (eventType == EVENT_SYSTEM_FOREGROUND && hwnd != IntPtr.Zero && idObject == OBJID_WINDOW)
        {
            Update(hwnd);
        }
    }

    private void Update(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var context = Resolve(hwnd);
        Current = context;
        ForegroundChanged?.Invoke(this, context);
    }

    private WindowContext Resolve(IntPtr hwnd)
    {
        var process = ResolveProcessName(hwnd);
        var title = ResolveTitle(hwnd);
        return new WindowContext(process, title);
    }

    private static string ResolveProcessName(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return string.Empty;
        }

        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size)
                ? Path.GetFileName(buffer.ToString())
                : string.Empty;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static string ResolveTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    // ── Win32 interop ─────────────────────────────────────────────────────────

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WM_QUIT = 0x0012;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

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
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

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

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
}
