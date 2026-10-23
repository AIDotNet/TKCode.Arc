using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The app's main navigation (an original TKCode design): a header for a logo or a workspace switcher, an optional filter
/// box, sections of pages with icons, counts and one level of nested pages, and a footer for the signed-in user. The
/// selected page carries a muted block behind it and an accent-colored icon; the block glides between rows on a snappy
/// spring. Parents open their pages with a height spring and a turning chevron. The sidebar collapses to a 64px icon rail
/// on a width spring: labels and counts fade, counts become dots, every page gets a tooltip on the right and a parent shows
/// its pages in a menu.
/// <para>
/// Keyboard (WAI-ARIA tree model): the list is one tab stop; ↑ / ↓, Home / End move, → opens a parent or enters it, ←
/// closes it or goes back to the parent, Enter / Space choose, typing jumps to a page by its first letters, and Cmd / Ctrl+B
/// collapses or expands the sidebar while focus is inside it.
/// </para>
/// </summary>
[TemplatePart(ListPart, typeof(SidebarListPanel))]
[TemplatePart(ContentPart, typeof(Panel))]
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(MarkerBlockPart, typeof(GlidingIndicator))]
[TemplatePart(FilterPart, typeof(SearchField))]
[TemplatePart(FilterButtonPart, typeof(SidebarIconButton))]
[TemplatePart(CollapseButtonPart, typeof(SidebarIconButton))]
[TemplatePart(FilterCollapseButtonPart, typeof(SidebarIconButton))]
[TemplatePart(HeaderRowPart, typeof(Control))]
[TemplatePart(NoMatchesPart, typeof(TextBlock))]
[TemplatePart(PaneHostPart, typeof(Panel))]
[PseudoClasses(CollapsedPseudoClass, FilteringPseudoClass, FilterToggleSidePseudoClass, OverlayPseudoClass)]
public class Sidebar : TemplatedControl
{
    public const string PaneHostPart = "PART_PaneHost";
    public const string ListPart = "PART_List";
    public const string ContentPart = "PART_Content";
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string MarkerBlockPart = "PART_MarkerBlock";
    public const string FilterPart = "PART_Filter";
    public const string FilterButtonPart = "PART_FilterButton";
    public const string CollapseButtonPart = "PART_CollapseButton";
    public const string FilterCollapseButtonPart = "PART_FilterCollapseButton";
    public const string HeaderRowPart = "PART_HeaderRow";
    public const string NoMatchesPart = "PART_NoMatches";

    /// <summary>Set while the sidebar is the icon rail (<see cref="IsCollapsed"/>).</summary>
    public const string CollapsedPseudoClass = ":collapsed";

    /// <summary>Set while the filter narrows the pages.</summary>
    public const string FilteringPseudoClass = ":filtering";

    /// <summary>Set while <see cref="ResizeMode"/> is <see cref="SidebarResizeMode.Overlay"/>.</summary>
    public const string OverlayPseudoClass = ":overlay-resize";

    /// <summary>Set while the collapse button sits beside the filter box (<see cref="CollapseButtonPlacement"/>) instead of in the header row.</summary>
    public const string FilterToggleSidePseudoClass = ":filter-toggle";

    #region Properties

