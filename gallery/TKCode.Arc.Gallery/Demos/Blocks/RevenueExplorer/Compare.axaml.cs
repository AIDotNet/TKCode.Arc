using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueExplorerCompareDemo : UserControl
{
    public RevenueExplorerCompareDemo()
    {
        InitializeComponent();
        Revenue.OpeningMrr = RevenueExplorerDemoData.Opening;
        Revenue.Months.AddRange(RevenueExplorerDemoData.Standard());
    }
}
