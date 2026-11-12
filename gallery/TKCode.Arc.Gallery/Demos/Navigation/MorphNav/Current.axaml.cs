using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MorphNavCurrentDemo : UserControl
{
    public MorphNavCurrentDemo()
    {
        InitializeComponent();
        Nav.Navigate += (_, e) =>
        {
            // A router would load e.Href here; the demo shows the page's name.
            Page.Text = e.Label;
            Nav.Current = e.Section ?? "overview";
        };
    }
}
