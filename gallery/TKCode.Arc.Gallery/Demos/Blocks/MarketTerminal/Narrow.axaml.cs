using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MarketTerminalNarrowDemo : UserControl
{
    public MarketTerminalNarrowDemo()
    {
        InitializeComponent();
        MarketTerminalDemoData.Fill(Terminal);
    }
}
