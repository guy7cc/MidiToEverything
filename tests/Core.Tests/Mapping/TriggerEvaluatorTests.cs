using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Tests.Mapping;

public class TriggerEvaluatorTests
{
    private static MidiMessage Note(MidiMessageType type, int value)
        => new("d", 1, type, 36, value);

    private static MidiMessage Cc(int value)
        => new("d", 1, MidiMessageType.ControlChange, 74, value);

    // ── Trigger mode ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(127, true)]
    public void Trigger_FiresAtOrAboveThreshold(int velocity, bool fires)
    {
        var t = new Trigger { Mode = TriggerMode.Trigger, Threshold = 1 };

        var r = TriggerEvaluator.Evaluate(t, Note(MidiMessageType.NoteOn, velocity));

        Assert.Equal(fires, r.ShouldFire);
        if (fires) Assert.Equal(TriggerPhase.Press, r.Phase);
    }

    // ── Hold mode ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hold_NoteOnPresses_NoteOffReleases()
    {
        var t = new Trigger { Mode = TriggerMode.Hold };

        Assert.Equal(TriggerPhase.Press,
            TriggerEvaluator.Evaluate(t, Note(MidiMessageType.NoteOn, 100)).Phase);
        Assert.Equal(TriggerPhase.Release,
            TriggerEvaluator.Evaluate(t, Note(MidiMessageType.NoteOff, 0)).Phase);
    }

    [Fact]
    public void Hold_NoteOnZeroVelocity_IsRelease()
    {
        var t = new Trigger { Mode = TriggerMode.Hold };

        Assert.Equal(TriggerPhase.Release,
            TriggerEvaluator.Evaluate(t, Note(MidiMessageType.NoteOn, 0)).Phase);
    }

    // ── Absolute mode ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(127, 1.0)]
    public void Absolute_NormalizesWithinRange(int value, double expected)
    {
        var t = new Trigger { Mode = TriggerMode.Absolute, RangeMin = 0, RangeMax = 127 };

        var r = TriggerEvaluator.Evaluate(t, Cc(value));

        Assert.Equal(TriggerPhase.Change, r.Phase);
        Assert.Equal(expected, r.Magnitude, 3);
    }

    [Fact]
    public void Absolute_AppliesInvertAndScale()
    {
        var t = new Trigger { Mode = TriggerMode.Absolute, Invert = true, Scale = 2.0 };

        var r = TriggerEvaluator.Evaluate(t, Cc(0)); // inverted 0 → 1.0, ×2 = 2.0

        Assert.Equal(2.0, r.Magnitude, 3);
    }

    [Theory]
    [InlineData(59, false)] // just below the window
    [InlineData(60, true)]  // lower edge → 0.0
    [InlineData(65, true)]  // inside → 0.5
    [InlineData(70, true)]  // upper edge → 1.0
    [InlineData(71, false)] // just above the window
    public void Absolute_Gate_FiresOnlyInsideWindow(int value, bool fires)
    {
        var t = new Trigger
        {
            Mode = TriggerMode.Absolute,
            RangeMin = 60,
            RangeMax = 70,
            OutOfRange = OutOfRangeBehavior.Gate,
        };

        var r = TriggerEvaluator.Evaluate(t, Cc(value));

        Assert.Equal(fires, r.ShouldFire);
        if (fires)
        {
            Assert.Equal(TriggerPhase.Change, r.Phase);
            Assert.InRange(r.Magnitude, 0.0, 1.0);
        }
    }

    [Theory]
    [InlineData(0, 0.0)]   // below window clamps to the edge and still fires
    [InlineData(127, 1.0)] // above window clamps to the edge and still fires
    public void Absolute_Clamp_IsDefault_AndFiresOutsideWindow(int value, double expected)
    {
        var t = new Trigger { Mode = TriggerMode.Absolute, RangeMin = 60, RangeMax = 70 };

        var r = TriggerEvaluator.Evaluate(t, Cc(value));

        Assert.True(r.ShouldFire);
        Assert.Equal(expected, r.Magnitude, 3);
    }

