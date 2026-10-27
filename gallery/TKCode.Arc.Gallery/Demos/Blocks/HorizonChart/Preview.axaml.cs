using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HorizonChartPreviewDemo : UserControl
{
    public HorizonChartPreviewDemo()
    {
        InitializeComponent();
        Chart.Days = HorizonChartDemoData.OrderDays();
        Chart.Series.AddRange(HorizonChartDemoData.OrderSeries());
    }
}
