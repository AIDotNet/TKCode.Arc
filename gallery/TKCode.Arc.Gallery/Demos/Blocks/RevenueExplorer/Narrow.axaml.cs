using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueExplorerNarrowDemo : UserControl
{
    public RevenueExplorerNarrowDemo()
    {
        InitializeComponent();
        Revenue.OpeningMrr = RevenueExplorerDemoData.Opening;
        Revenue.Months.AddRange(RevenueExplorerDemoData.Standard());
    }
}
