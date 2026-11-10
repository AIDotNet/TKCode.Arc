using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class DocsSidebarOwnPagesDemo : UserControl
{
    public DocsSidebarOwnPagesDemo()
    {
        InitializeComponent();
    }

    // A real site navigates here; the sidebar already marks the page.
    private void OnPageInvoked(object? sender, DocsSidebarPageEventArgs e) => Toast.Show(this, $"Open {e.Page.Key}");

    // Previous and Next walk the pages in reading order; the sidebar opens sections and scrolls to follow.
    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Go(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => Go(1);

    private void Go(int direction)
    {
        if (Docs.GetAdjacentPage(direction) is { } page)
        {
            Docs.CurrentKey = page.Key;
        }
    }
}
