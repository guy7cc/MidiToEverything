namespace MidiToEverything.Core.Domain;

/// <summary>
/// A concrete MIDI message received from a device, normalized for the mapping engine.
/// This is the runtime input to <see cref="Mapping.MappingResolver"/> — distinct from
/// the wildcard-capable <see cref="Signal"/> pattern stored in profiles.
/// </summary>
/// <param name="Device">Source device identifier (name-based).</param>
/// <param name="Channel">MIDI channel, 1..16.</param>
/// <param name="Type">Concrete message type.</param>
/// <param name="Number">Note/CC number (0..127), or null for PitchBend.</param>
/// <param name="Value">
/// Velocity (Note), controller value (CC, 0..127), or 14-bit value (PitchBend, 0..16383).
/// </param>
public sealed record MidiMessage(
    string Device,
    int Channel,
    MidiMessageType Type,
    int? Number,
    int Value);
