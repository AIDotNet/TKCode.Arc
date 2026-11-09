using System.Diagnostics;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.DemoApp;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Pages;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Gallery page: a top bar over either the home page or, like uiarc.dev/components, a sidebar (22 groups with counts)
/// beside the selected page. Hosts put it in a window (desktop) or in the single view (browser); the browser host also
/// keeps <see cref="Route"/> in the address bar (<see cref="RouteChanged"/>, <see cref="Navigate"/>).
/// </summary>
public sealed class GalleryShell : Grid
{
    private const double SidebarWidth = 280;

    /// <summary>Below this width the sidebar leaves the page and opens as a drawer: 280 of a phone's 390 would leave no page.</summary>
    public const double NarrowBelow = 760;

    private readonly GalleryTheme _theme = new();
    private readonly ContentControl _pageHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    // Built when a page first needs it: the home page, where most visits start, has no sidebar. See Sidebar.
    private GallerySidebar? _sidebar;
    private bool _preparing;
    // Long enough for the first frame and the home page's first work to finish; the slices leave the browser most of each frame.
    private static readonly TimeSpan SidebarPreparationStart = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SidebarPreparationSlice = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan SidebarPreparationGap = TimeSpan.FromMilliseconds(32);
    // Holds the sidebar's place before the page: a panel appends a late child to its visual children, and their order is the tab
    // order and the drawing order.
    private readonly Decorator _sidebarHost = new();
    private readonly GallerySponsorBanner _banner = new();
    private readonly StackPanel _chrome = new();
    private readonly Grid _body = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,*") };
    private readonly Drawer _drawer = new() { Side = DrawerSide.Left };
    private readonly Border _browseBar = new() { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 6), IsVisible = false };
    private readonly TextBlock _browseTitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 13);
    private bool _isNarrow;
    private GalleryStreamMarkView? _streamMark;
    private GalleryProjectsView? _projects;
    private GalleryComponentsView? _components;
    private GallerySettingsView? _settings;
    private GalleryHomeView? _home;
    private GalleryFriendsView? _friends;
    private DemoAppView? _demo;
    private GalleryRoute _route = GalleryRoute.Home;
    private GalleryEntry? _lastComponent;
    private GalleryEntry? _lastBlock;
    // The entry of the last page the sidebar opened (not the page: one left for the home page must not stay in memory).
    private GalleryEntry? _entryPageEntry;

    /// <param name="initial">The page to open on; the home page when null.</param>
    public GalleryShell(GalleryRoute? initial = null)
    {
        var topBar = TopBar = new GalleryTopBar(_theme, this);
        _chrome.Children.Add(_banner);
        _chrome.Children.Add(topBar);

        Grid.SetColumn(_pageHost, 1);
        Grid.SetRow(_pageHost, 1);
        Grid.SetRow(_sidebarHost, 1);
        Grid.SetColumnSpan(_browseBar, 2);
        _body.Children.Add(_sidebarHost);
        _body.Children.Add(_pageHost);
        _body.Children.Add(BuildBrowseBar());
        Grid.SetRow(_body, 1);

        RowDefinitions = new RowDefinitions("Auto,*");
        // The top bar's mega menu and menu sheet hang below its own row: they must draw over the page, not under it.
        _chrome.ZIndex = 1;
        Children.Add(_chrome);
        Children.Add(_body);
        GalleryText.Bind(_drawer, ModalOverlay.TitleProperty, GalleryShellLangResourceKind.SidebarOpen);
        Children.Add(_drawer);
        ArcTokens.Bind(this, Panel.BackgroundProperty, ArcDesignTokenKind.Background);
        // A key press goes to the focused element and up through its parents. The home page has nothing to focus, and a window
        // or a browser page hands a press with no focus to the root, so the shell takes the focus: Cmd/Ctrl+K works from the start.
        Focusable = true;
        AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => Focus());
            ScheduleSidebarPreparation();
        };
        Navigate(initial ?? GalleryRoute.Home);
    }

    public GalleryTheme Appearance => _theme;

    /// <summary>The component sidebar; hidden until a page with a sidebar opens. Built the first time it is needed.</summary>
    public GallerySidebar Sidebar
    {
        get
        {
            if (_sidebar is null)
            {
                CreateSidebar(deferred: false);
            }
            // Asked for while it is still being prepared: the rest is built now.
            _sidebar!.CompleteBuild();
            return _sidebar;
        }
    }

    /// <summary>
    /// Whether a shell prepares its sidebar in small steps after its first frame. The WebAssembly host turns this on: there, building
    /// the sidebar on the first click holds up that click for most of a second. Other hosts build it when it is first needed.
    /// </summary>
    public static bool PreparesSidebarWhenIdle { get; set; }

    private void CreateSidebar(bool deferred)
    {
        _sidebar = new GallerySidebar(deferred) { IsVisible = false };
        _sidebar.EntrySelected += OnEntrySelected;
        PlaceSidebar(_sidebar);
    }

    private void ScheduleSidebarPreparation()
    {
        if (!PreparesSidebarWhenIdle || _preparing || _sidebar is not null)
        {
            return;
        }
        _preparing = true;
        DispatcherTimer.RunOnce(PrepareSidebarStep, SidebarPreparationStart, DispatcherPriority.Background);
    }

    /// <summary>Builds sidebar groups for one short slice of time, then lets the page and its input run.</summary>
    private void PrepareSidebarStep()
    {
        if (!IsAttachedToVisualTree())
        {
            _preparing = false;
            return;
        }
        if (_sidebar is null)
        {
            CreateSidebar(deferred: true);
        }
        var slice = Stopwatch.StartNew();
        var more = true;
        while (more && slice.Elapsed < SidebarPreparationSlice)
        {
            more = _sidebar!.BuildStep();
        }
        if (more)
        {
            DispatcherTimer.RunOnce(PrepareSidebarStep, SidebarPreparationGap, DispatcherPriority.Background);
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    public GalleryTopBar TopBar { get; }

    /// <summary>The sponsor banner and the top bar, which draw above the page.</summary>
    public Control Chrome => _chrome;

    /// <summary>The page area under <see cref="Chrome"/>.</summary>
    public Control Body => _body;

    /// <summary>Whether the sidebar is a drawer because the page is narrow (<see cref="NarrowBelow"/>).</summary>
    public bool IsNarrow => _isNarrow;

    /// <summary>The drawer that holds the sidebar on a narrow page.</summary>
    public Drawer SidebarDrawer => _drawer;

    /// <summary>The line that opens the drawer on a narrow page with a sidebar.</summary>
    public Control BrowseBar => _browseBar;

    /// <summary>The page that is open.</summary>
    public GalleryRoute Route => _route;

    /// <summary>Raised when another page opens, whether from the sidebar, the top bar, a link on a page or <see cref="Navigate"/>.</summary>
    public event Action<GalleryRoute>? RouteChanged;

    /// <summary>Opens <paramref name="entry"/>'s page and reveals it in the sidebar.</summary>
    public void Select(GalleryEntry entry) => Navigate(GalleryRoute.To(entry));

    /// <summary>Opens the page at <paramref name="route"/>: the home page without a sidebar, any other with it.</summary>
    public void Navigate(GalleryRoute route)
    {
        // The dashboard demo is an app of its own, as on uiarc.dev/demo: its sidebar and top bar take the whole frame.
        SetChromeVisible(route.Kind != GalleryRouteKind.Demo);
        _drawer.IsOpen = false;
        if (route.Kind == GalleryRouteKind.StreamMark)
        {
            HideSidebar();
            ReleaseFocusFromPage();
            _pageHost.Content = _streamMark ??= new GalleryStreamMarkView(this);
            SetRoute(GalleryRoute.StreamMark);
            return;
        }
        if (route.Kind == GalleryRouteKind.Projects)
        {
            HideSidebar();
            ReleaseFocusFromPage();
            _pageHost.Content = _projects ??= new GalleryProjectsView(this);
            SetRoute(GalleryRoute.Projects);
            return;
        }
        if (route.Kind == GalleryRouteKind.ComponentsIndex)
        {
            HideSidebar();
            ReleaseFocusFromPage();
            _pageHost.Content = _components ??= new GalleryComponentsView(this);
            SetRoute(GalleryRoute.ComponentsIndex);
            return;
        }
        if (route.Kind == GalleryRouteKind.Settings)
        {
            HideSidebar();
            ReleaseFocusFromPage();
            var settings = _settings ??= new GallerySettingsView(this);
            settings.Show(route.Page ?? GallerySettingsView.AboutPage);
            _pageHost.Content = settings;
            SetRoute(GalleryRoute.Settings(settings.CurrentPage));
            return;
        }
        if (route.Kind == GalleryRouteKind.Demo)
        {
            HideSidebar();
            var demo = _demo ??= new DemoAppView(this);
            var page = demo.Resolve(route.Page);
            if (_pageHost.Content != demo)
            {
                ReleaseFocusFromPage();
                _pageHost.Content = demo;
            }
            demo.Show(page);
            SetRoute(GalleryRoute.Demo(page.Slug));
            return;
        }
        if (route.Kind == GalleryRouteKind.Friends)
        {
            HideSidebar();
            ReleaseFocusFromPage();
            _pageHost.Content = _friends ??= new GalleryFriendsView();
            SetRoute(GalleryRoute.Friends);
            return;
        }
        if (route.Entry is not { } entry)
        {
            HideSidebar();
            ReleaseFocusFromPage();
            _pageHost.Content = _home ??= new GalleryHomeView(this);
            SetRoute(GalleryRoute.Home);
            return;
        }
        if (_route == route && _pageHost.Content is not null && _sidebar is { IsVisible: true })
        {
            return;
        }
        var sidebar = Sidebar;
        sidebar.IsVisible = true;
        sidebar.Select(entry);
    }

    private void HideSidebar()
    {
        if (_sidebar is not null)
        {
            _sidebar.IsVisible = false;
        }
    }

    /// <summary>Opens the home page at its recommendation of the desktop app (the site only).</summary>
    public void ShowDownload()
    {
        Navigate(GalleryRoute.Home);
        Dispatcher.UIThread.Post(() => _home?.ShowDownload(), DispatcherPriority.Background);
    }

    /// <summary>Opens the settings at About and checks for a newer version (the desktop app's menu).</summary>
    public void CheckForUpdates()
    {
        Navigate(GalleryRoute.Settings(GallerySettingsView.AboutPage));
        _ = GalleryUpdates.CheckAsync();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SetNarrow(e.NewSize.Width < NarrowBelow);
    }

    /// <summary>Moves the sidebar between the page and the drawer.</summary>
    private void SetNarrow(bool narrow)
    {
        if (narrow == _isNarrow)
        {
            return;
        }
        _isNarrow = narrow;
        if (narrow)
        {
            if (_sidebar is not null)
            {
                _sidebarHost.Child = null;
                PlaceSidebar(_sidebar);
            }
        }
        else
        {
            _drawer.IsOpen = false;
            if (_sidebar is not null)
            {
                _drawer.Content = null;
                PlaceSidebar(_sidebar);
            }
        }
        Grid.SetColumn(_pageHost, narrow ? 0 : 1);
        Grid.SetColumnSpan(_pageHost, narrow ? 2 : 1);
        SyncBrowseBar();
    }

    /// <summary>Puts the sidebar where the page's width wants it: in the drawer on a narrow page, beside the page otherwise.</summary>
    private void PlaceSidebar(GallerySidebar sidebar)
    {
        if (_isNarrow)
        {
            sidebar.Width = double.NaN;
            sidebar.BorderThickness = default;
            _drawer.Content = sidebar;
        }
        else
        {
            sidebar.Width = SidebarWidth;
            sidebar.BorderThickness = new Thickness(0, 0, 1, 0);
            _sidebarHost.Child = sidebar;
        }
    }

    private Control BuildBrowseBar()
    {
        var open = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            Content = GallerySite.Labelled(GalleryShellLangResourceKind.SidebarOpen, ArcIcons.Menu)
        };
        GalleryText.Bind(open, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.SidebarOpen);
        open.Click += (_, _) => _drawer.IsOpen = true;
        _browseTitle.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        _browseTitle.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        _browseTitle.VerticalAlignment = VerticalAlignment.Center;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        Grid.SetColumn(_browseTitle, 1);
        row.Children.Add(open);
        row.Children.Add(_browseTitle);
        _browseBar.Child = row;
        ArcTokens.Bind(_browseBar, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(_browseBar, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        return _browseBar;
    }

    private void SyncBrowseBar()
    {
        _browseBar.IsVisible = _isNarrow && _route.Entry is not null;
        _browseTitle.Text = _route.Entry?.Title;
    }

    /// <summary>Shows or hides the sponsor banner and the top bar (the banner keeps its own dismissal).</summary>
    private void SetChromeVisible(bool visible) => _chrome.IsVisible = visible;

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
        if (_route.Kind is not (GalleryRouteKind.Docs or GalleryRouteKind.Components or GalleryRouteKind.Blocks))
        {
            Navigate(RouteFor(GalleryRouteKind.Components));
        }
        if (_isNarrow)
        {
            _drawer.IsOpen = true;
            Dispatcher.UIThread.Post(Sidebar.FocusSearch, DispatcherPriority.Background);
            return;
        }
        Sidebar.FocusSearch();
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
        // A click on the open page's row (or a route back to it) keeps the page and its demos' state instead of building it again.
        if (ReferenceEquals(entry, _entryPageEntry) && IsEntryPageShown)
        {
            _drawer.IsOpen = false;
            SetRoute(GalleryRoute.To(entry));
            return;
        }
        ReleaseFocusFromPage();
        _drawer.IsOpen = false;
        _entryPageEntry = entry;
        _pageHost.Content = GalleryPages.Create(entry);
        SetRoute(GalleryRoute.To(entry));
    }

    /// <summary>Whether the page host shows a sidebar page (not the home page, Friends or the dashboard demo).</summary>
    private bool IsEntryPageShown => _pageHost.Content is { } content
        && !ReferenceEquals(content, _home) && !ReferenceEquals(content, _friends) && !ReferenceEquals(content, _demo)
        && !ReferenceEquals(content, _streamMark) && !ReferenceEquals(content, _projects) && !ReferenceEquals(content, _components)
        && !ReferenceEquals(content, _settings);

    /// <summary>
    /// The window remembers the last focused element of its focus scope, and Avalonia forgets it only when that element
    /// still has the focus as it leaves. A page whose remembered element had lost the focus (the window was inactive, or
    /// the page was replaced from code) would stay in memory, so the shell takes the focus before the page goes.
    /// </summary>
    private void ReleaseFocusFromPage()
    {
        if (_pageHost.Content is Visual page && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
            && (ReferenceEquals(focused, page) || page.IsVisualAncestorOf(focused)))
        {
            Focus();
        }
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
        SyncBrowseBar();
        RouteChanged?.Invoke(route);
    }
}
