using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Tests.Mapping;

public class FiringEvaluatorTests
{
    private static Binding RelCc(RelativeOutput output, string key) => new()
    {
        Signal = new Signal { Type = SignalKind.Cc, Number = 14 },
        Trigger = new Trigger { Mode = TriggerMode.Relative, RelativeOutput = output },
        Actions = new InputAction[] { new KeyAction(new[] { key }) },
    };

    private static MidiMessage Cc(int value) => new("d", 1, MidiMessageType.ControlChange, 14, value);

    private static string[] Keys(Firing f) => ((KeyAction)f.Binding.Actions[0]).Keys.ToArray();

    [Fact]
    public void ReturnsOnlyTheBindingThatFires_PerDirection()
    {
        var profile = new Profile
        {
            Id = "base",
            Name = "b",
            Bindings = new[] { RelCc(RelativeOutput.FireOnIncrease, "a"), RelCc(RelativeOutput.FireOnDecrease, "b") },
        };
        var layers = new ProfileLayers(profile);
        var fe = new FiringEvaluator();

        var up = fe.Evaluate(Cc(1), layers); // two's complement +1 → increase
        Assert.Equal(new[] { "a" }, Keys(Assert.Single(up)));

        var down = fe.Evaluate(Cc(127), layers); // two's complement -1 → decrease
        Assert.Equal(new[] { "b" }, Keys(Assert.Single(down)));
    }

    [Fact]
    public void SignalMatchesButTriggerDoesNotFire_ReturnsEmpty()
    {
        // A fire-on-increase binding: a decrease tick matches the signal but must NOT fire,
        // so the monitor shows nothing for it (the bug this guards against).
        var profile = new Profile { Id = "base", Name = "b", Bindings = new[] { RelCc(RelativeOutput.FireOnIncrease, "a") } };

        Assert.Empty(new FiringEvaluator().Evaluate(Cc(127), new ProfileLayers(profile))); // 127 → -1 decrease
    }

    [Fact]
    public void NoMatchingSignal_ReturnsEmpty()
    {
        var profile = new Profile { Id = "base", Name = "b", Bindings = new[] { RelCc(RelativeOutput.FireOnEither, "a") } };

        var other = new MidiMessage("d", 1, MidiMessageType.ControlChange, 99, 1); // CC99, no binding
        Assert.Empty(new FiringEvaluator().Evaluate(other, new ProfileLayers(profile)));
    }
}
