using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Persistence;

/// <summary>
/// Builds the first-run configuration, matching the example in docs/03_ProfileSchema.md §5
/// so a fresh install is immediately usable and demonstrates the inheritance model.
/// </summary>
public static class DefaultConfig
{
    public static AppConfig Create() => new()
    {
        Version = ConfigMigrator.CurrentVersion,
        Settings = new AppSettings(),
        ActiveContext = new ActiveContextState { CurrentProfileId = "clip-studio" },
        BaseProfile = BaseProfile(),
        Profiles = new[] { ClipStudio(), Obs() },
    };

    private static Signal AnyNoteOn(int number) => new() { Type = SignalKind.NoteOn, Number = number };
    private static Signal AnyCc(int number) => new() { Type = SignalKind.Cc, Number = number };

    private static Profile BaseProfile() => new()
    {
        Id = "base",
        Name = "基本プロファイル",
        Bindings = new[]
        {
            new Binding
            {
                Signal = AnyNoteOn(36),
                Actions = new InputAction[] { new KeyAction(new[] { "ctrl", "z" }) },
                Label = "元に戻す",
            },
            new Binding
            {
                Signal = AnyNoteOn(37),
                Actions = new InputAction[] { new KeyAction(new[] { "ctrl", "c" }) },
                Label = "コピー",
            },
            new Binding
            {
                Signal = AnyNoteOn(51),
                Actions = new InputAction[] { new SwitchProfileAction(ProfileSwitchTarget.Next) },
                Label = "プロファイル切替(次)",
            },
        },
    };

    private static Profile ClipStudio() => new()
    {
        Id = "clip-studio",
        Name = "Clip Studio Paint",
        Match = new MatchRule { Pattern = @"^CLIPStudioPaint\.exe$", Priority = 10 },
        Bindings = new[]
        {
            new Binding
            {
                Signal = AnyCc(74),
                Trigger = new Trigger { Mode = TriggerMode.Absolute },
                Actions = new InputAction[] { new ScrollAction(ScrollAxis.Vertical, UseInputValue: true) },
                Label = "ブラシサイズ",
            },
            new Binding
            {
                Signal = AnyCc(7),
                Trigger = new Trigger { Mode = TriggerMode.Absolute },
                Actions = new InputAction[] { new CursorMoveAction(MoveMode.Absolute, UseInputValue: true) },
                Label = "不透明度スライダ",
            },
            new Binding
            {
                Signal = new Signal { Type = SignalKind.Note, Number = 40 },
                Trigger = new Trigger { Mode = TriggerMode.Hold },
                Actions = new InputAction[] { new KeyAction(new[] { "space" }, Hold: true) },
                Label = "手のひらツール(押下中)",
            },
            new Binding
            {
                Signal = AnyNoteOn(37),
                Actions = new InputAction[] { NoneAction.Instance },
                Label = "（基本のコピーを無効化）",
            },
        },
    };

    private static Profile Obs() => new()
    {
        Id = "obs",
        Name = "OBS Studio",
        Match = new MatchRule { Pattern = @"^obs64\.exe$", Priority = 5 },
        Bindings = new[]
        {
            new Binding
            {
                Signal = AnyNoteOn(36),
                Actions = new InputAction[] { new KeyAction(new[] { "ctrl", "shift", "1" }) },
                Label = "シーン1へ",
            },
        },
    };
}
