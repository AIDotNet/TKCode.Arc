using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DonutChartToggleSegmentsDemo : UserControl
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

    public DonutChartToggleSegmentsDemo()
    {
        InitializeComponent();
        Chart.ItemsSource = s_month;
    }

    // Segments keep their key, so the arcs move from their current angles to the new ones.
    private void OnMonthClick(object? sender, RoutedEventArgs e) => Chart.ItemsSource = s_month;

    private void OnQuarterClick(object? sender, RoutedEventArgs e) => Chart.ItemsSource = s_quarter;
}
