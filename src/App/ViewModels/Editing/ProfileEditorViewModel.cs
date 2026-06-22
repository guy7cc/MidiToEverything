using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Persistence;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MidiToEverything.App.ViewModels.Editing;

/// <summary>
/// Profile editor (docs/04_Roadmap.md M8): CRUD over profiles/bindings, MIDI "learn"
/// (FR-2.3), and import/export (FR-7.4). Saving persists the config and live-reloads the
/// running engine (FR-7.2).
/// </summary>
public partial class ProfileEditorViewModel : ObservableObject, IDisposable
{
    private readonly IProfileRepository _repository;
    private readonly ProfileManager _manager;
    private readonly IMidiSource _source;
    private volatile MidiMessage? _lastMessage;

    public ProfileEditorViewModel(IProfileRepository repository, ProfileManager manager, IMidiSource source)
    {
        _repository = repository;
        _manager = manager;
        _source = source;

        foreach (var profile in EditMapper.ToEditable(manager.CurrentConfig))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
        _source.MessageReceived += OnMessageReceived;
        LoadProcesses();
    }

    public ObservableCollection<EditableProfile> Profiles { get; } = new();

    /// <summary>Running windowed processes offered as target-process candidates.</summary>
    public ObservableCollection<RunningProcess> RunningProcesses { get; } = new();

    public Array SignalKinds { get; } = Enum.GetValues<SignalKind>();
    public Array TriggerModes { get; } = Enum.GetValues<TriggerMode>();
    public Array ActionKinds { get; } = Enum.GetValues<EditableActionKind>();

    [ObservableProperty] private EditableProfile? _selectedProfile;
    [ObservableProperty] private EditableBinding? _selectedBinding;
    [ObservableProperty] private RunningProcess? _selectedRunningProcess;
    [ObservableProperty] private string _lastSignalText = "(まだ受信していません)";

    /// <summary>Raised with true on Save, false on Cancel, so the window can close.</summary>
    public event EventHandler<bool>? CloseRequested;

    private void OnMessageReceived(object? sender, MidiMessage message)
    {
        _lastMessage = message;
        var text = $"{message.Device} ch{message.Channel} {message.Type} {message.Number} = {message.Value}";
        // Marshal to the UI thread for the status text.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => LastSignalText = text);
    }

    [RelayCommand]
    private void AddProfile()
    {
        var profile = new EditableProfile { Id = "", Name = "新しいプロファイル", ProcessNames = "" };
        Profiles.Add(profile);
        SelectedProfile = profile;
    }

    private void LoadProcesses()
    {
        RunningProcesses.Clear();
        foreach (var process in RunningProcessScanner.Scan())
        {
            RunningProcesses.Add(process);
        }
    }

    [RelayCommand]
    private void RefreshProcesses() => LoadProcesses();

    /// <summary>Append the selected running process to the profile's target list (no duplicates).</summary>
    [RelayCommand]
    private void AddProcess()
    {
        if (SelectedProfile is null || SelectedRunningProcess is null)
        {
            return;
        }

        var existing = SelectedProfile.ProcessNames
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (existing.Any(name => string.Equals(name, SelectedRunningProcess.Exe, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        existing.Add(SelectedRunningProcess.Exe);
        SelectedProfile.ProcessNames = string.Join(", ", existing);
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile is { IsBase: false } profile)
        {
            Profiles.Remove(profile);
            SelectedProfile = Profiles.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void AddBinding()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var binding = new EditableBinding();
        if (_lastMessage is not null)
        {
            ApplyLearn(binding, _lastMessage); // seed from the last received signal if any
        }

        SelectedProfile.Bindings.Add(binding);
        SelectedBinding = binding;
    }

    [RelayCommand]
    private void RemoveBinding()
    {
        if (SelectedProfile is not null && SelectedBinding is not null)
        {
            SelectedProfile.Bindings.Remove(SelectedBinding);
            SelectedBinding = null;
        }
    }

    /// <summary>Capture the last received MIDI signal into the selected binding (FR-2.3).</summary>
    [RelayCommand]
    private void Learn()
    {
        if (SelectedBinding is not null && _lastMessage is { } message)
        {
            ApplyLearn(SelectedBinding, message);
        }
    }

    private static void ApplyLearn(EditableBinding binding, MidiMessage message)
    {
        binding.Signal.Device = message.Device;
        binding.Signal.Channel = message.Channel.ToString();
        binding.Signal.Type = message.Type switch
        {
            MidiMessageType.NoteOn => SignalKind.NoteOn,
            MidiMessageType.NoteOff => SignalKind.NoteOff,
            MidiMessageType.ControlChange => SignalKind.Cc,
            MidiMessageType.PitchBend => SignalKind.PitchBend,
            MidiMessageType.ProgramChange => SignalKind.ProgramChange,
            _ => SignalKind.NoteOn,
        };
        binding.Signal.NumberText = message.Number?.ToString() ?? "";
    }

    [RelayCommand]
    private void Save()
    {
        var config = EditMapper.ToConfig(Profiles, _manager.CurrentConfig);
        _repository.Save(config);
        _manager.Reload(config);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    [RelayCommand]
    private void Export()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "midi-profiles.json",
        };
        if (dialog.ShowDialog() == true)
        {
            var config = EditMapper.ToConfig(Profiles, _manager.CurrentConfig);
            File.WriteAllText(dialog.FileName, ConfigSerializer.Serialize(config));
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var config = ConfigSerializer.Deserialize(File.ReadAllText(dialog.FileName));
        Profiles.Clear();
        foreach (var profile in EditMapper.ToEditable(config))
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
    }

    public void Dispose() => _source.MessageReceived -= OnMessageReceived;
}
