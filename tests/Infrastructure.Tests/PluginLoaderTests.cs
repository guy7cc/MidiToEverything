using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Infrastructure.Plugins;

namespace MidiToEverything.Infrastructure.Tests;

public class PluginLoaderTests
{
    [Fact]
    public void Instantiate_FindsPublicParameterlessPlugins()
    {
        var plugins = PluginLoader.Instantiate(typeof(PluginLoaderTests).Assembly).ToList();

        Assert.Contains(plugins, p => p.Id == "sample-test-plugin");
    }

    [Fact]
    public void Instantiate_SkipsTypesNeedingConstructorArgs()
    {
        var plugins = PluginLoader.Instantiate(typeof(PluginLoaderTests).Assembly).ToList();

        Assert.DoesNotContain(plugins, p => p is NeedsCtorPlugin);
    }

    [Fact]
    public void LoadFromDirectory_MissingFolder_ReturnsEmpty()
    {
        var loaded = new PluginLoader().LoadFromDirectory(Path.Combine(Path.GetTempPath(), "no-such-plugins-dir-xyz"));

        Assert.Empty(loaded);
    }
}

/// <summary>A discoverable plugin used to exercise <see cref="PluginLoader.Instantiate"/>.</summary>
public sealed class SampleTestPlugin : IActionPlugin
{
    public string Id => "sample-test-plugin";
    public void Execute(string command, string? arg) { }
}

/// <summary>Should NOT be instantiated (no parameterless constructor).</summary>
public sealed class NeedsCtorPlugin : IActionPlugin
{
    public NeedsCtorPlugin(int x) => Id = x.ToString();
    public string Id { get; }
    public void Execute(string command, string? arg) { }
}
