using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HorizonChartLatencyDemo : UserControl
{
    public HorizonChartLatencyDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = value => Milliseconds(value);
        Chart.Days = HorizonChartDemoData.LatencyDays();
        Chart.Series.AddRange(HorizonChartDemoData.LatencySeries());
    }

    private static string Milliseconds(double value)
    {
        var rounded = (int)System.Math.Round(value);
        return rounded switch
        {
            > 0 => $"+{rounded} ms",
            < 0 => $"−{-rounded} ms",
            _ => "0 ms"
        };
    }
}
