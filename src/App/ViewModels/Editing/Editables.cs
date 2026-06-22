using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MidiToEverything.Core.Domain;

namespace MidiToEverything.App.ViewModels.Editing;

/// <summary>The action shapes the editor exposes (a single action per binding; macros via JSON).</summary>
public enum EditableActionKind { Key, MouseClick, Scroll, CursorMove, SwitchProfile, None }

/// <summary>Editable view of a <see cref="Signal"/> (docs/03_ProfileSchema.md §1).</summary>
public partial class EditableSignal : ObservableObject
{
    [ObservableProperty] private string _device = Signal.AnyDevice;
    [ObservableProperty] private string _channel = Signal.AnyChannel;
    [ObservableProperty] private SignalKind _type = SignalKind.NoteOn;
    [ObservableProperty] private string _numberText = "";
}

/// <summary>Editable view of a <see cref="Binding"/> with a flattened single-action form.</summary>
public partial class EditableBinding : ObservableObject
{
    [ObservableProperty] private EditableSignal _signal = new();
    [ObservableProperty] private TriggerMode _mode = TriggerMode.Trigger;
    [ObservableProperty] private EditableActionKind _actionKind = EditableActionKind.Key;
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _label = "";
}

/// <summary>Editable view of a <see cref="Profile"/>. The base profile has no match rule.</summary>
public partial class EditableProfile : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isBase;
    [ObservableProperty] private string _processNames = "";
    [ObservableProperty] private string _titlePattern = "";
    [ObservableProperty] private int _priority;

    public ObservableCollection<EditableBinding> Bindings { get; } = new();

    public string DisplayName => IsBase ? $"{Name}（基本）" : Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}
