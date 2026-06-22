using Microsoft.Extensions.Logging;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App;

/// <summary>
/// Starts and stops the runtime as one unit (docs/02_Architecture.md §2): the MIDI source,
/// the foreground watcher (via <see cref="ProfileManager"/>), and the mapping pipeline, and
/// routes MIDI-driven profile switches into the manager.
/// </summary>
public sealed class EngineCoordinator : IAsyncDisposable
{
    private readonly IMidiSource _source;
    private readonly ProfileManager _profiles;
    private readonly MidiEventPipeline _pipeline;
    private readonly ILogger<EngineCoordinator> _logger;
    private bool _started;

    public EngineCoordinator(
        IMidiSource source,
        ProfileManager profiles,
        MidiEventPipeline pipeline,
        ILogger<EngineCoordinator> logger)
    {
        _source = source;
        _profiles = profiles;
        _pipeline = pipeline;
        _logger = logger;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _pipeline.ProfileSwitchRequested += OnProfileSwitchRequested;
        _pipeline.Start();   // subscribes to the source before it emits
        _source.Start();
        _profiles.Start();   // starts the foreground watcher and seeds the active profile
        _logger.LogInformation("Engine started.");
    }

    private void OnProfileSwitchRequested(object? sender, SwitchProfileAction action)
        => _profiles.HandleSwitch(action);

    public async ValueTask DisposeAsync()
    {
        if (!_started)
        {
            return;
        }

        _pipeline.ProfileSwitchRequested -= OnProfileSwitchRequested;
        await _pipeline.DisposeAsync();
        _profiles.Stop();
        _source.Stop();
        _logger.LogInformation("Engine stopped.");
    }
}
