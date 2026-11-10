using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PrizeWheelPreviewDemo : UserControl
{
    public PrizeWheelPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnSpinCompleted(object? sender, PrizeWheelResultEventArgs e) =>
        Readout.Text = $"You won: {e.Segment.Label}";
}
