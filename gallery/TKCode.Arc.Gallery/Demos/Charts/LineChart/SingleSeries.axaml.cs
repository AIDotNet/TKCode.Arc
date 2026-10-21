using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class LineChartSingleSeriesDemo : UserControl
{
    public LineChartSingleSeriesDemo()
    {
        InitializeComponent();

        // A list of ChartPoint needs no series.
        Chart.ItemsSource = ChartsADemoData.Days(14)
            .Select((day, i) => new ChartPoint(day, 120 + 40 * Math.Sin(i / 2.0) + i * 3))
            .ToArray();
    }
}
