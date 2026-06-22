using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;

namespace MidiToEverything.Infrastructure.Plugins;

/// <summary>
/// Discovers and instantiates <see cref="IActionPlugin"/>s from DLLs in a folder (docs/05 §5,
/// Phase 4). Each plugin loads in its own <see cref="AssemblyLoadContext"/>; shared contracts
/// (MidiToEverything.Core) resolve to the already-loaded host copy so types match.
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader>? logger = null)
        => _logger = logger ?? NullLogger<PluginLoader>.Instance;

    /// <summary>Load every plugin found in <paramref name="directory"/> (missing dir = none).</summary>
    public IReadOnlyList<IActionPlugin> LoadFromDirectory(string directory)
    {
        var plugins = new List<IActionPlugin>();
        if (!Directory.Exists(directory))
        {
            return plugins;
        }

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
        {
            try
            {
                var context = new PluginLoadContext(dll);
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
                plugins.AddRange(Instantiate(assembly));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin assembly {Dll}", dll);
            }
        }

        if (plugins.Count > 0)
        {
            _logger.LogInformation("Loaded {Count} action plugin(s): {Ids}",
                plugins.Count, string.Join(", ", plugins.Select(p => p.Id)));
        }

        return plugins;
    }

    /// <summary>Instantiate every public, parameterless <see cref="IActionPlugin"/> in the assembly.</summary>
    public static IEnumerable<IActionPlugin> Instantiate(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (type is null || type.IsAbstract || !type.IsClass
                || !typeof(IActionPlugin).IsAssignableFrom(type)
                || type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            IActionPlugin? plugin = null;
            try
            {
                plugin = (IActionPlugin?)Activator.CreateInstance(type);
            }
            catch
            {
                // skip a plugin whose constructor throws
            }

            if (plugin is not null)
            {
                yield return plugin;
            }
        }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath)
            : base(isCollectible: false) => _resolver = new AssemblyDependencyResolver(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Resolve plugin-private deps from its folder; let shared contracts (Core, already
            // loaded by the host) fall through to the default context by returning null.
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
