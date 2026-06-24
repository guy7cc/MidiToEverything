using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>
/// Supplies the rules active right now: the base rule plus every rule whose match regex matches the
/// foreground window, plus any force-enabled rules. The pipeline reads this per message.
/// <see cref="ProfileManager"/> owns selection from the active window; <see cref="MutableMappingContext"/>
/// lets tests set it directly.
/// </summary>
public interface IMappingContext
{
    ActiveRules Current { get; }
}

/// <summary>
/// Thread-safe, settable <see cref="IMappingContext"/>. The struct holds a list reference, so
/// reads/writes are guarded by a lock rather than relying on atomicity.
/// </summary>
public sealed class MutableMappingContext : IMappingContext
{
    private readonly object _gate = new();
    private ActiveRules _active;

    public MutableMappingContext(ActiveRules initial) => _active = initial;

    public ActiveRules Current
    {
        get { lock (_gate) { return _active; } }
    }

    public void Set(ActiveRules active)
    {
        lock (_gate) { _active = active; }
    }
}
