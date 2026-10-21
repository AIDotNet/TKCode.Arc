using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FloatingButtonGroupFromItemsSourceDemo : UserControl
{
    public FloatingButtonGroupFromItemsSourceDemo()
    {
        InitializeComponent();
        Zoom.ItemsSource = new[] { "Fit", "50%", "100%", "200%" };
    }
}
