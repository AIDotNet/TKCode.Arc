using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FilterToolbarFilterMenuDemo : UserControl
{
    public FilterToolbarFilterMenuDemo()
    {
        InitializeComponent();
    }

    private void OnFilterSelected(object? sender, FilterMenuSelectedEventArgs e) =>
        Picked.Text = $"Picked {e.Filter.Text}";
}
