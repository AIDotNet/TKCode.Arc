using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class DonutChartPage
{
    private static readonly ChartPoint[] s_month =
    [
        new("Search", 4820) { Key = "search" },
        new("Direct", 2960) { Key = "direct" },
        new("Social", 1840) { Key = "social" },
        new("Referral", 1210) { Key = "referral" },
        new("Email", 260) { Key = "email" },
        new("Podcasts", 140) { Key = "podcasts" }
    ];

    private static readonly ChartPoint[] s_quarter =
    [
        new("Search", 13900) { Key = "search" },
        new("Direct", 9800) { Key = "direct" },
        new("Social", 7200) { Key = "social" },
        new("Referral", 2600) { Key = "referral" },
        new("Email", 1900) { Key = "email" },
        new("Podcasts", 420) { Key = "podcasts" }
    ];

    public static Control Create()
    {
        var chart = new DonutChart { Label = "Visits by source", Unit = "visits", ItemsSource = s_month };
        var month = new Button { Content = "This month", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        month.Click += (_, _) => chart.ItemsSource = s_month;
        var quarter = new Button { Content = "This quarter", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        quarter.Click += (_, _) => chart.ItemsSource = s_quarter;

        var selected = GalleryUi.Text("Click a row or a segment to pin it.", ArcDesignTokenKind.TextSecondary);
        var select = new DonutChart
        {
            Label = "Spend by category",
            LegendAction = DonutLegendAction.Select,
            ValueFormat = "$#,##0",
            ItemsSource = new[]
            {
                new ChartPoint("Payroll", 64200), new ChartPoint("Cloud", 18400), new ChartPoint("Software", 9200),
                new ChartPoint("Travel", 4100), new ChartPoint("Office", 2300)
            }
        };
        select.ActiveKeyChanged += (_, _) => selected.Text = select.ActiveKey is { } key ? $"Pinned: {key}" : "Click a row or a segment to pin it.";

        var ringOnly = new DonutChart
        {
            Label = "Storage",
            ShowLegend = false,
            Diameter = 160,
            RingThickness = 18,
            ItemsSource = new[] { new ChartPoint("Photos", 41), new ChartPoint("Video", 27), new ChartPoint("Documents", 12), new ChartPoint("Free", 20) },
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Toggle segments", "Press a legend row to hide its segment; the ring redistributes and the total counts. Small parts fold into Other. Switch datasets to morph the arcs.",
                GalleryUi.Column(16, GalleryUi.Row(month, quarter), chart)),
            GalleryUi.Card("Select", "LegendAction=Select pins a segment (ActiveKey, two-way).", GalleryUi.Column(12, select, selected)),
            GalleryUi.Card("Ring only", "Without a legend the ring is focusable: arrow keys read each segment, Enter pins it.", ringOnly));
    }
}
