using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>Identity of a physical control, used to track per-control rising-edge state.</summary>
public readonly record struct ControlKey(string Device, int Channel, MidiMessageType Type, int? Number);

/// <summary>
/// Stateful rising-edge filter for value-gated triggers (docs/02_Architecture.md §3.2). A
/// <see cref="TriggerMode.Trigger"/> threshold or an <see cref="TriggerMode.Absolute"/> gate
/// keeps firing on every message while the control sits in its active zone; with
/// <see cref="Trigger.Edge"/> set this suppresses the repeats, emitting once when the control
/// enters the zone and again only after it leaves and re-enters.
///
/// Single-threaded use only (the pipeline worker reads one message at a time); not thread-safe.
/// State is keyed by physical control so it survives profile reloads and is bounded by the
/// number of distinct controls seen.
/// </summary>
public sealed class EdgeGate
{
    private readonly Dictionary<ControlKey, bool> _active = new();

    /// <summary>
    /// Decide whether a binding should emit for this evaluation. For non-edge triggers (and the
    /// event-driven Hold/Relative modes) this just mirrors <paramref name="result"/>.ShouldFire;
    /// for an edge trigger it returns true only on the rising edge into the active zone.
    /// </summary>
    public bool ShouldEmit(Trigger trigger, MidiMessage message, TriggerResult result)
    {
        var edge = trigger.Edge && trigger.Mode is TriggerMode.Trigger or TriggerMode.Absolute;
        if (!edge)
        {
            return result.ShouldFire;
        }

        var key = new ControlKey(message.Device, message.Channel, message.Type, message.Number);
        var firing = result.ShouldFire;
        var wasFiring = _active.TryGetValue(key, out var prev) && prev;
        _active[key] = firing;

        // Emit only on the transition from not-firing to firing.
        return firing && !wasFiring;
    }

    /// <summary>Forget all tracked state (e.g. on profile reload, to start edges fresh).</summary>
    public void Reset() => _active.Clear();
}
