using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class BrushChartPage
{
    public static Control Create()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var random = new Random(42);
        var points = new ChartPoint[640];
        double level = 1200;
        for (var i = 0; i < points.Length; i++)
        {
            level = Math.Max(200, level + (random.NextDouble() - 0.46) * 60);
            var weekly = (i % 7) is 5 or 6 ? -260 : 0;
            var spike = i == 410 ? 1800 : i == 411 ? 900 : 0;
            points[i] = new ChartPoint(start.AddDays(i), Math.Round(level + weekly + spike + random.NextDouble() * 120));
        }
        var chart = new BrushChart
        {
            Label = "Daily active users",
            Unit = "users",
            ItemsSource = points,
            Annotations =
            [
                new ChartAnnotation(start.AddDays(120), "v2 launch", "New onboarding flow shipped to everyone."),
                new ChartAnnotation(start.AddDays(410), "Press mention", "Featured in a weekly newsletter."),
                new ChartAnnotation(start.AddDays(530), "Pricing change")
            ]
        };
        var range = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary);
        chart.RangeChanged += (_, e) => range.Text = $"Window: {e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
        var last = points[^1].X is DateTimeOffset end ? end : start;
        var buttons = GalleryUi.Row(
            Preset("Last 30 days", () => chart.Range = new ChartTimeRange(last.AddDays(-30), last)),
            Preset("Last quarter", () => chart.Range = new ChartTimeRange(last.AddDays(-91), last)),
            Preset("Last year", () => chart.Range = new ChartTimeRange(last.AddDays(-365), last)),
            Preset("All", () => chart.Range = new ChartTimeRange(start, last)));
        return GalleryUi.Column(20,
            GalleryUi.Card("Overview and detail",
                "Drag the window to pan, drag a grip to resize, drag across empty track for a new window; double-click resets. Zoomed out, a seven-day average and its band replace the raw line. Tab to the window and its grips to move them with the keyboard.",
                GalleryUi.Column(16, buttons, chart, range)));
    }

    private static Button Preset(string text, Action action)
    {
        var button = new Button { Content = text, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        button.Click += (_, _) => action();
        return button;
    }
}
