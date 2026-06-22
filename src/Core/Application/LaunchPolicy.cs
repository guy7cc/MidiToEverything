namespace MidiToEverything.Core.Application;

/// <summary>
/// Runtime opt-in gate for external launch/command actions (docs/05 §6, Q5). Default off;
/// the user enables it explicitly and the choice is persisted in settings.
/// </summary>
public sealed class LaunchPolicy
{
    public LaunchPolicy(bool allowed = false) => Allowed = allowed;

    /// <summary>When false, launch actions no-op.</summary>
    public bool Allowed { get; set; }
}
