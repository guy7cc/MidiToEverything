using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MidiToEverything.App.Localization;
using MidiToEverything.Core.Application;
using MidiToEverything.Core.Application.Ports;
using MidiToEverything.Core.Domain;
using MidiToEverything.Core.Mapping;
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
    private readonly IUiaElementPicker _uiaPicker;
    private readonly ActionExecutor _executor;
    private volatile MidiMessage? _lastMessage;
    private EditableBinding? _editingOriginal; // the list binding being edited (null = creating new)
    private bool _loadingDraft;                // guards programmatic SelectedBinding changes
    private EditableProfile? _subscribedProfile;
    // Debounces auto-save: every change except an in-progress binding signal persists automatically.
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public ProfileEditorViewModel(IProfileRepository repository, ProfileManager manager, IMidiSource source,
        IUiaElementPicker uiaPicker, ActionExecutor executor)
    {
        _repository = repository;
        _manager = manager;
        _source = source;
        _uiaPicker = uiaPicker;
        _executor = executor;
        _saveTimer.Tick += (_, _) => SaveNow();

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
    public string[] UiaVerbs { get; } = { "invoke", "toggle", "setvalue" };
    public string[] HttpMethods { get; } = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    public string[] ObsOps { get; } =
    {
        "sceneswitch", "togglerecord", "togglestream", "togglerecordpause", "togglemute",
        "startrecord", "stoprecord", "startstream", "stopstream",
    };

    [ObservableProperty] private EditableProfile? _selectedProfile;
    [ObservableProperty] private EditableBinding? _selectedBinding;

    /// <summary>The binding currently being edited (a draft). Committed to the list on "save".</summary>
    [ObservableProperty] private EditableBinding? _draftBinding;

    /// <summary>True while a binding draft is open in the editor.</summary>
    public bool HasDraft => DraftBinding is not null;

    partial void OnDraftBindingChanged(EditableBinding? value) => OnPropertyChanged(nameof(HasDraft));

    // Switching profiles abandons any open draft, and re-targets field auto-save at the new profile.
    partial void OnSelectedProfileChanged(EditableProfile? value)
    {
        if (_subscribedProfile is not null)
        {
            _subscribedProfile.PropertyChanged -= OnProfileFieldChanged;
        }

        _subscribedProfile = value;
        if (value is not null)
        {
            value.PropertyChanged += OnProfileFieldChanged;
        }

        DiscardDraft();
    }

    // Profile name / match pattern / priority edits persist automatically (debounced).
    private void OnProfileFieldChanged(object? sender, PropertyChangedEventArgs e) => RequestAutoSave();

    /// <summary>Persist after a short idle (coalesces rapid edits like typing a name).</summary>
    private void RequestAutoSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Persist the current profiles to disk and reload the running engine immediately.</summary>
    private void SaveNow()
    {
        _saveTimer.Stop();
        var config = EditMapper.ToConfig(Profiles, _manager.CurrentConfig);
        _repository.Save(config);
        _manager.Reload(config);
    }

    // Clicking a binding in the list opens a working copy to edit (changes apply on commit).
    partial void OnSelectedBindingChanged(EditableBinding? value)
    {
        if (_loadingDraft || value is null)
        {
            return;
        }

        _editingOriginal = value;
        DraftBinding = value.Clone();
        SetLearnStatus(Loc.T("learn.editing"), isError: false);
    }

    [ObservableProperty] private RunningProcess? _selectedRunningProcess;
    [ObservableProperty] private string _processNameInput = "";
    [ObservableProperty] private string _matchStatus = "";
    [ObservableProperty] private bool _matchStatusIsError;
    [ObservableProperty] private string _lastSignalText = Loc.T("editor.notReceived");
    [ObservableProperty] private string _learnStatus = "";
    [ObservableProperty] private bool _learnStatusIsError;

    // Selecting a running process fills the input box with its exe (the user may still edit it).
    partial void OnSelectedRunningProcessChanged(RunningProcess? value)
    {
        if (value is not null)
        {
            ProcessNameInput = value.Exe;
        }
    }

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
        var profile = new EditableProfile { Id = Guid.NewGuid().ToString("N")[..8], Name = Loc.T("editor.newProfile") };
        Profiles.Add(profile);
        SelectedProfile = profile;
        SaveNow(); // profile add persists immediately
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

    /// <summary>
    /// Fold the selected/typed process into the profile's match regex (OR-merged). The user can
    /// pick a running process or type a name; the result is notified, including failures
    /// (empty name, or a hand-edited pattern that can no longer be extended automatically).
    /// </summary>
    [RelayCommand]
    private void AddProcessToPattern()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var name = ProcessNameInput?.Trim() ?? "";
        if (name.Length == 0 && SelectedRunningProcess is not null)
        {
            name = SelectedRunningProcess.Exe;
        }

        var result = MatchPatternBuilder.AddProcess(SelectedProfile.Pattern, name);
        switch (result.Status)
        {
            case AddProcessStatus.Added:
                SelectedProfile.Pattern = result.Pattern;
                SetMatchStatus($"「{name}」を判別パターンに追加しました。", isError: false);
                ProcessNameInput = "";
                break;
            case AddProcessStatus.AlreadyPresent:
                SetMatchStatus($"「{name}」はすでに含まれています。", isError: false);
                break;
            case AddProcessStatus.EmptyName:
                SetMatchStatus("起動中プロセスを選ぶか、プロセス名を入力してください。", isError: true);
                break;
            case AddProcessStatus.ReconstructionFailed:
                SetMatchStatus("正規表現の自動再構成に失敗しました。下の正規表現を手動で編集してください。", isError: true);
                break;
        }
    }

    private void SetMatchStatus(string message, bool isError)
    {
        MatchStatus = message;
        MatchStatusIsError = isError;
    }

    [RelayCommand]
    private void RemoveProfile()
    {
        if (SelectedProfile is { IsBase: false } profile)
        {
            Profiles.Remove(profile);
            SelectedProfile = Profiles.FirstOrDefault();
            SaveNow(); // profile delete persists immediately
        }
    }

    /// <summary>
    /// Add a new binding to the profile (persisted immediately) and open it for editing.
    /// Its signal content is a draft until "バインディングを保存" is pressed.
    /// </summary>
    [RelayCommand]
    private void AddBinding()
    {
        if (SelectedProfile is null)
        {
            SetLearnStatus(Loc.T("learn.selectProfile"), isError: true);
            return;
        }

        var binding = new EditableBinding();
        SelectedProfile.Bindings.Add(binding);
        SaveNow(); // binding add persists immediately

        _editingOriginal = binding;
        DraftBinding = binding.Clone();
        SetSelectedBindingGuarded(binding);
        SetLearnStatus(Loc.T("learn.added"), isError: false);
    }

    /// <summary>Remove the selected (committed) binding and close the editor.</summary>
    [RelayCommand]
    private void RemoveBinding()
    {
        if (SelectedProfile is not null && SelectedBinding is not null)
        {
            SelectedProfile.Bindings.Remove(SelectedBinding);
            SaveNow(); // binding delete persists immediately
        }

        DiscardDraft();
    }

    /// <summary>Commit the draft's signal content into the list and persist it.</summary>
    [RelayCommand]
    private void CommitBinding()
    {
        if (DraftBinding is null || SelectedProfile is null)
        {
            SetLearnStatus(Loc.T("learn.noDraft"), isError: true);
            return;
        }

        if (_editingOriginal is not null)
        {
            _editingOriginal.CopyValuesFrom(DraftBinding);
            SetSelectedBindingGuarded(_editingOriginal);
        }
        else
        {
            var committed = DraftBinding.Clone();
            SelectedProfile.Bindings.Add(committed);
            _editingOriginal = committed;
            DraftBinding = committed.Clone();
            SetSelectedBindingGuarded(committed);
        }

        SaveNow();
        SetLearnStatus(Loc.T("learn.saved"), isError: false);
    }

    private void DiscardDraft()
    {
        _editingOriginal = null;
        DraftBinding = null;
        SetSelectedBindingGuarded(null);
        SetLearnStatus("", isError: false);
    }

    private void SetSelectedBindingGuarded(EditableBinding? value)
    {
        _loadingDraft = true;
        SelectedBinding = value;
        _loadingDraft = false;
    }

    /// <summary>
    /// Capture the last received MIDI signal into the selected binding (FR-2.3). If no binding is
    /// selected, a new one is created in the current profile so "learn" always does something.
    /// Reports clearly when it can't proceed (no signal received yet, or no profile selected).
    /// </summary>
    [RelayCommand]
    private void Learn()
    {
        if (_lastMessage is not { } message)
        {
            SetLearnStatus(Loc.T("learn.noSignal"), isError: true);
            return;
        }

        if (DraftBinding is null)
        {
            if (SelectedProfile is null)
            {
                SetLearnStatus(Loc.T("learn.selectProfile"), isError: true);
                return;
            }

            // No draft open yet — start a new one so "learn" always does something.
            SetSelectedBindingGuarded(null);
            _editingOriginal = null;
            DraftBinding = new EditableBinding();
        }

        ApplyLearn(DraftBinding, message);

        var desc = $"{DraftBinding.Signal.Type} 番号{message.Number?.ToString() ?? "-"} ch{message.Channel}";
        SetLearnStatus(string.Format(Loc.T("learn.captured"), desc), isError: false);
    }

    /// <summary>Capture the UI element under the cursor (after a short hover delay) into the draft.</summary>
    [RelayCommand]
    private async Task PickUiaElement()
    {
        if (DraftBinding is null)
        {
            SetLearnStatus(Loc.T("learn.selectBinding"), isError: true);
            return;
        }

        SetLearnStatus(Loc.T("learn.uiaHover"), isError: false);
        var pick = await _uiaPicker.PickAsync();
        if (pick is null)
        {
            SetLearnStatus(Loc.T("learn.uiaFail"), isError: true);
            return;
        }

        DraftBinding.UiaWindow = pick.WindowPattern;
        DraftBinding.Detail = pick.ElementName;
        SetLearnStatus(string.Format(Loc.T("learn.uiaGot"), pick.ElementName, pick.WindowPattern), isError: false);
    }

    private void SetLearnStatus(string message, bool isError)
    {
        LearnStatus = message;
        LearnStatusIsError = isError;
    }

    /// <summary>Raised when a complex action requests its dedicated config dialog.</summary>
    public event EventHandler? ActionConfigRequested;

    /// <summary>Status line in the action-config dialog (test-run result).</summary>
    [ObservableProperty] private string _testStatus = "";

    [RelayCommand]
    private void OpenActionConfig()
    {
        if (DraftBinding is not null)
        {
            TestStatus = "";
            ActionConfigRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Run the draft action once with its current (unsaved) settings, to verify it works.</summary>
    [RelayCommand]
    private void TestAction()
    {
        if (DraftBinding is null)
        {
            return;
        }

        try
        {
            var action = EditMapper.ToAction(DraftBinding);
            var binding = new MidiToEverything.Core.Domain.Binding
            {
                Signal = new Signal(),
                Actions = new[] { action },
            };
            // Value-driven actions fire on Change; the rest fire on Press.
            var trigger = action is MidiOutAction { UseInputValue: true }
                ? new TriggerResult(TriggerPhase.Change, 1.0)
                : new TriggerResult(TriggerPhase.Press, 0);
            _executor.Execute(binding, trigger, new MidiMessage("test", 1, MidiMessageType.NoteOn, 0, 127));
            TestStatus = Loc.T("test.ran");
        }
        catch (Exception ex)
        {
            TestStatus = string.Format(Loc.T("test.failed"), ex.Message);
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
    private void Close() => CloseRequested?.Invoke(this, false);

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
        SaveNow(); // imported config persists immediately
    }

    public void Dispose()
    {
        if (_saveTimer.IsEnabled)
        {
            SaveNow(); // flush a pending debounced edit before closing
        }

        _saveTimer.Stop();
        _source.MessageReceived -= OnMessageReceived;
        if (_subscribedProfile is not null)
        {
            _subscribedProfile.PropertyChanged -= OnProfileFieldChanged;
        }
    }
}
