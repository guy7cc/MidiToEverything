using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Core.Application;

/// <summary>Holds loaded action plugins, keyed by their id (docs/05 §5, Phase 4).</summary>
public sealed class PluginRegistry
{
    private readonly Dictionary<string, IActionPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IActionPlugin plugin) => _plugins[plugin.Id] = plugin;

    public IActionPlugin? Get(string id) => _plugins.GetValueOrDefault(id);

    /// <summary>Ids of all loaded plugins (for diagnostics / editor hints).</summary>
    public IReadOnlyCollection<string> Ids => _plugins.Keys;
}
