using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Infrastructure.Midi;

/// <summary>
/// Normalizes DryWetMIDI <see cref="MidiEvent"/>s into the engine's <see cref="MidiMessage"/>
/// (docs/02_Architecture.md §3.3). Pure and device-free, so it is unit-tested without hardware.
/// </summary>
public static class MidiMessageFactory
{
    /// <summary>
    /// Maps a supported channel event to a <see cref="MidiMessage"/>. A NoteOn with zero
    /// velocity is normalized to NoteOff (a common "note off" encoding). Returns false for
    /// event types the engine does not act on.
    /// </summary>
    public static bool TryCreate(MidiEvent midiEvent, string device, out MidiMessage message)
    {
        switch (midiEvent)
        {
            case NoteOnEvent noteOn when (int)noteOn.Velocity == 0:
                message = new MidiMessage(device, Channel(noteOn.Channel), MidiMessageType.NoteOff, (int)noteOn.NoteNumber, 0);
                return true;

            case NoteOnEvent noteOn:
                message = new MidiMessage(device, Channel(noteOn.Channel), MidiMessageType.NoteOn, (int)noteOn.NoteNumber, (int)noteOn.Velocity);
                return true;

            case NoteOffEvent noteOff:
                message = new MidiMessage(device, Channel(noteOff.Channel), MidiMessageType.NoteOff, (int)noteOff.NoteNumber, (int)noteOff.Velocity);
                return true;

            case ControlChangeEvent cc:
                message = new MidiMessage(device, Channel(cc.Channel), MidiMessageType.ControlChange, (int)cc.ControlNumber, (int)cc.ControlValue);
                return true;

            case PitchBendEvent pitch:
                message = new MidiMessage(device, Channel(pitch.Channel), MidiMessageType.PitchBend, null, pitch.PitchValue);
                return true;

            case ProgramChangeEvent program:
                message = new MidiMessage(device, Channel(program.Channel), MidiMessageType.ProgramChange, (int)program.ProgramNumber, (int)program.ProgramNumber);
                return true;

            default:
                message = null!;
                return false;
        }
    }

    /// <summary>DryWetMIDI channels are 0..15; the engine uses 1..16.</summary>
    private static int Channel(FourBitNumber channel) => (int)channel + 1;
}
