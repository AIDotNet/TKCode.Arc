using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricExplorerFinanceDemo : UserControl
{
    public MetricExplorerFinanceDemo()
    {
        InitializeComponent();
        foreach (var metric in MetricExplorerDemoData.Finance())
        {
            metric.EndDate = MetricExplorerDemoData.Today;
            Explorer.Metrics.Add(metric);
        }
    }
}
