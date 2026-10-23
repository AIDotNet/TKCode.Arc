using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueGlobeNarrowDemo : UserControl
{
    public RevenueGlobeNarrowDemo()
    {
        InitializeComponent();
        RevenueGlobeSampleDay.Apply(Globe);
    }
}
