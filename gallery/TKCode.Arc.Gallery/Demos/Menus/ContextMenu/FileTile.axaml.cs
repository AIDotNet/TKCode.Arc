using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ContextMenuFileTileDemo : UserControl
{
    public ContextMenuFileTileDemo()
    {
        InitializeComponent();
    }

    private void OnReportItemClicked(object? sender, MenuEntryClickedEventArgs e)
        => Status.Text = $"Q3 report.pdf: {e.Header}";

    private void OnLaunchItemClicked(object? sender, MenuEntryClickedEventArgs e)
        => Status.Text = $"Launch.png: {e.Header}";

    // A row without an icon shows a check when it is checked and a quiet ellipsis otherwise.
    private void OnPinnedClick(object? sender, RoutedEventArgs e) => Pinned.IsChecked = !Pinned.IsChecked;
}
