using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class MetricCardPage
{
    public static Control Create()
    {
        var uptime = new MetricCard { Label = "Uptime", Value = 99.98, Decimals = 2, Suffix = "%", Context = "Last 30 days", Change = "+0.02%" };
        var orders = new MetricCard
        {
            Label = "Orders",
            Value = 1204,
            Context = "vs last week",
            Change = "+12%",
            Sparkline = new List<double> { 820, 910, 870, 990, 1040, 1010, 1204 }
        };
        var latency = new MetricCard
        {
            Label = "p95 latency",
            Value = 182,
            Suffix = " ms",
            Context = "vs yesterday",
            Change = "−8%",
            IsChangeGlyphVisible = true
        };
        foreach (var card in new[] { uptime, orders, latency })
        {
            card.Width = 280;
            card.VerticalAlignment = VerticalAlignment.Top;
        }

        var random = new Random(3);
        var update = new Button { Content = "New data", Variant = ButtonVariant.Secondary };
        update.Click += (_, _) =>
        {
            var next = Math.Max(200, orders.Value + random.Next(-260, 300));
            var delta = (next - orders.Value) / orders.Value * 100;
            orders.Value = next;
            orders.Change = delta >= 0 ? $"+{delta:0}%" : $"−{-delta:0}%";
            orders.Context = delta >= 0 ? "Up on last week" : "Down on last week";
            var points = new List<double>(orders.Sparkline!) { next };
            points.RemoveAt(0);
            orders.Sparkline = points;
            latency.Value = Math.Max(80, latency.Value + random.Next(-40, 40));
            latency.Change = latency.Value < 182 ? $"−{(182 - latency.Value) / 182 * 100:0}%" : $"+{(latency.Value - 182) / 182 * 100:0}%";
        };
        var clear = new Button { Content = "Toggle chip", Variant = ButtonVariant.Ghost };
        clear.Click += (_, _) => uptime.Change = uptime.Change is null ? "+0.02%" : null;

        return GalleryUi.Column(20,
            GalleryUi.Card("Metrics", "The number rolls from zero on first view; label, context and chip roll in from the side the number moved toward.",
                GalleryUi.Column(16, GalleryUi.Row(uptime, orders, latency), GalleryUi.Row(update, clear))),
            GalleryUi.Card("Plain number", "Without a change the chip is gone; windows 380px wide or narrower tighten the padding and wrap the chip under the label.",
                GalleryUi.Row(new MetricCard { Width = 280, Label = "Active projects", Value = 48, Context = "Across 6 teams" })));
    }
}
