using System.Globalization;
using System.Windows.Data;
using MidiToEverything.App.Localization;
using MidiToEverything.App.ViewModels.Editing;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App;

/// <summary>Localized descriptions of MIDI signal kinds and editor options (looked up via <see cref="Loc"/>).</summary>
public static class EditorHelp
{
    public static string SignalKind(SignalKind kind) => Loc.T($"help.signalKind.{kind}");

    /// <summary>Short tag shown after the name in the dropdown (e.g. "NoteOn（押した時）").</summary>
    public static string SignalKindTag(SignalKind kind) => Loc.T($"help.signalTag.{kind}");

    public static string TriggerMode(TriggerMode mode) => Loc.T($"help.trigger.{mode}");

    public static string ActionKind(EditableActionKind kind) => Loc.T($"help.action.{kind}");

    /// <summary>Localized display name for a trigger mode (shown in the editor dropdown).</summary>
    public static string TriggerModeName(TriggerMode mode) => Loc.T($"trigger.{mode}");

    /// <summary>Localized display name for an action kind (dropdown, bindings list, config header).</summary>
    public static string ActionKindName(EditableActionKind kind) => Loc.T($"action.{kind}");

    /// <summary>Step-by-step instructions for configuring a complex action (shown in the config dialog).</summary>
    public static string Instructions(EditableActionKind kind) => kind switch
    {
        EditableActionKind.Launch or EditableActionKind.Uia or EditableActionKind.Http
            or EditableActionKind.Osc or EditableActionKind.Obs or EditableActionKind.MidiOut
            or EditableActionKind.Macro or EditableActionKind.Toggle or EditableActionKind.Plugin
            => Loc.T($"help.instructions.{kind}"),
        _ => "",
    };

    /// <summary>Suggested "detail" values for an action kind (the combo dropdown; free text still allowed).</summary>
    public static IReadOnlyList<string> DetailCandidates(EditableActionKind kind) => kind switch
    {
        EditableActionKind.Key => new[]
        {
            "ctrl+z", "ctrl+y", "ctrl+c", "ctrl+x", "ctrl+v", "ctrl+s", "ctrl+a", "ctrl+f",
            "space", "enter", "tab", "esc", "delete", "backspace",
            "up", "down", "left", "right", "f1", "f2", "f5",
        },
        EditableActionKind.MouseClick => new[] { "left", "right", "middle", "left x2", "right x2" },
        EditableActionKind.Scroll => new[] { "vertical", "horizontal" },
        EditableActionKind.CursorMove => new[] { "relative", "absolute" },
        EditableActionKind.WindowControl => new[] { "minimize", "maximize", "restore", "close", "topmost" },
        EditableActionKind.MediaKey => new[] { "playpause", "next", "previous", "stop", "mute", "volumeup", "volumedown" },
        EditableActionKind.SetVolume => new[] { "master", "microphone" },
        EditableActionKind.VirtualDesktop => new[] { "next", "previous" },
        EditableActionKind.WindowsToggle => new[] { "darkmode" },
        EditableActionKind.SwitchProfile => new[] { "next", "prev", "toggle" },
        _ => Array.Empty<string>(),
    };
}

/// <summary>Binds a <see cref="SignalKind"/> to its "when it fires" help text.</summary>
public sealed class SignalKindHelpConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SignalKind k ? EditorHelp.SignalKind(k) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dropdown label: "NoteOn（押した時）".</summary>
public sealed class SignalKindItemConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SignalKind k ? $"{k}（{EditorHelp.SignalKindTag(k)}）" : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Binds a <see cref="TriggerMode"/> to its help text.</summary>
public sealed class TriggerModeHelpConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TriggerMode m ? EditorHelp.TriggerMode(m) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Binds an <see cref="EditableActionKind"/> to its help text.</summary>
public sealed class ActionKindHelpConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EditableActionKind k ? EditorHelp.ActionKind(k) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dropdown/list label: localized trigger-mode name (instead of the raw enum).</summary>
public sealed class TriggerModeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TriggerMode m ? EditorHelp.TriggerModeName(m) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dropdown/list label: localized action-kind name (instead of the raw enum).</summary>
public sealed class ActionKindNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EditableActionKind k ? EditorHelp.ActionKindName(k) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Binds an <see cref="EditableActionKind"/> to its configuration instructions.</summary>
public sealed class ActionInstructionsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EditableActionKind k ? EditorHelp.Instructions(k) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Binds an <see cref="EditableActionKind"/> to the suggested "detail" values.</summary>
public sealed class ActionDetailCandidatesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EditableActionKind k ? EditorHelp.DetailCandidates(k) : Array.Empty<string>();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
