using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Handlers;

/// <summary>Applies a window operation to the foreground window when the binding fires.</summary>
public sealed class WindowControlActionHandler : IActionHandler
{
    private readonly IWindowController _windows;

    public WindowControlActionHandler(IWindowController windows) => _windows = windows;

    public bool CanHandle(InputAction action) => action is WindowControlAction;

    public void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        // Fire once on press/change; ignore release so Note (on+off) bindings don't double-apply.
        if (trigger.Phase is TriggerPhase.Press || trigger.IsChange)
        {
            _windows.Apply(((WindowControlAction)action).Op);
        }
    }
}
