using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using System.Windows.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How keyboard focus and selection relate in a <see cref="Tabs"/> list.</summary>
public enum TabsActivationMode
{
    /// <summary>Arrow keys move focus and select the focused tab at once (Arc default).</summary>
    Automatic,

    /// <summary>Arrow keys only move focus; Enter or Space selects the focused tab.</summary>
    Manual
}

/// <summary>The look of a <see cref="Tabs"/> list.</summary>
public enum TabsVariant
{
    /// <summary>Triggers in a muted rounded shell with a raised pill under the selected one (Arc default).</summary>
    Pill,

    /// <summary>
    /// Browser-style cards on a full-width muted strip: the selected card glides along a baseline and opens into the panel
    /// below. Suits closable tabs and an add button.
    /// </summary>
    Card
}

/// <summary>
/// Arc tabs: switch between related views of the same object. A single selection pill glides between the
/// triggers on a morph spring, the incoming panel slides 8px in from the side of travel while the outgoing one
/// is pinned in place and fades, and the panel area springs from the old panel height to the new one so the
/// content below glides. Long tab lists scroll horizontally with soft edges and edge scroll buttons, and the
/// selected or focused tab is always scrolled into view.
/// <para>
/// Items are <see cref="TabItem"/> containers written inline (<c>&lt;arc:TabItem Header="Overview"&gt;…&lt;/arc:TabItem&gt;</c>)
/// or generated from <see cref="ItemsControl.ItemsSource"/>: <see cref="ItemsControl.ItemTemplate"/> renders the
/// trigger and <see cref="ContentTemplate"/> the panel.
/// </para>
/// <para>
/// <see cref="Variant"/> switches to browser-style cards. Tabs can be closable (<see cref="TabItem.IsClosable"/> or
/// <see cref="AreTabsClosable"/>): closing raises <see cref="TabCloseRequested"/> and the app removes the item; when the
/// selected tab is removed the selection moves to the next tab, or to the previous one at the end.
/// <see cref="IsAddButtonVisible"/> shows an add button after the last tab that raises <see cref="TabAddRequested"/>.
/// </para>
/// </summary>
[TemplatePart(ListViewportPart, typeof(ScrollViewer))]
[TemplatePart(ListPanelPart, typeof(Panel))]
[TemplatePart(SelectionPillPart, typeof(GlidingIndicator))]
[TemplatePart(ScrollLeftPart, typeof(AvaloniaButton))]
[TemplatePart(ScrollRightPart, typeof(AvaloniaButton))]
[TemplatePart(ContentHostPart, typeof(Control))]
[TemplatePart(ListRowPart, typeof(DockPanel))]
[PseudoClasses(OverflowPseudoClass, ScrolledStartPseudoClass, ScrolledEndPseudoClass, CardPseudoClass)]
public partial class Tabs : SelectingItemsControl
{
    public const string ListViewportPart = "PART_ListViewport";
    public const string ListPanelPart = "PART_ListPanel";
    public const string SelectionPillPart = "PART_SelectionPill";
    public const string ScrollLeftPart = "PART_ScrollLeft";
    public const string ScrollRightPart = "PART_ScrollRight";
    public const string ContentHostPart = "PART_ContentHost";

    /// <summary>The row that holds the scrolling list and, at its end, the add button.</summary>
    public const string ListRowPart = "PART_ListRow";

    /// <summary>Name of the add button, built the first time <see cref="IsAddButtonVisible"/> is true.</summary>
    public const string AddButtonPart = "PART_AddButton";

    /// <summary>Set while <see cref="Variant"/> is <see cref="TabsVariant.Card"/>.</summary>
    public const string CardPseudoClass = ":card";

    /// <summary>Set while the tab list is wider than its frame.</summary>
    public const string OverflowPseudoClass = ":overflow";

    /// <summary>Set while there are tabs hidden before the visible part of the list.</summary>
    public const string ScrolledStartPseudoClass = ":scrolled-start";

    /// <summary>Set while there are tabs hidden after the visible part of the list.</summary>
    public const string ScrolledEndPseudoClass = ":scrolled-end";

