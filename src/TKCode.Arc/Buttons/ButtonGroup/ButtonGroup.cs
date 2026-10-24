using System.Diagnostics;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Surface of a <see cref="ButtonGroup"/>.</summary>
public enum ButtonGroupVariant
{
    /// <summary>Surface with a border (default).</summary>
    Outline,

    /// <summary>The foreground fill of a primary button, for the main actions of a screen (use at most once per screen).</summary>
    Solid
}

/// <summary>Arguments of <see cref="ButtonGroup.ActionInvoked"/>.</summary>
public sealed class ButtonGroupActionEventArgs : RoutedEventArgs
{
    public ButtonGroupActionEventArgs(RoutedEvent routedEvent, ButtonGroupItem item, object? data) : base(routedEvent)
    {
        Item = item;
        Data = data;
    }

    /// <summary>The segment that was activated.</summary>
    public ButtonGroupItem Item { get; }

    /// <summary>The source item (the segment itself for inline items, the data object for <c>ItemsSource</c>).</summary>
    public object? Data { get; }
}

/// <summary>
/// Two to five related actions joined into one surface with hairline dividers (Arc button group): Share, Duplicate and
/// Archive on a document, zoom controls, Reply / Reply all / Forward. One soft highlight glides between segments under
/// the pointer or keyboard focus (the dividers beside it step aside), the pressed segment answers in place, and an
/// optional chevron segment (<see cref="Menu"/>) opens rarer or destructive actions in a menu aligned to the group's edge.
/// <para>
/// Segments are <see cref="ButtonGroupItem"/>s declared inline or generated from <c>ItemsSource</c> (plain data becomes
/// the label). When a horizontal row does not fit, segments with an icon drop their label (it moves to the tooltip and
/// the accessible name); if it still does not fit the row scrolls sideways without a scrollbar.
/// </para>
/// <para>
/// Keyboard: every segment is a Tab stop; the arrow keys along the orientation (mirrored right to left), Home and End
/// move between segments and wrap; Enter or Space activates. On the chevron, Enter, Space or ArrowDown opens the menu on
/// the first row and Escape returns focus. A label that changes right after its segment was pressed (Share to Copied)
/// is announced once.
/// </para>
/// </summary>
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(MenuButtonPart, typeof(Button))]
[TemplatePart(RowPart, typeof(StackPanel))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(VerticalPseudoClass, SmallPseudoClass, SolidPseudoClass, CompactPseudoClass, PressingPseudoClass,
    MenuOpenPseudoClass, HasMenuPseudoClass)]
public class ButtonGroup : ItemsControl
{
    public const string HighlightPart = "PART_Highlight";
    public const string MenuButtonPart = "PART_MenuButton";
    public const string RowPart = "PART_Row";
    public const string StatusPart = "PART_Status";
    public const string VerticalPseudoClass = ":vertical";
    public const string SmallPseudoClass = ":small";
    public const string SolidPseudoClass = ":solid";
    public const string CompactPseudoClass = ":compact";
    public const string PressingPseudoClass = ":pressing";
    public const string MenuOpenPseudoClass = ":menu-open";
    public const string HasMenuPseudoClass = ":has-menu";

    /// <summary>The visible highlight sits 3px inside its segment, so the group border stays crisp.</summary>
    private const double HighlightInset = 3;

    /// <summary>A segment that resizes shortly after the highlight started travelling keeps the spring; later ones snap.</summary>
    private static readonly TimeSpan FollowWindow = TimeSpan.FromMilliseconds(600);

    #region 公共属性定义

    /// <summary>Accessible name of the group (Arc: required), such as "Document actions".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ButtonGroup, string>(nameof(Label), "Actions");

    /// <summary>Outline (default) for a bordered surface, or Solid for the main action of a screen (use at most once).</summary>
    public static readonly StyledProperty<ButtonGroupVariant> VariantProperty =
        AvaloniaProperty.Register<ButtonGroup, ButtonGroupVariant>(nameof(Variant));

