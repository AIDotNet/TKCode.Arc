using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DonutChartSelectDemo : UserControl
{
    public DonutChartSelectDemo()
    {
        InitializeComponent();
        Chart.ItemsSource = new[]
        {
            new ChartPoint("Payroll", 64200),
            new ChartPoint("Cloud", 18400),
            new ChartPoint("Software", 9200),
            new ChartPoint("Travel", 4100),
            new ChartPoint("Office", 2300)
        };
    }

    private void OnActiveKeyChanged(object? sender, RoutedEventArgs e)
        => Pinned.Text = Chart.ActiveKey is { } key ? $"Pinned: {key}" : "Click a row or a segment to pin it.";
}
