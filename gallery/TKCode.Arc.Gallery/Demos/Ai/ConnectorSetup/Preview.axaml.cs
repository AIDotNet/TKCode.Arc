using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The simulated card; Replay puts a fresh card with a new simulation in place so the scripted run plays again.</summary>
public partial class ConnectorSetupPreviewDemo : UserControl
{
    public ConnectorSetupPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnReplay(object? sender, RoutedEventArgs e)
    {
        Host.Child = new ConnectorSetup { Simulation = ConnectorSetupDemoData.Simulation() };
    }
}
