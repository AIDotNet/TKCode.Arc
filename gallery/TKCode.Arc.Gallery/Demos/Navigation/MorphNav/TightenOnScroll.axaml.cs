using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphNavTightenOnScrollDemo : UserControl
{
    public MorphNavTightenOnScrollDemo()
    {
        InitializeComponent();
        Page.ScrollChanged += (_, _) => Nav.IsCompact = Page.Offset.Y > 24;
        Nav.Navigate += (_, e) => Nav.Current = e.Section ?? Nav.Current;
    }
}
