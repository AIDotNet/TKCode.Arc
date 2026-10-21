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
/// The app's main navigation (an original TKCode design): a header for a workspace switcher, an optional filter box,
/// sections of pages with icons, counts and one level of nested pages, and a footer for the signed-in user. The selected
/// page carries a muted block with an accent bar that glides between rows on a snappy spring; parents open their pages
/// with a height spring and a turning chevron. The sidebar collapses to a 64px icon rail on a width spring: labels and
/// counts fade, counts become dots, every page gets a tooltip on the right and a parent shows its pages in a menu.
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
[TemplatePart(MarkerBarPart, typeof(GlidingIndicator))]
[TemplatePart(FilterPart, typeof(SearchField))]
[TemplatePart(FilterButtonPart, typeof(SidebarIconButton))]
[TemplatePart(CollapseButtonPart, typeof(SidebarIconButton))]
[TemplatePart(HeaderRowPart, typeof(Control))]
[TemplatePart(NoMatchesPart, typeof(TextBlock))]
[PseudoClasses(CollapsedPseudoClass, FilteringPseudoClass)]
public class Sidebar : TemplatedControl
{
    public const string ListPart = "PART_List";
    public const string ContentPart = "PART_Content";
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string MarkerBlockPart = "PART_MarkerBlock";
    public const string MarkerBarPart = "PART_MarkerBar";
    public const string FilterPart = "PART_Filter";
    public const string FilterButtonPart = "PART_FilterButton";
    public const string CollapseButtonPart = "PART_CollapseButton";
    public const string HeaderRowPart = "PART_HeaderRow";
    public const string NoMatchesPart = "PART_NoMatches";

    /// <summary>Set while the sidebar is the icon rail (<see cref="IsCollapsed"/>).</summary>
    public const string CollapsedPseudoClass = ":collapsed";

    /// <summary>Set while the filter narrows the pages.</summary>
    public const string FilteringPseudoClass = ":filtering";

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

    /// <summary>Content at the top, beside the collapse button (a workspace switcher). It is hidden on the rail while the collapse button shows.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<Sidebar, object?>(nameof(Header));

    /// <summary>Content at the bottom (the signed-in user). It keeps its open-width layout on the rail, clipped to the rail; use the <c>:collapsed</c> pseudo-class to adapt it.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Sidebar, object?>(nameof(Footer));

    /// <summary>Shows the filter box under the header (hidden on the rail).</summary>
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

    /// <summary>Shows the collapse / expand button (PanelLeft icon) in the header row.</summary>
    public static readonly StyledProperty<bool> IsCollapseButtonVisibleProperty =
        AvaloniaProperty.Register<Sidebar, bool>(nameof(IsCollapseButtonVisible), true);

