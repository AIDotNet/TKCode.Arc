using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class LineChartRangesDemo : UserControl
{
    public LineChartRangesDemo()
    {
        InitializeComponent();
        ShowDays(30);
    }

    private void On7DaysClick(object? sender, RoutedEventArgs e) => ShowDays(7);

    private void On30DaysClick(object? sender, RoutedEventArgs e) => ShowDays(30);

    private void On90DaysClick(object? sender, RoutedEventArgs e) => ShowDays(90);

    private void OnToggleLoadingClick(object? sender, RoutedEventArgs e) => Chart.IsLoading = !Chart.IsLoading;

    // Replace the x positions and the values of both series together; every line morphs from the shape on screen.
    private void ShowDays(int days)
    {
        Chart.ItemsSource = ChartsADemoData.Days(days);
        Chart.Series[0].Values = ChartsADemoData.Daily(days, 420, 160, days, 2.4);
        Chart.Series[1].Values = ChartsADemoData.Daily(days, 380, 140, days + 11, 1.1);
    }

    private void OnActiveIndexChanged(object? sender, ChartActiveIndexChangedEventArgs e)
        => Readout.Text = e.Index is null
            ? "Hover or use the arrow keys to read values."
            : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
}
