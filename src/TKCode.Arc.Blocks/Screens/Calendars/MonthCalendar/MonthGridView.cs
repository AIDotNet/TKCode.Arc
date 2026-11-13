using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The body of the Calendar view: the weekday row and the month grid; on a month change the old grid fades out where it is
/// (150 ms) while the new one fades in and slides from 12 px in the direction of travel (180 ms, enter curve; Arc Month
/// calendar, design reference: notes §5). Reduced and Off swap at once. In the compact month (narrower than 560) the grid and
/// the selected day's list scroll together under the weekday row.
/// </summary>
internal sealed class MonthMonthView : Control
{
    private readonly MonthCalendar _owner;
    private readonly MonthWeekdayRow _weekdays;
    private readonly Panel _gridHost = new() { ClipToBounds = true };
    private readonly ScrollViewer? _scroller;
    private readonly StackPanel? _compactStack;
    private readonly MonthPhoneDayList? _dayList;
    private MonthGridView _grid;
    private readonly bool _compact;

    public MonthMonthView(MonthCalendar owner)
    {
        _owner = owner;
        _compact = owner.IsCompact;
        _weekdays = new MonthWeekdayRow(owner) { Compact = _compact };
        _grid = new MonthGridView(owner, _compact);
        _gridHost.Children.Add(_grid);
        VisualChildren.Add(_weekdays);
        LogicalChildren.Add(_weekdays);
        if (_compact)
        {
            _dayList = new MonthPhoneDayList(owner);
            _compactStack = new StackPanel();
            _compactStack.Children.Add(_gridHost);
            _compactStack.Children.Add(_dayList);
            _scroller = new ScrollViewer
            {
                Content = _compactStack,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            };
            VisualChildren.Add(_scroller);
            LogicalChildren.Add(_scroller);
        }
        else
        {
            VisualChildren.Add(_gridHost);
            LogicalChildren.Add(_gridHost);
        }
    }

    public MonthGridView Grid => _grid;

    public void Refresh()
    {
        _weekdays.InvalidateVisual();
        _weekdays.Refresh();
        _grid.Refresh();
        _dayList?.Rebuild();
    }

    public void ChangeMonth(int direction)
    {
        var old = _grid;
        var fresh = new MonthGridView(_owner, _compact);
        _grid = fresh;
        _gridHost.Children.Add(fresh);
        fresh.Refresh();
        _dayList?.Rebuild();
        var hadFocus = old.IsKeyboardFocusWithin;
        if (!ArcMotion.IsFull(this))
        {
            _gridHost.Children.Remove(old);
            if (hadFocus)
            {
                _owner.FocusGridDay();
            }
            return;
        }
        old.IsHitTestVisible = false;
        var fade = new MotionValue<double>(old, 1, MotionVectors.Double);
        fade.Changed += v => old.Opacity = v;
        _ = Leave(fade, old);
        var shift = new TranslateTransform(12 * direction, 0);
        fresh.RenderTransform = shift;
        fresh.Opacity = 0;
        var enter = new MotionValue<double>(fresh, 0, MotionVectors.Double);
        enter.Changed += v =>
        {
            fresh.Opacity = v;
            shift.X = (1 - v) * 12 * direction;
        };
        _ = enter.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(180), ArcEasings.Enter));
        if (hadFocus)
        {
            _owner.FocusGridDay();
        }
    }

    private async Task Leave(MotionValue<double> fade, MonthGridView old)
    {
        await fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.InOut));
        _gridHost.Children.Remove(old);
    }

    public void FocusDay(DateOnly day) => _grid.FocusDay(day);

    protected override Size MeasureOverride(Size availableSize)
    {
        _weekdays.Measure(new Size(availableSize.Width, 32));
        var rest = new Size(availableSize.Width, Math.Max(0, availableSize.Height - 32));
        if (_scroller is not null)
        {
            _scroller.Measure(rest);
        }
        else
        {
            _gridHost.Measure(rest);
        }
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _weekdays.Arrange(new Rect(0, 0, finalSize.Width, 32));
        var rest = new Rect(0, 32, finalSize.Width, Math.Max(0, finalSize.Height - 32));
        if (_scroller is not null)
        {
            _scroller.Arrange(rest);
        }
        else
        {
            _gridHost.Arrange(rest);
        }
        return finalSize;
    }
}

/// <summary>The weekday row: 32 high, 12px muted names inset 12 (centred letters in the compact month), weekends at 80%, a divider below.</summary>
internal sealed class MonthWeekdayRow : Control
{
    private readonly MonthCalendar _owner;
    private readonly List<WeekCalendarText> _texts = new();

    public MonthWeekdayRow(MonthCalendar owner)
    {
        _owner = owner;
    }

    public bool Compact { get; init; }

    public void Refresh() => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        var columns = MonthCalendarModel.Columns(MonthCalendarModel.WeekStart(_owner.Month, _owner.FirstDayOfWeek), _owner.ShowWeekends);
        var w = Bounds.Width / Math.Max(1, columns.Count);
        var face = new Typeface(ScreenText.Body(this));
        var weekend = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.TextMuted, 0.8));
        var narrow = Compact || Bounds.Width < 562;
        for (var i = 0; i < columns.Count; i++)
        {
            while (_texts.Count <= i)
            {
                _texts.Add(new WeekCalendarText());
            }
            var day = columns[i];
            var name = MonthCalendarModel.WeekdayName(day.DayOfWeek, _owner.EffectiveCulture, narrow);
            var layout = _texts[i].Get(name, face, 12, MonthCalendarModel.IsWeekend(day) ? weekend : paint.MutedBrush, w - 12, 1,
                16.8, -0.13);
            if (layout is null)
            {
                continue;
            }
            var x = narrow ? i * w + (w - layout.WidthIncludingTrailingWhitespace) / 2 : i * w + 12;
            layout.Draw(context, new Point(x, (Bounds.Height - layout.Height) / 2));
        }
        context.FillRectangle(paint.SubtleBrush, new Rect(0, Bounds.Height - 1, Bounds.Width, 1));
    }
}

