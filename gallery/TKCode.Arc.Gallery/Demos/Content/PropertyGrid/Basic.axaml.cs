using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PropertyGridBasicDemo : UserControl
{
    public PropertyGridBasicDemo()
    {
        InitializeComponent();
        // DemoArc is marked [PropertyGridModel]: the generator registered its properties at startup.
        Properties.SelectedObject = new DemoArc();
    }
}
