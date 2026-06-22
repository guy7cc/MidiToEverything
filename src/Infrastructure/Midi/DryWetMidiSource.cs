using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Midi;

/// <summary>
/// <see cref="IMidiSource"/> backed by DryWetMIDI (docs/02_Architecture.md §3.3). Listens to
/// every accepted input device, normalizes events via <see cref="MidiMessageFactory"/>, and
/// uses <see cref="DevicesWatcher"/> for hot-plug (FR-1.2).
///
/// Hot-plug handling is reconciliation-based: each watcher event re-scans
/// <see cref="InputDevice.GetAll"/> and diffs it against the attached set. This avoids
/// depending on the runtime type of <c>e.Device</c> or reading its <c>Name</c> after removal
/// (which DryWetMIDI documents as throwing), and makes same-name reconnection automatic (FR-1.4).
///
/// NOTE: on Windows, DevicesWatcher delivers physical device changes via Win32
/// <c>WM_DEVICECHANGE</c>, so the host must run a message loop (the WPF Dispatcher in the app;
/// a Win32 message pump in the console monitor). Without a pump, initial enumeration and input
/// still work but add/remove events never fire.
///
/// <see cref="MessageReceived"/> is raised on a DryWetMIDI callback thread; handlers must be cheap.
/// </summary>
public sealed class DryWetMidiSource : IMidiSource, IDisposable
{
    private readonly ILogger<DryWetMidiSource> _logger;
    private readonly Func<string, bool> _accept;
    private readonly object _gate = new();
    private readonly Dictionary<string, InputDevice> _attached = new(StringComparer.OrdinalIgnoreCase);
    private bool _started;

    public DryWetMidiSource(
        ILogger<DryWetMidiSource>? logger = null,
        IEnumerable<string>? watchedDevices = null)
    {
        _logger = logger ?? NullLogger<DryWetMidiSource>.Instance;
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

        DevicesWatcher.Instance.DeviceAdded -= OnDevicesChanged;
        DevicesWatcher.Instance.DeviceRemoved -= OnDevicesChanged;

        foreach (var info in Devices)
        {
            Detach(info.Id);
        }
    }

    public void Dispose() => Stop();

    private void OnDevicesChanged(object? sender, DeviceAddedRemovedEventArgs e) => Reconcile();

    /// <summary>Diff current input devices against the attached set, attaching/detaching as needed.</summary>
    private void Reconcile()
    {
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