    // ── Relative mode ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RelativeFormat.TwosComplement, 1, 1)]
    [InlineData(RelativeFormat.TwosComplement, 127, -1)]
    [InlineData(RelativeFormat.TwosComplement, 65, -63)]
    [InlineData(RelativeFormat.BinaryOffset, 65, 1)]
    [InlineData(RelativeFormat.BinaryOffset, 63, -1)]
    [InlineData(RelativeFormat.SignedBit, 0x01, 1)]
    [InlineData(RelativeFormat.SignedBit, 0x41, -1)]
    public void Relative_DecodesSignedDelta(RelativeFormat format, int value, int expected)
    {
        Assert.Equal(expected, TriggerEvaluator.DecodeRelative(format, value));
    }

    [Fact]
    public void Relative_ZeroDelta_DoesNotFire()
    {
        var t = new Trigger { Mode = TriggerMode.Relative, RelativeFormat = RelativeFormat.BinaryOffset };

        var r = TriggerEvaluator.Evaluate(t, Cc(64)); // 64 - 64 = 0

        Assert.False(r.ShouldFire);
    }

    [Fact]
    public void Relative_AppliesScale()
    {
        var t = new Trigger { Mode = TriggerMode.Relative, Scale = 3.0 };

        var r = TriggerEvaluator.Evaluate(t, Cc(2)); // +2 × 3 = 6

        Assert.Equal(6.0, r.Magnitude, 3);
    }

    [Theory]
    [InlineData(2, TriggerPhase.Increase)]   // +2
    [InlineData(126, TriggerPhase.Decrease)] // 126 - 128 = -2
    public void Relative_SplitsIntoIncreaseAndDecrease(int value, TriggerPhase expected)
    {
        var t = new Trigger { Mode = TriggerMode.Relative };

        var r = TriggerEvaluator.Evaluate(t, Cc(value));

        Assert.Equal(expected, r.Phase);
        Assert.True(r.IsChange); // both directions are value-carrying changes
    }

    [Fact]
    public void Relative_Invert_FlipsDirection()
    {
        var t = new Trigger { Mode = TriggerMode.Relative, Invert = true };

        var r = TriggerEvaluator.Evaluate(t, Cc(2)); // +2 inverted → decrease

        Assert.Equal(TriggerPhase.Decrease, r.Phase);
        Assert.Equal(-2.0, r.Magnitude, 3);
    }

    [Theory]
    [InlineData(2, RelativeOutput.FireOnIncrease, true)]    // +2 increase → fires
    [InlineData(126, RelativeOutput.FireOnIncrease, false)] // -2 → no fire
    [InlineData(126, RelativeOutput.FireOnDecrease, true)]  // -2 decrease → fires
    [InlineData(2, RelativeOutput.FireOnDecrease, false)]   // +2 → no fire
    public void Relative_FireOutput_FiresOnChosenDirectionOnly(int value, RelativeOutput output, bool fires)
    {
        var t = new Trigger { Mode = TriggerMode.Relative, RelativeOutput = output };

        var r = TriggerEvaluator.Evaluate(t, Cc(value));

        Assert.Equal(fires, r.ShouldFire);
        if (fires)
        {
            Assert.Equal(TriggerPhase.Press, r.Phase); // fires like a button (no amount)
        }
    }

    [Fact]
    public void Relative_AbsoluteDelta_NotHandledByPureEvaluator()
    {
        // AbsoluteDelta is stateful and routed to DeltaTracker; the pure evaluator emits nothing.
        var t = new Trigger { Mode = TriggerMode.Relative, RelativeFormat = RelativeFormat.AbsoluteDelta };

        Assert.False(TriggerEvaluator.Evaluate(t, Cc(2)).ShouldFire);
    }
}
