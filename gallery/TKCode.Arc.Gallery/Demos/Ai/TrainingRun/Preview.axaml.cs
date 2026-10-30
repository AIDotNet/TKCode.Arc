using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated run, healthy or failing once with out of memory.</summary>
public partial class TrainingRunPreviewDemo : UserControl
{
    private readonly bool _ready;

    public TrainingRunPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Run.Scenario = Scenario.SelectedValue as string == "oom" ? TrainingRunScenario.Oom : TrainingRunScenario.Healthy;
        }
    }
}
