using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Input;

/// <summary>
/// Decorates an <see cref="IInputSink"/> with a global on/off gate used for the emergency stop
/// (safety requirement, docs/01_PRD.md §5). When <see cref="Enabled"/> is false all emission is
/// dropped; key releases are still forwarded so no key is left stuck down.
/// </summary>
public sealed class GatedInputSink : IInputSink
{
    private readonly IInputSink _inner;

    public GatedInputSink(IInputSink inner) => _inner = inner;

    /// <summary>When false, suppresses all input emission (emergency stop).</summary>
    public bool Enabled { get; set; } = true;

    public event EventHandler<bool>? EnabledChanged;

    public bool Toggle()
    {
        Enabled = !Enabled;
        EnabledChanged?.Invoke(this, Enabled);
        return Enabled;
    }

    public void KeyTap(IReadOnlyList<string> keys)
    {
        if (Enabled) _inner.KeyTap(keys);
    }

    public void KeyDown(IReadOnlyList<string> keys)
    {
        if (Enabled) _inner.KeyDown(keys);
    }

    // Always forward releases so a held key is never stranded when the gate closes.
    public void KeyUp(IReadOnlyList<string> keys) => _inner.KeyUp(keys);

    public void MouseClick(MouseButton button, bool doubleClick)
    {
        if (Enabled) _inner.MouseClick(button, doubleClick);
    }

    public void MoveCursor(MoveMode mode, double dx, double dy)
    {
        if (Enabled) _inner.MoveCursor(mode, dx, dy);
    }

    public void Scroll(ScrollAxis axis, double amount)
    {
        if (Enabled) _inner.Scroll(axis, amount);
    }
}