    /// <summary>Small (36px) or Medium (44px, default). Large is treated as Medium.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ButtonGroup, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>Lays the segments out in a row (Horizontal, default) or a column (Vertical).</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ButtonGroup, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>When a horizontal row does not fit, segments with an icon drop their label (default true).</summary>
    public static readonly StyledProperty<bool> IsLabelCollapseEnabledProperty =
        AvaloniaProperty.Register<ButtonGroup, bool>(nameof(IsLabelCollapseEnabled), true);

    /// <summary>Adds a trailing chevron segment that opens this menu, aligned to the group's end (start when vertical).</summary>
    public static readonly StyledProperty<MenuSurface?> MenuProperty =
        AvaloniaProperty.Register<ButtonGroup, MenuSurface?>(nameof(Menu));

    /// <summary>Accessible name and tooltip of the chevron segment.</summary>
    public static readonly StyledProperty<string> MenuLabelProperty =
        AvaloniaProperty.Register<ButtonGroup, string>(nameof(MenuLabel), "More actions");

    public static readonly DirectProperty<ButtonGroup, bool> AreLabelsCollapsedProperty =
        AvaloniaProperty.RegisterDirect<ButtonGroup, bool>(nameof(AreLabelsCollapsed), o => o.AreLabelsCollapsed);

    /// <summary>Menu minimum width (theme: 13rem).</summary>
    internal static readonly StyledProperty<double> MenuMinWidthProperty =
        AvaloniaProperty.Register<ButtonGroup, double>(nameof(MenuMinWidth), 208);

    /// <summary>Raised after a segment's own click handlers and command.</summary>
    public static readonly RoutedEvent<ButtonGroupActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<ButtonGroup, ButtonGroupActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a row of the chevron menu is activated.</summary>
    public static readonly RoutedEvent<MenuEntryClickedEventArgs> MenuItemClickedEvent =
        RoutedEvent.Register<ButtonGroup, MenuEntryClickedEventArgs>(nameof(MenuItemClicked), RoutingStrategies.Bubble);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public ButtonGroupVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    public ControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    public bool IsLabelCollapseEnabled { get => GetValue(IsLabelCollapseEnabledProperty); set => SetValue(IsLabelCollapseEnabledProperty, value); }

    public MenuSurface? Menu { get => GetValue(MenuProperty); set => SetValue(MenuProperty, value); }

    public string MenuLabel { get => GetValue(MenuLabelProperty); set => SetValue(MenuLabelProperty, value); }

    private bool _labelsCollapsed;

    /// <summary>True while segments show only their icons because the row did not fit.</summary>
    public bool AreLabelsCollapsed
    {
        get => _labelsCollapsed;
        private set => SetAndRaise(AreLabelsCollapsedProperty, ref _labelsCollapsed, value);
    }

    internal double MenuMinWidth { get => GetValue(MenuMinWidthProperty); set => SetValue(MenuMinWidthProperty, value); }

    public event EventHandler<ButtonGroupActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    public event EventHandler<MenuEntryClickedEventArgs>? MenuItemClicked
    {
        add => AddHandler(MenuItemClickedEvent, value);
        remove => RemoveHandler(MenuItemClickedEvent, value);
    }

    #endregion

    private readonly List<Control> _segments = new();
    private readonly List<Control> _navigable = new();
    private GlidingIndicator? _highlight;
    private ButtonsChevronTrigger? _menuButton;
    // The menu listened to (only while attached) and whether the group is in a visual tree.
    private MenuSurface? _watchedMenu;
    private bool _attached;
    private StackPanel? _row;
    private ButtonsLiveRegion? _status;
    private Control? _hover;
    private Control? _pressed;
    private Control? _focused;
    private Control? _shown;
    private long _travelStamp;
    private ButtonGroupItem? _activated;
    private long _activatedStamp;
    private double _fullWidth;
    private double _blockedAt;

