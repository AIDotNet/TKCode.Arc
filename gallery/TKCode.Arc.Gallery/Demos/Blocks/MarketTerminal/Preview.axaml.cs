using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MarketTerminalPreviewDemo : UserControl
{
    public MarketTerminalPreviewDemo()
    {
        InitializeComponent();
        MarketTerminalDemoData.Fill(Terminal);
    }
}
