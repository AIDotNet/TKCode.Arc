using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the simulated sample flow playing, a failing step with Retry, the empty state and loading.</summary>
public partial class AiWorkflowPreviewDemo : UserControl
{
    private readonly bool _ready;

    public AiWorkflowPreviewDemo()
    {
        InitializeComponent();
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Host.Child = Create(Scenario.SelectedValue as string ?? "sample");
        }
    }

    private static AiWorkflow Create(string scenario)
    {
        switch (scenario)
        {
            case "failing":
            {
                // The sample flow, but creating the issue fails on the first try; Retry resumes the run from it.
                var steps = AiWorkflowDemoData.Steps;
                steps[2].Paths[0].Steps[1].Error = "The tracker token has expired. Reconnect it, then retry.";
                var flow = new AiWorkflow
                {
                    Title = AiWorkflowDemoData.Title,
                    Description = AiWorkflowDemoData.Description,
                    Steps = steps,
                    Catalog = AiWorkflowDemoData.Catalog,
                    AutoPlay = false
                };
                flow.AttachedToVisualTree += (_, _) => flow.StartRun();
                return flow;
            }
            case "empty":
                return new AiWorkflow
                {
                    Title = "New workflow", Description = "Pick a trigger to start", Steps = [], Catalog = AiWorkflowDemoData.Catalog,
                    AutoPlay = false
                };
            case "loading":
                return new AiWorkflow { Title = AiWorkflowDemoData.Title, Description = "Loading the flow", IsLoading = true, AutoPlay = false };
            default:
                return new AiWorkflow { Simulation = AiWorkflowDemoData.Simulation() };
        }
    }
}
