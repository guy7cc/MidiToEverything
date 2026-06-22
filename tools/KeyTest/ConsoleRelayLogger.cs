using Microsoft.Extensions.Logging;

namespace MidiToEverything.Tools.KeyTest;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> routing warnings/errors (unknown key tokens, SendInput/UIPI
/// failures) to a console writer, without pulling in a full logging stack.
/// </summary>
internal sealed class ConsoleRelayLogger<T> : ILogger<T>
{
    private readonly Action<LogLevel, string> _sink;

    public ConsoleRelayLogger(Action<LogLevel, string> sink) => _sink = sink;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += " : " + exception.Message;
        }

        _sink(logLevel, message);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
