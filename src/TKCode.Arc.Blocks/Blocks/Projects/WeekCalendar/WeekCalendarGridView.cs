using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The three panes of one range that travel together (header row, all-day row, grid), with the spring of their offset and
/// their fade. Pooled by <see cref="WeekCalendarGridView"/>: a slot that has slid out is hidden and reused for the next range.
/// </summary>
internal sealed class WeekCalendarPaneSlot
{
    private readonly TranslateTransform _headShift = new();
    private readonly TranslateTransform _allDayShift = new();
    private readonly TranslateTransform _gridShift = new();

    public WeekCalendarPaneSlot(WeekCalendar owner, Visual motionOwner)
    {
        Head = new WeekCalendarHeadPane(owner) { RenderTransform = _headShift };
        AllDay = new WeekCalendarAllDayPane(owner) { RenderTransform = _allDayShift };
        Grid = new WeekCalendarGridPane(owner) { RenderTransform = _gridShift };
        X = new MotionValue<double>(motionOwner, 0, MotionVectors.Double);
        Fade = new MotionValue<double>(motionOwner, 1, MotionVectors.Double);
        X.Changed += ApplyX;
        Fade.Changed += ApplyFade;
    }

    public WeekCalendarHeadPane Head { get; }

    public WeekCalendarAllDayPane AllDay { get; }

    public WeekCalendarGridPane Grid { get; }

    /// <summary>The panes' horizontal offset (px; 0 = in place).</summary>
    public MotionValue<double> X { get; }

    /// <summary>The panes' opacity.</summary>
    public MotionValue<double> Fade { get; }

    /// <summary>The days shown.</summary>
    public WeekCalendarDays Days { get; private set; }

    /// <summary>True while on its way out.</summary>
    public bool IsLeaving { get; set; }

    /// <summary>Changes every time the slot is recycled (stale completions check it).</summary>
    public int Generation { get; set; }

    /// <summary>True while its offset or fade is moving.</summary>
    public bool IsMoving => X.IsAnimating || Fade.IsAnimating;

    public void Bind(WeekCalendarDays days, DateOnly cursor)
    {
        Days = days;
        Head.Bind(days, cursor);
        AllDay.Bind(days);
        Grid.Bind(days, cursor);
    }

    public void SetVisible(bool visible)
    {
        Head.IsVisible = visible;
        AllDay.IsVisible = visible;
        Grid.IsVisible = visible;
    }

    /// <summary>A leaving slot takes no input and leaves the tab order.</summary>
    public void SetActive(bool active)
    {
        Head.IsHitTestVisible = active;
        Head.IsEnabled = active;
        AllDay.SetActive(active);
        Grid.SetActive(active);
    }

    private void ApplyX(double x)
    {
        _headShift.X = x;
        _allDayShift.X = x;
        _gridShift.X = x;
    }

    private void ApplyFade(double opacity)
    {
        Head.Opacity = opacity;
        AllDay.Opacity = opacity;
        Grid.Opacity = opacity;
    }
}

/// <summary>
/// The time grid of the week calendar (notes §3.4): the header row and the all-day row stay above a vertical scroller
/// (Avalonia has no sticky positioning; their columns are computed with the grid's own edges, so they line up to the pixel),
/// and the time gutter scrolls with the hours. Ranges change by sliding (or fading) pane slots, up to three at a time, each
/// carrying its speed when interrupted; finished slots are pooled.
/// </summary>
internal sealed partial class WeekCalendarGridView : Control
{
    /// <summary>At most this many ranges are on screen at once while slides overlap.</summary>
    public const int MaxSlots = 3;

    private readonly WeekCalendar _owner;
    private readonly TextBlock _corner;
    private readonly Panel _headViewport;
    private readonly TextBlock _allDayLabel;
    private readonly Button _expandButton;
    private readonly ArcGlyph _expandGlyph;
    private readonly Panel _allDayViewport;
    private readonly ScrollArea _scroll;
    private readonly WeekCalendarBody _body;
    private readonly WeekCalendarRules _rules;
    private readonly List<WeekCalendarPaneSlot> _slots = new();
    private readonly Stack<WeekCalendarPaneSlot> _pool = new();
    private readonly MotionValue<double> _allDayHeight;
    private bool _initialScrollDone;
    private bool _stacked;
    private bool _tight;
    private bool _focusWithin;

