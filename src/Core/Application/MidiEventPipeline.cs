using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>
/// The hot path (docs/02_Architecture.md §2/§4): MIDI messages arrive on a callback thread
/// and are enqueued into a bounded <see cref="Channel{T}"/>; a single long-lived worker
/// (single-writer to the sink, preserving order) resolves each message against the current
/// profile layers, evaluates its trigger, and emits via <see cref="ActionExecutor"/>.
/// The callback thread does no heavy work — it only timestamps and enqueues.
/// </summary>
public sealed class MidiEventPipeline : IAsyncDisposable
{
    private readonly IMidiSource _source;
    private readonly IMappingContext _context;
    private readonly FiringEvaluator _firing;
    private readonly ActionExecutor _executor;
    private readonly ILogger<MidiEventPipeline> _logger;
    private readonly Channel<Envelope> _channel;

    private CancellationTokenSource? _cts;
    private Task? _worker;

    public MidiEventPipeline(
        IMidiSource source,
        IMappingContext context,
        ActionExecutor executor,
        MappingResolver? resolver = null,
        ILogger<MidiEventPipeline>? logger = null,
        int capacity = 4096)
    {
        _source = source;
        _context = context;
        _executor = executor;
        _firing = new FiringEvaluator(resolver);
        _logger = logger ?? NullLogger<MidiEventPipeline>.Instance;
        _channel = Channel.CreateBounded<Envelope>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
    }

    /// <summary>Forwards profile-switch requests from bindings (FR-5.4).</summary>
    public event EventHandler<SwitchProfileAction>? ProfileSwitchRequested
    {
        add => _executor.ProfileSwitchRequested += value;
        remove => _executor.ProfileSwitchRequested -= value;
    }

    public void Start()
    {
        if (_worker is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _source.MessageReceived += OnMessageReceived;
        _worker = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_worker is null)
        {
            return;
        }

        _source.MessageReceived -= OnMessageReceived;
        _channel.Writer.TryComplete();
        _cts!.Cancel();

        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }

        _worker = null;
        _cts.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    // Runs on the MIDI callback thread: timestamp and enqueue only (no heavy work).
    private void OnMessageReceived(object? sender, MidiMessage message)
        => _channel.Writer.TryWrite(new Envelope(message, Stopwatch.GetTimestamp()));

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await foreach (var envelope in _channel.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                Process(envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    private void Process(Envelope envelope)
    {
        var message = envelope.Message;

        // FiringEvaluator applies matching + trigger + AbsoluteDelta + edge, returning every binding
        // that actually fires (one control can drive several, e.g. a relative knob's increase and
        // decrease split across two bindings). The pipeline just executes each.
        var firings = _firing.Evaluate(message, _context.Current);
        foreach (var firing in firings)
        {
            _executor.Execute(firing.Binding, firing.Trigger, message);
        }

        if (firings.Count > 0 && _logger.IsEnabled(LogLevel.Trace))
        {
            var micros = (Stopwatch.GetTimestamp() - envelope.Timestamp) * 1_000_000.0 / Stopwatch.Frequency;
            _logger.LogTrace("Emitted {Count} action(s) in {Micros:F1}us", firings.Count, micros);
        }
    }

    /// <summary>A message paired with its receive timestamp for latency measurement.</summary>
    private readonly record struct Envelope(MidiMessage Message, long Timestamp);
}
