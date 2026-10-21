using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DropdownMenuMenuSurfaceDemo : UserControl
{
    public DropdownMenuMenuSurfaceDemo()
    {
        InitializeComponent();
    }

    // The surface is opened from code against any anchor, here the button.
    private void OnAnchorClick(object? sender, RoutedEventArgs e)
    {
        var surface = (MenuSurface)this.FindResource("DraftMenu")!;
        if (surface.IsOpen)
        {
            surface.Close(returnFocus: false);
        }
        else
        {
            surface.Open(Anchor);
        }
    }
}
