using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;
using Binding = MidiToEverything.Core.Domain.Binding;

namespace MidiToEverything.App.ViewModels.Editing;

/// <summary>Converts between the editor's editable view models and the domain config.</summary>
internal static class EditMapper
{
    // ── Domain → Editable ─────────────────────────────────────────────────────

    public static List<EditableProfile> ToEditable(AppConfig config)
    {
        var result = new List<EditableProfile> { ToEditable(config.BaseProfile, isBase: true) };
        result.AddRange(config.Profiles.Select(p => ToEditable(p, isBase: false)));
        return result;
    }

    private static EditableProfile ToEditable(Profile profile, bool isBase)
    {
        var editable = new EditableProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            IsBase = isBase,
            Pattern = profile.Match?.Pattern ?? "",
            Priority = profile.Match?.Priority ?? 0,
        };

        foreach (var binding in profile.Bindings)
        {
            editable.Bindings.Add(ToEditable(binding));
        }

        return editable;
    }

    private static EditableBinding ToEditable(Binding binding)
    {
        var action = binding.Actions.Count > 0 ? binding.Actions[0] : NoneAction.Instance;
        var (kind, detail) = DescribeAction(action);
        var editable = new EditableBinding
        {
            Signal = new EditableSignal
            {
                Device = binding.Signal.Device,
                Channel = binding.Signal.Channel,
                Type = binding.Signal.Type,
                NumberText = binding.Signal.Number?.ToString() ?? "",
            },
            Mode = binding.Trigger.Mode,
            ActionKind = kind,
            Detail = detail,
            Label = binding.Label ?? "",
        };

        if (action is LaunchAction l)
        {
            editable.Arguments = l.Arguments ?? "";
            editable.WorkingDir = l.WorkingDir ?? "";
        }
        else if (action is UiaAction u)
        {
            editable.UiaWindow = u.WindowPattern;
            editable.UiaVerb = u.Verb.ToString().ToLowerInvariant();
            editable.UiaValue = u.Value ?? "";
        }

        return editable;
    }

    private static (EditableActionKind Kind, string Detail) DescribeAction(InputAction action) => action switch
    {
        KeyAction k => (EditableActionKind.Key, string.Join("+", k.Keys)),
        MouseClickAction m => (EditableActionKind.MouseClick, (m.Double ? $"{m.Button} x2" : m.Button.ToString()).ToLowerInvariant()),
        ScrollAction s => (EditableActionKind.Scroll, s.Axis.ToString().ToLowerInvariant()),
        CursorMoveAction c => (EditableActionKind.CursorMove, c.Mode.ToString().ToLowerInvariant()),
        WindowControlAction w => (EditableActionKind.WindowControl, WindowOpDetail(w.Op)),
        MediaKeyAction mk => (EditableActionKind.MediaKey, mk.Key.ToString().ToLowerInvariant()),
        TypeTextAction tt => (EditableActionKind.TypeText, tt.Text),
        LaunchAction l => (EditableActionKind.Launch, l.Target),
        SetVolumeAction v => (EditableActionKind.SetVolume, v.Target.ToString().ToLowerInvariant()),
        UiaAction u => (EditableActionKind.Uia, u.ElementName),
        VirtualDesktopAction vd => (EditableActionKind.VirtualDesktop, vd.Op == DesktopOp.Previous ? "previous" : "next"),
        WindowsToggleAction wt => (EditableActionKind.WindowsToggle, wt.Setting.ToString().ToLowerInvariant()),
        BrightnessAction => (EditableActionKind.Brightness, ""),
        SwitchProfileAction sp => (EditableActionKind.SwitchProfile, SwitchDetail(sp)),
        _ => (EditableActionKind.None, ""),
    };

    private static string SwitchDetail(SwitchProfileAction sp) => sp.Target switch
    {
        ProfileSwitchTarget.Next => "next",
        ProfileSwitchTarget.Previous => "prev",
        ProfileSwitchTarget.Toggle => "toggle",
        _ => sp.ProfileId ?? "",
    };

    /// <summary>Display/round-trip string for a window op (matches the editor candidates).</summary>
    public static string WindowOpDetail(WindowOp op) => op switch
    {
        WindowOp.Maximize => "maximize",
        WindowOp.Restore => "restore",
        WindowOp.Close => "close",
        WindowOp.ToggleTopMost => "topmost",
        _ => "minimize",
    };

    public static WindowOp ParseWindowOp(string detail) => detail.Trim().ToLowerInvariant() switch
    {
        "maximize" or "max" => WindowOp.Maximize,
        "restore" => WindowOp.Restore,
        "close" => WindowOp.Close,
        "topmost" or "toggletopmost" or "pin" => WindowOp.ToggleTopMost,
        _ => WindowOp.Minimize,
    };

    // ── Editable → Domain ─────────────────────────────────────────────────────

    public static AppConfig ToConfig(IReadOnlyList<EditableProfile> profiles, AppConfig template)
    {
        var basePart = profiles.FirstOrDefault(p => p.IsBase) ?? profiles[0];
        var others = profiles.Where(p => !p.IsBase);

        return template with
        {
            BaseProfile = ToDomain(basePart),
            Profiles = others.Select(ToDomain).ToArray(),
        };
    }

    private static Profile ToDomain(EditableProfile p)
    {
        MatchRule? match = null;
        if (!p.IsBase && !string.IsNullOrWhiteSpace(p.Pattern))
        {
            match = new MatchRule { Pattern = p.Pattern.Trim(), Priority = p.Priority };
        }

        return new Profile
        {
            Id = string.IsNullOrWhiteSpace(p.Id) ? Guid.NewGuid().ToString("N")[..8] : p.Id.Trim(),
            Name = string.IsNullOrWhiteSpace(p.Name) ? p.Id : p.Name.Trim(),
            Match = match,
            Bindings = p.Bindings.Select(ToDomain).ToArray(),
        };
    }

    private static Binding ToDomain(EditableBinding b)
    {
        int? number = int.TryParse(b.Signal.NumberText.Trim(), out var n) ? n : null;
        var signal = new Signal
        {
            Device = string.IsNullOrWhiteSpace(b.Signal.Device) ? Signal.AnyDevice : b.Signal.Device.Trim(),
            Channel = string.IsNullOrWhiteSpace(b.Signal.Channel) ? Signal.AnyChannel : b.Signal.Channel.Trim(),
            Type = b.Signal.Type,
            Number = b.Signal.Type == SignalKind.PitchBend ? null : number,
        };

        return new Binding
        {
            Signal = signal,
            Trigger = new Trigger { Mode = b.Mode },
            Actions = new[] { ToAction(b) },
            Label = string.IsNullOrWhiteSpace(b.Label) ? null : b.Label.Trim(),
        };
    }

    private static InputAction ToAction(EditableBinding b)
    {
        var detail = b.Detail.Trim();
        return b.ActionKind switch
        {
            EditableActionKind.Key => new KeyAction(SplitKeys(detail), Hold: b.Mode == TriggerMode.Hold),
            EditableActionKind.MouseClick => ParseMouse(detail),
            EditableActionKind.Scroll => new ScrollAction(ParseAxis(detail), UseInputValue: true),
            EditableActionKind.CursorMove => new CursorMoveAction(ParseMove(detail), UseInputValue: true),
            EditableActionKind.WindowControl => new WindowControlAction(ParseWindowOp(detail)),
            EditableActionKind.MediaKey => new MediaKeyAction(ParseMediaKey(detail)),
            EditableActionKind.TypeText => new TypeTextAction(b.Detail), // keep raw text (spaces/newlines)
            EditableActionKind.Launch => new LaunchAction(detail, NullIfBlank(b.Arguments), NullIfBlank(b.WorkingDir)),
            EditableActionKind.SetVolume => new SetVolumeAction(ParseVolumeTarget(detail)),
            EditableActionKind.Uia => new UiaAction(b.UiaWindow.Trim(), detail, ParseUiaVerb(b.UiaVerb), NullIfBlank(b.UiaValue)),
            EditableActionKind.VirtualDesktop => new VirtualDesktopAction(
                detail.Trim().ToLowerInvariant() is "previous" or "prev" ? DesktopOp.Previous : DesktopOp.Next),
            EditableActionKind.WindowsToggle => new WindowsToggleAction(WindowsSetting.DarkMode),
            EditableActionKind.Brightness => new BrightnessAction(),
            EditableActionKind.SwitchProfile => ParseSwitch(detail),
            _ => NoneAction.Instance,
        };
    }

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static MediaKey ParseMediaKey(string detail) => detail.Trim().ToLowerInvariant() switch
    {
        "next" => MediaKey.Next,
        "previous" or "prev" => MediaKey.Previous,
        "stop" => MediaKey.Stop,
        "mute" => MediaKey.Mute,
        "volumeup" or "volup" => MediaKey.VolumeUp,
        "volumedown" or "voldown" => MediaKey.VolumeDown,
        _ => MediaKey.PlayPause,
    };

    public static VolumeTarget ParseVolumeTarget(string detail) =>
        detail.Trim().ToLowerInvariant() is "microphone" or "mic" ? VolumeTarget.Microphone : VolumeTarget.Master;

    public static UiaVerb ParseUiaVerb(string verb) => verb.Trim().ToLowerInvariant() switch
    {
        "toggle" => UiaVerb.Toggle,
        "setvalue" or "value" => UiaVerb.SetValue,
        _ => UiaVerb.Invoke,
    };

    private static string[] SplitKeys(string detail) => detail
        .Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static MouseClickAction ParseMouse(string detail)
    {
        var doubleClick = detail.Contains("x2", StringComparison.OrdinalIgnoreCase) ||
                          detail.Contains("double", StringComparison.OrdinalIgnoreCase);
        var button = detail.Contains("right", StringComparison.OrdinalIgnoreCase) ? MouseButton.Right
            : detail.Contains("middle", StringComparison.OrdinalIgnoreCase) ? MouseButton.Middle
            : MouseButton.Left;
        return new MouseClickAction(button, doubleClick);
    }

    private static ScrollAxis ParseAxis(string detail) =>
        detail.StartsWith("h", StringComparison.OrdinalIgnoreCase) ? ScrollAxis.Horizontal : ScrollAxis.Vertical;

    private static MoveMode ParseMove(string detail) =>
        detail.StartsWith("a", StringComparison.OrdinalIgnoreCase) ? MoveMode.Absolute : MoveMode.Relative;

    private static SwitchProfileAction ParseSwitch(string detail) => detail.ToLowerInvariant() switch
    {
        "next" or "" => new SwitchProfileAction(ProfileSwitchTarget.Next),
        "prev" or "previous" => new SwitchProfileAction(ProfileSwitchTarget.Previous),
        "toggle" => new SwitchProfileAction(ProfileSwitchTarget.Toggle),
        _ => new SwitchProfileAction(ProfileSwitchTarget.Specific, detail),
    };
}
