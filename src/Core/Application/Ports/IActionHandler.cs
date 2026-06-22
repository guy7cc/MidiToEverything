using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Handles one family of <see cref="InputAction"/>. New action categories are added by
/// registering a handler (docs/05_ActionExpansion.md §3.2); <see cref="ActionExecutor"/>
/// stays closed to modification (Open/Closed).
/// </summary>
public interface IActionHandler
{
    /// <summary>True when this handler knows how to execute the given action.</summary>
    bool CanHandle(InputAction action);

    /// <summary>Execute the action. <paramref name="trigger"/> carries phase + continuous magnitude.</summary>
    void Execute(InputAction action, TriggerResult trigger, MidiMessage message);
}
