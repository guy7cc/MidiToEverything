using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Port over UI Automation for actuating a control in another window (docs/05 §3.2, Phase 2).
/// </summary>
public interface IUiaDriver
{
    /// <summary>
    /// Find the window matching <paramref name="windowPattern"/> (regex over "process\ntitle"),
    /// locate the element by Name/AutomationId, and apply the verb. No-op if not found.
    /// </summary>
    void Actuate(string windowPattern, string elementName, UiaVerb verb, string? value);
}

/// <summary>What the element picker captured (docs/05 §3.5, the UIA "Learn").</summary>
public sealed record UiaPick(string WindowPattern, string ElementName);

/// <summary>Editor-time helper that captures the element under the cursor.</summary>
public interface IUiaElementPicker
{
    /// <summary>
    /// After a short delay (so the user can hover the target), capture the element under the
    /// cursor and the owning window. Returns null if nothing usable was found.
    /// </summary>
    Task<UiaPick?> PickAsync();
}
