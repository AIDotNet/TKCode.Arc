using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConnectedScatterNarrowDemo : UserControl
{
    public ConnectedScatterNarrowDemo()
    {
        InitializeComponent();
        Scatter.XAxis = ConnectedScatterDemoData.CostAxis();
        Scatter.YAxis = ConnectedScatterDemoData.ValueAxis();
        Scatter.Series.AddRange(ConnectedScatterDemoData.Channels());
    }
}
