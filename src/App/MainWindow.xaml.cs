using System.Windows;
using MidiToEverything.Core;

namespace MidiToEverything.App;

/// <summary>
/// Shell window. For M0 it only confirms the host is up; later milestones
/// add the device list, input monitor and visualizer (docs/04_Roadmap.md M7).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = $"v{AppInfo.Version} — scaffold ready (M0)";
    }
}
