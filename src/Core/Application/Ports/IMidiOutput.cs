using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Port over MIDI output (docs/05 §5, Phase 3). The device is resolved by a name regex; the
/// adapter caches the opened output. Channel is 1..16; data values are 0..127.
/// </summary>
public interface IMidiOutput
{
    void Send(string devicePattern, MidiOutKind kind, int channel, int data1, int data2);
}
