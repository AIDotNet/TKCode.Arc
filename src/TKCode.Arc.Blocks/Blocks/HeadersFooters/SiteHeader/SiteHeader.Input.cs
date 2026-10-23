// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>A one-shot timer on the UI thread (hover intent and leave grace).</summary>
internal sealed class SiteHeaderTimer
{
    private DispatcherTimer? _timer;
    private Action? _action;

    /// <summary>True while a callback is waiting.</summary>
    public bool IsPending => _timer is not null;

    public void Start(TimeSpan delay, Action action)
    {
        Stop();
        _action = action;
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) => RunNow();
        _timer = timer;
        timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _action = null;
    }

    /// <summary>Runs the waiting callback now (tests; the timer calls it when it elapses).</summary>
    public void RunNow()
    {
        var action = _action;
        Stop();
        action?.Invoke();
    }
}

public partial class SiteHeader
{
    private readonly SiteHeaderTimer _openTimer = new();
    private readonly SiteHeaderTimer _closeTimer = new();
    private readonly TranslateTransform _stick = new();
    private string? _hoveredValue;
    private ScrollViewer? _scroll;
    private TopLevel? _top;
    private Rect _lastCurrentRect;
    private Rect _lastHoverRect;
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.3, 0);
    private static readonly SpringSpec s_morph = SpringSpec.FromVisual(0.42, 0);

    /// <summary>The value of the nav item the hover block is on (tests read it).</summary>
    internal string? HoveredValue => _hoveredValue;

    /// <summary>True while a hover-intent or leave-grace timer is waiting (tests read it).</summary>
    internal bool HasPendingOpen => _openTimer.IsPending;

    internal bool HasPendingClose => _closeTimer.IsPending;

    /// <summary>Runs the waiting hover-intent callback at once (tests: headless timers do not fire without a frame).</summary>
    internal void FlushOpenTimer() => _openTimer.RunNow();

    /// <summary>Runs the waiting leave-grace callback at once.</summary>
    internal void FlushCloseTimer() => _closeTimer.RunNow();

    internal TranslateTransform StickTransform => _stick;

    internal GlidingIndicator? HoverBlock => _hover;

    internal GlidingIndicator? UnderlineIndicator => _underline;

    internal GlidingIndicator? CapsuleIndicator => _capsule;

    internal SiteNavButton? NavButton(string value) => _buttons.TryGetValue(value, out var b) ? b : null;

    internal SiteMenuButton? MenuButton => _menuButton;

    internal SiteActionButton? SecondaryButton => _secondary;

    internal SiteActionButton? PrimaryButton => _primary;

    internal SiteBrandButton? BrandButton => _brand;

    internal Panel? NavItemsPanel => _navItems;

    internal Border? SurfaceBorder => _surface;

    private void StopTimers()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
    }

    // ---- pointer --------------------------------------------------------------------------------------------

    private void OnNavPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is SiteNavButton button)
        {
            HandleNavEnter(button, e.Pointer.Type);
        }
    }

    /// <summary>Test hook: the pointer of <paramref name="type"/> entered a navigation item.</summary>
    internal void RaisePointerEnter(SiteNavButton button, PointerType type = PointerType.Mouse) => HandleNavEnter(button, type);

    internal void RaiseRegionEnter() => HandleRegionEnter(PointerType.Mouse);

    internal void RaiseRegionExit() => HandleRegionExit(PointerType.Mouse);

    internal void RaiseNavListExit() => SetHovered(null);

    private void HandleNavEnter(SiteNavButton button, PointerType type)
    {
        // Arc: only a real mouse hovers (touch and pen taps must not open panels or move the hover block).
        if (type != PointerType.Mouse || ItemOf(button) is not { } item)
        {
            return;
        }
        SetHovered(item.Value);
        if (HasPanel(item))
        {
            _closeTimer.Stop();
            _openTimer.Stop();
            if (_openValue is not null)
            {
                OpenPanelCore(item.Value, false);
            }
            else
            {
                var value = item.Value;
                _openTimer.Start(HoverIntent, () => OpenPanelCore(value, false));
            }
        }
        else if (_openValue is not null)
        {
            _closeTimer.Start(LeaveGrace, () => ClosePanelCore(false));
        }
    }

    private void OnNavListPointerExited(object? sender, PointerEventArgs e) => SetHovered(null);

    private void OnNavGotFocus(object? sender, RoutedEventArgs e) => SetHovered(null);

    private void OnRegionPointerEntered(object? sender, PointerEventArgs e) => HandleRegionEnter(e.Pointer.Type);

    private void OnRegionPointerExited(object? sender, PointerEventArgs e) => HandleRegionExit(e.Pointer.Type);

    private void HandleRegionEnter(PointerType type)
    {
        if (type == PointerType.Mouse)
        {
            _closeTimer.Stop();
        }
    }

    private void HandleRegionExit(PointerType type)
    {
        if (type == PointerType.Mouse)
        {
            _openTimer.Stop();
            _closeTimer.Start(LeaveGrace, () => ClosePanelCore(false));
        }
    }

    // ---- indicators -----------------------------------------------------------------------------------------

    private void SetHovered(string? value)
    {
        if (string.Equals(_hoveredValue, value, StringComparison.Ordinal))
        {
            return;
        }
        _hoveredValue = value;
        UpdateHoverIndicator(true);
    }

    private void UpdateHoverIndicator(bool animate)
    {
        if (_hover is null)
        {
            return;
        }
        if (_hoveredValue is null || Variant == SiteHeaderVariant.Centered || !_buttons.TryGetValue(_hoveredValue, out var button) || button.Bounds.Width <= 0)
        {
            // Arc unmounts the block at once: it vanishes without a fade and the next one lands in place.
            _hover.Hide(false);
            _lastHoverRect = default;
            return;
        }
        var rect = button.Bounds;
        if (rect == _lastHoverRect)
        {
            return;
        }
        _lastHoverRect = rect;
        _hover.MoveTo(rect, s_glide, animate);
    }

    private void UpdateCurrentIndicator(bool animate)
    {
        if (_underline is null || _capsule is null)
        {
            return;
        }
        if (Current is null || !_buttons.TryGetValue(Current, out var button) || button.Bounds.Width <= 0)
        {
            if (_lastCurrentRect != default)
            {
                _underline.Hide(false);
                _capsule.Hide(false);
                _lastCurrentRect = default;
            }
            return;
        }
        Rect rect;
        var centered = Variant == SiteHeaderVariant.Centered;
        if (centered)
        {
            rect = button.Bounds;
        }
        else
        {
            // Arc: left 12, right 12 (30 with a chevron), 4 above the bottom, 2 high.
            var b = button.Bounds;
            var left = 12.0;
            var right = button.HasChevron ? 30.0 : 12.0;
            rect = new Rect(b.X + left, b.Bottom - 4 - 2, Math.Max(0, b.Width - left - right), 2);
        }
        if (rect == _lastCurrentRect)
        {
            return;
        }
        _lastCurrentRect = rect;
        if (centered)
        {
            _underline.Hide(false);
            _capsule.MoveTo(rect, s_morph, animate);
        }
        else
        {
            _capsule.Hide(false);
            _underline.MoveTo(rect, s_morph, animate);
        }
    }

    private void OnNavButtonPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            // Layout moved an item (a font loaded, the window changed): follow without a glide.
            UpdateCurrentIndicator(false);
            UpdateHoverIndicator(false);
        }
    }

    // ---- keyboard -------------------------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not Visual source)
        {
            return;
        }
        if (source is SiteNavButton nav && _buttons.TryGetValue(((SiteHeaderItem?)nav.Tag)?.Value ?? string.Empty, out var found) && found == nav)
        {
            OnNavKeyDown(e, nav);
        }
        else if (_panelView is not null && _panelView.IsVisualAncestorOf(source))
        {
            OnPanelKeyDown(e, source);
        }
    }

    private void OnNavKeyDown(KeyEventArgs e, SiteNavButton nav)
    {
        var item = ItemOf(nav)!;
        if (e.Key == Key.Down && HasPanel(item))
        {
            // Arc: ArrowDown opens the panel and moves focus to its first link (on the next frame).
            e.Handled = true;
            OpenPanelCore(item.Value, true);
            return;
        }
        if (e.Key != Key.Left && e.Key != Key.Right)
        {
            return;
        }
        var buttons = _resolved.Select(i => _buttons.GetValueOrDefault(i.Value)).Where(b => b is not null).Cast<SiteNavButton>().ToList();
        var index = buttons.IndexOf(nav);
        if (index < 0 || buttons.Count == 0)
        {
            return;
        }
        e.Handled = true;
        var next = (index + (e.Key == Key.Right ? 1 : -1) + buttons.Count) % buttons.Count;
        buttons[next].Focus(NavigationMethod.Directional, e.KeyModifiers);
        if (_openValue is not null && ItemOf(buttons[next]) is { } target)
        {
            if (HasPanel(target))
            {
                OpenPanelCore(target.Value, false);
            }
            else
            {
                ClosePanelCore(false);
            }
        }
    }

    private void OnPanelKeyDown(KeyEventArgs e, Visual source)
    {
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End))
        {
            return;
        }
        var links = _panelView!.FocusTargets();
        if (links.Count == 0)
        {
            return;
        }
        e.Handled = true;
        var index = links.FindIndex(l => l == source || l.IsVisualAncestorOf(source));
        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => links.Count - 1,
            Key.Down => Math.Min(index + 1, links.Count - 1),
            _ => index - 1
        };
        if (next < 0)
        {
            ClosePanelCore(true);
            return;
        }
        links[next].Focus(NavigationMethod.Directional, e.KeyModifiers);
    }

    // ---- outside presses and Escape (Arc: document listeners while something is open) ------------------------

    private void AttachOutside()
    {
        if (_top is not null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }
        _top = top;
        top.AddHandler(InputElement.PointerPressedEvent, OnOutsidePressed, RoutingStrategies.Tunnel, true);
        top.AddHandler(InputElement.KeyDownEvent, OnTopKeyDown, RoutingStrategies.Tunnel, true);
    }

    private void DetachOutside()
    {
        if (_top is null)
        {
            return;
        }
        _top.RemoveHandler(InputElement.PointerPressedEvent, OnOutsidePressed);
        _top.RemoveHandler(InputElement.KeyDownEvent, OnTopKeyDown);
        _top = null;
    }

    private void UpdateOutside()
    {
        if (_openValue is not null || IsMenuOpen)
        {
            AttachOutside();
        }
        else
        {
            DetachOutside();
        }
    }

    private void OnOutsidePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual visual && (visual == this || this.IsVisualAncestorOf(visual)))
        {
            return;
        }
        StopTimers();
        ClosePanelCore(false);
        CloseMenuCore(false);
    }

    private void OnTopKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }
        if (_openValue is not null)
        {
            ClosePanelCore(true);
        }
        else if (IsMenuOpen)
        {
            CloseMenuCore(true);
        }
    }

    // ---- scrolling and sticking -----------------------------------------------------------------------------

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncScrollSource();
        UpdateOutside();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimers();
        DetachOutside();
        HookScroll(null);
    }

    private void OnScrollSourceChanged(ScrollViewer? oldValue, ScrollViewer? newValue) => SyncScrollSource();

    private void SyncScrollSource()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        HookScroll(ScrollSource ?? this.FindAncestorOfType<ScrollViewer>());
    }

    private void HookScroll(ScrollViewer? scroll)
    {
        if (ReferenceEquals(scroll, _scroll))
        {
            return;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll.RemoveHandler(InputElement.PointerWheelChangedEvent, OnScrollWheel);
        }
        _scroll = scroll;
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
            _scroll.AddHandler(InputElement.PointerWheelChangedEvent, OnScrollWheel, RoutingStrategies.Tunnel, true);
        }
        UpdateScrolled();
        UpdateSticky();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateScrolled();
        UpdateSticky();
        UpdateSheetLimits();
    }

    /// <summary>While the sheet is open the page stays where it is: the wheel does not scroll it (the sheet's own list still scrolls).</summary>
    private void OnScrollWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!IsMenuOpen || _sheet is null)
        {
            return;
        }
        if (e.Source is Visual visual && _sheet.IsVisualAncestorOf(visual))
        {
            return;
        }
        e.Handled = true;
    }

    private void UpdateScrolled()
    {
        var y = _scroll?.Offset.Y ?? 0;
        // Solid past the threshold, clear again only near the top: no flicker at the edge.
        IsScrolled = IsScrolled ? y > ScrollThreshold / 2 : y > ScrollThreshold;
        UpdatePseudoClasses();
    }

    private void UpdateSticky()
    {
        if (!IsSticky || _scroll is null || !_scroll.IsVisualAncestorOf(this))
        {
            _stick.Y = 0;
            return;
        }
        // Avalonia has no position: sticky. The header's own place in the scrolled content is summed from the layout
        // bounds (they do not include render transforms); past it the header is translated to stay at the viewport's top.
        var basePos = 0.0;
        Visual? v = this;
        while (v is not null && v.GetVisualParent() is { } parent && parent is not ScrollContentPresenter)
        {
            basePos += v.Bounds.Y;
            v = parent;
        }
        _stick.Y = Math.Max(0, _scroll.Offset.Y - basePos);
    }

    /// <summary>The height of the viewport the header sits in: the scroll view's, else the window's (the sheet's and the scrim's size).</summary>
    internal double ViewportHeight()
    {
        if (_scroll is { } scroll && scroll.Viewport.Height > 0)
        {
            return scroll.Viewport.Height;
        }
        if (TopLevel.GetTopLevel(this) is { } top && top.ClientSize.Height > 0)
        {
            return top.ClientSize.Height;
        }
        return 800;
    }
}
