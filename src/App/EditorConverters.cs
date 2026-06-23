using System.Globalization;
using System.Windows;
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

    /// <summary>What the signal sends to the trigger (label next to the signal→trigger arrow).</summary>
    public static string SignalFlow(SignalKind kind) => Loc.T($"signalFlow.{kind}");

    public static string TriggerMode(TriggerMode mode) => Loc.T($"help.trigger.{mode}");

    public static string ActionKind(EditableActionKind kind) => Loc.T($"help.action.{kind}");

    /// <summary>Localized display name for a trigger mode (shown in the editor dropdown).</summary>
    public static string TriggerModeName(TriggerMode mode) => Loc.T($"trigger.{mode}");

    /// <summary>Localized display name for an action kind (dropdown, bindings list, config header).</summary>
    public static string ActionKindName(EditableActionKind kind) => Loc.T($"action.{kind}");

    /// <summary>Localized display name for an Absolute out-of-range behavior (clamp / gate).</summary>
    public static string OutOfRangeName(OutOfRangeBehavior behavior) => Loc.T($"outOfRange.{behavior}");

    /// <summary>Localized display name for how a Relative trigger decodes its delta.</summary>
    public static string RelativeFormatName(RelativeFormat format) => Loc.T($"relativeFormat.{format}");

    /// <summary>
    /// Localized RelativeOutput label, phrased for the source: rotation (right/left turn) for an
    /// encoder, or value change (increased/decreased) for AbsoluteDelta.
    /// </summary>
    public static string RelativeOutputName(RelativeOutput output, RelativeFormat format)
    {
        var abs = format == RelativeFormat.AbsoluteDelta;
        return output switch
        {
            RelativeOutput.FireOnIncrease => Loc.T(abs ? "relOut.abs.up" : "relOut.enc.right"),
            RelativeOutput.FireOnDecrease => Loc.T(abs ? "relOut.abs.down" : "relOut.enc.left"),
            RelativeOutput.FireOnEither => Loc.T("relOut.either"),
            _ => Loc.T("relOut.amount"),
        };
    }

    /// <summary>How the selected action consumes the action amount it receives.</summary>
    public static string ActionAmountUsage(EditableActionKind kind) => kind switch
    {
        EditableActionKind.Scroll => Loc.T("help.amount.scroll"),
        EditableActionKind.CursorMove => Loc.T("help.amount.cursor"),
        EditableActionKind.SetVolume => Loc.T("help.amount.volume"),
        EditableActionKind.Brightness => Loc.T("help.amount.brightness"),
        EditableActionKind.MidiOut => Loc.T("help.amount.midiout"),
        _ => Loc.T("help.amount.none"),
    };

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
            "ctrl+-", "ctrl+=", "ctrl+0",
            "space", "enter", "tab", "esc", "delete", "backspace",
            "up", "down", "left", "right", "f1", "f2", "f5",
            "-", "=", "/", "numpad1", "add", "subtract",
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

/// <summary>"What flows" label next to the signal→trigger arrow.</summary>
public sealed class SignalFlowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SignalKind k ? EditorHelp.SignalFlow(k) : "";

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

/// <summary>Dropdown label: localized Absolute out-of-range behavior name.</summary>
public sealed class OutOfRangeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is OutOfRangeBehavior b ? EditorHelp.OutOfRangeName(b) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dropdown label: localized relative-encoder format name.</summary>
public sealed class RelativeFormatNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is RelativeFormat f ? EditorHelp.RelativeFormatName(f) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dropdown label for a RelativeOutput, phrased for the current RelativeFormat (rotation vs value).</summary>
public sealed class RelativeOutputLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2 && values[0] is RelativeOutput o && values[1] is RelativeFormat f
            ? EditorHelp.RelativeOutputName(o, f)
            : "";

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Action-side explanation of how the received action amount is used.</summary>
public sealed class ActionAmountUsageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EditableActionKind k ? EditorHelp.ActionAmountUsage(k) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Shows a trigger field only in the modes that actually use it (parameter = field key), so the
/// editor hides settings that have no effect in the current <see cref="TriggerMode"/>. Mirrors
/// the engine: Trigger/Hold use threshold; Absolute uses range/out-of-range; Absolute+Relative
/// use dead zone/scale/invert; Relative uses the signed format; edge applies to Trigger/Absolute.
/// </summary>
public sealed class ModeFieldVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TriggerMode m || parameter is not string key)
        {
            return Visibility.Collapsed;
        }

        var visible = key switch
        {
            "threshold" => m is TriggerMode.Trigger or TriggerMode.Hold,
            "rangeMin" or "rangeMax" or "outOfRange" => m is TriggerMode.Absolute,
            "deadzone" or "scale" or "invert" => m is TriggerMode.Absolute or TriggerMode.Relative,
            "relativeFormat" or "relativeOutput" => m is TriggerMode.Relative,
            "edge" => m is TriggerMode.Trigger or TriggerMode.Absolute,
            "wrap" => m is TriggerMode.RelativeFromAbsolute,
            _ => false,
        };

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

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
