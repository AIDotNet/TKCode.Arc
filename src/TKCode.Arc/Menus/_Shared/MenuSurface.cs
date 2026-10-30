using System.Collections;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The shared Arc menu surface: a raised panel of <see cref="MenuEntry"/> rows (plus <see cref="MenuEntrySeparator"/>
/// and <see cref="MenuEntryLabel"/>) anchored to any control through Core <see cref="ArcPopup"/>. One highlight glides
/// between rows on a snappy spring for the pointer and jumps for the keyboard (with a 70ms grace across gaps), rows
/// fade in with Arc's short stagger, and the full menu keyboard model is built in: ArrowUp / ArrowDown (looping, disabled
/// rows skipped), Home / End, typeahead, Enter / Space to activate, Escape or Tab to close and return focus to the anchor.
/// <para>
/// Use it directly to give any control a menu (split buttons, button groups, swipe rows), or through
/// <see cref="DropdownMenu"/>, <see cref="ContextMenu"/> and <see cref="UserMenu"/>. Items can be declared inline or
/// supplied with <see cref="ItemsControl.ItemsSource"/> (<see cref="MenuEntry"/> instances are used as is; other data
/// becomes the header of a generated row). The popup and the row visuals are created on first open and reused.
/// A surface placed directly in a layout works as a static menu panel (highlight, keyboard and activation without a popup).
/// </para>
/// <para>
/// Rows with child rows open submenus: each submenu is a surface of its own, floating right of the menu with its first
/// row level with the row that opened it (left when there is no room). Hovering a submenu row opens it after a short
/// delay, and the pointer can cross other rows on its way into the submenu. ArrowRight, Enter or Space open it on its
/// first row, ArrowLeft or Escape close only that level, Tab closes them all, and activating a row anywhere closes the
/// whole menu and raises <see cref="ItemClicked"/> on this (the outermost) surface.
/// </para>
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(ScrollViewerPart, typeof(ScrollViewer))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(ItemsHostPart, typeof(Panel))]
[PseudoClasses(OpenPseudoClass, DangerHighlightPseudoClass)]
public class MenuSurface : HeaderedItemsControl
{
    public const string FramePart = "PART_Frame";
    public const string ScrollViewerPart = "PART_ScrollViewer";
    public const string HighlightPart = "PART_Highlight";
    public const string ItemsHostPart = "PART_ItemsHost";
    public const string OpenPseudoClass = ":open";
    public const string DangerHighlightPseudoClass = ":danger-highlight";

    private static readonly TimeSpan HighlightGrace = TimeSpan.FromMilliseconds(70);

    // A submenu opens when the pointer rests on its row this long (passing over the row does not open it).
    internal static readonly TimeSpan SubmenuHoverDelay = TimeSpan.FromMilliseconds(100);

    // Space between a menu and a submenu beside it.
    internal const double SubmenuGap = 4;

    // Space kept between a floating menu and the window edge when its height is clamped to the window.
    private const double WindowEdgeMargin = 8;

    // Rows that stagger in when the surface has not been laid out yet (no viewport to test against).
    private const int UnmeasuredStaggerRows = 12;

    #region 公共属性定义

    /// <summary>Geometry and motion family (inherited by the rows). Defaults to <see cref="MenuSurfaceVariant.Dropdown"/>.</summary>
    public static readonly StyledProperty<MenuSurfaceVariant> VariantProperty =
        AvaloniaProperty.Register<MenuSurface, MenuSurfaceVariant>(nameof(Variant), inherits: true);

    /// <summary>Whether the menu is open (two-way). Opening needs a <see cref="PlacementTarget"/>.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MenuSurface, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The anchor. Presses on it pass through the light-dismiss layer so a trigger can toggle the menu.</summary>
    public static readonly StyledProperty<Control?> PlacementTargetProperty =
        AvaloniaProperty.Register<MenuSurface, Control?>(nameof(PlacementTarget));

    public static readonly StyledProperty<MenuSide> SideProperty =
        AvaloniaProperty.Register<MenuSurface, MenuSide>(nameof(Side), MenuSide.Bottom);

    public static readonly StyledProperty<MenuAlignment> AlignmentProperty =
        AvaloniaProperty.Register<MenuSurface, MenuAlignment>(nameof(Alignment), MenuAlignment.End);

    /// <summary>Gap between the anchor and the menu (Arc dropdown: 6, split button: 4, user menu: 8).</summary>
    public static readonly StyledProperty<double> AnchorGapProperty =
        AvaloniaProperty.Register<MenuSurface, double>(nameof(AnchorGap), 6);

    /// <summary>Element that receives focus when the menu closes from the keyboard or a selection (default: the anchor).</summary>
    public static readonly StyledProperty<IInputElement?> FocusReturnTargetProperty =
        AvaloniaProperty.Register<MenuSurface, IInputElement?>(nameof(FocusReturnTarget));

    /// <summary>Closes on any scroll or window resize instead of following the anchor (Arc context menu).</summary>
    public static readonly StyledProperty<bool> ClosesOnViewportChangeProperty =
        AvaloniaProperty.Register<MenuSurface, bool>(nameof(ClosesOnViewportChange));

