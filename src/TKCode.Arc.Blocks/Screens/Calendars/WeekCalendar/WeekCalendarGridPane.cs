using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The grid of one range (Arc's <c>pane</c>): a cell per day (focusable, the weekend shaded), the events laid out side by
/// side, and the current time line on today's range. Several coexist while a range slides; the grid view pools them and
/// <see cref="Bind"/> reuses their cells and event blocks (keyed by event and day), so a slide never rebuilds a week.
/// Event positions go through <see cref="Flip"/>: unarmed they move at once; the editing stage arms it for its flights.
/// </summary>
internal sealed partial class WeekCalendarGridPane : Control
{
    private readonly WeekCalendar _owner;
    private readonly List<WeekCalendarDayCell> _cells = new();
    private readonly List<WeekCalendarEventPresenter> _presenters = new();
    private readonly Dictionary<(WeekCalendarEvent Event, DateOnly Day), WeekCalendarEventPresenter> _byKey = new();
    private readonly Stack<WeekCalendarEventPresenter> _free = new();
    private readonly WeekCalendarNowLayer _nowRow;
    private readonly WeekCalendarNowLayer _nowMarker;

    public WeekCalendarGridPane(WeekCalendar owner)
    {
        _owner = owner;
        Flip = new ProjectsFlip(this);
        _nowRow = new WeekCalendarNowLayer(owner, marker: false) { ZIndex = 1 };
        _nowMarker = new WeekCalendarNowLayer(owner, marker: true) { ZIndex = 9 };
        AddChild(_nowRow);
        AddChild(_nowMarker);
        ClipToBounds = false;
    }

    /// <summary>Position flights of the event blocks (the editing stage arms it).</summary>
    public ProjectsFlip Flip { get; }

    /// <summary>The days shown.</summary>
    public WeekCalendarDays Days { get; private set; }

    /// <summary>The day cells, left to right.</summary>
    public IReadOnlyList<WeekCalendarDayCell> Cells => _cells.Take(Days.Count).ToList();

    /// <summary>The event blocks shown, in time order (day, then start).</summary>
    public IReadOnlyList<WeekCalendarEventPresenter> Presenters => _presenters;

    /// <summary>The cell of <paramref name="day"/>, or null when it is not shown.</summary>
    public WeekCalendarDayCell? CellFor(DateOnly day)
    {
        var column = Days.ColumnOf(day);
        return column >= 0 && column < Days.Count && column < _cells.Count ? _cells[column] : null;
    }

    /// <summary>The block of <paramref name="calendarEvent"/> (its first day shown), or null.</summary>
    public WeekCalendarEventPresenter? PresenterFor(WeekCalendarEvent calendarEvent) =>
        _presenters.FirstOrDefault(p => ReferenceEquals(p.Event, calendarEvent));

    private void AddChild(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    // Binding --------------------------------------------------------------------------------------------------------------

    /// <summary>Shows <paramref name="days"/> with <paramref name="cursor"/> as the grid's tab stop.</summary>
    public void Bind(WeekCalendarDays days, DateOnly cursor)
    {
        Days = days;
        var index = _owner.Index;
        var grid = _owner.TimeGrid;
        var culture = _owner.EffectiveCulture;
        var labels = _owner.Labels;
        var today = _owner.Today;
        while (_cells.Count < days.Count)
        {
            var cell = new WeekCalendarDayCell(_owner, this) { ZIndex = 0 };
            _cells.Add(cell);
            AddChild(cell);
        }
        for (var i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            if (i >= days.Count)
            {
                cell.IsVisible = false;
                continue;
            }
            var day = days.DayAt(i);
            cell.IsVisible = true;
            cell.Bind(day, day == today, day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && _owner.ShowWeekendShading,
                labels.DayCell(WeekCalendarTime.DayName(day, culture), day == today, index.CountOn(day)));
        }
        SetCursor(cursor);

        // Event blocks: reuse the block an event had (same day), then free blocks, then new ones.
        var wanted = new List<(WeekCalendarSegment Segment, double Start, double End, WeekCalendarColumnSlot Slot)>();
        for (var i = 0; i < days.Count; i++)
        {
            var segments = index.TimedInHours(days.DayAt(i), grid);
            var slots = WeekCalendarOverlapLayout.Layout(segments.Select(s => (s.Start, s.End)).ToList());
            for (var j = 0; j < segments.Count; j++)
            {
                wanted.Add((segments[j].Segment, segments[j].Start, segments[j].End, slots[j]));
            }
        }
        var keep = new HashSet<(WeekCalendarEvent, DateOnly)>(wanted.Select(w => (w.Segment.Event, w.Segment.Day)));
        // Blocks whose event left their day: an event that moved to another day keeps its block, so it flies from where it was.
        var orphans = new Dictionary<WeekCalendarEvent, Stack<WeekCalendarEventPresenter>>();
        foreach (var (key, presenter) in _byKey.ToList())
        {
            if (!keep.Contains(key))
            {
                _byKey.Remove(key);
                if (!orphans.TryGetValue(key.Event, out var stack))
                {
                    stack = new Stack<WeekCalendarEventPresenter>();
                    orphans[key.Event] = stack;
                }
                stack.Push(presenter);
            }
        }
        _presenters.Clear();
        var tab = 1;
        var selected = _owner.SelectedEvent;
        foreach (var (segment, start, end, slot) in wanted)
        {
            var key = (segment.Event, segment.Day);
            if (!_byKey.TryGetValue(key, out var presenter))
            {
                if (orphans.TryGetValue(segment.Event, out var stack) && stack.Count > 0)
                {
                    presenter = stack.Pop();
                }
                else
                {
                    presenter = _free.Count > 0 ? _free.Pop() : CreatePresenter();
                    presenter.IsVisible = true;
                }
                _byKey[key] = presenter;
            }
            presenter.Bind(segment, start, end);
            presenter.Slot = slot;
            presenter.IsSelected = ReferenceEquals(segment.Event, selected);
            presenter.ZIndex = ZIndexOf(presenter);
            KeyboardNavigation.SetTabIndex(presenter, tab++);
            _presenters.Add(presenter);
        }
        foreach (var stack in orphans.Values)
        {
            while (stack.Count > 0)
            {
                var presenter = stack.Pop();
                if (ReferenceEquals(presenter, _lifted))
                {
                    EndLift();
                }
                Release(presenter);
            }
        }
        AutomationProperties.SetName(this, WeekCalendarTime.RangeTitle(days.Start, days.End, culture));
        AutomationProperties.SetHelpText(this, _owner.IsEditable ? labels.GridHelp + " " + labels.EditingHelp : labels.GridHelp);
        UpdateNow();
        InvalidateMeasure();
        InvalidateArrange();
    }

    private WeekCalendarEventPresenter CreatePresenter()
    {
        var presenter = new WeekCalendarEventPresenter(_owner) { ZIndex = 5 };
        AddChild(presenter);
        return presenter;
    }

    private void Release(WeekCalendarEventPresenter presenter)
    {
        presenter.IsVisible = false;
        Flip.Forget(presenter);
        _free.Push(presenter);
    }

    /// <summary>Makes <paramref name="cursor"/>'s cell the grid's only tab stop.</summary>
    public void SetCursor(DateOnly cursor)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            var isCursor = i < Days.Count && Days.DayAt(i) == cursor;
            KeyboardNavigation.SetIsTabStop(_cells[i], isCursor);
            _cells[i].IsCursor = isCursor;
        }
    }

    /// <summary>The clock moved: the time line and the past blocks.</summary>
    public void UpdateNow()
    {
        var now = _owner.CurrentNow;
        foreach (var presenter in _presenters)
        {
            presenter.UpdatePast(now);
        }
        var today = _owner.Today;
        var column = Days.ColumnOf(today);
        var grid = _owner.TimeGrid;
        var minute = _owner.NowWall.TimeOfDay.TotalMinutes;
        var shown = _owner.ShowNowIndicator && Days.Contains(today) && minute >= grid.StartMinute && minute <= grid.EndMinute;
        _nowRow.Set(shown, grid.YOf(minute), column, Days.Count);
        _nowMarker.Set(shown, grid.YOf(minute), column, Days.Count);
    }

    /// <summary>The theme changed.</summary>
    public void OnThemeChanged()
    {
        foreach (var presenter in _presenters)
        {
            presenter.OnThemeChanged();
        }
        foreach (var cell in _cells)
        {
            cell.InvalidateVisual();
        }
        _nowRow.InvalidateVisual();
        _nowMarker.InvalidateVisual();
    }

    /// <summary>Takes the pane out of (or back into) the tab order and hit testing (a pane on its way out).</summary>
    public void SetActive(bool active)
    {
        IsHitTestVisible = active;
        IsEnabled = active;
    }

    // Hit testing for the editing stage --------------------------------------------------------------------------------------

    /// <summary>The day and the minute of the day under <paramref name="point"/> (pane coordinates), unsnapped.</summary>
    public (DateOnly Day, double Minute) TimeAt(Point point)
    {
        var column = WeekCalendarColumns.At(point.X, Days.Count, Bounds.Width);
        return (Days.DayAt(column), _owner.TimeGrid.MinuteAt(point.Y));
    }

    /// <summary>The day and the minute under <paramref name="point"/>, rounded down to <paramref name="step"/> minutes.</summary>
    public (DateOnly Day, int Minute) SnappedTimeAt(Point point, int step = WeekCalendarTimeGrid.SnapMinutes)
    {
        var (day, minute) = TimeAt(point);
        return (day, WeekCalendarTimeGrid.SnapDown((int)Math.Floor(minute), step));
    }

    /// <summary>The rect a block from <paramref name="start"/> to <paramref name="end"/> on <paramref name="day"/> takes at full width.</summary>
    public Rect BoxFor(DateOnly day, double start, double end, WeekCalendarColumnSlot? slot = null)
    {
        var column = WeekCalendarColumns.Column(Days.ColumnOf(day), Days.Count, Bounds.Size);
        var s = slot ?? new WeekCalendarColumnSlot(0, 1, 1);
        var (top, height) = _owner.TimeGrid.EventBox(start, end);
        return new Rect(column.X + column.Width * s.Left, top, column.Width * s.Width, height);
    }

    // Layout ---------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var height = _owner.TimeGrid.BodyHeight;
        var size = new Size(width, height);
        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].Measure(i < Days.Count ? WeekCalendarColumns.Column(i, Days.Count, size).Size : default);
        }
        foreach (var presenter in _presenters)
        {
            presenter.Measure(RectOf(presenter, size).Size);
        }
        _nowRow.Measure(size);
        _nowMarker.Measure(size);
        MeasureEditing(size);
        return size;
    }

    private Rect RectOf(WeekCalendarEventPresenter presenter, Size size)
    {
        if (ReferenceEquals(presenter, _lifted) && _liftedAt is { } at)
        {
            // A dragged block takes its whole day column at the time it is dragged to (Arc: no relayout while dragging).
            var col = WeekCalendarColumns.Column(Days.ColumnOf(at.Day), Days.Count, size);
            var (liftTop, liftHeight) = _owner.TimeGrid.EventBox(at.Start, at.End);
            return new Rect(col.X, liftTop, col.Width, liftHeight);
        }
        var column = WeekCalendarColumns.Column(Days.ColumnOf(presenter.Day), Days.Count, size);
        var slot = presenter.Slot;
        var (top, height) = _owner.TimeGrid.EventBox(presenter.StartMinute, presenter.EndMinute);
        return new Rect(column.X + column.Width * slot.Left, top, column.Width * slot.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].Arrange(i < Days.Count ? WeekCalendarColumns.Column(i, Days.Count, finalSize) : default);
        }
        foreach (var presenter in _presenters)
        {
            var rect = RectOf(presenter, finalSize);
            presenter.Arrange(rect);
            Flip.Place(presenter, rect);
        }
        _nowRow.Arrange(new Rect(finalSize));
        _nowMarker.Arrange(new Rect(finalSize));
        ArrangeEditing(finalSize);
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.Table);
}

