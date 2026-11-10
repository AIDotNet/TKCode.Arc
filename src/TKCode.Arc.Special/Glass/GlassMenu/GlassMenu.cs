using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A menu on frosted glass: commands on a translucent surface that blurs and tints what lies behind it (see
/// <see cref="GlassBackdrop"/>). One highlight of brighter glass glides from row to row under the pointer or the keyboard
/// focus, and a soft glow inside it follows the pointer along the row. Place it inline, or attach it to a button with
/// <c>arc:GlassMenu.Menu</c> (or call <see cref="Open"/>) to show it as a popup that closes when a command runs, on
/// Escape or on a click outside.
/// </summary>
/// <remarks>
/// <para>Rows are <see cref="GlassMenuItem"/>s and <see cref="GlassMenuSeparator"/>s written inline, or any items from
/// <c>ItemsSource</c> (shown as text). Up and Down move between the commands (wrapping, skipping disabled ones), Home and
/// End jump to the ends, Enter or Space runs the command, typing a letter jumps to the next command that starts with it,
/// and Escape closes a popup menu and returns the focus to its button.</para>
/// <para>Under the ArcDesign <c>ReduceTransparency</c> token the menu is opaque. Reduced motion moves the highlight at
/// once and shows no glow.</para>
/// </remarks>
[TemplatePart(HighlightPart, typeof(GlassMenuHighlight))]
[TemplatePart(PanePart, typeof(GlassPane))]
[PseudoClasses(OpenPseudoClass)]
public class GlassMenu : ItemsControl
{
    public const string HighlightPart = "PART_Highlight";
    public const string PanePart = "PART_Pane";
    public const string OpenPseudoClass = ":open";

    /// <summary>Raised when a command row runs (click, tap, Enter or Space), after its own <c>Command</c>.</summary>
    public static readonly RoutedEvent<GlassMenuItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<GlassMenu, GlassMenuItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>
    /// A menu that opens from this control: a click or tap toggles it, and Down, Enter or Space open it with the first
    /// command focused. On a <see cref="Button"/> it opens on <c>Click</c>.
    /// </summary>
    public static readonly AttachedProperty<GlassMenu?> MenuProperty =
        AvaloniaProperty.RegisterAttached<GlassMenu, Control, GlassMenu?>("Menu");

    /// <summary>Whether the menu is open as a popup (read-only; use <see cref="Open"/> and <see cref="Close"/>).</summary>
    public static readonly DirectProperty<GlassMenu, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<GlassMenu, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>Where a popup menu opens relative to its anchor (below it, aligned to its left edge, by default).</summary>
    public static readonly StyledProperty<PlacementMode> PlacementProperty =
        AvaloniaProperty.Register<GlassMenu, PlacementMode>(nameof(Placement), PlacementMode.BottomEdgeAlignedLeft);

    internal static readonly StyledProperty<double> AnchorGapProperty =
        AvaloniaProperty.Register<GlassMenu, double>(nameof(AnchorGap), 8);

    private GlassMenuHighlight? _highlight;
    private GlassPane? _pane;
    private GlassMenuItem? _active;
    private ArcPopup? _popup;
    private Control? _anchor;
    private bool _isOpen;
    private bool _pointerInside;
    private bool _openedByKeyboard;
    private bool _layoutHooked;

    static GlassMenu()
    {
        FocusableProperty.OverrideDefaultValue<GlassMenu>(true);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<GlassMenu>(KeyboardNavigationMode.Once);
        MenuProperty.Changed.AddClassHandler<Control>(OnMenuChanged);
    }

    public GlassMenu()
    {
        AddHandler(Button.ClickEvent, OnItemClick);
        AddHandler(GotFocusEvent, OnItemGotFocus, handledEventsToo: true);
    }

    /// <summary>Raised when a command row runs.</summary>
    public event EventHandler<GlassMenuItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    /// <inheritdoc cref="PlacementProperty"/>
    public PlacementMode Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    internal double AnchorGap
    {
        get => GetValue(AnchorGapProperty);
        set => SetValue(AnchorGapProperty, value);
    }

    /// <summary>The row the highlight is on, or null.</summary>
    public GlassMenuItem? HighlightedItem => _active;

    internal GlassMenuHighlight? Highlight => _highlight;

    internal GlassPane? Pane => _pane;

    internal ArcPopup? Popup => _popup;

    /// <summary>Reads <see cref="MenuProperty"/>.</summary>
    public static GlassMenu? GetMenu(Control element) => element.GetValue(MenuProperty);

    /// <summary>Sets <see cref="MenuProperty"/>.</summary>
    public static void SetMenu(Control element, GlassMenu? value) => element.SetValue(MenuProperty, value);

    protected override Type StyleKeyOverride => typeof(GlassMenu);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is GlassMenuItem or GlassMenuSeparator)
        {
            recycleKey = null;
            return false;
        }
        return NeedsContainer<GlassMenuItem>(item, out recycleKey);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new GlassMenuItem();

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        UpdateTabStop();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _highlight = e.NameScope.Find<GlassMenuHighlight>(HighlightPart);
        _pane = e.NameScope.Find<GlassPane>(PanePart);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_layoutHooked)
        {
            LayoutUpdated += OnLayoutUpdated;
            _layoutHooked = true;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_layoutHooked)
        {
            LayoutUpdated -= OnLayoutUpdated;
            _layoutHooked = false;
        }
        SetActive(null, animate: false);
    }

    /// <summary>The command rows in order (separators and non-command content skipped).</summary>
    private IEnumerable<GlassMenuItem> Rows()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is GlassMenuItem row)
            {
                yield return row;
            }
        }
    }

    private static bool IsUsable(GlassMenuItem row) => row.IsEffectivelyEnabled && row.IsEffectivelyVisible;

    private void UpdateTabStop()
    {
        GlassMenuItem? first = null;
        foreach (var row in Rows())
        {
            if (IsUsable(row))
            {
                first = row;
                break;
            }
        }
        KeyboardNavigation.SetTabOnceActiveElement(this, first);
        if (ItemsPanelRoot is { } panel)
        {
            KeyboardNavigation.SetTabOnceActiveElement(panel, first);
        }
    }

    // ---- Highlight ----

    private void SetActive(GlassMenuItem? row, bool animate)
    {
        if (!ReferenceEquals(_active, row))
        {
            _active?.SetHighlighted(false);
            _active = row;
            _active?.SetHighlighted(true);
        }
        if (_highlight is null)
        {
            return;
        }
        if (row is null)
        {
            _highlight.Hide();
            return;
        }
        if (RowRect(row) is { } rect)
        {
            _highlight.MoveTo(rect, animate && IsLoaded);
        }
    }

    private Rect? RowRect(GlassMenuItem row) =>
        _highlight is not null && row.TranslatePoint(default, _highlight) is { } at ? new Rect(at, row.Bounds.Size) : null;

    /// <summary>A popup that just opened or a row that resized: the highlight lands on its row at once.</summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_active is { } row && _highlight is { IsShown: true } highlight && RowRect(row) is { } rect &&
            rect != highlight.TargetRect)
        {
            highlight.MoveTo(rect, animate: false);
        }
    }

    private GlassMenuItem? RowFrom(object? source)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is GlassMenuItem row)
            {
                return row;
            }
        }
        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerInside = true;
        _pane?.PointAt(e.GetPosition(_pane));
        var row = RowFrom(e.Source);
        if (row is not null && IsUsable(row))
        {
            SetActive(row, animate: true);
            if (_highlight is not null)
            {
                _highlight.GlowAt(e.GetPosition(_highlight).X);
            }
        }
        else if (row is null && !IsKeyboardFocusWithin)
        {
            // Over the padding or a separator: the highlight stays on the last row until the pointer leaves.
            _highlight?.GlowAt(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
        _pane?.PointAt(null);
        _highlight?.GlowAt(null);
        if (FocusedRow() is { } focused)
        {
            // The keyboard still has a row: the highlight goes back to it.
            SetActive(focused, animate: true);
        }
        else
        {
            SetActive(null, animate: true);
        }
    }

    private GlassMenuItem? FocusedRow()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && this.IsVisualAncestorOf(focused) ? RowFrom(focused) : null;
    }

    private void OnItemGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (RowFrom(e.Source) is { } row)
        {
            SetActive(row, animate: true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsKeyboardFocusWithinProperty && !change.GetNewValue<bool>() && !_pointerInside)
        {
            SetActive(null, animate: true);
        }
    }

    // ---- Keyboard ----

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.Down:
                    Move(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    Move(-1);
                    e.Handled = true;
                    return;
                case Key.Home:
                    MoveToEnd(first: true);
                    e.Handled = true;
                    return;
                case Key.End:
                    MoveToEnd(first: false);
                    e.Handled = true;
                    return;
                case Key.Escape when IsOpen:
                    Close(returnFocus: true);
                    e.Handled = true;
                    return;
                case Key.Tab when IsOpen:
                    Close(returnFocus: false);
                    return;
            }
        }
        base.OnKeyDown(e);
    }

    /// <summary>Focuses the next usable row in <paramref name="direction"/> from the highlighted one, wrapping.</summary>
    private void Move(int direction)
    {
        var rows = Rows().Where(IsUsable).ToList();
        if (rows.Count == 0)
        {
            return;
        }
        var current = _active is null ? -1 : rows.IndexOf(_active);
        int next;
        if (current < 0)
        {
            next = direction > 0 ? 0 : rows.Count - 1;
        }
        else
        {
            next = (current + direction + rows.Count) % rows.Count;
        }
        rows[next].Focus(NavigationMethod.Directional);
    }

    private void MoveToEnd(bool first)
    {
        var rows = Rows().Where(IsUsable).ToList();
        if (rows.Count > 0)
        {
            (first ? rows[0] : rows[^1]).Focus(NavigationMethod.Directional);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrWhiteSpace(e.Text))
        {
            return;
        }
        var letter = e.Text.Trim()[0];
        var rows = Rows().Where(IsUsable).ToList();
        if (rows.Count == 0)
        {
            return;
        }
        var start = _active is null ? -1 : rows.IndexOf(_active);
        for (var n = 1; n <= rows.Count; n++)
        {
            var row = rows[(start + n + rows.Count) % rows.Count];
            if (row.Label is { Length: > 0 } label && char.ToUpperInvariant(label[0]) == char.ToUpperInvariant(letter))
            {
                row.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }
    }

    // ---- Commands ----

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not GlassMenuItem row || !ReferenceEquals(ItemsControlFromItemContainer(row) ?? RowOwner(row), this))
        {
            return;
        }
        var index = IndexFromContainer(row);
        var item = index >= 0 ? Items[index] : row;
        RaiseEvent(new GlassMenuItemInvokedEventArgs(ItemInvokedEvent, row, item));
        if (IsOpen)
        {
            Close(returnFocus: true);
        }
    }

    /// <summary>Rows written inline are their own containers; they belong to the menu they sit in.</summary>
    private GlassMenu? RowOwner(GlassMenuItem row) => row.FindLogicalAncestorOfType<GlassMenu>();

    // ---- Popup ----

    /// <summary>
    /// Shows the menu as a popup at <paramref name="anchor"/>. With <paramref name="focusFirstItem"/> (opened from the
    /// keyboard) the first command takes the focus; otherwise the menu does, without a highlight. The menu must not be
    /// placed in the page itself.
    /// </summary>
    public void Open(Control anchor, bool focusFirstItem = false)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (IsOpen)
        {
            return;
        }
        if (_popup is null && (Parent is not null || this.GetVisualParent() is not null))
        {
            throw new InvalidOperationException("A GlassMenu placed in the page cannot open as a popup; give Open a menu that is not in the tree.");
        }
        if (_popup is null)
        {
            _popup = new ArcPopup
            {
                EnterOffset = 4,
                EnterScale = 0.97,
                WindowManagerAddShadowHint = false,
                IsLightDismissEnabled = true,
                // The glass draws its own shadow; the frame's would double it.
                OverlayHostShadow = default,
                PopupRootShadow = default
            };
            _popup.Closed += OnPopupClosed;
            _popup.Child = this;
        }
        if (_popup.Parent is null)
        {
            ((ISetLogicalParent)_popup).SetParent(anchor);
        }
        _anchor = anchor;
        _openedByKeyboard = focusFirstItem;
        _popup.Placement = Placement;
        _popup.MarginToAnchor = AnchorGap;
        _popup.PlacementTarget = anchor;
        _popup.OverlayInputPassThroughElement = anchor;
        IsOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        _popup.IsOpen = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen)
            {
                return;
            }
            if (focusFirstItem)
            {
                MoveToEnd(first: true);
            }
            else
            {
                Focus(NavigationMethod.Pointer);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes a popup menu; with <paramref name="returnFocus"/> the focus goes back to its anchor.</summary>
    public void Close(bool returnFocus = true)
    {
        if (!IsOpen || _popup is null)
        {
            return;
        }
        var anchor = _anchor;
        if (returnFocus && anchor is not null)
        {
            // Hand the focus back before the popup closes, or it returns without a keyboard ring.
            anchor.Focus(_openedByKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        MarkClosed();
        _popup.IsOpen = false;
    }

    /// <summary>Opens the menu at <paramref name="anchor"/>, or closes it when it is open.</summary>
    public void Toggle(Control anchor, bool focusFirstItem = false)
    {
        if (IsOpen)
        {
            Close(returnFocus: true);
        }
        else
        {
            Open(anchor, focusFirstItem);
        }
    }

    private void MarkClosed()
    {
        IsOpen = false;
        PseudoClasses.Set(OpenPseudoClass, false);
        _pointerInside = false;
        _pane?.PointAt(null);
        SetActive(null, animate: false);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // Light dismiss (a click outside) closes the popup without Close.
        if (IsOpen)
        {
            MarkClosed();
        }
        if (_popup is { IsOpen: false, Parent: not null } popup)
        {
            ((ISetLogicalParent)popup).SetParent(null);
        }
        _anchor = null;
    }

    private static void OnMenuChanged(Control anchor, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is GlassMenu old)
        {
            old.Close(returnFocus: false);
            if (anchor is Button oldButton)
            {
                oldButton.Click -= OnAnchorClick;
            }
            else
            {
                anchor.Tapped -= OnAnchorTapped;
            }
            anchor.KeyDown -= OnAnchorKeyDown;
        }
        if (e.NewValue is GlassMenu)
        {
            if (anchor is Button button)
            {
                button.Click += OnAnchorClick;
            }
            else
            {
                anchor.Tapped += OnAnchorTapped;
            }
            anchor.KeyDown += OnAnchorKeyDown;
        }
    }

    private static void OnAnchorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control anchor && GetMenu(anchor) is { } menu)
        {
            // A click from Enter or Space comes with the keyboard ring on the button.
            menu.Toggle(anchor, focusFirstItem: anchor.Classes.Contains(":focus-visible"));
        }
    }

    private static void OnAnchorTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control anchor && GetMenu(anchor) is { } menu)
        {
            menu.Toggle(anchor);
        }
    }

    private static void OnAnchorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || sender is not Control anchor || GetMenu(anchor) is not { } menu || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var opens = e.Key == Key.Down || (anchor is not Button && e.Key is Key.Enter or Key.Space);
        if (opens && !menu.IsOpen)
        {
            menu.Open(anchor, focusFirstItem: true);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassMenuAutomationPeer(this);

    private sealed class GlassMenuAutomationPeer : ItemsControlAutomationPeer
    {
        public GlassMenuAutomationPeer(GlassMenu owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;
    }
}
