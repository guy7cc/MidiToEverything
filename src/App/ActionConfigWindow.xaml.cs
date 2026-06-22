using System.Windows;

namespace MidiToEverything.App;

/// <summary>
/// Dedicated configuration dialog for a complex action (docs/05 §3.5). Hosts the action's fields,
/// per-kind instructions, and an unsaved-settings test-run. DataContext is the editor view model;
/// the fields bind to its current draft binding.
/// </summary>
public partial class ActionConfigWindow : Window
{
    public ActionConfigWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowChromeFix.Apply(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
