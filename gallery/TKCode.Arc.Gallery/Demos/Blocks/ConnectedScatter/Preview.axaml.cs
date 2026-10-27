using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConnectedScatterPreviewDemo : UserControl
{
    public ConnectedScatterPreviewDemo()
    {
        InitializeComponent();
        Scatter.XAxis = ConnectedScatterDemoData.CostAxis();
        Scatter.YAxis = ConnectedScatterDemoData.ValueAxis();
        Scatter.Series.AddRange(ConnectedScatterDemoData.Channels());
    }
}
