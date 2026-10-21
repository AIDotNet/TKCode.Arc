using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class BrushChartOverviewAndDetailDemo : UserControl
{
    private static readonly DateTimeOffset s_start = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly DateTimeOffset _last;

    public BrushChartOverviewAndDetailDemo()
    {
        InitializeComponent();

        // 640 days with a weekend dip, a slow drift and one spike (deterministic).
        var random = new Random(42);
        var points = new ChartPoint[640];
        double level = 1200;
        for (var i = 0; i < points.Length; i++)
        {
            level = Math.Max(200, level + (random.NextDouble() - 0.46) * 60);
            var weekly = (i % 7) is 5 or 6 ? -260 : 0;
            var spike = i == 410 ? 1800 : i == 411 ? 900 : 0;
            points[i] = new ChartPoint(s_start.AddDays(i), Math.Round(level + weekly + spike + random.NextDouble() * 120));
        }
        _last = s_start.AddDays(points.Length - 1);
        Chart.ItemsSource = points;

        Chart.Annotations =
        [
            new ChartAnnotation(s_start.AddDays(120), "v2 launch", "New onboarding flow shipped to everyone."),
            new ChartAnnotation(s_start.AddDays(410), "Press mention", "Featured in a weekly newsletter."),
            new ChartAnnotation(s_start.AddDays(530), "Pricing change")
        ];
    }

    private void OnLast30DaysClick(object? sender, RoutedEventArgs e) => ShowLast(30);

    private void OnLastQuarterClick(object? sender, RoutedEventArgs e) => ShowLast(91);

    private void OnLastYearClick(object? sender, RoutedEventArgs e) => ShowLast(365);

    private void OnAllClick(object? sender, RoutedEventArgs e) => Chart.Range = new ChartTimeRange(s_start, _last);

    // The window slides to the new range on a spring.
    private void ShowLast(int days) => Chart.Range = new ChartTimeRange(_last.AddDays(-days), _last);

    private void OnRangeChanged(object? sender, ChartTimeRangeChangedEventArgs e)
        => RangeText.Text = $"Window: {e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
}
