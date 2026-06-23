using System.Text.RegularExpressions;

namespace MidiToEverything.Core.Domain;

/// <summary>
/// A wildcard-capable address that selects which <see cref="MidiMessage"/>s a binding
/// reacts to. <see cref="Device"/> may be "*" (any) and <see cref="Channel"/> may be
/// "any". See docs/03_ProfileSchema.md §1.
/// </summary>
public sealed record Signal
{
    public const string AnyDevice = "*";
    public const string AnyChannel = "any";

    /// <summary>Device-name regex, or "*"/empty for any device (matched case-insensitively).</summary>
    public string Device { get; init; } = AnyDevice;

    /// <summary>MIDI channel as "any" or "1".."16".</summary>
    public string Channel { get; init; } = AnyChannel;

    /// <summary>Message kind this pattern matches.</summary>
    public SignalKind Type { get; init; }

    /// <summary>Note/CC number, or null (e.g. for PitchBend or "match any number").</summary>
    public int? Number { get; init; }

    /// <summary>Stable string key for diagnostics/grouping (docs/03_ProfileSchema.md §1).</summary>
    public string Key => $"dev:{Device}|ch:{Channel}|{Type}:{Number?.ToString() ?? "-"}";

    /// <summary>True when this pattern matches the given concrete message.</summary>
    public bool Matches(MidiMessage message)
    {
        if (!DeviceMatches(message.Device))
        {
            return false;
        }

        if (Channel != AnyChannel)
        {
            if (!int.TryParse(Channel, out var ch) || ch != message.Channel)
            {
                return false;
            }
        }

        if (!TypeMatches(Type, message.Type))
        {
            return false;
        }

        // PitchBend has no number. Otherwise a null Number is a wildcard over numbers.
        if (Type != SignalKind.PitchBend && Number is { } n && n != message.Number)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// True when <paramref name="message"/> is the Note Off that releases this NoteOn signal
    /// (same device/channel/number). A Hold trigger uses this so the held key is released when the
    /// device sends an explicit Note Off rather than NoteOn velocity 0 (docs/03_ProfileSchema.md §2).
    /// </summary>
    public bool MatchesNoteRelease(MidiMessage message)
    {
        if (Type != SignalKind.NoteOn || message.Type != MidiMessageType.NoteOff)
        {
            return false;
        }

        if (!DeviceMatches(message.Device))
        {
            return false;
        }

        if (Channel != AnyChannel && (!int.TryParse(Channel, out var ch) || ch != message.Channel))
        {
            return false;
        }

        return Number is null || Number == message.Number;
    }

    /// <summary>
    /// Specificity score used to disambiguate when several bindings in the same profile
    /// match one message: the most concrete (fewest wildcards) wins.
    /// </summary>
    public int Specificity
    {
        get
        {
            var score = 0;
            if (Device != AnyDevice) score++;
            if (Channel != AnyChannel) score++;
            if (Number is not null) score++;
            if (Type != SignalKind.Note) score++; // Note (on+off) is broader than a specific kind
            return score;
        }
    }

    private bool DeviceMatches(string deviceName)
    {
        if (Device == AnyDevice || string.IsNullOrEmpty(Device))
        {
            return true;
        }

        try
        {
            return Regex.IsMatch(deviceName, Device, RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // invalid regex matches nothing
        }
    }

    private static bool TypeMatches(SignalKind kind, MidiMessageType type) => kind switch
    {
        SignalKind.NoteOn => type == MidiMessageType.NoteOn,
        SignalKind.NoteOff => type == MidiMessageType.NoteOff,
        SignalKind.Note => type is MidiMessageType.NoteOn or MidiMessageType.NoteOff,
        SignalKind.Cc => type == MidiMessageType.ControlChange,
        SignalKind.PitchBend => type == MidiMessageType.PitchBend,
        SignalKind.ProgramChange => type == MidiMessageType.ProgramChange,
        _ => false,
    };
}
