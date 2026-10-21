using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataFlowEventPipelineDemo : UserControl
{
    public DataFlowEventPipelineDemo()
    {
        InitializeComponent();
    }

    private void OnStepChanged(object? sender, IllustrationStepChangedEventArgs e)
    {
        Status.Text = $"Step {e.Step + 1} of {Flow.StepCount}";
    }

    private void OnHalfSpeedClick(object? sender, RoutedEventArgs e)
    {
        Flow.Speed = 0.5;
    }

    private void OnNormalSpeedClick(object? sender, RoutedEventArgs e)
    {
        Flow.Speed = 1;
    }

    private void OnDoubleSpeedClick(object? sender, RoutedEventArgs e)
    {
        Flow.Speed = 2;
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        Flow.Restart();
    }
}
