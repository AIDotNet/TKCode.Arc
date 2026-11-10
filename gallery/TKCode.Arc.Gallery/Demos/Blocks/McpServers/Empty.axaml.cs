using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class McpServersEmptyDemo : UserControl
{
    public McpServersEmptyDemo()
    {
        InitializeComponent();
        Block.Servers = new ObservableCollection<McpServer>();
    }
}
