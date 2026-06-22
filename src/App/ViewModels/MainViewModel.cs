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
/// marshals updates to the UI thread, batching the high-rate monitor/visualizer feed on a
/// timer so a busy controller never freezes the UI (FR-2.4).
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
    private readonly Dictionary<string, IndicatorViewModel> _indicatorIndex = new();

    public MainViewModel(IMidiSource source, ProfileManager profiles, GatedInputSink gate)
    {
        _source = source;
        _profiles = profiles;
        _gate = gate;
        _dispatcher = Dispatcher.CurrentDispatcher;

        EmissionEnabled = gate.Enabled;
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
    public ObservableCollection<IndicatorViewModel> Indicators { get; } = new();

    [ObservableProperty] private string _activeProfile = "—";
    [ObservableProperty] private string _contextWindow = "—";
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _emissionEnabled = true;
    [ObservableProperty] private bool _monitorPaused;

    public string EmissionLabel => EmissionEnabled ? "稼働中" : "停止中 (緊急停止)";

    partial void OnEmissionEnabledChanged(bool value) => OnPropertyChanged(nameof(EmissionLabel));

    [RelayCommand]
    private void ToggleEmission() => _gate.Toggle();

    [RelayCommand]
    private void TogglePause() => MonitorPaused = !MonitorPaused;

    [RelayCommand]
    private void ClearMonitor() => Monitor.Clear();

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
        if (_incoming.IsEmpty)
        {
            return;
        }

        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var drained = 0;
        while (_incoming.TryDequeue(out var message) && drained < 512)
        {
            drained++;
            UpdateIndicator(message);

            if (!MonitorPaused)
            {
                Monitor.Insert(0, new MonitorEntry(message, time)); // newest first
            }
        }

        while (Monitor.Count > MaxLogRows)
        {
            Monitor.RemoveAt(Monitor.Count - 1);
        }
    }

    private void UpdateIndicator(MidiMessage m)
    {
        var (key, label) = Describe(m);
        if (!_indicatorIndex.TryGetValue(key, out var indicator))
        {
            indicator = new IndicatorViewModel(key, label);
            _indicatorIndex[key] = indicator;
            Indicators.Add(indicator);
        }

        switch (m.Type)
        {
            case MidiMessageType.NoteOn:
                indicator.IsActive = true;
                indicator.Value = m.Value / 127.0;
                indicator.ValueText = m.Value.ToString();
                break;
            case MidiMessageType.NoteOff:
                indicator.IsActive = false;
                indicator.Value = 0;
                indicator.ValueText = "off";
                break;
            case MidiMessageType.ControlChange:
                indicator.Value = m.Value / 127.0;
                indicator.ValueText = m.Value.ToString();
                break;
            case MidiMessageType.PitchBend:
                indicator.Value = m.Value / 16383.0;
                indicator.ValueText = m.Value.ToString();
                break;
            case MidiMessageType.ProgramChange:
                indicator.Value = m.Value / 127.0;
                indicator.ValueText = m.Value.ToString();
                break;
        }
    }

    private static (string Key, string Label) Describe(MidiMessage m)
    {
        var prefix = $"{m.Device}|ch{m.Channel}";
        return m.Type switch
        {
            MidiMessageType.NoteOn or MidiMessageType.NoteOff =>
                ($"{prefix}|note{m.Number}", $"Note {m.Number} {MonitorEntry.NoteName(m.Number!.Value)}"),
            MidiMessageType.ControlChange => ($"{prefix}|cc{m.Number}", $"CC {m.Number}"),
            MidiMessageType.PitchBend => ($"{prefix}|pitch", "Pitch Bend"),
            MidiMessageType.ProgramChange => ($"{prefix}|prog", "Program"),
            _ => ($"{prefix}|other", "Other"),
        };
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