    /// <summary>
    /// Children of an <see cref="ItemsControl.ItemsSource"/> item: a non-empty result turns its row into a submenu row
    /// (same signature as <c>TreeView.ChildrenSelector</c>). Submenus use it for their own items too.
    /// </summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<MenuSurface, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Raised when a row is activated (before the menu closes); rows of submenus raise it on the outermost surface.</summary>
    public static readonly RoutedEvent<MenuEntryClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<MenuSurface, MenuEntryClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<MenuSurface, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Direct);

    /// <summary>Raised when the menu has closed (after its exit motion).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<MenuSurface, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Direct);

    public MenuSurfaceVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public Control? PlacementTarget
    {
        get => GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public MenuSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public MenuAlignment Alignment
    {
        get => GetValue(AlignmentProperty);
        set => SetValue(AlignmentProperty, value);
    }

    public double AnchorGap
    {
        get => GetValue(AnchorGapProperty);
        set => SetValue(AnchorGapProperty, value);
    }

    public IInputElement? FocusReturnTarget
    {
        get => GetValue(FocusReturnTargetProperty);
        set => SetValue(FocusReturnTargetProperty, value);
    }

    public bool ClosesOnViewportChange
    {
        get => GetValue(ClosesOnViewportChangeProperty);
        set => SetValue(ClosesOnViewportChangeProperty, value);
    }

    /// <inheritdoc cref="ChildrenSelectorProperty"/>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => GetValue(ChildrenSelectorProperty);
        set => SetValue(ChildrenSelectorProperty, value);
    }

    public event EventHandler<MenuEntryClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    private readonly List<MenuEntry> _stops = new();
    private readonly ListKeyboardNavigator _navigator;
    private MenuSurfacePopup? _popup;
    private Border? _frame;
    private ScrollViewer? _scrollViewer;
    private GlidingIndicator? _highlight;
    private Panel? _itemsHost;
    private Point? _lastPointer;
    private double _windowRoom = double.PositiveInfinity;
    private MenuEntry? _active;
    private DispatcherTimer? _graceTimer;
    private MenuOpenFocus _pendingFocus;
    private Point? _anchorPoint;
    private bool _popupOpen;
    private bool _closing;
    private bool _reopenPending;
    private bool _keyboardModality;
    private TopLevel? _dismissTopLevel;
    private MenuSurfaceSheet? _sheet;

    // Submenus: the row this surface belongs to (null for the outermost surface), the row whose submenu is open, the row
    // waiting for the hover delay, and the safe zone the pointer crosses on its way into an open submenu.
    private MenuEntry? _parentRow;
    private MenuEntry? _openRow;
    private MenuEntry? _hoverRow;
    private DispatcherTimer? _hoverTimer;
    private DispatcherTimer? _zoneTimer;
    private SubmenuSafeZone? _zone;
    private DateTime _zoneUntil;
    private bool _towardSubmenu;
    private bool _pointerOnOpenRow;
    private bool _zoneHolds;
    private Point? _chainPoint;
    private PointerEventArgs? _lastChainArgs;
    private readonly List<Interactive> _chainHooks = new();
    private TopLevel? _inlineDismissTopLevel;

    static MenuSurface()
    {
        FocusableProperty.OverrideDefaultValue<MenuSurface>(true);
        IsTabStopProperty.OverrideDefaultValue<MenuSurface>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<MenuSurface>(KeyboardNavigationMode.Once);
    }

    public MenuSurface()
    {
        _navigator = new ListKeyboardNavigator(() => _stops.Count, i => _stops[i].IsEffectivelyEnabled, i => _stops[i].TypeaheadText);
        AddHandler(GotFocusEvent, OnDescendantGotFocus, RoutingStrategies.Bubble);
        SizeChanged += OnSurfaceSizeChanged;
    }

    /// <summary>The row the highlight is on (pointer or keyboard position), or null.</summary>
    public MenuEntry? HighlightedEntry => _active;

    /// <summary>The popup hosting this surface (null until the first open).</summary>
    internal MenuSurfacePopup? Popup => _popup;

    /// <summary>The highlight part (tests).</summary>
    internal GlidingIndicator? Highlight => _highlight;

    /// <summary>The bottom-sheet presenter (null until the surface first opens as a sheet).</summary>
    internal MenuSurfaceSheet? Sheet => _sheet;

    /// <summary>True for the surface of a submenu (it belongs to a row of another surface).</summary>
    internal bool IsSubmenu => _parentRow is not null;

    /// <summary>The row that opens this submenu (null for the outermost surface).</summary>
    internal MenuEntry? ParentRow => _parentRow;

    /// <summary>The surface holding <see cref="ParentRow"/>.</summary>
    internal MenuSurface? ParentSurface => _parentRow?.Surface;

    /// <summary>The outermost surface of the chain this surface belongs to.</summary>
    internal MenuSurface Root
    {
        get
        {
            var surface = this;
            while (surface.ParentSurface is { } parent)
            {
                surface = parent;
            }
            return surface;
        }
    }

    /// <summary>The row whose submenu is open (null when none is).</summary>
    internal MenuEntry? OpenSubmenuRow => _openRow;

    /// <summary>The open submenu (null when none is).</summary>
    internal MenuSurface? OpenSubmenuSurface => _openRow?.Submenu;

    /// <summary>The safe zone the pointer is crossing toward the open submenu (tests).</summary>
    internal SubmenuSafeZone? SafeZone => _zone;

    /// <summary>How long a safe zone holds (tests lengthen or shorten it so they do not depend on the machine's speed).</summary>
    internal TimeSpan SafeZoneLifetime { get; set; } = SubmenuSafeZone.Lifetime;

    internal void AttachToParentRow(MenuEntry row) => _parentRow = row;

    /// <summary>
    /// Opens the menu against <paramref name="anchor"/> (placed by <see cref="Side"/>, <see cref="Alignment"/> and
    /// <see cref="AnchorGap"/>). Pass <see cref="MenuOpenFocus.First"/> or <see cref="MenuOpenFocus.Last"/> when a key
    /// opened it so the highlight starts on a row.
    /// </summary>
    public void Open(Control anchor, MenuOpenFocus focus = MenuOpenFocus.Surface)
    {
        OpenCore(anchor, null, focus);
    }

    /// <summary>
    /// Opens the menu at <paramref name="position"/> (in <paramref name="anchor"/> coordinates), growing from that point.
    /// Opening again while open moves the menu to the new point.
    /// </summary>
    public void OpenAt(Control anchor, Point position, MenuOpenFocus focus = MenuOpenFocus.Surface)
    {
        OpenCore(anchor, position, focus);
    }

    /// <summary>Opens the menu against <paramref name="anchor"/>, or closes it when it is open (trigger clicks).</summary>
    public void Toggle(Control anchor, MenuOpenFocus focus = MenuOpenFocus.Surface)
    {
        if (IsOpen)
        {
            Close(returnFocus: false);
        }
        else
        {
            Open(anchor, focus);
        }
    }

    /// <summary>Closes the menu. With <paramref name="returnFocus"/> the anchor (or <see cref="FocusReturnTarget"/>) takes focus back.</summary>
    public void Close(bool returnFocus = true)
    {
        if (!IsOpen && !_popupOpen)
        {
            return;
        }
        ReturnFocus(returnFocus);
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OpenCore(Control anchor, Point? position, MenuOpenFocus focus)
    {
        _pendingFocus = focus;
        _anchorPoint = position;
        if (IsOpen && _popupOpen && !_closing)
        {
            if (!ReferenceEquals(PlacementTarget, anchor) || position is not null)
            {
                // Reopening at a new point swaps the menu at once and grows a fresh one from the new anchor.
                SetCurrentValue(PlacementTargetProperty, anchor);
                CloseImmediately();
                ShowPopup();
            }
            return;
        }
        SetCurrentValue(PlacementTargetProperty, anchor);
        if (IsOpen)
        {
            ShowPopup();
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                ShowPopup();
            }
            else
            {
                HidePopup();
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }
        _frame = e.NameScope.Find<Border>(FramePart);
        _scrollViewer = e.NameScope.Find<ScrollViewer>(ScrollViewerPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _itemsHost = e.NameScope.Find<Panel>(ItemsHostPart);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
        _frame?.SetCurrentValue(MaxHeightProperty, _windowRoom);
    }

    #region Containers

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is MenuEntry or MenuEntrySeparator or MenuEntryLabel)
        {
            recycleKey = null;
            return false;
        }
        return NeedsContainer<MenuEntry>(item, out recycleKey);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new MenuEntry();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is MenuEntry entry && !ReferenceEquals(entry, item))
        {
            entry.Header = item;
            if (ItemTemplate is { } template)
            {
                entry.HeaderTemplate = template;
            }
            if (item is not null && ChildrenSelector?.Invoke(item) is { } children)
            {
                entry.HasSelectedChildren = true;
                entry.SetCurrentValue(MenuEntry.ItemsSourceProperty, children);
            }
        }
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (ReferenceEquals(container, _openRow))
        {
            CloseSubmenu(returnFocus: false);
        }
        if (ReferenceEquals(container, _active))
        {
            _active = null;
        }
        if (container is MenuEntry { HasSelectedChildren: true } entry)
        {
            entry.HasSelectedChildren = false;
            entry.SetCurrentValue(MenuEntry.ItemsSourceProperty, null);
        }
    }

    #endregion

    #region Popup lifecycle

    private void ShowPopup()
    {
        var target = PlacementTarget;
        if (target is null || TopLevel.GetTopLevel(target) is null ||
            (_popup is null || !ReferenceEquals(_popup.Surface, this)) && this.GetVisualParent() is not null)
        {
            // No anchor, or the surface is placed inline in a layout: it cannot float.
            SetCurrentValue(IsOpenProperty, false);
            return;
        }
        if (_closing)
        {
            _reopenPending = true;
            return;
        }
        if (_popupOpen)
        {
            return;
        }
        EnsurePopup();
        var popup = _popup!;
        if (!ReferenceEquals(popup.Parent, target))
        {
            if (popup.Parent is not null)
            {
                ((ISetLogicalParent)popup).SetParent(null);
            }
            ((ISetLogicalParent)popup).SetParent(target);
        }
        if (Variant == MenuSurfaceVariant.Sheet)
        {
            _sheet ??= new MenuSurfaceSheet(this);
            _sheet.Prepare(target, TopLevel.GetTopLevel(target)!);
        }
        else
        {
            _sheet?.Release();
        }
        popup.PlacementTarget = target;
        popup.OverlayInputPassThroughElement = target;
        popup.Configure(Variant, Side, Alignment, AnchorGap, _anchorPoint, ParentSurface, _parentRow);
        SetActive(null, false, animate: false);
        ClampToWindow(target);
        // Every open starts at the first row; a keyboard open on the last row scrolls it into view.
        _scrollViewer?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        _popupOpen = true;
        popup.IsOpen = true;
        if (!popup.IsOpen)
        {
            _popupOpen = false;
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    /// <summary>
    /// Limits the frame to the room the window leaves on the larger side of the anchor (the popup is hosted in the
    /// window's overlay layer, so anything taller is cut off). The variant's own maximum height still applies on top,
    /// and the rows scroll beyond either limit. Sheets size themselves.
    /// </summary>
    private void ClampToWindow(Control target)
    {
        _windowRoom = WindowRoom(target);
        // The template is applied on the first open, after this runs; OnApplyTemplate picks the value up then.
        _frame?.SetCurrentValue(MaxHeightProperty, _windowRoom);
    }

    private double WindowRoom(Control target)
    {
        if (Variant == MenuSurfaceVariant.Sheet || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return double.PositiveInfinity;
        }
        var client = topLevel.ClientSize.Height;
        var origin = target.TranslatePoint(_anchorPoint ?? default, topLevel) ?? default;
        var anchor = _anchorPoint is null ? new Rect(origin, target.Bounds.Size) : new Rect(origin, new Size(0, 0));
        double room;
        if (_anchorPoint is null && Side is MenuSide.Left or MenuSide.Right)
        {
            // Side menus line up with the anchor and slide along the window edge, so the whole height is usable.
            room = client;
        }
        else
        {
            var gap = _anchorPoint is null ? AnchorGap : 0;
            room = Math.Max(anchor.Top, client - anchor.Bottom) - gap;
        }
        // A zoomed surface lays out in unscaled units (ArcZoom), so the window room shrinks by the zoom.
        return Math.Max(0, room - WindowEdgeMargin) / ArcZoom.GetScale(target);
    }

    private void HidePopup()
    {
        StopGrace();
        // Submenus close first, so focus inside them comes back to this surface (and from here to the anchor).
        CloseSubmenu(returnFocus: false);
        ParentSurface?.OnSubmenuClosing(this);
        if (_popup is not { } popup || !_popupOpen || _closing)
        {
            _reopenPending = false;
            return;
        }
        _closing = true;
        _reopenPending = false;
        DetachDismissHandlers();
        PseudoClasses.Set(OpenPseudoClass, false);
        if (IsKeyboardFocusWithin)
        {
            ReturnFocus(false);
        }
        if (Variant == MenuSurfaceVariant.Sheet)
        {
            _sheet?.OnClosing();
        }
        popup.IsOpen = false;
    }

    private void CloseImmediately()
    {
        if (_popup is not { } popup)
        {
            return;
        }
        popup.IsMotionEnabled = false;
        HidePopup();
        if (_popupOpen)
        {
            // The popup refused to close synchronously (pinned or detached); let the closed handler finish.
            _reopenPending = true;
        }
    }

    private void EnsurePopup()
    {
        if (_popup is not null)
        {
            return;
        }
        _popup = new MenuSurfacePopup
        {
            Child = this,
            TakesFocusFromNativeControl = true,
            // A submenu never dismisses itself: the outermost surface closes the whole chain on a press outside it.
            IsLightDismissEnabled = !IsSubmenu
        };
        _popup.Opened += OnPopupOpened;
        _popup.Closed += OnPopupClosed;
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        PseudoClasses.Set(OpenPseudoClass, true);
        AttachDismissHandlers();
        PlayEntrance();
        var focus = _pendingFocus;
        Dispatcher.UIThread.Post(() => ApplyOpenFocus(focus), DispatcherPriority.Loaded);
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        var wasClosing = _closing;
        _closing = false;
        _popupOpen = false;
        DetachDismissHandlers();
        PseudoClasses.Set(OpenPseudoClass, false);
        SetActive(null, false, animate: false);
        if (!wasClosing)
        {
            // Closed by the popup itself (light dismiss, the window lost focus, the anchor left): HidePopup did not run,
            // so the sheet's scrim was never told to go and would keep covering the window.
            _sheet?.OnClosing();
        }
        _sheet?.OnClosed();
        if (_reopenPending && IsOpen)
        {
            _reopenPending = false;
            ShowPopup();
            return;
        }
        _reopenPending = false;
        CloseSubmenu(returnFocus: false);
        ParentSurface?.OnSubmenuClosing(this);
        if (!wasClosing && IsOpen)
        {
            // Light dismiss (another window, the anchor detached): sync the state.
            SetCurrentValue(IsOpenProperty, false);
        }
        if (_popup is { Parent: not null } popup)
        {
            ((ISetLogicalParent)popup).SetParent(null);
        }
        RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
    }

    private void OnSurfaceSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Menus that grow away from their anchor edge (sheets, menus flipped above the trigger) must stay attached to
        // it when their content settles after the first placement; the positioner does not follow size changes.
        if (_popupOpen && !_closing)
        {
            _popup?.Reposition();
        }
        if (_active is { } active && _highlight is not null && _itemsHost is not null)
        {
            SetActive(active, false, animate: false);
        }
    }

    private void ApplyOpenFocus(MenuOpenFocus focus)
    {
        if (!_popupOpen || _closing || focus == MenuOpenFocus.None)
        {
            return;
        }
        RefreshStops();
        int? index = focus switch
        {
            MenuOpenFocus.First => _navigator.Navigate(_stops.Count - 1, Key.Home),
            MenuOpenFocus.Last => _navigator.Navigate(0, Key.End),
            _ => null
        };
        if (index is { } i && i >= 0 && i < _stops.Count)
        {
            _keyboardModality = true;
            MoveTo(_stops[i]);
        }
        else
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    private void ReturnFocus(bool requested)
    {
        if (!requested && !IsKeyboardFocusWithin)
        {
            return;
        }
        var target = FocusReturnTarget ?? PlacementTarget;
        target?.Focus(_keyboardModality && requested ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    #endregion

    #region Dismissal

    private void AttachDismissHandlers()
    {
        DetachDismissHandlers();
        if (IsSubmenu || PlacementTarget is not { } target || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return;
        }
        _dismissTopLevel = topLevel;
        topLevel.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (ClosesOnViewportChange)
        {
            topLevel.AddHandler(ScrollViewer.ScrollChangedEvent, OnViewportScrolled, RoutingStrategies.Bubble, handledEventsToo: true);
            topLevel.SizeChanged += OnViewportResized;
        }
    }

    private void DetachDismissHandlers()
    {
        if (_dismissTopLevel is not { } topLevel)
        {
            return;
        }
        topLevel.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        topLevel.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnViewportScrolled);
        topLevel.SizeChanged -= OnViewportResized;
        _dismissTopLevel = null;
    }

    private bool IsInside(object? source) =>
        source is Visual visual && (ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual));

    /// <summary>True for <paramref name="source"/> inside this surface or any submenu open from it (at any depth).</summary>
    internal bool IsInsideChain(object? source) =>
        IsInside(source) || OpenSubmenuSurface is { } submenu && submenu.IsInsideChain(source);

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsInsideChain(e.Source))
        {
            return;
        }
        // Presses on the anchor pass through so its own click can toggle the menu. A menu opened at a point (context menu)
        // has a large anchor such as a whole grid body, so a press there closes it and a context request reopens it.
        if (_anchorPoint is null && PlacementTarget is { } target && e.Source is Visual visual &&
            (ReferenceEquals(visual, target) || target.IsVisualAncestorOf(visual)))
        {
            return;
        }
        Close(returnFocus: false);
    }

    private void OnViewportScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta == default || IsInsideChain(e.Source))
        {
            return;
        }
        Close(returnFocus: false);
    }

    private void OnViewportResized(object? sender, SizeChangedEventArgs e) => Close(returnFocus: false);

    #endregion

    #region Highlight and pointer

    private void OnDescendantGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is MenuEntry entry && !ReferenceEquals(entry, _active))
        {
            var glide = e.NavigationMethod == NavigationMethod.Pointer && _active is not null;
            SetActive(entry, glide, animate: true);
        }
    }

    private void SetActive(MenuEntry? entry, bool glide, bool animate)
    {
        if (_active is { } previous && !ReferenceEquals(previous, entry))
        {
            previous.SetHighlighted(false);
        }
        _active = entry;
        if (entry is null)
        {
            _highlight?.Hide(animate);
            PseudoClasses.Set(DangerHighlightPseudoClass, false);
            return;
        }
        entry.SetHighlighted(true);
        PseudoClasses.Set(DangerHighlightPseudoClass, entry.IsDestructive && Variant is MenuSurfaceVariant.Dropdown or MenuSurfaceVariant.Context);
        if (_highlight is null || _itemsHost is null)
        {
            return;
        }
        var row = entry.RowVisual;
        var origin = row.TranslatePoint(default, _itemsHost) ?? default;
        _highlight.MoveTo(new Rect(origin, row.Bounds.Size), glide, ArcSpring.Snappy);
    }

    private static MenuEntry? EntryFrom(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is MenuEntry entry)
            {
                return entry;
            }
            if (visual is MenuSurface)
            {
                return null;
            }
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch || _closing)
        {
            return;
        }
        _keyboardModality = false;
        _lastPointer = e.GetPosition(this);
        var entry = EntryFrom(e.Source);
        if (entry is { IsEffectivelyEnabled: true } && new Rect(entry.RowVisual.Bounds.Size).Contains(e.GetPosition(entry.RowVisual)))
        {
            StopGrace();
            if (!entry.TracksPointerHighlight)
            {
                // Inline switches are not rows: the pointer over them releases the highlight (unless the keyboard put it there).
                if (_active is not null && !entry.IsKeyboardFocusWithin)
                {
                    SetActive(null, false, animate: true);
                    Focus(NavigationMethod.Pointer);
                }
                return;
            }
            if (!ReferenceEquals(entry, _active))
            {
                if (_openRow is not null && _zoneHolds)
                {
                    // On the way into the open submenu: rows crossed on the diagonal do not take the highlight.
                    return;
                }
                if (!entry.Focus(NavigationMethod.Pointer) || !ReferenceEquals(entry, _active))
                {
                    SetActive(entry, _active is not null, animate: true);
                }
            }
            ScheduleSubmenu(entry);
            return;
        }
        StopHover();
        StartGrace();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.OffsetDelta.Y != 0 && _openRow is not null && !_keyboardModality)
        {
            // The wheel moved the row an open submenu is attached to; the submenu does not follow it.
            CloseSubmenu(returnFocus: false);
        }
        if (e.OffsetDelta.Y == 0 || _keyboardModality || _closing || !_popupOpen || _lastPointer is not { } point)
        {
            return;
        }
        // The wheel moves the rows under a still pointer: the highlight follows the row now under it.
        if (EntryAtPointer(point) is { IsEffectivelyEnabled: true, TracksPointerHighlight: true } entry)
        {
            if (!ReferenceEquals(entry, _active))
            {
                StopGrace();
                if (!entry.Focus(NavigationMethod.Pointer) || !ReferenceEquals(entry, _active))
                {
                    SetActive(entry, false, animate: false);
                }
            }
        }
        else if (_active is not null)
        {
            SetActive(null, false, animate: true);
            Focus(NavigationMethod.Pointer);
        }
    }

    /// <summary>
    /// The row under <paramref name="point"/> (surface coordinates) at the scroll viewer's current offset. Worked out from
    /// the rows' positions in the scrolled content instead of a hit test: the offset changes before the rows are
    /// arranged at it, so a hit test would still find the row that was there before the scroll.
    /// </summary>
    private MenuEntry? EntryAtPointer(Point point)
    {
        if (_scrollViewer is not { } scroll || _itemsHost is not { } host ||
            this.TranslatePoint(point, scroll) is not { } inViewer || !new Rect(scroll.Bounds.Size).Contains(inViewer))
        {
            return null;
        }
        var content = inViewer + scroll.Offset;
        var count = ItemCount;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is MenuEntry { IsVisible: true } entry &&
                entry.RowVisual.TranslatePoint(default, host) is { } origin &&
                new Rect(origin, entry.RowVisual.Bounds.Size).Contains(content))
            {
                return entry;
            }
        }
        return null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _lastPointer = null;
        if (e.Pointer.Type != PointerType.Touch)
        {
            StartGrace();
        }
    }

    private void StartGrace()
    {
        // The row of an open submenu keeps the highlight while the pointer is away (in the submenu, or on its way there).
        if (_active is null || _keyboardModality || _openRow is not null)
        {
            return;
        }
        if (_graceTimer is null)
        {
            _graceTimer = new DispatcherTimer(HighlightGrace, DispatcherPriority.Input, OnGraceElapsed);
        }
        if (!_graceTimer.IsEnabled)
        {
            _graceTimer.Start();
        }
    }

    private void StopGrace() => _graceTimer?.Stop();

    private void OnGraceElapsed(object? sender, EventArgs e)
    {
        StopGrace();
        if (_active is null || _keyboardModality || _closing || _openRow is not null)
        {
            return;
        }
        // A short grace keeps the highlight gliding across separators and gaps; then the pointer released it.
        SetActive(null, false, animate: true);
        Focus(NavigationMethod.Pointer);
    }

    #endregion

    #region Keyboard

    private void RefreshStops()
    {
        _stops.Clear();
        var count = ItemCount;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is MenuEntry { IsVisible: true } entry)
            {
                _stops.Add(entry);
            }
        }
    }

    private void MoveTo(MenuEntry entry)
    {
        StopGrace();
        StopHover();
        if (_openRow is not null && !ReferenceEquals(entry, _openRow))
        {
            CloseSubmenu(returnFocus: false);
        }
        entry.Focus(NavigationMethod.Directional);
        if (!ReferenceEquals(entry, _active))
        {
            SetActive(entry, false, animate: false);
        }
        entry.BringIntoView();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }
        _keyboardModality = true;
        StopGrace();
        RefreshStops();
        var current = _active is null ? -1 : _stops.IndexOf(_active);
        int? next = null;
        switch (e.Key)
        {
            case Key.Down:
                next = current < 0 ? _navigator.Navigate(_stops.Count - 1, Key.Home) : _navigator.Navigate(current, Key.Down);
                break;
            case Key.Up:
                next = current < 0 ? _navigator.Navigate(0, Key.End) : _navigator.Navigate(current, Key.Up);
                break;
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
                next = _navigator.Navigate(Math.Max(current, 0), e.Key);
                break;
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                _active?.Activate(NavigationMethod.Directional);
                return;
            case Key.Escape:
                e.Handled = true;
                // Escape closes only this level: a submenu hands focus back to its row.
                CloseLevel();
                return;
            case Key.Tab:
                e.Handled = true;
                CloseChain(NavigationMethod.Tab);
                return;
            case Key.Left:
            case Key.Right:
                if (_active is { } row && row.HandleMenuKey(e))
                {
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Right && _active is { HasSubmenu: true, IsEffectivelyEnabled: true } parent)
                {
                    e.Handled = true;
                    OpenSubmenu(parent, MenuOpenFocus.First);
                    return;
                }
                if (e.Key == Key.Left && IsSubmenu)
                {
                    e.Handled = true;
                    CloseLevel();
                    return;
                }
                break;
        }
        if (next is { } index && index >= 0 && index < _stops.Count)
        {
            e.Handled = true;
            MoveTo(_stops[index]);
            return;
        }
        if (e.Key is Key.Down or Key.Up or Key.Home or Key.End)
        {
            e.Handled = true;
            return;
        }
        if (e.KeySymbol is { Length: 1 } symbol && !char.IsControl(symbol[0]) && symbol[0] != ' ' &&
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0)
        {
            if (_navigator.TypeAhead(current, symbol) is { } match && match >= 0 && match < _stops.Count)
            {
                MoveTo(_stops[match]);
            }
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    #endregion

    #region Rows

    internal void UncheckRadioSiblings(MenuEntry entry)
    {
        RefreshStops();
        foreach (var other in _stops)
        {
            if (!ReferenceEquals(other, entry) && other.ToggleType == MenuEntryToggleType.Radio &&
                string.Equals(other.GroupName, entry.GroupName, StringComparison.Ordinal))
            {
                other.SetCurrentValue(MenuEntry.IsCheckedProperty, false);
            }
        }
    }

    internal void OnEntryActivated(MenuEntry entry, NavigationMethod method)
    {
        _keyboardModality = method != NavigationMethod.Pointer;
        var item = ItemFromContainer(entry) ?? entry;
        var root = Root;
        root._keyboardModality = _keyboardModality;
        // One event for the whole menu, from the outermost surface (DropdownMenu and ContextMenu listen there).
        root.RaiseEvent(new MenuEntryClickedEventArgs(ItemClickedEvent, entry, item));
        if (entry.StaysOpenOnClick)
        {
            return;
        }
        if (root.IsOpen)
        {
            root.Close(returnFocus: true);
        }
        else if (!ReferenceEquals(root, this))
        {
            // A static (inline) menu stays; its submenus close and focus goes back to the row that opened them.
            root.CloseSubmenu(returnFocus: true);
        }
    }

    /// <summary>Escape (and ArrowLeft in a submenu): closes this level only; a submenu returns focus to its row.</summary>
    private void CloseLevel()
    {
        ParentSurface?.SetKeyboardModality();
        Close(returnFocus: true);
    }

    /// <summary>Tab: closes the whole menu from any level.</summary>
    private void CloseChain(NavigationMethod method)
    {
        var root = Root;
        root._keyboardModality = method != NavigationMethod.Pointer;
        if (root.IsOpen)
        {
            root.Close(returnFocus: true);
        }
        else
        {
            root.CloseSubmenu(returnFocus: true);
        }
    }

    private void SetKeyboardModality()
    {
        _keyboardModality = true;
        StopGrace();
    }

    #endregion

    #region Submenus

    /// <summary>
    /// Opens the submenu of <paramref name="row"/> (closing another open one). <see cref="MenuOpenFocus.None"/> keeps focus
    /// on the row (pointer), <see cref="MenuOpenFocus.First"/> moves the keyboard position to the submenu's first row. A
    /// submenu that is already open only takes the keyboard position.
    /// </summary>
    internal void OpenSubmenu(MenuEntry row, MenuOpenFocus focus)
    {
        StopHover();
        if (!row.HasSubmenu || !row.IsEffectivelyEnabled || TopLevel.GetTopLevel(row) is null)
        {
            return;
        }
        if (ReferenceEquals(_openRow, row) && row.Submenu is { IsOpen: true } open)
        {
            if (focus is MenuOpenFocus.First or MenuOpenFocus.Last)
            {
                open.ApplyOpenFocus(focus);
            }
            return;
        }
        CloseSubmenu(returnFocus: false);
        if (!ReferenceEquals(row, _active))
        {
            SetActive(row, false, animate: false);
        }
        if (focus != MenuOpenFocus.None)
        {
            _keyboardModality = true;
        }
        var submenu = row.EnsureSubmenu();
        submenu.PrepareAsSubmenuOf(this);
        _openRow = row;
        row.SetSubmenuOpen(true);
        _zone = null;
        _zoneHolds = false;
        _pointerOnOpenRow = !_keyboardModality;
        HookChainPointer(submenu);
        if (!_popupOpen)
        {
            // A static menu has no light-dismiss handler of its own: presses outside the chain close the submenus.
            HookInlineDismiss();
        }
        submenu.Open(row, focus);
        if (!submenu.IsOpen)
        {
            OnSubmenuClosing(submenu);
        }
    }

    /// <summary>Closes the open submenu (and the ones below it).</summary>
    internal void CloseSubmenu(bool returnFocus)
    {
        StopHover();
        if (_openRow?.Submenu is { } submenu)
        {
            submenu.Close(returnFocus);
        }
        ClearOpenRow();
    }

    /// <summary>A submenu of this surface started closing (by itself, a key, or its chain).</summary>
    private void OnSubmenuClosing(MenuSurface submenu)
    {
        if (_openRow is { } row && ReferenceEquals(row.Submenu, submenu))
        {
            ClearOpenRow();
        }
    }

    private void ClearOpenRow()
    {
        if (_openRow is not { } row)
        {
            return;
        }
        _openRow = null;
        row.SetSubmenuOpen(false);
        _zone = null;
        _zoneHolds = false;
        _zoneTimer?.Stop();
        UnhookChainPointer();
        UnhookInlineDismiss();
    }

    /// <summary>Takes the owner's look and data rules and the place beside its row (called before every open).</summary>
    private void PrepareAsSubmenuOf(MenuSurface owner)
    {
        // A bottom sheet has no side room: its submenus float above their row as account panels. Beside a submenu that
        // had to open on the left, the next level keeps going left instead of turning back over the menu.
        var sheet = owner.Variant == MenuSurfaceVariant.Sheet;
        var left = owner.IsSubmenu && owner.Popup is { LastTravelDirection.X: > 0 };
        SetCurrentValue(VariantProperty, sheet ? MenuSurfaceVariant.Account : owner.Variant);
        SetCurrentValue(SideProperty, sheet ? MenuSide.Top : left ? MenuSide.Left : MenuSide.Right);
        SetCurrentValue(AlignmentProperty, sheet ? MenuAlignment.End : MenuAlignment.Start);
        SetCurrentValue(AnchorGapProperty, SubmenuGap);
        SetCurrentValue(ItemTemplateProperty, owner.ItemTemplate);
        SetCurrentValue(ChildrenSelectorProperty, owner.ChildrenSelector);
        SetCurrentValue(ClosesOnViewportChangeProperty, false);
    }

    private void ScheduleSubmenu(MenuEntry entry)
    {
        if (!entry.HasSubmenu || ReferenceEquals(entry, _openRow))
        {
            if (!ReferenceEquals(entry, _hoverRow))
            {
                StopHover();
            }
            return;
        }
        if (ReferenceEquals(entry, _hoverRow) && _hoverTimer is { IsEnabled: true })
        {
            return;
        }
        _hoverRow = entry;
        _hoverTimer ??= new DispatcherTimer(SubmenuHoverDelay, DispatcherPriority.Input, OnHoverElapsed);
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void StopHover()
    {
        _hoverTimer?.Stop();
        _hoverRow = null;
    }

    private void OnHoverElapsed(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        var row = _hoverRow;
        _hoverRow = null;
        if (row is not null && ReferenceEquals(row, _active) && !_closing && (_popupOpen || this.GetVisualParent() is not null))
        {
            OpenSubmenu(row, MenuOpenFocus.None);
        }
    }

    /// <summary>
    /// Follows the pointer while a submenu is open. Moves over a popup end at its host instead of reaching the window, so
    /// this surface and the submenu report their own moves and the window reports the moves everywhere else.
    /// </summary>
    private void HookChainPointer(MenuSurface submenu)
    {
        UnhookChainPointer();
        _chainPoint = null;
        if (TopLevel.GetTopLevel(this) is { } topLevel)
        {
            Hook(topLevel);
        }
        Hook(this);
        Hook(submenu);

        void Hook(Interactive target)
        {
            target.AddHandler(PointerMovedEvent, OnChainPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            _chainHooks.Add(target);
        }
    }

    private void UnhookChainPointer()
    {
        foreach (var target in _chainHooks)
        {
            target.RemoveHandler(PointerMovedEvent, OnChainPointerMoved);
        }
        _chainHooks.Clear();
        _lastChainArgs = null;
    }

    private void HookInlineDismiss()
    {
        UnhookInlineDismiss();
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }
        _inlineDismissTopLevel = topLevel;
        topLevel.AddHandler(PointerPressedEvent, OnInlinePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void UnhookInlineDismiss()
    {
        if (_inlineDismissTopLevel is { } topLevel)
        {
            topLevel.RemoveHandler(PointerPressedEvent, OnInlinePointerPressed);
            _inlineDismissTopLevel = null;
        }
    }

    private void OnInlinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsInsideChain(e.Source))
        {
            CloseSubmenu(returnFocus: false);
        }
    }

    /// <summary>
    /// Every pointer move while a submenu of this surface is open (tunnel, so it runs before the rows see the move): on
    /// the submenu or its row the submenu stays; leaving the row starts the safe zone toward the submenu, and while the
    /// pointer heads through it the rows it crosses do not take the highlight. Landing on another row closes the
    /// submenu (that row takes the highlight right after); gaps, separators and the space outside the menu keep it.
    /// </summary>
    private void OnChainPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || _openRow is not { Submenu: { } submenu } row || _closing)
        {
            return;
        }
        if (ReferenceEquals(e, _lastChainArgs))
        {
            // Hooked on the window and on the surfaces: a move inside a static menu reaches two of them.
            return;
        }
        _lastChainArgs = e;
        var point = e.GetPosition(this);
        var dx = _chainPoint is { } last ? point.X - last.X : 0;
        _chainPoint = point;
        _zoneHolds = false;
        if (submenu.IsInsideChain(e.Source))
        {
            _zone = null;
            _pointerOnOpenRow = false;
            return;
        }
        if (IsInside(e.Source) && new Rect(row.RowVisual.Bounds.Size).Contains(e.GetPosition(row.RowVisual)))
        {
            _zone = null;
            _pointerOnOpenRow = true;
            return;
        }
        if (_pointerOnOpenRow)
        {
            _pointerOnOpenRow = false;
            StartZone(point, submenu);
        }
        if (IsInZone(point, dx))
        {
            _zoneHolds = true;
            return;
        }
        if (IsInside(e.Source) && EntryFrom(e.Source) is { IsEffectivelyEnabled: true, TracksPointerHighlight: true } other &&
            !ReferenceEquals(other, row) && new Rect(other.RowVisual.Bounds.Size).Contains(e.GetPosition(other.RowVisual)))
        {
            CloseSubmenu(returnFocus: false);
        }
    }

    private void StartZone(Point exit, MenuSurface submenu)
    {
        _zone = null;
        if (submenu.TranslatePoint(default, this) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, submenu.Bounds.Size);
        var zone = new SubmenuSafeZone(exit, rect, rect.Center.X >= exit.X);
        _zone = zone;
        _zoneUntil = DateTime.UtcNow + SafeZoneLifetime;
        _towardSubmenu = true;
        _zoneTimer ??= new DispatcherTimer(SafeZoneLifetime, DispatcherPriority.Input, OnZoneElapsed);
        _zoneTimer.Stop();
        _zoneTimer.Interval = SafeZoneLifetime;
        _zoneTimer.Start();
    }

    private bool IsInZone(Point point, double dx)
    {
        if (_zone is not { } zone)
        {
            return false;
        }
        if (DateTime.UtcNow > _zoneUntil)
        {
            _zone = null;
            return false;
        }
        _towardSubmenu = zone.IsToward(dx, _towardSubmenu);
        if (_towardSubmenu && zone.Contains(point))
        {
            return true;
        }
        _zone = null;
        return false;
    }

    /// <summary>The pointer rested on its way: the zone lapses, and a row it rests on takes the highlight (closing the submenu).</summary>
    private void OnZoneElapsed(object? sender, EventArgs e)
    {
        _zoneTimer?.Stop();
        var held = _zone is not null && _zoneHolds;
        _zone = null;
        _zoneHolds = false;
        if (!held || _openRow is not { } row || _lastPointer is not { } point ||
            EntryAtPointer(point) is not { IsEffectivelyEnabled: true, TracksPointerHighlight: true } entry ||
            ReferenceEquals(entry, row))
        {
            return;
        }
        CloseSubmenu(returnFocus: false);
        if (!entry.Focus(NavigationMethod.Pointer) || !ReferenceEquals(entry, _active))
        {
            SetActive(entry, _active is not null, animate: true);
        }
        ScheduleSubmenu(entry);
    }

    #endregion

    #region Entrance stagger

    /// <summary>
    /// Rows fade in by index after the surface starts its enter (Arc: dropdown and context rows 35ms apart, capped at the
    /// fifth, 240ms on the enter curve from 2px toward the anchor; account rows 30ms + 16ms apart, 200ms from 3px below).
    /// Code-driven because each row's delay depends on its index and on the moment the popup opened, which ControlTheme
    /// transitions cannot express. Full motion only; Reduced and Off show the rows at once. Only the rows inside the
    /// scroll viewport play: a long menu would otherwise animate every hidden row's opacity and transform each frame.
    /// </summary>
    private void PlayEntrance()
    {
        var full = ArcMotion.IsFull(PlacementTarget ?? (AvaloniaObject)this);
        var sheet = Variant == MenuSurfaceVariant.Sheet;
        var account = sheet || Variant == MenuSurfaceVariant.Account;
        var direction = _popup?.LastTravelDirection ?? default;
        var offset = account ? new Vector(0, 3) : Variant == MenuSurfaceVariant.Context || direction == default ? new Vector(0, -2) : direction * 2;
        var duration = account ? TimeSpan.FromMilliseconds(200) : ArcMotionPresets.Standard;
        var index = 0;
        if (account && HeaderPresenter is { } header)
        {
            MenuSurfaceStagger.Play(header, full, Delay(index++), duration, offset);
        }
        var viewport = _scrollViewer is { Viewport.Height: > 0 } scroll
            ? new Rect(scroll.Offset.X, scroll.Offset.Y, scroll.Viewport.Width, scroll.Viewport.Height)
            : (Rect?)null;
        var count = ItemCount;
        var row = 0;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is MenuEntry entry)
            {
                var visible = viewport is { } view
                    ? entry.Bounds.Bottom > view.Top && entry.Bounds.Top < view.Bottom
                    : row < UnmeasuredStaggerRows;
                row++;
                MenuSurfaceStagger.Play(entry, full && visible, Delay(index), duration, offset);
                if (full && visible)
                {
                    index++;
                }
            }
        }

        TimeSpan Delay(int i) => sheet
            ? TimeSpan.FromMilliseconds(60 + 20 * i)
            : account
                ? TimeSpan.FromMilliseconds(30 + 16 * i)
                : TimeSpan.FromMilliseconds(35 * Math.Min(i, 4));
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new MenuSurfaceAutomationPeer(this);

    private sealed class MenuSurfaceAutomationPeer : ControlAutomationPeer
    {
        public MenuSurfaceAutomationPeer(MenuSurface owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;
    }
}

