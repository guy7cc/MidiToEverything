using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Handlers;

/// <summary>Switches virtual desktop by emitting Win+Ctrl+Left/Right through the input sink.</summary>
public sealed class VirtualDesktopActionHandler : FireOnPressHandler
{
    private readonly IInputSink _sink;

    public VirtualDesktopActionHandler(IInputSink sink) => _sink = sink;

    public override bool CanHandle(InputAction action) => action is VirtualDesktopAction;

    protected override void Fire(InputAction action)
    {
        var op = ((VirtualDesktopAction)action).Op;
        _sink.KeyTap(new[] { "win", "ctrl", op == DesktopOp.Next ? "right" : "left" });
    }
}

/// <summary>Toggles a Windows setting (dark/light theme, ...).</summary>
public sealed class WindowsToggleActionHandler : FireOnPressHandler
{
    private readonly ISystemToggle _toggle;

    public WindowsToggleActionHandler(ISystemToggle toggle) => _toggle = toggle;

    public override bool CanHandle(InputAction action) => action is WindowsToggleAction;

    protected override void Fire(InputAction action) => _toggle.Toggle(((WindowsToggleAction)action).Setting);
}

/// <summary>Sets display brightness from the continuous input value (Absolute fader).</summary>
public sealed class BrightnessActionHandler : IActionHandler
{
    private readonly IDisplayBrightness _display;

    public BrightnessActionHandler(IDisplayBrightness display) => _display = display;

    public bool CanHandle(InputAction action) => action is BrightnessAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        if (trigger.Phase == TriggerPhase.Change)
        {
            _display.SetBrightness(Math.Clamp(trigger.Magnitude, 0, 1));
        }
    }
}
