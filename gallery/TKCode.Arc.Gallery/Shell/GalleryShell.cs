using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Gallery page: a top bar over either the home page or, like uiarc.dev/components, a sidebar (22 groups with counts)
/// beside the selected page. Hosts put it in a window (desktop) or in the single view (browser); the browser host also
/// keeps <see cref="Route"/> in the address bar (<see cref="RouteChanged"/>, <see cref="Navigate"/>).
/// </summary>
public sealed class GalleryShell : Grid
{
    private const double SidebarWidth = 280;

    private readonly GalleryTheme _theme = new();
    private readonly ContentControl _pageHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly GallerySidebar _sidebar = new() { Width = SidebarWidth };
    private readonly GallerySponsorBanner _banner = new();
    private GalleryHomeView? _home;
    private GalleryFriendsView? _friends;
    private GalleryRoute _route = GalleryRoute.Home;
    private GalleryEntry? _lastComponent;
    private GalleryEntry? _lastBlock;

    /// <param name="initial">The page to open on; the home page when null.</param>
    public GalleryShell(GalleryRoute? initial = null)
    {
        var topBar = TopBar = new GalleryTopBar(_theme, this);
        Grid.SetRow(_banner, 0);
        Grid.SetRow(topBar, 1);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        _sidebar.EntrySelected += OnEntrySelected;
        Grid.SetColumn(_pageHost, 1);
        body.Children.Add(_sidebar);
        body.Children.Add(_pageHost);
        Grid.SetRow(body, 2);

        RowDefinitions = new RowDefinitions("Auto,Auto,*");
        Children.Add(_banner);
        Children.Add(topBar);
        Children.Add(body);
        ArcTokens.Bind(this, Panel.BackgroundProperty, ArcDesignTokenKind.Background);
        // A key press goes to the focused element and up through its parents. The home page has nothing to focus, and a window
        // or a browser page hands a press with no focus to the root, so the shell takes the focus: Cmd/Ctrl+K works from the start.
        Focusable = true;
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => Focus());
        Navigate(initial ?? GalleryRoute.Home);
    }

    public GalleryTheme Appearance => _theme;

    public GallerySidebar Sidebar => _sidebar;

    public GalleryTopBar TopBar { get; }

    /// <summary>The page that is open.</summary>
    public GalleryRoute Route => _route;

    /// <summary>Raised when another page opens, whether from the sidebar, the top bar, a link on a page or <see cref="Navigate"/>.</summary>
    public event Action<GalleryRoute>? RouteChanged;

    /// <summary>Opens <paramref name="entry"/>'s page and reveals it in the sidebar.</summary>
    public void Select(GalleryEntry entry) => Navigate(GalleryRoute.To(entry));

    /// <summary>Opens the page at <paramref name="route"/>: the home page without a sidebar, any other with it.</summary>
    public void Navigate(GalleryRoute route)
    {
        if (route.Kind == GalleryRouteKind.Friends)
        {
            _sidebar.IsVisible = false;
            _pageHost.Content = _friends ??= new GalleryFriendsView();
            SetRoute(GalleryRoute.Friends);
            return;
        }
        if (route.Entry is not { } entry)
        {
            _sidebar.IsVisible = false;
            _pageHost.Content = _home ??= new GalleryHomeView(this);
            SetRoute(GalleryRoute.Home);
            return;
        }
        if (_route == route && _pageHost.Content is not null && _sidebar.IsVisible)
        {
            return;
        }
        _sidebar.IsVisible = true;
        _sidebar.Select(entry);
    }

    /// <summary>
    /// The page a top menu item opens: the component or block that was open last, so a visitor returns to where they were,
    /// or the first one; Docs opens its first page.
    /// </summary>
    public GalleryRoute RouteFor(GalleryRouteKind kind) => kind switch
    {
        GalleryRouteKind.Components => GalleryRoute.To(_lastComponent ?? GalleryCatalog.Categories[0].Entries[0]),
        GalleryRouteKind.Blocks => GalleryRoute.To(_lastBlock ?? GalleryCatalog.Blocks.AllEntries.First()),
        GalleryRouteKind.Docs => GalleryRoute.To(GalleryCatalog.Docs.Entries[0]),
        GalleryRouteKind.Friends => GalleryRoute.Friends,
        _ => GalleryRoute.Home
    };

    /// <summary>Moves the focus to the sidebar search; on the home page, which has no sidebar, the components open first.</summary>
    public void FocusSearch()
    {
        if (_route.Kind == GalleryRouteKind.Home)
        {
            Navigate(RouteFor(GalleryRouteKind.Components));
        }
        _sidebar.FocusSearch();
    }

    /// <summary>Cmd/Ctrl+K: moves the focus to the sidebar search.</summary>
    internal static bool IsSearchShortcut(KeyEventArgs e) =>
        e.Key == Key.K && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && IsSearchShortcut(e))
        {
            FocusSearch();
            e.Handled = true;
        }
    }

    private void OnEntrySelected(GalleryEntry entry)
    {
        _pageHost.Content = GalleryPages.Create(entry);
        SetRoute(GalleryRoute.To(entry));
    }

    private void SetRoute(GalleryRoute route)
    {
        switch (route.Kind)
        {
            case GalleryRouteKind.Components:
                _lastComponent = route.Entry;
                break;
            case GalleryRouteKind.Blocks:
                _lastBlock = route.Entry;
                break;
        }
        if (route == _route)
        {
            return;
        }
        _route = route;
        TopBar.SyncRoute(route);
        RouteChanged?.Invoke(route);
    }
}
