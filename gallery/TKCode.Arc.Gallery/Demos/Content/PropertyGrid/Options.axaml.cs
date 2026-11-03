using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PropertyGridOptionsDemo : UserControl
{
    public PropertyGridOptionsDemo()
    {
        InitializeComponent();
        Properties.SelectedObject = new DemoLine();
    }
}
