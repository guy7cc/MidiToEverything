using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Input;

/// <summary>
/// <see cref="IInputSink"/> implemented with Win32 <c>SendInput</c> (docs/02_Architecture.md §3.4).
/// Keys are sent as scan codes by default for broad app/game compatibility (FR-4.1); modifier
/// chords press in order and release in reverse. Unknown key tokens are skipped with a warning.
///
/// UIPI note: a non-elevated process cannot send input to elevated windows (PRD §6); callers
/// should surface that when emission appears to have no effect.
/// </summary>
public sealed class Win32InputSink : IInputSink
{
    private readonly ILogger<Win32InputSink> _logger;

    public Win32InputSink(ILogger<Win32InputSink>? logger = null)
        => _logger = logger ?? NullLogger<Win32InputSink>.Instance;

    public void KeyTap(IReadOnlyList<string> keys)
    {
        var (modifiers, mains) = Split(keys);

        foreach (var m in modifiers) SendKey(m, up: false);
        foreach (var k in mains) SendKey(k, up: false);
        for (var i = mains.Count - 1; i >= 0; i--) SendKey(mains[i], up: true);
        for (var i = modifiers.Count - 1; i >= 0; i--) SendKey(modifiers[i], up: true);
    }

    public void KeyDown(IReadOnlyList<string> keys)
    {
        var (modifiers, mains) = Split(keys);
        foreach (var m in modifiers) SendKey(m, up: false);
        foreach (var k in mains) SendKey(k, up: false);
    }

    public void KeyUp(IReadOnlyList<string> keys)
    {
        var (modifiers, mains) = Split(keys);
        for (var i = mains.Count - 1; i >= 0; i--) SendKey(mains[i], up: true);
        for (var i = modifiers.Count - 1; i >= 0; i--) SendKey(modifiers[i], up: true);
    }

    public void MouseClick(MouseButton button, bool doubleClick)
    {
        var (down, up) = button switch
        {
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
        };

        var clicks = doubleClick ? 2 : 1;
        for (var i = 0; i < clicks; i++)
        {
            SendMouse(down, 0, 0, 0);
            SendMouse(up, 0, 0, 0);
        }
    }

    public void MoveCursor(MoveMode mode, double dx, double dy)
    {
        if (mode == MoveMode.Absolute)
        {
            // dx/dy are normalized 0..1 across the virtual desktop.
            var x = (int)Math.Round(Math.Clamp(dx, 0, 1) * 65535);
            var y = (int)Math.Round(Math.Clamp(dy, 0, 1) * 65535);
            SendMouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, x, y, 0);
        }
        else
        {
            SendMouse(MOUSEEVENTF_MOVE, (int)Math.Round(dx), (int)Math.Round(dy), 0);
        }
    }

    public void Scroll(ScrollAxis axis, double amount)
    {
        // amount is in wheel-delta units (120 per notch).
        var delta = unchecked((uint)(int)Math.Round(amount));
        var flag = axis == ScrollAxis.Horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL;
        SendMouse(flag, 0, 0, delta);
    }

    public void SendMediaKey(MediaKey key)
    {
        var vk = key switch
        {
            MediaKey.PlayPause => VK_MEDIA_PLAY_PAUSE,
            MediaKey.Next => VK_MEDIA_NEXT_TRACK,
            MediaKey.Previous => VK_MEDIA_PREV_TRACK,
            MediaKey.Stop => VK_MEDIA_STOP,
            MediaKey.Mute => VK_VOLUME_MUTE,
            MediaKey.VolumeUp => VK_VOLUME_UP,
            MediaKey.VolumeDown => VK_VOLUME_DOWN,
            _ => (ushort)0,
        };
        if (vk == 0)
        {
            return;
        }

        SendVirtualKey(vk, up: false);
        SendVirtualKey(vk, up: true);
    }

    public void TypeText(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\r')
            {
                continue; // CR of a CRLF pair; the LF sends Enter
            }

            if (ch == '\n')
            {
                SendVirtualKey(VK_RETURN, up: false);
                SendVirtualKey(VK_RETURN, up: true);
            }
            else
            {
                SendUnicode(ch, up: false);
                SendUnicode(ch, up: true);
            }
        }
    }

    private void SendVirtualKey(ushort vk, bool up)
    {
        var flags = up ? KEYEVENTF_KEYUP : 0u;
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = { ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = flags } },
        };
        Send(input);
    }

    private void SendUnicode(char ch, bool up)
    {
        var flags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0u);
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = { ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = flags } },
        };
        Send(input);
    }

    private void SendKey(string token, bool up)
    {
        if (!KeyCodes.TryResolve(token, out var vk, out var extended))
        {
            _logger.LogWarning("Unknown key token '{Token}', skipped", token);
            return;
        }

        var scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        var flags = KEYEVENTF_SCANCODE;
        if (extended) flags |= KEYEVENTF_EXTENDEDKEY;
        if (up) flags |= KEYEVENTF_KEYUP;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = { ki = new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = flags } },
        };
        Send(input);
    }

    private void SendMouse(uint flags, int dx, int dy, uint mouseData)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = mouseData, dwFlags = flags } },
        };
        Send(input);
    }

    private void Send(INPUT input)
    {
        var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            _logger.LogWarning("SendInput failed (Win32 error {Error}). Possibly blocked by UIPI.",
                Marshal.GetLastWin32Error());
        }
    }

    private static (List<string> Modifiers, List<string> Mains) Split(IReadOnlyList<string> keys)
    {
        var modifiers = new List<string>();
        var mains = new List<string>();
        foreach (var key in keys)
        {
            (KeyCodes.IsModifier(key) ? modifiers : mains).Add(key);
        }

        return (modifiers, mains);
    }

    // ── Win32 interop ─────────────────────────────────────────────────────────

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_VOLUME_MUTE = 0xAD;
    private const ushort VK_VOLUME_DOWN = 0xAE;
    private const ushort VK_VOLUME_UP = 0xAF;
    private const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
    private const ushort VK_MEDIA_PREV_TRACK = 0xB1;
    private const ushort VK_MEDIA_STOP = 0xB2;
    private const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x1000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

    private const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
