using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Port over window management of the foreground window (docs/05_ActionExpansion.md §3.2).
/// The Windows adapter uses Win32 (ShowWindow/SetWindowPos/PostMessage).
/// </summary>
public interface IWindowController
{
    /// <summary>Apply the operation to the currently focused window.</summary>
    void Apply(WindowOp op);
}
