using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatManagerPreviewDemo : UserControl
{
    public SeatManagerPreviewDemo()
    {
        InitializeComponent();
        SeatManagerSamples.Apply(Card);
    }
}
