using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: a simulated search in its three scenarios.</summary>
public partial class SourceCardsPreviewDemo : UserControl
{
    private readonly bool _ready;

    public SourceCardsPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (!_ready || Cards.Simulation is not { } simulation)
        {
            return;
        }
        // A new scenario starts the story over.
        simulation.Scenario = (Scenario.SelectedValue as string) switch
        {
            "error" => SourceCardsScenario.Error,
            "empty" => SourceCardsScenario.Empty,
            _ => SourceCardsScenario.Live
        };
    }
}
