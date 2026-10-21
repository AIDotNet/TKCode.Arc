using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class BarChartWeekAndMonthDemo : UserControl
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    public BarChartWeekAndMonthDemo()
    {
        InitializeComponent();
        ShowWeek();
    }

    private void OnWeekClick(object? sender, RoutedEventArgs e) => ShowWeek();

    private void OnMonthClick(object? sender, RoutedEventArgs e) => ShowMonth();

    // Bars with the same key morph to their new height; new bars grow in beside them.
    private void ShowWeek()
    {
        var days = ChartsADemoData.Days(7);
        var values = ChartsADemoData.Daily(7, 46, 30, 3);
        Chart.Period = "Sep 24–30, 2026";
        Chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])
        {
            Label = day.ToString("dddd, MMM d", s_culture),
            AxisLabel = day.ToString("ddd", s_culture)
        }).ToArray();
    }

    private void ShowMonth()
    {
        var days = ChartsADemoData.Days(30);
        var values = ChartsADemoData.Daily(30, 40, 34, 9);
        Chart.Period = "September 2026";
        Chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])).ToArray();
    }
}
