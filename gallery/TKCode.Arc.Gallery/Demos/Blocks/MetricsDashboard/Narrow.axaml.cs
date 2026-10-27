using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricsDashboardNarrowDemo : UserControl
{
    public MetricsDashboardNarrowDemo()
    {
        InitializeComponent();
        Dashboard.EndDate = MetricsDashboardDemoData.EndDate;
        Dashboard.Metrics.AddRange(MetricsDashboardDemoData.SiteMetrics());
        Dashboard.PrimaryItems.AddRange(MetricsDashboardDemoData.TopPaths());
        Dashboard.SecondaryItems.AddRange(MetricsDashboardDemoData.Referrers());
    }
}