/// <summary>
/// One month of the grid (Arc Month calendar, design reference: notes §2.2): rows share the height, 7 (or 5) equal columns
/// with 1px dividers, out-of-month cells tinted 1.6%, the selected cell 4.5%, the hovered 2.6%. Day numbers are 24 circle
/// buttons; events are chips and bars in 21 px lanes from 29 below the cell top, with "N more" in the last lane when a day
/// overflows. Pressing a chip and moving 5 px drags it (a ghost follows, the target days light up); dragging across empty
/// days plans a span; a double click adds an event. The compact month (narrower than 560) draws 50 high rows with 32
/// circles and a load meter (one 5 × 3 tick per event, up to four) and no chips.
/// </summary>
internal sealed class MonthGridView : Control
{
    public const double DragThreshold = 5;

    private readonly MonthCalendar _owner;
    private readonly bool _compact;
    private readonly List<MonthDayButton> _days = new();
    private readonly List<MonthChip> _chips = new();
    private readonly List<MonthMoreButton> _more = new();
    private readonly List<MonthChip> _leaving = new();
    private DateOnly _month;
    private DateOnly _first;
    private int _weeks;
    private List<DateOnly>[] _rows = Array.Empty<List<DateOnly>>();
    private double _packedHeight = -1;
    private double _packedWidth = -1;
    private bool _dirty = true;
    private DateOnly? _hoverDay;
    private DateOnly? _rangeStart;
    private DateOnly? _rangeEnd;
    private (DateOnly Start, DateOnly End)? _dropRange;

    // Pointer state.
    private Point _pressPoint;
    private MonthChip? _pressChip;
    private DateOnly? _pressDay;
    private bool _dragging;
    private bool _spanning;
    private int _grabOffset;
    private MonthGhost? _ghost;
    private Vector _ghostOffset;
    private IPointer? _pointer;

    public MonthGridView(MonthCalendar owner, bool compact)
    {
        _owner = owner;
        _compact = compact;
        _month = owner.Month;
        ClipToBounds = false;
        Focusable = false;
    }

    public DateOnly Month => _month;

    public bool Compact => _compact;

    public IReadOnlyList<MonthChip> Chips => _chips;

    public IReadOnlyList<MonthMoreButton> MoreButtons => _more;

    public IReadOnlyList<MonthDayButton> DayButtons => _days;

    public int Weeks => _weeks;

    public void Refresh()
    {
        _dirty = true;
        InvalidateMeasure();
        InvalidateVisual();
        foreach (var d in _days)
        {
            d.InvalidateVisual();
        }
    }

    private double RowHeight(double height) => _compact ? 50 : height / Math.Max(1, _weeks);

    private double ColumnWidth(double width) => width / Math.Max(1, _rows.Length > 0 ? _rows[0].Count : 7);

    // Building ---------------------------------------------------------------------------------------------------------

    private void BuildDays()
    {
        var (first, weeks) = MonthCalendarModel.GridOf(_month, _owner.FirstDayOfWeek);
        _first = first;
        _weeks = weeks;
        _rows = new List<DateOnly>[weeks];
        for (var r = 0; r < weeks; r++)
        {
            _rows[r] = MonthCalendarModel.Columns(first.AddDays(r * 7), _owner.ShowWeekends);
        }
        var dates = _rows.SelectMany(r => r).ToList();
        while (_days.Count > dates.Count)
        {
            var last = _days[^1];
            _days.RemoveAt(_days.Count - 1);
            VisualChildren.Remove(last);
            LogicalChildren.Remove(last);
        }
        while (_days.Count < dates.Count)
        {
            var b = new MonthDayButton(_owner, this, _compact);
            _days.Add(b);
            VisualChildren.Add(b);
            LogicalChildren.Add(b);
        }
        for (var i = 0; i < dates.Count; i++)
        {
            _days[i].Date = dates[i];
            _days[i].InMonth = dates[i].Month == _month.Month && dates[i].Year == _month.Year;
        }
        UpdateTabStops();
    }

    private void UpdateTabStops()
    {
        var focus = _owner.FocusDate;
        var any = _days.Any(d => d.Date == focus);
        var selected = _owner.SelectedDate;
        foreach (var d in _days)
        {
            d.IsTabStop = any ? d.Date == focus : d.Date == selected || (!_days.Any(x => x.Date == selected) && ReferenceEquals(d, _days.FirstOrDefault(x => x.InMonth)));
            d.Refresh();
        }
    }

