using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Core.Tests.Fakes;

/// <summary>Test double for <see cref="IWindowWatcher"/> that lets a test drive foreground changes.</summary>
public sealed class FakeWindowWatcher : IWindowWatcher
{
    public event EventHandler<WindowContext>? ForegroundChanged;

    public WindowContext Current { get; private set; } = WindowContext.Unknown;

    public bool Started { get; private set; }

    public void Start() => Started = true;

    public void Stop() => Started = false;

    public void SetForeground(string processName, string windowTitle = "")
    {
        Current = new WindowContext(processName, windowTitle);
        ForegroundChanged?.Invoke(this, Current);
    }
}
