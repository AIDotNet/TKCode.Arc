using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatManagerNarrowDemo : UserControl
{
    public SeatManagerNarrowDemo()
    {
        InitializeComponent();
        SeatManagerSamples.Apply(Card);
        Card.Target = 14;
    }
}
