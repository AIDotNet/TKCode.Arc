using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricExplorerRangesDemo : UserControl
{
    public MetricExplorerRangesDemo()
    {
        InitializeComponent();
        Explorer.Ranges.Add(new ExplorerRange("7D", 7, "7 days", "vs 7 days ago"));
        Explorer.Ranges.Add(new ExplorerRange("60D", 60, "60 days", "vs 60 days ago"));
        Explorer.Ranges.Add(new ExplorerRange("6M", 182, "6 months", "vs 6 months ago", 7));
        foreach (var metric in MetricExplorerDemoData.Workspace().Take(3))
        {
            metric.EndDate = MetricExplorerDemoData.Today;
            Explorer.Metrics.Add(metric);
        }
    }
}
