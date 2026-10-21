using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DonutChartRingOnlyDemo : UserControl
{
    public DonutChartRingOnlyDemo()
    {
        InitializeComponent();
        Chart.ItemsSource = new[]
        {
            new ChartPoint("Photos", 41),
            new ChartPoint("Video", 27),
            new ChartPoint("Documents", 12),
            new ChartPoint("Free", 20)
        };
    }
}
