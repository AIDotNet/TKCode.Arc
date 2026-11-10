using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatMapPreviewDemo : UserControl
{
    public SeatMapPreviewDemo()
    {
        InitializeComponent();
        SeatMapSamples.ApplyHall(Map);
    }
}
