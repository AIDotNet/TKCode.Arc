using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentStatusPreviewDemo : UserControl
{
    private readonly AgentRunSimulation _simulation = new();

    public AgentStatusPreviewDemo()
    {
        InitializeComponent();
        Pill.Simulation = _simulation;
        // The demo owns the run: stop its timer when the page goes away.
        DetachedFromVisualTree += (_, _) => _simulation.Dispose();
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The event also fires while the XAML loads, before the named field exists: read the sender.
        var research = sender is SegmentedControl { SelectedValue: "research" };
        _simulation.Scenario = research ? AgentStatusSamples.Research : AgentStatusSamples.Coding;
    }

    private void OnReplay(object? sender, RoutedEventArgs e) => _simulation.Replay();
}
