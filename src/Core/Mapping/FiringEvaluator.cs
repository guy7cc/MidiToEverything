using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Mapping;

/// <summary>A binding that actually fired for a message, paired with its trigger result.</summary>
public readonly record struct Firing(Binding Binding, TriggerResult Trigger);

/// <summary>
/// Resolves a <see cref="MidiMessage"/> to the bindings that actually <em>fire</em> for it —
/// applying signal matching, trigger evaluation, the stateful AbsoluteDelta diff, and the
/// rising-edge gate, exactly as the hot path does. Shared by <c>MidiEventPipeline</c> (which then
/// executes each firing) and the input monitor (which displays them), so the monitor shows what
/// truly fired rather than every binding whose signal merely matched.
///
/// Stateful per physical control (owns a <see cref="DeltaTracker"/> and <see cref="EdgeGate"/>);
/// feed messages in order. Not thread-safe — use one instance per single-threaded consumer.
/// </summary>
public sealed class FiringEvaluator
{
    private readonly DeltaTracker _delta = new();
    private readonly EdgeGate _edge = new();

    /// <summary>
    /// The bindings that fire for this message — the union across <em>all</em> active rules. Within
    /// each rule only the highest-specificity matches are kept (<see cref="Profile.FindAllMatches"/>);
    /// across rules they co-fire (no priority override). <c>none</c>/block bindings are inert and emit
    /// nothing. Empty when nothing fires.
    /// </summary>
    public IReadOnlyList<Firing> Evaluate(MidiMessage message, ActiveRules active)
    {
        var bindings = new List<Binding>();
        foreach (var rule in active.Rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            foreach (var binding in rule.FindAllMatches(message))
            {
                if (!binding.IsBlock) // `none` is inert in the union model (nothing to block)
                {
                    bindings.Add(binding);
                }
            }
        }

        if (bindings.Count == 0)
        {
            return Array.Empty<Firing>();
        }

        var fired = new List<Firing>();

        // AbsoluteDelta is stateful per control: advance its baseline once per message and reuse
        // the raw delta for every co-winning binding (each applies its own Wrap).
        int? absDelta = null;
        var absAdvanced = false;

        foreach (var binding in bindings)
        {
            TriggerResult trigger;
            if (binding.Trigger is { Mode: TriggerMode.Relative, RelativeFormat: RelativeFormat.AbsoluteDelta })
            {
                if (!absAdvanced)
                {
                    absDelta = _delta.Advance(message);
                    absAdvanced = true;
                }

                trigger = absDelta is { } raw
                    ? TriggerEvaluator.RelativeResult(binding.Trigger, DeltaTracker.ApplyWrap(binding.Trigger, raw))
                    : TriggerResult.None;
            }
            else
            {
                trigger = TriggerEvaluator.Evaluate(binding.Trigger, message);
            }

            // EdgeGate collapses repeated in-zone fires to a single rising edge when Trigger.Edge
            // is set; otherwise it just mirrors ShouldFire.
            if (_edge.ShouldEmit(binding.Trigger, message, trigger))
            {
                fired.Add(new Firing(binding, trigger));
            }
        }

        return fired;
    }
}
