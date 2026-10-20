using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Gallery page, laid out like uiarc.dev/components: a top bar, a component sidebar (22 groups with counts)
/// and the selected component's page. Hosts put it in a window (desktop) or in the single view (browser).
/// </summary>
public sealed class GalleryShell : Grid
{
    private const double SidebarWidth = 280;

    private readonly GalleryTheme _theme = new();
    private readonly ContentControl _pageHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly GallerySidebar _sidebar = new();

    public GalleryShell()
    {
        var topBar = TopBar = new GalleryTopBar(_theme, this, _sidebar.FocusSearch);
        Grid.SetRow(topBar, 0);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions($"{SidebarWidth},*") };
        _sidebar.EntrySelected += entry => _pageHost.Content = GalleryPages.Create(entry);
        Grid.SetColumn(_pageHost, 1);
        body.Children.Add(_sidebar);
        body.Children.Add(_pageHost);
        Grid.SetRow(body, 1);

        RowDefinitions = new RowDefinitions("Auto,*");
        Children.Add(topBar);
        Children.Add(body);
        ArcTokens.Bind(this, Panel.BackgroundProperty, ArcDesignTokenKind.Background);
        _sidebar.Select(GalleryCatalog.Categories[0].Entries[0]);
    }

    public GalleryTheme Appearance => _theme;

    public GallerySidebar Sidebar => _sidebar;

    public GalleryTopBar TopBar { get; }

    /// <summary>Opens <paramref name="entry"/>'s page and reveals it in the sidebar.</summary>
    public void Select(GalleryEntry entry) => _sidebar.Select(entry);

    /// <summary>Cmd/Ctrl+K: moves the focus to the sidebar search.</summary>
    internal static bool IsSearchShortcut(KeyEventArgs e) =>
        e.Key == Key.K && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && IsSearchShortcut(e))
        {
            _sidebar.FocusSearch();
            e.Handled = true;
        }
    }
}
