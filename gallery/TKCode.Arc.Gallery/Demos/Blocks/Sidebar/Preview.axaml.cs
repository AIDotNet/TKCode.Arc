using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SidebarPreviewDemo : UserControl
{
    private bool _narrow;

    public SidebarPreviewDemo()
    {
        InitializeComponent();
        // On a phone-width screen the sidebar starts as the icon rail, so the page keeps its room; crossing 640 px
        // either way collapses or opens it once, and the toggle still works in between.
        SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 640;
            if (narrow != _narrow)
            {
                _narrow = narrow;
                Nav.IsCollapsed = narrow;
            }
        };
    }

    // The toggle lives in the page's top bar, outside the sidebar: IsCollapsed is a normal two-way property.
    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();
}
