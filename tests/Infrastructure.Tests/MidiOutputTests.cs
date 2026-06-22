using MidiToEverything.Core.Domain;
using MidiToEverything.Infrastructure.Midi;

namespace MidiToEverything.Infrastructure.Tests;

public class MidiOutputTests
{
    [Fact]
    public void Send_ToUnknownDevice_DoesNotThrow()
    {
        using var output = new DryWetMidiOutput();

        var ex = Record.Exception(() =>
            output.Send("no-such-midi-device-xyz", MidiOutKind.ControlChange, channel: 1, data1: 7, data2: 64));

        Assert.Null(ex);
    }

    [Fact]
    public void Send_WithInvalidRegex_DoesNotThrow()
    {
        using var output = new DryWetMidiOutput();

        var ex = Record.Exception(() =>
            output.Send("(", MidiOutKind.NoteOn, channel: 1, data1: 60, data2: 100));

        Assert.Null(ex);
    }
}
