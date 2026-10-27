using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConnectedScatterWeeklyDemo : UserControl
{
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("de-DE");

    public ConnectedScatterWeeklyDemo()
    {
        InitializeComponent();
        Scatter.ValueFormatter = value => value.ToString("N0", s_culture) + " €";
        Scatter.RatioFormatter = ratio => ratio.ToString("0.00", CultureInfo.InvariantCulture) + "×";
        Scatter.XAxis = new ScatterAxis("Spend", "Weekly ad spend", 6000, 16000, 2000);
        Scatter.YAxis = new ScatterAxis("Revenue", "Weekly revenue", 16000, 40000, 8000);
        Scatter.XAxis.TickFormatter = value => (value / 1000).ToString("0", CultureInfo.InvariantCulture) + "k €";
        Scatter.YAxis.TickFormatter = value => (value / 1000).ToString("0", CultureInfo.InvariantCulture) + "k €";
        Scatter.Series.AddRange(ConnectedScatterDemoData.Weekly());
    }
}