    /// <summary>The selected page (two-way). A parent cannot be selected: choosing it opens and closes its pages.</summary>
    public static readonly StyledProperty<SidebarItem?> SelectedItemProperty =
        AvaloniaProperty.Register<Sidebar, SidebarItem?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceSelectedItem);

    /// <summary>The <see cref="SidebarItem.Key"/> of the selected page (two-way), kept in step with <see cref="SelectedItem"/>; bind it to your route.</summary>
    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<Sidebar, string?>(nameof(SelectedKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the sidebar is the 64px icon rail (two-way). The width springs between the two.</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<Sidebar, bool>(nameof(IsCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Content at the top (a logo and title, a workspace switcher), beside the collapse button when that sits in the header row. It is hidden on the rail while the collapse button shows.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<Sidebar, object?>(nameof(Header));

    /// <summary>Content at the bottom (the signed-in user, a storage meter), under a thin separator. It keeps its open-width layout on the rail, clipped to the rail; use the <c>:collapsed</c> pseudo-class to adapt it.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Sidebar, object?>(nameof(Footer));

    /// <summary>Shows the filter box under the header (it becomes a search button on the rail).</summary>
    public static readonly StyledProperty<bool> IsFilterVisibleProperty =
        AvaloniaProperty.Register<Sidebar, bool>(nameof(IsFilterVisible));

    /// <summary>The filter text (two-way). Pages whose label contains it stay; a parent with a matching page opens.</summary>
    public static readonly StyledProperty<string> FilterTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(FilterText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Placeholder and accessible name of the filter box.</summary>
    public static readonly StyledProperty<string> FilterPlaceholderProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(FilterPlaceholder), "Filter");

    /// <summary>Text shown when the filter matches no page.</summary>
    public static readonly StyledProperty<string> NoMatchesTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(NoMatchesText), "No matches");

    /// <summary>Shows the collapse / expand button (PanelLeft icon). Its place is <see cref="CollapseButtonPlacement"/>.</summary>
    public static readonly StyledProperty<bool> IsCollapseButtonVisibleProperty =
        AvaloniaProperty.Register<Sidebar, bool>(nameof(IsCollapseButtonVisible), true);

    /// <summary>Where the collapse button sits while the sidebar is open: in the header row (default) or beside the filter box. On the rail it always sits at the top.</summary>
    public static readonly StyledProperty<SidebarCollapseButtonPlacement> CollapseButtonPlacementProperty =
        AvaloniaProperty.Register<Sidebar, SidebarCollapseButtonPlacement>(nameof(CollapseButtonPlacement));

    /// <summary>Accessible name and tooltip of the collapse button while the sidebar is open.</summary>
    public static readonly StyledProperty<string> CollapseButtonTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(CollapseButtonText), "Collapse sidebar");

    /// <summary>Accessible name and tooltip of the collapse button while the sidebar is the rail.</summary>
    public static readonly StyledProperty<string> ExpandButtonTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(ExpandButtonText), "Expand sidebar");

    /// <summary>How the width change between the open sidebar and the rail reaches the page next to it: <see cref="SidebarResizeMode.Overlay"/> (default) or <see cref="SidebarResizeMode.Push"/>.</summary>
    public static readonly StyledProperty<SidebarResizeMode> ResizeModeProperty =
        AvaloniaProperty.Register<Sidebar, SidebarResizeMode>(nameof(ResizeMode), SidebarResizeMode.Overlay);

    /// <summary>Added to the accessible name of a page that has <see cref="SidebarItem.IsUnread"/> ("Inbox, unread").</summary>
    public static readonly StyledProperty<string> UnreadTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(UnreadText), "unread");

    /// <summary>Name of the navigation landmark.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(Label), "Sidebar");

    /// <summary>Runs when a page is chosen; the parameter is the <see cref="SidebarItem"/>.</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<Sidebar, ICommand?>(nameof(SelectCommand));

    /// <summary>Raised when a page is chosen (click, Enter or Space, or a page in a rail menu), also when it already was the selected page.</summary>
    public static readonly RoutedEvent<SidebarItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<Sidebar, SidebarItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Width of the open sidebar (from the theme): rows keep this layout while the width springs to the rail.</summary>
    internal static readonly StyledProperty<double> OpenWidthProperty =
        AvaloniaProperty.Register<Sidebar, double>(nameof(OpenWidth), double.NaN);

    /// <inheritdoc cref="SelectedItemProperty"/>
    public SidebarItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="SelectedKeyProperty"/>
    public string? SelectedKey
    {
        get => GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    /// <inheritdoc cref="IsCollapsedProperty"/>
    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc cref="IsFilterVisibleProperty"/>
    public bool IsFilterVisible
    {
        get => GetValue(IsFilterVisibleProperty);
        set => SetValue(IsFilterVisibleProperty, value);
    }

    /// <inheritdoc cref="FilterTextProperty"/>
    public string FilterText
    {
        get => GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    /// <inheritdoc cref="FilterPlaceholderProperty"/>
    public string FilterPlaceholder
    {
        get => GetValue(FilterPlaceholderProperty);
        set => SetValue(FilterPlaceholderProperty, value);
    }

    /// <inheritdoc cref="NoMatchesTextProperty"/>
    public string NoMatchesText
    {
        get => GetValue(NoMatchesTextProperty);
        set => SetValue(NoMatchesTextProperty, value);
    }

    /// <inheritdoc cref="IsCollapseButtonVisibleProperty"/>
    public bool IsCollapseButtonVisible
    {
        get => GetValue(IsCollapseButtonVisibleProperty);
        set => SetValue(IsCollapseButtonVisibleProperty, value);
    }

    /// <inheritdoc cref="CollapseButtonPlacementProperty"/>
    public SidebarCollapseButtonPlacement CollapseButtonPlacement
    {
        get => GetValue(CollapseButtonPlacementProperty);
        set => SetValue(CollapseButtonPlacementProperty, value);
    }

    /// <inheritdoc cref="CollapseButtonTextProperty"/>
    public string CollapseButtonText
    {
        get => GetValue(CollapseButtonTextProperty);
        set => SetValue(CollapseButtonTextProperty, value);
    }

    /// <inheritdoc cref="ExpandButtonTextProperty"/>
    public string ExpandButtonText
    {
        get => GetValue(ExpandButtonTextProperty);
        set => SetValue(ExpandButtonTextProperty, value);
    }

    /// <inheritdoc cref="ResizeModeProperty"/>
    public SidebarResizeMode ResizeMode
    {
        get => GetValue(ResizeModeProperty);
        set => SetValue(ResizeModeProperty, value);
    }

    /// <summary>
    /// A transform that slides the page along with an <see cref="SidebarResizeMode.Overlay"/> sidebar: bind the
    /// <c>RenderTransform</c> of the page next to the sidebar to it (<c>RenderTransform="{Binding #Nav.PageShift}"</c>).
    /// It is the distance between where the page is laid out (right after the sidebar's new width) and where the pane's edge
    /// is while it springs, so the page glides instead of jumping. It stays at 0 in <see cref="SidebarResizeMode.Push"/>.
    /// </summary>
    public ITransform PageShift => _pageShift;

    /// <inheritdoc cref="UnreadTextProperty"/>
    public string UnreadText
    {
        get => GetValue(UnreadTextProperty);
        set => SetValue(UnreadTextProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="SelectCommandProperty"/>
    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    internal double OpenWidth
    {
        get => GetValue(OpenWidthProperty);
        set => SetValue(OpenWidthProperty, value);
    }

    /// <summary>The sections of pages, in order (the XAML content).</summary>
    [Content]
    public AvaloniaList<SidebarSection> Sections { get; } = new();

    /// <summary>Raised when a page is chosen (click, Enter or Space, or a page in a rail menu), also when it already was the selected page.</summary>
    public event EventHandler<SidebarItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    #endregion

    private readonly List<SidebarNode> _sections = new();
    private readonly Dictionary<SidebarItem, SidebarNode> _byItem = new();
    private readonly Dictionary<SidebarSection, SidebarNode> _bySection = new();
    private readonly List<SidebarNode> _navigable = new();
    private readonly Dictionary<object, object> _owners = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<SidebarSection> _dirtySections = new();
    private readonly HashSet<SidebarItem> _dirtyParents = new();
    private readonly ListKeyboardNavigator _navigator;
    private SidebarListPanel? _list;
    private Panel? _content;
    private ScrollViewer? _scroll;
    private GlidingIndicator? _markerBlock;
    private SearchField? _filter;
    private SidebarIconButton? _filterButton;
    private SidebarIconButton? _collapseButton;
    private SidebarIconButton? _filterCollapseButton;
    private Control? _headerRow;
    private TextBlock? _noMatches;
    private MenuSurface? _flyout;
    private SidebarNode? _flyoutNode;
    private SidebarNode? _focusNode;
    private readonly TranslateTransform _pageShift = new();
    private MotionValue<double>? _paneWidth;
    private Panel? _paneHost;
    private SidebarNode? _selectedNode;
    private SidebarNode? _currentNode;
    private Rect _markerTarget;
    private bool _rebuildPosted;
    private bool _dirtyAll;
    private bool _rebuildAll;
    private bool _syncingKey;

    static Sidebar()
    {
        FocusableProperty.OverrideDefaultValue<Sidebar>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<Sidebar>(AutomationLandmarkType.Navigation);
    }

    public Sidebar()
    {
        _navigator = new ListKeyboardNavigator(() => _navigable.Count, i => _navigable[i].IsEnabled, i => _navigable[i].Label)
        {
            Wrap = false
        };
        Sections.CollectionChanged += OnSectionsChanged;
        // The default mode raises no property change, so its pseudo-class is set here.
        PseudoClasses.Set(OverlayPseudoClass, ResizeMode == SidebarResizeMode.Overlay);
        AutomationProperties.SetName(this, Label);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Collapses the sidebar to the icon rail, or opens it from the rail.</summary>
    public void ToggleCollapsed() => SetCurrentValue(IsCollapsedProperty, !IsCollapsed);

    /// <summary>
    /// The width the pane shows right now. It is the layout <see cref="Visual.Bounds"/> width, except while an
    /// <see cref="SidebarResizeMode.Overlay"/> sidebar springs: the layout width has already jumped and the pane catches up.
    /// </summary>
    internal double PaneWidth => ResizeMode == SidebarResizeMode.Overlay && _paneWidth is { } motion ? motion.Value : Width;

    /// <summary>How far the layout width falls short of the open width (what the rows are measured and arranged with).</summary>
    internal double MeasureDeficit
    {
        get
        {
            var open = OpenWidth;
            var width = Width;
            if (double.IsNaN(open) || double.IsNaN(width) || open <= 0)
            {
                return 0;
            }
            return Math.Max(0, open - width);
        }
    }

    /// <summary>The rows the arrow keys move through, in order (tests).</summary>
    internal IReadOnlyList<SidebarNode> NavigableNodes => _navigable;

    /// <summary>The row of <paramref name="item"/> (null until the sidebar is built).</summary>
    internal SidebarRow? RowOf(SidebarItem item) => _byItem.TryGetValue(item, out var node) ? node.Row : null;

    /// <summary>The heading control of <paramref name="section"/>, if it has one.</summary>
    internal SidebarSectionHeader? HeaderOf(SidebarSection section)
        => _bySection.TryGetValue(section, out var node) && node.HasHeader ? node.Header : null;

    /// <summary>The row the selection marker rests on.</summary>
    internal SidebarRow? MarkerRow => MarkerNode()?.Row;

    /// <summary>The marker's target rectangle in list coordinates.</summary>
    internal Rect MarkerTarget => _markerTarget;

    internal GlidingIndicator? MarkerBlock => _markerBlock;

    /// <summary>The rail menu of a parent (null until first opened).</summary>
    internal MenuSurface? Flyout => _flyout;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_list is not null)
        {
            _list.Arranged = null;
            _list.PropertyChanged -= OnListPropertyChanged;
        }
        if (_collapseButton is not null)
        {
            _collapseButton.Click -= OnCollapseButtonClick;
        }
        if (_filterCollapseButton is not null)
        {
            _filterCollapseButton.Click -= OnCollapseButtonClick;
        }
        if (_filterButton is not null)
        {
            _filterButton.Click -= OnFilterButtonClick;
        }
        if (_filter is not null)
        {
            _filter.SearchSubmitted -= OnFilterSubmitted;
            _filter.RemoveHandler(KeyDownEvent, OnFilterKeyDown);
        }
        base.OnApplyTemplate(e);
        _paneHost = e.NameScope.Find<Panel>(PaneHostPart);
        _list = e.NameScope.Find<SidebarListPanel>(ListPart);
        _content = e.NameScope.Find<Panel>(ContentPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _markerBlock = e.NameScope.Find<GlidingIndicator>(MarkerBlockPart);
        _filter = e.NameScope.Find<SearchField>(FilterPart);
        _filterButton = e.NameScope.Find<SidebarIconButton>(FilterButtonPart);
        _collapseButton = e.NameScope.Find<SidebarIconButton>(CollapseButtonPart);
        _filterCollapseButton = e.NameScope.Find<SidebarIconButton>(FilterCollapseButtonPart);
        _headerRow = e.NameScope.Find<Control>(HeaderRowPart);
        _noMatches = e.NameScope.Find<TextBlock>(NoMatchesPart);
        _markerTarget = default;
        if (_list is not null)
        {
            _list.Arranged = OnListArranged;
            _list.PropertyChanged += OnListPropertyChanged;
        }
        if (_collapseButton is not null)
        {
            _collapseButton.Click += OnCollapseButtonClick;
        }
        if (_filterCollapseButton is not null)
        {
            _filterCollapseButton.Click += OnCollapseButtonClick;
        }
        if (_filterButton is not null)
        {
            _filterButton.Click += OnFilterButtonClick;
        }
        if (_filter is not null)
        {
            _filter.SearchSubmitted += OnFilterSubmitted;
            _filter.AddHandler(KeyDownEvent, OnFilterKeyDown, RoutingStrategies.Tunnel);
        }
        UpdateChrome();
        // A new template builds every row again.
        _dirtyAll = true;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_content is not null && _list is not null && (_dirtyAll || (_sections.Count == 0 && Sections.Count > 0)))
        {
            // Back on screen after a detach: the rows were released, build them again.
            _dirtyAll = true;
            Rebuild();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Off screen the sidebar holds no row and listens to no item: the items and sections it shows (often shared or
        // static) must not keep it alive. The rows are built again when it returns.
        if (_content is not null)
        {
            _flyout?.Close(returnFocus: false);
            DiscardAll();
            _rebuildPosted = false;
            _dirtyAll = false;
            _dirtySections.Clear();
            _dirtyParents.Clear();
            _selectedNode = null;
            _currentNode = null;
            _focusNode = null;
            _navigable.Clear();
            _markerTarget = default;
        }
    }

    private void OnListPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SidebarListPanel.RowGapProperty || e.Property == SidebarListPanel.ChildrenMarginProperty ||
            e.Property == SidebarListPanel.GuideMarginProperty)
        {
            // The containers read these when they are built.
            _dirtyAll = true;
            _rebuildAll = true;
            InvalidateStructure();
        }
    }

    /// <summary>True when the collapse button sits beside the filter box (the filter must show for that).</summary>
    private bool ToggleBesideFilter => CollapseButtonPlacement == SidebarCollapseButtonPlacement.Filter && IsFilterVisible && IsCollapseButtonVisible;

    /// <summary>The header row, the collapse buttons and the filter follow the rail and the visibility switches.</summary>
    private void UpdateChrome()
    {
        var collapsed = IsCollapsed;
        var beside = ToggleBesideFilter;
        PseudoClasses.Set(FilterToggleSidePseudoClass, beside);
        // The header row's button shows while open (header placement) and always on the rail; the filter row's only while open.
        var headerButton = IsCollapseButtonVisible && (!beside || collapsed);
        var filterButton = beside && !collapsed;
        var text = collapsed ? ExpandButtonText : CollapseButtonText;
        ConfigureCollapseButton(_collapseButton, text, collapsed, headerButton);
        ConfigureCollapseButton(_filterCollapseButton, text, collapsed, filterButton);
        // The filter row keeps the button's room while the sidebar is open, so the box does not resize when it fades on the rail.
        ConfigureVisibility(_collapseButton, headerButton);
        ConfigureVisibility(_filterCollapseButton, beside);
        if (_headerRow is not null)
        {
            _headerRow.IsVisible = Header is not null || headerButton;
        }
        if (_filter is not null)
        {
            // On the rail the filter gives way to a search button in the same place (nothing below it moves).
            _filter.Focusable = !collapsed;
        }
        if (_filterButton is not null)
        {
            _filterButton.Focusable = collapsed;
            AutomationProperties.SetName(_filterButton, FilterPlaceholder);
            Tooltip.SetTip(_filterButton, collapsed ? FilterPlaceholder : null);
            Tooltip.SetSide(_filterButton, TooltipSide.Right);
        }
    }

    private static void ConfigureCollapseButton(SidebarIconButton? button, string text, bool collapsed, bool shown)
    {
        if (button is null)
        {
            return;
        }
        AutomationProperties.SetName(button, text);
        Tooltip.SetTip(button, shown ? text : null);
        Tooltip.SetSide(button, collapsed ? TooltipSide.Right : TooltipSide.Bottom);
        // Hidden buttons fade out in the theme; they must not take focus either.
        button.Focusable = shown;
    }

    private static void ConfigureVisibility(SidebarIconButton? button, bool visible)
    {
        if (button is not null)
        {
            button.IsVisible = visible;
        }
    }

    #endregion

    #region Property changes

    private static SidebarItem? CoerceSelectedItem(AvaloniaObject sender, SidebarItem? value)
    {
        // A parent only opens and closes its pages and an action only acts; neither is ever the selected page.
        if (value is not null && sender is Sidebar sidebar && (value.IsAction || sidebar.IsParent(value)))
        {
            return sidebar.GetValue(SelectedItemProperty);
        }
        return value;
    }

    private bool IsParent(SidebarItem item)
    {
        if (!item.HasChildren)
        {
            return false;
        }
        foreach (var section in Sections)
        {
            if (section.Items.Contains(item))
            {
                return true;
            }
        }
        return false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedItemProperty)
        {
            var item = change.GetNewValue<SidebarItem?>();
            if (!_syncingKey)
            {
                _syncingKey = true;
                try
                {
                    SetCurrentValue(SelectedKeyProperty, item?.Key);
                }
                finally
                {
                    _syncingKey = false;
                }
            }
            UpdateSelectionVisuals();
            UpdateMarker(animate: true);
            UpdateTabStops();
            // A selection made elsewhere (a route change) scrolls its row into view.
            if (MarkerNode()?.Row is { } row)
            {
                Reveal(row);
            }
        }
        else if (change.Property == SelectedKeyProperty)
        {
            if (!_syncingKey)
            {
                _syncingKey = true;
                try
                {
                    SetCurrentValue(SelectedItemProperty, FindByKey(change.GetNewValue<string?>()));
                }
                finally
                {
                    _syncingKey = false;
                }
            }
        }
        else if (change.Property == IsCollapsedProperty)
        {
            OnCollapsedChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == WidthProperty)
        {
            OnLayoutWidthChanged(change.GetNewValue<double>());
        }
        else if (change.Property == ResizeModeProperty)
        {
            PseudoClasses.Set(OverlayPseudoClass, change.GetNewValue<SidebarResizeMode>() == SidebarResizeMode.Overlay);
            OnLayoutWidthChanged(Width, jump: true);
        }
        else if (change.Property == FilterTextProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == HeaderProperty || change.Property == IsCollapseButtonVisibleProperty ||
                 change.Property == CollapseButtonTextProperty || change.Property == ExpandButtonTextProperty ||
                 change.Property == FilterPlaceholderProperty || change.Property == CollapseButtonPlacementProperty ||
                 change.Property == IsFilterVisibleProperty)
        {
            UpdateChrome();
        }
        else if (change.Property == UnreadTextProperty)
        {
            foreach (var node in _byItem.Values)
            {
                UpdateRowContent(node);
            }
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string>());
        }
    }

    #region Pane width (overlay)

    /// <summary>
    /// In overlay mode the layout width has jumped to its new value; the pane springs there on its own. This is code
    /// rather than a theme transition because the visual width must move while the layout width stays still: a transition
    /// on <c>Width</c> would lay out the whole page next to the sidebar on every frame.
    /// </summary>
    private void OnLayoutWidthChanged(double width, bool jump = false)
    {
        if (ResizeMode != SidebarResizeMode.Overlay)
        {
            if (_paneWidth is not null)
            {
                // Back to push: the pane is the layout width again.
                _paneWidth.Jump(double.IsNaN(width) ? 0 : width);
                SetPageShift(0);
                _paneHost?.InvalidateArrange();
            }
            return;
        }
        if (double.IsNaN(width))
        {
            return;
        }
        if (_paneWidth is null)
        {
            _paneWidth = new MotionValue<double>(this, width, MotionVectors.Double);
            _paneWidth.Changed += OnPaneWidthTick;
        }
        else if (jump)
        {
            _paneWidth.Jump(width);
        }
        else
        {
            // Off and Reduced jump, a sidebar that is not on screen yet jumps too (MotionValue decides).
            _ = _paneWidth.AnimateTo(width, MotionSpec.Spring(ArcSpring.Smooth));
        }
        OnPaneWidthTick(_paneWidth.Value);
    }

    private void OnPaneWidthTick(double value)
    {
        // Arrange only: the layout width (and with it the page) is not invalidated.
        _paneHost?.InvalidateArrange();
        SetPageShift(ResizeMode == SidebarResizeMode.Overlay && !double.IsNaN(Width) ? value - Width : 0);
    }

    private void SetPageShift(double x)
    {
        if (Math.Abs(_pageShift.X - x) > 0.01)
        {
            _pageShift.X = x;
        }
    }

    #endregion

    private void OnCollapsedChanged(bool collapsed)
    {
        PseudoClasses.Set(CollapsedPseudoClass, collapsed);
        if (!collapsed)
        {
            _flyout?.Close(returnFocus: false);
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var focusInside = focused is not null && this.IsVisualAncestorOf(focused);
        UpdateChrome();
        Refresh(animate: true);
        if (!focusInside || focused is null)
        {
            return;
        }
        // Focus that sat on something the rail folds away (the filter, a child page) moves to the list's tab stop.
        var node = NodeFromSource(focused);
        var lost = ReferenceEquals(focused, _filter) || (_filter is not null && _filter.IsVisualAncestorOf(focused)) ||
                   (node is not null && !_navigable.Contains(node));
        if (lost)
        {
            var target = node?.Parent is { } parent && _navigable.Contains(parent) ? parent : _focusNode;
            if (target is not null)
            {
                MoveFocus(target, NavigationMethod.Directional);
            }
        }
    }

    private void OnCollapseButtonClick(object? sender, RoutedEventArgs e) => ToggleCollapsed();

    /// <summary>The rail's search button opens the sidebar and puts the caret in the filter.</summary>
    private void OnFilterButtonClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsCollapsedProperty, false);
        _filter?.Focus(NavigationMethod.Pointer);
    }

    #endregion

    #region Structure

    private void OnSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dirtyAll = true;
        InvalidateStructure();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is not null && _owners.TryGetValue(sender, out var owner))
        {
            if (owner is SidebarSection section)
            {
                _dirtySections.Add(section);
            }
            else if (owner is SidebarItem item)
            {
                _dirtyParents.Add(item);
            }
            InvalidateStructure();
        }
    }

    /// <summary>Sections or item collections changed: bring the rows in step once, after the current batch of changes.</summary>
    private void InvalidateStructure()
    {
        if (_content is null || _rebuildPosted)
        {
            return;
        }
        _rebuildPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildPosted)
            {
                Rebuild();
            }
        }, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Brings the rows in step with the sections and their items. Only what changed is touched: rows of pages that stay
    /// keep their controls (and their focus and selection state), new pages get new rows, gone pages lose theirs.
    /// </summary>
    private void Rebuild()
    {
        _rebuildPosted = false;
        if (_content is null || _list is null || TopLevel.GetTopLevel(this) is null)
        {
            // Off screen (or not templated yet) nothing is built; attaching builds everything that is pending.
            _dirtyAll = true;
            return;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var focusedNode = focused is not null ? NodeFromSource(focused) : null;
        var tabItem = _focusNode?.Item;
        var tabSection = _focusNode is { IsHeading: true } ? _focusNode.Section : null;

        if (_rebuildAll)
        {
            DiscardAll();
            _rebuildAll = false;
            _dirtyAll = true;
        }

        // First take away what is gone (a control icon lives in one row only, so a moved page must let go of it first)...
        foreach (var item in _dirtyParents)
        {
            if (_byItem.TryGetValue(item, out var parent))
            {
                RemoveStaleChildren(parent);
            }
        }
        if (_dirtyAll)
        {
            RemoveStaleSections();
        }
        foreach (var section in _dirtySections)
        {
            if (_bySection.TryGetValue(section, out var node))
            {
                RemoveStaleItems(node);
            }
        }
        // ... then add what is new and put everything in order.
        if (_dirtyAll)
        {
            AddMissingSections();
        }
        foreach (var section in _dirtySections)
        {
            if (_bySection.TryGetValue(section, out var node))
            {
                AddMissingItems(node);
            }
        }
        foreach (var item in _dirtyParents)
        {
            if (_byItem.TryGetValue(item, out var parent))
            {
                AddMissingChildren(parent);
            }
        }
        _dirtyAll = false;
        _dirtySections.Clear();
        _dirtyParents.Clear();

        if (SelectedItem is null && SelectedKey is { } key && FindByKey(key) is { } match)
        {
            _syncingKey = true;
            try
            {
                SetCurrentValue(SelectedItemProperty, match);
            }
            finally
            {
                _syncingKey = false;
            }
        }

        // The tab stop stays on the same page or heading when it still exists.
        _focusNode = null;
        if (tabItem is not null && _byItem.TryGetValue(tabItem, out var tabNode))
        {
            _focusNode = tabNode;
        }
        else if (tabSection is not null && _bySection.TryGetValue(tabSection, out var tabHeading))
        {
            _focusNode = tabHeading;
        }
        Refresh(animate: false);
        if (focusedNode is not null && focused is not null && !this.IsVisualAncestorOf(focused))
        {
            // The focused row went away: focus moves to the list's tab stop.
            if (_focusNode is not null && _navigable.Contains(_focusNode))
            {
                MoveFocus(_focusNode, NavigationMethod.Unspecified);
            }
        }
    }

    private void DiscardAll()
    {
        foreach (var sectionNode in _sections.ToArray())
        {
            RemoveSection(sectionNode);
        }
        _content!.Children.Clear();
    }

    private void RemoveStaleSections()
    {
        var current = new HashSet<SidebarSection>(Sections);
        foreach (var sectionNode in _sections.ToArray())
        {
            if (!current.Contains(sectionNode.Section))
            {
                RemoveSection(sectionNode);
            }
        }
    }

    private void RemoveSection(SidebarNode sectionNode)
    {
        foreach (var node in sectionNode.SectionItems.ToArray())
        {
            RemoveItem(node);
        }
        var section = sectionNode.Section;
        section.PropertyChanged -= OnSectionPropertyChanged;
        section.Items.CollectionChanged -= OnItemsChanged;
        _owners.Remove(section.Items);
        _bySection.Remove(section);
        _sections.Remove(sectionNode);
        if (sectionNode.SectionRoot is { } root)
        {
            _content?.Children.Remove(root);
        }
        if (ReferenceEquals(_focusNode, sectionNode))
        {
            _focusNode = null;
        }
    }

    /// <summary>Removes a page row (and the rows of its children) from the structure and its container.</summary>
    private void RemoveItem(SidebarNode node)
    {
        foreach (var child in node.Children.ToArray())
        {
            RemoveItem(child);
        }
        var item = node.Item!;
        item.PropertyChanged -= OnItemPropertyChanged;
        if (node.Parent is null)
        {
            item.Items.CollectionChanged -= OnItemsChanged;
            _owners.Remove(item.Items);
        }
        if (node.Row is { } row)
        {
            // A control icon can live in one row only.
            row.IconContent = null;
            row.TrailingContent = null;
        }
        _byItem.Remove(item);
        if (node.Parent is { } parent)
        {
            parent.Children.Remove(node);
            parent.ChildrenRows?.Children.Remove(node.Root!);
        }
        else if (_bySection.TryGetValue(node.Section, out var sectionNode))
        {
            sectionNode.SectionItems.Remove(node);
            sectionNode.SectionStack?.Children.Remove(node.Root!);
        }
        if (ReferenceEquals(node, _flyoutNode))
        {
            _flyout?.Close(returnFocus: false);
        }
        if (ReferenceEquals(node, _focusNode))
        {
            _focusNode = null;
        }
    }

    /// <summary>A parent whose pages are gone (or a page that gained pages) needs a row of the other kind.</summary>
    private static bool IsRightKind(SidebarNode node) => (node.ChildrenHost is not null) == node.Item!.HasChildren;

    private void RemoveStaleChildren(SidebarNode parent)
    {
        if (!IsRightKind(parent))
        {
            // The row is rebuilt in its section.
            var section = parent.Section;
            RemoveItem(parent);
            _dirtySections.Add(section);
            return;
        }
        var current = new HashSet<SidebarItem>(parent.Item!.Items);
        foreach (var child in parent.Children.ToArray())
        {
            if (!current.Contains(child.Item!))
            {
                RemoveItem(child);
            }
        }
    }

    private void RemoveStaleItems(SidebarNode sectionNode)
    {
        var current = new HashSet<SidebarItem>(sectionNode.Section.Items);
        foreach (var node in sectionNode.SectionItems.ToArray())
        {
            if (!current.Contains(node.Item!) || !IsRightKind(node))
            {
                RemoveItem(node);
            }
        }
    }

    private void AddMissingSections()
    {
        var order = new List<SidebarNode>(Sections.Count);
        foreach (var section in Sections)
        {
            if (_bySection.TryGetValue(section, out var existing))
            {
                order.Add(existing);
                continue;
            }
            var node = BuildSection(section);
            order.Add(node);
        }
        var inOrder = order.Count == _content!.Children.Count;
        for (var i = 0; inOrder && i < order.Count; i++)
        {
            inOrder = ReferenceEquals(order[i].SectionRoot, _content.Children[i]);
        }
        if (!inOrder)
        {
            _content.Children.Clear();
            foreach (var node in order)
            {
                _content.Children.Add(node.SectionRoot!);
            }
        }
        _sections.Clear();
        _sections.AddRange(order);
        for (var i = 0; i < order.Count; i++)
        {
            order[i].Index = i;
        }
    }

    private SidebarNode BuildSection(SidebarSection section)
    {
        var sectionNode = new SidebarNode(section, null, null);
        var root = new StackPanel();
        // Every section has a heading control (the space above it); the first untitled one stays hidden (see Refresh).
        var header = new SidebarSectionHeader(this, sectionNode);
        sectionNode.Header = header;
        root.Children.Add(header);
        var body = new StackPanel { Spacing = _list!.RowGap };
        var bodyHost = new SidebarHeightSpring { Child = body };
        sectionNode.SectionStack = body;
        sectionNode.SectionBody = bodyHost;
        sectionNode.SectionRoot = root;
        root.Children.Add(bodyHost);
        _bySection[section] = sectionNode;
        section.PropertyChanged += OnSectionPropertyChanged;
        section.Items.CollectionChanged += OnItemsChanged;
        _owners[section.Items] = section;
        AddMissingItems(sectionNode);
        return sectionNode;
    }

    private void AddMissingItems(SidebarNode sectionNode)
    {
        var section = sectionNode.Section;
        var stack = sectionNode.SectionStack!;
        var order = new List<SidebarNode>(section.Items.Count);
        foreach (var item in section.Items)
        {
            if (_byItem.TryGetValue(item, out var existing))
            {
                if (ReferenceEquals(existing.Section, section) && existing.Parent is null)
                {
                    order.Add(existing);
                }
                // A page listed twice keeps its first row.
                continue;
            }
            order.Add(BuildItem(section, item, null));
        }
        if (!SameOrder(order, stack))
        {
            stack.Children.Clear();
            foreach (var node in order)
            {
                stack.Children.Add(node.Root!);
            }
        }
        sectionNode.SectionItems.Clear();
        sectionNode.SectionItems.AddRange(order);
    }

    private void AddMissingChildren(SidebarNode parent)
    {
        if (parent.ChildrenRows is not { } stack)
        {
            return;
        }
        var order = new List<SidebarNode>(parent.Item!.Items.Count);
        foreach (var child in parent.Item.Items)
        {
            if (_byItem.TryGetValue(child, out var existing))
            {
                if (ReferenceEquals(existing.Parent, parent))
                {
                    order.Add(existing);
                }
                continue;
            }
            order.Add(BuildItem(parent.Section, child, parent));
        }
        if (!SameOrder(order, stack))
        {
            stack.Children.Clear();
            foreach (var node in order)
            {
                stack.Children.Add(node.Root!);
            }
        }
        parent.Children.Clear();
        parent.Children.AddRange(order);
    }

    private static bool SameOrder(List<SidebarNode> order, Panel stack)
    {
        if (order.Count != stack.Children.Count)
        {
            return false;
        }
        for (var i = 0; i < order.Count; i++)
        {
            if (!ReferenceEquals(order[i].Root, stack.Children[i]))
            {
                return false;
            }
        }
        return true;
    }

    private SidebarNode BuildItem(SidebarSection section, SidebarItem item, SidebarNode? parent)
    {
        var node = new SidebarNode(section, item, parent);
        _byItem[item] = node;
        item.PropertyChanged += OnItemPropertyChanged;
        var row = new SidebarRow(this, node);
        Tooltip.SetSide(row, TooltipSide.Right);
        node.Row = row;
        UpdateRowContent(node);
        if (parent is null && item.HasChildren)
        {
            row.IsParent = true;
            item.Items.CollectionChanged += OnItemsChanged;
            _owners[item.Items] = item;
            var stack = new StackPanel { Spacing = _list!.RowGap, Margin = _list.ChildrenMargin };
            node.ChildrenRows = stack;
            var guide = new Border
            {
                Width = 1,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Margin = _list.GuideMargin,
                IsHitTestVisible = false
            };
            guide.Bind(Border.BackgroundProperty, _list.GetObservable(SidebarListPanel.GuideBrushProperty));
            var panel = new Panel { Margin = new Thickness(0, _list.RowGap, 0, 0) };
            panel.Children.Add(guide);
            panel.Children.Add(stack);
            node.ChildrenStack = panel;
            node.ChildrenHost = new SidebarHeightSpring { Child = panel };
            node.Root = new StackPanel { Children = { row, node.ChildrenHost } };
            AddMissingChildren(node);
        }
        else
        {
            if (parent is null)
            {
                // A page without pages still reports when pages are added (it becomes a parent).
                item.Items.CollectionChanged += OnItemsChanged;
                _owners[item.Items] = item;
            }
            node.Root = row;
        }
        return node;
    }

    private void UpdateRowContent(SidebarNode node)
    {
        if (node is not { Item: { } item, Row: { } row })
        {
            return;
        }
        row.Label = item.Label;
        switch (item.Icon)
        {
            case Geometry geometry:
                row.IconContent = null;
                row.Glyph = geometry;
                break;
            default:
                row.Glyph = null;
                row.IconContent = item.Icon;
                break;
        }
        switch (item.TrailingIcon)
        {
            case Geometry geometry:
                row.TrailingContent = null;
                row.TrailingGlyph = geometry;
                break;
            default:
                row.TrailingGlyph = null;
                row.TrailingContent = item.TrailingIcon;
                break;
        }
        row.Monogram = string.IsNullOrEmpty(item.Label) ? null : char.ToUpperInvariant(item.Label[0]).ToString();
        row.Badge = string.IsNullOrEmpty(item.Badge) ? null : item.Badge;
        row.IsUnread = item.IsUnread;
        row.IsAction = item.IsAction;
        row.IsEnabled = item.IsEnabled;
        var name = item.Label;
        if (row.Badge is not null)
        {
            name = $"{name}, {row.Badge}";
        }
        if (item.IsUnread)
        {
            name = $"{name}, {UnreadText}";
        }
        AutomationProperties.SetName(row, name);
    }

    private SidebarItem? FindByKey(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var section in Sections)
        {
            foreach (var item in section.Items)
            {
                if (string.Equals(item.Key, key, StringComparison.Ordinal) && !item.HasChildren && !item.IsAction)
                {
                    return item;
                }
                foreach (var child in item.Items)
                {
                    if (string.Equals(child.Key, key, StringComparison.Ordinal) && !child.IsAction)
                    {
                        return child;
                    }
                }
            }
        }
        return null;
    }

    private void OnSectionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SidebarSection.HeaderProperty || e.Property == SidebarSection.IsCollapsibleProperty ||
            e.Property == SidebarSection.IsExpandedProperty)
        {
            Refresh(animate: true);
        }
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not SidebarItem item)
        {
            return;
        }
        if (e.Property == SidebarItem.IsExpandedProperty)
        {
            Refresh(animate: true);
        }
        else if (e.Property == SidebarItem.LabelProperty || e.Property == SidebarItem.IconProperty ||
                 e.Property == SidebarItem.TrailingIconProperty || e.Property == SidebarItem.BadgeProperty ||
                 e.Property == SidebarItem.IsEnabledProperty || e.Property == SidebarItem.IsUnreadProperty ||
                 e.Property == SidebarItem.IsActionProperty)
        {
            if (_byItem.TryGetValue(item, out var node))
            {
                UpdateRowContent(node);
            }
            if (e.Property == SidebarItem.LabelProperty || e.Property == SidebarItem.IsEnabledProperty)
            {
                Refresh(animate: false);
            }
        }
        else if (e.Property == SidebarItem.KeyProperty)
        {
            if (ReferenceEquals(item, SelectedItem))
            {
                _syncingKey = true;
                try
                {
                    SetCurrentValue(SelectedKeyProperty, item.Key);
                }
                finally
                {
                    _syncingKey = false;
                }
            }
            else if (SelectedItem is null && SelectedKey is { } key && string.Equals(item.Key, key, StringComparison.Ordinal) &&
                     !item.IsAction && !item.HasChildren)
            {
                SetCurrentValue(SelectedItemProperty, item);
            }
        }
    }

    #endregion

    #region Visibility, filter and marker

    private static bool Matches(string? label, string query)
        => label is not null && label.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Applies the rail, the filter and every open / closed state to the rows, then moves the marker and the tab stop.
    /// With <paramref name="animate"/> the marker glides to a new row; the containers spring by themselves.
    /// </summary>
    private void Refresh(bool animate)
    {
        var rail = IsCollapsed;
        var query = rail ? string.Empty : (FilterText ?? string.Empty).Trim();
        var filtering = query.Length > 0;
        PseudoClasses.Set(FilteringPseudoClass, filtering);
        _navigable.Clear();
        var anyShown = false;
        foreach (var sectionNode in _sections)
        {
            var section = sectionNode.Section;
            var shown = 0;
            foreach (var node in sectionNode.SectionItems)
            {
                var item = node.Item!;
                var selfMatch = !filtering || Matches(item.Label, query);
                var childMatch = false;
                foreach (var child in node.Children)
                {
                    var match = filtering && Matches(child.Item!.Label, query);
                    childMatch |= match;
                    child.IsShown = !filtering || selfMatch || match;
                    child.Row!.IsVisible = child.IsShown;
                    child.Row.IsRail = rail;
                }
                node.IsShown = selfMatch || childMatch;
                node.Root!.IsVisible = node.IsShown;
                var row = node.Row!;
                row.IsRail = rail;
                if (node.ChildrenHost is { } host)
                {
                    var expanded = childMatch || item.IsExpanded;
                    node.IsOpen = expanded && !rail;
                    row.IsExpanded = expanded;
                    host.IsExpanded = node.IsOpen;
                    node.ChildrenStack!.IsHitTestVisible = node.IsOpen;
                }
                if (!ReferenceEquals(node, _flyoutNode))
                {
                    Tooltip.SetTip(row, rail ? item.Label : null);
                }
                shown += node.IsShown ? 1 : 0;
            }

            var titled = !string.IsNullOrEmpty(section.Header);
            var collapsible = titled && section.IsCollapsible;
            sectionNode.IsShown = shown > 0 || (!filtering && sectionNode.SectionItems.Count == 0 && titled);
            sectionNode.SectionRoot!.IsVisible = sectionNode.IsShown;
            sectionNode.IsOpen = rail || !collapsible || section.IsExpanded || filtering;
            sectionNode.SectionBody!.IsExpanded = sectionNode.IsOpen;
            sectionNode.SectionBody.Child!.IsHitTestVisible = sectionNode.IsOpen;
            if (sectionNode.Header is { } header)
            {
                // The first section only has a heading when it has a title; later ones always do (the gap above them).
                sectionNode.HasHeader = titled || sectionNode.Index > 0;
                header.IsVisible = sectionNode.HasHeader;
                header.Title = section.Header;
                header.IsCollapsible = collapsible;
                header.IsExpanded = !collapsible || section.IsExpanded || filtering;
                header.IsRail = rail;
                // The first visible section needs no gap above it.
                header.HasGap = anyShown;
            }
            if (!sectionNode.IsShown)
            {
                continue;
            }
            anyShown = true;
            if (sectionNode.Header is { IsInteractive: true, IsVisible: true })
            {
                _navigable.Add(sectionNode);
            }
            if (!sectionNode.IsOpen)
            {
                continue;
            }
            foreach (var node in sectionNode.SectionItems)
            {
                if (!node.IsShown)
                {
                    continue;
                }
                _navigable.Add(node);
                if (!node.IsOpen)
                {
                    continue;
                }
                foreach (var child in node.Children)
                {
                    if (child.IsShown)
                    {
                        _navigable.Add(child);
                    }
                }
            }
        }
        if (_noMatches is not null)
        {
            _noMatches.IsVisible = filtering && !anyShown;
        }
        UpdateSelectionVisuals();
        UpdateMarker(animate);
        UpdateTabStops();
    }

    /// <summary>
    /// The row the marker rests on: the selected page when it is visible, otherwise its parent when the parent is closed
    /// or the sidebar is the rail; nothing when the page is folded away in a closed section or filtered out.
    /// </summary>
    private SidebarNode? MarkerNode()
    {
        if (SelectedItem is not { } selected || !_byItem.TryGetValue(selected, out var node) ||
            !_bySection.TryGetValue(node.Section, out var section) || !section.IsShown || !section.IsOpen)
        {
            return null;
        }
        if (node.Parent is not { } parent)
        {
            return node.IsShown ? node : null;
        }
        if (parent.IsShown && parent.IsOpen && node.IsShown)
        {
            return node;
        }
        return parent.IsShown ? parent : null;
    }

    private void UpdateSelectionVisuals()
    {
        var marker = MarkerNode();
        var selected = SelectedItem is { } item && _byItem.TryGetValue(item, out var selectedNode) ? selectedNode : null;
        if (!ReferenceEquals(selected, _selectedNode))
        {
            if (_selectedNode?.Row is { } old)
            {
                old.IsSelected = false;
            }
            _selectedNode = selected;
        }
        if (_selectedNode?.Row is { } now)
        {
            now.IsSelected = true;
        }
        // The marker rests on a closed parent (or the rail) when the selected page is folded away inside it.
        var current = marker is not null && !ReferenceEquals(marker, selected) ? marker : null;
        if (!ReferenceEquals(current, _currentNode))
        {
            if (_currentNode?.Row is { } old)
            {
                old.IsCurrent = false;
            }
            _currentNode = current;
        }
        if (_currentNode?.Row is { } cur)
        {
            cur.IsCurrent = true;
        }
    }

    private void OnListArranged() => UpdateMarker(animate: false);

    /// <summary>
    /// Moves the marker to its row. A selection change glides (snappy spring). Layout passes (a section opening above, the
    /// width spring, a resize) snap it to the moved row, unless a glide is still running: then it is retargeted, so the
    /// spring keeps its speed instead of jumping.
    /// </summary>
    private void UpdateMarker(bool animate, bool force = false)
    {
        if (_list is null || _markerBlock is null)
        {
            return;
        }
        var node = MarkerNode();
        if (node?.Row is not { } row)
        {
            _markerTarget = default;
            _markerBlock.Hide(animate);
            return;
        }
        if (row.Bounds.Width <= 0 || row.TranslatePoint(default, _list) is not { } origin)
        {
            // Not laid out yet: the next arrange places it.
            return;
        }
        var block = new Rect(origin, row.Bounds.Size);
        if (!animate)
        {
            if (!force && _markerBlock.HasTarget && Near(block, _markerTarget))
            {
                return;
            }
            animate = _markerBlock.HasTarget && !Near(_markerBlock.CurrentRect, _markerTarget);
        }
        _markerTarget = block;
        _markerBlock.MoveTo(block, animate, ArcSpring.Snappy);
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    #endregion

    #region Focus and activation

    private IEnumerable<SidebarNode> AllViews()
    {
        foreach (var section in _sections)
        {
            if (section.Header is not null)
            {
                yield return section;
            }
        }
        foreach (var node in _byItem.Values)
        {
            yield return node;
        }
    }

    /// <summary>
    /// One tab stop for the whole list (roving focus): the focused row while focus is in the list, otherwise the row
    /// the marker rests on, otherwise the first row.
    /// </summary>
    private void UpdateTabStops()
    {
        var listFocused = _list?.IsKeyboardFocusWithin == true;
        if (_focusNode is null || !_navigable.Contains(_focusNode) || (!listFocused && !_focusNode.IsEnabled))
        {
            _focusNode = null;
        }
        if (!listFocused || _focusNode is null)
        {
            var marker = MarkerNode();
            if (marker is not null && _navigable.Contains(marker))
            {
                _focusNode = marker;
            }
            else if (_focusNode is null)
            {
                _focusNode = _navigable.Find(n => n.IsEnabled);
            }
        }
        foreach (var node in AllViews())
        {
            if (node.View is { } view)
            {
                view.IsTabStop = ReferenceEquals(node, _focusNode);
            }
        }
    }

    private void MoveFocus(SidebarNode node, NavigationMethod method)
    {
        _focusNode = node;
        foreach (var other in AllViews())
        {
            if (other.View is { } view)
            {
                view.IsTabStop = ReferenceEquals(other, node);
            }
        }
        if (node.View is not { } target)
        {
            return;
        }
        target.Focus(method);
        Reveal(target);
    }

    /// <summary>Scrolls the list just enough to show <paramref name="view"/> (only the sidebar's own list scrolls).</summary>
    private void Reveal(Control view)
    {
        if (_scroll is null || view.Bounds.Height <= 0 || _scroll.Viewport.Height <= 0 ||
            view.TranslatePoint(default, _scroll) is not { } origin)
        {
            return;
        }
        var top = origin.Y - 4;
        var bottom = origin.Y + view.Bounds.Height + 4;
        var height = _scroll.Viewport.Height;
        var delta = top < 0 ? top : bottom > height ? bottom - height : 0;
        if (Math.Abs(delta) > 0.5)
        {
            var max = Math.Max(0, _scroll.Extent.Height - height);
            _scroll.Offset = new Vector(_scroll.Offset.X, Math.Clamp(_scroll.Offset.Y + delta, 0, max));
        }
    }

    internal void OnViewFocused(SidebarNode node)
    {
        if (!ReferenceEquals(_focusNode, node) && _navigable.Contains(node))
        {
            _focusNode = node;
            foreach (var other in AllViews())
            {
                if (other.View is { } view)
                {
                    view.IsTabStop = ReferenceEquals(other, node);
                }
            }
        }
    }

    internal void OnViewClicked(SidebarNode node)
    {
        if (!node.IsEnabled)
        {
            return;
        }
        if (_navigable.Contains(node))
        {
            MoveFocus(node, NavigationMethod.Pointer);
        }
        Activate(node, NavigationMethod.Pointer);
    }

    /// <summary>
    /// Chooses a row: a heading folds its section, a parent opens or closes (on the rail it shows its pages in a menu),
    /// a page becomes the selection and raises <see cref="ItemInvoked"/> (an action only raises it).
    /// </summary>
    internal void Activate(SidebarNode node, NavigationMethod method)
    {
        if (node.IsHeading)
        {
            node.Section.SetCurrentValue(SidebarSection.IsExpandedProperty, !node.Section.IsExpanded);
            return;
        }
        var item = node.Item!;
        if (!item.IsEnabled)
        {
            return;
        }
        if (node.Children.Count > 0)
        {
            if (IsCollapsed)
            {
                OpenFlyout(node, method is NavigationMethod.Directional or NavigationMethod.Tab);
            }
            else
            {
                item.SetCurrentValue(SidebarItem.IsExpandedProperty, !item.IsExpanded);
            }
            return;
        }
        Invoke(item);
    }

    internal void SetNodeExpanded(SidebarNode node, bool expanded)
    {
        if (node.IsHeading)
        {
            if (node.Section.IsCollapsible)
            {
                node.Section.SetCurrentValue(SidebarSection.IsExpandedProperty, expanded);
            }
        }
        else if (node.Children.Count > 0)
        {
            node.Item!.SetCurrentValue(SidebarItem.IsExpandedProperty, expanded);
        }
    }

    private void Invoke(SidebarItem item)
    {
        // An action acts without becoming the selected page.
        if (!item.IsAction)
        {
            SetCurrentValue(SelectedItemProperty, item);
        }
        RaiseEvent(new SidebarItemInvokedEventArgs(ItemInvokedEvent, item));
        Execute(item.Command, item.CommandParameter);
        Execute(SelectCommand, item);
    }

    private static void Execute(ICommand? command, object? parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void OnFilterSubmitted(object? sender, SearchSubmittedEventArgs e)
    {
        // Enter in the filter chooses the first page that is left.
        foreach (var node in _navigable)
        {
            if (node is { IsHeading: false, IsEnabled: true } && node.Children.Count == 0)
            {
                Invoke(node.Item!);
                return;
            }
        }
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.None && _navigable.Find(n => n.IsEnabled) is { } first)
        {
            MoveFocus(first, NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    #endregion

    #region Rail menu

    private void OpenFlyout(SidebarNode node, bool fromKeyboard)
    {
        if (node.Row is not { } row)
        {
            return;
        }
        var menu = _flyout ??= CreateFlyout();
        menu.Items.Clear();
        menu.Items.Add(new MenuEntryLabel { Text = node.Item!.Label });
        foreach (var child in node.Children)
        {
            var item = child.Item!;
            menu.Items.Add(new MenuEntry
            {
                Header = item.Label,
                Icon = item.Icon as Geometry,
                InputGestureText = item.Badge,
                IsEnabled = item.IsEnabled,
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "sidebar-pages",
                IsChecked = ReferenceEquals(item, SelectedItem),
                Tag = item
            });
        }
        _flyoutNode = node;
        // The tooltip would open over the menu.
        Tooltip.SetTip(row, null);
        // The menu opens just past the sidebar's edge, not over the rail's inset.
        var edge = this.TranslatePoint(new Point(Bounds.Width, 0), row)?.X ?? row.Bounds.Width;
        menu.AnchorGap = Math.Max(4, edge - row.Bounds.Width + 6);
        menu.Open(row, fromKeyboard ? MenuOpenFocus.First : MenuOpenFocus.Surface);
    }

    private MenuSurface CreateFlyout()
    {
        var menu = new MenuSurface { Side = MenuSide.Right, Alignment = MenuAlignment.Start, AnchorGap = 8 };
        menu.ItemClicked += OnFlyoutItemClicked;
        menu.Closed += OnFlyoutClosed;
        return menu;
    }

    private void OnFlyoutItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is SidebarItem item && item.IsEnabled)
        {
            Invoke(item);
        }
    }

    private void OnFlyoutClosed(object? sender, RoutedEventArgs e)
    {
        if (_flyoutNode is { Row: { } row, Item: { } item })
        {
            Tooltip.SetTip(row, IsCollapsed ? item.Label : null);
        }
        _flyoutNode = null;
    }

    #endregion

    #region Keyboard

    private static bool IsCommandModifier(KeyModifiers modifiers) => modifiers is KeyModifiers.Control or KeyModifiers.Meta;

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Cmd / Ctrl+B anywhere inside the sidebar toggles the rail (before a text box sees it).
        if (e.Key == Key.B && IsCommandModifier(e.KeyModifiers))
        {
            ToggleCollapsed();
            e.Handled = true;
        }
    }

    private SidebarNode? NodeFromSource(object? source)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            switch (visual)
            {
                case SidebarRow row when ReferenceEquals(row.Owner, this):
                    return row.Node;
                case SidebarSectionHeader header when ReferenceEquals(header.Owner, this):
                    return header.Node;
            }
        }
        return null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0 ||
            NodeFromSource(e.Source) is not { } node)
        {
            return;
        }
        var index = _navigable.IndexOf(node);
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.Home:
            case Key.End:
            case Key.PageDown:
            case Key.PageUp:
                _navigator.PageSize = Math.Max(1, (int)((_scroll?.Viewport.Height ?? 340) / 36) - 1);
                if (_navigator.Navigate(Math.Max(index, 0), e.Key) is { } target && target >= 0 && target < _navigable.Count)
                {
                    MoveFocus(_navigable[target], NavigationMethod.Directional);
                }
                e.Handled = true;
                break;
            case Key.Right:
                OnRight(node, index);
                e.Handled = true;
                break;
            case Key.Left:
                OnLeft(node);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Activate(node, NavigationMethod.Directional);
                e.Handled = true;
                break;
        }
    }

    private void OnRight(SidebarNode node, int index)
    {
        if (node.IsHeading)
        {
            if (!node.IsOpen)
            {
                SetNodeExpanded(node, true);
            }
            else if (index + 1 < _navigable.Count && !_navigable[index + 1].IsHeading && ReferenceEquals(_navigable[index + 1].Section, node.Section))
            {
                MoveFocus(_navigable[index + 1], NavigationMethod.Directional);
            }
            return;
        }
        if (node.Children.Count == 0)
        {
            return;
        }
        if (IsCollapsed)
        {
            OpenFlyout(node, fromKeyboard: true);
        }
        else if (!node.IsOpen)
        {
            SetNodeExpanded(node, true);
        }
        else if (index + 1 < _navigable.Count && ReferenceEquals(_navigable[index + 1].Parent, node))
        {
            MoveFocus(_navigable[index + 1], NavigationMethod.Directional);
        }
    }

    private void OnLeft(SidebarNode node)
    {
        if (node.IsHeading)
        {
            if (node.IsOpen)
            {
                SetNodeExpanded(node, false);
            }
            return;
        }
        if (node.Children.Count > 0 && node.IsOpen && node.Item!.IsExpanded)
        {
            SetNodeExpanded(node, false);
            return;
        }
        if (node.Parent is { } parent && _navigable.Contains(parent))
        {
            MoveFocus(parent, NavigationMethod.Directional);
            return;
        }
        // A page of a collapsible section goes up to the section's heading.
        if (node.Parent is null && _bySection.TryGetValue(node.Section, out var heading) && _navigable.Contains(heading))
        {
            MoveFocus(heading, NavigationMethod.Directional);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrWhiteSpace(e.Text) || NodeFromSource(e.Source) is not { } node)
        {
            return;
        }
        var index = _navigable.IndexOf(node);
        if (_navigator.TypeAhead(Math.Max(index, 0), e.Text) is { } target && target >= 0 && target < _navigable.Count)
        {
            MoveFocus(_navigable[target], NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new SidebarAutomationPeer(this);

    /// <summary>The navigation landmark, named by <see cref="Label"/>.</summary>
    private sealed class SidebarAutomationPeer : ControlAutomationPeer
    {
        public SidebarAutomationPeer(Sidebar owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((Sidebar)Owner).Label;
    }
}
