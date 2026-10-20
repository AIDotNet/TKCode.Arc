using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class SparklinePage
{
    public static Control Create()
    {
        var days = ChartsADemoData.Days(14);
        var labels = days.Select(d => d.ToString("MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US"))).ToArray();
        Sparkline Card(string label, string value, string change, ChartTone tone, double[] data) => new()
        {
            Label = label,
            Value = value,
            Change = change,
            Tone = tone,
            ItemsSource = data,
            PointLabels = labels,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var revenue = Card("Revenue", "$48.2K", "+4.1%", ChartTone.Success, ChartsADemoData.Daily(14, 40, 14, 1, 0.9));
        var churn = Card("Churn", "2.4%", "+0.3 pts", ChartTone.Danger, ChartsADemoData.Daily(14, 20, 8, 2, 0.4));
        var latency = Card("Latency", "182 ms", "−6 ms", ChartTone.Accent, ChartsADemoData.Daily(14, 190, 30, 3, -0.8));
        var queue = Card("Queue depth", "1,204", "Rising", ChartTone.Warning, ChartsADemoData.Daily(14, 900, 300, 4, 25));

        var plain = new Sparkline
        {
            Label = "Visits (scrub for the value)",
            ItemsSource = ChartsADemoData.Daily(30, 300, 120, 7, 3),
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var randomize = new Button { Content = "New data", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        var seed = 10;
        randomize.Click += (_, _) => plain.ItemsSource = ChartsADemoData.Daily(30, 300, 120, seed++, 3);

        var table = new StackPanel { Spacing = 8 };
        foreach (var (name, data) in new[] { ("api-gateway", 11), ("billing", 12), ("search", 13) })
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*") };
            row.Children.Add(GalleryUi.Text(name));
            var cell = new Sparkline { Label = name, ItemsSource = ChartsADemoData.Daily(20, 50, 30, data), ShowArea = false, IsInteractive = false, Width = 160, DrawingHeight = 32 };
            Grid.SetColumn(cell, 1);
            row.Children.Add(cell);
            table.Children.Add(row);
        }

        return GalleryUi.Column(20,
            GalleryUi.Card("KPI cards", "Hover or focus and use the arrow keys: the cursor springs between points and the later stretch dims; the headline and aside roll.",
                GalleryUi.Row(revenue, churn, latency, queue)),
            GalleryUi.Card("Without a headline", "With no Value, the scrubbed value and its point read beside the label. New data morphs the line.",
                GalleryUi.Column(12, randomize, plain)),
            GalleryUi.Card("Dense table", "IsInteractive=False drops the tab stop and pointer handling; ShowArea=False keeps it quiet.", table));
    }
}