/// <summary>
/// One day of a grid pane (Arc's <c>gridcell</c>): the weekend shade, the column's left rule, the half-hour slot under the
/// pointer with its time, and the keyboard focus (muted fill and ring). Only the cursor day is a tab stop.
/// </summary>
internal sealed partial class WeekCalendarDayCell : Control
{
    /// <summary>0 at rest, 1 while the pointer is over the cell (the theme fades the slot in 160ms).</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarDayCell, double>(nameof(HoverAmount));

    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _slotText = new();
    private double _pointerY = double.NaN;

    static WeekCalendarDayCell()
    {
        FocusableProperty.OverrideDefaultValue<WeekCalendarDayCell>(true);
        AffectsRender<WeekCalendarDayCell>(HoverAmountProperty, IsFocusedProperty);
    }

    public WeekCalendarDayCell(WeekCalendar owner, WeekCalendarGridPane pane)
    {
        _owner = owner;
        Pane = pane;
    }

    /// <summary>The pane the cell belongs to.</summary>
    public WeekCalendarGridPane Pane { get; }

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    /// <summary>The day.</summary>
    public DateOnly Day { get; private set; }

    /// <summary>True for today.</summary>
    public bool IsToday { get; private set; }

    /// <summary>True for a shaded weekend day.</summary>
    public bool IsShaded { get; private set; }

