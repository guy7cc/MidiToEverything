using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Tests.Mapping;

/// <summary>
/// Profiles mirroring the example config in docs/03_ProfileSchema.md §5, used to assert
/// the conflict-resolution table in §6.
/// </summary>
internal static class SchemaFixture
{
    public static MidiMessage NoteOn(int number, int velocity = 100, string device = "akai", int channel = 1)
        => new(device, channel, MidiMessageType.NoteOn, number, velocity);

    public static MidiMessage NoteOff(int number, string device = "akai", int channel = 1)
        => new(device, channel, MidiMessageType.NoteOff, number, 0);

    public static MidiMessage Cc(int number, int value, string device = "akai", int channel = 1)
        => new(device, channel, MidiMessageType.ControlChange, number, value);

    private static Signal AnyNoteOn(int number) => new()
    {
        Device = Signal.AnyDevice,
        Channel = Signal.AnyChannel,
        Type = SignalKind.NoteOn,
        Number = number,
    };

    private static Signal AnyCc(int number) => new()
    {
        Device = Signal.AnyDevice,
        Channel = Signal.AnyChannel,
        Type = SignalKind.Cc,
        Number = number,
    };

    public static Profile Base() => new()
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

    public static Profile ClipStudio() => new()
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
                Signal = new Signal
                {
                    Device = Signal.AnyDevice, Channel = Signal.AnyChannel,
                    Type = SignalKind.Note, Number = 40,
                },
                Trigger = new Trigger { Mode = TriggerMode.Hold },
                Actions = new InputAction[] { new KeyAction(new[] { "space" }, Hold: true) },
                Label = "手のひらツール(押下中)",
            },
            new Binding
            {
                // Block the base "copy" inside CSP (FR-6.4).
                Signal = AnyNoteOn(37),
                Actions = new InputAction[] { NoneAction.Instance },
                Label = "（基本のコピーを無効化）",
            },
        },
    };

    public static Profile Obs() => new()
    {
        Id = "obs",
        Name = "OBS Studio",
        Match = new MatchRule { Pattern = @"^obs64\.exe$", Priority = 5 },
        Bindings = new[]
        {
            new Binding
            {
                // Override base Note36 (undo) with a scene switch (FR-6.2).
                Signal = AnyNoteOn(36),
                Actions = new InputAction[] { new KeyAction(new[] { "ctrl", "shift", "1" }) },
                Label = "シーン1へ",
            },
        },
    };
}
