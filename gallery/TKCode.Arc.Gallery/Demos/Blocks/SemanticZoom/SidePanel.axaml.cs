using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SemanticZoomSidePanelDemo : UserControl
{
    public SemanticZoomSidePanelDemo()
    {
        InitializeComponent();
        Map.Organization = SemanticZoomDemoData.Shared;
    }
}