    private void Pack(double width, double height)
    {
        var events = _owner.ViewEvents().ToList();
        var old = _chips.ToDictionary(c => c.Key, c => c);
        var oldByEvent = new Dictionary<MonthCalendarEvent, MonthChip>();
        foreach (var c in _chips)
        {
            oldByEvent.TryAdd(c.Event, c);
        }
        var keep = new HashSet<MonthChip>();
        var next = new List<MonthChip>();
        foreach (var m in _more)
        {
            VisualChildren.Remove(m);
            LogicalChildren.Remove(m);
        }
        _more.Clear();
        if (!_compact)
        {
            var rowH = RowHeight(height);
            var lanes = MonthCalendarModel.LaneCount(rowH);
            for (var r = 0; r < _rows.Length; r++)
            {
                var layout = MonthCalendarModel.Pack(_rows[r], events, lanes);
                foreach (var item in layout.Items)
                {
                    var key = MonthChip.KeyOf(item, r);
                    if (!old.TryGetValue(key, out var chip) || keep.Contains(chip))
                    {
                        chip = new MonthChip(_owner, this);
                        VisualChildren.Add(chip);
                        LogicalChildren.Add(chip);
                        // A chip whose event was shown somewhere else (a move) fades in; others appear at once (Arc).
                        if (oldByEvent.ContainsKey(item.Event) && ArcMotion.IsFull(this))
                        {
                            chip.FadeIn();
                        }
                    }
                    keep.Add(chip);
                    chip.Set(item, r);
                    next.Add(chip);
                }
                foreach (var more in layout.More)
                {
                    var button = new MonthMoreButton(_owner, this, more, r);
                    _more.Add(button);
                    VisualChildren.Add(button);
                    LogicalChildren.Add(button);
                }
            }
        }
        foreach (var c in _chips)
        {
            if (keep.Contains(c))
            {
                continue;
            }
            // Leaving chips fade where they were (Arc: ~150 ms) and then go.
            if (ArcMotion.IsFull(this) && IsEffectivelyVisible)
            {
                _leaving.Add(c);
                c.IsHitTestVisible = false;
                c.Focusable = false;
                _ = FadeAway(c);
            }
            else
            {
                VisualChildren.Remove(c);
                LogicalChildren.Remove(c);
            }
        }
        _chips.Clear();
        _chips.AddRange(next);
        foreach (var c in _chips)
        {
            c.UpdateDim();
        }
        _packedHeight = height;
        _packedWidth = width;
        _dirty = false;
    }

