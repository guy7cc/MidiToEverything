using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MidiToEverything.Core.Domain;
using MidiToEverything.Infrastructure.Midi;

namespace MidiToEverything.Infrastructure.Tests;

/// <summary>
/// Verifies DryWetMIDI event -> MidiMessage normalization without any hardware: the events
/// are plain value objects that can be constructed directly.
/// </summary>
public class MidiMessageFactoryTests
{
    private static MidiMessage Map(MidiEvent ev)
    {
        Assert.True(MidiMessageFactory.TryCreate(ev, "dev", out var message));
        return message;
    }

    [Fact]
    public void NoteOn_MapsWithChannelOffsetByOne()
    {
        var ev = new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100) { Channel = (FourBitNumber)0 };

        var m = Map(ev);

        Assert.Equal(MidiMessageType.NoteOn, m.Type);
        Assert.Equal(1, m.Channel);          // DryWetMIDI 0 -> engine 1
        Assert.Equal(60, m.Number);
        Assert.Equal(100, m.Value);
        Assert.Equal("dev", m.Device);
    }

    [Fact]
    public void NoteOn_WithZeroVelocity_BecomesNoteOff()
    {
        var ev = new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)0) { Channel = (FourBitNumber)3 };

        var m = Map(ev);

        Assert.Equal(MidiMessageType.NoteOff, m.Type);
        Assert.Equal(4, m.Channel);
        Assert.Equal(0, m.Value);
    }

    [Fact]
    public void NoteOff_Maps()
    {
        var ev = new NoteOffEvent((SevenBitNumber)40, (SevenBitNumber)64) { Channel = (FourBitNumber)15 };

        var m = Map(ev);

        Assert.Equal(MidiMessageType.NoteOff, m.Type);
        Assert.Equal(16, m.Channel);
        Assert.Equal(40, m.Number);
    }

    [Fact]
    public void ControlChange_Maps()
    {
        var ev = new ControlChangeEvent((SevenBitNumber)74, (SevenBitNumber)64) { Channel = (FourBitNumber)0 };

        var m = Map(ev);

        Assert.Equal(MidiMessageType.ControlChange, m.Type);
        Assert.Equal(74, m.Number);
        Assert.Equal(64, m.Value);
    }

    [Fact]
    public void PitchBend_MapsWithNullNumberAnd14BitValue()
    {
        var ev = new PitchBendEvent(8192) { Channel = (FourBitNumber)0 };

        var m = Map(ev);

        Assert.Equal(MidiMessageType.PitchBend, m.Type);
        Assert.Null(m.Number);
        Assert.Equal(8192, m.Value);
    }

    [Fact]
    public void ProgramChange_Maps()
    {
        var ev = new ProgramChangeEvent((SevenBitNumber)5) { Channel = (FourBitNumber)0 };

        var m = Map(ev);

        Assert.Equal(MidiMessageType.ProgramChange, m.Type);
        Assert.Equal(5, m.Number);
    }

    [Fact]
    public void UnsupportedEvent_ReturnsFalse()
    {
        Assert.False(MidiMessageFactory.TryCreate(new TextEvent("hello"), "dev", out _));
    }
}
