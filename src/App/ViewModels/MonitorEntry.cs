using MidiToEverything.Core.Domain;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace MidiToEverything.App.ViewModels;

/// <summary>One row in the input monitor log (FR-2.1). Immutable; built off the UI thread is fine.</summary>
public sealed class MonitorEntry
{
    public MonitorEntry(MidiMessage message, string time, string? action = null)
    {
        Time = time;
        Source = $"{message.Device}  ch{message.Channel:00}";
        Detail = Describe(message);
        Accent = AccentFor(message.Type);
        Action = action ?? "";
    }

    public string Time { get; }
    public string Source { get; }
    public string Detail { get; }
    public Brush Accent { get; }

    /// <summary>Label of the action this input maps to in the active profile layers ("" if none).</summary>
    public string Action { get; }

    /// <summary>Arrowed action label for display, or empty when no action matches.</summary>
    public string ActionLabel => Action.Length == 0 ? "" : $"→ {Action}";

    private static string Describe(MidiMessage m) => m.Type switch
    {
        MidiMessageType.NoteOn => $"NoteOn   {m.Number} {NoteName(m.Number!.Value)}  vel {m.Value}",
        MidiMessageType.NoteOff => $"NoteOff  {m.Number} {NoteName(m.Number!.Value)}",
        MidiMessageType.ControlChange => $"CC {m.Number}  = {m.Value}",
        MidiMessageType.PitchBend => $"PitchBend = {m.Value}",
        MidiMessageType.ProgramChange => $"Program  = {m.Number}",
        _ => m.Type.ToString(),
    };

    private static Brush AccentFor(MidiMessageType type) => type switch
    {
        MidiMessageType.NoteOn => Brushes.MediumSeaGreen,
        MidiMessageType.NoteOff => Brushes.Gray,
        MidiMessageType.ControlChange => Brushes.DeepSkyBlue,
        MidiMessageType.PitchBend => Brushes.Orchid,
        MidiMessageType.ProgramChange => Brushes.Goldenrod,
        _ => Brushes.LightGray,
    };

    public static string NoteName(int note)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        return $"{names[note % 12]}{(note / 12) - 1}";
    }
}
