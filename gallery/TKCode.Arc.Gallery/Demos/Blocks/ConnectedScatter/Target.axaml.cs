using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConnectedScatterTargetDemo : UserControl
{
    public ConnectedScatterTargetDemo()
    {
        InitializeComponent();
        Scatter.XAxis = new ScatterAxis("CAC", "Customer acquisition cost", 900, 1800, 100);
        Scatter.YAxis = new ScatterAxis("LTV", "12 month customer value", 3500, 6000, 500);
        Scatter.Series.AddRange(ConnectedScatterDemoData.Channels());
    }
}
