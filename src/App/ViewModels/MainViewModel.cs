using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiToEverything.App.Localization;
using MidiToEverything.App.ViewModels.Editing;
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
    private const int MaxLogRows = 500;

    private readonly IMidiSource _source;
    private readonly ProfileManager _profiles;
    private readonly GatedInputSink _gate;
    private readonly LaunchPolicy _launchPolicy;
    private readonly IProfileRepository _repository;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _flushTimer;
    private readonly ConcurrentQueue<MidiMessage> _incoming = new();
    private readonly MappingResolver _resolver = new();

    public MainViewModel(IMidiSource source, ProfileManager profiles, GatedInputSink gate,
        LaunchPolicy launchPolicy, IProfileRepository repository)
    {
        _source = source;
        _profiles = profiles;
        _gate = gate;
        _launchPolicy = launchPolicy;
        _repository = repository;
        _dispatcher = Dispatcher.CurrentDispatcher;

        EmissionEnabled = gate.Enabled;
        _allowExternalLaunch = launchPolicy.Allowed;
        _runAtStartup = profiles.CurrentConfig.Settings.StartWithWindows;
        var settings = profiles.CurrentConfig.Settings;
        _obsHost = settings.ObsHost;
        _obsPort = settings.ObsPort;
        _obsPassword = settings.ObsPassword;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == Loc.Instance.Language) ?? Languages.FirstOrDefault();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        _isAutoDetect = source.DetectionMode == MidiDetectionMode.AutoPolling;
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
            Interval = TimeSpan.FromMilliseconds(30),
        };
        _flushTimer.Tick += (_, _) => Flush();
        _flushTimer.Start();
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

    // Device-detection mode toggle (B-1): switch between periodic polling and manual rescan.
    partial void OnIsAutoDetectChanged(bool value)
    {
        _source.DetectionMode = value ? MidiDetectionMode.AutoPolling : MidiDetectionMode.Manual;
        OnPropertyChanged(nameof(DetectModeLabel));
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
        if (_incoming.IsEmpty || MonitorPaused)
        {
            // Still drain when paused so the queue does not grow unbounded.
            while (_incoming.TryDequeue(out _)) { }
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var drained = 0;
        while (_incoming.TryDequeue(out var message) && drained < 512)
        {
            drained++;
            Monitor.Insert(0, new MonitorEntry(message, time, ResolveActionLabel(message))); // newest first
        }

        while (Monitor.Count > MaxLogRows)
        {
            Monitor.RemoveAt(Monitor.Count - 1);
        }
    }

    /// <summary>
    /// Find the action this input maps to in the active profile layers (the same resolution the
    /// engine uses), so the monitor can show it. Independent of the emission gate / trigger state:
    /// it reports "this input has a mapping", not "it fired just now". Null when nothing matches.
    /// </summary>
    private string? ResolveActionLabel(MidiMessage message)
    {
        var resolution = _resolver.ResolveAll(message, _profiles.Current);
        if (!resolution.ShouldEmit || resolution.Bindings.Count == 0)
        {
            return null;
        }

        // Several bindings can share one control (e.g. a relative knob's increase/decrease split);
        // show each action so the monitor reflects everything the input drives.
        var labels = resolution.Bindings
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
        _source.MessageReceived -= OnMessageReceived;
        _source.DeviceConnected -= OnDeviceConnected;
        _source.DeviceDisconnected -= OnDeviceDisconnected;
        _profiles.Changed -= OnProfileChanged;
        _gate.EnabledChanged -= OnEmissionChanged;
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
    }
}
