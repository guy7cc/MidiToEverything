namespace MidiToEverything.Core.Domain;

/// <summary>
/// Concrete MIDI message type as received from a device. Unlike <see cref="SignalKind"/>
/// there is no combined "Note" — a wire message is always a specific NoteOn or NoteOff.
/// </summary>
public enum MidiMessageType
{
    NoteOn,
    NoteOff,
    ControlChange,
    PitchBend,
    ProgramChange,
}

/// <summary>
/// The message kind a <see cref="Signal"/> pattern matches against. Includes the
/// convenience <see cref="Note"/> which matches both NoteOn and NoteOff.
/// Mirrors the schema "type" field (see docs/03_ProfileSchema.md §1).
/// </summary>
public enum SignalKind
{
    NoteOn,
    NoteOff,
    Note,
    Cc,
    PitchBend,
    ProgramChange,
}

/// <summary>
/// How an incoming value is interpreted before it drives an action
/// (docs/03_ProfileSchema.md §2).
/// </summary>
public enum TriggerMode
{
    /// <summary>Fire once when value reaches the threshold (buttons/pads).</summary>
    Trigger,

    /// <summary>Press on NoteOn (value >= threshold), release on NoteOff.</summary>
    Hold,

    /// <summary>Map the absolute value within a range to a continuous magnitude.</summary>
    Absolute,

    /// <summary>
    /// Interpret the change as a signed delta. How the delta is decoded is chosen by
    /// <see cref="RelativeFormat"/> (endless-encoder encodings or <see cref="RelativeFormat.AbsoluteDelta"/>);
    /// what to do with it (send as amount, or fire on a direction) by <see cref="RelativeOutput"/>.
    /// </summary>
    Relative,

    /// <summary>
    /// Legacy mode kept only so older config files still load; on read it is migrated to
    /// <see cref="Relative"/> with <see cref="RelativeFormat.AbsoluteDelta"/>. Not selectable in
    /// the editor and never reaches the engine.
    /// </summary>
    RelativeFromAbsolute,
}

/// <summary>
/// What a <see cref="TriggerMode.Relative"/> trigger does with the decoded increase/decrease:
/// send it as an action amount, or fire (like a button) on a single direction.
/// </summary>
public enum RelativeOutput
{
    /// <summary>Send the signed delta as an action amount (an amount trigger).</summary>
    Amount,

    /// <summary>Fire once per increase tick; ignore decreases (a fire trigger).</summary>
    FireOnIncrease,

    /// <summary>Fire once per decrease tick; ignore increases (a fire trigger).</summary>
    FireOnDecrease,
}

/// <summary>
/// What an <see cref="TriggerMode.Absolute"/> trigger does with values that fall outside its
/// <c>[RangeMin, RangeMax]</c> window (docs/03_ProfileSchema.md §2).
/// </summary>
public enum OutOfRangeBehavior
{
    /// <summary>Snap out-of-range values to the nearest edge and keep firing (the legacy default).</summary>
    Clamp,

    /// <summary>Fire only while the value is inside the window; outside produces no emission.</summary>
    Gate,
}

/// <summary>Signed-value encoding used by relative (endless) encoders.</summary>
public enum RelativeFormat
{
    /// <summary>1..63 = +1..+63, 65..127 = -63..-1 (value - 128), 64/0 = 0.</summary>
    TwosComplement,

    /// <summary>Bit 6 (0x40) is the sign, low 6 bits are magnitude.</summary>
    SignedBit,

    /// <summary>value - 64 (64 = no change).</summary>
    BinaryOffset,

    /// <summary>
    /// Not an encoder encoding: treat the change of an absolute value (current − previous) as the
    /// delta, so a plain absolute knob/fader drives relative actions. Stateful (handled by
    /// <see cref="Mapping.DeltaTracker"/>); set <see cref="Trigger.Wrap"/> for endless knobs that
    /// wrap 127→0.
    /// </summary>
    AbsoluteDelta,
}
