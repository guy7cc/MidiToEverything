using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
using MidiToEverything.Core.Persistence;
using MidiToEverything.Core.Tests.Fakes;

namespace MidiToEverything.Core.Tests.Application;

/// <summary>
/// End-to-end (through fakes) verification of the hot path: FakeMidiSource → pipeline →
/// resolver → trigger → ActionExecutor → RecordingInputSink (docs/04_Roadmap.md M3).
/// </summary>
public sealed class MidiEventPipelineTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private readonly AppConfig _config = DefaultConfig.Create();
    private readonly FakeMidiSource _source = new();
    private readonly RecordingInputSink _sink = new();
    private readonly MutableMappingContext _context;
    private readonly MidiEventPipeline _pipeline;

    public MidiEventPipelineTests()
    {
        _context = new MutableMappingContext(new ActiveRules(_config.BaseProfile));
        _pipeline = new MidiEventPipeline(_source, _context, new ActionExecutor(_sink));
    }

    public Task InitializeAsync()
    {
        _pipeline.Start();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _pipeline.DisposeAsync();

    private Profile Profile(string id) => _config.Profiles.Single(p => p.Id == id);

    private static MidiMessage NoteOn(int n, int velocity = 100) => new("akai", 1, MidiMessageType.NoteOn, n, velocity);
    private static MidiMessage NoteOff(int n) => new("akai", 1, MidiMessageType.NoteOff, n, 0);
    private static MidiMessage Cc(int n, int value) => new("akai", 1, MidiMessageType.ControlChange, n, value);

    [Fact]
    public async Task BaseUndo_NoContext_EmitsKeyTap()
    {
        _source.Emit(NoteOn(36));

        await _sink.WaitForCountAsync(1, Timeout);
        var tap = Assert.IsType<KeyTapCall>(_sink.Calls[0]);
        Assert.Equal(new[] { "ctrl", "z" }, tap.Keys);
    }

    [Fact]
    public async Task HoldKey_InClipStudio_EmitsDownThenUp()
    {
        _context.Set(new ActiveRules(new[] { _config.BaseProfile, Profile("clip-studio") }));

        _source.Emit(NoteOn(40));   // press the pad
        _source.Emit(NoteOff(40));  // release it

        await _sink.WaitForCountAsync(2, Timeout);
        Assert.Equal(new[] { "space" }, Assert.IsType<KeyDownCall>(_sink.Calls[0]).Keys);
        Assert.Equal(new[] { "space" }, Assert.IsType<KeyUpCall>(_sink.Calls[1]).Keys);
    }

    [Fact]
    public async Task Hold_NoteOnSignal_StillReleasesOnNoteOff()
    {
        // A Hold binding whose signal is NoteOn (e.g. captured via "learn") must still release when
        // the device sends an explicit Note Off — otherwise the held key never comes back up.
        var profile = new Profile
        {
            Id = "base",
            Name = "b",
            Bindings = new[]
            {
                new Binding
                {
                    Signal = new Signal { Type = SignalKind.NoteOn, Number = 39 },
                    Trigger = new Trigger { Mode = TriggerMode.Hold },
                    Actions = new InputAction[] { new KeyAction(new[] { "space" }, Hold: true) },
                },
            },
        };
        _context.Set(new ActiveRules(profile));

        _source.Emit(NoteOn(39));  // press → key down
        _source.Emit(NoteOff(39)); // release → key up (this used to be ignored)

        await _sink.WaitForCountAsync(2, Timeout);
        Assert.Equal(new[] { "space" }, Assert.IsType<KeyDownCall>(_sink.Calls[0]).Keys);
        Assert.Equal(new[] { "space" }, Assert.IsType<KeyUpCall>(_sink.Calls[1]).Keys);
    }

    [Fact]
    public async Task NoneInOneRule_DoesNotBlockAnotherRule_Union()
    {
        // Union model: CSP's Note37 `none` is inert and no longer suppresses the base copy, so the
        // base binding still fires. (To suppress base in CSP, scope the base rule's regex instead.)
        _context.Set(new ActiveRules(new[] { _config.BaseProfile, Profile("clip-studio") }));

        _source.Emit(NoteOn(37));

        await _sink.WaitForCountAsync(1, Timeout);
        Assert.Equal(new[] { "ctrl", "c" }, Assert.IsType<KeyTapCall>(_sink.Calls[0]).Keys);
    }

    [Fact]
    public async Task Cc74_Absolute_InClipStudio_EmitsScrollWithMagnitude()
    {
        _context.Set(new ActiveRules(new[] { _config.BaseProfile, Profile("clip-studio") }));

        _source.Emit(Cc(74, 127)); // full value → normalized 1.0

        await _sink.WaitForCountAsync(1, Timeout);
        var scroll = Assert.IsType<ScrollCall>(_sink.Calls[0]);
        Assert.Equal(ScrollAxis.Vertical, scroll.Axis);
        Assert.Equal(1.0, scroll.Amount, 3);
    }

    [Fact]
    public async Task FixedScroll_OnButtonPress_ScrollsThatAmount()
    {
        // A button (Trigger mode) with a fixed, non-value-driven scroll must scroll a set amount —
        // so the wheel can be driven from a pad, not only a knob.
        var profile = new Profile
        {
            Id = "base",
            Name = "b",
            Bindings = new[]
            {
                new Binding
                {
                    Signal = new Signal { Type = SignalKind.NoteOn, Number = 36 },
                    Trigger = new Trigger { Mode = TriggerMode.Trigger },
                    Actions = new InputAction[] { new ScrollAction(ScrollAxis.Vertical, -120, UseInputValue: false) },
                },
            },
        };
        _context.Set(new ActiveRules(profile));

        _source.Emit(NoteOn(36));

        await _sink.WaitForCountAsync(1, Timeout);
        var scroll = Assert.IsType<ScrollCall>(_sink.Calls[0]);
        Assert.Equal(ScrollAxis.Vertical, scroll.Axis);
        Assert.Equal(-120, scroll.Amount, 3);
    }

    [Fact]
    public async Task SwitchProfileBinding_RaisesEvent()
    {
        var tcs = new TaskCompletionSource<SwitchProfileAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pipeline.ProfileSwitchRequested += (_, a) => tcs.TrySetResult(a);

        _source.Emit(NoteOn(51)); // base: switchProfile next

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout));
        Assert.Same(tcs.Task, completed);
        Assert.Equal(ProfileSwitchTarget.Next, (await tcs.Task).Target);
    }

    [Fact]
    public async Task TwoRelativeBindings_SameCc_SplitIncreaseAndDecrease()
    {
        // One knob (CC14): one binding fires on increase, the other on decrease — the documented
        // two-binding split. Both bindings must be evaluated, not just the first match.
        var profile = new Profile
        {
            Id = "base",
            Name = "b",
            Bindings = new[]
            {
                RelCc(14, RelativeOutput.FireOnIncrease, "a"),
                RelCc(14, RelativeOutput.FireOnDecrease, "b"),
            },
        };
        _context.Set(new ActiveRules(profile));

        _source.Emit(Cc(14, 1));   // two's complement 1 → +1 → increase → fires "a"
        _source.Emit(Cc(14, 127)); // two's complement 127 → -1 → decrease → fires "b"

        await _sink.WaitForCountAsync(2, Timeout);
        Assert.Equal(new[] { "a" }, Assert.IsType<KeyTapCall>(_sink.Calls[0]).Keys);
        Assert.Equal(new[] { "b" }, Assert.IsType<KeyTapCall>(_sink.Calls[1]).Keys);
    }

    private static Binding RelCc(int number, RelativeOutput output, string key) => new()
    {
        Signal = new Signal { Type = SignalKind.Cc, Number = number },
        Trigger = new Trigger { Mode = TriggerMode.Relative, RelativeFormat = RelativeFormat.TwosComplement, RelativeOutput = output },
        Actions = new InputAction[] { new KeyAction(new[] { key }) },
    };

    [Fact]
    public async Task MessageOrder_IsPreserved()
    {
        _source.Emit(NoteOn(36)); // undo
        _source.Emit(NoteOn(37)); // copy

        await _sink.WaitForCountAsync(2, Timeout);
        Assert.Equal(new[] { "ctrl", "z" }, Assert.IsType<KeyTapCall>(_sink.Calls[0]).Keys);
        Assert.Equal(new[] { "ctrl", "c" }, Assert.IsType<KeyTapCall>(_sink.Calls[1]).Keys);
    }
}
