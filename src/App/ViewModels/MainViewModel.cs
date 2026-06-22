using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Infrastructure.Input;

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
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _flushTimer;
    private readonly ConcurrentQueue<MidiMessage> _incoming = new();

    public MainViewModel(IMidiSource source, ProfileManager profiles, GatedInputSink gate)
    {
        _source = source;
        _profiles = profiles;
        _gate = gate;
        _dispatcher = Dispatcher.CurrentDispatcher;

        EmissionEnabled = gate.Enabled;
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

    public string EmissionLabel => EmissionEnabled ? "稼働中" : "停止中 (緊急停止)";
    public string DetectModeLabel => IsAutoDetect ? "自動更新" : "手動更新";

    partial void OnEmissionEnabledChanged(bool value) => OnPropertyChanged(nameof(EmissionLabel));

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
            Monitor.Insert(0, new MonitorEntry(message, time)); // newest first
        }

        while (Monitor.Count > MaxLogRows)
        {
            Monitor.RemoveAt(Monitor.Count - 1);
        }
    }

    public void Dispose()
    {
        _flushTimer.Stop();
        _source.MessageReceived -= OnMessageReceived;
        _source.DeviceConnected -= OnDeviceConnected;
        _source.DeviceDisconnected -= OnDeviceDisconnected;
        _profiles.Changed -= OnProfileChanged;
        _gate.EnabledChanged -= OnEmissionChanged;
    }
}
