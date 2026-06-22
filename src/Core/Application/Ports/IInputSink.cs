using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Port over OS input emission (docs/02_Architecture.md §3.4). The Windows adapter uses
/// SendInput (scan codes by default); tests record calls. Kept low-level and OS-agnostic
/// so <see cref="ActionExecutor"/> owns the action-to-call translation.
/// </summary>
public interface IInputSink
{
    /// <summary>Press and immediately release a key chord (modifiers + key).</summary>
    void KeyTap(IReadOnlyList<string> keys);

    /// <summary>Press a chord and hold it down (paired with <see cref="KeyUp"/>).</summary>
    void KeyDown(IReadOnlyList<string> keys);

    /// <summary>Release a previously held chord.</summary>
    void KeyUp(IReadOnlyList<string> keys);

    void MouseClick(MouseButton button, bool doubleClick);

    /// <summary>
    /// Move the cursor. For <see cref="MoveMode.Relative"/>, dx/dy are pixel deltas; for
    /// <see cref="MoveMode.Absolute"/>, dx/dy are normalized 0..1 screen coordinates.
    /// </summary>
    void MoveCursor(MoveMode mode, double dx, double dy);

    /// <summary>Scroll the wheel; positive is up/right.</summary>
    void Scroll(ScrollAxis axis, double amount);

    /// <summary>Tap a media/transport key (play-pause, next, mute, ...).</summary>
    void SendMediaKey(MediaKey key);

    /// <summary>Type a literal Unicode string.</summary>
    void TypeText(string text);
}
