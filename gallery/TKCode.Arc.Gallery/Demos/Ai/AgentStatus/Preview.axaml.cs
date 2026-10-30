using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentStatusPreviewDemo : UserControl
{
    public AgentStatusPreviewDemo()
    {
        InitializeComponent();
        // The demo owns the run: stop its timer when the page goes away.
        DetachedFromVisualTree += (_, _) => Pill.Simulation?.Dispose();
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The event also fires while the XAML loads, before the named field exists: read the sender.
        var research = sender is SegmentedControl { SelectedValue: "research" };
        if (Pill?.Simulation is { } simulation)
        {
            simulation.Scenario = research ? AgentStatusDemoData.Research : AgentStatusDemoData.Coding;
        }
    }

    private void OnReplay(object? sender, RoutedEventArgs e) => Pill.Simulation?.Replay();
}
