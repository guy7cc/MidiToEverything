namespace MidiToEverything.Core.Application.Ports;

/// <summary>The foreground window's identifying context (docs/02_Architecture.md §3.4).</summary>
/// <param name="ProcessName">Executable file name, e.g. "CLIPStudioPaint.exe".</param>
/// <param name="WindowTitle">Foreground window title.</param>
public sealed record WindowContext(string ProcessName, string WindowTitle)
{
    /// <summary>Placeholder used before any foreground change has been observed.</summary>
    public static readonly WindowContext Unknown = new(string.Empty, string.Empty);
}

/// <summary>
/// Port over foreground-window monitoring (docs/02_Architecture.md §3.4). The Windows
/// adapter uses SetWinEventHook; tests use a fake.
/// </summary>
public interface IWindowWatcher
{
    /// <summary>Raised when the foreground window changes (FR-5.1).</summary>
    event EventHandler<WindowContext>? ForegroundChanged;

    /// <summary>The most recently observed foreground context.</summary>
    WindowContext Current { get; }

    void Start();

    void Stop();
}