    /// <summary>Width of the edge fade over the scrolled list (Arc: 35px).</summary>
    internal const double EdgeFade = 35;

    /// <summary>Room kept free for a scroll button when a tab is scrolled into view (Arc: 34px).</summary>
    internal const double RevealInset = 34;

    /// <summary>How far the selected card runs past the list's bottom so its bottom stroke is clipped away.</summary>
    internal const double CardIndicatorOverhang = 2;

    #region Properties

    /// <summary>Template for the panel of data items (the trigger uses <see cref="ItemsControl.ItemTemplate"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<Tabs, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>Whether arrow keys select the tab they move to (<see cref="TabsActivationMode.Automatic"/>, Arc default).</summary>
    public static readonly StyledProperty<TabsActivationMode> ActivationModeProperty =
        AvaloniaProperty.Register<Tabs, TabsActivationMode>(nameof(ActivationMode));

    /// <summary>Margin around the panel; its top edge is the space between the trigger list and the panel (Arc: 16px).</summary>
    public static readonly StyledProperty<Thickness> ContentMarginProperty =
        AvaloniaProperty.Register<Tabs, Thickness>(nameof(ContentMargin));

    /// <summary>Accessible name of the left scroll button.</summary>
    public static readonly StyledProperty<string> ScrollLeftLabelProperty =
        AvaloniaProperty.Register<Tabs, string>(nameof(ScrollLeftLabel), "Scroll tabs left");

    /// <summary>Accessible name of the right scroll button.</summary>
    public static readonly StyledProperty<string> ScrollRightLabelProperty =
        AvaloniaProperty.Register<Tabs, string>(nameof(ScrollRightLabel), "Scroll tabs right");

    /// <summary>The look of the list: the Arc pill (default) or browser-style cards.</summary>
    public static readonly StyledProperty<TabsVariant> VariantProperty =
        AvaloniaProperty.Register<Tabs, TabsVariant>(nameof(Variant));

    /// <summary>
    /// Whether every tab is closable, as if each set <see cref="TabItem.IsClosable"/> (handy with
    /// <see cref="ItemsControl.ItemsSource"/>). A tab's own <see cref="TabItem.IsClosable"/> still makes it closable when this is
    /// false. Default false.
    /// </summary>
    public static readonly StyledProperty<bool> AreTabsClosableProperty =
        AvaloniaProperty.Register<Tabs, bool>(nameof(AreTabsClosable));

    /// <summary>Whether an add button follows the last tab (it stays in view when the list scrolls). Default false.</summary>
    public static readonly StyledProperty<bool> IsAddButtonVisibleProperty =
        AvaloniaProperty.Register<Tabs, bool>(nameof(IsAddButtonVisible));

    /// <summary>Accessible name of the add button.</summary>
    public static readonly StyledProperty<string> AddTabLabelProperty =
        AvaloniaProperty.Register<Tabs, string>(nameof(AddTabLabel), "Add tab");

    /// <summary>Accessible name of the close buttons.</summary>
    public static readonly StyledProperty<string> CloseTabLabelProperty =
        AvaloniaProperty.Register<Tabs, string>(nameof(CloseTabLabel), "Close tab");

    /// <summary>Executed when the add button is pressed, after <see cref="TabAddRequested"/> unless a handler handled it.</summary>
    public static readonly StyledProperty<ICommand?> AddTabCommandProperty =
        AvaloniaProperty.Register<Tabs, ICommand?>(nameof(AddTabCommand));

    /// <summary>
    /// Executed with the tab's item (see <see cref="TabCloseRequestedEventArgs.Item"/>) when the user closes a tab, after
    /// <see cref="TabCloseRequested"/> unless a handler handled it.
    /// </summary>
    public static readonly StyledProperty<ICommand?> CloseTabCommandProperty =
        AvaloniaProperty.Register<Tabs, ICommand?>(nameof(CloseTabCommand));

