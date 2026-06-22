using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App.ViewModels.Editing;

/// <summary>The action shapes the editor exposes (a single action per binding; macros via JSON).</summary>
public enum EditableActionKind
{
    Key, MouseClick, Scroll, CursorMove, WindowControl,
    MediaKey, TypeText, Launch, SetVolume, Uia,
    VirtualDesktop, WindowsToggle, Brightness, Http, Osc, Obs, MidiOut,
    Macro, Toggle, Plugin, SwitchProfile, None,
}

/// <summary>Editable view of a <see cref="Signal"/> (docs/03_ProfileSchema.md §1).</summary>
public partial class EditableSignal : ObservableObject
{
    [ObservableProperty] private string _device = Signal.AnyDevice;
    [ObservableProperty] private string _channel = Signal.AnyChannel;
    [ObservableProperty] private SignalKind _type = SignalKind.NoteOn;
    [ObservableProperty] private string _numberText = "";

    public EditableSignal Clone() => new() { Device = Device, Channel = Channel, Type = Type, NumberText = NumberText };

    public void CopyFrom(EditableSignal other)
    {
        Device = other.Device;
        Channel = other.Channel;
        Type = other.Type;
        NumberText = other.NumberText;
    }
}

/// <summary>Editable view of a <see cref="Binding"/> with a flattened single-action form.</summary>
public partial class EditableBinding : ObservableObject
{
    [ObservableProperty] private EditableSignal _signal = new();
    [ObservableProperty] private TriggerMode _mode = TriggerMode.Trigger;
    [ObservableProperty] private EditableActionKind _actionKind = EditableActionKind.Key;
    [ObservableProperty] private string _detail = "";

    /// <summary>Launch action: command-line arguments.</summary>
    [ObservableProperty] private string _arguments = "";

    /// <summary>Launch action: working directory.</summary>
    [ObservableProperty] private string _workingDir = "";

    /// <summary>Uia action: target-window regex ("process\ntitle").</summary>
    [ObservableProperty] private string _uiaWindow = "";

    /// <summary>Uia action: verb (invoke / toggle / setvalue).</summary>
    [ObservableProperty] private string _uiaVerb = "invoke";

    /// <summary>Uia action: value for SetValue.</summary>
    [ObservableProperty] private string _uiaValue = "";

    /// <summary>Http action: HTTP method.</summary>
    [ObservableProperty] private string _httpMethod = "GET";

    /// <summary>Http action: request body.</summary>
    [ObservableProperty] private string _httpBody = "";

    /// <summary>Osc action: target "host:port".</summary>
    [ObservableProperty] private string _oscTarget = "";

    /// <summary>Obs action: operation name (sceneswitch / togglerecord / ...).</summary>
    [ObservableProperty] private string _obsOpName = "togglerecord";

    /// <summary>MidiOut action: message spec "kind ch data1 data2" (e.g. "cc 1 7 64").</summary>
    [ObservableProperty] private string _midiOutSpec = "cc 1 7 64";

    /// <summary>Macro action: delay between steps (ms).</summary>
    [ObservableProperty] private string _macroDelay = "30";

    /// <summary>Toggle action: LED feedback spec "device note [ch]" (optional).</summary>
    [ObservableProperty] private string _toggleLed = "";

    /// <summary>Plugin action: argument passed to the plugin command.</summary>
    [ObservableProperty] private string _pluginArg = "";

    [ObservableProperty] private string _label = "";

    /// <summary>A deep copy, used as the editable draft so changes only apply on commit.</summary>
    public EditableBinding Clone() => new()
    {
        Signal = Signal.Clone(),
        Mode = Mode,
        ActionKind = ActionKind,
        Detail = Detail,
        Arguments = Arguments,
        WorkingDir = WorkingDir,
        UiaWindow = UiaWindow,
        UiaVerb = UiaVerb,
        UiaValue = UiaValue,
        HttpMethod = HttpMethod,
        HttpBody = HttpBody,
        OscTarget = OscTarget,
        ObsOpName = ObsOpName,
        MidiOutSpec = MidiOutSpec,
        MacroDelay = MacroDelay,
        ToggleLed = ToggleLed,
        PluginArg = PluginArg,
        Label = Label,
    };

    /// <summary>Copy values from another binding into this one (commit a draft into the list item).</summary>
    public void CopyValuesFrom(EditableBinding other)
    {
        Signal.CopyFrom(other.Signal);
        Mode = other.Mode;
        ActionKind = other.ActionKind;
        Detail = other.Detail;
        Arguments = other.Arguments;
        WorkingDir = other.WorkingDir;
        UiaWindow = other.UiaWindow;
        UiaVerb = other.UiaVerb;
        UiaValue = other.UiaValue;
        HttpMethod = other.HttpMethod;
        HttpBody = other.HttpBody;
        OscTarget = other.OscTarget;
        ObsOpName = other.ObsOpName;
        MidiOutSpec = other.MidiOutSpec;
        MacroDelay = other.MacroDelay;
        ToggleLed = other.ToggleLed;
        PluginArg = other.PluginArg;
        Label = other.Label;
    }
}

/// <summary>Editable view of a <see cref="Profile"/>. The base profile has no match rule.</summary>
public partial class EditableProfile : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isBase;

    /// <summary>Unified window-match regex (matched against "process\ntitle").</summary>
    [ObservableProperty] private string _pattern = "";
    [ObservableProperty] private int _priority;

    public ObservableCollection<EditableBinding> Bindings { get; } = new();

    public string DisplayName => IsBase ? $"{Name}（基本）" : Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}
