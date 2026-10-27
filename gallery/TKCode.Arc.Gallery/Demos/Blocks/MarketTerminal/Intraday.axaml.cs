using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MarketTerminalIntradayDemo : UserControl
{
    public MarketTerminalIntradayDemo()
    {
        InitializeComponent();
        MarketTerminalDemoData.Fill(Terminal);
    }
}