    private async Task FadeAway(MonthChip chip)
    {
        await chip.FadeOut();
        _leaving.Remove(chip);
        VisualChildren.Remove(chip);
        LogicalChildren.Remove(chip);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        if (_dirty || _days.Count == 0)
        {
            BuildDays();
        }
        var height = _compact ? _weeks * 50 : (double.IsInfinity(availableSize.Height) ? _weeks * 120 : availableSize.Height);
        if (_dirty || Math.Abs(_packedHeight - height) > 0.5 || Math.Abs(_packedWidth - width) > 0.5)
        {
            Pack(width, height);
        }
        foreach (var child in VisualChildren.OfType<Control>())
        {
            child.Measure(new Size(width, height));
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var colW = ColumnWidth(finalSize.Width);
        var rowH = RowHeight(finalSize.Height);
        var i = 0;
        for (var r = 0; r < _rows.Length; r++)
        {
            for (var c = 0; c < _rows[r].Count; c++)
            {
                var d = _days[i++];
                var size = _compact ? 32 : 24;
                var x = _compact ? c * colW + (colW - size) / 2 : c * colW + 6;
                d.Arrange(new Rect(x, r * rowH + (_compact ? 5 : 4), size, size));
            }
        }
        foreach (var chip in _chips.Concat(_leaving))
        {
            chip.Arrange(chip.LayoutRect(colW, rowH));
        }
        foreach (var m in _more)
        {
            m.Arrange(new Rect(m.Column * colW + 4, m.Row * rowH + MonthCalendarModel.LaneTop + m.Lane * MonthCalendarModel.LanePitch,
                colW - 8, 19));
        }
        if (_ghost is not null)
        {
            _ghost.Arrange(new Rect(_ghost.Position, _ghost.DesiredSize));
        }
        return finalSize;
    }

    // Rendering --------------------------------------------------------------------------------------------------------

    private Rect CellRect(int row, int col, double colW, double rowH) => new(col * colW, row * rowH, colW, rowH);

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        var colW = ColumnWidth(Bounds.Width);
        var rowH = RowHeight(Bounds.Height);
        var outside = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.Foreground, 0.016));
        var selectedFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.Foreground, 0.045));
        var hoverFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.Foreground, 0.026));
        // Drop and span highlight: the secondary text at 10% (Arc dark: oklab(0.8 0 0 / .1)).
        var rangeFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.TextSecondary, 0.10));
        var selected = _owner.SelectedDate;
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        for (var r = 0; r < _rows.Length; r++)
        {
            for (var c = 0; c < _rows[r].Count; c++)
            {
                var day = _rows[r][c];
                var rect = CellRect(r, c, colW, rowH);
                var inMonth = day.Month == _month.Month && day.Year == _month.Year;
                IBrush? fill = null;
                if (InRange(day))
                {
                    fill = rangeFill;
                }
                else if (day == selected && !_compact)
                {
                    fill = selectedFill;
                }
                else if (_hoverDay == day && !_compact)
                {
                    fill = hoverFill;
                }
                else if (!inMonth && !_compact)
                {
                    fill = outside;
                }
                if (fill is not null)
                {
                    context.FillRectangle(fill, rect);
                }
                if (!_compact && c > 0)
                {
                    context.FillRectangle(paint.SubtleBrush, new Rect(rect.X, rect.Y, 1, rect.Height));
                }
                if (_compact)
                {
                    DrawLoad(context, day, rect, day == selected, paint);
                }
            }
            if (!_compact && r < _rows.Length - 1)
            {
                context.FillRectangle(paint.SubtleBrush, new Rect(0, (r + 1) * rowH - 1, Bounds.Width, 1));
            }
        }
    }

    private void DrawLoad(DrawingContext context, DateOnly day, Rect cell, bool selected, ScreenPaint paint)
    {
        var count = 0;
        foreach (var e in _owner.ViewEvents())
        {
            if (MonthCalendarModel.Covers(e, day))
            {
                count++;
            }
        }
        count = Math.Min(4, count);
        if (count == 0)
        {
            return;
        }
        var total = count * 5 + (count - 1) * 2;
        var x = cell.X + (cell.Width - total) / 2;
        var y = cell.Y + 5 + 32 + 3;
        var brush = selected ? paint.ForegroundBrush : paint.MutedBrush;
        for (var i = 0; i < count; i++)
        {
            context.DrawRectangle(brush, null, new Rect(x + i * 7, y, 5, 3), 1.5, 1.5);
        }
    }

    private bool InRange(DateOnly day)
    {
        if (_dropRange is { } drop)
        {
            return day >= drop.Start && day <= drop.End;
        }
        if (_rangeStart is { } a && _rangeEnd is { } b)
        {
            var lo = a < b ? a : b;
            var hi = a < b ? b : a;
            return day >= lo && day <= hi;
        }
        if (_owner.Pane == MonthPane.Form && _owner.Draft is { } draft && draft.End > draft.Start)
        {
            return day >= draft.Start && day <= draft.End;
        }
        return false;
    }

    // Hit testing ------------------------------------------------------------------------------------------------------

    public DateOnly? DayAt(Point p)
    {
        if (_rows.Length == 0 || p.X < 0 || p.Y < 0 || p.X >= Bounds.Width || p.Y >= Bounds.Height)
        {
            return null;
        }
        var colW = ColumnWidth(Bounds.Width);
        var rowH = RowHeight(Bounds.Height);
        var r = Math.Clamp((int)(p.Y / rowH), 0, _rows.Length - 1);
        var c = Math.Clamp((int)(p.X / colW), 0, _rows[r].Count - 1);
        return _rows[r][c];
    }

    private static T? Find<T>(object? source) where T : class
    {
        var v = source as Visual;
        while (v is not null)
        {
            if (v is T t)
            {
                return t;
            }
            v = v.GetVisualParent() as Visual;
        }
        return null;
    }

    // Pointer ----------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_pointer is not null && ReferenceEquals(e.Pointer, _pointer))
        {
            if (_pressChip is not null)
            {
                if (!_dragging && Distance(p, _pressPoint) >= DragThreshold && _pressChip.Event.IsEnabled)
                {
                    StartDrag(p);
                }
                if (_dragging)
                {
                    UpdateDrag(p);
                }
            }
            else if (_pressDay is { } start && !_compact)
            {
                var day = DayAt(p);
                if (day is { } d && (d != start || _spanning))
                {
                    _spanning = true;
                    _rangeStart = start;
                    _rangeEnd = d;
                    InvalidateVisual();
                }
            }
            return;
        }
        var hover = e.Pointer.Type == PointerType.Touch ? null : DayAt(p);
        if (hover != _hoverDay)
        {
            _hoverDay = hover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoverDay is not null)
        {
            _hoverDay = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var p = e.GetPosition(this);
        if (Find<MonthMoreButton>(e.Source) is { } more)
        {
            _owner.ShowDayOf(more.Date);
            e.Handled = true;
            return;
        }
        var chip = Find<MonthChip>(e.Source);
        if (chip is null && e.ClickCount == 2 && DayAt(p) is { } dbl && !_compact)
        {
            _owner.OpenNewEvent(dbl);
            e.Handled = true;
            return;
        }
        _pressPoint = p;
        _pressChip = chip;
        _pressDay = chip is null ? DayAt(p) : null;
        _dragging = false;
        _spanning = false;
        _pointer = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointer is null || !ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        var p = e.GetPosition(this);
        _pointer = null;
        e.Pointer.Capture(null);
        if (_pressChip is { } chip)
        {
            if (_dragging)
            {
                EndDrag(p, commit: true);
            }
            else if (Distance(p, _pressPoint) < DragThreshold)
            {
                _owner.OpenEvent(chip.Event);
            }
        }
        else if (_pressDay is { } start)
        {
            if (_spanning && _rangeEnd is { } end && end != start)
            {
                var lo = start < end ? start : end;
                var hi = start < end ? end : start;
                _rangeStart = _rangeEnd = null;
                _owner.OpenNewEvent(lo, hi);
            }
            else
            {
                _rangeStart = _rangeEnd = null;
                _owner.ShowDayOf(start);
            }
            InvalidateVisual();
        }
        _pressChip = null;
        _pressDay = null;
        _spanning = false;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            EndDrag(default, commit: false);
        }
        _pointer = null;
        _pressChip = null;
        _pressDay = null;
        _spanning = false;
        _rangeStart = _rangeEnd = null;
        InvalidateVisual();
    }

    private void StartDrag(Point p)
    {
        var chip = _pressChip!;
        _dragging = true;
        chip.IsDragSource = true;
        var grabDay = DayAt(_pressPoint) ?? chip.Event.StartDate;
        _grabOffset = Math.Max(0, grabDay.DayNumber - chip.Event.StartDate.DayNumber);
        var colW = ColumnWidth(Bounds.Width);
        _ghost = new MonthGhost(_owner, chip.Event, Math.Min(240, Math.Max(colW - 8, chip.Bounds.Width)));
        VisualChildren.Add(_ghost);
        _ghost.ZIndex = 50;
        _ghostOffset = new Vector(_pressPoint.X - chip.Bounds.X, _pressPoint.Y - chip.Bounds.Y);
        Cursor = new Cursor(StandardCursorType.DragMove);
        UpdateDrag(p);
    }

    private void UpdateDrag(Point p)
    {
        if (_ghost is null || _pressChip is null)
        {
            return;
        }
        _ghost.Position = new Point(p.X - _ghostOffset.X, p.Y - _ghostOffset.Y);
        _ghost.Measure(Size.Infinity);
        InvalidateArrange();
        var day = DayAt(p);
        if (day is { } d)
        {
            var start = d.AddDays(-_grabOffset);
            var length = _pressChip.Event.LastDate.DayNumber - _pressChip.Event.StartDate.DayNumber;
            _dropRange = (start, start.AddDays(length));
        }
        else
        {
            _dropRange = null;
        }
        InvalidateVisual();
    }

    private void EndDrag(Point p, bool commit)
    {
        var chip = _pressChip;
        _dragging = false;
        Cursor = null;
        if (_ghost is not null)
        {
            VisualChildren.Remove(_ghost);
            _ghost = null;
        }
        var drop = _dropRange;
        _dropRange = null;
        InvalidateVisual();
        if (chip is null)
        {
            return;
        }
        chip.IsDragSource = false;
        if (commit && drop is { } range && range.Start != chip.Event.StartDate)
        {
            _owner.MoveEvent(chip.Event, range.Start);
        }
    }

    public bool IsDragging => _dragging;

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // Keyboard ---------------------------------------------------------------------------------------------------------

    public void FocusDay(DateOnly day)
    {
        if (_dirty)
        {
            BuildDays();
        }
        _owner.FocusDate = day;
        UpdateTabStops();
        var button = _days.FirstOrDefault(d => d.Date == day);
        button?.Focus(NavigationMethod.Tab);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Source is Visual v && Find<MonthChip>(v) is { } chip)
        {
            ChipKey(chip, e);
            return;
        }
        if (Find<MonthDayButton>(e.Source) is not { } button)
        {
            return;
        }
        var step = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -7,
            Key.Down => 7,
            _ => 0,
        };
        if (step != 0 && e.KeyModifiers == KeyModifiers.None)
        {
            var target = button.Date.AddDays(step);
            if (!_owner.ShowWeekends)
            {
                while (MonthCalendarModel.IsWeekend(target))
                {
                    target = target.AddDays(Math.Sign(step));
                }
            }
            _owner.FocusDate = target;
            if (_days.Any(d => d.Date == target))
            {
                FocusDay(target);
            }
            else
            {
                _owner.Month = target;
                _owner.FocusGridDay();
            }
            e.Handled = true;
            return;
        }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && e.KeyModifiers == KeyModifiers.None)
        {
            _owner.ShowDayOf(button.Date);
            e.Handled = true;
        }
    }

    private void ChipKey(MonthChip chip, KeyEventArgs e)
    {
        var ev = chip.Event;
        if (e.KeyModifiers == KeyModifiers.Alt)
        {
            var step = e.Key switch
            {
                Key.Left => -1,
                Key.Right => 1,
                Key.Up => -7,
                Key.Down => 7,
                _ => 0,
            };
            if (step != 0)
            {
                var id = ev.Id;
                _owner.MoveEvent(ev, ev.StartDate.AddDays(step));
                Dispatcher.UIThread.Post(() => _owner.FocusChip(id), DispatcherPriority.Input);
                e.Handled = true;
            }
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Delete:
            case Key.Back:
                _owner.DeleteEvent(ev);
                _owner.FocusGridDay();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                _owner.OpenEvent(ev);
                e.Handled = true;
                break;
        }
    }

    public bool FocusChip(string id)
    {
        var chip = _chips.FirstOrDefault(c => c.Event.Id == id);
        if (chip is null)
        {
            return false;
        }
        chip.Focus(NavigationMethod.Tab);
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GridPeer(this);

    private sealed class GridPeer(MonthGridView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;

        protected override string? GetNameCore()
        {
            var grid = (MonthGridView)Owner;
            return MonthCalendarModel.Format(grid._month, grid._owner.Labels.MonthTitleFormat, grid._owner.EffectiveCulture);
        }
    }
}

