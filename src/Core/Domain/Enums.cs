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

    /// <summary>Interpret the value as a signed delta (endless encoder).</summary>
    Relative,
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
}
