using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SidebarPreviewDemo : UserControl
{
    public SidebarPreviewDemo()
    {
        InitializeComponent();
    }

    // The toggle lives in the page's top bar, outside the sidebar: IsCollapsed is a normal two-way property.
    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();
}