    public WeekCalendarGridView(WeekCalendar owner)
    {
        _owner = owner;
        _corner = new TextBlock
        {
            FontSize = 10.5,
            LineHeight = 14.7,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 10, 8),
            IsHitTestVisible = false,
        };
        AutomationProperties.SetAccessibilityView(_corner, AccessibilityView.Raw);
        _headViewport = new Panel { ClipToBounds = true };
        _allDayLabel = new TextBlock
        {
            FontSize = 10.5,
            LineHeight = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 8, 0),
            TextWrapping = TextWrapping.NoWrap,
            IsHitTestVisible = false,
        };
        _expandGlyph = new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 14, StrokeThickness = 2 };
        _expandButton = new Button
        {
            Variant = ButtonVariant.Ghost,
            Content = _expandGlyph,
            Padding = default,
            MinHeight = 0,
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 26, 6, 0),
            IsVisible = false,
        };
        _expandButton.Click += OnExpandClick;
        _allDayViewport = new Panel { ClipToBounds = true };
        _body = new WeekCalendarBody(owner);
        _scroll = new ScrollArea
        {
            Content = _body,
            FadeLength = 0,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        _scroll.ScrollChanged += OnScrollChanged;
        _rules = new WeekCalendarRules(owner) { IsHitTestVisible = false };
        foreach (var child in new Control[] { _corner, _headViewport, _allDayLabel, _expandButton, _allDayViewport, _scroll, _rules })
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
        _allDayHeight = new MotionValue<double>(this, WeekCalendarAllDayLayout.RowPadding + WeekCalendarAllDayLayout.LaneHeight,
            MotionVectors.Double);
        _allDayHeight.Changed += _ => InvalidateMeasure();
        AddHandler(GotFocusEvent, OnFocusChanged, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, OnFocusChanged, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Bubble);
        InitializeEditing();
    }

    /// <summary>The slot of the range shown (the one arriving while a slide runs).</summary>
    public WeekCalendarPaneSlot? Current => _slots.Count > 0 ? _slots[^1] : null;

    /// <summary>Every slot on screen, oldest first.</summary>
    public IReadOnlyList<WeekCalendarPaneSlot> Slots => _slots;

    /// <summary>Slots waiting for reuse.</summary>
    public int PooledCount => _pool.Count;

    /// <summary>The scroller of the hours.</summary>
    public ScrollArea Scroller => _scroll;

    /// <summary>The scrolling body (gutter, rules and grid panes).</summary>
    public WeekCalendarBody Body => _body;

    /// <summary>The all-day row's expand button.</summary>
    public Button ExpandButton => _expandButton;

    /// <summary>The height of the all-day row now (it animates when expanding).</summary>
    public double AllDayHeight => _allDayHeight.Value;

    /// <summary>The time zone label in the corner.</summary>
    public string CornerText => _corner.Text ?? string.Empty;

    /// <summary>True when keyboard focus is in the grid (a cell, an event, an all-day bar).</summary>
    public bool HasFocusWithin => IsKeyboardFocusWithin;

    /// <summary>True when the focused element shows a keyboard ring.</summary>
    public bool FocusVisible =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control c && c.Classes.Contains(":focus-visible");

    // Ranges ---------------------------------------------------------------------------------------------------------------

    /// <summary>Shows <paramref name="days"/>: in place, sliding toward <paramref name="direction"/>, or fading.</summary>
    public void Show(WeekCalendarDays days, DateOnly cursor, WeekCalendarTransition transition, int direction)
    {
        if (ContinueSwipe(days, transition, direction))
        {
            Current!.Bind(days, cursor);
            return;
        }
        var current = Current;
        var width = _headViewport.Bounds.Width;
        if (current is null || transition == WeekCalendarTransition.None || !(width > 0))
        {
            foreach (var leaving in _slots.Where(s => !ReferenceEquals(s, current)).ToList())
            {
                Recycle(leaving);
            }
            current ??= Rent();
            current.IsLeaving = false;
            current.SetActive(true);
            current.X.Jump(0);
            current.Fade.Jump(1);
            current.Bind(days, cursor);
            AfterRangeChanged(animate: false);
            return;
        }
        if (current.Days == days)
        {
            current.Bind(days, cursor);
            return;
        }
        current.IsLeaving = true;
        current.SetActive(false);
        while (_slots.Count >= MaxSlots)
        {
            Recycle(_slots[0]);
        }
        var incoming = Rent();
        incoming.IsLeaving = false;
        incoming.SetActive(true);
        incoming.Bind(days, cursor);
        if (transition == WeekCalendarTransition.Slide)
        {
            var travel = Math.Sign(direction) * width;
            incoming.Fade.Jump(1);
            incoming.X.Jump(travel);
            // The range slide is driven in code: several panes share one axis, each keeps its speed when retargeted.
            _ = incoming.X.AnimateTo(0, MotionSpec.Spring(WeekCalendarMotion.RangeSlide));
            Leave(current, current.X.AnimateTo(-travel, MotionSpec.Spring(WeekCalendarMotion.RangeSlide)));
        }
        else
        {
            incoming.X.Jump(0);
            incoming.Fade.Jump(0);
            _ = incoming.Fade.AnimateTo(1, WeekCalendarMotion.FadeIn());
            Leave(current, current.Fade.AnimateTo(0, WeekCalendarMotion.FadeOut()));
        }
        AfterRangeChanged(animate: false);
        UpdateSliding();
    }

    private async void Leave(WeekCalendarPaneSlot slot, Task<bool> motion)
    {
        var generation = slot.Generation;
        var done = await motion;
        if (done && slot.Generation == generation && slot.IsLeaving && _slots.Contains(slot))
        {
            Recycle(slot);
        }
        UpdateSliding();
    }

    private void UpdateSliding() =>
        _owner.SetSliding(_slots.Any(s => s.IsMoving) || _slots.Count > 1);

    private WeekCalendarPaneSlot Rent()
    {
        WeekCalendarPaneSlot slot;
        if (_pool.Count > 0)
        {
            slot = _pool.Pop();
        }
        else
        {
            slot = new WeekCalendarPaneSlot(_owner, this);
            _headViewport.Children.Add(slot.Head);
            _allDayViewport.Children.Add(slot.AllDay);
            _body.Viewport.Children.Add(slot.Grid);
            slot.Head.SetTight(_tight);
        }
        slot.SetVisible(true);
        _slots.Add(slot);
        // The arriving panes draw over the leaving ones.
        Restack();
        return slot;
    }

    private void Recycle(WeekCalendarPaneSlot slot)
    {
        _slots.Remove(slot);
        slot.Generation++;
        slot.IsLeaving = false;
        slot.X.Jump(0);
        slot.Fade.Jump(1);
        slot.SetVisible(false);
        slot.SetActive(true);
        _pool.Push(slot);
    }

    private void Restack()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            _slots[i].Head.ZIndex = i;
            _slots[i].AllDay.ZIndex = i;
            _slots[i].Grid.ZIndex = i;
        }
    }

    /// <summary>The data or a display option changed: every slot redraws its days in place.</summary>
    public void Refresh()
    {
        foreach (var slot in _slots)
        {
            slot.Bind(slot.Days, _owner.Date);
        }
        if (Current is null && _owner.Range.Count > 0)
        {
            Show(_owner.Range, _owner.Date, WeekCalendarTransition.None, 0);
            return;
        }
        AfterRangeChanged(animate: false);
    }

    /// <summary>The cursor day moved inside the range.</summary>
    public void SetCursor(DateOnly cursor)
    {
        if (Current is { } slot)
        {
            slot.Grid.SetCursor(cursor);
            slot.Head.ShowCursor(_focusWithin, cursor);
        }
    }

    /// <summary>Focuses the cell of <paramref name="day"/> in the current range.</summary>
    public bool FocusDay(DateOnly day, NavigationMethod method)
    {
        if (Current?.Grid.CellFor(day) is not { } cell)
        {
            return false;
        }
        // Layout first: a cell created this turn is not focusable until it is measured.
        if (!cell.IsArrangeValid)
        {
            UpdateLayout();
        }
        return cell.Focus(method);
    }

    /// <summary>The clock moved.</summary>
    public void OnNowChanged()
    {
        var today = _owner.Today;
        foreach (var slot in _slots)
        {
            var headToday = slot.Head.DayButtons.FirstOrDefault(b => b.IsToday)?.Day;
            if (headToday != (slot.Days.Contains(today) ? today : (DateOnly?)null))
            {
                // The day rolled over: rebind for today's pill and cell names.
                slot.Bind(slot.Days, _owner.Date);
            }
            slot.Grid.UpdateNow();
        }
        _body.Gutter.InvalidateVisual();
    }

    public void OnThemeChanged()
    {
        foreach (var slot in _slots.Concat(_pool))
        {
            slot.Head.OnThemeChanged();
            slot.AllDay.OnThemeChanged();
            slot.Grid.OnThemeChanged();
        }
        _body.InvalidateVisual();
        _body.Gutter.InvalidateVisual();
        _rules.InvalidateVisual();
        UpdateTexts();
    }

    /// <summary>The toolbar went to two rows (smaller gutter labels) or the calendar got tight (smaller header dates).</summary>
    public void SetStacked(bool stacked, bool tight)
    {
        if (stacked != _stacked)
        {
            _stacked = stacked;
            _body.Gutter.IsStacked = stacked;
        }
        if (tight != _tight)
        {
            _tight = tight;
            foreach (var slot in _slots.Concat(_pool))
            {
                slot.Head.SetTight(tight);
            }
        }
    }

    private void AfterRangeChanged(bool animate)
    {
        UpdateTexts();
        UpdateAllDayHeight(animate);
        _body.InvalidateMeasure();
        _body.Gutter.InvalidateVisual();
    }

    private void UpdateTexts()
    {
        var range = _owner.Range;
        _corner.Text = WeekCalendarTime.OffsetLabel(WeekCalendarTime.OffsetOn(_owner.Zone, range.Count > 0 ? range.Start : _owner.Today));
        _allDayLabel.Text = _owner.Labels.AllDay;
        var paint = _owner.Paint;
        paint.EnsureReady();
        _corner.Foreground = paint.TextMuted;
        _allDayLabel.Foreground = paint.TextMuted;
        _corner.FontFamily = paint.BodyFamily;
        _allDayLabel.FontFamily = paint.BodyFamily;
    }

    // All-day row --------------------------------------------------------------------------------------------------------

    /// <summary>The lane limit or the expanded state changed.</summary>
    public void OnAllDayLayoutChanged(bool expandToggled)
    {
        foreach (var slot in _slots)
        {
            slot.AllDay.Bind(slot.Days);
        }
        UpdateAllDayHeight(expandToggled);
    }

    private void UpdateAllDayHeight(bool animate)
    {
        var lanes = Current?.AllDay.Lanes;
        var limit = _owner.AllDayLaneLimit;
        var expanded = _owner.IsAllDayExpanded;
        var target = lanes?.RowHeight(limit, expanded) ?? WeekCalendarAllDayLayout.RowPadding + WeekCalendarAllDayLayout.LaneHeight;
        var overflows = lanes?.Overflows(limit) == true;
        _expandButton.IsVisible = overflows;
        _expandGlyph.Data = expanded ? ArcIcons.ChevronUp : ArcIcons.ChevronDown;
        var name = expanded ? _owner.Labels.ShowFewerAllDayEvents : _owner.Labels.ShowAllDayEvents;
        AutomationProperties.SetName(_expandButton, name);
        Tooltip.SetTip(_expandButton, name);
        if (Math.Abs(_allDayHeight.Target - target) < 0.01 && !_allDayHeight.IsAnimating)
        {
            return;
        }
        if (animate && ArcMotion.IsFull(this))
        {
            // Code-driven: the row's height follows a toggle only, never a range change, which a theme transition cannot tell apart.
            _ = _allDayHeight.AnimateTo(target, MotionSpec.Tween(WeekCalendarMotion.AllDayResize, ArcEasings.Standard));
        }
        else
        {
            _allDayHeight.Jump(target);
        }
    }

    private void OnExpandClick(object? sender, RoutedEventArgs e) =>
        _owner.SetCurrentValue(WeekCalendar.IsAllDayExpandedProperty, !_owner.IsAllDayExpanded);

    // Scrolling ---------------------------------------------------------------------------------------------------------------

    /// <summary>Scrolls so <paramref name="minute"/> sits <paramref name="fraction"/> of the way down the visible hours.</summary>
    public void ScrollToMinute(double minute, double fraction, bool animate)
    {
        var grid = _owner.TimeGrid;
        var viewport = _scroll.Viewport.Height > 0 ? _scroll.Viewport.Height : BodyViewportHeight;
        var max = Math.Max(0, grid.BodyHeight - viewport);
        var offset = Math.Clamp(grid.YOf(minute) - fraction * viewport, 0, max);
        _initialScrollDone = true;
        if (animate)
        {
            _scroll.ScrollToOffset(new Vector(0, offset));
        }
        else
        {
            _scroll.Offset = new Vector(0, offset);
        }
    }

    /// <summary>The visible height of the hours.</summary>
    public double BodyViewportHeight => Math.Max(0, Bounds.Height - WeekCalendarMetrics.HeaderRowHeight - _allDayHeight.Value);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        TryInitialScroll();
        OnScrolledWhileEditing();
    }

    private void TryInitialScroll()
    {
        if (_initialScrollDone || _scroll.Extent.Height <= 0 || _scroll.Viewport.Height <= 0)
        {
            return;
        }
        _initialScrollDone = true;
        var grid = _owner.TimeGrid;
        double offset;
        if (_owner.InitialScrollTime is { } time)
        {
            offset = Math.Max(0, grid.YOf(time.ToTimeSpan().TotalMinutes));
        }
        else
        {
            offset = grid.InitialOffset(_owner.Index.EarliestMinute(_owner.Range, grid));
        }
        var max = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        _scroll.Offset = new Vector(0, Math.Min(offset, max));
    }

    // Focus and keys ---------------------------------------------------------------------------------------------------------

    private void OnFocusChanged(object? sender, RoutedEventArgs e)
    {
        var within = _body.IsKeyboardFocusWithin;
        if (within == _focusWithin)
        {
            return;
        }
        _focusWithin = within;
        if (Current is { } slot)
        {
            slot.Head.ShowCursor(within, _owner.Date);
        }
    }

    /// <summary>Lets the editing stage take keys first (N, Enter, Delete, Alt+arrows, Ctrl+Z).</summary>
    partial void OnEditingKey(KeyEventArgs e, ref bool handled);

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var handled = false;
        OnEditingKey(e, ref handled);
        if (handled)
        {
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var source = e.Source as Visual;
        var cell = source?.FindAncestorOfType<WeekCalendarDayCell>(includeSelf: true);
        var presenter = source?.FindAncestorOfType<WeekCalendarEventPresenter>(includeSelf: true);
        switch (e.Key)
        {
            case Key.Left or Key.Right when cell is not null:
                _owner.MoveCursor(e.Key == Key.Right ? 1 : -1, cell.Day);
                e.Handled = true;
                break;
            case Key.Left or Key.Right when presenter is not null:
                _owner.MoveCursor(e.Key == Key.Right ? 1 : -1, presenter.Day);
                e.Handled = true;
                break;
            case Key.Up or Key.Down when presenter is not null:
                e.Handled = FocusNeighbourEvent(presenter, e.Key == Key.Down ? 1 : -1);
                break;
            case Key.T when cell is not null || presenter is not null:
                _owner.GoToToday();
                e.Handled = true;
                break;
            case Key.D when cell is not null || presenter is not null:
                _owner.SwitchView(WeekCalendarView.Day);
                e.Handled = true;
                break;
            case Key.W when cell is not null || presenter is not null:
                _owner.SwitchView(WeekCalendarView.Week);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Up / Down on an event: the previous or next event of the range in time order.</summary>
    private bool FocusNeighbourEvent(WeekCalendarEventPresenter presenter, int step)
    {
        if (Current?.Grid is not { } grid)
        {
            return false;
        }
        var list = grid.Presenters;
        var index = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], presenter))
            {
                index = i;
                break;
            }
        }
        var next = index + step;
        if (index < 0 || next < 0 || next >= list.Count)
        {
            return false;
        }
        return list[next].Focus(NavigationMethod.Directional);
    }

    // Layout -----------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 800;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : _owner.EffectiveGridHeight;
        var gutter = WeekCalendarMetrics.GutterWidth;
        var header = WeekCalendarMetrics.HeaderRowHeight;
        var allDay = _allDayHeight.Value;
        _corner.Measure(new Size(gutter, header));
        _headViewport.Measure(new Size(Math.Max(0, width - gutter), header));
        _allDayLabel.Measure(new Size(gutter, allDay));
        _expandButton.Measure(new Size(gutter, allDay));
        _allDayViewport.Measure(new Size(Math.Max(0, width - gutter), allDay));
        _scroll.Measure(new Size(width, Math.Max(0, height - header - allDay)));
        _rules.Measure(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var gutter = WeekCalendarMetrics.GutterWidth;
        var header = WeekCalendarMetrics.HeaderRowHeight;
        var allDay = _allDayHeight.Value;
        _corner.Arrange(new Rect(0, 0, gutter, header));
        _headViewport.Arrange(new Rect(gutter, 0, Math.Max(0, width - gutter), header));
        _allDayLabel.Arrange(new Rect(0, header, gutter, allDay));
        _expandButton.Arrange(new Rect(0, header, gutter, allDay));
        _allDayViewport.Arrange(new Rect(gutter, header, Math.Max(0, width - gutter), allDay));
        _scroll.Arrange(new Rect(0, header + allDay, width, Math.Max(0, finalSize.Height - header - allDay)));
        _rules.AllDayTop = header;
        _rules.HeaderBottom = header + allDay;
        _rules.Arrange(new Rect(finalSize));
        if (!_initialScrollDone)
        {
            Dispatcher.UIThread.Post(TryInitialScroll, DispatcherPriority.Loaded);
        }
        return finalSize;
    }
}

