using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelTrainingOwnLossesDemo : UserControl
{
    private readonly List<string> _losses = new();

    public ModelTrainingOwnLossesDemo()
    {
        InitializeComponent();
        // A learning rate that is too large: the loss falls, overshoots and bounces instead of settling.
        Card.StepCount = 8;
        Card.LossValues = [2.31, 1.12, 1.64, 0.98, 1.41, 0.92, 1.27, 0.95];
    }

    private void OnIterationCompleted(object? sender, ModelTrainingIterationEventArgs e)
    {
        if (e.Iteration == 1)
        {
            _losses.Clear();
        }
        _losses.Add($"{e.Iteration}: {e.Loss:0.00}");
        Log.Text = string.Join("  ·  ", _losses);
    }
}
