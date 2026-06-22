using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;

namespace MidiToEverything.Core.Application;

/// <summary>Snapshot of which profile is active and why (for UI/tray, FR-5.6).</summary>
/// <param name="Effective">Top layer in effect: pinned ?? context ?? base.</param>
/// <param name="Context">Profile matched to the foreground window, if any.</param>
/// <param name="Pinned">Manually pinned profile, if any (FR-5.5).</param>
/// <param name="IsPinned">True when a manual pin overrides auto-switch.</param>
/// <param name="Window">The foreground window context.</param>
public sealed record ActiveProfileState(
    Profile Effective,
    Profile? Context,
    Profile? Pinned,
    bool IsPinned,
    WindowContext Window);

/// <summary>
/// Owns active-profile selection (docs/02_Architecture.md §3.6) and serves the current
/// <see cref="ProfileLayers"/> to the pipeline via <see cref="IMappingContext"/>.
///
/// Auto-switches to the highest-priority profile whose <see cref="MatchRule"/> matches the
/// foreground window (FR-5.1/5.2). A manual switch — from the UI (FR-5.3) or a MIDI
/// <see cref="SwitchProfileAction"/> (FR-5.4) — pins a profile, overriding auto-switch until
/// unpinned (FR-5.5).
/// </summary>
public sealed class ProfileManager : IMappingContext, IDisposable
{
    private readonly IWindowWatcher _watcher;
    private readonly ILogger<ProfileManager> _logger;
    private readonly object _gate = new();

    private AppConfig _config;
    private Profile _base;
    private IReadOnlyList<Profile> _profiles;
    private Profile? _context;
    private string? _pinnedId;
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
    /// Swap in an edited configuration and re-evaluate the active profile, so changes from the
    /// editor take effect immediately without a restart (FR-7.2). A pin pointing at a profile
    /// that no longer exists is cleared.
    /// </summary>
    public void Reload(AppConfig config)
    {
        lock (_gate)
        {
            _config = config;
            _base = config.BaseProfile;
            _profiles = config.Profiles;
            if (_pinnedId is not null && Find(_pinnedId) is null)
            {
                _pinnedId = null;
            }

            _context = Match(_window);
        }

        _logger.LogInformation("Configuration reloaded ({Count} profiles).", config.Profiles.Count);
        RaiseChanged();
    }

    /// <summary>Raised whenever the effective profile, pin state, or foreground window changes (FR-5.6).</summary>
    public event EventHandler<ActiveProfileState>? Changed;

    public ProfileLayers Current
    {
        get { lock (_gate) { return BuildLayers(); } }
    }

    public ActiveProfileState State
    {
        get { lock (_gate) { return BuildState(); } }
    }

    public bool IsPinned
    {
        get { lock (_gate) { return _pinnedId is not null; } }
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

    /// <summary>Manually pin a profile (UI selection), overriding auto-switch (FR-5.3/5.5).</summary>
    public void Pin(string profileId)
    {
        lock (_gate)
        {
            if (Find(profileId) is null)
            {
                return;
            }

            _pinnedId = profileId;
        }

        RaiseChanged();
    }

    /// <summary>Clear the manual pin and resume auto-switching (FR-5.5).</summary>
    public void Unpin()
    {
        lock (_gate)
        {
            _pinnedId = null;
        }

        RaiseChanged();
    }

    /// <summary>Handle a MIDI-driven profile switch (FR-5.4).</summary>
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
                Pin(id);
                break;
            case ProfileSwitchTarget.Toggle:
                TogglePin();
                break;
        }
    }

    private void OnForegroundChanged(object? sender, WindowContext context) => UpdateContext(context);

    private void UpdateContext(WindowContext context)
    {
        lock (_gate)
        {
            _window = context;
            _context = Match(context);
        }

        RaiseChanged();
    }

    private Profile? Match(WindowContext context) => _profiles
        .Where(p => p.Enabled && p.Match is not null &&
                    p.Match.Matches(context.ProcessName, context.WindowTitle))
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

            var currentId = _pinnedId ?? _context?.Id;
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

            _pinnedId = _profiles[next].Id;
        }

        RaiseChanged();
    }

    private void TogglePin()
    {
        lock (_gate)
        {
            // Pinned -> release to auto. Auto with a context match -> pin that context.
            _pinnedId = _pinnedId is not null ? null : _context?.Id;
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

    private ProfileLayers BuildLayers()
    {
        var pinned = _pinnedId is null ? null : Find(_pinnedId);
        return new ProfileLayers(_base, Context: _context, Pinned: pinned);
    }

    private ActiveProfileState BuildState()
    {
        var pinned = _pinnedId is null ? null : Find(_pinnedId);
        var effective = pinned ?? _context ?? _base;
        return new ActiveProfileState(effective, _context, pinned, pinned is not null, _window);
    }

    private void RaiseChanged()
    {
        ActiveProfileState state;
        lock (_gate)
        {
            state = BuildState();
        }

        _logger.LogDebug("Active profile: {Profile} (pinned={Pinned}) window={Process}",
            state.Effective.Name, state.IsPinned, state.Window.ProcessName);
        Changed?.Invoke(this, state);
    }
}