/// <summary>Per-row entrance state (opacity and a small travel), reused across opens.</summary>
internal sealed class MenuSurfaceStagger
{
    private static readonly AttachedProperty<MenuSurfaceStagger?> StateProperty =
        AvaloniaProperty.RegisterAttached<MenuSurfaceStagger, Visual, MenuSurfaceStagger?>("State");

    private readonly Visual _visual;
    private readonly TranslateTransform _transform = new();
    private readonly MotionValue<double> _progress;
    private Vector _offset;

    private MenuSurfaceStagger(Visual visual)
    {
        _visual = visual;
        _progress = new MotionValue<double>(visual, 1, MotionVectors.Double);
        _progress.Changed += Apply;
    }

    public static void Play(Visual visual, bool animate, TimeSpan delay, TimeSpan duration, Vector offset)
    {
        var state = visual.GetValue(StateProperty);
        if (!animate)
        {
            state?._progress.Jump(1);
            return;
        }
        if (state is null)
        {
            state = new MenuSurfaceStagger(visual);
            visual.SetValue(StateProperty, state);
        }
        state._offset = offset;
        if (!ReferenceEquals(visual.RenderTransform, state._transform))
        {
            visual.RenderTransform = state._transform;
        }
        state._progress.Jump(0);
        _ = state._progress.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.Enter, delay));
    }

    private void Apply(double value)
    {
        _visual.Opacity = value;
        _transform.X = _offset.X * (1 - value);
        _transform.Y = _offset.Y * (1 - value);
    }
}
