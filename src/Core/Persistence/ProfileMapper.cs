using MidiToEverything.Core.Application;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.Core.Persistence;

/// <summary>Converts between persisted DTOs and the domain model (docs/02_Architecture.md §3.5).</summary>
internal static class ProfileMapper
{
    // ── DTO → Domain ──────────────────────────────────────────────────────────

    public static AppConfig ToDomain(ConfigDto dto) => new()
    {
        Version = dto.Version,
        Settings = ToDomain(dto.Settings),
        BaseProfile = ToDomain(dto.BaseProfile),
        Profiles = dto.Profiles.Select(ToDomain).ToArray(),
        ActiveContext = dto.ActiveContext is null
            ? null
            : new ActiveContextState
            {
                PinnedProfileId = dto.ActiveContext.PinnedProfileId,
                CurrentProfileId = dto.ActiveContext.CurrentProfileId,
            },
    };

    private static AppSettings ToDomain(SettingsDto s) => new()
    {
        StartWithWindows = s.StartWithWindows,
        EmergencyStopHotkey = s.EmergencyStopHotkey,
        AllowExternalLaunch = s.AllowExternalLaunch,
        ObsHost = s.ObsHost,
        ObsPort = s.ObsPort,
        ObsPassword = s.ObsPassword,
        WatchedDevices = s.WatchedDevices.ToArray(),
        Monitor = new MonitorSettings { MaxLogLines = s.Monitor.MaxLogLines, UiThrottleMs = s.Monitor.UiThrottleMs },
    };

    private static Profile ToDomain(ProfileDto p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Enabled = p.Enabled,
        Match = p.Match is null
            ? null
            : new MatchRule
            {
                Pattern = p.Match.Pattern,
                Priority = p.Match.Priority,
            },
        Bindings = p.Bindings.Select(ToDomain).ToArray(),
    };

    private static Binding ToDomain(BindingDto b) => new()
    {
        Signal = new Signal
        {
            Device = b.Signal.Device,
            Channel = b.Signal.Channel,
            Type = b.Signal.Type,
            Number = b.Signal.Number,
        },
        Trigger = ToDomain(b.Trigger),
        Actions = b.Actions.Select(ToDomain).ToArray(),
        Label = b.Label,
        Enabled = b.Enabled,
    };

    private static Trigger ToDomain(TriggerDto? t)
    {
        if (t is null)
        {
            return Trigger.Default;
        }

        return new Trigger
        {
            Mode = t.Mode,
            Threshold = t.Threshold,
            RangeMin = t.Range is { Length: 2 } ? t.Range[0] : 0,
            RangeMax = t.Range is { Length: 2 } ? t.Range[1] : 127,
            Deadzone = t.Deadzone,
            Invert = t.Invert,
            Scale = t.Scale,
            RelativeFormat = t.RelativeFormat,
        };
    }

    private static InputAction ToDomain(ActionDto a) => a switch
    {
        KeyActionDto k => new KeyAction(k.Keys.ToArray(), k.Hold, k.Repeat),
        MouseClickActionDto m => new MouseClickAction(m.Button, m.Double),
        CursorMoveActionDto c => new CursorMoveAction(c.Mode, c.Dx, c.Dy, c.UseInputValue),
        ScrollActionDto s => new ScrollAction(s.Axis, s.Amount, s.UseInputValue),
        SwitchProfileActionDto sp => ToSwitchProfile(sp.Target),
        WindowControlActionDto w => new WindowControlAction(w.Op),
        MediaKeyActionDto mk => new MediaKeyAction(mk.Key),
        TypeTextActionDto tt => new TypeTextAction(tt.Text),
        LaunchActionDto l => new LaunchAction(l.Target, l.Arguments, l.WorkingDir),
        SetVolumeActionDto v => new SetVolumeAction(v.Target),
        UiaActionDto u => new UiaAction(u.WindowPattern, u.ElementName, u.Verb, u.Value),
        VirtualDesktopActionDto vd => new VirtualDesktopAction(vd.Op),
        WindowsToggleActionDto wt => new WindowsToggleAction(wt.Setting),
        BrightnessActionDto => new BrightnessAction(),
        HttpActionDto h => new HttpAction(h.Url, h.Method, h.Body),
        OscActionDto o => new OscAction(o.Target, o.Address, o.Args),
        ObsActionDto ob => new ObsAction(ob.Op, ob.Arg),
        NoneActionDto => NoneAction.Instance,
        _ => throw new NotSupportedException($"Unknown action DTO: {a.GetType().Name}"),
    };

    private static SwitchProfileAction ToSwitchProfile(string target) => target.Trim().ToLowerInvariant() switch
    {
        "next" or "" => new SwitchProfileAction(ProfileSwitchTarget.Next),
        "prev" or "previous" => new SwitchProfileAction(ProfileSwitchTarget.Previous),
        "toggle" => new SwitchProfileAction(ProfileSwitchTarget.Toggle),
        _ => new SwitchProfileAction(ProfileSwitchTarget.Specific, target),
    };

