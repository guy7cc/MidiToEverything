using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>
/// Supplies the profile layers in effect right now (base + context + pinned). The pipeline
/// reads this per message. ProfileManager (M6) will own selection from the active window;
/// for now <see cref="MutableMappingContext"/> lets callers set it directly.
/// </summary>
public interface IMappingContext
{
    ProfileLayers Current { get; }
}

/// <summary>
/// Thread-safe, settable <see cref="IMappingContext"/>. The struct holds multiple
/// references, so reads/writes are guarded by a lock rather than relying on atomicity.
/// </summary>
public sealed class MutableMappingContext : IMappingContext
{
    private readonly object _gate = new();
    private ProfileLayers _layers;

    public MutableMappingContext(ProfileLayers initial) => _layers = initial;

    public ProfileLayers Current
    {
        get { lock (_gate) { return _layers; } }
    }

    public void Set(ProfileLayers layers)
    {
        lock (_gate) { _layers = layers; }
    }
}
