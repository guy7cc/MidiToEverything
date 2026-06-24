using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>Snapshot of which rules are active and why (for UI/tray, FR-5.6).</summary>
/// <param name="Active">All rules active now: the base rule + every rule whose regex matches the
/// foreground window + any force-enabled rule.</param>
/// <param name="Pinned">A manually force-enabled rule, if any (FR-5.5).</param>
/// <param name="IsPinned">True when a rule is manually force-enabled.</param>
/// <param name="Window">The foreground window context.</param>
public sealed record ActiveProfileState(
    IReadOnlyList<Profile> Active,
    Profile? Pinned,
    bool IsPinned,
    WindowContext Window)
{
    /// <summary>A representative rule for single-line display: pinned, else highest-priority match, else base.</summary>
    public Profile Effective =>
        Pinned
        ?? Active.Where(p => p.Match is not null).OrderByDescending(p => p.Match!.Priority).FirstOrDefault()
        ?? Active[0];

    /// <summary>Names of the active rules in order (base first), for an "active rules" summary.</summary>
    public IReadOnlyList<string> ActiveNames => Active.Select(p => p.Name).ToArray();
}

/// <summary>
/// Owns the active-rule set (docs/02_Architecture.md §3.6) and serves the current
/// <see cref="ActiveRules"/> to the pipeline via <see cref="IMappingContext"/>.
///
/// Every rule whose <see cref="MatchRule"/> matches the foreground window is active simultaneously
/// (FR-5.1/5.2); the base rule is always active; their bindings co-fire (no priority override). A
/// manual force-enable — from the UI (FR-5.3) or a MIDI <see cref="SwitchProfileAction"/> (FR-5.4) —
/// keeps a rule active regardless of its regex (FR-5.5).
/// </summary>
public sealed class ProfileManager : IMappingContext, IDisposable
{
    private readonly IWindowWatcher _watcher;
    private readonly ILogger<ProfileManager> _logger;
    private readonly object _gate = new();

    private AppConfig _config;
    private Profile _base;
    private IReadOnlyList<Profile> _profiles;

    // Manual override per rule id: true = force on (active regardless of regex), false = force off
    // (inactive regardless of regex). Absent = auto (regex decides). Session-only.
    private readonly Dictionary<string, bool> _manual = new();
    private WindowContext _window = WindowContext.Unknown;

    public ProfileManager(AppConfig config, IWindowWatcher watcher, ILogger<ProfileManager>? logger = null)
    {
        _config = config;
        _base = config.BaseProfile;
        _profiles = config.Profiles;
        _watcher = watcher;
        _logger = logger ?? NullLogger<ProfileManager>.Instance;
    }

    /// <summary>The configuration currently driving the engine.</summary>
    public AppConfig CurrentConfig
    {
        get { lock (_gate) { return _config; } }
    }

    /// <summary>
    /// Swap in an edited configuration and re-evaluate the active rules, so changes from the
    /// editor take effect immediately without a restart (FR-7.2). A force-enable pointing at a rule
    /// that no longer exists is cleared.
    /// </summary>
    public void Reload(AppConfig config)
    {
        lock (_gate)
        {
            _config = config;
            _base = config.BaseProfile;
            _profiles = config.Profiles;

            // Drop manual overrides for rules that no longer exist.
            foreach (var id in _manual.Keys.Where(id => Find(id) is null).ToList())
            {
                _manual.Remove(id);
            }
        }

        _logger.LogInformation("Configuration reloaded ({Count} rules).", config.Profiles.Count);
        RaiseChanged();
    }

    /// <summary>Raised whenever the active rule set, force-enable state, or foreground window changes (FR-5.6).</summary>
    public event EventHandler<ActiveProfileState>? Changed;

    public ActiveRules Current
    {
        get { lock (_gate) { return BuildActive(); } }
    }

    public ActiveProfileState State
    {
        get { lock (_gate) { return BuildState(); } }
    }

    /// <summary>True when any rule has a manual force on/off override in effect.</summary>
    public bool IsPinned
    {
        get { lock (_gate) { return _manual.Count > 0; } }
    }

    public void Start()
    {
        _watcher.ForegroundChanged += OnForegroundChanged;
        _watcher.Start();
        UpdateContext(_watcher.Current);
    }

    public void Stop()
    {
        _watcher.ForegroundChanged -= OnForegroundChanged;
        _watcher.Stop();
    }

    public void Dispose() => Stop();

