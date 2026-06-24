using System.Text.Json.Serialization;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Persistence;

// DTOs mirror the on-disk JSON shape (docs/03_ProfileSchema.md §5) and are deliberately
// decoupled from the domain model so the file format can evolve independently. Mapping
// between the two lives in ProfileMapper. Mutable classes with defaults are used so
// omitted JSON fields fall back to schema defaults.

internal sealed class ConfigDto
{
    public int Version { get; set; } = ConfigMigrator.CurrentVersion;
    public SettingsDto Settings { get; set; } = new();
    public ActiveContextDto? ActiveContext { get; set; }
    public ProfileDto BaseProfile { get; set; } = new();
    public List<ProfileDto> Profiles { get; set; } = new();
}

internal sealed class SettingsDto
{
    public bool StartWithWindows { get; set; }
    public string? EmergencyStopHotkey { get; set; } = "ctrl+alt+pause";
    public bool AllowExternalLaunch { get; set; }
    public string Language { get; set; } = "ja";
    public bool AutoUpdate { get; set; } = true;
    public string UpdateChannel { get; set; } = "stable";
    public int UpdateCheckHours { get; set; } = 24;
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool StartEmissionEnabled { get; set; } = true;
    public bool TrayNotifications { get; set; } = true;
    public string Theme { get; set; } = "dark";
    public string AccentColor { get; set; } = "blue";
    public double UiScale { get; set; } = 1.0;
    public bool AutoDetectDevices { get; set; } = true;
    public string ObsHost { get; set; } = "localhost";
    public int ObsPort { get; set; } = 4455;
    public string ObsPassword { get; set; } = "";
    public List<string> WatchedDevices { get; set; } = new() { "*" };
    public string LogLevel { get; set; } = "Debug";
    public int LogRetentionDays { get; set; } = 7;
    public bool CrashAutoRestart { get; set; } = true;
    public MonitorDto Monitor { get; set; } = new();
}

internal sealed class MonitorDto
{
    public int MaxLogLines { get; set; } = 500;
    public int UiThrottleMs { get; set; } = 30;
}

internal sealed class ActiveContextDto
{
    public string? PinnedProfileId { get; set; }
    public string? CurrentProfileId { get; set; }
}

internal sealed class ProfileDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public MatchDto? Match { get; set; }
    public List<BindingDto> Bindings { get; set; } = new();
}

internal sealed class MatchDto
{
    /// <summary>Unified match regex (v2+), evaluated against "process\ntitle".</summary>
    public string Pattern { get; set; } = "";

    public int Priority { get; set; }

    // ── v1 legacy fields: read for migration only, never written (null is omitted) ──
    public List<string>? ProcessNames { get; set; }
    public string? TitlePattern { get; set; }
}

internal sealed class BindingDto
{
    public SignalDto Signal { get; set; } = new();
    public TriggerDto? Trigger { get; set; }
    public List<ActionDto> Actions { get; set; } = new();
    public string? Label { get; set; }
    public bool Enabled { get; set; } = true;
}

internal sealed class SignalDto
{
    public string Device { get; set; } = Signal.AnyDevice;

    /// <summary>Accepts a JSON number or string; stored as string ("any" or "1".."16").</summary>
    [JsonConverter(typeof(ChannelJsonConverter))]
    public string Channel { get; set; } = Signal.AnyChannel;

    public SignalKind Type { get; set; }
    public int? Number { get; set; }
}

internal sealed class TriggerDto
{
    public TriggerMode Mode { get; set; } = TriggerMode.Trigger;
    public int Threshold { get; set; } = 1;

    /// <summary>Inclusive [min,max]; absent means the default 0..127.</summary>
    public int[]? Range { get; set; }

    public int Deadzone { get; set; }
    public bool Invert { get; set; }
    public double Scale { get; set; } = 1.0;
    public RelativeFormat RelativeFormat { get; set; } = RelativeFormat.TwosComplement;

    /// <summary>Relative output: <c>amount</c> (default) / <c>fireOnIncrease</c> / <c>fireOnDecrease</c>.</summary>
    public RelativeOutput RelativeOutput { get; set; } = RelativeOutput.Amount;

    /// <summary>Absolute out-of-range behavior: <c>clamp</c> (default) or <c>gate</c>.</summary>
    public OutOfRangeBehavior OutOfRange { get; set; } = OutOfRangeBehavior.Clamp;

    /// <summary>Rising-edge mode for value-gated triggers (fire once on entry).</summary>
    public bool Edge { get; set; }

    /// <summary>Wrap-around handling for Relative + AbsoluteDelta (endless absolute knobs).</summary>
    public bool Wrap { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyActionDto), "key")]
