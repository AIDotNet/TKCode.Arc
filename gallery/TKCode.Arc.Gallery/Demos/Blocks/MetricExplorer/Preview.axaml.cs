using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricExplorerPreviewDemo : UserControl
{
    public MetricExplorerPreviewDemo()
    {
        InitializeComponent();
        foreach (var metric in MetricExplorerDemoData.Workspace())
        {
            metric.EndDate = MetricExplorerDemoData.Today;
            Explorer.Metrics.Add(metric);
        }
    }
}
