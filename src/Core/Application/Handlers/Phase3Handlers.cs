using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Handlers;

/// <summary>Sends an HTTP request / webhook.</summary>
public sealed class HttpActionHandler : FireOnPressHandler
{
    private readonly IHttpSender _http;

    public HttpActionHandler(IHttpSender http) => _http = http;

    public override bool CanHandle(InputAction action) => action is HttpAction;

    protected override void Fire(InputAction action)
    {
        var h = (HttpAction)action;
        if (!string.IsNullOrWhiteSpace(h.Url))
        {
            _http.Send(h.Url, h.Method, h.Body);
        }
    }
}

/// <summary>Controls OBS Studio via obs-websocket.</summary>
public sealed class ObsActionHandler : FireOnPressHandler
{
    private readonly IObsClient _obs;

    public ObsActionHandler(IObsClient obs) => _obs = obs;

    public override bool CanHandle(InputAction action) => action is ObsAction;

    protected override void Fire(InputAction action)
    {
        var o = (ObsAction)action;
        _obs.Send(o.Op, o.Arg);
    }
}

/// <summary>Sends a MIDI message to an output device (fixed, or input-value-driven for CC).</summary>
public sealed class MidiOutActionHandler : IActionHandler
{
    private readonly IMidiOutput _output;

    public MidiOutActionHandler(IMidiOutput output) => _output = output;

    public bool CanHandle(InputAction action) => action is MidiOutAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        var m = (MidiOutAction)action;
        if (m.UseInputValue)
        {
            if (trigger.Phase == TriggerPhase.Change)
            {
                var value = Math.Clamp((int)Math.Round(trigger.Magnitude * 127), 0, 127);
                _output.Send(m.Device, m.Kind, m.Channel, m.Data1, value);
            }
        }
        else if (trigger.Phase is TriggerPhase.Press or TriggerPhase.Change)
        {
            _output.Send(m.Device, m.Kind, m.Channel, m.Data1, m.Data2);
        }
    }
}

/// <summary>Sends an OSC message over UDP.</summary>
public sealed class OscActionHandler : FireOnPressHandler
{
    private readonly IOscSender _osc;

    public OscActionHandler(IOscSender osc) => _osc = osc;

    public override bool CanHandle(InputAction action) => action is OscAction;

    protected override void Fire(InputAction action)
    {
        var o = (OscAction)action;
        if (!string.IsNullOrWhiteSpace(o.Target) && !string.IsNullOrWhiteSpace(o.Address))
        {
            _osc.Send(o.Target, o.Address, o.Args);
        }
    }
}
