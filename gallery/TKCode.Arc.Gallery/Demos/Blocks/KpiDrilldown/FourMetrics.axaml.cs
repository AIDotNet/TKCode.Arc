using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KpiDrilldownFourMetricsDemo : UserControl
{
    public KpiDrilldownFourMetricsDemo()
    {
        InitializeComponent();
        Block.EndDate = KpiDrilldownDemoData.End;
        Block.Ranges = [14, 28, 56];
        Block.Metrics.AddRange(KpiDrilldownDemoData.Metrics(4, withBreakdowns: false));
    }
}
