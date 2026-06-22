namespace MidiToEverything.Core.Application.Ports;

/// <summary>
/// Contract a third-party plugin implements to add custom actions (docs/05 §5, Phase 4). A plugin
/// assembly is dropped in the app's <c>plugins/</c> folder; the host discovers public, parameterless
/// implementations and routes <c>PluginAction</c>s to them by <see cref="Id"/>.
/// </summary>
public interface IActionPlugin
{
    /// <summary>Stable identifier used to route actions to this plugin (e.g. "my-plugin").</summary>
    string Id { get; }

    /// <summary>Run a command (with an optional argument) requested by a binding.</summary>
    void Execute(string command, string? arg);
}
