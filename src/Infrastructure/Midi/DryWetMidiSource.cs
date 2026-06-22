using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Midi;

/// <summary>
/// <see cref="IMidiSource"/> backed by DryWetMIDI (docs/02_Architecture.md §3.3). Listens to
/// every accepted input device, normalizes events via <see cref="MidiMessageFactory"/>, and
/// uses <see cref="DevicesWatcher"/> for hot-plug add/remove (FR-1.2). Reconnection of a
/// same-named device re-attaches automatically because matching is name-based (FR-1.4).
///
/// <see cref="MessageReceived"/> is raised on a DryWetMIDI callback thread; handlers must be
/// cheap (the pipeline only enqueues).
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

        DevicesWatcher.Instance.DeviceAdded += OnDeviceAdded;
        DevicesWatcher.Instance.DeviceRemoved += OnDeviceRemoved;

        foreach (var device in InputDevice.GetAll())
        {
            TryAttach(device);
        }
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

        DevicesWatcher.Instance.DeviceAdded -= OnDeviceAdded;
        DevicesWatcher.Instance.DeviceRemoved -= OnDeviceRemoved;

        foreach (var info in Devices)
        {
            Detach(info.Id);
        }
    }

    public void Dispose() => Stop();

    private void OnDeviceAdded(object? sender, DeviceAddedRemovedEventArgs e)
    {
        if (e.Device is not InputDevice)
        {
            return; // outputs are not input sources
        }

        try
        {
            TryAttach(InputDevice.GetByName(e.Device.Name));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Device added but no usable input: {Name}", e.Device.Name);
        }
    }

    private void OnDeviceRemoved(object? sender, DeviceAddedRemovedEventArgs e)
    {
        if (e.Device is InputDevice)
        {
            Detach(e.Device.Name);
        }
    }

    private void TryAttach(InputDevice device)
    {
        var name = device.Name;
        var attached = false;

        lock (_gate)
        {
            if (_accept(name) && !_attached.ContainsKey(name))
            {
                try
                {
                    device.EventReceived += OnEventReceived;
                    device.StartEventsListening();
                    _attached[name] = device;
                    attached = true;
                }
                catch (Exception ex)
                {
                    // Common when a DAW already holds the device (PRD §6 known limitation).
                    _logger.LogWarning(ex, "Could not listen to MIDI device {Name}", name);
                }
            }
        }

        if (!attached)
        {
            device.Dispose();
            return;
        }

        _logger.LogInformation("MIDI device attached: {Name}", name);
        DeviceConnected?.Invoke(this, new MidiDeviceInfo(name, name));
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
        var device = sender as InputDevice;
        var name = device?.Name ?? "unknown";

        if (MidiMessageFactory.TryCreate(e.Event, name, out var message))
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}
