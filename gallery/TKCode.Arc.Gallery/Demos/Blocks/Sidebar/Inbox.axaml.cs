using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SidebarInboxDemo : UserControl
{
    public SidebarInboxDemo()
    {
        InitializeComponent();
    }

    // The toggle lives in the list's top bar, outside the sidebar: IsCollapsed is a normal two-way property.
    private void OnToggleSidebar(object? sender, RoutedEventArgs e) => Nav.ToggleCollapsed();
}
