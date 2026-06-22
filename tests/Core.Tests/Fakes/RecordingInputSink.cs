using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Tests.Fakes;

/// <summary>A recorded call to <see cref="IInputSink"/>.</summary>
public abstract record SinkCall;
public sealed record KeyTapCall(IReadOnlyList<string> Keys) : SinkCall;
public sealed record KeyDownCall(IReadOnlyList<string> Keys) : SinkCall;
public sealed record KeyUpCall(IReadOnlyList<string> Keys) : SinkCall;
public sealed record MouseClickCall(MouseButton Button, bool Double) : SinkCall;
public sealed record MoveCall(MoveMode Mode, double Dx, double Dy) : SinkCall;
public sealed record ScrollCall(ScrollAxis Axis, double Amount) : SinkCall;
public sealed record MediaKeyCall(MediaKey Key) : SinkCall;
public sealed record TypeTextCall(string Text) : SinkCall;

/// <summary>
/// Test double for <see cref="IInputSink"/> that records calls instead of touching the OS,
/// with an awaitable signal so async-pipeline tests can wait for a target count without sleeping.
/// </summary>
public sealed class RecordingInputSink : IInputSink
{
    private readonly object _gate = new();
    private readonly List<SinkCall> _calls = new();
    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<SinkCall> Calls
    {
        get { lock (_gate) { return _calls.ToArray(); } }
    }

    public void KeyTap(IReadOnlyList<string> keys) => Record(new KeyTapCall(keys));
    public void KeyDown(IReadOnlyList<string> keys) => Record(new KeyDownCall(keys));
    public void KeyUp(IReadOnlyList<string> keys) => Record(new KeyUpCall(keys));
    public void MouseClick(MouseButton button, bool doubleClick) => Record(new MouseClickCall(button, doubleClick));
    public void MoveCursor(MoveMode mode, double dx, double dy) => Record(new MoveCall(mode, dx, dy));
    public void Scroll(ScrollAxis axis, double amount) => Record(new ScrollCall(axis, amount));
    public void SendMediaKey(MediaKey key) => Record(new MediaKeyCall(key));
    public void TypeText(string text) => Record(new TypeTextCall(text));

    /// <summary>Waits until at least <paramref name="count"/> calls are recorded, or throws on timeout.</summary>
    public async Task WaitForCountAsync(int count, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (true)
        {
            Task signal;
            lock (_gate)
            {
                if (_calls.Count >= count)
                {
                    return;
                }

                signal = _signal.Task;
            }

            var completed = await Task.WhenAny(signal, Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);
            if (completed != signal)
            {
                throw new TimeoutException($"Expected {count} sink calls within {timeout}, got {Calls.Count}.");
            }
        }
    }

    /// <summary>Lets a test assert "nothing was emitted" after a quiet period.</summary>
    public async Task AssertNoCallsAsync(TimeSpan window)
    {
        await Task.Delay(window).ConfigureAwait(false);
        if (Calls.Count != 0)
        {
            throw new InvalidOperationException($"Expected no sink calls, got {Calls.Count}.");
        }
    }

    private void Record(SinkCall call)
    {
        lock (_gate)
        {
            _calls.Add(call);
            var previous = _signal;
            _signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            previous.SetResult();
        }
    }
}
