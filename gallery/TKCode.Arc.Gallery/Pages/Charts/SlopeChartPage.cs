using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class SlopeChartPage
{
    private static readonly SlopeItem[] Conversion =
    [
        new("search", "Organic search", 3.1, 4.6), new("email", "Email", 5.8, 5.2), new("paid", "Paid social", 2.2, 3.4),
        new("referral", "Referral", 4.4, 4.1), new("direct", "Direct", 3.6, 3.7), new("display", "Display ads", 1.2, 0.9)
    ];

    private static readonly SlopeItem[] Retention =
    [
        new("search", "Organic search", 41, 48), new("email", "Email", 62, 57), new("paid", "Paid social", 23, 31),
        new("referral", "Referral", 55, 60), new("direct", "Direct", 47, 44), new("display", "Display ads", 18, 21)
    ];

    public static Control Create()
    {
        var chart = new SlopeChart
        {
            Label = "Conversion rate by channel",
            StartLabel = "Q1",
            EndLabel = "Q2",
            ItemsSource = Conversion,
            HighlightKey = "search",
            ValueFormatter = v => $"{v:0.0}%",
            ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} pts"
        };
        var conversion = new Button { Content = "Conversion", Variant = ButtonVariant.Secondary };
        var retention = new Button { Content = "Retention", Variant = ButtonVariant.Secondary };
        conversion.Click += (_, _) =>
        {
            chart.Label = "Conversion rate by channel";
            chart.ItemsSource = Conversion;
        };
        retention.Click += (_, _) =>
        {
            chart.Label = "90-day retention by channel";
            chart.ItemsSource = Retention;
        };
        var reading = GalleryUi.Text("Hover a line or focus the chart and use the arrow keys.", TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted, 13);
        chart.ActiveKeyChanged += (_, e) => reading.Text = e.Item is { } item ? $"{item.Label}: {item.Start} → {item.End}" : "No item in focus";

        var crowded = new SlopeChart { Label = "Close values", StartLabel = "2023", EndLabel = "2024", IsRankVisible = false, MaxWidth = 420, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        crowded.Items.Add(new SlopeItem("a", "Alpha", 10, 12));
        crowded.Items.Add(new SlopeItem("b", "Beta", 10.2, 12.1));
        crowded.Items.Add(new SlopeItem("c", "Gamma", 10.4, 8));
        crowded.Items.Add(new SlopeItem("d", "Delta with a long name", 10.5, 12.2));

        return GalleryUi.Column(20,
            GalleryUi.Card("Before and after", "Lines draw in on first view; switching datasets slides every line, dot and label to its new slope.",
                GalleryUi.Column(16, chart, GalleryUi.Row(conversion, retention), reading)),
            GalleryUi.Card("Label collisions", "Labels that would overlap push apart and connect back to their dots with thin leaders.", crowded));
    }
}
