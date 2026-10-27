using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricsDashboardProductUsageDemo : UserControl
{
    public MetricsDashboardProductUsageDemo()
    {
        InitializeComponent();
        Dashboard.Ranges.Clear();
        Dashboard.Ranges.AddRange([14, 28, 84]);
        Dashboard.EndDate = MetricsDashboardDemoData.EndDate;
        Dashboard.Metrics.AddRange(MetricsDashboardDemoData.UsageMetrics());
        Dashboard.PrimaryItems.AddRange(MetricsDashboardDemoData.Workspaces());
        Dashboard.SecondaryItems.AddRange(MetricsDashboardDemoData.Plans());
    }
}