    /// <summary>True for the cursor day (the grid's tab stop).</summary>
    public bool IsCursor { get; set; }

    /// <summary>The name read by screen readers.</summary>
    public string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    public void Bind(DateOnly day, bool isToday, bool shaded, string name)
    {
        Day = day;
        IsToday = isToday;
        IsShaded = shaded;
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var y = e.GetPosition(this).Y;
        var slot = _owner.TimeGrid.SlotAt(y);
        if (double.IsNaN(_pointerY) || _owner.TimeGrid.SlotAt(_pointerY) != slot)
        {
            _pointerY = y;
            InvalidateVisual();
        }
        _pointerY = y;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
        {
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _owner.SetCursor(Day, focusCell: true);
        }
    }

    /// <summary>The first minute of the half-hour slot the pointer is over, or null.</summary>
    public int? HoverSlot => double.IsNaN(_pointerY) || !IsPointerOver ? null : _owner.TimeGrid.SlotAt(_pointerY);

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var bounds = new Rect(Bounds.Size);
        // The whole column takes the pointer, not only its painted parts.
        context.FillRectangle(Brushes.Transparent, bounds);
        if (IsShaded)
        {
            context.FillRectangle(paint.WeekendShade, bounds);
        }
        var focused = Classes.Contains(":focus-visible");
        if (focused)
        {
            context.FillRectangle(paint.CellFocus, bounds);
        }
        context.FillRectangle(paint.BorderSubtle, new Rect(0, 0, 1, bounds.Height));
        var hover = HoverAmount;
        if (hover > 0.001 && !double.IsNaN(_pointerY))
        {
            var grid = _owner.TimeGrid;
            var slot = grid.SlotAt(_pointerY);
            var top = grid.YOf(slot);
            var height = WeekCalendarTimeGrid.SlotMinutes / 60.0 * grid.HourHeight;
            using (context.PushOpacity(hover))
            {
                context.DrawRectangle(paint.Ghost, null, new RoundedRect(new Rect(0, top, bounds.Width, height), 8));
                var label = _slotText.Get(WeekCalendarTime.ShortTime(slot, _owner.EffectiveCulture), paint.Body, 12, paint.TextMuted,
                    Math.Max(0, bounds.Width - 16), 1, 14.4);
                label?.Draw(context, new Point(8, top + 3));
            }
        }
        if (focused)
        {
            // The 2px ring 2px outside the column; the grid clips its top and bottom, leaving two rules.
            context.DrawRectangle(null, paint.FocusRing, new Rect(-3, -3, bounds.Width + 6, bounds.Height + 6));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.DataItem);
}

