using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Tools.KeyTest;

/// <summary>
/// Decorates an <see cref="IInputSink"/> to echo each emitted action to the console before
/// forwarding it, so the user can see what is being sent alongside its effect in the target window.
/// </summary>
internal sealed class LoggingInputSink : IInputSink
{
    private readonly IInputSink _inner;
    private readonly Action<string> _echo;

    public LoggingInputSink(IInputSink inner, Action<string> echo)
    {
        _inner = inner;
        _echo = echo;
    }

    public void KeyTap(IReadOnlyList<string> keys)
    {
        _echo($"key tap   {Chord(keys)}");
        _inner.KeyTap(keys);
    }

    public void KeyDown(IReadOnlyList<string> keys)
    {
        _echo($"key down  {Chord(keys)}");
        _inner.KeyDown(keys);
    }

    public void KeyUp(IReadOnlyList<string> keys)
    {
        _echo($"key up    {Chord(keys)}");
        _inner.KeyUp(keys);
    }

    public void MouseClick(MouseButton button, bool doubleClick)
    {
        _echo($"click     {button}{(doubleClick ? " x2" : "")}");
        _inner.MouseClick(button, doubleClick);
    }

    public void MoveCursor(MoveMode mode, double dx, double dy)
    {
        _echo($"move      {mode} {dx:F2},{dy:F2}");
        _inner.MoveCursor(mode, dx, dy);
    }

    public void Scroll(ScrollAxis axis, double amount)
    {
        _echo($"scroll    {axis} {amount:F0}");
        _inner.Scroll(axis, amount);
    }

    private static string Chord(IReadOnlyList<string> keys) => string.Join("+", keys);
}
