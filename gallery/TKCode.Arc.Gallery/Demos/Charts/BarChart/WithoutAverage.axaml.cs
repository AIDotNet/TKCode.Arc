using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BarChartWithoutAverageDemo : UserControl
{
    public BarChartWithoutAverageDemo()
    {
        InitializeComponent();

        // Categories as strings.
        Chart.ItemsSource = Enumerable.Range(0, 12)
            .Select(i => new ChartPoint($"W{i + 27}", 4 + (i * 7 % 9)))
            .ToArray();
    }
}
