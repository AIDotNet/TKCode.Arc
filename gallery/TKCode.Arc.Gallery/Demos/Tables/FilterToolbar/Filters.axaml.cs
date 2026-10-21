using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class FilterToolbarFiltersDemo : UserControl
{
    public FilterToolbarFiltersDemo()
    {
        InitializeComponent();
    }

    private void OnFiltersChanged(object? sender, FilterToolbarFiltersChangedEventArgs e) =>
        Log.Text = $"{e.Kind}: {e.Filter?.Text ?? "all"} · {e.ActiveFilters.Count} active";
}