    // ── Domain → DTO ──────────────────────────────────────────────────────────

    public static ConfigDto ToDto(AppConfig config) => new()
    {
        Version = config.Version,
        Settings = new SettingsDto
        {
            StartWithWindows = config.Settings.StartWithWindows,
            EmergencyStopHotkey = config.Settings.EmergencyStopHotkey,
            AllowExternalLaunch = config.Settings.AllowExternalLaunch,
            ObsHost = config.Settings.ObsHost,
            ObsPort = config.Settings.ObsPort,
            ObsPassword = config.Settings.ObsPassword,
            WatchedDevices = config.Settings.WatchedDevices.ToList(),
            Monitor = new MonitorDto
            {
                MaxLogLines = config.Settings.Monitor.MaxLogLines,
                UiThrottleMs = config.Settings.Monitor.UiThrottleMs,
            },
        },
        ActiveContext = config.ActiveContext is null
            ? null
            : new ActiveContextDto
            {
                PinnedProfileId = config.ActiveContext.PinnedProfileId,
                CurrentProfileId = config.ActiveContext.CurrentProfileId,
            },
        BaseProfile = ToDto(config.BaseProfile),
        Profiles = config.Profiles.Select(ToDto).ToList(),
    };

    private static ProfileDto ToDto(Profile p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Enabled = p.Enabled,
        Match = p.Match is null
            ? null
            : new MatchDto
            {
                Pattern = p.Match.Pattern,
                Priority = p.Match.Priority,
            },
        Bindings = p.Bindings.Select(ToDto).ToList(),
    };

    private static BindingDto ToDto(Binding b) => new()
    {
        Signal = new SignalDto
        {
            Device = b.Signal.Device,
            Channel = b.Signal.Channel,
            Type = b.Signal.Type,
            Number = b.Signal.Number,
        },
        Trigger = new TriggerDto
        {
            Mode = b.Trigger.Mode,
            Threshold = b.Trigger.Threshold,
            Range = new[] { b.Trigger.RangeMin, b.Trigger.RangeMax },
            Deadzone = b.Trigger.Deadzone,
            Invert = b.Trigger.Invert,
            Scale = b.Trigger.Scale,
            RelativeFormat = b.Trigger.RelativeFormat,
        },
        Actions = b.Actions.Select(ToDto).ToList(),
        Label = b.Label,
        Enabled = b.Enabled,
    };

    private static ActionDto ToDto(InputAction a) => a switch
    {
        KeyAction k => new KeyActionDto { Keys = k.Keys.ToList(), Hold = k.Hold, Repeat = k.Repeat },
        MouseClickAction m => new MouseClickActionDto { Button = m.Button, Double = m.Double },
        CursorMoveAction c => new CursorMoveActionDto { Mode = c.Mode, Dx = c.Dx, Dy = c.Dy, UseInputValue = c.UseInputValue },
        ScrollAction s => new ScrollActionDto { Axis = s.Axis, Amount = s.Amount, UseInputValue = s.UseInputValue },
        SwitchProfileAction sp => new SwitchProfileActionDto { Target = FromSwitchProfile(sp) },
        WindowControlAction w => new WindowControlActionDto { Op = w.Op },
        MediaKeyAction mk => new MediaKeyActionDto { Key = mk.Key },
        TypeTextAction tt => new TypeTextActionDto { Text = tt.Text },
        LaunchAction l => new LaunchActionDto { Target = l.Target, Arguments = l.Arguments, WorkingDir = l.WorkingDir },
        SetVolumeAction v => new SetVolumeActionDto { Target = v.Target },
        UiaAction u => new UiaActionDto { WindowPattern = u.WindowPattern, ElementName = u.ElementName, Verb = u.Verb, Value = u.Value },
        VirtualDesktopAction vd => new VirtualDesktopActionDto { Op = vd.Op },
        WindowsToggleAction wt => new WindowsToggleActionDto { Setting = wt.Setting },
        BrightnessAction => new BrightnessActionDto(),
        HttpAction h => new HttpActionDto { Url = h.Url, Method = h.Method, Body = h.Body },
        OscAction o => new OscActionDto { Target = o.Target, Address = o.Address, Args = o.Args },
        ObsAction ob => new ObsActionDto { Op = ob.Op, Arg = ob.Arg },
        NoneAction => new NoneActionDto(),
        _ => throw new NotSupportedException($"Unknown action: {a.GetType().Name}"),
    };

    private static string FromSwitchProfile(SwitchProfileAction sp) => sp.Target switch
    {
        ProfileSwitchTarget.Next => "next",
        ProfileSwitchTarget.Previous => "prev",
        ProfileSwitchTarget.Toggle => "toggle",
        ProfileSwitchTarget.Specific => sp.ProfileId ?? "",
        _ => "next",
    };
}
