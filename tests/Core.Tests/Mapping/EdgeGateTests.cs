using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Tests.Mapping;

public class EdgeGateTests
{
    private static MidiMessage Cc(int value, int number = 74)
        => new("d", 1, MidiMessageType.ControlChange, number, value);

    private static bool Emit(EdgeGate gate, Trigger trigger, MidiMessage m)
        => gate.ShouldEmit(trigger, m, TriggerEvaluator.Evaluate(trigger, m));

    [Fact]
    public void NonEdge_Trigger_FiresEveryTimeInZone()
    {
        var gate = new EdgeGate();
        var t = new Trigger { Mode = TriggerMode.Trigger, Threshold = 100 };

        Assert.True(Emit(gate, t, Cc(110)));
        Assert.True(Emit(gate, t, Cc(120))); // still firing, no suppression without Edge
    }

    [Fact]
    public void Edge_Trigger_FiresOnceUntilItLeavesAndReenters()
    {
        var gate = new EdgeGate();
        var t = new Trigger { Mode = TriggerMode.Trigger, Threshold = 100, Edge = true };

        Assert.True(Emit(gate, t, Cc(110)));  // rising edge → fire
        Assert.False(Emit(gate, t, Cc(120))); // still in zone → suppressed
        Assert.False(Emit(gate, t, Cc(90)));  // left the zone → no fire (and resets)
        Assert.True(Emit(gate, t, Cc(115)));  // re-entered → fire again
    }

    [Fact]
    public void Edge_AbsoluteGate_FiresOnceOnEntry()
    {
        var gate = new EdgeGate();
        var t = new Trigger
        {
            Mode = TriggerMode.Absolute,
            RangeMin = 100,
            RangeMax = 127,
            OutOfRange = OutOfRangeBehavior.Gate,
            Edge = true,
        };

        Assert.False(Emit(gate, t, Cc(80)));  // outside window
        Assert.True(Emit(gate, t, Cc(110)));  // entered → fire
        Assert.False(Emit(gate, t, Cc(120))); // still inside → suppressed
        Assert.False(Emit(gate, t, Cc(60)));  // left
        Assert.True(Emit(gate, t, Cc(127)));  // re-entered → fire
    }

    [Fact]
    public void Edge_TracksControlsIndependently()
    {
        var gate = new EdgeGate();
        var t = new Trigger { Mode = TriggerMode.Trigger, Threshold = 100, Edge = true };

        Assert.True(Emit(gate, t, Cc(110, number: 74)));  // control A rising edge
        Assert.True(Emit(gate, t, Cc(110, number: 75)));  // control B is independent
        Assert.False(Emit(gate, t, Cc(120, number: 74))); // A still in zone
    }

    [Fact]
    public void Reset_ClearsState_SoNextEntryFires()
    {
        var gate = new EdgeGate();
        var t = new Trigger { Mode = TriggerMode.Trigger, Threshold = 100, Edge = true };

        Assert.True(Emit(gate, t, Cc(110)));
        Assert.False(Emit(gate, t, Cc(120)));

        gate.Reset();

        Assert.True(Emit(gate, t, Cc(120))); // state forgotten → fires as a fresh edge
    }

    [Fact]
    public void Edge_IgnoredForRelativeMode()
    {
        var gate = new EdgeGate();
        var t = new Trigger { Mode = TriggerMode.Relative, Edge = true };

        // Relative ticks are inherently events; Edge must not suppress consecutive ticks.
        Assert.True(Emit(gate, t, Cc(2)));
        Assert.True(Emit(gate, t, Cc(2)));
    }
}
