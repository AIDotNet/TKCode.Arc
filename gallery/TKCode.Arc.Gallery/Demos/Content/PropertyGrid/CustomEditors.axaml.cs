using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PropertyGridCustomEditorsDemo : UserControl
{
    public PropertyGridCustomEditorsDemo()
    {
        InitializeComponent();
        Properties.SelectedObject = new DemoArc();
    }
}
