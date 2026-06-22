using System.Globalization;
using System.Windows.Data;
using MidiToEverything.App.ViewModels.Editing;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App;

/// <summary>Concise Japanese descriptions of MIDI signal kinds and editor options (help text).</summary>
public static class EditorHelp
{
    public static string SignalKind(SignalKind kind) => kind switch
    {
        Core.Domain.SignalKind.NoteOn => "鍵盤やパッドを押した瞬間に発火",
        Core.Domain.SignalKind.NoteOff => "鍵盤やパッドを離した瞬間に発火",
        Core.Domain.SignalKind.Note => "押した時と離した時の両方（押している間の動作=ホールド用）",
        Core.Domain.SignalKind.Cc => "ノブ/フェーダー等を動かした時に発火（0〜127の連続値）",
        Core.Domain.SignalKind.PitchBend => "ピッチベンドホイールを動かした時に発火（番号なし）",
        Core.Domain.SignalKind.ProgramChange => "音色/プログラムを切り替えた時に発火",
        _ => "",
    };

    /// <summary>Short tag shown after the name in the dropdown (e.g. "NoteOn（押した時）").</summary>
    public static string SignalKindTag(SignalKind kind) => kind switch
    {
        Core.Domain.SignalKind.NoteOn => "押した時",
        Core.Domain.SignalKind.NoteOff => "離した時",
        Core.Domain.SignalKind.Note => "押下/離す 両方",
        Core.Domain.SignalKind.Cc => "ノブ/フェーダー",
        Core.Domain.SignalKind.PitchBend => "ベンドホイール",
        Core.Domain.SignalKind.ProgramChange => "音色切替",
        _ => "",
    };

    public static string TriggerMode(TriggerMode mode) => mode switch
    {
        Core.Domain.TriggerMode.Trigger => "しきい値以上で1回だけ発火（ボタン的な単発動作）",
        Core.Domain.TriggerMode.Hold => "押している間だけ作用（NoteOnで押下、NoteOffで解放）",
        Core.Domain.TriggerMode.Absolute => "入力値(0〜127)を連続量として扱う（フェーダー→スクロール量など）",
        Core.Domain.TriggerMode.Relative => "値の増減を相対量として扱う（エンドレスエンコーダ向け）",
        _ => "",
    };

    public static string ActionKind(EditableActionKind kind) => kind switch
    {
        EditableActionKind.Key => "キーボード入力。詳細に「ctrl+z」のようにキーを指定",
        EditableActionKind.MouseClick => "マウスクリック。詳細に left / right / middle（x2 でダブルクリック）",
        EditableActionKind.Scroll => "ホイールスクロール。詳細に vertical / horizontal",
        EditableActionKind.CursorMove => "カーソル移動。詳細に relative（相対）/ absolute（絶対）",
        EditableActionKind.WindowControl => "最前面ウィンドウを操作。詳細に minimize / maximize / restore / close / topmost",
        EditableActionKind.MediaKey => "メディアキー。詳細に playpause / next / previous / stop / mute / volumeup / volumedown",
        EditableActionKind.TypeText => "詳細に入力した文字列をそのまま入力（定型文）。改行可",
        EditableActionKind.Launch => "アプリ/ファイル/URL を起動。詳細=対象、引数・作業ディレクトリは下の欄。要・外部起動の許可",
        EditableActionKind.SetVolume => "音量を入力値で設定（フェーダー=Absolute推奨）。詳細に master / microphone",
        EditableActionKind.Uia => "別ウィンドウのUI要素を操作。要素名＋対象ウィンドウ＋動作(invoke/toggle/setvalue)。「要素を取得」でカーソル下の要素を取込",
        EditableActionKind.VirtualDesktop => "仮想デスクトップ切替（Win+Ctrl+矢印）。詳細に next / previous",
        EditableActionKind.WindowsToggle => "Windows設定を切替。詳細に darkmode（ダーク/ライト テーマ）",
        EditableActionKind.Brightness => "画面輝度を入力値で設定（フェーダー=Absolute推奨）。ノートPC等の内蔵ディスプレイ対応",
        EditableActionKind.Http => "HTTPリクエスト/Webhook送信。詳細=URL、メソッド・本文は下の欄（Home Assistant / IFTTT 等）",
        EditableActionKind.Osc => "OSCメッセージ送信(UDP)。詳細=アドレス(例 /1/fader1)、宛先 host:port と引数は下の欄",
        EditableActionKind.SwitchProfile => "プロファイル切替。詳細に next / prev / toggle / プロファイルID",
        EditableActionKind.None => "何もしない（基本プロファイルの同じ割当を無効化＝ブロック）",
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

/// <summary>Binds an <see cref="EditableActionKind"/> to the suggested "detail" values.</summary>
public sealed class ActionDetailCandidatesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is EditableActionKind k ? EditorHelp.DetailCandidates(k) : Array.Empty<string>();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