[JsonDerivedType(typeof(MouseClickActionDto), "mouseClick")]
[JsonDerivedType(typeof(CursorMoveActionDto), "cursorMove")]
[JsonDerivedType(typeof(ScrollActionDto), "scroll")]
[JsonDerivedType(typeof(SwitchProfileActionDto), "switchProfile")]
[JsonDerivedType(typeof(WindowControlActionDto), "windowControl")]
[JsonDerivedType(typeof(MediaKeyActionDto), "mediaKey")]
[JsonDerivedType(typeof(TypeTextActionDto), "typeText")]
[JsonDerivedType(typeof(LaunchActionDto), "launch")]
[JsonDerivedType(typeof(SetVolumeActionDto), "setVolume")]
[JsonDerivedType(typeof(UiaActionDto), "uia")]
[JsonDerivedType(typeof(VirtualDesktopActionDto), "virtualDesktop")]
[JsonDerivedType(typeof(WindowsToggleActionDto), "windowsToggle")]
[JsonDerivedType(typeof(BrightnessActionDto), "brightness")]
[JsonDerivedType(typeof(HttpActionDto), "http")]
[JsonDerivedType(typeof(OscActionDto), "osc")]
[JsonDerivedType(typeof(ObsActionDto), "obs")]
[JsonDerivedType(typeof(MidiOutActionDto), "midiOut")]
[JsonDerivedType(typeof(MacroActionDto), "macro")]
[JsonDerivedType(typeof(ToggleActionDto), "toggle")]
[JsonDerivedType(typeof(PluginActionDto), "plugin")]
[JsonDerivedType(typeof(NoneActionDto), "none")]
internal abstract class ActionDto;

internal sealed class KeyActionDto : ActionDto
{
    public List<string> Keys { get; set; } = new();
    public bool Hold { get; set; }
    public bool Repeat { get; set; }
}

internal sealed class MouseClickActionDto : ActionDto
{
    public MouseButton Button { get; set; } = MouseButton.Left;
    public bool Double { get; set; }
}

internal sealed class CursorMoveActionDto : ActionDto
{
    public MoveMode Mode { get; set; } = MoveMode.Relative;
    public int Dx { get; set; }
    public int Dy { get; set; }
    public bool UseInputValue { get; set; } = true;
}

internal sealed class ScrollActionDto : ActionDto
{
    public ScrollAxis Axis { get; set; } = ScrollAxis.Vertical;
    public int Amount { get; set; } = 120;
    public bool UseInputValue { get; set; } = true;
}

internal sealed class SwitchProfileActionDto : ActionDto
{
    /// <summary>"next" | "prev" | "toggle" | a profile id (docs/03_ProfileSchema.md §3).</summary>
    public string Target { get; set; } = "next";
}

internal sealed class WindowControlActionDto : ActionDto
{
    public WindowOp Op { get; set; } = WindowOp.Minimize;
}

internal sealed class MediaKeyActionDto : ActionDto
{
    public MediaKey Key { get; set; } = MediaKey.PlayPause;
}

internal sealed class TypeTextActionDto : ActionDto
{
    public string Text { get; set; } = "";
}

internal sealed class LaunchActionDto : ActionDto
{
    public string Target { get; set; } = "";
    public string? Arguments { get; set; }
    public string? WorkingDir { get; set; }
}

internal sealed class SetVolumeActionDto : ActionDto
{
    public VolumeTarget Target { get; set; } = VolumeTarget.Master;
}

internal sealed class UiaActionDto : ActionDto
{
    public string WindowPattern { get; set; } = "";
    public string ElementName { get; set; } = "";
    public UiaVerb Verb { get; set; } = UiaVerb.Invoke;
    public string? Value { get; set; }
}

internal sealed class VirtualDesktopActionDto : ActionDto
{
    public DesktopOp Op { get; set; } = DesktopOp.Next;
}

internal sealed class WindowsToggleActionDto : ActionDto
{
    public WindowsSetting Setting { get; set; } = WindowsSetting.DarkMode;
}

internal sealed class BrightnessActionDto : ActionDto;

internal sealed class HttpActionDto : ActionDto
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "GET";
    public string? Body { get; set; }
}

internal sealed class OscActionDto : ActionDto
{
    public string Target { get; set; } = "";
    public string Address { get; set; } = "";
    public string Args { get; set; } = "";
}

internal sealed class ObsActionDto : ActionDto
{
    public ObsOp Op { get; set; } = ObsOp.ToggleRecord;
    public string? Arg { get; set; }
}

internal sealed class MidiOutActionDto : ActionDto
{
    public string Device { get; set; } = "";
    public MidiOutKind Kind { get; set; } = MidiOutKind.ControlChange;
    public int Channel { get; set; } = 1;
    public int Data1 { get; set; }
    public int Data2 { get; set; }
    public bool UseInputValue { get; set; }
}

internal sealed class MacroActionDto : ActionDto
{
    public List<List<string>> Steps { get; set; } = new();
    public int StepDelayMs { get; set; } = 30;
}

internal sealed class ToggleActionDto : ActionDto
{
    public List<string> KeysA { get; set; } = new();
    public List<string> KeysB { get; set; } = new();
    public string? LedDevice { get; set; }
    public int LedChannel { get; set; } = 1;
    public int LedNote { get; set; }
}

internal sealed class PluginActionDto : ActionDto
{
    public string PluginId { get; set; } = "";
    public string Command { get; set; } = "";
    public string? Arg { get; set; }
}

internal sealed class NoneActionDto : ActionDto;
