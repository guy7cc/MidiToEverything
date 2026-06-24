using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiToEverything.App.Localization;
using MidiToEverything.App.ViewModels.Editing;
using MidiToEverything.Core;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
using MidiToEverything.Infrastructure.Input;
using MidiToEverything.Infrastructure.Startup;

namespace MidiToEverything.App.ViewModels;

/// <summary>
/// Main-window view model (docs/02_Architecture.md §3.6). Subscribes to the engine and
/// marshals updates to the UI thread, batching the high-rate monitor feed on a timer so a
/// busy controller never freezes the UI (FR-2.4). Also drives the device-detection mode
/// toggle and manual rescan (B-1).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IMidiSource _source;
    private readonly ProfileManager _profiles;
    private readonly GatedInputSink _gate;
    private readonly LaunchPolicy _launchPolicy;
    private readonly IProfileRepository _repository;
    private readonly IUpdateChecker _updateChecker;
    private readonly IUpdateInstaller _updateInstaller;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _flushTimer;
    private readonly DispatcherTimer _updateTimer;
    private readonly ConcurrentQueue<MidiMessage> _incoming = new();
    private readonly FiringEvaluator _firing = new();

    public MainViewModel(IMidiSource source, ProfileManager profiles, GatedInputSink gate,
        LaunchPolicy launchPolicy, IProfileRepository repository,
        IUpdateChecker updateChecker, IUpdateInstaller updateInstaller)
    {
        _source = source;
        _profiles = profiles;
        _gate = gate;
        _launchPolicy = launchPolicy;
        _repository = repository;
        _updateChecker = updateChecker;
        _updateInstaller = updateInstaller;
        _dispatcher = Dispatcher.CurrentDispatcher;

        EmissionEnabled = gate.Enabled;
        _allowExternalLaunch = launchPolicy.Allowed;
        _runAtStartup = profiles.CurrentConfig.Settings.StartWithWindows;
        var settings = profiles.CurrentConfig.Settings;
        _autoUpdate = settings.AutoUpdate;
        _startMinimized = settings.StartMinimized;
        _closeToTray = settings.CloseToTray;
        _startEmissionEnabled = settings.StartEmissionEnabled;
        _emergencyHotkey = settings.EmergencyStopHotkey ?? "ctrl+alt+pause";
        _obsHost = settings.ObsHost;
        _obsPort = settings.ObsPort;
        _obsPassword = settings.ObsPassword;
        _updateChannel = settings.UpdateChannel;
        _updateCheckHours = settings.UpdateCheckHours;
        _monitorMaxLines = settings.Monitor.MaxLogLines;
        _monitorThrottleMs = settings.Monitor.UiThrottleMs;
        _logLevel = settings.LogLevel;
        _logRetentionDays = settings.LogRetentionDays;
        _crashAutoRestart = settings.CrashAutoRestart;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == Loc.Instance.Language) ?? Languages.FirstOrDefault();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        _isAutoDetect = settings.AutoDetectDevices;
        source.DetectionMode = _isAutoDetect ? MidiDetectionMode.AutoPolling : MidiDetectionMode.Manual;
        ApplyState(profiles.State);
        foreach (var device in source.Devices)
        {
            Devices.Add(device.Name);
        }

        _source.MessageReceived += OnMessageReceived;
        _source.DeviceConnected += OnDeviceConnected;
        _source.DeviceDisconnected += OnDeviceDisconnected;
        _profiles.Changed += OnProfileChanged;
        _gate.EnabledChanged += OnEmissionChanged;

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1, _monitorThrottleMs)),
        };
        _flushTimer.Tick += (_, _) => Flush();
        _flushTimer.Start();

        // Auto-update: check on startup (after the UI settles) and every 24 hours.
        _updateTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromHours(Math.Max(1, _updateCheckHours)),
        };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(manual: false);
        if (AutoUpdate)
        {
            _updateTimer.Start();
            _ = _dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(async () => await CheckForUpdatesAsync(manual: false)));
        }
    }

    public ObservableCollection<string> Devices { get; } = new();
    public ObservableCollection<MonitorEntry> Monitor { get; } = new();

    [ObservableProperty] private string _activeProfile = "—";
    [ObservableProperty] private string _contextWindow = "—";
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _emissionEnabled = true;
    [ObservableProperty] private bool _monitorPaused;
    [ObservableProperty] private bool _isAutoDetect = true;
    [ObservableProperty] private bool _allowExternalLaunch;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private string _obsHost = "localhost";
    [ObservableProperty] private int _obsPort = 4455;
    [ObservableProperty] private string _obsPassword = "";

    // Diagnostics / logging (Batch C).
    [ObservableProperty] private int _monitorMaxLines = 500;
    [ObservableProperty] private int _monitorThrottleMs = 30;
    [ObservableProperty] private string _logLevel = "Debug";
    [ObservableProperty] private int _logRetentionDays = 7;
    [ObservableProperty] private bool _crashAutoRestart = true;

    public IReadOnlyList<string> LogLevels { get; } =
        new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };

    // ── Startup / window ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray = true;
    [ObservableProperty] private bool _startEmissionEnabled = true;

    /// <summary>Global emergency-stop hotkey spec (e.g. "ctrl+alt+pause"); registered by MainWindow.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmergencyHotkeyValid))]
    private string _emergencyHotkey = "";

    /// <summary>True when the spec parses to a registrable hotkey (the field shows red otherwise).</summary>
    public bool EmergencyHotkeyValid => HotkeyParser.TryParse(EmergencyHotkey, out _, out _);

    partial void OnStartMinimizedChanged(bool value) => PersistSetting(s => s with { StartMinimized = value });
    partial void OnCloseToTrayChanged(bool value) => PersistSetting(s => s with { CloseToTray = value });
    partial void OnStartEmissionEnabledChanged(bool value) => PersistSetting(s => s with { StartEmissionEnabled = value });
    partial void OnEmergencyHotkeyChanged(string value) => PersistSetting(s => s with { EmergencyStopHotkey = value });

    private void PersistSetting(Func<AppSettings, AppSettings> mutate)
    {
        var config = _profiles.CurrentConfig;
        var updated = config with { Settings = mutate(config.Settings) };
        _repository.Save(updated);
        _profiles.Reload(updated);
    }

    // ── Auto-update ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool _autoUpdate = true;
    [ObservableProperty] private string _updateChannel = "stable";
    [ObservableProperty] private int _updateCheckHours = 24;

    public IReadOnlyList<string> UpdateChannels { get; } = new[] { "stable", "prerelease" };

    /// <summary>The available newer release, or null when up to date / not yet checked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(UpdateBannerText))]
    private UpdateInfo? _availableUpdate;

    /// <summary>Status line for the update flow (checking / downloading / error).</summary>
    [ObservableProperty] private string _updateStatus = "";

    public bool HasUpdate => AvailableUpdate is not null;

    public string UpdateBannerText =>
        AvailableUpdate is null ? "" : string.Format(Loc.T("update.available"), AvailableUpdate.Version);

    public string EmissionLabel => Loc.T(EmissionEnabled ? "main.status.running" : "main.status.stopped");
    public string DetectModeLabel => Loc.T(IsAutoDetect ? "main.detect.auto" : "main.detect.manual");

    /// <summary>Available UI languages (discovered from the translation files).</summary>
    public IReadOnlyList<LanguageOption> Languages => Loc.Instance.Languages;

    [ObservableProperty] private LanguageOption? _selectedLanguage;

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null || value.Code == Loc.Instance.Language)
        {
            return;
        }

        Loc.Instance.SetLanguage(value.Code);
        var config = _profiles.CurrentConfig;
        var updated = config with { Settings = config.Settings with { Language = value.Code } };
        _repository.Save(updated);
        _profiles.Reload(updated);
    }

    partial void OnEmissionEnabledChanged(bool value) => OnPropertyChanged(nameof(EmissionLabel));

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EmissionLabel));
        OnPropertyChanged(nameof(DetectModeLabel));
    }

    // External-launch opt-in (Q5): toggle the runtime gate and persist the choice.
    partial void OnAllowExternalLaunchChanged(bool value)
    {
        _launchPolicy.Allowed = value;
        var config = _profiles.CurrentConfig;
        var updated = config with { Settings = config.Settings with { AllowExternalLaunch = value } };
        _repository.Save(updated);
        _profiles.Reload(updated);
    }

    // Launch-at-startup: apply to the HKCU Run key and persist in config (so it survives
    // restart and stays in sync with the tray menu's toggle). _suppressStartupWrite guards
    // the reverse sync (config -> checkbox) from looping back here.
    private bool _suppressStartupWrite;

    partial void OnRunAtStartupChanged(bool value)
    {
        if (_suppressStartupWrite)
        {
            return;
        }

        WindowsStartup.SetEnabled(value, Environment.ProcessPath ?? string.Empty);
        var config = _profiles.CurrentConfig;
        var updated = config with { Settings = config.Settings with { StartWithWindows = value } };
        _repository.Save(updated);
        _profiles.Reload(updated);
    }

    // OBS connection settings: persist on edit; the OBS client reads them on next connect.
    partial void OnObsHostChanged(string value) => SaveObsSettings();
    partial void OnObsPortChanged(int value) => SaveObsSettings();
    partial void OnObsPasswordChanged(string value) => SaveObsSettings();

    private void SaveObsSettings()
    {
        var config = _profiles.CurrentConfig;
        var updated = config with
        {
            Settings = config.Settings with { ObsHost = ObsHost, ObsPort = ObsPort, ObsPassword = ObsPassword },
        };
        _repository.Save(updated);
        _profiles.Reload(updated);
    }

    // Device-detection mode toggle (B-1): switch between periodic polling and manual rescan (persisted).
    partial void OnIsAutoDetectChanged(bool value)
    {
        _source.DetectionMode = value ? MidiDetectionMode.AutoPolling : MidiDetectionMode.Manual;
        OnPropertyChanged(nameof(DetectModeLabel));
        PersistSetting(s => s with { AutoDetectDevices = value });
    }

    // Diagnostics / logging (Batch C).
    partial void OnMonitorMaxLinesChanged(int value) =>
        PersistSetting(s => s with { Monitor = s.Monitor with { MaxLogLines = value } });

    partial void OnMonitorThrottleMsChanged(int value)
    {
        _flushTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, value));
        PersistSetting(s => s with { Monitor = s.Monitor with { UiThrottleMs = value } });
    }

    partial void OnLogLevelChanged(string value)
    {
        App.LogLevelSwitch.MinimumLevel = App.ParseLogLevel(value);
        PersistSetting(s => s with { LogLevel = value });
    }

    partial void OnLogRetentionDaysChanged(int value) =>
        PersistSetting(s => s with { LogRetentionDays = value });

    partial void OnCrashAutoRestartChanged(bool value)
    {
        CrashReporter.AutoRestart = value;
        PersistSetting(s => s with { CrashAutoRestart = value });
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        var dir = Path.Combine(AppInfo.DataDirectory, "logs");
        try
        {
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    [RelayCommand]
    private void ToggleEmission() => _gate.Toggle();

    [RelayCommand]
    private void TogglePause() => MonitorPaused = !MonitorPaused;

    [RelayCommand]
    private void ClearMonitor() => Monitor.Clear();

    /// <summary>Force an immediate device re-scan (B-1; the manual "refresh" button).</summary>
    [RelayCommand]
    private void RescanDevices() => _source.Rescan();

    // ── Auto-update ───────────────────────────────────────────────────────────

    // Persist the toggle; (re)start or stop the periodic check accordingly.
    partial void OnAutoUpdateChanged(bool value)
    {
        var config = _profiles.CurrentConfig;
        var updated = config with { Settings = config.Settings with { AutoUpdate = value } };
        _repository.Save(updated);
        _profiles.Reload(updated);

        if (value)
        {
            _updateTimer.Start();
            _ = CheckForUpdatesAsync(manual: false);
        }
        else
        {
            _updateTimer.Stop();
        }
    }

    partial void OnUpdateChannelChanged(string value) =>
        PersistSetting(s => s with { UpdateChannel = value });

    partial void OnUpdateCheckHoursChanged(int value)
    {
        _updateTimer.Interval = TimeSpan.FromHours(Math.Max(1, value));
        PersistSetting(s => s with { UpdateCheckHours = value });
    }

    [RelayCommand]
    private Task CheckForUpdates() => CheckForUpdatesAsync(manual: true);

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (manual)
        {
            UpdateStatus = Loc.T("update.checking");
        }

        var update = await _updateChecker.GetUpdateAsync(
            AppInfo.Version, includePrerelease: UpdateChannel == "prerelease");
        AvailableUpdate = update;
        UpdateStatus = update is not null
            ? ""
            : manual ? Loc.T("update.upToDate") : "";
    }

    /// <summary>Download the installer and launch it, then exit so it can replace the running files.</summary>
    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (AvailableUpdate is not { } update)
        {
            return;
        }

        try
        {
            var progress = new Progress<double>(p => UpdateStatus = string.Format(Loc.T("update.downloading"), (int)(p * 100)));
            UpdateStatus = string.Format(Loc.T("update.downloading"), 0);
            var path = await _updateInstaller.DownloadAsync(update, progress);

            UpdateStatus = Loc.T("update.launching");
            _updateInstaller.Launch(path);
            System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatus = string.Format(Loc.T("update.failed"), ex.Message);
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        AvailableUpdate = null;
        UpdateStatus = "";
    }

    // ── Engine events (background threads) ────────────────────────────────────

    private void OnMessageReceived(object? sender, MidiMessage message) => _incoming.Enqueue(message);

    private void OnDeviceConnected(object? sender, MidiDeviceInfo device)
        => _dispatcher.BeginInvoke(() => { if (!Devices.Contains(device.Name)) Devices.Add(device.Name); });

    private void OnDeviceDisconnected(object? sender, MidiDeviceInfo device)
        => _dispatcher.BeginInvoke(() => Devices.Remove(device.Name));

    private void OnProfileChanged(object? sender, ActiveProfileState state)
        => _dispatcher.BeginInvoke(() => ApplyState(state));

    private void OnEmissionChanged(object? sender, bool enabled)
        => _dispatcher.BeginInvoke(() => EmissionEnabled = enabled);

    private void ApplyState(ActiveProfileState state)
    {
        ActiveProfile = state.Effective.Name;
        IsPinned = state.IsPinned;
        var process = string.IsNullOrEmpty(state.Window.ProcessName) ? "(なし)" : state.Window.ProcessName;
        ContextWindow = string.IsNullOrEmpty(state.Window.WindowTitle)
            ? process
            : $"{process} — {state.Window.WindowTitle}";

        // Keep the header checkbox in sync if the setting changed elsewhere (e.g. tray menu).
        _suppressStartupWrite = true;
        RunAtStartup = _profiles.CurrentConfig.Settings.StartWithWindows;
        _suppressStartupWrite = false;
    }

    // ── Batched UI update (UI thread) ─────────────────────────────────────────

    private void Flush()
    {
        if (_incoming.IsEmpty)
        {
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var drained = 0;
        while (_incoming.TryDequeue(out var message) && drained < 512)
        {
            drained++;
            // Always evaluate (the firing state is stateful and must track every message in order,
            // matching the engine); only add a row when not paused.
            var action = ResolveActionLabel(message);
            if (!MonitorPaused)
            {
                Monitor.Insert(0, new MonitorEntry(message, time, action)); // newest first
            }
        }

        var max = Math.Max(1, MonitorMaxLines);
        while (Monitor.Count > max)
        {
            Monitor.RemoveAt(Monitor.Count - 1);
        }
    }

    /// <summary>
    /// The action(s) that <em>actually fire</em> for this message — the same matching + trigger +
    /// edge evaluation the engine uses (<see cref="FiringEvaluator"/>), not merely "the signal
    /// matched". Null when nothing fires (e.g. a fire-on-increase binding on a decrease tick).
    /// </summary>
    private string? ResolveActionLabel(MidiMessage message)
    {
        var firings = _firing.Evaluate(message, _profiles.Current);
        if (firings.Count == 0)
        {
            return null;
        }

        var labels = firings
            .Select(f => f.Binding)
            .Where(b => b.Actions.Count > 0)
            .Select(DescribeBindingAction)
            .ToList();

        return labels.Count == 0 ? null : string.Join("  /  ", labels);
    }

    private static string DescribeBindingAction(MidiToEverything.Core.Domain.Binding binding)
    {
        var (kind, detail) = EditMapper.DescribeAction(binding.Actions[0]);
        var label = EditorHelp.ActionKindName(kind);
        detail = Shorten(detail);
        if (detail.Length > 0)
        {
            label += $" {detail}";
        }

        if (binding.Actions.Count > 1)
        {
            label += $" +{binding.Actions.Count - 1}";
        }

        return label;
    }

    private static string Shorten(string detail)
    {
        var s = detail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > 40 ? s[..39] + "…" : s;
    }

    public void Dispose()
    {
        _flushTimer.Stop();
        _updateTimer.Stop();
        _source.MessageReceived -= OnMessageReceived;
        _source.DeviceConnected -= OnDeviceConnected;
        _source.DeviceDisconnected -= OnDeviceDisconnected;
        _profiles.Changed -= OnProfileChanged;
        _gate.EnabledChanged -= OnEmissionChanged;
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
    }
}