    static ButtonGroup()
    {
        FocusableProperty.OverrideDefaultValue<ButtonGroup>(false);
    }

    public ButtonGroup()
    {
        AddHandler(AvaloniaButton.ClickEvent, OnSegmentClick);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnAnyPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    internal bool IsSmall => Size == ControlSize.Small;

    /// <summary>The segment that holds the highlight: pointer, else the pressed one (touch), else the open menu's chevron, else keyboard focus.</summary>
    internal Control? ActiveSegment => _hover ?? _pressed ?? (Menu?.IsOpen == true ? _menuButton : null) ?? _focused;

    /// <summary>The chevron segment (null before the template is applied).</summary>
    public Control? MenuAnchor => _menuButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_menuButton is not null)
        {
            _menuButton.Menu = null;
            _menuButton.GotFocus -= OnChevronGotFocus;
            _menuButton.PropertyChanged -= OnSegmentPropertyChanged;
        }
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _menuButton = e.NameScope.Find<ButtonsChevronTrigger>(MenuButtonPart);
        _row = e.NameScope.Find<StackPanel>(RowPart);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
        if (_menuButton is not null)
        {
            _menuButton.GotFocus += OnChevronGotFocus;
            _menuButton.PropertyChanged += OnSegmentPropertyChanged;
        }
        SyncMenu();
        SyncLayout();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        AutomationProperties.SetName(this, Label);
        SyncLayout();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == SizeProperty || change.Property == OrientationProperty || change.Property == VariantProperty)
        {
            SyncLayout();
            SyncMenu();
        }
        else if (change.Property == IsLabelCollapseEnabledProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == MenuProperty)
        {
            WatchMenu(_attached ? change.GetNewValue<MenuSurface?>() : null);
            SyncMenu();
            Place();
        }
        else if (change.Property == MenuLabelProperty || change.Property == MenuMinWidthProperty)
        {
            SyncMenu();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>())
        {
            _focused = null;
            Place();
        }
    }

    private void SyncLayout()
    {
        var vertical = Orientation == Orientation.Vertical;
        PseudoClasses.Set(VerticalPseudoClass, vertical);
        PseudoClasses.Set(SmallPseudoClass, IsSmall);
        PseudoClasses.Set(SolidPseudoClass, Variant == ButtonGroupVariant.Solid);
        if (vertical && AreLabelsCollapsed)
        {
            SetCollapsed(false);
        }
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is ButtonGroupItem item)
            {
                item.SyncFromGroup();
            }
        }
        if (_menuButton is { } chevron)
        {
            chevron.Classes.Set("vertical", vertical);
            chevron.Classes.Set("small", IsSmall);
            chevron.Classes.Set("solid", Variant == ButtonGroupVariant.Solid);
        }
        Place();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        WatchMenu(Menu);
        // The menu may have opened or closed while the group was away.
        PseudoClasses.Set(MenuOpenPseudoClass, Menu?.IsOpen == true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        WatchMenu(null);
    }

    /// <summary>The menu is the app's and can outlive the group: the group listens to it only while attached.</summary>
    private void WatchMenu(MenuSurface? menu)
    {
        if (_watchedMenu is { } old)
        {
            old.ItemClicked -= OnMenuItemClicked;
            old.PropertyChanged -= OnMenuPropertyChanged;
        }
        _watchedMenu = menu;
        if (menu is not null)
        {
            menu.ItemClicked += OnMenuItemClicked;
            menu.PropertyChanged += OnMenuPropertyChanged;
        }
    }

    private void SyncMenu()
    {
        var menu = Menu;
        PseudoClasses.Set(HasMenuPseudoClass, menu is not null);
        if (menu is not null)
        {
            // A non-modal dropdown 6px below the chevron, aligned to the group's end (start for a column).
            menu.Side = MenuSide.Bottom;
            menu.Alignment = Orientation == Orientation.Vertical ? MenuAlignment.Start : MenuAlignment.End;
            menu.AnchorGap = 6;
            menu.SetValue(MinWidthProperty, MenuMinWidth, BindingPriority.Template);
            AutomationProperties.SetName(menu, MenuLabel);
        }
        if (_menuButton is { } chevron)
        {
            chevron.Menu = menu;
            chevron.IsVisible = menu is not null;
            AutomationProperties.SetName(chevron, MenuLabel);
            Tooltip.SetTip(chevron, MenuLabel);
            Tooltip.SetSide(chevron, Orientation == Orientation.Vertical ? TooltipSide.Right : TooltipSide.Top);
        }
        PseudoClasses.Set(MenuOpenPseudoClass, menu?.IsOpen == true);
    }

    #region 容器

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is ButtonGroupItem)
        {
            recycleKey = null;
            return false;
        }
        recycleKey = DefaultRecycleKey;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new ButtonGroupItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is ButtonGroupItem segment)
        {
            if (!ReferenceEquals(item, segment) && string.IsNullOrEmpty(segment.Label))
            {
                segment.Label = item?.ToString() ?? string.Empty;
            }
            segment.Group = this;
            segment.PropertyChanged += OnSegmentPropertyChanged;
            segment.SyncFromGroup();
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        Place();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is ButtonGroupItem segment)
        {
            segment.PropertyChanged -= OnSegmentPropertyChanged;
            segment.Group = null;
            if (ReferenceEquals(_hover, segment))
            {
                _hover = null;
            }
            if (ReferenceEquals(_pressed, segment))
            {
                _pressed = null;
            }
            if (ReferenceEquals(_focused, segment))
            {
                _focused = null;
            }
            if (ReferenceEquals(_activated, segment))
            {
                _activated = null;
            }
            Place();
        }
    }

    /// <summary>The visible segments in order, chevron last. Reuses one list (no allocation per pointer move).</summary>
    private List<Control> Segments()
    {
        _segments.Clear();
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is ButtonGroupItem { IsVisible: true } segment)
            {
                _segments.Add(segment);
            }
        }
        if (_menuButton is { IsVisible: true } chevron)
        {
            _segments.Add(chevron);
        }
        return _segments;
    }

    private static bool IsInert(Control segment) => segment is ButtonGroupItem item ? item.IsInert : !segment.IsEffectivelyEnabled;

    #endregion

    #region 高亮

    private Rect? Box(Control segment)
    {
        if (_highlight is null || segment.TranslatePoint(default, _highlight) is not { } origin)
        {
            return null;
        }
        return new Rect(origin, segment.Bounds.Size).Deflate(HighlightInset);
    }

    /// <summary>
    /// Moves the highlight to the active segment: it appears in place (fading in) when arriving from nowhere and travels
    /// on the snappy spring between segments; the dividers on both sides of the active segment fade.
    /// </summary>
    private void Place()
    {
        var segments = Segments();
        var active = ActiveSegment;
        var activeIndex = active is null ? -1 : segments.IndexOf(active);
        for (var i = 0; i < segments.Count; i++)
        {
            var joined = i > 0;
            var quiet = activeIndex >= 0 && (i == activeIndex || i == activeIndex + 1);
            if (segments[i] is ButtonGroupItem item)
            {
                item.SetSegmentState(joined, quiet, i == activeIndex);
            }
            else
            {
                segments[i].Classes.Set("joined", joined);
                segments[i].Classes.Set("quiet", quiet);
            }
        }
        PseudoClasses.Set(PressingPseudoClass, _pressed is not null && ReferenceEquals(_pressed, active));
        if (_highlight is null)
        {
            return;
        }
        // Hovering or pressing a disabled segment shows nothing; keyboard focus on one still shows where the keyboard is.
        if (active is null || activeIndex < 0 || (IsInert(active) && !ReferenceEquals(active, _focused)) || Box(active) is not { } target)
        {
            _highlight.Hide(IsLoaded);
            _shown = null;
            return;
        }
        var full = IsLoaded && ArcMotion.IsFull(this);
        if (_shown is null || !_highlight.HasTarget)
        {
            _highlight.MoveTo(target, IsLoaded);
        }
        else if (!ReferenceEquals(active, _shown))
        {
            _highlight.MoveTo(target, full, ArcSpring.Snappy);
            _travelStamp = Stopwatch.GetTimestamp();
        }
        else
        {
            _highlight.MoveTo(target, full && Stopwatch.GetElapsedTime(_travelStamp) < FollowWindow, ArcSpring.Snappy);
        }
        _shown = active;
    }

    /// <summary>Segments resize when a label morphs or labels collapse; the highlight stays locked to its segment.</summary>
    private void Follow(Control segment)
    {
        if (_highlight is null || !ReferenceEquals(segment, _shown) || Box(segment) is not { } target)
        {
            return;
        }
        var traveling = IsLoaded && ArcMotion.IsFull(this) && Stopwatch.GetElapsedTime(_travelStamp) < FollowWindow;
        _highlight.MoveTo(target, traveling, ArcSpring.Snappy);
    }

    private void OnSegmentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Control segment)
        {
            return;
        }
        if (e.Property == BoundsProperty)
        {
            Follow(segment);
        }
        else if (e.Property == AvaloniaButton.IsPressedProperty)
        {
            if (!e.GetNewValue<bool>() && ReferenceEquals(segment, _pressed))
            {
                _pressed = null;
                Place();
            }
        }
        else if (e.Property == IsEffectivelyEnabledProperty || e.Property == ButtonGroupItem.IsDisabledProperty ||
                 e.Property == IsVisibleProperty)
        {
            // A segment that turns disabled under a still pointer drops the highlight.
            Place();
        }
        else if (e.Property == ButtonGroupItem.LabelProperty && segment is ButtonGroupItem item)
        {
            // A label that changes right after its segment was pressed (Copied) is announced once; the quiet revert is not.
            if (ReferenceEquals(item, _activated) && item.Value is null &&
                Stopwatch.GetElapsedTime(_activatedStamp) < TimeSpan.FromSeconds(1))
            {
                _status?.Announce(item.Label);
            }
        }
        else if (e.Property == ButtonGroupItem.IconProperty || e.Property == ButtonGroupItem.IsIconOnlyProperty ||
                 e.Property == ButtonGroupItem.ValueProperty)
        {
            InvalidateMeasure();
        }
    }

    private Control? HitSegment(PointerEventArgs e)
    {
        foreach (var segment in Segments())
        {
            var point = e.GetPosition(segment);
            if (point.X >= 0 && point.Y >= 0 && point.X < segment.Bounds.Width && point.Y < segment.Bounds.Height)
            {
                return IsInert(segment) ? null : segment;
            }
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Touch has no hover; it gets the highlight from the press.
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var hit = HitSegment(e);
        if (!ReferenceEquals(hit, _hover))
        {
            _hover = hit;
            Place();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover is not null)
        {
            _hover = null;
            Place();
        }
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var hit = HitSegment(e);
        if (!ReferenceEquals(hit, _pressed))
        {
            _pressed = hit;
            Place();
        }
    }

    private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressed is not null)
        {
            _pressed = null;
            Place();
        }
    }

    #endregion

    #region 焦点与键盘

    /// <summary>Keyboard focus shows the highlight; a click focuses without it, so nothing stays lit after the pointer leaves.</summary>
    internal void OnSegmentFocused(Control segment, NavigationMethod method)
    {
        _focused = method is NavigationMethod.Tab or NavigationMethod.Directional ? segment : null;
        Place();
    }

    private void OnChevronGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is Control chevron)
        {
            OnSegmentFocused(chevron, e.NavigationMethod);
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        // Disabled segments stay focusable (aria-disabled) so the keyboard can find them; a disabled group has no stops.
        _navigable.Clear();
        var index = -1;
        foreach (var segment in Segments())
        {
            if (!segment.IsEffectivelyEnabled)
            {
                continue;
            }
            if (segment.IsFocused)
            {
                index = _navigable.Count;
            }
            _navigable.Add(segment);
        }
        if (index < 0)
        {
            return;
        }
        var vertical = Orientation == Orientation.Vertical;
        var rtl = !vertical && FlowDirection == FlowDirection.RightToLeft;
        var forward = vertical ? Key.Down : rtl ? Key.Left : Key.Right;
        var backward = vertical ? Key.Up : rtl ? Key.Right : Key.Left;
        // ArrowDown on the chevron opens its menu.
        if (e.Key == Key.Down && ReferenceEquals(_navigable[index], _menuButton))
        {
            return;
        }
        var last = _navigable.Count - 1;
        var target = e.Key == forward ? (index == last ? 0 : index + 1)
            : e.Key == backward ? (index == 0 ? last : index - 1)
            : e.Key == Key.Home ? 0
            : e.Key == Key.End ? last
            : -1;
        if (target < 0)
        {
            return;
        }
        e.Handled = true;
        _navigable[target].Focus(NavigationMethod.Directional);
    }

    #endregion

    #region 标签折叠

    private bool CanCollapse
    {
        get
        {
            if (!IsLabelCollapseEnabled || Orientation == Orientation.Vertical)
            {
                return false;
            }
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is ButtonGroupItem { IsCollapsible: true })
                {
                    return true;
                }
            }
            return false;
        }
    }

    private void SetCollapsed(bool collapsed)
    {
        if (AreLabelsCollapsed == collapsed)
        {
            return;
        }
        AreLabelsCollapsed = collapsed;
        PseudoClasses.Set(CompactPseudoClass, collapsed);
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is ButtonGroupItem item)
            {
                item.Rebuild();
            }
        }
    }

    /// <summary>
    /// Labels collapse to icons when the row does not fit the width it is given, and come back once that width is enough
    /// for the full row again (remembering the width that forced the collapse, so the row never flickers between states).
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (!CanCollapse)
        {
            if (AreLabelsCollapsed)
            {
                SetCollapsed(false);
                size = base.MeasureOverride(availableSize);
            }
            return size;
        }
        if (double.IsInfinity(availableSize.Width) || _row is null)
        {
            return size;
        }
        var available = availableSize.Width;
        if (!AreLabelsCollapsed)
        {
            var natural = _row.DesiredSize.Width + BorderThickness.Left + BorderThickness.Right;
            if (natural > available + 1)
            {
                _fullWidth = natural + 2;
                _blockedAt = available;
                SetCollapsed(true);
                size = base.MeasureOverride(availableSize);
            }
        }
        else if (available > _blockedAt && available >= _fullWidth)
        {
            SetCollapsed(false);
            size = base.MeasureOverride(availableSize);
        }
        return size;
    }

    #endregion

    internal void OnSegmentActivating(ButtonGroupItem item)
    {
        _activated = item;
        _activatedStamp = Stopwatch.GetTimestamp();
    }

    private void OnSegmentClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is ButtonGroupItem item && ReferenceEquals(item.Group, this))
        {
            var data = ItemFromContainer(item) ?? item;
            RaiseEvent(new ButtonGroupActionEventArgs(ActionInvokedEvent, item, data));
        }
    }

    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MenuSurface.IsOpenProperty)
        {
            PseudoClasses.Set(MenuOpenPseudoClass, e.GetNewValue<bool>());
            Place();
        }
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        RaiseEvent(new MenuEntryClickedEventArgs(MenuItemClickedEvent, e.Entry, e.Item));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ButtonGroupAutomationPeer(this);

    private sealed class ButtonGroupAutomationPeer : ItemsControlAutomationPeer
    {
        public ButtonGroupAutomationPeer(ButtonGroup owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
