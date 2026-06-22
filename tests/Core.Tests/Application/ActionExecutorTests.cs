using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Handlers;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
using MidiToEverything.Core.Tests.Fakes;

namespace MidiToEverything.Core.Tests.Application;

public class ActionExecutorTests
{
    private readonly RecordingInputSink _sink = new();
    private ActionExecutor Executor => new(_sink);

    private static MidiMessage Msg => new("d", 1, MidiMessageType.NoteOn, 36, 100);

    private static Binding With(params InputAction[] actions) => new()
    {
        Signal = new Signal { Type = SignalKind.NoteOn, Number = 36 },
        Actions = actions,
    };

    [Fact]
    public void MouseClick_IsEmitted()
    {
        Executor.Execute(With(new MouseClickAction(MouseButton.Right, Double: true)),
            new TriggerResult(TriggerPhase.Press, 0), Msg);

        var click = Assert.IsType<MouseClickCall>(Assert.Single(_sink.Calls));
        Assert.Equal(MouseButton.Right, click.Button);
        Assert.True(click.Double);
    }

    [Fact]
    public void ValueDrivenCursorMove_UsesMagnitude()
    {
        Executor.Execute(With(new CursorMoveAction(MoveMode.Relative, UseInputValue: true)),
            new TriggerResult(TriggerPhase.Change, 12.5), Msg);

        var move = Assert.IsType<MoveCall>(Assert.Single(_sink.Calls));
        Assert.Equal(12.5, move.Dx, 3);
        Assert.Equal(0, move.Dy);
    }

    [Fact]
    public void FixedCursorMove_UsesConfiguredDelta()
    {
        Executor.Execute(With(new CursorMoveAction(MoveMode.Relative, Dx: 5, Dy: -3, UseInputValue: false)),
            new TriggerResult(TriggerPhase.Change, 99), Msg);

        var move = Assert.IsType<MoveCall>(Assert.Single(_sink.Calls));
        Assert.Equal(5, move.Dx);
        Assert.Equal(-3, move.Dy);
    }

