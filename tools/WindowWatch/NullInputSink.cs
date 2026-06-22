using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Tools.WindowWatch;

/// <summary>No-op sink: this tool demonstrates profile switching, not key emission.</summary>
internal sealed class NullInputSink : IInputSink
{
    public void KeyTap(IReadOnlyList<string> keys) { }
    public void KeyDown(IReadOnlyList<string> keys) { }
    public void KeyUp(IReadOnlyList<string> keys) { }
    public void MouseClick(MouseButton button, bool doubleClick) { }
    public void MoveCursor(MoveMode mode, double dx, double dy) { }
    public void Scroll(ScrollAxis axis, double amount) { }
}
