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
    public List<string> WatchedDevices { get; set; } = new() { "*" };
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
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyActionDto), "key")]
[JsonDerivedType(typeof(MouseClickActionDto), "mouseClick")]
[JsonDerivedType(typeof(CursorMoveActionDto), "cursorMove")]
[JsonDerivedType(typeof(ScrollActionDto), "scroll")]
[JsonDerivedType(typeof(SwitchProfileActionDto), "switchProfile")]
[JsonDerivedType(typeof(WindowControlActionDto), "windowControl")]
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

internal sealed class NoneActionDto : ActionDto;
