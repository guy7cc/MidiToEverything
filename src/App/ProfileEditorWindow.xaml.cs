using System.Windows;
using MidiToEverything.App.ViewModels.Editing;

namespace MidiToEverything.App;

/// <summary>Profile editor window (docs/04_Roadmap.md M8). Closes via the view model's events.</summary>
public partial class ProfileEditorWindow : Window
{
    private readonly ProfileEditorViewModel _viewModel;

    public ProfileEditorWindow(ProfileEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool saved)
    {
        DialogResult = saved;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
