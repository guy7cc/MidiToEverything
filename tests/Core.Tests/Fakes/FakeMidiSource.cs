using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Tests.Fakes;

/// <summary>Test double for <see cref="IMidiSource"/> that lets a test script MIDI input and hot-plug events.</summary>
public sealed class FakeMidiSource : IMidiSource
{
    private readonly List<MidiDeviceInfo> _devices = new();

    public event EventHandler<MidiMessage>? MessageReceived;
    public event EventHandler<MidiDeviceInfo>? DeviceConnected;
    public event EventHandler<MidiDeviceInfo>? DeviceDisconnected;

    public IReadOnlyList<MidiDeviceInfo> Devices => _devices;

    public bool Started { get; private set; }

    public void Start() => Started = true;

    public void Stop() => Started = false;

    public void Emit(MidiMessage message) => MessageReceived?.Invoke(this, message);

    public void Connect(MidiDeviceInfo device)
    {
        _devices.Add(device);
        DeviceConnected?.Invoke(this, device);
    }

    public void Disconnect(MidiDeviceInfo device)
    {
        _devices.Remove(device);
        DeviceDisconnected?.Invoke(this, device);
    }
}
