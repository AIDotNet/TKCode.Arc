using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The agenda of narrow calendars (notes §3.7): the week as a list grouped by day in a scroller, each day's heading held at
/// the top while its events scroll under it. Weeks change by sliding or fading pooled panes, like the grid.
/// </summary>
internal sealed class WeekCalendarAgendaView : Control
{
    private readonly WeekCalendar _owner;
    private readonly Panel _viewport = new() { ClipToBounds = true };
    private readonly List<WeekCalendarAgendaPane> _panes = new();
    private readonly Stack<WeekCalendarAgendaPane> _pool = new();

    public WeekCalendarAgendaView(WeekCalendar owner)
    {
        _owner = owner;
        LogicalChildren.Add(_viewport);
        VisualChildren.Add(_viewport);
    }

    /// <summary>The pane of the week shown.</summary>
    public WeekCalendarAgendaPane? Current => _panes.Count > 0 ? _panes[^1] : null;

    /// <summary>Every pane on screen, oldest first.</summary>
    public IReadOnlyList<WeekCalendarAgendaPane> Panes => _panes;

    public void Show(WeekCalendarDays days, WeekCalendarTransition transition, int direction)
    {
        var current = Current;
        var width = _viewport.Bounds.Width;
        if (current is null || transition == WeekCalendarTransition.None || !(width > 0) || !IsVisible)
        {
            foreach (var leaving in _panes.Where(p => !ReferenceEquals(p, current)).ToList())
            {
                Recycle(leaving);
            }
            current ??= Rent();
            current.IsLeaving = false;
            current.IsEnabled = true;
            current.X.Jump(0);
            current.Fade.Jump(1);
            current.Bind(days);
            return;
        }
        if (current.Days == days)
        {
            current.Bind(days);
            return;
        }
        current.IsLeaving = true;
        current.IsEnabled = false;
        current.IsHitTestVisible = false;
        while (_panes.Count >= WeekCalendarGridView.MaxSlots)
        {
            Recycle(_panes[0]);
        }
        var incoming = Rent();
        incoming.IsLeaving = false;
        incoming.IsEnabled = true;
        incoming.IsHitTestVisible = true;
        incoming.Bind(days);
        incoming.ScrollToTop();
        if (transition == WeekCalendarTransition.Slide)
        {
            var travel = Math.Sign(direction) * width;
            incoming.Fade.Jump(1);
            incoming.X.Jump(travel);
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
    }

    private async void Leave(WeekCalendarAgendaPane pane, Task<bool> motion)
    {
        var generation = pane.Generation;
        if (await motion && pane.Generation == generation && pane.IsLeaving && _panes.Contains(pane))
        {
            Recycle(pane);
        }
    }

    private WeekCalendarAgendaPane Rent()
    {
        var pane = _pool.Count > 0 ? _pool.Pop() : CreatePane();
        pane.IsVisible = true;
        _panes.Add(pane);
        for (var i = 0; i < _panes.Count; i++)
        {
            _panes[i].ZIndex = i;
        }
        return pane;
    }

    private WeekCalendarAgendaPane CreatePane()
    {
        var pane = new WeekCalendarAgendaPane(_owner, this);
        _viewport.Children.Add(pane);
        return pane;
    }

    private void Recycle(WeekCalendarAgendaPane pane)
    {
        _panes.Remove(pane);
        pane.Generation++;
        pane.IsLeaving = false;
        pane.X.Jump(0);
        pane.Fade.Jump(1);
        pane.IsVisible = false;
        pane.IsEnabled = true;
        pane.IsHitTestVisible = true;
        _pool.Push(pane);
    }

    public void Refresh()
    {
        foreach (var pane in _panes)
        {
            pane.Bind(pane.Days);
        }
        if (Current is null && _owner.Range.Count > 0)
        {
            Show(_owner.Range, WeekCalendarTransition.None, 0);
        }
    }

    public void OnNowChanged()
    {
        var today = _owner.Today;
        foreach (var pane in _panes)
        {
            if (pane.Today != today)
            {
                pane.Bind(pane.Days);
            }
        }
    }

    public void OnThemeChanged()
    {
        foreach (var pane in _panes.Concat(_pool))
        {
            pane.OnThemeChanged();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _viewport.Measure(availableSize);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 390;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : _owner.EffectiveGridHeight;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewport.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>One week of the agenda: a scroller with the day sections; its offset and fade travel as one.</summary>
internal sealed class WeekCalendarAgendaPane : Control
{
    private readonly WeekCalendar _owner;
    private readonly ScrollArea _scroll;
    private readonly WeekCalendarAgendaList _list;
    private readonly TranslateTransform _shift = new();

    public WeekCalendarAgendaPane(WeekCalendar owner, Visual motionOwner)
    {
        _owner = owner;
        _list = new WeekCalendarAgendaList(owner);
        _scroll = new ScrollArea
        {
            Content = _list,
            FadeLength = 0,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        _scroll.ScrollChanged += (_, _) => _list.PinHeaders(_scroll.Offset.Y);
        LogicalChildren.Add(_scroll);
        VisualChildren.Add(_scroll);
        RenderTransform = _shift;
        X = new MotionValue<double>(motionOwner, 0, MotionVectors.Double);
        Fade = new MotionValue<double>(motionOwner, 1, MotionVectors.Double);
        X.Changed += x => _shift.X = x;
        Fade.Changed += o => Opacity = o;
    }

    public MotionValue<double> X { get; }

    public MotionValue<double> Fade { get; }

    public bool IsLeaving { get; set; }

    public int Generation { get; set; }

    public WeekCalendarDays Days { get; private set; }

    /// <summary>The day this pane last drew as today.</summary>
    public DateOnly Today { get; private set; }

    /// <summary>The scroller.</summary>
    public ScrollArea Scroller => _scroll;

    /// <summary>The list of sections.</summary>
    public WeekCalendarAgendaList List => _list;

    public void Bind(WeekCalendarDays days)
    {
        Days = days;
        Today = _owner.Today;
        _list.Bind(days);
        _list.PinHeaders(_scroll.Offset.Y);
    }

    public void ScrollToTop() => _scroll.Offset = default;

    public void OnThemeChanged() => _list.OnThemeChanged();

    protected override Size MeasureOverride(Size availableSize)
    {
        _scroll.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _scroll.Arrange(new Rect(finalSize));
        _list.PinHeaders(_scroll.Offset.Y);
        return finalSize;
    }
}

/// <summary>
/// The agenda's sections laid out by hand (padding 4 above, 24 below; sections 12 in from the sides): per day a heading
/// (50), then its rows (about 52, 2 apart, 8 after the last) or "Nothing planned". All-day events come first (longer
/// later on the same first day) and repeat on every day they cover; then the timed events by start.
/// </summary>
internal sealed class WeekCalendarAgendaList : Control
{
    public const double HeaderHeight = 50;
    public const double RowHeight = 52;
    public const double RowGap = 2;
    public const double EmptyHeight = 28.8;
    public const double SidePadding = 12;

    private readonly WeekCalendar _owner;
    private readonly List<WeekCalendarAgendaHeader> _headers = new();
    private readonly List<WeekCalendarAgendaRow> _rows = new();
    private readonly List<WeekCalendarAgendaEmpty> _empties = new();
    private readonly List<(double Top, double Bottom)> _sections = new();
    private readonly List<(Control Control, Rect Rect)> _layout = new();
    private double _height;

    public WeekCalendarAgendaList(WeekCalendar owner)
    {
        _owner = owner;
    }

    /// <summary>The day headings, in order.</summary>
    public IReadOnlyList<WeekCalendarAgendaHeader> Headers => _headers.Where(h => h.IsVisible).ToList();

    /// <summary>The rows shown, in order.</summary>
    public IReadOnlyList<WeekCalendarAgendaRow> Rows => _rows.Where(r => r.IsVisible).ToList();

    /// <summary>The "Nothing planned" lines shown.</summary>
    public IReadOnlyList<WeekCalendarAgendaEmpty> Empties => _empties.Where(r => r.IsVisible).ToList();

    private T Take<T>(List<T> pool, int index, Func<T> create) where T : Control
    {
        while (pool.Count <= index)
        {
            var control = create();
            pool.Add(control);
            LogicalChildren.Add(control);
            VisualChildren.Add(control);
        }
        pool[index].IsVisible = true;
        return pool[index];
    }

    public void Bind(WeekCalendarDays days)
    {
        var index = _owner.Index;
        var today = _owner.Today;
        var culture = _owner.EffectiveCulture;
        var labels = _owner.Labels;
        _layout.Clear();
        _sections.Clear();
        int headers = 0, rows = 0, empties = 0;
        var y = 4.0;
        for (var i = 0; i < days.Count; i++)
        {
            var day = days.DayAt(i);
            var top = y;
            var header = Take(_headers, headers++, () => new WeekCalendarAgendaHeader(_owner) { ZIndex = 2 });
            header.Bind(day, day == today);
            _layout.Add((header, new Rect(SidePadding, y, 0, HeaderHeight)));
            y += HeaderHeight;
            var items = new List<(WeekCalendarEvent Event, bool AllDay, double Start, double End)>();
            foreach (var entry in index.AllDayOn(day))
            {
                items.Add((entry.Event, true, 0, 0));
            }
            foreach (var segment in index.Timed(day))
            {
                items.Add((segment.Event, false, segment.StartMinute, segment.EndMinute));
            }
            if (items.Count == 0)
            {
                var empty = Take(_empties, empties++, () => new WeekCalendarAgendaEmpty(_owner));
                empty.Bind(labels.NothingPlanned);
                _layout.Add((empty, new Rect(SidePadding, y, 0, EmptyHeight)));
                y += EmptyHeight;
            }
            else
            {
                for (var k = 0; k < items.Count; k++)
                {
                    var (e, allDay, start, end) = items[k];
                    var row = Take(_rows, rows++, () => new WeekCalendarAgendaRow(_owner) { ZIndex = 1 });
                    row.Bind(e, day, allDay, start, end);
                    _layout.Add((row, new Rect(SidePadding, y, 0, RowHeight)));
                    y += RowHeight + (k < items.Count - 1 ? RowGap : 0);
                }
                y += 8;
            }
            _sections.Add((top, y));
            AutomationProperties.SetName(header, WeekCalendarTime.DayName(day, culture));
        }
        y += 24;
        _height = y;
        for (var k = headers; k < _headers.Count; k++)
        {
            _headers[k].IsVisible = false;
        }
        for (var k = rows; k < _rows.Count; k++)
        {
            _rows[k].IsVisible = false;
        }
        for (var k = empties; k < _empties.Count; k++)
        {
            _empties[k].IsVisible = false;
        }
        InvalidateMeasure();
    }

    public void OnThemeChanged()
    {
        foreach (var c in _headers)
        {
            c.OnThemeChanged();
        }
        foreach (var c in _rows)
        {
            c.OnThemeChanged();
        }
        foreach (var c in _empties)
        {
            c.InvalidateVisual();
        }
    }

    /// <summary>
    /// Holds each day's heading at the top of the scroller while its section is under it; the next section's heading pushes
    /// it out (CSS sticky, by hand).
    /// </summary>
    public void PinHeaders(double offset)
    {
        var shown = 0;
        for (var i = 0; i < _headers.Count && shown < _sections.Count; i++)
        {
            var header = _headers[i];
            if (!header.IsVisible)
            {
                continue;
            }
            var (top, bottom) = _sections[shown++];
            var shift = Math.Clamp(offset - top, 0, Math.Max(0, bottom - top - HeaderHeight));
            header.Pin(shift);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 390;
        foreach (var (control, rect) in _layout)
        {
            control.Measure(new Size(Math.Max(0, width - 2 * SidePadding), rect.Height));
        }
        return new Size(width, _height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Max(0, finalSize.Width - 2 * SidePadding);
        foreach (var (control, rect) in _layout)
        {
            control.Arrange(new Rect(rect.X, rect.Y, width, rect.Height));
        }
        return finalSize;
    }
}

/// <summary>A day's heading in the agenda: the date in a circle (accent for today), the weekday and the short date, "Today".</summary>
internal sealed class WeekCalendarAgendaHeader : Control
{
    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _date = new();
    private readonly WeekCalendarText _weekday = new();
    private readonly WeekCalendarText _short = new();
    private readonly WeekCalendarText _tag = new();
    private readonly TranslateTransform _pin = new();

    public WeekCalendarAgendaHeader(WeekCalendar owner)
    {
        _owner = owner;
        RenderTransform = _pin;
        IsHitTestVisible = false;
    }

    public DateOnly Day { get; private set; }

    public bool IsToday { get; private set; }

    /// <summary>How far the heading is held down to stay at the top of the scroller.</summary>
    public double PinOffset => _pin.Y;

    public void Bind(DateOnly day, bool isToday)
    {
        Day = day;
        IsToday = isToday;
        InvalidateVisual();
    }

    public void Pin(double offset)
    {
        if (Math.Abs(_pin.Y - offset) > 0.01)
        {
            _pin.Y = offset;
        }
    }

    public void OnThemeChanged()
    {
        _date.Reset();
        _weekday.Reset();
        _short.Reset();
        _tag.Reset();
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.Header);

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var culture = _owner.EffectiveCulture;
        var size = Bounds.Size;
        // Opaque, so rows scroll under a held heading (the section's width, as Arc's sticky h3).
        context.FillRectangle(paint.Surface, new Rect(size));
        const double circle = 30;
        var cy = 12 + circle / 2;
        var circleRect = new Rect(4, 12, circle, circle);
        if (IsToday)
        {
            context.DrawEllipse(paint.Accent, null, circleRect.Center, circle / 2, circle / 2);
        }
        var date = _date.Get(Day.Day.ToString(culture), paint.DisplayMedium, 16, IsToday ? paint.AccentForeground : paint.Foreground,
            double.PositiveInfinity, 1, 17.6);
        if (date is not null)
        {
            date.Draw(context, new Point(circleRect.Center.X - WeekCalendarText.NaturalWidth(date) / 2, cy - 8.8));
        }
        var x = circleRect.Right + 10;
        var weekday = _weekday.Get(WeekCalendarTime.Weekday(Day.DayOfWeek, culture), paint.DisplayMedium, 14, paint.Foreground,
            double.PositiveInfinity, 1, 15.4);
        weekday?.Draw(context, new Point(x, cy - 7.7));
        x += WeekCalendarText.NaturalWidth(weekday) + 6;
        var shortDate = _short.Get(WeekCalendarTime.ShortDate(Day, culture), paint.DisplayMedium, 14, paint.TextSecondary,
            double.PositiveInfinity, 1, 15.4);
        shortDate?.Draw(context, new Point(x, cy - 7.7));
        if (IsToday)
        {
            var tag = _tag.Get(_owner.Labels.TodayTag, paint.Body, 12, paint.TextSecondary, double.PositiveInfinity, 1, 13.2);
            tag?.Draw(context, new Point(size.Width - 4 - WeekCalendarText.NaturalWidth(tag), cy - 6.6));
        }
    }
}

/// <summary>"Nothing planned" under an empty day of the agenda, aligned with the titles.</summary>
internal sealed class WeekCalendarAgendaEmpty : Control
{
    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _text = new();
    private string _label = string.Empty;

    public WeekCalendarAgendaEmpty(WeekCalendar owner)
    {
        _owner = owner;
        IsHitTestVisible = false;
    }

    public string Label => _label;

    public void Bind(string label)
    {
        _label = label;
        AutomationProperties.SetName(this, label);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new WeekCalendarGroupPeer(this, AutomationControlType.Text);

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var layout = _text.Get(_label, paint.Body, 12, paint.TextMuted, Math.Max(0, Bounds.Width - 48), 1, 16.8);
        layout?.Draw(context, new Point(44, 2));
    }
}

/// <summary>
/// One event in the agenda (notes §3.7): its times on the left, the calendar's color bar, the title over the place, and up
/// to three attendees with a count. A button; past events are not dimmed here (Arc).
/// </summary>
internal sealed class WeekCalendarAgendaRow : AvaloniaButton
{
    /// <summary>0 at rest, 1 under the pointer or keyboard focus.</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarAgendaRow, double>(nameof(HoverAmount));

    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _start = new();
    private readonly WeekCalendarText _end = new();
    private readonly WeekCalendarText _title = new();
    private readonly WeekCalendarText _place = new();
    private readonly WeekCalendarText _more = new();

    static WeekCalendarAgendaRow()
    {
        AffectsRender<WeekCalendarAgendaRow>(HoverAmountProperty, IsFocusedProperty);
    }

    public WeekCalendarAgendaRow(WeekCalendar owner)
    {
        _owner = owner;
        ClipToBounds = false;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarAgendaRow);

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    public WeekCalendarEvent? Event { get; private set; }

    public DateOnly Day { get; private set; }

    public bool IsAllDay { get; private set; }

    public double StartMinute { get; private set; }

    public double EndMinute { get; private set; }

    public string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    public void Bind(WeekCalendarEvent calendarEvent, DateOnly day, bool allDay, double start, double end)
    {
        Event = calendarEvent;
        Day = day;
        IsAllDay = allDay;
        StartMinute = start;
        EndMinute = end;
        var labels = _owner.Labels;
        var culture = _owner.EffectiveCulture;
        var title = string.IsNullOrWhiteSpace(calendarEvent.Title) ? labels.Untitled : calendarEvent.Title;
        var calendar = _owner.CalendarOf(calendarEvent)?.Name;
        var name = !string.IsNullOrWhiteSpace(calendarEvent.AutomationName)
            ? calendarEvent.AutomationName!
            : allDay
                ? labels.AgendaAllDayEvent(title, calendar, calendarEvent.Location)
                : labels.AgendaEvent(title, WeekCalendarTime.FullTime(start, culture), WeekCalendarTime.FullTime(end, culture), calendar,
                    calendarEvent.Location);
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    public void OnThemeChanged()
    {
        _start.Reset();
        _end.Reset();
        _title.Reset();
        _place.Reset();
        _more.Reset();
        InvalidateVisual();
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Event is { } e)
        {
            _owner.InvokeEvent(e, this);
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, WeekCalendarAgendaList.RowHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        if (Event is not { } e)
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var culture = _owner.EffectiveCulture;
        var size = Bounds.Size;
        var bounds = new Rect(size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (HoverAmount > 0.001)
        {
            using (context.PushOpacity(HoverAmount))
            {
                context.DrawRectangle(paint.SurfaceMuted, null, new RoundedRect(bounds, Math.Min(WeekCalendarMetrics.AgendaRowRadius, paint.Radius14)));
            }
        }
        var cy = size.Height / 2;
        // Times, right-aligned in a 64px column after the 4px padding.
        var timeRight = 4 + WeekCalendarMetrics.AgendaTimeWidth;
        if (IsAllDay)
        {
            var allDay = _start.Get(_owner.Labels.AllDayTime, paint.Body, 12, paint.Foreground, WeekCalendarMetrics.AgendaTimeWidth, 1, 16.2);
            allDay?.Draw(context, new Point(timeRight - WeekCalendarText.NaturalWidth(allDay), cy - 8.1));
        }
        else
        {
            var start = _start.Get(WeekCalendarTime.ShortTime(StartMinute, culture), paint.Body, 12, paint.Foreground,
                WeekCalendarMetrics.AgendaTimeWidth, 1, 16.2);
            var end = _end.Get(WeekCalendarTime.ShortTime(EndMinute, culture), paint.Body, 12, paint.TextSecondary,
                WeekCalendarMetrics.AgendaTimeWidth, 1, 16.2);
            start?.Draw(context, new Point(timeRight - WeekCalendarText.NaturalWidth(start), cy - 16.2));
            end?.Draw(context, new Point(timeRight - WeekCalendarText.NaturalWidth(end), cy));
        }
        var barX = timeRight + WeekCalendarMetrics.AgendaGap;
        context.DrawRectangle(paint.For(e.CalendarId).Bar, null, new RoundedRect(new Rect(barX, cy - 18, 4, 36), 2));
        var textX = barX + 4 + WeekCalendarMetrics.AgendaGap;
        IReadOnlyList<ProjectPerson> people = (IReadOnlyList<ProjectPerson>?)e.Attendees ?? Array.Empty<ProjectPerson>();
        var shown = Math.Min(people.Count, WeekCalendarMetrics.AgendaAvatarMax);
        var extra = people.Count - shown;
        const double d = WeekCalendarMetrics.AgendaAvatarSize;
        var avatarsWidth = shown == 0 ? 0 : shown * d - (shown - 1) * 5 + (extra > 0 ? d - 5 : 0);
        var textRight = size.Width - 10 - (avatarsWidth > 0 ? avatarsWidth + WeekCalendarMetrics.AgendaGap : 0);
        var room = Math.Max(0, textRight - textX);
        var title = string.IsNullOrWhiteSpace(e.Title) ? _owner.Labels.Untitled : e.Title;
        var titleLayout = _title.Get(title, paint.BodyMedium, 14, paint.Foreground, room, 1, 19.6);
        if (!string.IsNullOrWhiteSpace(e.Location))
        {
            var place = _place.Get(e.Location, paint.Body, 12, paint.TextSecondary, room, 1, 16.8);
            var block = 19.6 + 16.8;
            titleLayout?.Draw(context, new Point(textX, cy - block / 2));
            place?.Draw(context, new Point(textX, cy - block / 2 + 19.6));
        }
        else
        {
            titleLayout?.Draw(context, new Point(textX, cy - 9.8));
        }
        if (shown > 0)
        {
            var x = size.Width - 10 - avatarsWidth + d / 2;
            for (var i = 0; i < shown; i++)
            {
                var center = new Point(x + i * (d - 5), cy);
                context.DrawEllipse(paint.SurfaceRaised, null, center, d / 2 + 2, d / 2 + 2);
                paint.Avatars.Draw(context, _owner, people[i], center, d / 2, 0);
            }
            if (extra > 0)
            {
                var center = new Point(x + shown * (d - 5), cy);
                context.DrawEllipse(paint.SurfaceRaised, null, center, d / 2 + 2, d / 2 + 2);
                context.DrawEllipse(paint.SurfaceMuted, null, center, d / 2, d / 2);
                var more = _more.Get("+" + extra.ToString(culture), paint.BodyMedium, 9, paint.TextSecondary, double.PositiveInfinity, 1, 9);
                more?.Draw(context, new Point(center.X - WeekCalendarText.NaturalWidth(more) / 2, center.Y - 4.5));
            }
        }
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, paint.FocusRing, new RoundedRect(bounds.Inflate(3), paint.Radius14 > 0 ? paint.Radius14 + 3 : 0));
        }
    }
}
