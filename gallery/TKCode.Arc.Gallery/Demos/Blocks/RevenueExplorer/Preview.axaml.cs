using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueExplorerPreviewDemo : UserControl
{
    public RevenueExplorerPreviewDemo()
    {
        InitializeComponent();
        Revenue.OpeningMrr = RevenueExplorerDemoData.Opening;
        Revenue.Months.AddRange(RevenueExplorerDemoData.Standard());
    }
}