/// <summary>A day number: a 24 circle (32 in the compact month), today filled, keyboard focus ring; named with the full date, today and the event count.</summary>
internal sealed class MonthDayButton : Control
{
    private readonly MonthCalendar _owner;
    private readonly MonthGridView _grid;
    private readonly bool _compact;
    private readonly WeekCalendarText _text = new();

    public MonthDayButton(MonthCalendar owner, MonthGridView grid, bool compact)
    {
        _owner = owner;
        _grid = grid;
        _compact = compact;
        Focusable = true;
        Cursor = compact ? new Cursor(StandardCursorType.Hand) : null;
    }

    public DateOnly Date { get; set; }

    public bool InMonth { get; set; }

    public void Refresh()
    {
        var count = 0;
        foreach (var e in _owner.ViewEvents())
        {
            if (MonthCalendarModel.Covers(e, Date))
            {
                count++;
            }
        }
        var l = _owner.Labels;
        var countText = count == 0 ? l.NoEvents : count == 1 ? l.OneEvent : string.Format(_owner.EffectiveCulture, l.ManyEvents, count);
        var name = string.Format(_owner.EffectiveCulture, l.DayName,
            MonthCalendarModel.Format(Date, "dddd, MMMM d", _owner.EffectiveCulture),
            Date == _owner.EffectiveToday ? l.TodaySuffix : string.Empty, countText);
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.FocusDate = Date;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        var size = Bounds.Width;
        var today = Date == _owner.EffectiveToday;
        var center = new Point(size / 2, size / 2);
        IBrush fg;
        if (today)
        {
            // Arc: today's circle is oklch(0.8) with dark text in dark, oklch(0.33) with white text in light.
            var fill = paint.IsDark ? Oklch.ToColor(0.8, 0, 0) : Oklch.ToColor(0.33, 0, 0);
            context.DrawEllipse(new ImmutableSolidColorBrush(fill), null, center, size / 2, size / 2);
            fg = paint.IsDark ? new ImmutableSolidColorBrush(paint.Background) : paint.SurfaceBrush;
        }
        else
        {
            fg = InMonth ? paint.ForegroundBrush : paint.MutedBrush;
            if (_compact && Date == _owner.SelectedDate)
            {
                context.DrawEllipse(paint.ActiveFill, null, center, size / 2, size / 2);
            }
        }
        var layout = _text.Get(Date.Day.ToString(_owner.EffectiveCulture), new Typeface(ScreenText.Body(this)),
            _compact ? 13.5 : 13, fg, double.PositiveInfinity, 1, double.NaN, -0.13);
        layout?.Draw(context, new Point((size - layout.WidthIncludingTrailingWhitespace) / 2, (size - layout.Height) / 2));
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawEllipse(null, paint.FocusPen, center, size / 2 + 1, size / 2 + 1);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DayPeer(this);

    private sealed class DayPeer(MonthDayButton owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public void Invoke() => ((MonthDayButton)Owner)._owner.ShowDayOf(((MonthDayButton)Owner).Date);
    }
}

/// <summary>
/// An event in the grid. A timed chip: 19 high, radius 5, padding 0 6, gap 5, the time 13/500 in the calendar's tone then the
/// title, no fill at rest (hover foreground 6%, open 9%); an invitation waiting for a reply is outlined with a dashed tone
/// line at 55% and a secondary title; a declined one strikes its muted title. A bar (all-day, multi-day): the tone's fill,
/// 13/500 tone text, square and flush at a clipped end. Filtered out: opacity 0.28 (150 ms); dragged from: 0.35.
/// </summary>
internal sealed class MonthChip : Control
{
    private readonly MonthCalendar _owner;
    private readonly MonthGridView _grid;
    private readonly WeekCalendarText _time = new();
    private readonly WeekCalendarText _title = new();
    private MotionValue<double>? _opacity;
    private bool _hover;
    private bool _dragSource;
    private double _dimTarget = 1;
    private double _fade = 1;

