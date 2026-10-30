using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated chat, handed off or with nobody available, and the empty card.</summary>
public partial class AgentHandoffPreviewDemo : UserControl
{
    private readonly AgentHandoffSimulation _simulation;
    private readonly bool _ready;

    public AgentHandoffPreviewDemo()
    {
        InitializeComponent();
        _simulation = Chat.Simulation!;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (!_ready)
        {
            return;
        }
        var picked = Scenario.SelectedValue as string;
        // A new scenario starts the story over; Empty takes the simulation away.
        _simulation.Scenario = picked == "unavailable" ? AgentHandoffScenario.Unavailable : AgentHandoffScenario.Handoff;
        Chat.Simulation = picked == "empty" ? null : _simulation;
    }
}
