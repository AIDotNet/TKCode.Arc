using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SidebarLiveUpdatesDemo : UserControl
{
    private static readonly (string Label, Avalonia.Media.Geometry Icon)[] s_pages =
    [
        ("Release plan", ArcIcons.Flag),
        ("Support queue", ArcIcons.MessageSquare),
        ("Brand assets", ArcIcons.Image),
        ("Hiring board", ArcIcons.Users)
    ];

    private int _next;

    public SidebarLiveUpdatesDemo()
    {
        InitializeComponent();
    }

    private SidebarItem Inbox => Nav.Sections[0].Items[1];

    private SidebarSection Pinned => Nav.Sections[1];

    private void OnNewMessage(object? sender, RoutedEventArgs e)
    {
        // The row's count follows the item; on the icon rail it shows as a dot.
        var count = int.TryParse(Inbox.Badge, out var n) ? n : 0;
        Inbox.Badge = (count + 1).ToString();
    }

    private void OnMarkRead(object? sender, RoutedEventArgs e) => Inbox.Badge = null;

    private void OnPin(object? sender, RoutedEventArgs e)
    {
        var (label, icon) = s_pages[_next % s_pages.Length];
        _next++;
        Pinned.IsExpanded = true;
        Pinned.Items.Add(new SidebarItem { Key = $"pinned/{_next}", Label = label, Icon = icon });
    }

    private void OnUnpin(object? sender, RoutedEventArgs e)
    {
        if (Pinned.Items.Count > 0)
        {
            Pinned.Items.RemoveAt(Pinned.Items.Count - 1);
        }
    }
}
