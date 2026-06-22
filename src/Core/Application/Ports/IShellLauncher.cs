namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Port over the OS shell for launching programs/files/URLs (docs/05_ActionExpansion.md §5).
/// Gated behind the external-launch opt-in (Q5).
/// </summary>
public interface IShellLauncher
{
    void Launch(string target, string? arguments, string? workingDir);
}
