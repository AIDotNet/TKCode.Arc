using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChoroplethExplorerSingleAreaDemo : UserControl
{
    public ChoroplethExplorerSingleAreaDemo()
    {
        InitializeComponent();
        Explorer.Areas.Add(ChoroplethExplorerDemoData.Area("de"));
    }
}
