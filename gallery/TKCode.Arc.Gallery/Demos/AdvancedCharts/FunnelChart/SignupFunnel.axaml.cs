using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FunnelChartSignupFunnelDemo : UserControl
{
    public FunnelChartSignupFunnelDemo()
    {
        InitializeComponent();
        // The band morphs to the chosen view; a segment key isolates that segment and rescales it to the full band.
        ViewPicker.SelectionChanged += (_, _) => Chart.View = ViewPicker.SelectedValue as string ?? "total";
    }
}
