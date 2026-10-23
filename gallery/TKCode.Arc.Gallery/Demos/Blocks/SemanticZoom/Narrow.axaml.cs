using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SemanticZoomNarrowDemo : UserControl
{
    public SemanticZoomNarrowDemo()
    {
        InitializeComponent();
        Map.Organization = SemanticZoomDemoData.Shared;
    }
}
