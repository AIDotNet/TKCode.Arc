using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulation, a queued job, a failed one and the empty canvas.</summary>
public partial class ImageRevealPreviewDemo : UserControl
{
    private readonly ImageRevealSimulation? _simulation;
    private readonly bool _ready;

    public ImageRevealPreviewDemo()
    {
        InitializeComponent();
        _simulation = Reveal.Simulation;
        Reveal.RetryRequested += OnRetry;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has run.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "simulated");
        }
    }

    private void Show(string scenario)
    {
        switch (scenario)
        {
            case "queued":
                Reveal.QueueLabel = "Queued, 2 ahead";
                Reveal.Status = ImageRevealStatus.Queued;
                break;
            case "failed":
                Reveal.ErrorMessage = "The image could not be generated. Try again in a moment.";
                Reveal.Status = ImageRevealStatus.Error;
                break;
            case "empty":
                // No status, no image and no simulation: the plain surface.
                Reveal.QueueLabel = null;
                Reveal.Simulation = null;
                Reveal.Status = null;
                break;
            default:
                // No status: the simulation runs again.
                Reveal.QueueLabel = null;
                Reveal.Simulation = _simulation;
                Reveal.Status = null;
                break;
        }
    }

    private void OnRetry(object? sender, RoutedEventArgs e) => Scenario.SelectedValue = "simulated";
}
