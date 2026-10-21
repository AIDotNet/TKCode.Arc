using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class IconMorphMorphGlyphDemo : UserControl
{
    // Menu, close and check: menu to close keeps its vertices, close to check resamples by arc length.
    private static readonly string[] Shapes =
    [
        "M4 6h16 M4 12h16 M4 18h16",
        "M6 6 L18 18 M12 12h0 M6 18 L18 6",
        "M4 12.5l5 5L20 6.5"
    ];

    private int _index;

    public IconMorphMorphGlyphDemo()
    {
        InitializeComponent();
    }

    private void OnMorphClick(object? sender, RoutedEventArgs e)
    {
        _index = (_index + 1) % Shapes.Length;
        Morph.Data = Shapes[_index];
    }
}
