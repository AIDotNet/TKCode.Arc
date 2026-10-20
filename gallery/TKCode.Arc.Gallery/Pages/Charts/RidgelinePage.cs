using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class RidgelinePage
{
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>Daily highs per month around a seasonal mean (deterministic, normal noise).</summary>
    private static RidgelineSeries[] Highs(double[] means, double spread, int seed)
    {
        var random = new Random(seed);
        double Normal() => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        return means.Select((mean, month) => new RidgelineSeries(Months[month].ToLowerInvariant(), Months[month],
            Enumerable.Range(0, 30).Select(_ => Math.Round(mean + Normal() * spread * (month is 0 or 11 ? 1.3 : 1), 1)).ToArray())).ToArray();
    }

    private static readonly RidgelineSeries[] Zurich = Highs([3, 5, 10, 14, 19, 22, 25, 24, 20, 14, 8, 4], 3.2, 4);
    private static readonly RidgelineSeries[] Lisbon = Highs([15, 16, 19, 20, 22, 26, 28, 29, 27, 23, 18, 15], 2.4, 9);

    public static Control Create()
    {
        var chart = new Ridgeline { Label = "Daily highs in Zurich", Unit = "°C", ItemsSource = Zurich, DomainMinimum = -5, DomainMaximum = 35 };
        var zurich = new Button { Content = "Zurich", Variant = ButtonVariant.Secondary };
        var lisbon = new Button { Content = "Lisbon", Variant = ButtonVariant.Secondary };
        zurich.Click += (_, _) =>
        {
            chart.Label = "Daily highs in Zurich";
            chart.ItemsSource = Zurich;
        };
        lisbon.Click += (_, _) =>
        {
            chart.Label = "Daily highs in Lisbon";
            chart.ItemsSource = Lisbon;
        };
        var latency = new Ridgeline
        {
            Label = "Response time by region",
            ValueFormatter = v => $"{v:0} ms",
            IsTinted = false,
            RowHeight = 36,
            Overlap = 1.8
        };
        var random = new Random(2);
        foreach (var (id, label, center, tail) in new[] { ("eu", "Europe", 120.0, 30.0), ("us", "Americas", 160.0, 45.0), ("ap", "Asia Pacific", 210.0, 70.0) })
        {
            latency.Items.Add(new RidgelineSeries(id, label, Enumerable.Range(0, 80)
                .Select(i => center + (random.NextDouble() - 0.5) * tail + (i % 9 == 0 ? tail * 1.6 : 0)).ToArray()));
        }
        return GalleryUi.Column(20,
            GalleryUi.Card("Distributions by month", "Hover lifts a ridge with its middle half and median; switching cities morphs every ridge on one spring.",
                GalleryUi.Column(16, chart, GalleryUi.Row(zurich, lisbon))),
            GalleryUi.Card("Untinted, taller rows", "IsTinted = false draws every ridge in Series1; a bimodal tail shows as a second hump.", latency));
    }
}
