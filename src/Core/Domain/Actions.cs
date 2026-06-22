namespace MidiToEverything.Core.Domain;

/// <summary>
/// Base type for an OS-level action a binding emits. The concrete subtypes form a
/// discriminated union (docs/03_ProfileSchema.md §3). JSON polymorphism attributes are
/// added in the persistence milestone (M2); the engine only depends on these shapes.
/// </summary>
public abstract record InputAction;

/// <summary>Mouse buttons usable by click actions.</summary>
public enum MouseButton { Left, Right, Middle }

/// <summary>Axis/direction for movement and scroll actions.</summary>
public enum MoveMode { Relative, Absolute }
public enum ScrollAxis { Vertical, Horizontal }

/// <summary>Profile-switch targets (docs/03_ProfileSchema.md §3).</summary>
public enum ProfileSwitchTarget { Next, Previous, Toggle, Specific }

/// <summary>Operations on the foreground window (docs/05_ActionExpansion.md §5, Phase 1).</summary>
public enum WindowOp { Minimize, Maximize, Restore, Close, ToggleTopMost }

/// <summary>Media/transport keys (docs/05 §5, Phase 1).</summary>
public enum MediaKey { PlayPause, Next, Previous, Stop, Mute, VolumeUp, VolumeDown }

/// <summary>Audio endpoint a volume action targets (docs/05 §5, Phase 1).</summary>
public enum VolumeTarget { Master, Microphone }

/// <summary>How a UI Automation element is actuated (docs/05 §5, Phase 2).</summary>
public enum UiaVerb { Invoke, Toggle, SetValue }

/// <summary>Virtual-desktop switch direction (docs/05 §5, Phase 2).</summary>
public enum DesktopOp { Next, Previous }

/// <summary>A toggleable Windows setting (docs/05 §5, Phase 2).</summary>
public enum WindowsSetting { DarkMode }

/// <summary>Send a keyboard shortcut. <see cref="Hold"/> keeps keys down until release.</summary>
public sealed record KeyAction(
    IReadOnlyList<string> Keys,
    bool Hold = false,
    bool Repeat = false) : InputAction;

/// <summary>Emit a mouse click.</summary>
public sealed record MouseClickAction(
    MouseButton Button = MouseButton.Left,
    bool Double = false) : InputAction;

/// <summary>Move the cursor. When <see cref="UseInputValue"/> the MIDI value drives the amount.</summary>
public sealed record CursorMoveAction(
    MoveMode Mode = MoveMode.Relative,
    int Dx = 0,
    int Dy = 0,
    bool UseInputValue = true) : InputAction;

/// <summary>Scroll the wheel. When <see cref="UseInputValue"/> the MIDI value drives the amount.</summary>
public sealed record ScrollAction(
    ScrollAxis Axis = ScrollAxis.Vertical,
    int Amount = 120,
    bool UseInputValue = true) : InputAction;

/// <summary>Switch the active profile (manual switch from a MIDI signal, FR-5.4).</summary>
public sealed record SwitchProfileAction(
    ProfileSwitchTarget Target = ProfileSwitchTarget.Next,
    string? ProfileId = null) : InputAction;

/// <summary>Apply a window operation to the foreground window (docs/05 §5, Phase 1).</summary>
public sealed record WindowControlAction(WindowOp Op = WindowOp.Minimize) : InputAction;

/// <summary>Send a media/transport key (play-pause, next, mute, ...).</summary>
public sealed record MediaKeyAction(MediaKey Key = MediaKey.PlayPause) : InputAction;

/// <summary>Type a literal string (a snippet / text expansion).</summary>
public sealed record TypeTextAction(string Text = "") : InputAction;

/// <summary>
/// Launch a program/file/URL via the shell. Gated behind an opt-in setting (Q5);
/// the handler no-ops when external launch is disabled.
/// </summary>
public sealed record LaunchAction(
    string Target = "",
    string? Arguments = null,
    string? WorkingDir = null) : InputAction;

/// <summary>Set an audio endpoint volume from the input value (Absolute fader → 0..100%).</summary>
public sealed record SetVolumeAction(VolumeTarget Target = VolumeTarget.Master) : InputAction;

/// <summary>
/// Actuate a control in another window via UI Automation (docs/05 §5, Phase 2). The target
/// window is matched by <see cref="WindowPattern"/> (regex over "process\ntitle"); the element
/// is found by Name or AutomationId.
/// </summary>
public sealed record UiaAction(
    string WindowPattern = "",
    string ElementName = "",
    UiaVerb Verb = UiaVerb.Invoke,
    string? Value = null) : InputAction;

/// <summary>Switch virtual desktop (next/previous) via Win+Ctrl+Arrow (docs/05 §5, Phase 2).</summary>
public sealed record VirtualDesktopAction(DesktopOp Op = DesktopOp.Next) : InputAction;

/// <summary>Toggle a Windows setting, e.g. dark/light app theme (docs/05 §5, Phase 2).</summary>
public sealed record WindowsToggleAction(WindowsSetting Setting = WindowsSetting.DarkMode) : InputAction;

/// <summary>Set display brightness from the input value (Absolute fader → 0..100%, Phase 2).</summary>
public sealed record BrightnessAction : InputAction;

/// <summary>
/// Explicit "do nothing" that also blocks fallback to the base profile (FR-6.4).
/// Its presence as the sole action marks a binding as a block.
/// </summary>
public sealed record NoneAction : InputAction
{
    public static readonly NoneAction Instance = new();
}
