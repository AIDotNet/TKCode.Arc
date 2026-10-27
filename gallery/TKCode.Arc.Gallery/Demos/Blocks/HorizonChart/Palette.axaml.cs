using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class HorizonChartPaletteDemo : UserControl
{
    public HorizonChartPaletteDemo()
    {
        InitializeComponent();
        Chart.AboveColor = Avalonia.Media.Color.Parse("#7C3AED");
        Chart.BelowColor = Avalonia.Media.Color.Parse("#0D9488");
        Chart.Days = HorizonChartDemoData.OrderDays();
        Chart.Series.AddRange(HorizonChartDemoData.OrderSeries());
        ByName.Click += (_, _) => Chart.Sort = HorizonSort.Name;
        ByPeak.Click += (_, _) => Chart.Sort = HorizonSort.Peak;
        OneBand.Click += (_, _) => Chart.Bands = 1;
        FourBands.Click += (_, _) => Chart.Bands = 4;
    }
}
