using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ScrollAreaVerticalDemo : UserControl
{
    public ScrollAreaVerticalDemo()
    {
        InitializeComponent();
    }

    private void OnEdgesChanged(object? sender, ScrollAreaEdgesChangedEventArgs e)
        => Edges.Text = "Edges: " + string.Join(", ",
            new[] { e.Edges.Top ? "top" : null, e.Edges.Bottom ? "bottom" : null }.Where(s => s is not null).DefaultIfEmpty("none"));
}
