using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SlopeChartBeforeAndAfterDemo : UserControl
{
    private static readonly SlopeItem[] s_conversion =
    [
        new("search", "Organic search", 3.1, 4.6), new("email", "Email", 5.8, 5.2), new("paid", "Paid social", 2.2, 3.4),
        new("referral", "Referral", 4.4, 4.1), new("direct", "Direct", 3.6, 3.7), new("display", "Display ads", 1.2, 0.9)
    ];

    private static readonly SlopeItem[] s_retention =
    [
        new("search", "Organic search", 41, 48), new("email", "Email", 62, 57), new("paid", "Paid social", 23, 31),
        new("referral", "Referral", 55, 60), new("direct", "Direct", 47, 44), new("display", "Display ads", 18, 21)
    ];

    public SlopeChartBeforeAndAfterDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = v => $"{v:0.0}%";
        Chart.ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} pts";
        Chart.ItemsSource = s_conversion;
    }

    // Change the label in the same step as the data; every line, dot and label slides to its new slope.
    private void OnConversionClick(object? sender, RoutedEventArgs e)
    {
        Chart.Label = "Conversion rate by channel";
        Chart.ItemsSource = s_conversion;
    }

    private void OnRetentionClick(object? sender, RoutedEventArgs e)
    {
        Chart.Label = "90-day retention by channel";
        Chart.ItemsSource = s_retention;
    }

    private void OnActiveKeyChanged(object? sender, SlopeActiveKeyChangedEventArgs e)
        => Reading.Text = e.Item is { } item ? $"{item.Label}: {item.Start} → {item.End}" : "No item in focus";
}