/// <summary>The two rules over the header: under the date row (subtle) and under the all-day row (the header's edge).</summary>
internal sealed class WeekCalendarRules : Control
{
    private readonly WeekCalendar _owner;
    private double _allDayTop;
    private double _headerBottom;

    public WeekCalendarRules(WeekCalendar owner)
    {
        _owner = owner;
        Focusable = false;
    }

    public double AllDayTop
    {
        get => _allDayTop;
        set
        {
            if (Math.Abs(_allDayTop - value) > 0.01)
            {
                _allDayTop = value;
                InvalidateVisual();
            }
        }
    }

    public double HeaderBottom
    {
        get => _headerBottom;
        set
        {
            if (Math.Abs(_headerBottom - value) > 0.01)
            {
                _headerBottom = value;
                InvalidateVisual();
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var width = Bounds.Width;
        context.FillRectangle(paint.BorderSubtle, new Rect(0, _allDayTop, width, 1));
        context.FillRectangle(paint.Border, new Rect(0, _headerBottom, width, 1));
    }
}

/// <summary>
/// The scrolling body of the grid: the gutter on the left, the hour and half-hour rules, and the clipped viewport the grid
/// panes slide in. As tall as the hours shown.
/// </summary>
internal sealed class WeekCalendarBody : Control
{
    private readonly WeekCalendar _owner;

    public WeekCalendarBody(WeekCalendar owner)
    {
        _owner = owner;
        Gutter = new WeekCalendarGutter(owner);
        Viewport = new Panel { ClipToBounds = true };
        LogicalChildren.Add(Gutter);
        VisualChildren.Add(Gutter);
        LogicalChildren.Add(Viewport);
        VisualChildren.Add(Viewport);
    }

    public WeekCalendarGutter Gutter { get; }

    /// <summary>The clipped area the grid panes slide in.</summary>
    public Panel Viewport { get; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 800;
        var height = _owner.TimeGrid.BodyHeight;
        Gutter.Measure(new Size(WeekCalendarMetrics.GutterWidth, height));
        Viewport.Measure(new Size(Math.Max(0, width - WeekCalendarMetrics.GutterWidth), height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var gutter = WeekCalendarMetrics.GutterWidth;
        Gutter.Arrange(new Rect(0, 0, gutter, finalSize.Height));
        Viewport.Arrange(new Rect(gutter, 0, Math.Max(0, finalSize.Width - gutter), finalSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var grid = _owner.TimeGrid;
        var left = WeekCalendarMetrics.GutterWidth;
        var width = Math.Max(0, Bounds.Width - left);
        // No surface fill: the frame paints it, and an opaque fill here would cover the frame's rounded corners.
        for (var hour = grid.StartHour; hour < grid.EndHour; hour++)
        {
            var y = grid.HourY(hour);
            if (hour > grid.StartHour)
            {
                context.FillRectangle(paint.BorderSubtle, new Rect(left, y, width, 1));
            }
            context.FillRectangle(paint.HalfHourLine, new Rect(left, y + grid.HourHeight / 2, width, 1));
        }
    }
}

/// <summary>
/// The time gutter (notes §3.4–3.5): an hour label beside every line but the first, and the current time in an accent pill
/// when today is shown; labels the pill would touch are hidden.
/// </summary>
internal sealed class WeekCalendarGutter : Control
{
    private readonly WeekCalendar _owner;
    private readonly Dictionary<int, WeekCalendarText> _labels = new();
    private readonly WeekCalendarText _now = new();
    private bool _isStacked;

    public WeekCalendarGutter(WeekCalendar owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>10px labels on two-row toolbars (narrow calendars), else 10.5.</summary>
    public bool IsStacked
    {
        get => _isStacked;
        set
        {
            if (_isStacked != value)
            {
                _isStacked = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>The y of the now pill, or null when it is not drawn.</summary>
    public double? NowY
    {
        get
        {
            var grid = _owner.TimeGrid;
            var minute = _owner.NowWall.TimeOfDay.TotalMinutes;
            if (!_owner.ShowNowIndicator || !_owner.Range.Contains(_owner.Today) || minute < grid.StartMinute || minute > grid.EndMinute)
            {
                return null;
            }
            return grid.YOf(minute);
        }
    }

    /// <summary>True when the hour label at <paramref name="hour"/> is hidden by the now pill.</summary>
    public bool IsLabelHidden(int hour) =>
        NowY is { } y && Math.Abs(_owner.TimeGrid.HourY(hour) - y) < WeekCalendarMetrics.NowLabelClearance;

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var grid = _owner.TimeGrid;
        var culture = _owner.EffectiveCulture;
        var size = IsStacked ? 10.0 : 10.5;
        var right = Bounds.Width - 10;
        var nowY = NowY;
        for (var hour = grid.StartHour + 1; hour < grid.EndHour; hour++)
        {
            if (nowY is { } ny && Math.Abs(grid.HourY(hour) - ny) < WeekCalendarMetrics.NowLabelClearance)
            {
                continue;
            }
            if (!_labels.TryGetValue(hour, out var cache))
            {
                cache = new WeekCalendarText();
                _labels[hour] = cache;
            }
            var layout = cache.Get(WeekCalendarTime.HourLabel(hour, culture), paint.Body, size, paint.TextMuted, double.PositiveInfinity, 1, size);
            if (layout is null)
            {
                continue;
            }
            var y = grid.HourY(hour) - size / 2;
            layout.Draw(context, new Point(right - WeekCalendarText.NaturalWidth(layout), y));
        }
        if (nowY is { } pillY)
        {
            var text = _now.Get(WeekCalendarTime.FullTime(_owner.NowWall.TimeOfDay.TotalMinutes, culture), paint.BodyMedium, 10.5,
                paint.AccentForeground, double.PositiveInfinity, 1, 10.5);
            if (text is not null)
            {
                var width = WeekCalendarText.NaturalWidth(text) + 12;
                var pill = new Rect(Bounds.Width - 5 - width, pillY - 8.25, width, 16.5);
                context.DrawRectangle(paint.Accent, null, new RoundedRect(pill, 8.25));
                text.Draw(context, new Point(pill.X + 6, pill.Y + 3));
            }
        }
    }
}
