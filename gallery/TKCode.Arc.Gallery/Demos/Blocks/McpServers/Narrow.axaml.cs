using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class McpServersNarrowDemo : UserControl
{
    public McpServersNarrowDemo()
    {
        InitializeComponent();
        // Open the server with a tool turned off, to show the tools at this width.
        Block.ExpandedServer = Block.Servers?.FirstOrDefault(s => s.Id == "ledgerline");
    }
}
