using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KpiDrilldownPreviewDemo : UserControl
{
    public KpiDrilldownPreviewDemo()
    {
        InitializeComponent();
        Block.EndDate = KpiDrilldownDemoData.End;
        Block.Dimensions.AddRange(KpiDrilldownDemoData.Dimensions());
        Block.Metrics.AddRange(KpiDrilldownDemoData.Metrics());
    }
}
