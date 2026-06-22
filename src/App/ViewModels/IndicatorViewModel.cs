using CommunityToolkit.Mvvm.ComponentModel;

namespace MidiToEverything.App.ViewModels;

/// <summary>
/// A live indicator for one physical control (knob/fader/key), shown in the visualizer
/// (FR-3.1/3.2). <see cref="Value"/> is normalized 0..1 for the gauge; <see cref="IsActive"/>
/// highlights a pressed note.
/// </summary>
public partial class IndicatorViewModel : ObservableObject
{
    public IndicatorViewModel(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _valueText = "—";
}
