using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatMapCabinDemo : UserControl
{
    public SeatMapCabinDemo()
    {
        InitializeComponent();
        SeatMapSamples.ApplyCabin(Map);
    }
}
