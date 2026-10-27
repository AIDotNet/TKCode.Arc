using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ConnectedScatterMinimalDemo : UserControl
{
    public ConnectedScatterMinimalDemo()
    {
        InitializeComponent();
        Scatter.XAxis = new ScatterAxis { Name = "CAC" };
        Scatter.YAxis = new ScatterAxis { Name = "LTV" };
        Scatter.Series.AddRange(ConnectedScatterDemoData.PlainChannels());
    }
}
