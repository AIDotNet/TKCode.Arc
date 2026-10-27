using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueExplorerLongHistoryDemo : UserControl
{
    public RevenueExplorerLongHistoryDemo()
    {
        InitializeComponent();
        Revenue.OpeningMrr = RevenueExplorerDemoData.Opening;
        Revenue.Months.AddRange(RevenueExplorerDemoData.LongHistory());
    }
}
