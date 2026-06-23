using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Handlers;

/// <summary>Common base for actions that fire once on press/change (not on release).</summary>
public abstract class FireOnPressHandler : IActionHandler
{
    public abstract bool CanHandle(InputAction action);

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        if (trigger.Phase is TriggerPhase.Press || trigger.IsChange)
        {
            Fire(action);
        }
    }

    protected abstract void Fire(InputAction action);
}

/// <summary>Sends a media/transport key.</summary>
public sealed class MediaKeyActionHandler : FireOnPressHandler
{
    private readonly IInputSink _sink;

    public MediaKeyActionHandler(IInputSink sink) => _sink = sink;

    public override bool CanHandle(InputAction action) => action is MediaKeyAction;

    protected override void Fire(InputAction action) => _sink.SendMediaKey(((MediaKeyAction)action).Key);
}

/// <summary>Types a literal string.</summary>
public sealed class TypeTextActionHandler : FireOnPressHandler
{
    private readonly IInputSink _sink;

    public TypeTextActionHandler(IInputSink sink) => _sink = sink;

    public override bool CanHandle(InputAction action) => action is TypeTextAction;

    protected override void Fire(InputAction action)
    {
        var text = ((TypeTextAction)action).Text;
        if (!string.IsNullOrEmpty(text))
        {
            _sink.TypeText(text);
        }
    }
}

/// <summary>Launches a program/file/URL when external launch is enabled (opt-in, Q5).</summary>
public sealed class LaunchActionHandler : FireOnPressHandler
{
    private readonly IShellLauncher _launcher;
    private readonly LaunchPolicy _policy;

    public LaunchActionHandler(IShellLauncher launcher, LaunchPolicy policy)
    {
        _launcher = launcher;
        _policy = policy;
    }

    public override bool CanHandle(InputAction action) => action is LaunchAction;

    protected override void Fire(InputAction action)
    {
        if (!_policy.Allowed)
        {
            return; // external launch disabled
        }

        var launch = (LaunchAction)action;
        if (!string.IsNullOrWhiteSpace(launch.Target))
        {
            _launcher.Launch(launch.Target, launch.Arguments, launch.WorkingDir);
        }
    }
}

/// <summary>Actuates a control in another window via UI Automation (Phase 2).</summary>
public sealed class UiaActionHandler : FireOnPressHandler
{
    private readonly IUiaDriver _driver;

    public UiaActionHandler(IUiaDriver driver) => _driver = driver;

    public override bool CanHandle(InputAction action) => action is UiaAction;

    protected override void Fire(InputAction action)
    {
        var u = (UiaAction)action;
        if (!string.IsNullOrWhiteSpace(u.ElementName))
        {
            _driver.Actuate(u.WindowPattern, u.ElementName, u.Verb, u.Value);
        }
    }
}

/// <summary>Sets an audio endpoint volume from the continuous input value (Absolute fader).</summary>
public sealed class SetVolumeActionHandler : IActionHandler
{
    private readonly ISystemAudio _audio;

    public SetVolumeActionHandler(ISystemAudio audio) => _audio = audio;

    public bool CanHandle(InputAction action) => action is SetVolumeAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        // Value-driven: the trigger magnitude (Absolute => 0..1) is the target level.
        if (trigger.Phase == TriggerPhase.Change)
        {
            _audio.SetVolume(((SetVolumeAction)action).Target, Math.Clamp(trigger.Magnitude, 0, 1));
        }
    }
}
