using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Tests.Mapping;

public class DeltaTrackerTests
{
    private static MidiMessage Cc(int value, int number = 74)
        => new("d", 1, MidiMessageType.ControlChange, number, value);

    private static Trigger T(bool wrap = false, int deadzone = 0, bool invert = false, double scale = 1.0)
        => new() { Mode = TriggerMode.Relative, RelativeFormat = RelativeFormat.AbsoluteDelta, Wrap = wrap, Deadzone = deadzone, Invert = invert, Scale = scale };

    [Fact]
    public void FirstValue_OnlyEstablishesBaseline_NoFire()
    {
        var dt = new DeltaTracker();
        Assert.False(dt.Evaluate(T(), Cc(64)).ShouldFire);
    }

    [Fact]
    public void RisingValue_Increases_FallingValue_Decreases()
    {
        var dt = new DeltaTracker();
        Assert.False(dt.Evaluate(T(), Cc(64)).ShouldFire); // baseline

        var up = dt.Evaluate(T(), Cc(70));
        Assert.Equal(TriggerPhase.Increase, up.Phase);
        Assert.Equal(6, up.Magnitude, 3); // 70 - 64

        var down = dt.Evaluate(T(), Cc(60));
        Assert.Equal(TriggerPhase.Decrease, down.Phase);
        Assert.Equal(-10, down.Magnitude, 3); // 60 - 70
    }

    [Fact]
    public void NoChange_DoesNotFire()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(), Cc(50));
        Assert.False(dt.Evaluate(T(), Cc(50)).ShouldFire);
    }

    [Fact]
    public void Deadzone_IgnoresSmallChanges()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(deadzone: 2), Cc(50));
        Assert.False(dt.Evaluate(T(deadzone: 2), Cc(52)).ShouldFire); // |2| <= 2 ignored
        Assert.True(dt.Evaluate(T(deadzone: 2), Cc(55)).ShouldFire);  // |3| > 2 fires
    }

    [Fact]
    public void Invert_FlipsDirection()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(invert: true), Cc(50));
        var r = dt.Evaluate(T(invert: true), Cc(55)); // +5 inverted → decrease
        Assert.Equal(TriggerPhase.Decrease, r.Phase);
        Assert.Equal(-5, r.Magnitude, 3);
    }

    [Fact]
    public void Scale_MultipliesDelta()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(scale: 3.0), Cc(50));
        Assert.Equal(9, dt.Evaluate(T(scale: 3.0), Cc(53)).Magnitude, 3); // +3 × 3
    }

    [Fact]
    public void Wrap_TreatsBigJumpAsSmallStep()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(wrap: true), Cc(127));
        var r = dt.Evaluate(T(wrap: true), Cc(0)); // 0-127 = -127 → +1 with wrap
        Assert.Equal(TriggerPhase.Increase, r.Phase);
        Assert.Equal(1, r.Magnitude, 3);

        var r2 = dt.Evaluate(T(wrap: true), Cc(127)); // 127-0 = +127 → -1 with wrap
        Assert.Equal(TriggerPhase.Decrease, r2.Phase);
        Assert.Equal(-1, r2.Magnitude, 3);
    }

    [Fact]
    public void NoWrap_BigJumpIsRealDelta()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(wrap: false), Cc(127));
        var r = dt.Evaluate(T(wrap: false), Cc(0)); // -127, no wrap
        Assert.Equal(TriggerPhase.Decrease, r.Phase);
        Assert.Equal(-127, r.Magnitude, 3);
    }

    [Fact]
    public void TracksControlsIndependently()
    {
        var dt = new DeltaTracker();
        dt.Evaluate(T(), Cc(50, number: 74));
        dt.Evaluate(T(), Cc(50, number: 75));
        Assert.Equal(5, dt.Evaluate(T(), Cc(55, number: 74)).Magnitude, 3);
        Assert.Equal(-5, dt.Evaluate(T(), Cc(45, number: 75)).Magnitude, 3);
    }
}
