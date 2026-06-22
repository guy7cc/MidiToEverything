using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Tests.Domain;

public class SignalMatchTests
{
    private static MidiMessage NoteOn(int number, string device = "akai", int channel = 1)
        => new(device, channel, MidiMessageType.NoteOn, number, 100);

    [Fact]
    public void WildcardDeviceAndChannel_Match()
    {
        var signal = new Signal { Type = SignalKind.NoteOn, Number = 36 };

        Assert.True(signal.Matches(NoteOn(36, "anything", 7)));
    }

    [Fact]
    public void SpecificDevice_IsCaseInsensitive()
    {
        var signal = new Signal { Device = "AKAI", Type = SignalKind.NoteOn, Number = 36 };

        Assert.True(signal.Matches(NoteOn(36, "akai")));
        Assert.False(signal.Matches(NoteOn(36, "korg")));
    }

    [Fact]
    public void SpecificChannel_MustMatch()
    {
        var signal = new Signal { Channel = "2", Type = SignalKind.NoteOn, Number = 36 };

        Assert.True(signal.Matches(NoteOn(36, channel: 2)));
        Assert.False(signal.Matches(NoteOn(36, channel: 3)));
    }

    [Fact]
    public void NoteKind_MatchesBothOnAndOff()
    {
        var signal = new Signal { Type = SignalKind.Note, Number = 40 };

        Assert.True(signal.Matches(new MidiMessage("d", 1, MidiMessageType.NoteOn, 40, 100)));
        Assert.True(signal.Matches(new MidiMessage("d", 1, MidiMessageType.NoteOff, 40, 0)));
    }

    [Fact]
    public void CcKind_DoesNotMatchNote()
    {
        var signal = new Signal { Type = SignalKind.Cc, Number = 40 };

        Assert.False(signal.Matches(NoteOn(40)));
    }

    [Fact]
    public void PitchBend_IgnoresNumber()
    {
        var signal = new Signal { Type = SignalKind.PitchBend };

        Assert.True(signal.Matches(new MidiMessage("d", 1, MidiMessageType.PitchBend, null, 8192)));
    }

    [Fact]
    public void NullNumber_IsWildcardOverNumbers()
    {
        var signal = new Signal { Type = SignalKind.NoteOn, Number = null };

        Assert.True(signal.Matches(NoteOn(36)));
        Assert.True(signal.Matches(NoteOn(99)));
    }

    [Fact]
    public void Specificity_OrdersConcreteAboveWildcard()
    {
        var wildcard = new Signal { Type = SignalKind.Note, Number = null };
        var concrete = new Signal { Device = "akai", Channel = "1", Type = SignalKind.NoteOn, Number = 36 };

        Assert.True(concrete.Specificity > wildcard.Specificity);
    }

    [Fact]
    public void FindBestMatch_PrefersMoreSpecificBinding()
    {
        var broad = new Binding
        {
            Signal = new Signal { Type = SignalKind.Note, Number = null },
            Actions = new InputAction[] { new KeyAction(new[] { "a" }) },
        };
        var specific = new Binding
        {
            Signal = new Signal { Device = "akai", Channel = "1", Type = SignalKind.NoteOn, Number = 36 },
            Actions = new InputAction[] { new KeyAction(new[] { "b" }) },
        };
        var profile = new Profile
        {
            Id = "p", Name = "p",
            Bindings = new[] { broad, specific },
        };

        var best = profile.FindBestMatch(NoteOn(36));

        Assert.Same(specific, best);
    }
}
