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
        var layers = new ActiveRules(profile);
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

        Assert.Empty(new FiringEvaluator().Evaluate(Cc(127), new ActiveRules(profile))); // 127 → -1 decrease
    }

    [Fact]
    public void NoMatchingSignal_ReturnsEmpty()
    {
        var profile = new Profile { Id = "base", Name = "b", Bindings = new[] { RelCc(RelativeOutput.FireOnEither, "a") } };

        var other = new MidiMessage("d", 1, MidiMessageType.ControlChange, 99, 1); // CC99, no binding
        Assert.Empty(new FiringEvaluator().Evaluate(other, new ActiveRules(profile)));
    }

    private static Binding NoteKey(int note, string key) => new()
    {
        Signal = new Signal { Type = SignalKind.NoteOn, Number = note },
        Actions = new InputAction[] { new KeyAction(new[] { key }) },
    };

    [Fact]
    public void MultipleActiveRules_SameSignal_BothFire_Union()
    {
        // Two active rules each bind Note 36 — with the union model both fire (no override).
        var baseRule = new Profile { Id = "base", Name = "base", Bindings = new[] { NoteKey(36, "z") } };
        var appRule = new Profile { Id = "app", Name = "app", Bindings = new[] { NoteKey(36, "y") } };
        var fired = new FiringEvaluator().Evaluate(
            new MidiMessage("d", 1, MidiMessageType.NoteOn, 36, 100),
            new ActiveRules(new[] { baseRule, appRule }));

        var keys = fired.Select(f => Keys(f)[0]).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "y", "z" }, keys); // both rules co-fire
    }

    [Fact]
    public void NoneBinding_IsInert_DoesNotBlockOtherRules()
    {
        // A `none` (block) binding no longer suppresses anything; the other rule still fires.
        var blocker = new Profile
        {
            Id = "app", Name = "app",
            Bindings = new[]
            {
                new Binding
                {
                    Signal = new Signal { Type = SignalKind.NoteOn, Number = 36 },
                    Actions = new InputAction[] { new NoneAction() },
                },
            },
        };
        var baseRule = new Profile { Id = "base", Name = "base", Bindings = new[] { NoteKey(36, "z") } };

        var fired = new FiringEvaluator().Evaluate(
            new MidiMessage("d", 1, MidiMessageType.NoteOn, 36, 100),
            new ActiveRules(new[] { baseRule, blocker }));

        Assert.Equal(new[] { "z" }, Keys(Assert.Single(fired))); // base fires; none is inert
    }
}
