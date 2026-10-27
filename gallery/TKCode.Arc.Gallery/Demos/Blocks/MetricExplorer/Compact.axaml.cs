using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricExplorerCompactDemo : UserControl
{
    public MetricExplorerCompactDemo()
    {
        InitializeComponent();
        foreach (var metric in MetricExplorerDemoData.Workspace())
        {
            metric.EndDate = MetricExplorerDemoData.Today;
            Explorer.Metrics.Add(metric);
        }
    }
}
