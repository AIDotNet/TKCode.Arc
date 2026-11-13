using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The navigation of a documentation site, built on <see cref="Sidebar"/>: the product name with a version switcher,
/// a search box that narrows the pages as you type ("/" or Ctrl+K puts the caret in it), titled sections that fold,
/// pages that open into sub-pages, and the current page marked. Whenever <see cref="CurrentKey"/> changes, from a click
/// or from your router, the page's section and parent open and the list scrolls so the page is in view.
/// <see cref="GetAdjacentPage"/> gives the previous and next page for a docs footer. Without <see cref="Sections"/> it
/// shows an invented example site.
/// </summary>
[TemplatePart(SidebarPart, typeof(Sidebar))]
[TemplatePart(VersionMenuPart, typeof(DropdownMenu))]
public class DocsSidebar : TemplatedControl
{
    public const string SidebarPart = "PART_Sidebar";
    public const string VersionMenuPart = "PART_VersionMenu";

    internal const string VersionButtonPart = "PART_VersionButton";

    #region Properties

    /// <summary>The product name in the header.</summary>
    public static readonly StyledProperty<string> ProductNameProperty =
        AvaloniaProperty.Register<DocsSidebar, string>(nameof(ProductName), "Harborline");

    /// <summary>The key of the page being read (two-way); bind it to your route. Its section and parent open and it scrolls into view.</summary>
    public static readonly StyledProperty<string?> CurrentKeyProperty =
        AvaloniaProperty.Register<DocsSidebar, string?>(nameof(CurrentKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The documentation versions in the switcher, newest first; empty hides the switcher. The default lists three invented versions.</summary>
    public static readonly DirectProperty<DocsSidebar, IList<DocsSidebarVersion>> VersionsProperty =
        AvaloniaProperty.RegisterDirect<DocsSidebar, IList<DocsSidebarVersion>>(nameof(Versions), o => o.Versions, (o, v) => o.Versions = v);

    /// <summary>The <see cref="DocsSidebarVersion.Name"/> of the chosen version (two-way); null means the first.</summary>
    public static readonly StyledProperty<string?> SelectedVersionProperty =
        AvaloniaProperty.Register<DocsSidebar, string?>(nameof(SelectedVersion), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text (two-way). Pages whose title contains it stay; their sections and parents open.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<DocsSidebar, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether "/" and Ctrl+K (Cmd+K) anywhere in the window put the caret in the search box.</summary>
    public static readonly StyledProperty<bool> IsSearchShortcutEnabledProperty =
        AvaloniaProperty.Register<DocsSidebar, bool>(nameof(IsSearchShortcutEnabled), true);

    /// <summary>Content under the pages, such as a link to the changelog or a theme switch.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<DocsSidebar, object?>(nameof(Footer));

    /// <summary>The built-in text; replace the whole object to localize the sidebar.</summary>
    public static readonly StyledProperty<DocsSidebarLabels> LabelsProperty =
        AvaloniaProperty.Register<DocsSidebar, DocsSidebarLabels>(nameof(Labels), new DocsSidebarLabels());

    /// <inheritdoc cref="ProductNameProperty"/>
    public string ProductName { get => GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }

    /// <inheritdoc cref="CurrentKeyProperty"/>
    public string? CurrentKey { get => GetValue(CurrentKeyProperty); set => SetValue(CurrentKeyProperty, value); }

    private IList<DocsSidebarVersion> _versions = new AvaloniaList<DocsSidebarVersion>
    {
        new("v3.2", "latest"),
        new("v3.1"),
        new("v2.9", "legacy")
    };

    /// <inheritdoc cref="VersionsProperty"/>
    public IList<DocsSidebarVersion> Versions
    {
        get => _versions;
        set
        {
            if (SetAndRaise(VersionsProperty, ref _versions, value ?? new AvaloniaList<DocsSidebarVersion>()))
            {
                BuildVersions();
            }
        }
    }

    /// <inheritdoc cref="SelectedVersionProperty"/>
    public string? SelectedVersion { get => GetValue(SelectedVersionProperty); set => SetValue(SelectedVersionProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="IsSearchShortcutEnabledProperty"/>
    public bool IsSearchShortcutEnabled { get => GetValue(IsSearchShortcutEnabledProperty); set => SetValue(IsSearchShortcutEnabledProperty, value); }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DocsSidebarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The sections of pages, in order (the XAML content). Empty shows <see cref="SampleSections"/>.</summary>
    [Content]
    public AvaloniaList<DocsSidebarSection> Sections { get; } = new();

    /// <summary>The sections on screen: <see cref="Sections"/>, or the examples while it is empty.</summary>
    public IReadOnlyList<DocsSidebarSection> ShownSections => _shown;

    /// <summary>The current page, or null.</summary>
    public DocsSidebarPage? CurrentPage => CurrentKey is { } key ? Find(key) : null;

    #endregion

    #region Events

    /// <summary>Raised when the person chooses a page (it becomes <see cref="CurrentKey"/>), also when it already was current.</summary>
    public static readonly RoutedEvent<DocsSidebarPageEventArgs> PageInvokedEvent =
        RoutedEvent.Register<DocsSidebar, DocsSidebarPageEventArgs>(nameof(PageInvoked), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when <see cref="CurrentKey"/> changes, whether a page was chosen or the key was set in code, through a
    /// binding or by your router. Choosing the page that is already current raises <see cref="PageInvoked"/> only.
    /// </summary>
    public static readonly RoutedEvent<DocsSidebarSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<DocsSidebar, DocsSidebarSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when another version is chosen in the switcher.</summary>
    public static readonly RoutedEvent<DocsSidebarVersionChangedEventArgs> VersionChangedEvent =
        RoutedEvent.Register<DocsSidebar, DocsSidebarVersionChangedEventArgs>(nameof(VersionChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PageInvokedEvent"/>
    public event EventHandler<DocsSidebarPageEventArgs>? PageInvoked
    {
        add => AddHandler(PageInvokedEvent, value);
        remove => RemoveHandler(PageInvokedEvent, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<DocsSidebarSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="VersionChangedEvent"/>
    public event EventHandler<DocsSidebarVersionChangedEventArgs>? VersionChanged
    {
        add => AddHandler(VersionChangedEvent, value);
        remove => RemoveHandler(VersionChangedEvent, value);
    }

    #endregion

    /// <summary>An invented documentation site: Getting started, Guides, API reference and Help.</summary>
    public static IReadOnlyList<DocsSidebarSection> SampleSections()
    {
        static DocsSidebarPage Parent(string key, string title, params DocsSidebarPage[] pages)
        {
            var page = new DocsSidebarPage(key, title);
            page.Pages.AddRange(pages);
            return page;
        }

        return
        [
            new DocsSidebarSection("Getting started",
                new DocsSidebarPage("start/introduction", "Introduction"),
                new DocsSidebarPage("start/installation", "Installation"),
                new DocsSidebarPage("start/quickstart", "Quickstart"),
                new DocsSidebarPage("start/project-structure", "Project structure")),
            new DocsSidebarSection("Guides",
                Parent("guides/routing", "Routing",
                    new DocsSidebarPage("guides/routing/static", "Static routes"),
                    new DocsSidebarPage("guides/routing/dynamic", "Dynamic routes"),
                    new DocsSidebarPage("guides/routing/redirects", "Redirects")),
                new DocsSidebarPage("guides/data", "Loading data"),
                new DocsSidebarPage("guides/caching", "Caching", "New"),
                new DocsSidebarPage("guides/forms", "Forms and actions"),
                new DocsSidebarPage("guides/deploying", "Deploying")),
            new DocsSidebarSection("API reference",
                new DocsSidebarPage("api/config", "Configuration"),
                Parent("api/cli", "Command line",
                    new DocsSidebarPage("api/cli/dev", "dev"),
                    new DocsSidebarPage("api/cli/build", "build"),
                    new DocsSidebarPage("api/cli/deploy", "deploy")),
                new DocsSidebarPage("api/hooks", "Hooks", "Beta")),
            new DocsSidebarSection("Help",
                new DocsSidebarPage("help/troubleshooting", "Troubleshooting"),
                new DocsSidebarPage("help/changelog", "Changelog"))
        ];
    }

    private IReadOnlyList<DocsSidebarSection> _shown;
    private readonly Dictionary<DocsSidebarPage, SidebarItem> _items = new();
    private readonly List<IDisposable> _pageBindings = new();
    private readonly Dictionary<DocsSidebarSection, SidebarSection> _sections = new();
    private Sidebar? _sidebar;
    private DropdownMenu? _versionMenu;
    private Control? _versionButton;
    private TextBlock? _versionText;
    private Run? _productRun;
    private Run? _docsRun;
    private TopLevel? _topLevel;
    private bool _syncing;
    private bool _rebuildPosted;
    private DispatcherTimer? _revealAgain;

    static DocsSidebar()
    {
        FocusableProperty.OverrideDefaultValue<DocsSidebar>(false);
    }

    public DocsSidebar()
    {
        _shown = SampleSections();
        SetCurrentValue(CurrentKeyProperty, "guides/caching");
        Sections.CollectionChanged += OnSectionsChanged;
    }

    #region Public methods

    /// <summary>The page with <paramref name="key"/>, or null.</summary>
    public DocsSidebarPage? Find(string key) => AllPages().FirstOrDefault(p => p.Key == key);

    /// <summary>The page before (<paramref name="direction"/> −1) or after (+1) the current one in reading order, or null at the ends.</summary>
    public DocsSidebarPage? GetAdjacentPage(int direction)
    {
        var pages = ReadingOrder();
        var index = pages.FindIndex(p => p.Page.Key == CurrentKey);
        if (index < 0)
        {
            return null;
        }
        var step = Math.Sign(direction);
        if (step == 0)
        {
            return pages[index].Page;
        }
        // Hidden and disabled pages are not reachable from the sidebar, so a "next" link must not point at them.
        for (var at = index + step; at >= 0 && at < pages.Count; at += step)
        {
            if (pages[at].Reachable)
            {
                return pages[at].Page;
            }
        }
        return null;
    }

    /// <summary>
    /// The pages to read in order, each with whether the sidebar lets a person reach it. Parents with sub-pages are
    /// folders, not pages to read.
    /// </summary>
    private List<(DocsSidebarPage Page, bool Reachable)> ReadingOrder()
    {
        var order = new List<(DocsSidebarPage, bool)>();
        foreach (var section in _shown)
        {
            foreach (var page in section.Pages)
            {
                var open = section.IsVisible && page.IsVisible && page.IsEnabled;
                if (page.Pages.Count == 0)
                {
                    order.Add((page, open));
                    continue;
                }
                foreach (var child in page.Pages)
                {
                    if (child.Pages.Count == 0)
                    {
                        order.Add((child, open && child.IsVisible && child.IsEnabled));
                    }
                }
            }
        }
        return order;
    }

    /// <summary>Puts the caret in the search box and selects its text.</summary>
    public void FocusSearch()
    {
        if (FindFilter() is not { } filter)
        {
            return;
        }
        var box = filter as TextBox ?? filter.DocsSidebarFind<TextBox>();
        if (box is not null)
        {
            box.Focus(NavigationMethod.Tab);
            box.SelectAll();
        }
    }

    /// <summary>Opens the current page's section and parent and scrolls it into view.</summary>
    public void RevealCurrent()
    {
        if (CurrentPage is not { } page)
        {
            return;
        }
        foreach (var section in _shown)
        {
            if (section.Pages.Contains(page) || section.Pages.Any(p => p.Pages.Contains(page)))
            {
                section.SetCurrentValue(DocsSidebarSection.IsExpandedProperty, true);
            }
            foreach (var parent in section.Pages)
            {
                if (parent.Pages.Contains(page) && _items.TryGetValue(parent, out var parentItem))
                {
                    parentItem.SetCurrentValue(SidebarItem.IsExpandedProperty, true);
                }
            }
        }
        // The row has its place after the next layout; opening sections spring their height, so look again once they settle.
        Dispatcher.UIThread.Post(ScrollToCurrent, DispatcherPriority.Loaded);
        if (ArcMotion.IsFull(this))
        {
            _revealAgain ??= new DispatcherTimer(TimeSpan.FromMilliseconds(420), DispatcherPriority.Background, (_, _) =>
            {
                _revealAgain?.Stop();
                ScrollToCurrent();
            });
            _revealAgain.Stop();
            _revealAgain.Start();
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_sidebar is not null)
        {
            _sidebar.ItemInvoked -= OnItemInvoked;
            _sidebar.PropertyChanged -= OnSidebarPropertyChanged;
        }
        if (_versionMenu is not null)
        {
            _versionMenu.ItemClicked -= OnVersionClicked;
        }
        base.OnApplyTemplate(e);
        _sidebar = e.NameScope.Find<Sidebar>(SidebarPart);
        _versionMenu = e.NameScope.Find<DropdownMenu>(VersionMenuPart);
        _versionButton = e.NameScope.Find<Control>(VersionButtonPart);
        _versionText = e.NameScope.Find<TextBlock>("PART_VersionText");
        _productRun = e.NameScope.Find<Run>("PART_ProductRun");
        _docsRun = e.NameScope.Find<Run>("PART_DocsRun");
        if (_sidebar is not null)
        {
            _sidebar.ItemInvoked += OnItemInvoked;
            _sidebar.PropertyChanged += OnSidebarPropertyChanged;
        }
        if (_versionMenu is not null)
        {
            _versionMenu.ItemClicked += OnVersionClicked;
        }
        UpdateTexts();
        BuildVersions();
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        RevealCurrent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel = null;
        _revealAgain?.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentKeyProperty)
        {
            if (!_syncing && _sidebar is not null)
            {
                _syncing = true;
                _sidebar.SelectedKey = CurrentKey;
                _syncing = false;
            }
            RevealCurrent();
            var newKey = change.GetNewValue<string?>();
            RaiseEvent(new DocsSidebarSelectionChangedEventArgs(
                SelectionChangedEvent, change.GetOldValue<string?>(), newKey, newKey is null ? null : Find(newKey)));
        }
        else if (change.Property == SearchTextProperty)
        {
            if (_sidebar is not null && _sidebar.FilterText != SearchText)
            {
                _sidebar.FilterText = SearchText;
            }
        }
        else if (change.Property == SelectedVersionProperty)
        {
            UpdateVersion();
        }
        else if (change.Property == LabelsProperty || change.Property == ProductNameProperty)
        {
            UpdateTexts();
            UpdateVersion();
        }
    }

    private void OnSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _shown = Sections.Count > 0 ? Sections.ToArray() : SampleSections();
        // XAML adds sections one at a time; build once after the batch.
        if (!_rebuildPosted)
        {
            _rebuildPosted = true;
            Dispatcher.UIThread.Post(() =>
            {
                _rebuildPosted = false;
                Rebuild();
            }, DispatcherPriority.Send);
        }
    }

    #endregion

    #region Content

    private IEnumerable<DocsSidebarPage> AllPages()
    {
        foreach (var section in _shown)
        {
            foreach (var page in section.Pages)
            {
                yield return page;
                foreach (var child in page.Pages)
                {
                    yield return child;
                }
            }
        }
    }

    private void Rebuild()
    {
        if (_sidebar is null)
        {
            return;
        }
        foreach (var (section, built) in _sections)
        {
            section.PropertyChanged -= OnSectionPropertyChanged;
            built.PropertyChanged -= OnBuiltSectionPropertyChanged;
        }
        foreach (var binding in _pageBindings)
        {
            binding.Dispose();
        }
        _pageBindings.Clear();
        _items.Clear();
        _sections.Clear();
        _sidebar.Sections.Clear();
        foreach (var section in _shown)
        {
            var built = new SidebarSection { Header = section.Title, IsCollapsible = true, IsExpanded = section.IsExpanded };
            // Hiding a section (or a page) follows live, so an app can switch parts of the menu as its data changes.
            _pageBindings.Add(built.Bind(SidebarSection.IsVisibleProperty, section.GetObservable(DocsSidebarSection.IsVisibleProperty)));
            foreach (var page in section.Pages)
            {
                var item = Item(page);
                foreach (var child in page.Pages)
                {
                    item.Items.Add(Item(child));
                }
                built.Items.Add(item);
            }
            _sections[section] = built;
            section.PropertyChanged += OnSectionPropertyChanged;
            built.PropertyChanged += OnBuiltSectionPropertyChanged;
            _sidebar.Sections.Add(built);
        }
        _syncing = true;
        _sidebar.SelectedKey = CurrentKey;
        _sidebar.FilterText = SearchText;
        _syncing = false;
        RevealCurrent();
    }

    private SidebarItem Item(DocsSidebarPage page)
    {
        var item = new SidebarItem { Key = page.Key, Label = page.Title, Badge = page.Badge, Tag = page };
        _pageBindings.Add(item.Bind(SidebarItem.IsEnabledProperty, page.GetObservable(DocsSidebarPage.IsEnabledProperty)));
        _pageBindings.Add(item.Bind(SidebarItem.IsVisibleProperty, page.GetObservable(DocsSidebarPage.IsVisibleProperty)));
        _items[page] = item;
        return item;
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_productRun is not null)
        {
            _productRun.Text = ProductName;
        }
        if (_docsRun is not null)
        {
            _docsRun.Text = " " + labels.Docs;
        }
        if (_sidebar is not null)
        {
            _sidebar.FilterPlaceholder = labels.Search;
            _sidebar.NoMatchesText = labels.NoMatches;
            _sidebar.Label = labels.Navigation;
        }
    }

    private void BuildVersions()
    {
        if (_versionMenu is null)
        {
            return;
        }
        _versionMenu.Items.Clear();
        foreach (var version in _versions)
        {
            _versionMenu.Items.Add(new MenuEntry
            {
                Header = version.MenuText,
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "docs-version",
                Tag = version
            });
        }
        _versionMenu.IsVisible = _versions.Count > 0;
        UpdateVersion();
    }

    /// <summary>The chosen version: <see cref="SelectedVersion"/> when it is listed, otherwise the first.</summary>
    public DocsSidebarVersion? EffectiveVersion
        => _versions.FirstOrDefault(v => v.Name == SelectedVersion) ?? _versions.FirstOrDefault();

    private void UpdateVersion()
    {
        var current = EffectiveVersion;
        if (_versionText is not null)
        {
            _versionText.Text = current?.Name;
        }
        if (_versionButton is not null && current is not null)
        {
            AutomationProperties.SetName(_versionButton, Labels.VersionName(current.Name));
        }
        if (_versionMenu is not null)
        {
            foreach (var entry in _versionMenu.Items.OfType<MenuEntry>())
            {
                entry.IsChecked = ReferenceEquals(entry.Tag, current);
            }
        }
    }

    private Control? FindFilter()
        => _sidebar?.DocsSidebarFind<Control>(Sidebar.FilterPart);

    /// <summary>Scrolls the sidebar's list just enough to show the current row, with a little room around it.</summary>
    private void ScrollToCurrent()
    {
        if (_sidebar is null || CurrentPage is not { } page || !_items.TryGetValue(page, out var item)
            || _sidebar.RowOf(item) is not Control row || row.Bounds.Height <= 0)
        {
            return;
        }
        var margin = row.Bounds.Height * 1.5;
        row.BringIntoView(new Rect(0, -margin, row.Bounds.Width, row.Bounds.Height + 2 * margin));
    }

    #endregion

    #region Handlers

    private void OnItemInvoked(object? sender, SidebarItemInvokedEventArgs e)
    {
        if (e.Item.Tag is DocsSidebarPage page)
        {
            RaiseEvent(new DocsSidebarPageEventArgs(PageInvokedEvent, page));
        }
    }

    private void OnSidebarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_sidebar is null || _syncing)
        {
            return;
        }
        if (e.Property == Sidebar.SelectedKeyProperty && _sidebar.SelectedKey is { } key && key != CurrentKey)
        {
            _syncing = true;
            SetCurrentValue(CurrentKeyProperty, key);
            _syncing = false;
        }
        else if (e.Property == Sidebar.FilterTextProperty && _sidebar.FilterText != SearchText)
        {
            SetCurrentValue(SearchTextProperty, _sidebar.FilterText ?? string.Empty);
        }
    }

    private void OnSectionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DocsSidebarSection.IsExpandedProperty && sender is DocsSidebarSection section
            && _sections.TryGetValue(section, out var built) && built.IsExpanded != section.IsExpanded)
        {
            built.IsExpanded = section.IsExpanded;
        }
    }

    private void OnBuiltSectionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != SidebarSection.IsExpandedProperty || sender is not SidebarSection built)
        {
            return;
        }
        foreach (var (section, other) in _sections)
        {
            if (ReferenceEquals(other, built) && section.IsExpanded != built.IsExpanded)
            {
                section.SetCurrentValue(DocsSidebarSection.IsExpandedProperty, built.IsExpanded);
            }
        }
    }

    private void OnVersionClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is not DocsSidebarVersion version)
        {
            return;
        }
        var old = EffectiveVersion?.Name;
        SetCurrentValue(SelectedVersionProperty, version.Name);
        UpdateVersion();
        if (old != version.Name)
        {
            RaiseEvent(new DocsSidebarVersionChangedEventArgs(VersionChangedEvent, old, version.Name));
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsSearchShortcutEnabled || !IsEffectivelyVisible || e.Handled)
        {
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        var slash = e.Key == Key.Oem2 && e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox;
        if (slash || (command && e.Key == Key.K))
        {
            e.Handled = true;
            FocusSearch();
        }
    }

    #endregion
}

/// <summary>Visual tree helpers for <see cref="DocsSidebar"/>.</summary>
internal static class DocsSidebarVisuals
{
    /// <summary>The first visual descendant of type <typeparamref name="T"/> (with <paramref name="name"/>, when given).</summary>
    public static T? DocsSidebarFind<T>(this Visual root, string? name = null) where T : Visual
    {
        foreach (var visual in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
        {
            if (visual is T match && (name is null || (visual as Control)?.Name == name))
            {
                return match;
            }
        }
        return null;
    }
}
