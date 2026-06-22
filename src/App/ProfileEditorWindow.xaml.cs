using System.Windows;
using MidiToEverything.App.ViewModels.Editing;

namespace MidiToEverything.App;

/// <summary>
/// Profile editor window (docs/04_Roadmap.md M8). Shares the main window's themed chrome
/// (custom title bar + maximize fix). Closes via the view model's events; the title-bar close
/// button cancels.
/// </summary>
public partial class ProfileEditorWindow : Window
{
    private readonly ProfileEditorViewModel _viewModel;

    public ProfileEditorWindow(ProfileEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.ActionConfigRequested += OnActionConfigRequested;
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowChromeFix.Apply(this);
        UpdateMaxRestoreGlyph();
    }

    private void OnCloseRequested(object? sender, bool saved)
    {
        DialogResult = saved;
        Close();
    }

    private void OnActionConfigRequested(object? sender, EventArgs e)
    {
        var dialog = new ActionConfigWindow { Owner = this, DataContext = _viewModel };
        dialog.ShowDialog();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.ActionConfigRequested -= OnActionConfigRequested;
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreGlyph()
        => MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
}
