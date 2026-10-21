using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Demos;

public partial class ImageCompareVerticalDemo : UserControl
{
    public ImageCompareVerticalDemo()
    {
        InitializeComponent();
    }

    private void OnSwingClick(object? sender, RoutedEventArgs e)
    {
        Compare.Orientation = Compare.Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        Swing.Content = Compare.Orientation == Orientation.Vertical ? "Swing to horizontal" : "Swing to vertical";
    }
}
