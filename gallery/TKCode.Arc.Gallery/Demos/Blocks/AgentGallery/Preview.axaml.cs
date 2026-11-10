using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentGalleryPreviewDemo : UserControl
{
    public AgentGalleryPreviewDemo()
    {
        InitializeComponent();
        // Two sample agents start installed.
        Gallery.InstalledIds = ["inbox-sorter", "patch-pilot"];
    }
}