    /// <summary>Accessible name and tooltip of the collapse button while the sidebar is open.</summary>
    public static readonly StyledProperty<string> CollapseButtonTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(CollapseButtonText), "Collapse sidebar");

    /// <summary>Accessible name and tooltip of the collapse button while the sidebar is the rail.</summary>
    public static readonly StyledProperty<string> ExpandButtonTextProperty =
        AvaloniaProperty.Register<Sidebar, string>(nameof(ExpandButtonText), "Expand sidebar");

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
    private readonly HashSet<SidebarSection> _watchedSections = new();
    private readonly HashSet<SidebarItem> _watchedItems = new();
    private readonly ListKeyboardNavigator _navigator;
    private SidebarListPanel? _list;
    private Panel? _content;
    private ScrollViewer? _scroll;
    private GlidingIndicator? _markerBlock;
    private GlidingIndicator? _markerBar;
    private SearchField? _filter;
    private SidebarIconButton? _filterButton;
    private SidebarIconButton? _collapseButton;
    private Control? _headerRow;
    private TextBlock? _noMatches;
    private MenuSurface? _flyout;
    private SidebarNode? _flyoutNode;
    private SidebarNode? _focusNode;
    private Rect _markerTarget;
    private bool _rebuildPosted;
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
        Sections.CollectionChanged += OnStructureChanged;
        AutomationProperties.SetName(this, Label);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Collapses the sidebar to the icon rail, or opens it from the rail.</summary>
    public void ToggleCollapsed() => SetCurrentValue(IsCollapsedProperty, !IsCollapsed);

    /// <summary>How far the current width falls short of the open width (the rows' layout makes up for it).</summary>
    internal double LayoutDeficit
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
    internal SidebarSectionHeader? HeaderOf(SidebarSection section) => _bySection.TryGetValue(section, out var node) ? node.Header : null;

    /// <summary>The row the selection marker rests on.</summary>
    internal SidebarRow? MarkerRow => MarkerNode()?.Row;

    /// <summary>The marker's target rectangle in list coordinates.</summary>
    internal Rect MarkerTarget => _markerTarget;

    internal GlidingIndicator? MarkerBlock => _markerBlock;

    internal GlidingIndicator? MarkerBar => _markerBar;

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
        _list = e.NameScope.Find<SidebarListPanel>(ListPart);
        _content = e.NameScope.Find<Panel>(ContentPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _markerBlock = e.NameScope.Find<GlidingIndicator>(MarkerBlockPart);
        _markerBar = e.NameScope.Find<GlidingIndicator>(MarkerBarPart);
        _filter = e.NameScope.Find<SearchField>(FilterPart);
        _filterButton = e.NameScope.Find<SidebarIconButton>(FilterButtonPart);
        _collapseButton = e.NameScope.Find<SidebarIconButton>(CollapseButtonPart);
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
        Rebuild();
    }

    private void OnListPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SidebarListPanel.RowGapProperty || e.Property == SidebarListPanel.ChildrenMarginProperty ||
            e.Property == SidebarListPanel.GuideMarginProperty)
        {
            InvalidateStructure();
        }
        else if (e.Property == SidebarListPanel.MarkerBarWidthProperty || e.Property == SidebarListPanel.MarkerBarHeightProperty ||
                 e.Property == SidebarListPanel.MarkerBarInsetProperty)
        {
            UpdateMarker(animate: false, force: true);
        }
    }

    /// <summary>The header row, the collapse button and the filter follow the rail and the visibility switches.</summary>
    private void UpdateChrome()
    {
        var collapsed = IsCollapsed;
        if (_collapseButton is not null)
        {
            var text = collapsed ? ExpandButtonText : CollapseButtonText;
            AutomationProperties.SetName(_collapseButton, text);
            Tooltip.SetTip(_collapseButton, text);
            Tooltip.SetSide(_collapseButton, collapsed ? TooltipSide.Right : TooltipSide.Bottom);
        }
        if (_headerRow is not null)
        {
            _headerRow.IsVisible = Header is not null || IsCollapseButtonVisible;
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

    #endregion

    #region Property changes

    private static SidebarItem? CoerceSelectedItem(AvaloniaObject sender, SidebarItem? value)
    {
        // A parent only opens and closes its pages; it is never the selected page.
        if (value is not null && sender is Sidebar sidebar && sidebar.IsParent(value))
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
        else if (change.Property == FilterTextProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == HeaderProperty || change.Property == IsCollapseButtonVisibleProperty ||
                 change.Property == CollapseButtonTextProperty || change.Property == ExpandButtonTextProperty ||
                 change.Property == FilterPlaceholderProperty)
        {
            UpdateChrome();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string>());
        }
    }

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

    private void OnStructureChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateStructure();

    /// <summary>Sections or item collections changed: rebuild the rows once, after the current batch of changes.</summary>
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

    private void Rebuild()
    {
        _rebuildPosted = false;
        UpdateSubscriptions();
        if (_content is null || _list is null)
        {
            return;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var focusedNode = focused is not null ? NodeFromSource(focused) : null;
        var focusedItem = focusedNode?.Item;
        var focusedSection = focusedNode is { IsHeading: true } ? focusedNode.Section : null;
        var tabItem = _focusNode?.Item;
        var tabSection = _focusNode is { IsHeading: true } ? _focusNode.Section : null;

        // A control icon can live in one row only: release it from the rows being replaced.
        foreach (var node in _byItem.Values)
        {
            if (node.Row is { } row)
            {
                row.IconContent = null;
            }
        }
        _content.Children.Clear();
        _sections.Clear();
        _byItem.Clear();
        _bySection.Clear();
        _focusNode = null;

        for (var i = 0; i < Sections.Count; i++)
        {
            var section = Sections[i];
            if (_bySection.ContainsKey(section))
            {
                continue;
            }
            var sectionNode = new SidebarNode(section, null, null);
            var root = new StackPanel();
            if (i > 0 || !string.IsNullOrEmpty(section.Header))
            {
                var header = new SidebarSectionHeader(this, sectionNode);
                sectionNode.Header = header;
                root.Children.Add(header);
            }
            var body = new StackPanel { Spacing = _list.RowGap };
            foreach (var item in section.Items)
            {
                if (_byItem.ContainsKey(item))
                {
                    continue;
                }
                var node = BuildItem(section, item, null);
                sectionNode.SectionItems.Add(node);
                body.Children.Add(node.Root!);
            }
            var bodyHost = new SidebarHeightSpring { Child = body };
            sectionNode.SectionBody = bodyHost;
            sectionNode.SectionRoot = root;
            root.Children.Add(bodyHost);
            _content.Children.Add(root);
            _sections.Add(sectionNode);
            _bySection[section] = sectionNode;
        }

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
        if (tabItem is not null && _byItem.TryGetValue(tabItem, out var tabNode))
        {
            _focusNode = tabNode;
        }
        else if (tabSection is not null && _bySection.TryGetValue(tabSection, out var tabHeading))
        {
            _focusNode = tabHeading;
        }
        Refresh(animate: false);
        if (focusedItem is not null || focusedSection is not null)
        {
            var target = focusedItem is not null && _byItem.TryGetValue(focusedItem, out var item) ? item
                : focusedSection is not null && _bySection.TryGetValue(focusedSection, out var heading) ? heading
                : _focusNode;
            if (target is not null && _navigable.Contains(target))
            {
                MoveFocus(target, NavigationMethod.Unspecified);
            }
        }
    }

    private SidebarNode BuildItem(SidebarSection section, SidebarItem item, SidebarNode? parent)
    {
        var node = new SidebarNode(section, item, parent);
        _byItem[item] = node;
        var row = new SidebarRow(this, node);
        Tooltip.SetSide(row, TooltipSide.Right);
        node.Row = row;
        UpdateRowContent(node);
        if (parent is null && item.HasChildren)
        {
            row.IsParent = true;
            var stack = new StackPanel { Spacing = _list!.RowGap, Margin = _list.ChildrenMargin };
            foreach (var child in item.Items)
            {
                if (_byItem.ContainsKey(child))
                {
                    continue;
                }
                var childNode = BuildItem(section, child, node);
                node.Children.Add(childNode);
                stack.Children.Add(childNode.Root!);
            }
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
        }
        else
        {
            node.Root = row;
        }
        return node;
    }

    private static void UpdateRowContent(SidebarNode node)
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
        row.Monogram = string.IsNullOrEmpty(item.Label) ? null : char.ToUpperInvariant(item.Label[0]).ToString();
        row.Badge = string.IsNullOrEmpty(item.Badge) ? null : item.Badge;
        row.IsEnabled = item.IsEnabled;
        AutomationProperties.SetName(row, row.Badge is null ? item.Label : $"{item.Label}, {row.Badge}");
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
                if (string.Equals(item.Key, key, StringComparison.Ordinal) && !item.HasChildren)
                {
                    return item;
                }
                foreach (var child in item.Items)
                {
                    if (string.Equals(child.Key, key, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }
            }
        }
        return null;
    }

    private void UpdateSubscriptions()
    {
        var sections = new HashSet<SidebarSection>(Sections);
        var items = new HashSet<SidebarItem>();
        foreach (var section in Sections)
        {
            foreach (var item in section.Items)
            {
                items.Add(item);
                foreach (var child in item.Items)
                {
                    items.Add(child);
                }
            }
        }
        foreach (var section in _watchedSections)
        {
            if (!sections.Contains(section))
            {
                section.PropertyChanged -= OnSectionPropertyChanged;
                section.Items.CollectionChanged -= OnStructureChanged;
            }
        }
        foreach (var section in sections)
        {
            if (!_watchedSections.Contains(section))
            {
                section.PropertyChanged += OnSectionPropertyChanged;
                section.Items.CollectionChanged += OnStructureChanged;
            }
        }
        foreach (var item in _watchedItems)
        {
            if (!items.Contains(item))
            {
                item.PropertyChanged -= OnItemPropertyChanged;
                item.Items.CollectionChanged -= OnStructureChanged;
            }
        }
        foreach (var item in items)
        {
            if (!_watchedItems.Contains(item))
            {
                item.PropertyChanged += OnItemPropertyChanged;
                item.Items.CollectionChanged += OnStructureChanged;
            }
        }
        _watchedSections.Clear();
        _watchedSections.UnionWith(sections);
        _watchedItems.Clear();
        _watchedItems.UnionWith(items);
    }

    private void OnSectionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SidebarSection.HeaderProperty || e.Property == SidebarSection.IsCollapsibleProperty)
        {
            if (sender is SidebarSection section && _bySection.TryGetValue(section, out var node) && node.Header is not null)
            {
                // A heading that stays a heading updates in place; one that appears or goes needs a rebuild.
                Refresh(animate: true);
                return;
            }
            InvalidateStructure();
        }
        else if (e.Property == SidebarSection.IsExpandedProperty)
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
                 e.Property == SidebarItem.BadgeProperty || e.Property == SidebarItem.IsEnabledProperty)
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
            else if (SelectedItem is null && SelectedKey is { } key && string.Equals(item.Key, key, StringComparison.Ordinal))
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
            if (sectionNode.Header is { IsInteractive: true })
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
        var selected = SelectedItem;
        foreach (var node in _byItem.Values)
        {
            var row = node.Row!;
            row.IsSelected = ReferenceEquals(node.Item, selected);
            row.IsCurrent = ReferenceEquals(node, marker) && !row.IsSelected;
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
        if (_list is null || _markerBlock is null || _markerBar is null)
        {
            return;
        }
        var node = MarkerNode();
        if (node?.Row is not { } row)
        {
            _markerTarget = default;
            _markerBlock.Hide(animate);
            _markerBar.Hide(animate);
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
        var barHeight = Math.Min(_list.MarkerBarHeight, block.Height);
        var bar = new Rect(block.X + _list.MarkerBarInset, block.Y + (block.Height - barHeight) / 2, _list.MarkerBarWidth, barHeight);
        _markerBlock.MoveTo(block, animate, ArcSpring.Snappy);
        _markerBar.MoveTo(bar, animate, ArcSpring.Snappy);
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
    /// a page becomes the selection and raises <see cref="ItemInvoked"/>.
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
        SetCurrentValue(SelectedItemProperty, item);
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
