using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A staggered grid of variations with a Generate again button.</summary>
public partial class ImageRevealGridDemo : UserControl
{
    private int _run;

    public ImageRevealGridDemo()
    {
        InitializeComponent();
    }

    private void OnGenerateAgain(object? sender, RoutedEventArgs e)
    {
        _run++;
        Tile1.RunKey = _run;
        Tile2.RunKey = _run;
        Tile3.RunKey = _run;
        Tile4.RunKey = _run;
    }
}