/// <summary>The current time across a grid pane: the thin row under the events, or today's thick segment and dot over them.</summary>
internal sealed class WeekCalendarNowLayer : Control
{
    private readonly WeekCalendar _owner;
    private readonly bool _marker;
    private bool _shown;
    private double _y;
    private int _column;
    private int _columns;

    public WeekCalendarNowLayer(WeekCalendar owner, bool marker)
    {
        _owner = owner;
        _marker = marker;
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>True while drawn.</summary>
    public bool IsShown => _shown;

    /// <summary>The y of the line.</summary>
    public double Y => _y;

    public void Set(bool shown, double y, int column, int columns)
    {
        if (shown == _shown && Math.Abs(y - _y) < 0.01 && column == _column && columns == _columns)
        {
            return;
        }
        _shown = shown;
        _y = y;
        _column = column;
        _columns = columns;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_shown)
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var size = Bounds.Size;
        if (!_marker)
        {
            context.FillRectangle(paint.NowRow, new Rect(0, _y - 0.5, size.Width, 1));
            return;
        }
        var column = WeekCalendarColumns.Column(_column, _columns, size);
        context.FillRectangle(paint.Accent, new Rect(column.X, _y - 1, column.Width, 2));
        var center = new Point(column.X - 2, _y);
        context.DrawEllipse(paint.Surface, null, center, 7, 7);
        context.DrawEllipse(paint.Accent, null, center, 5, 5);
    }
}

/// <summary>An automation peer with a fixed control type (the grid, its cells, the all-day bars' row).</summary>
internal sealed class WeekCalendarGroupPeer(Control owner, AutomationControlType type) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => type;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