    /// <summary>Raised when the user closes a closable tab (close button, middle click, Delete); the app removes the item.</summary>
    public static readonly RoutedEvent<TabCloseRequestedEventArgs> TabCloseRequestedEvent =
        RoutedEvent.Register<Tabs, TabCloseRequestedEventArgs>(nameof(TabCloseRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the user presses the add button; the app adds an item (and usually selects it).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TabAddRequestedEvent =
        RoutedEvent.Register<Tabs, RoutedEventArgs>(nameof(TabAddRequested), RoutingStrategies.Bubble);

    /// <summary>The panel content of the selected tab.</summary>
    public static readonly DirectProperty<Tabs, object?> SelectedContentProperty =
        AvaloniaProperty.RegisterDirect<Tabs, object?>(nameof(SelectedContent), o => o.SelectedContent);

    /// <summary>The template of the selected tab's panel.</summary>
    public static readonly DirectProperty<Tabs, IDataTemplate?> SelectedContentTemplateProperty =
        AvaloniaProperty.RegisterDirect<Tabs, IDataTemplate?>(nameof(SelectedContentTemplate), o => o.SelectedContentTemplate);

    /// <inheritdoc cref="ContentMarginProperty"/>
    public Thickness ContentMargin
    {
        get => GetValue(ContentMarginProperty);
        set => SetValue(ContentMarginProperty, value);
    }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <inheritdoc cref="ActivationModeProperty"/>
    public TabsActivationMode ActivationMode
    {
        get => GetValue(ActivationModeProperty);
        set => SetValue(ActivationModeProperty, value);
    }

    /// <inheritdoc cref="ScrollLeftLabelProperty"/>
    public string ScrollLeftLabel
    {
        get => GetValue(ScrollLeftLabelProperty);
        set => SetValue(ScrollLeftLabelProperty, value);
    }

    /// <inheritdoc cref="ScrollRightLabelProperty"/>
    public string ScrollRightLabel
    {
        get => GetValue(ScrollRightLabelProperty);
        set => SetValue(ScrollRightLabelProperty, value);
    }

    /// <inheritdoc cref="VariantProperty"/>
    public TabsVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <inheritdoc cref="AreTabsClosableProperty"/>
    public bool AreTabsClosable
    {
        get => GetValue(AreTabsClosableProperty);
        set => SetValue(AreTabsClosableProperty, value);
    }

    /// <inheritdoc cref="IsAddButtonVisibleProperty"/>
    public bool IsAddButtonVisible
    {
        get => GetValue(IsAddButtonVisibleProperty);
        set => SetValue(IsAddButtonVisibleProperty, value);
    }

    /// <inheritdoc cref="AddTabLabelProperty"/>
    public string AddTabLabel
    {
        get => GetValue(AddTabLabelProperty);
        set => SetValue(AddTabLabelProperty, value);
    }

    /// <inheritdoc cref="CloseTabLabelProperty"/>
    public string CloseTabLabel
    {
        get => GetValue(CloseTabLabelProperty);
        set => SetValue(CloseTabLabelProperty, value);
    }

    /// <inheritdoc cref="AddTabCommandProperty"/>
    public ICommand? AddTabCommand
    {
        get => GetValue(AddTabCommandProperty);
        set => SetValue(AddTabCommandProperty, value);
    }

    /// <inheritdoc cref="CloseTabCommandProperty"/>
    public ICommand? CloseTabCommand
    {
        get => GetValue(CloseTabCommandProperty);
        set => SetValue(CloseTabCommandProperty, value);
    }

    /// <inheritdoc cref="TabCloseRequestedEvent"/>
    public event EventHandler<TabCloseRequestedEventArgs>? TabCloseRequested
    {
        add => AddHandler(TabCloseRequestedEvent, value);
        remove => RemoveHandler(TabCloseRequestedEvent, value);
    }

    /// <inheritdoc cref="TabAddRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? TabAddRequested
    {
        add => AddHandler(TabAddRequestedEvent, value);
        remove => RemoveHandler(TabAddRequestedEvent, value);
    }


    /// <inheritdoc cref="SelectedContentProperty"/>
    public object? SelectedContent
    {
        get;
        private set => SetAndRaise(SelectedContentProperty, ref field, value);
    }

    private IDataTemplate? _selectedContentTemplate;

    /// <inheritdoc cref="SelectedContentTemplateProperty"/>
    public IDataTemplate? SelectedContentTemplate
    {
        get => _selectedContentTemplate;
        private set => SetAndRaise(SelectedContentTemplateProperty, ref _selectedContentTemplate, value);
    }

    #endregion

    private ScrollViewer? _viewport;
    private TabsListPanel? _listPanel;
    private GlidingIndicator? _pill;
    private AvaloniaButton? _scrollLeft;
    private AvaloniaButton? _scrollRight;
    private TabsPanelHost? _contentHost;
    private MotionValue<double>? _scroll;
    private DockPanel? _listRow;
    private (bool Start, bool End, double Fade) _edgeMaskKey;
    private Rect _pillTarget;
    private bool _overflow;
    private bool _scrolledStart;
    private bool _scrolledEnd;
    private int _focusIndex = -1;
    private bool _revealPending;
    private bool _revealPendingInstant;

    static Tabs()
    {
        SelectionModeProperty.OverrideDefaultValue<Tabs>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<Tabs>(false);
        // The list reveals the selected tab itself (RevealIndex, on the smooth spring). The stock scroll-into-view of the
        // selected item would also jump the list and, because the request bubbles, scroll any page the tabs sit in.
        AutoScrollToSelectedItemProperty.OverrideDefaultValue<Tabs>(false);
    }

    public Tabs()
    {
        // Items is the same view for inline items and ItemsSource; it reports a removal before the selection model does.
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    /// <summary>Selects the tab at <paramref name="index"/> when it exists and is enabled.</summary>
    public bool SelectTab(int index)
    {
        if (index < 0 || index >= ItemCount || !IsTabEnabled(index))
        {
            return false;
        }
        SelectedIndex = index;
        return true;
    }

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new TabItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<TabItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is TabItem tab)
        {
            ApplyOwnerState(tab);
            if (!ReferenceEquals(container, item) && ContentTemplate is { } template && !tab.IsSet(ContentControl.ContentTemplateProperty))
            {
                tab.ContentTemplate = template;
            }
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (index == SelectedIndex)
        {
            UpdateContent(0, animate: false);
        }
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        UpdateTabStops();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_viewport is not null)
        {
            _viewport.ScrollChanged -= OnViewportScrollChanged;
            _viewport.SizeChanged -= OnViewportSizeChanged;
            _viewport.RemoveHandler(PointerWheelChangedEvent, OnViewportWheel);
        }
        _scrollLeft?.Click -= OnScrollLeftClick;
        _scrollRight?.Click -= OnScrollRightClick;
        _listPanel?.Arranged = null;
        DetachAddButton();

        base.OnApplyTemplate(e);

        _viewport = e.NameScope.Find<ScrollViewer>(ListViewportPart);
        _listPanel = e.NameScope.Find<TabsListPanel>(ListPanelPart);
        _pill = e.NameScope.Find<GlidingIndicator>(SelectionPillPart);
        // Built by UpdateOverflow the first time the list overflows, next to the viewport.
        _scrollLeft = null;
        _scrollRight = null;
        _contentHost = e.NameScope.Find<TabsPanelHost>(ContentHostPart);
        _listRow = e.NameScope.Find<DockPanel>(ListRowPart);
        _pillTarget = default;
        UpdateAddButton();

        if (_viewport is not null)
        {
            _viewport.ScrollChanged += OnViewportScrollChanged;
            _viewport.SizeChanged += OnViewportSizeChanged;
            _viewport.AddHandler(PointerWheelChangedEvent, OnViewportWheel, RoutingStrategies.Tunnel);
        }
        _listPanel?.Arranged = OnListArranged;
        _revealPending = true;
        _revealPendingInstant = true;
        UpdateContent(0, animate: false);
    }

    #endregion

    #region Selection

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            var from = change.GetOldValue<int>();
            var to = change.GetNewValue<int>();
            if (to < 0 && _removedSelection >= 0)
            {
                // The selected tab is being removed; the next selection follows at once (Tabs.Closing.cs), so the old
                // panel and pill stay until then instead of emptying for a moment.
                return;
            }
            var direction = from >= 0 && to >= 0 && from != to ? Math.Sign(to - from) : 0;
            _focusIndex = to;
            UpdateContent(direction, animate: true);
            UpdateIndicator(animate: true);
            UpdateTabStops();
            // A tab that is not laid out yet (just added, or before the first layout) is revealed once it is.
            _revealPending = !RevealIndex(to);
            _revealPendingInstant = !IsLoaded;
        }
        else if (change.Property == ContentTemplateProperty)
        {
            UpdateContent(0, animate: false);
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(CardPseudoClass, change.GetNewValue<TabsVariant>() == TabsVariant.Card);
            UpdateOwnerStates();
            UpdateScrollButtonVariant(_scrollLeft);
            UpdateScrollButtonVariant(_scrollRight);
            UpdateAddButton();
            // The card's rect differs from the pill's; the list arranges again with the new paddings and then snaps it.
            _pillTarget = default;
        }
        else if (change.Property == AreTabsClosableProperty || change.Property == CloseTabLabelProperty)
        {
            UpdateOwnerStates();
        }
        else if (change.Property == IsAddButtonVisibleProperty)
        {
            UpdateAddButton();
        }
    }

    /// <summary>Called by a <see cref="TabItem"/> when its content or template changes.</summary>
    internal void OnTabContentChanged(TabItem tab)
    {
        if (IndexFromContainer(tab) == SelectedIndex)
        {
            UpdateContent(0, animate: false);
        }
    }

    /// <summary>Called by a <see cref="TabItem"/> when it is pressed (mouse down, like Radix).</summary>
    internal void OnTabPressed(TabItem tab)
    {
        var index = IndexFromContainer(tab);
        if (index >= 0 && tab.IsEffectivelyEnabled)
        {
            _focusIndex = index;
            SelectedIndex = index;
        }
    }

    /// <summary>Called by a <see cref="TabItem"/> when it receives keyboard focus.</summary>
    internal void OnTabFocused(TabItem tab, NavigationMethod method)
    {
        var index = IndexFromContainer(tab);
        if (index < 0)
        {
            return;
        }
        _focusIndex = index;
        UpdateTabStops();
        RevealIndex(index);
        // Radix activates a tab when it receives focus in automatic mode (arrow keys and programmatic focus).
        if (ActivationMode == TabsActivationMode.Automatic && method == NavigationMethod.Directional && index != SelectedIndex)
        {
            SelectedIndex = index;
        }
    }

    private void UpdateContent(int direction, bool animate)
    {
        var index = SelectedIndex;
        object? content = null;
        IDataTemplate? template = null;
        if (index >= 0 && ContainerFromIndex(index) is TabItem tab)
        {
            content = tab.Content;
            template = tab.ContentTemplate ?? ContentTemplate;
        }
        else if (index >= 0 && index < ItemCount)
        {
            var item = Items[index];
            if (item is not TabItem)
            {
                content = item;
                template = ContentTemplate;
            }
        }
        SelectedContentTemplate = template;
        SelectedContent = content;
        _contentHost?.Show(content, template, direction, animate && IsLoaded);
    }

    // A tab can take focus and selection when it is enabled and visible (hidden items are skipped, AGENTS.md item rules).
    private bool IsTabEnabled(int index) => ContainerFromIndex(index) is not { } container || (container.IsEffectivelyEnabled && container.IsVisible);

    private void UpdateTabStops()
    {
        var count = ItemCount;
        var stop = _focusIndex >= 0 && _focusIndex < count ? _focusIndex : SelectedIndex;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is TabItem tab)
            {
                tab.IsTabStop = i == stop || (stop < 0 && i == 0);
            }
        }
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            base.OnKeyDown(e);
            return;
        }
        var tab = FindTab(source);
        if (tab is null)
        {
            base.OnKeyDown(e);
            return;
        }
        var current = IndexFromContainer(tab);
        int? target = e.Key switch
        {
            Key.Left => Step(current, -1),
            Key.Right => Step(current, 1),
            Key.Home => FirstEnabled(0, 1),
            Key.End => FirstEnabled(ItemCount - 1, -1),
            _ => null
        };
        if (target is { } index && index >= 0)
        {
            e.Handled = true;
            FocusTab(index);
            return;
        }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && current >= 0)
        {
            e.Handled = true;
            SelectedIndex = current;
            return;
        }
        if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None && current >= 0 && tab.IsEffectivelyClosable)
        {
            e.Handled = true;
            RequestClose(tab, NavigationMethod.Tab);
            return;
        }
        base.OnKeyDown(e);
    }

    private TabItem? FindTab(Visual source)
    {
        for (Visual? v = source; v is not null && v != this; v = v.GetVisualParent())
        {
            if (v is TabItem tab && IndexFromContainer(tab) >= 0)
            {
                return tab;
            }
        }
        return null;
    }

    private void FocusTab(int index)
    {
        if (ContainerFromIndex(index) is TabItem tab)
        {
            _focusIndex = index;
            UpdateTabStops();
            tab.Focus(NavigationMethod.Directional);
            if (ActivationMode == TabsActivationMode.Automatic)
            {
                SelectedIndex = index;
            }
        }
    }

    private int Step(int current, int delta)
    {
        var count = ItemCount;
        if (count == 0)
        {
            return -1;
        }
        var index = current;
        for (var i = 0; i < count; i++)
        {
            // Radix tabs loop at the ends.
            index = ((index + delta) % count + count) % count;
            if (IsTabEnabled(index))
            {
                return index;
            }
        }
        return current;
    }

    private int FirstEnabled(int start, int step)
    {
        for (var i = start; i >= 0 && i < ItemCount; i += step)
        {
            if (IsTabEnabled(i))
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region Indicator

    private void OnListArranged()
    {
        // Passive layout changes (fonts, resizes, items) snap the pill; an equal target leaves a glide in flight alone.
        UpdateIndicator(animate: false, onlyWhenMoved: true);
        UpdateOverflow();
        // The selected tab starts in view (Arc reveals it on mount), without motion.
        RevealPending();
    }

    private void UpdateIndicator(bool animate, bool onlyWhenMoved = false)
    {
        if (_pill is null || _listPanel is null)
        {
            return;
        }
        var index = SelectedIndex;
        if (index < 0 || ContainerFromIndex(index) is not { IsVisible: true } container || container.Bounds.Width <= 0)
        {
            _pillTarget = default;
            _pill.Hide(animate: false);
            return;
        }
        var origin = container.TranslatePoint(default, _listPanel);
        if (origin is null)
        {
            return;
        }
        var target = new Rect(origin.Value, container.Bounds.Size).Deflate(0.5);
        if (Variant == TabsVariant.Card)
        {
            // The card runs past the bottom of the list, over the strip's baseline; the viewport clips its bottom edge, so it
            // reads as open into the panel below.
            target = new Rect(target.X, target.Y, target.Width, target.Height + container.Margin.Bottom + CardIndicatorOverhang);
        }
        if (onlyWhenMoved && _pill.HasTarget && Near(target, _pillTarget))
        {
            return;
        }
        _pillTarget = target;
        _pill.MoveTo(target, animate && IsLoaded, ArcSpring.Morph);
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    #endregion

    #region Overflow scrolling

    private void OnViewportScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateOverflow();
        // The list's extent catches up after the list arranges; a reveal that waited for it runs now.
        RevealPending();
    }

    private void RevealPending()
    {
        if (_revealPending && SelectedIndex >= 0 && RevealIndex(SelectedIndex, _revealPendingInstant))
        {
            _revealPending = false;
        }
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateOverflow();

    private void OnViewportWheel(object? sender, PointerWheelEventArgs e) => _scroll?.Stop();

    private void UpdateOverflow()
    {
        if (_viewport is null)
        {
            return;
        }
        var extent = _viewport.Extent.Width;
        var width = _viewport.Viewport.Width;
        var max = Math.Max(0, extent - width);
        var offset = _viewport.Offset.X;
        var overflow = extent > width + 1;
        var start = overflow && offset > 1;
        var end = overflow && offset < max - 1;
        var built = overflow && _scrollLeft is null && EnsureScrollButtons();
        if (built || overflow != _overflow || start != _scrolledStart || end != _scrolledEnd)
        {
            _overflow = overflow;
            _scrolledStart = start;
            _scrolledEnd = end;
            PseudoClasses.Set(OverflowPseudoClass, overflow);
            PseudoClasses.Set(ScrolledStartPseudoClass, start);
            PseudoClasses.Set(ScrolledEndPseudoClass, end);
            UpdateScrollButton(_scrollLeft, overflow, start);
            UpdateScrollButton(_scrollRight, overflow, end);
        }
        UpdateEdgeMask(width);
    }

    private void UpdateEdgeMask(double width)
    {
        if (_viewport is null)
        {
            return;
        }
        if ((!_scrolledStart && !_scrolledEnd) || width <= 0)
        {
            _viewport.OpacityMask = null;
            _edgeMaskKey = default;
            return;
        }
        var fade = Math.Round(Math.Min(0.5, EdgeFade / width), 4);
        var key = (_scrolledStart, _scrolledEnd, fade);
        if (_viewport.OpacityMask is not null && key == _edgeMaskKey)
        {
            return;
        }
        _edgeMaskKey = key;
        // A new immutable brush only when the edges or the width change; scrolling in between allocates nothing.
        var stops = new[]
        {
            new ImmutableGradientStop(0, _scrolledStart ? Colors.Transparent : Colors.Black),
            new ImmutableGradientStop(_scrolledStart ? fade : 0, Colors.Black),
            new ImmutableGradientStop(_scrolledEnd ? 1 - fade : 1, Colors.Black),
            new ImmutableGradientStop(1, _scrolledEnd ? Colors.Transparent : Colors.Black)
        };
        _viewport.OpacityMask = new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative));
    }

    /// <summary>
    /// Builds the two scroll buttons over the ends of the list. Done in code (not in the template) because most tab lists
    /// never overflow, and two templated buttons per Tabs cost more than the rest of the list header.
    /// </summary>
    private bool EnsureScrollButtons()
    {
        if (_viewport?.Parent is not Panel host)
        {
            return false;
        }
        _scrollLeft = CreateScrollButton(ScrollLeftPart, ArcIcons.ChevronLeft, HorizontalAlignment.Left, TabsTokenKind.ScrollStartRadius, ScrollLeftLabelProperty);
        _scrollRight = CreateScrollButton(ScrollRightPart, ArcIcons.ChevronRight, HorizontalAlignment.Right, TabsTokenKind.ScrollEndRadius, ScrollRightLabelProperty);
        _scrollLeft.Click += OnScrollLeftClick;
        _scrollRight.Click += OnScrollRightClick;
        UpdateScrollButtonVariant(_scrollLeft);
        UpdateScrollButtonVariant(_scrollRight);
        host.Children.Add(_scrollLeft);
        host.Children.Add(_scrollRight);
        return true;
    }

    private Button CreateScrollButton(string name, Geometry glyph, HorizontalAlignment alignment, TabsTokenKind radius, StyledProperty<string> label)
    {
        var icon = new ArcGlyph { Data = glyph, StrokeThickness = 2 };
        icon.Bind(ArcGlyph.SizeProperty, icon.GetResourceObservable(TabsTokens.Key(TabsTokenKind.ScrollGlyphSize)));
        // Local values: a ghost button's hover and press fills would otherwise show over the list's muted shell.
        var button = new Button
        {
            Name = name,
            Variant = ButtonVariant.Ghost,
            IsVisible = false,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 0,
            Padding = default,
            BorderThickness = default,
            IsPressScaleEnabled = false,
            Content = icon
        };
        button.Bind(WidthProperty, button.GetResourceObservable(TabsTokens.Key(TabsTokenKind.ScrollButtonWidth)), BindingPriority.LocalValue);
        button.Bind(Button.CornerRadiusProperty, button.GetResourceObservable(TabsTokens.Key(radius)), BindingPriority.LocalValue);
        ArcTokens.Bind(button, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted, BindingPriority.LocalValue);
        ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.Foreground, BindingPriority.LocalValue);
        button.Bind(Avalonia.Automation.AutomationProperties.NameProperty, this.GetObservable(label));
        return button;
    }

    // A disabled end button (the list is scrolled to that end) is invisible and lets the pointer through.
    private static void UpdateScrollButton(AvaloniaButton? button, bool overflow, bool enabled)
    {
        if (button is null)
        {
            return;
        }
        button.IsVisible = overflow;
        button.IsEnabled = enabled;
        button.Opacity = enabled ? 1 : 0;
        button.IsHitTestVisible = enabled;
    }

    private void OnScrollLeftClick(object? sender, RoutedEventArgs e) => ScrollList(-1);

    private void OnScrollRightClick(object? sender, RoutedEventArgs e) => ScrollList(1);

    private void ScrollList(int direction)
    {
        if (_viewport is null)
        {
            return;
        }
        var from = _scroll is { IsAnimating: true } running ? running.Target : _viewport.Offset.X;
        ScrollTo(from + direction * _viewport.Viewport.Width * 0.75);
    }

    /// <summary>
    /// Scrolls the list so the tab at <paramref name="index"/> is in view (clear of the scroll buttons). False when that
    /// cannot be worked out yet: the tab is not laid out, or the viewport's extent does not cover it yet.
    /// </summary>
    private bool RevealIndex(int index, bool instant = false)
    {
        if (_viewport is null || index < 0 || ContainerFromIndex(index) is not { } container || container.Bounds.Width <= 0)
        {
            return false;
        }
        var origin = container.TranslatePoint(default, _viewport);
        if (origin is null)
        {
            return false;
        }
        var offset = _viewport.Offset.X;
        var itemLeft = origin.Value.X;
        var itemRight = itemLeft + container.Bounds.Width;
        if (offset + itemRight > _viewport.Extent.Width + 0.5)
        {
            // The scroll viewer updates its extent after the list arranges; until then a reveal would clamp to a stale end.
            return false;
        }
        var max = Math.Max(0, _viewport.Extent.Width - _viewport.Viewport.Width);
        var left = offset > 1 ? RevealInset : 0;
        var right = _viewport.Viewport.Width - (offset < max - 1 ? RevealInset : 0);
        var delta = itemLeft < left ? itemLeft - left : itemRight > right ? itemRight - right : 0;
        if (Math.Abs(delta) > 0.5)
        {
            if (instant)
            {
                _scroll?.Stop();
                _viewport.Offset = new Vector(Math.Clamp(offset + delta, 0, max), _viewport.Offset.Y);
            }
            else
            {
                ScrollTo(offset + delta);
            }
        }
        return true;
    }

    private void ScrollTo(double x)
    {
        if (_viewport is null)
        {
            return;
        }
        var max = Math.Max(0, _viewport.Extent.Width - _viewport.Viewport.Width);
        var target = Math.Clamp(x, 0, max);
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, _viewport.Offset.X, MotionVectors.Double);
            _scroll.Changed += value =>
            {
                _viewport?.Offset = new Vector(value, _viewport.Offset.Y);
            };
        }
        if (!_scroll.IsAnimating)
        {
            _scroll.Jump(_viewport.Offset.X);
        }
        // P30: smooth scroll on the smooth spring; Reduced and Off jump (MotionValue honors the level).
        _ = _scroll.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new TabsAutomationPeer(this);

    private sealed class TabsAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public TabsAutomationPeer(Tabs owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
