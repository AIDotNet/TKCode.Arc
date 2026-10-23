using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SemanticZoomPreviewDemo : UserControl
{
    public SemanticZoomPreviewDemo()
    {
        InitializeComponent();
        // Seeded sample data, shared by the demos on this page: the same map on every run.
        Map.Organization = SemanticZoomDemoData.Shared;
    }
}
