using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The default demo: the block live (a simulation), finished and interrupted (the app's own data), and empty.</summary>
public partial class ReasoningBlockPreviewDemo : UserControl
{
    private readonly ReasoningBlockSimulation _simulation;
    private readonly bool _ready;

    public ReasoningBlockPreviewDemo()
    {
        InitializeComponent();
        // Open the reasoning once the simulated model is done, as an app might when the answer starts.
        _simulation = Block.Simulation!;
        _simulation.Finished += (_, _) => Block.IsExpanded = true;
        Block.RetryRequested += OnRetryRequested;
        _ready = true;
    }

    private void OnScenarioChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The control applies SelectedValue while the XAML loads, before the constructor has finished.
        if (_ready)
        {
            Show(Scenario.SelectedValue as string ?? "live");
        }
    }

    private void Show(string scenario)
    {
        switch (scenario)
        {
            case "finished":
                Block.ErrorText = null;
                Block.Elapsed = ReasoningBlockDemoData.FinishedElapsed;
                Block.Reasoning = ReasoningBlockDemoData.Reasoning;
                Block.Status = ReasoningStatus.Done;
                Block.IsExpanded = true;
                break;
            case "interrupted":
                Block.Elapsed = ReasoningBlockDemoData.InterruptedElapsed;
                Block.Reasoning = ReasoningBlockDemoData.Interrupted;
                Block.ErrorText = ReasoningBlockDemoData.InterruptedReason;
                Block.Status = ReasoningStatus.Error;
                Block.IsExpanded = true;
                break;
            default:
                // With no data of its own the block takes its simulation up again and starts it over; without a
                // simulation it reads "Thought process" with nothing to open.
                Block.IsExpanded = false;
                Block.ClearValue(ReasoningBlock.ErrorTextProperty);
                Block.ClearValue(ReasoningBlock.ElapsedProperty);
                Block.ClearValue(ReasoningBlock.StatusProperty);
                Block.ClearValue(ReasoningBlock.ReasoningProperty);
                Block.Simulation = scenario == "empty" ? null : _simulation;
                break;
        }
    }

    private void OnRetryRequested(object? sender, RoutedEventArgs e)
    {
        Scenario.SelectedValue = "live";
        Show("live");
    }
}
