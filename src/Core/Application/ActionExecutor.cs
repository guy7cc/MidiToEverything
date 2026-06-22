using MidiToEverything.Core.Application.Handlers;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>
/// Dispatches a resolved <see cref="Binding"/>'s actions to the registered
/// <see cref="IActionHandler"/>s (docs/05_ActionExpansion.md §3.2). Profile-switch actions
/// are surfaced as an event for ProfileManager rather than handled here; <see cref="NoneAction"/>
/// is an explicit no-op. New action categories are added by registering a handler — this
/// class stays closed to modification.
/// </summary>
public sealed class ActionExecutor
{
    private readonly IReadOnlyList<IActionHandler> _handlers;

    public ActionExecutor(IEnumerable<IActionHandler> handlers) => _handlers = handlers.ToArray();

    /// <summary>Convenience ctor wiring the built-in OS input handlers (keyboard/mouse/cursor/scroll).</summary>
    public ActionExecutor(IInputSink sink) : this(DefaultHandlers(sink))
    {
    }

    /// <summary>The built-in keyboard/mouse/cursor/scroll handlers over an <see cref="IInputSink"/>.</summary>
    public static IReadOnlyList<IActionHandler> DefaultHandlers(IInputSink sink) => new IActionHandler[]
    {
        new KeyActionHandler(sink),
        new MouseClickActionHandler(sink),
        new CursorMoveActionHandler(sink),
        new ScrollActionHandler(sink),
    };

    /// <summary>Raised when a binding requests a profile switch (FR-5.4); handled by ProfileManager.</summary>
    public event EventHandler<SwitchProfileAction>? ProfileSwitchRequested;

    public void Execute(Binding binding, TriggerResult trigger, MidiMessage message)
    {
        foreach (var action in binding.Actions)
        {
            Execute(action, trigger, message);
        }
    }

    private void Execute(InputAction action, TriggerResult trigger, MidiMessage message)
    {
        switch (action)
        {
            case SwitchProfileAction switchProfile:
                ProfileSwitchRequested?.Invoke(this, switchProfile);
                return;

            case NoneAction:
                // Explicit "do nothing"; the block-vs-fallback decision lives in the resolver.
                return;
        }

        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(action))
            {
                handler.Execute(action, trigger, message);
                return;
            }
        }

        // No handler registered for this action (e.g. a forward-compat type the build
        // does not know): ignore. Unknown persisted actions are warned about at load time.
    }
}