    public MonthChip(MonthCalendar owner, MonthGridView grid)
    {
        _owner = owner;
        _grid = grid;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public MonthCalendarEvent Event { get; private set; } = null!;

    public MonthLaneItem Item { get; private set; }

    public int Row { get; private set; }

    public string Key => KeyOf(Item, Row);

    public static string KeyOf(MonthLaneItem item, int row) =>
        item.Event.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + row + ":" + item.StartColumn;

    public bool IsBar => Item.IsBar;

    public bool IsDragSource
    {
        get => _dragSource;
        set
        {
            _dragSource = value;
            ApplyOpacity();
        }
    }

    public void Set(MonthLaneItem item, int row)
    {
        Item = item;
        Row = row;
        Event = item.Event;
        AutomationProperties.SetName(this, NameOf(item.Event));
        InvalidateVisual();
    }

    private string NameOf(MonthCalendarEvent e)
    {
        var o = _owner;
        var l = o.Labels;
        string when;
        if (e.IsMultiDay)
        {
            when = string.Format(o.EffectiveCulture, l.DateRange, o.MonthDay(e.StartDate), o.MonthDay(e.LastDate)) + ", " +
                   (e.IsAllDay ? l.AllDay.ToLowerInvariant() : o.RowTime(e.StartTime!.Value));
        }
        else if (e.IsAllDay)
        {
            when = o.ShortDate(e.StartDate) + ", " + l.AllDay.ToLowerInvariant();
        }
        else
        {
            when = o.ShortDate(e.StartDate) + ", " + string.Format(o.EffectiveCulture, l.DateRange, o.RowTime(e.StartTime!.Value),
                o.RowTime(e.EffectiveEndTime));
        }
        var suffix = e.Rsvp switch
        {
            MonthCalendarRsvp.Pending => l.AwaitingYourReplySuffix,
            MonthCalendarRsvp.No => l.DeclinedSuffix,
            _ => string.Empty,
        };
        return e.Title + ", " + when + suffix;
    }

    public Rect LayoutRect(double colW, double rowH)
    {
        var item = Item;
        var x = item.StartColumn * colW + (item.ClipStart ? 0 : 4);
        var right = (item.EndColumn + 1) * colW - (item.ClipEnd ? 0 : 4);
        var y = Row * rowH + MonthCalendarModel.LaneTop + item.Lane * MonthCalendarModel.LanePitch;
        return new Rect(x, y, Math.Max(0, right - x), 19);
    }

    public void UpdateDim()
    {
        _dimTarget = _owner.Matches(Event) ? 1 : 0.28;
        ApplyOpacity(animate: true);
    }

    public void FadeIn()
    {
        _fade = 0;
        Opacity = 0;
        var v = Ensure();
        v.Jump(0);
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var motion = Ensure();
            motion.Changed -= OnFade;
            motion.Changed += OnFade;
            await motion.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard));
        });
    }

    private MotionValue<double> Ensure() => _opacity ??= new MotionValue<double>(this, 1, MotionVectors.Double);

    private void OnFade(double v)
    {
        _fade = v;
        ApplyOpacity();
    }

    public async Task FadeOut()
    {
        var motion = Ensure();
        motion.Jump(_fade);
        motion.Changed -= OnFade;
        motion.Changed += OnFade;
        await motion.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard));
    }

    private MotionValue<double>? _dim;
    private double _dimValue = 1;

    private void ApplyOpacity(bool animate = false)
    {
        if (animate && Math.Abs(_dimValue - _dimTarget) > 0.001)
        {
            if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && IsEffectivelyVisible && _grid.IsArrangeValid)
            {
                _dim ??= new MotionValue<double>(this, _dimValue, MotionVectors.Double);
                _dim.Changed -= OnDim;
                _dim.Changed += OnDim;
                _ = _dim.AnimateTo(_dimTarget, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard, TimeSpan.FromMilliseconds(80)));
                return;
            }
            _dimValue = _dimTarget;
        }
        Opacity = _fade * (_dragSource ? 0.35 : _dimValue);
    }

    private void OnDim(double v)
    {
        _dimValue = v;
        Opacity = _fade * (_dragSource ? 0.35 : _dimValue);
    }

    public double DimValue => _dimTarget;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hover = true;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var e = Event;
        var paint = _owner.Paint;
        var tone = _owner.ToneOf(e);
        var rect = new Rect(Bounds.Size);
        var item = Item;
        var left = item.ClipStart ? 0 : 5;
        var right = item.ClipEnd ? 0 : 5;
        var radii = new CornerRadius(left, right, right, left);
        var open = ReferenceEquals(_owner.OpenedEvent, e) && _owner.Pane == MonthPane.Event;
        var face = new Typeface(ScreenText.Body(this));
        var medium = new Typeface(ScreenText.Body(this), FontStyle.Normal, FontWeight.Medium);
        if (item.IsBar)
        {
            var fill = _hover || open ? tone.BarHover : tone.BarFill;
            DrawRounded(context, fill, null, rect, radii);
            var bar = _title.Get(e.Title, medium, 13, tone.BarText, rect.Width - 12, 1, 18.2, -0.13);
            bar?.Draw(context, new Point(6, (19 - bar.Height) / 2));
        }
        else
        {
            var pending = e.Rsvp == MonthCalendarRsvp.Pending;
            var declined = e.Rsvp == MonthCalendarRsvp.No;
            IBrush? fill = open ? paint.OpenFill : _hover ? paint.ChipHoverFill : null;
            if (fill is not null || pending)
            {
                DrawRounded(context, fill ?? Brushes.Transparent, pending ? tone.PendingOutline : null, pending ? rect.Deflate(0.5) : rect,
                    radii);
            }
            else
            {
                context.FillRectangle(Brushes.Transparent, rect);
            }
            var timeText = MonthCalendarModel.ChipTime(e.StartTime!.Value, _owner.EffectiveCulture);
            var time = _time.Get(timeText, medium, 13, tone.Tone, rect.Width - 12, 1, 18.2, -0.13);
            var x = 6.0;
            if (time is not null)
            {
                time.Draw(context, new Point(x, (19 - time.Height) / 2));
                x += time.WidthIncludingTrailingWhitespace + 5;
            }
            var titleBrush = declined ? paint.MutedBrush : pending ? paint.SecondaryBrush : paint.ForegroundBrush;
            var title = _title.Get(e.Title, face, 13, titleBrush, Math.Max(0, rect.Width - x - 6), 1, 18.2, -0.13);
            if (title is not null)
            {
                var y = (19 - title.Height) / 2;
                title.Draw(context, new Point(x, y));
                if (declined)
                {
                    var w = title.WidthIncludingTrailingWhitespace;
                    context.FillRectangle(paint.MutedBrush, new Rect(x, Math.Round(y + title.Height / 2), w, 1));
                }
            }
        }
        if (Classes.Contains(":focus-visible"))
        {
            DrawRounded(context, null, paint.FocusPen, rect.Inflate(1), new CornerRadius(6));
        }
    }

    private static void DrawRounded(DrawingContext context, IBrush? fill, IPen? pen, Rect rect, CornerRadius radii)
    {
        if (radii.TopLeft == radii.TopRight && radii.TopLeft == radii.BottomLeft)
        {
            context.DrawRectangle(fill, pen, rect, radii.TopLeft, radii.TopLeft);
            return;
        }
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var r = rect;
            g.BeginFigure(new Point(r.Left + radii.TopLeft, r.Top), true);
            g.LineTo(new Point(r.Right - radii.TopRight, r.Top));
            if (radii.TopRight > 0)
            {
                g.ArcTo(new Point(r.Right, r.Top + radii.TopRight), new Size(radii.TopRight, radii.TopRight), 0, false, SweepDirection.Clockwise);
            }
            g.LineTo(new Point(r.Right, r.Bottom - radii.BottomRight));
            if (radii.BottomRight > 0)
            {
                g.ArcTo(new Point(r.Right - radii.BottomRight, r.Bottom), new Size(radii.BottomRight, radii.BottomRight), 0, false, SweepDirection.Clockwise);
            }
            g.LineTo(new Point(r.Left + radii.BottomLeft, r.Bottom));
            if (radii.BottomLeft > 0)
            {
                g.ArcTo(new Point(r.Left, r.Bottom - radii.BottomLeft), new Size(radii.BottomLeft, radii.BottomLeft), 0, false, SweepDirection.Clockwise);
            }
            g.LineTo(new Point(r.Left, r.Top + radii.TopLeft));
            if (radii.TopLeft > 0)
            {
                g.ArcTo(new Point(r.Left + radii.TopLeft, r.Top), new Size(radii.TopLeft, radii.TopLeft), 0, false, SweepDirection.Clockwise);
            }
            g.EndFigure(true);
        }
        context.DrawGeometry(fill, pen, geometry);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChipPeer(this);

    private sealed class ChipPeer(MonthChip owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public void Invoke() => ((MonthChip)Owner)._owner.OpenEvent(((MonthChip)Owner).Event);
    }
}

