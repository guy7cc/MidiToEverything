using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Midi;

/// <summary>
/// <see cref="IMidiSource"/> backed by DryWetMIDI (docs/02_Architecture.md §3.3). Listens to
/// every accepted input device and normalizes events via <see cref="MidiMessageFactory"/>.
///
/// Hot-plug (FR-1.2) is driven by <b>periodic reconciliation</b>: a timer re-scans
/// <see cref="InputDevice.GetAll"/> and diffs it against the attached set, attaching new
/// devices and detaching vanished ones. Polling is used because DryWetMIDI's
/// <see cref="DevicesWatcher"/> does not reliably raise events for physical USB MIDI devices
/// on Windows (it needs WM_DEVICECHANGE delivery that is environment-dependent). The watcher
/// is still subscribed so environments where it works get instant response; polling is the
/// dependable fallback. Reconciliation is also what makes same-name reconnection automatic
/// (FR-1.4) and recovers input after a replug.
///
/// <see cref="MessageReceived"/> is raised on a DryWetMIDI callback thread; handlers must be cheap.
/// </summary>
public sealed class DryWetMidiSource : IMidiSource, IDisposable
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger<DryWetMidiSource> _logger;
    private readonly Func<string, bool> _accept;
    private readonly TimeSpan _pollInterval;
    private readonly object _gate = new();             // guards _attached
    private readonly object _reconcileGate = new();    // serializes reconcile passes
    private readonly Dictionary<string, InputDevice> _attached = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _pollTimer;
    private bool _started;

    public DryWetMidiSource(
        ILogger<DryWetMidiSource>? logger = null,
        IEnumerable<string>? watchedDevices = null,
        TimeSpan? pollInterval = null)
    {
        _logger = logger ?? NullLogger<DryWetMidiSource>.Instance;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        var patterns = (watchedDevices ?? new[] { "*" }).ToArray();
        _accept = name => patterns.Any(p => p == "*" ||
            string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
    }

    public event EventHandler<MidiMessage>? MessageReceived;
    public event EventHandler<MidiDeviceInfo>? DeviceConnected;
    public event EventHandler<MidiDeviceInfo>? DeviceDisconnected;

    public IReadOnlyList<MidiDeviceInfo> Devices
    {
        get
        {
            lock (_gate)
            {
                return _attached.Keys.Select(name => new MidiDeviceInfo(name, name)).ToArray();
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
        }

        DevicesWatcher.Instance.DeviceAdded += OnDevicesChanged;
        DevicesWatcher.Instance.DeviceRemoved += OnDevicesChanged;

        Reconcile();

        // Reliable fallback for systems where the watcher does not fire (see class remarks).
        _pollTimer = new Timer(_ => Reconcile(), null, _pollInterval, _pollInterval);
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
        }

        _pollTimer?.Dispose();
        _pollTimer = null;

        DevicesWatcher.Instance.DeviceAdded -= OnDevicesChanged;
        DevicesWatcher.Instance.DeviceRemoved -= OnDevicesChanged;

        foreach (var info in Devices)
        {
            Detach(info.Id);
        }
    }

    public void Dispose() => Stop();

    private void OnDevicesChanged(object? sender, DeviceAddedRemovedEventArgs e) => Reconcile();

    /// <summary>
    /// Diff current input devices against the attached set, attaching/detaching as needed.
    /// Serialized so overlapping watcher+timer passes never run concurrently; a pass that
    /// arrives while one is running is simply skipped (the next tick catches up).
    /// </summary>
    private void Reconcile()
    {
        if (!Monitor.TryEnter(_reconcileGate))
        {
            return;
        }

        try
        {
            if (!Volatile.Read(ref _started))
            {
                return;
            }

            // Snapshot the system's input devices by name (fresh handles we own).
            var current = new Dictionary<string, InputDevice>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in InputDevice.GetAll())
            {
                if (!current.TryAdd(device.Name, device))
                {
                    device.Dispose(); // duplicate name in this scan
                }
            }

            var connected = new List<string>();
            List<string> disconnected;

            lock (_gate)
            {
                disconnected = _attached.Keys.Where(name => !current.ContainsKey(name)).ToList();

                foreach (var (name, device) in current)
                {
                    if (_attached.ContainsKey(name) || !_accept(name))
                    {
                        device.Dispose(); // already attached, or filtered out
                        continue;
                    }

                    try
                    {
                        device.EventReceived += OnEventReceived;
                        device.StartEventsListening();
                        _attached[name] = device;
                        connected.Add(name);
                    }
                    catch (Exception ex)
                    {
                        // Common when a DAW already holds the device (PRD §6 known limitation).
                        _logger.LogWarning(ex, "Could not listen to MIDI device {Name}", name);
                        device.Dispose();
                    }
                }
            }

            foreach (var name in disconnected)
            {
                Detach(name);
            }

            foreach (var name in connected)
            {
                _logger.LogInformation("MIDI device attached: {Name}", name);
                DeviceConnected?.Invoke(this, new MidiDeviceInfo(name, name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MIDI device reconciliation failed");
        }
        finally
        {
            Monitor.Exit(_reconcileGate);
        }
    }

    private void Detach(string name)
    {
        InputDevice? device;
        lock (_gate)
        {
            _attached.Remove(name, out device);
        }

        if (device is null)
        {
            return;
        }

        try
        {
            device.EventReceived -= OnEventReceived;
            device.StopEventsListening();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while detaching {Name}", name);
        }
        finally
        {
            device.Dispose();
        }

        _logger.LogInformation("MIDI device detached: {Name}", name);
        DeviceDisconnected?.Invoke(this, new MidiDeviceInfo(name, name));
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        var name = (sender as InputDevice)?.Name ?? "unknown";

        if (MidiMessageFactory.TryCreate(e.Event, name, out var message))
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}
