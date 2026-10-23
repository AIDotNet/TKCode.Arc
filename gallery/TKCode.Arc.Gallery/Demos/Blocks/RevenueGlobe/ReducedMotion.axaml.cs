using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueGlobeReducedMotionDemo : UserControl
{
    public RevenueGlobeReducedMotionDemo()
    {
        InitializeComponent();
        RevenueGlobeSampleDay.Apply(Globe);
    }
}
