using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>
/// The profile layers in effect for a given moment, ordered highest priority first
/// when iterated (docs/02_Architecture.md §3.2):
/// pinned (manual hold) &gt; context (active window) &gt; base (global).
/// </summary>
/// <param name="Base">The always-on base/global profile (FR-6.1).</param>
/// <param name="Context">Profile matched to the foreground window, if any.</param>
/// <param name="Pinned">Manually pinned profile overriding auto-switch, if any (FR-5.5).</param>
public readonly record struct ProfileLayers(
    Profile Base,
    Profile? Context = null,
    Profile? Pinned = null)
{
    /// <summary>Layers from highest to lowest priority, nulls skipped.</summary>
    public IEnumerable<Profile> InPriorityOrder()
    {
        if (Pinned is { } p) yield return p;
        if (Context is { } c) yield return c;
        yield return Base;
    }
}

/// <summary>
/// Pure resolution of a <see cref="MidiMessage"/> against layered profiles. The first
/// layer (highest priority) that defines a matching binding decides the outcome:
/// a real binding overrides lower layers (FR-6.2); an explicit block stops fallback
/// (FR-6.4); a layer with no match falls through to the next (FR-6.3).
///
/// Stateless and UI/OS-free, so the entire conflict-resolution behavior is unit-testable
/// (docs/04_Roadmap.md M1).
/// </summary>
public sealed class MappingResolver
{
    public MappingResolution Resolve(MidiMessage message, ProfileLayers layers)
    {
        foreach (var profile in layers.InPriorityOrder())
        {
            if (!profile.Enabled)
            {
                continue;
            }

            var binding = profile.FindBestMatch(message);
            if (binding is null)
            {
                continue; // layer defines nothing — fall through (FR-6.3)
            }

            return binding.IsBlock
                ? MappingResolution.Blocked(profile.Id)  // explicit block — no fallback (FR-6.4)
                : MappingResolution.Resolved(binding, profile.Id); // override / base hit (FR-6.2)
        }

        return MappingResolution.NoMatch;
    }
}
