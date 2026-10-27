using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricsDashboardEventsDemo : UserControl
{
    public MetricsDashboardEventsDemo()
    {
        InitializeComponent();
        Dashboard.EndDate = MetricsDashboardDemoData.EndDate;
        Dashboard.Metrics.AddRange(MetricsDashboardDemoData.SiteMetrics());
        Dashboard.PrimaryItems.AddRange(MetricsDashboardDemoData.TopPaths());
        Dashboard.SecondaryItems.AddRange(MetricsDashboardDemoData.Referrers());

        NextMetric.Click += (_, _) => Dashboard.SelectedMetricIndex = (Dashboard.SelectedMetricIndex + 1) % Dashboard.EffectiveMetrics.Count;
        LongestRange.Click += (_, _) => Dashboard.SelectedRange = Dashboard.Ranges.Max();
        LatestPoint.Click += (_, _) => Dashboard.InspectPoint(int.MaxValue);
        ClearPoint.Click += (_, _) => Dashboard.ClearInspection();

        Dashboard.RangeChanged += (_, e) => Log.Text = $"Range: {e.OldRange} → {e.NewRange} days";
        Dashboard.SelectedMetricChanged += (_, e) => Log.Text = $"Metric: {e.Metric?.Label}";
        Dashboard.CompareChanged += (_, e) => Log.Text = e.IsEnabled ? "Comparing with the previous period" : "Comparison off";
        Dashboard.PointInspected += (_, e) =>
            Log.Text = $"{e.Date:MMM d} ({e.Days} day{(e.Days == 1 ? "" : "s")}): {e.Value:N1}" +
                       (e.PreviousValue is { } previous ? $", previous {previous:N1}" : "");
    }
}