    [Fact]
    public void MultipleActions_RunInOrder()
    {
        Executor.Execute(
            With(new KeyAction(new[] { "ctrl", "c" }), new KeyAction(new[] { "ctrl", "v" })),
            new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Collection(_sink.Calls,
            c => Assert.Equal(new[] { "ctrl", "c" }, Assert.IsType<KeyTapCall>(c).Keys),
            c => Assert.Equal(new[] { "ctrl", "v" }, Assert.IsType<KeyTapCall>(c).Keys));
    }

    [Fact]
    public void ActionWithNoRegisteredHandler_IsIgnored()
    {
        var executor = new ActionExecutor(Array.Empty<IActionHandler>());

        var ex = Record.Exception(() => executor.Execute(
            With(new MouseClickAction()), new TriggerResult(TriggerPhase.Press, 0), Msg));

        Assert.Null(ex);
        Assert.Empty(_sink.Calls);
    }

    [Fact]
    public void Dispatch_PicksTheHandlerThatCanHandle()
    {
        var probe = new ProbeHandler();
        var executor = new ActionExecutor(new IActionHandler[] { probe });

        executor.Execute(With(new MouseClickAction()), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(1, probe.Count);
    }

    private sealed class ProbeHandler : IActionHandler
    {
        public int Count { get; private set; }
        public bool CanHandle(InputAction action) => action is MouseClickAction;
        public void Execute(InputAction action, TriggerResult trigger, MidiMessage message) => Count++;
    }

    [Fact]
    public void WindowControl_AppliesOpOnPress()
    {
        var windows = new RecordingWindowController();
        var executor = new ActionExecutor(new IActionHandler[] { new WindowControlActionHandler(windows) });

        executor.Execute(With(new WindowControlAction(WindowOp.Maximize)),
            new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(WindowOp.Maximize, Assert.Single(windows.Ops));
    }

    [Fact]
    public void WindowControl_IgnoresRelease()
    {
        var windows = new RecordingWindowController();
        var executor = new ActionExecutor(new IActionHandler[] { new WindowControlActionHandler(windows) });

        executor.Execute(With(new WindowControlAction()),
            new TriggerResult(TriggerPhase.Release, 0), Msg);

        Assert.Empty(windows.Ops);
    }

    private sealed class RecordingWindowController : IWindowController
    {
        public List<WindowOp> Ops { get; } = new();
        public void Apply(WindowOp op) => Ops.Add(op);
    }

    [Fact]
    public void MediaKey_IsTapped()
    {
        var executor = new ActionExecutor(new IActionHandler[] { new MediaKeyActionHandler(_sink) });

        executor.Execute(With(new MediaKeyAction(MediaKey.Next)), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(MediaKey.Next, Assert.IsType<MediaKeyCall>(Assert.Single(_sink.Calls)).Key);
    }

    [Fact]
    public void TypeText_IsTyped()
    {
        var executor = new ActionExecutor(new IActionHandler[] { new TypeTextActionHandler(_sink) });

        executor.Execute(With(new TypeTextAction("hi")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal("hi", Assert.IsType<TypeTextCall>(Assert.Single(_sink.Calls)).Text);
    }

    [Fact]
    public void Launch_NoOpsWhenPolicyDisallows()
    {
        var launcher = new RecordingLauncher();
        var executor = new ActionExecutor(new IActionHandler[]
        {
            new LaunchActionHandler(launcher, new LaunchPolicy(allowed: false)),
        });

        executor.Execute(With(new LaunchAction("notepad.exe")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Empty(launcher.Targets);
    }

    [Fact]
    public void Launch_RunsWhenPolicyAllows()
    {
        var launcher = new RecordingLauncher();
        var executor = new ActionExecutor(new IActionHandler[]
        {
            new LaunchActionHandler(launcher, new LaunchPolicy(allowed: true)),
        });

        executor.Execute(With(new LaunchAction("notepad.exe", "a.txt")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(("notepad.exe", "a.txt"), Assert.Single(launcher.Targets));
    }

    [Fact]
    public void SetVolume_UsesMagnitudeOnChange()
    {
        var audio = new RecordingAudio();
        var executor = new ActionExecutor(new IActionHandler[] { new SetVolumeActionHandler(audio) });

        executor.Execute(With(new SetVolumeAction(VolumeTarget.Master)), new TriggerResult(TriggerPhase.Change, 0.75), Msg);

        var (target, level) = Assert.Single(audio.Calls);
        Assert.Equal(VolumeTarget.Master, target);
        Assert.Equal(0.75, level, 3);
    }

    private sealed class RecordingLauncher : IShellLauncher
    {
        public List<(string Target, string? Args)> Targets { get; } = new();
        public void Launch(string target, string? arguments, string? workingDir) => Targets.Add((target, arguments));
    }

    private sealed class RecordingAudio : ISystemAudio
    {
        public List<(VolumeTarget Target, double Level)> Calls { get; } = new();
        public void SetVolume(VolumeTarget target, double level) => Calls.Add((target, level));
    }

    [Fact]
    public void Uia_ActuatesElementOnPress()
    {
        var driver = new RecordingUiaDriver();
        var executor = new ActionExecutor(new IActionHandler[] { new UiaActionHandler(driver) });

        executor.Execute(With(new UiaAction("^notepad", "OK", UiaVerb.Invoke)),
            new TriggerResult(TriggerPhase.Press, 0), Msg);

        var call = Assert.Single(driver.Calls);
        Assert.Equal("^notepad", call.Window);
        Assert.Equal("OK", call.Element);
        Assert.Equal(UiaVerb.Invoke, call.Verb);
    }

    [Fact]
    public void Uia_NoOpsWhenElementNameBlank()
    {
        var driver = new RecordingUiaDriver();
        var executor = new ActionExecutor(new IActionHandler[] { new UiaActionHandler(driver) });

        executor.Execute(With(new UiaAction("^notepad", "")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Empty(driver.Calls);
    }

    private sealed class RecordingUiaDriver : IUiaDriver
    {
        public List<(string Window, string Element, UiaVerb Verb, string? Value)> Calls { get; } = new();
        public void Actuate(string windowPattern, string elementName, UiaVerb verb, string? value)
            => Calls.Add((windowPattern, elementName, verb, value));
    }

    [Theory]
    [InlineData(DesktopOp.Next, "right")]
    [InlineData(DesktopOp.Previous, "left")]
    public void VirtualDesktop_SendsWinCtrlArrow(DesktopOp op, string arrow)
    {
        var executor = new ActionExecutor(new IActionHandler[] { new VirtualDesktopActionHandler(_sink) });

        executor.Execute(With(new VirtualDesktopAction(op)), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(new[] { "win", "ctrl", arrow }, Assert.IsType<KeyTapCall>(Assert.Single(_sink.Calls)).Keys);
    }

    [Fact]
    public void WindowsToggle_TogglesSetting()
    {
        var toggle = new RecordingToggle();
        var executor = new ActionExecutor(new IActionHandler[] { new WindowsToggleActionHandler(toggle) });

        executor.Execute(With(new WindowsToggleAction(WindowsSetting.DarkMode)), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(WindowsSetting.DarkMode, Assert.Single(toggle.Settings));
    }

    [Fact]
    public void Brightness_UsesMagnitudeOnChange()
    {
        var display = new RecordingBrightness();
        var executor = new ActionExecutor(new IActionHandler[] { new BrightnessActionHandler(display) });

        executor.Execute(With(new BrightnessAction()), new TriggerResult(TriggerPhase.Change, 0.4), Msg);

        Assert.Equal(0.4, Assert.Single(display.Levels), 3);
    }

    private sealed class RecordingToggle : ISystemToggle
    {
        public List<WindowsSetting> Settings { get; } = new();
        public void Toggle(WindowsSetting setting) => Settings.Add(setting);
    }

    private sealed class RecordingBrightness : IDisplayBrightness
    {
        public List<double> Levels { get; } = new();
        public void SetBrightness(double level) => Levels.Add(level);
    }

    [Fact]
    public void Http_SendsOnPress()
    {
        var http = new RecordingHttp();
        var executor = new ActionExecutor(new IActionHandler[] { new HttpActionHandler(http) });

        executor.Execute(With(new HttpAction("http://x/y", "POST", "b")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(("http://x/y", "POST", "b"), Assert.Single(http.Calls));
    }

    [Fact]
    public void Osc_SendsOnPress()
    {
        var osc = new RecordingOsc();
        var executor = new ActionExecutor(new IActionHandler[] { new OscActionHandler(osc) });

        executor.Execute(With(new OscAction("127.0.0.1:9000", "/a", "1 2")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(("127.0.0.1:9000", "/a", "1 2"), Assert.Single(osc.Calls));
    }

    private sealed class RecordingHttp : IHttpSender
    {
        public List<(string Url, string Method, string? Body)> Calls { get; } = new();
        public void Send(string url, string method, string? body) => Calls.Add((url, method, body));
    }

    private sealed class RecordingOsc : IOscSender
    {
        public List<(string Target, string Address, string Args)> Calls { get; } = new();
        public void Send(string target, string address, string args) => Calls.Add((target, address, args));
    }

    [Fact]
    public void Obs_SendsOpOnPress()
    {
        var obs = new RecordingObs();
        var executor = new ActionExecutor(new IActionHandler[] { new ObsActionHandler(obs) });

        executor.Execute(With(new ObsAction(ObsOp.SceneSwitch, "Scene 1")), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal((ObsOp.SceneSwitch, "Scene 1"), Assert.Single(obs.Calls));
    }

    private sealed class RecordingObs : IObsClient
    {
        public List<(ObsOp Op, string? Arg)> Calls { get; } = new();
        public void Send(ObsOp op, string? arg) => Calls.Add((op, arg));
    }

    [Fact]
    public void MidiOut_SendsFixedMessageOnPress()
    {
        var midi = new RecordingMidiOut();
        var executor = new ActionExecutor(new IActionHandler[] { new MidiOutActionHandler(midi) });

        executor.Execute(With(new MidiOutAction("^loop", MidiOutKind.ControlChange, 1, 7, 64)),
            new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Equal(("^loop", MidiOutKind.ControlChange, 1, 7, 64), Assert.Single(midi.Calls));
    }

    [Fact]
    public void MidiOut_DrivesData2FromInputValueOnChange()
    {
        var midi = new RecordingMidiOut();
        var executor = new ActionExecutor(new IActionHandler[] { new MidiOutActionHandler(midi) });

        executor.Execute(With(new MidiOutAction("^loop", MidiOutKind.ControlChange, 1, 7, 0, UseInputValue: true)),
            new TriggerResult(TriggerPhase.Change, 1.0), Msg);

        Assert.Equal(127, Assert.Single(midi.Calls).Data2); // magnitude 1.0 -> 127
    }

    private sealed class RecordingMidiOut : IMidiOutput
    {
        public List<(string Device, MidiOutKind Kind, int Channel, int Data1, int Data2)> Calls { get; } = new();
        public void Send(string devicePattern, MidiOutKind kind, int channel, int data1, int data2)
            => Calls.Add((devicePattern, kind, channel, data1, data2));
    }

    [Fact]
    public async Task Macro_RunsStepsInOrder()
    {
        var executor = new ActionExecutor(new IActionHandler[] { new MacroActionHandler(_sink) });
        var steps = new IReadOnlyList<string>[] { new[] { "ctrl", "c" }, new[] { "ctrl", "v" } };

        executor.Execute(With(new MacroAction(steps, 0)), new TriggerResult(TriggerPhase.Press, 0), Msg);
        await _sink.WaitForCountAsync(2, TimeSpan.FromSeconds(2));

        Assert.Collection(_sink.Calls,
            c => Assert.Equal(new[] { "ctrl", "c" }, Assert.IsType<KeyTapCall>(c).Keys),
            c => Assert.Equal(new[] { "ctrl", "v" }, Assert.IsType<KeyTapCall>(c).Keys));
    }

    [Fact]
    public void Toggle_AlternatesKeysAndDrivesLed()
    {
        var midi = new RecordingMidiOut();
        var executor = new ActionExecutor(new IActionHandler[] { new ToggleActionHandler(_sink, midi) });
        var action = new ToggleAction(new[] { "a" }, new[] { "b" }, "^loop", 1, 36);

        executor.Execute(With(action), new TriggerResult(TriggerPhase.Press, 0), Msg);
        executor.Execute(With(action), new TriggerResult(TriggerPhase.Press, 0), Msg);

        Assert.Collection(_sink.Calls,
            c => Assert.Equal(new[] { "a" }, Assert.IsType<KeyTapCall>(c).Keys),
            c => Assert.Equal(new[] { "b" }, Assert.IsType<KeyTapCall>(c).Keys));
        Assert.Collection(midi.Calls,
            c => Assert.Equal(127, c.Data2), // first press: state A, LED lit
            c => Assert.Equal(0, c.Data2));  // second press: state B, LED off
    }
}
