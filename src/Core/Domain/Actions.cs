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

/// <summary>
/// Explicit "do nothing" that also blocks fallback to the base profile (FR-6.4).
/// Its presence as the sole action marks a binding as a block.
/// </summary>
public sealed record NoneAction : InputAction
{
    public static readonly NoneAction Instance = new();
}
