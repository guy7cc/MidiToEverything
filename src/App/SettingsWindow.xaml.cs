using System.Windows;

namespace MidiToEverything.App;

/// <summary>
/// Settings dialog. Bound to the shared <see cref="ViewModels.MainViewModel"/> (passed as
/// DataContext), so each change persists immediately just as the inline controls did.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        // Match the other windows: DWM-round the corners (Windows 11+) for a consistent look.
        SourceInitialized += (_, _) => WindowChromeFix.Apply(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
