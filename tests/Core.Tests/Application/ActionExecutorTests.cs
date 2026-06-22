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
}
