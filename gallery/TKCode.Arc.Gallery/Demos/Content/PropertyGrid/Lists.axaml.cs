using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PropertyGridListsDemo : UserControl
{
    public PropertyGridListsDemo()
    {
        InitializeComponent();
        Properties.SelectedObject = new DemoPolyline();
        // Open the list and its first vertex, so the nesting shows at once.
        Properties.FindRow("Vertices")!.IsExpanded = true;
        Properties.FindRow("Vertices[0]")!.IsExpanded = true;
    }
}