    /// <summary>Force-enable a rule (active regardless of its regex), e.g. from the UI (FR-5.3/5.5).</summary>
    public void Pin(string profileId)
    {
        if (Find(profileId) is null || profileId == _base.Id)
        {
            return;
        }

        lock (_gate)
        {
            _manual[profileId] = true;
        }

        RaiseChanged();
    }

    /// <summary>Clear all manual overrides and resume regex-only matching (FR-5.5).</summary>
    public void Unpin()
    {
        lock (_gate)
        {
            _manual.Clear();
        }

        RaiseChanged();
    }

    /// <summary>Flip a rule's effective active state with a manual override (force on if off, off if on).</summary>
    public void ToggleRule(string profileId)
    {
        var rule = Find(profileId);
        if (rule is null || profileId == _base.Id)
        {
            return;
        }

        lock (_gate)
        {
            _manual[profileId] = !IsActive(rule);
        }

        RaiseChanged();
    }

    /// <summary>Handle a MIDI-driven rule toggle/force (FR-5.4).</summary>
    public void HandleSwitch(SwitchProfileAction action)
    {
        switch (action.Target)
        {
            case ProfileSwitchTarget.Next:
                Cycle(+1);
                break;
            case ProfileSwitchTarget.Previous:
                Cycle(-1);
                break;
            case ProfileSwitchTarget.Specific when action.ProfileId is { } id:
                ToggleRule(id);
                break;
            case ProfileSwitchTarget.Toggle when PrimaryMatch() is { } primary:
                ToggleRule(primary.Id);
                break;
        }
    }

    private void OnForegroundChanged(object? sender, WindowContext context) => UpdateContext(context);

    private void UpdateContext(WindowContext context)
    {
        lock (_gate)
        {
            _window = context;
        }

        RaiseChanged();
    }

    /// <summary>Whether a rule is active now: a manual override wins, otherwise its regex decides.</summary>
    private bool IsActive(Profile p)
    {
        if (!p.Enabled)
        {
            return false;
        }

        if (_manual.TryGetValue(p.Id, out var forced))
        {
            return forced;
        }

        return p.Match is not null && p.Match.Matches(_window.ProcessName, _window.WindowTitle);
    }

    /// <summary>A representative regex-matched rule (highest priority) for toggle/cycle.</summary>
    private Profile? PrimaryMatch() => _profiles
        .Where(p => p.Enabled && p.Match is not null &&
                    p.Match.Matches(_window.ProcessName, _window.WindowTitle))
        .OrderByDescending(p => p.Match!.Priority)
        .FirstOrDefault();

    private void Cycle(int direction)
    {
        lock (_gate)
        {
            if (_profiles.Count == 0)
            {
                return;
            }

            // Cycle exclusively force-enables one rule (clearing other manual overrides).
            var currentId = _manual.FirstOrDefault(kv => kv.Value).Key ?? PrimaryMatch()?.Id;
            var index = currentId is null ? -1 : IndexOf(currentId);

            int next;
            if (index < 0)
            {
                // Nothing selected yet: Next picks the first, Previous the last.
                next = direction > 0 ? 0 : _profiles.Count - 1;
            }
            else
            {
                next = ((index + direction) % _profiles.Count + _profiles.Count) % _profiles.Count;
            }

            _manual.Clear();
            _manual[_profiles[next].Id] = true;
        }

        RaiseChanged();
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < _profiles.Count; i++)
        {
            if (_profiles[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private Profile? Find(string id) =>
        id == _base.Id ? _base : _profiles.FirstOrDefault(p => p.Id == id);

    private ActiveRules BuildActive()
    {
        var rules = new List<Profile> { _base };
        foreach (var p in _profiles)
        {
            if (IsActive(p))
            {
                rules.Add(p);
            }
        }

        return new ActiveRules(rules);
    }

    private ActiveProfileState BuildState()
    {
        // A representative force-on rule for display (the "pinned" badge), if any.
        var forcedOnId = _manual.FirstOrDefault(kv => kv.Value).Key;
        var pinned = forcedOnId is null ? null : Find(forcedOnId);
        var active = BuildActive().Rules;
        return new ActiveProfileState(active, pinned, _manual.Count > 0, _window);
    }

    private void RaiseChanged()
    {
        ActiveProfileState state;
        lock (_gate)
        {
            state = BuildState();
        }

        _logger.LogDebug("Active rules: {Names} (pinned={Pinned}) window={Process}",
            string.Join(", ", state.ActiveNames), state.IsPinned, state.Window.ProcessName);
        Changed?.Invoke(this, state);
    }
}
