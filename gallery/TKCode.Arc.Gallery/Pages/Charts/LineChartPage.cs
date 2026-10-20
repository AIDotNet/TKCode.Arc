using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class LineChartPage
{
    public static Control Create()
    {
        // Ranges: switching morphs every line from the shape on screen.
        var chart = new LineChart { Label = "Signups", CategoryLabel = "Date" };
        var current = new ChartSeries { Key = "current", Name = "This period" };
        var previous = new ChartSeries { Key = "previous", Name = "Previous period", IsDashed = true };
        chart.Series.Add(current);
        chart.Series.Add(previous);
        void Range(int days)
        {
            chart.ItemsSource = ChartsADemoData.Days(days);
            current.Values = ChartsADemoData.Daily(days, 420, 160, days, 2.4);
            previous.Values = ChartsADemoData.Daily(days, 380, 140, days + 11, 1.1);
        }
        Range(30);
        var readout = GalleryUi.Text("Hover or use the arrow keys to read values.", ArcDesignTokenKind.TextSecondary);
        chart.ActiveIndexChanged += (_, e) => readout.Text = e.Index is null
            ? "Hover or use the arrow keys to read values."
            : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
        var ranges = GalleryUi.Row(
            RangeButton("7 days", () => Range(7)),
            RangeButton("30 days", () => Range(30)),
            RangeButton("90 days", () => Range(90)),
            RangeButton("Toggle loading", () => chart.IsLoading = !chart.IsLoading));

        // Metrics: a single series from records, linear segments, a unit and a value format.
        var latency = new LineChart
        {
            Label = "p95 latency",
            Unit = "ms",
            Curve = ChartCurve.Linear,
            PlotHeight = 180,
            ItemsSource = ChartsADemoData.Days(14).Select((day, i) => new ChartPoint(day, 120 + 40 * Math.Sin(i / 2.0) + i * 3)).ToArray()
        };

        var empty = new LineChart { Label = "Refunds", EmptyLabel = "No refunds in this range", PlotHeight = 160 };
        empty.Series.Add(new ChartSeries { Name = "Refunds", Values = Array.Empty<double>() });

        return GalleryUi.Column(20,
            GalleryUi.Card("Ranges", "Switch the range: every line morphs from the shape on screen and the value axis springs to fit. Toggle a series in the legend; it flattens into the baseline.",
                GalleryUi.Column(16, ranges, chart, readout)),
            GalleryUi.Card("Single series", "A record list (ChartPoint) with straight segments and a unit in the tooltip.", latency),
            GalleryUi.Card("Empty", "No points: the plot says why.", empty));
    }

    private static Button RangeButton(string text, Action action)
    {
        var button = new Button { Content = text, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        button.Click += (_, _) => action();
        return button;
    }
}
