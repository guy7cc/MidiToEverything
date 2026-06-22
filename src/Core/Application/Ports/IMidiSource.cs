using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>A MIDI input device known to the system.</summary>
/// <param name="Id">Stable identifier (name-based) used for matching/reconnection.</param>
/// <param name="Name">Human-readable device name.</param>
public sealed record MidiDeviceInfo(string Id, string Name);

/// <summary>
/// Port over the MIDI input subsystem (docs/02_Architecture.md §3.3). The Windows adapter
/// wraps DryWetMIDI; tests use a fake. Implementations raise <see cref="MessageReceived"/>
/// on a driver/callback thread, so handlers must be cheap and non-blocking.
/// </summary>
public interface IMidiSource
{
    /// <summary>Raised for every normalized incoming message (on a callback thread).</summary>
    event EventHandler<MidiMessage>? MessageReceived;

    /// <summary>Raised when a device is connected/hot-plugged (FR-1.2).</summary>
    event EventHandler<MidiDeviceInfo>? DeviceConnected;

    /// <summary>Raised when a device is disconnected (FR-1.2).</summary>
    event EventHandler<MidiDeviceInfo>? DeviceDisconnected;

    /// <summary>Currently connected devices.</summary>
    IReadOnlyList<MidiDeviceInfo> Devices { get; }

    /// <summary>Begin listening to devices and hot-plug events.</summary>
    void Start();

    /// <summary>Stop listening and release devices.</summary>
    void Stop();
}
