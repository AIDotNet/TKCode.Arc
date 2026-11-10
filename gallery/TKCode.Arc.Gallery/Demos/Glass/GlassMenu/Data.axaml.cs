using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GlassMenuDataDemo : UserControl
{
    public GlassMenuDataDemo()
    {
        InitializeComponent();
        Sort.ItemsSource = new[] { "Name", "Date modified", "Size", "Kind" };
        Sort.ItemInvoked += (_, e) => Log.Text = $"Sorted by {e.Item}";
    }
}
