using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChoroplethExplorerCardDemo : UserControl
{
    public ChoroplethExplorerCardDemo()
    {
        InitializeComponent();
        Explorer.Areas.Add(ChoroplethExplorerDemoData.Area("fr"));
    }
}
