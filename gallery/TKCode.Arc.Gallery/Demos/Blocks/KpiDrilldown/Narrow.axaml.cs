using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KpiDrilldownNarrowDemo : UserControl
{
    public KpiDrilldownNarrowDemo()
    {
        InitializeComponent();
        Block.EndDate = KpiDrilldownDemoData.End;
        Block.Dimensions.AddRange(KpiDrilldownDemoData.Dimensions());
        Block.Metrics.AddRange(KpiDrilldownDemoData.Metrics(3));
    }
}
