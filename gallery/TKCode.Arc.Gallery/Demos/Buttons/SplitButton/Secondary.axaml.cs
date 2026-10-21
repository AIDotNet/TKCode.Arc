using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SplitButtonSecondaryDemo : UserControl
{
    public SplitButtonSecondaryDemo()
    {
        InitializeComponent();
    }

    // Picking a format makes it the default.
    private void OnItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        Export.Label = e.Header?.ToString() ?? Export.Label;
    }
}
