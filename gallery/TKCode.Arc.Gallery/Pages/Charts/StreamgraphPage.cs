using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class StreamgraphPage
{
    private static readonly string[] s_topics = ["Billing", "Login", "Performance", "Integrations", "Mobile"];

    public static Control Create()
    {
        var chart = new Streamgraph { Label = "Support tickets by topic", Unit = "tickets", CategoryLabel = "Week" };
        var series = s_topics.Select((topic, i) => new ChartSeries { Key = topic.ToLowerInvariant(), Name = topic }).ToArray();
        foreach (var s in series)
        {
            chart.Series.Add(s);
        }
        void Range(int weeks)
        {
            var start = ChartsADemoData.Today.AddDays(-7 * (weeks - 1));
            chart.ItemsSource = Enumerable.Range(0, weeks).Select(w => start.AddDays(7 * w)).ToArray();
            for (var i = 0; i < series.Length; i++)
            {
                series[i].Values = ChartsADemoData.Daily(weeks, 60 - i * 9, 40, weeks * 7 + i, i % 2 == 0 ? 0.8 : -0.3);
            }
        }
        Range(26);
        var buttons = GalleryUi.Row(
            Button("12 weeks", () => Range(12)),
            Button("26 weeks", () => Range(26)),
            Button("52 weeks", () => Range(52)),
            Button("Wiggle", () => chart.Offset = StreamOffset.Wiggle),
            Button("Silhouette", () => chart.Offset = StreamOffset.Silhouette),
            Button("Zero", () => chart.Offset = StreamOffset.Zero));
        return GalleryUi.Column(20,
            GalleryUi.Card("Topics over time", "Hover a layer to isolate it and read every layer at that week; up and down arrows change the layer. Switch ranges: every layer blends from the shape on screen.",
                GalleryUi.Column(16, buttons, chart)));
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        button.Click += (_, _) => action();
        return button;
    }
}