/// <summary>"N more": 19 high, radius 5, padding 0 6, 13px secondary text, hover foreground 6%; opens the day in the panel.</summary>
internal sealed class MonthMoreButton : Control
{
    private readonly MonthCalendar _owner;
    private readonly WeekCalendarText _text = new();
    private bool _hover;

    public MonthMoreButton(MonthCalendar owner, MonthGridView grid, MonthMoreItem item, int row)
    {
        _owner = owner;
        Column = item.Column;
        Lane = item.Lane;
        Count = item.Count;
        Date = item.Date;
        Row = row;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetName(this, string.Format(owner.EffectiveCulture, owner.Labels.MoreName, Count,
            MonthCalendarModel.Format(Date, "dddd, MMMM d", owner.EffectiveCulture)));
    }

    public int Column { get; }
    public int Lane { get; }
    public int Count { get; }
    public int Row { get; }
    public DateOnly Date { get; }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = e.Pointer.Type != PointerType.Touch;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if ((e.Key == Key.Enter || e.Key == Key.Space) && e.KeyModifiers == KeyModifiers.None)
        {
            _owner.ShowDayOf(Date);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        var rect = new Rect(Bounds.Size);
        var selected = _owner.SelectedDate == Date;
        context.DrawRectangle(_hover || selected && _owner.Pane == MonthPane.Day && false ? paint.ChipHoverFill : Brushes.Transparent, null, rect, 5, 5);
        var layout = _text.Get(string.Format(_owner.EffectiveCulture, _owner.Labels.More, Count), new Typeface(ScreenText.Body(this)), 13,
            paint.SecondaryBrush, rect.Width - 12, 1, 18.2, -0.13);
        layout?.Draw(context, new Point(6, (19 - layout.Height) / 2));
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, paint.FocusPen, rect.Inflate(1), 6, 6);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MorePeer(this);

