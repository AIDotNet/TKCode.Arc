using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HorizonChartCompactDemo : UserControl
{
    public HorizonChartCompactDemo()
    {
        InitializeComponent();
        Chart.Days = HorizonChartDemoData.OrderDays();
        Chart.Series.AddRange(HorizonChartDemoData.OrderSeries());
    }
}
