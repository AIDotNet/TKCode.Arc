using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricsDashboardPreviewDemo : UserControl
{
    public MetricsDashboardPreviewDemo()
    {
        InitializeComponent();
        Dashboard.EndDate = MetricsDashboardDemoData.EndDate;
        Dashboard.Metrics.AddRange(MetricsDashboardDemoData.SiteMetrics());
        Dashboard.PrimaryItems.AddRange(MetricsDashboardDemoData.TopPaths());
        Dashboard.SecondaryItems.AddRange(MetricsDashboardDemoData.Referrers());
    }
}