    private sealed class MorePeer(MonthMoreButton owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public void Invoke() => ((MonthMoreButton)Owner)._owner.ShowDayOf(((MonthMoreButton)Owner).Date);
    }
}

/// <summary>The drag ghost: a 22 high card (raised fill, 1px Border, radius 6, padding 0 7, 12px text, shadow 0 8 22 at 22%).</summary>
internal sealed class MonthGhost : Control
{
    private readonly MonthCalendar _owner;
    private readonly MonthCalendarEvent _event;
    private readonly double _width;
    private readonly WeekCalendarText _time = new();
    private readonly WeekCalendarText _title = new();

    public MonthGhost(MonthCalendar owner, MonthCalendarEvent e, double width)
    {
        _owner = owner;
        _event = e;
        _width = width;
        IsHitTestVisible = false;
    }

    public Point Position { get; set; }

    protected override Size MeasureOverride(Size availableSize) => new(_width, 22);

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        var rect = new Rect(0, 0, _width, 22);
        var shadow = new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 22, Color = Color.FromArgb(56, 0, 0, 0) });
        context.DrawRectangle(paint.IsDark ? paint.RaisedBrush : paint.SurfaceBrush, paint.BorderPen, rect.Deflate(0.5), 6, 6, shadow);
        var tone = _owner.ToneOf(_event);
        var x = 7.0;
        var face = new Typeface(ScreenText.Body(this));
        if (_event.StartTime is { } start && !_event.IsMultiDay)
        {
            var t = _time.Get(MonthCalendarModel.ChipTime(start, _owner.EffectiveCulture), new Typeface(face.FontFamily, FontStyle.Normal, FontWeight.Medium),
                12, tone.Tone, _width, 1, 16.8, -0.12);
            if (t is not null)
            {
                t.Draw(context, new Point(x, (22 - t.Height) / 2));
                x += t.WidthIncludingTrailingWhitespace + 5;
            }
        }
        var title = _title.Get(_event.Title, face, 12, paint.ForegroundBrush, Math.Max(0, _width - x - 7), 1, 16.8, -0.12);
        title?.Draw(context, new Point(x, (22 - title.Height) / 2));
    }
}
