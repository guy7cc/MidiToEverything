using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;
using MidiToEverything.App.ViewModels.Editing;
using MidiToEverything.Infrastructure.Input;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace MidiToEverything.App;

/// <summary>
/// Validates the editor's signal fields so invalid entries can be flagged in red. "Operable"
/// means the value actually produces the intended match/action.
/// </summary>
public static class SignalValidation
{
    /// <summary>Note/CC number: empty (= any) or an integer 0..127.</summary>
    public static bool Number(string? text)
    {
        var t = (text ?? string.Empty).Trim();
        return t.Length == 0 || (int.TryParse(t, out var n) && n is >= 0 and <= 127);
    }

    /// <summary>MIDI channel: "any"/empty or an integer 1..16.</summary>
    public static bool Channel(string? text)
    {
        var t = (text ?? string.Empty).Trim();
        return t.Length == 0
            || t.Equals("any", StringComparison.OrdinalIgnoreCase)
            || (int.TryParse(t, out var n) && n is >= 1 and <= 16);
    }

    /// <summary>Device: "*"/empty or a valid regex.</summary>
    public static bool Device(string? text)
    {
        var t = (text ?? string.Empty).Trim();
        if (t.Length == 0 || t == "*")
        {
            return true;
        }

        try
        {
            _ = Regex.Match(string.Empty, t);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Detail: a value the selected action can actually use.</summary>
    public static bool Detail(EditableActionKind kind, string? detail)
    {
        var d = (detail ?? string.Empty).Trim();
        var lower = d.ToLowerInvariant();
        return kind switch
        {
            EditableActionKind.Key => d.Length > 0 && SplitKeys(d).All(t => KeyCodes.TryResolve(t, out _, out _)),
            EditableActionKind.MouseClick => lower.Contains("left") || lower.Contains("right") || lower.Contains("middle"),
            EditableActionKind.Scroll => lower is "vertical" or "horizontal",
            EditableActionKind.CursorMove => lower is "relative" or "absolute",
            EditableActionKind.WindowControl => lower is "minimize" or "maximize" or "restore" or "close" or "topmost",
            EditableActionKind.MediaKey => lower is "playpause" or "next" or "previous" or "stop" or "mute" or "volumeup" or "volumedown",
            EditableActionKind.TypeText => d.Length > 0,
            EditableActionKind.Launch => d.Length > 0,
            EditableActionKind.SetVolume => lower is "master" or "microphone",
            EditableActionKind.SwitchProfile => d.Length > 0,
            EditableActionKind.None => true,
            _ => true,
        };
    }

    private static string[] SplitKeys(string detail) => detail
        .Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static Brush BrushFor(bool valid)
    {
        var key = valid ? "TextBrush" : "DangerBrush";
        return Application.Current?.Resources[key] as Brush ?? Brushes.White;
    }
}

/// <summary>Red when the number is invalid, normal otherwise.</summary>
public sealed class NumberValidBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => SignalValidation.BrushFor(SignalValidation.Number(value as string));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Red when the channel is invalid.</summary>
public sealed class ChannelValidBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => SignalValidation.BrushFor(SignalValidation.Channel(value as string));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Red when the device regex is invalid.</summary>
public sealed class DeviceValidBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => SignalValidation.BrushFor(SignalValidation.Device(value as string));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Red when the detail is not usable by the selected action. Values: [detail, actionKind].</summary>
public sealed class DetailValidBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[1] is EditableActionKind kind)
        {
            return SignalValidation.BrushFor(SignalValidation.Detail(kind, values[0] as string));
        }

        return SignalValidation.BrushFor(true);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
