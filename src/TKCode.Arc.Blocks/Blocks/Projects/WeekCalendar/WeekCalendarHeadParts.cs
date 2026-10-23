using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The header row of one range (Arc's <c>headPane</c>): a weekday and a date pill per day, sliding with its grid.</summary>
internal sealed class WeekCalendarHeadPane : Control
{
    private readonly WeekCalendar _owner;
    private readonly List<WeekCalendarHeadDay> _days = new();
    private bool _showCursor;

    public WeekCalendarHeadPane(WeekCalendar owner)
    {
        _owner = owner;
    }

    /// <summary>The days shown.</summary>
    public WeekCalendarDays Days { get; private set; }

    /// <summary>The day buttons shown, left to right.</summary>
    public IReadOnlyList<WeekCalendarHeadDay> DayButtons => _days.Take(Days.Count).ToList();

    public void Bind(WeekCalendarDays days, DateOnly cursor)
    {
        Days = days;
        var culture = _owner.EffectiveCulture;
        var today = _owner.Today;
        while (_days.Count < days.Count)
        {
            var button = new WeekCalendarHeadDay(_owner);
            _days.Add(button);
            LogicalChildren.Add(button);
            VisualChildren.Add(button);
        }
        for (var i = 0; i < _days.Count; i++)
        {
            var button = _days[i];
            button.IsVisible = i < days.Count;
            if (i >= days.Count)
            {
                continue;
            }
            var day = days.DayAt(i);
            button.Bind(day, day == today, day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                _owner.Labels.ShowDay(WeekCalendarTime.DayName(day, culture)));
        }
        SetCursor(cursor);
        InvalidateArrange();
    }

    /// <summary>Marks the cursor day (its pill shows only while the grid has keyboard focus).</summary>
    public void SetCursor(DateOnly cursor)
    {
        for (var i = 0; i < _days.Count; i++)
        {
            _days[i].IsCursor = i < Days.Count && Days.DayAt(i) == cursor && _showCursor;
        }
    }

    /// <summary>Shows or hides the cursor pill (the grid gained or lost focus).</summary>
    public void ShowCursor(bool show, DateOnly cursor)
    {
        _showCursor = show;
        SetCursor(cursor);
    }

    public void SetTight(bool tight)
    {
        foreach (var day in _days)
        {
            day.IsTight = tight;
        }
    }

    public void OnThemeChanged()
    {
        foreach (var day in _days)
        {
            day.OnThemeChanged();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, WeekCalendarMetrics.HeaderRowHeight);
        for (var i = 0; i < _days.Count; i++)
        {
            _days[i].Measure(i < Days.Count ? WeekCalendarColumns.Column(i, Days.Count, size).Size : default);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < _days.Count; i++)
        {
            _days[i].Arrange(i < Days.Count ? WeekCalendarColumns.Column(i, Days.Count, finalSize) : default);
        }
        return finalSize;
    }
}

/// <summary>
/// One day of the header (notes §3.4): the short weekday over the date in a pill (today: accent; the cursor day: muted while
/// the grid has focus; hover: muted). Clicking it moves the cursor there; it is not a tab stop (Arc: tabindex −1).
/// </summary>
internal sealed class WeekCalendarHeadDay : AvaloniaButton
{
    /// <summary>0 at rest, 1 under the pointer (the theme fades the pill in 160ms).</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarHeadDay, double>(nameof(HoverAmount));

    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _weekday = new();
    private readonly WeekCalendarText _date = new();
    private bool _isCursor;
    private bool _isTight;

    static WeekCalendarHeadDay()
    {
        AffectsRender<WeekCalendarHeadDay>(HoverAmountProperty, IsFocusedProperty);
        FocusableProperty.OverrideDefaultValue<WeekCalendarHeadDay>(false);
    }

    public WeekCalendarHeadDay(WeekCalendar owner)
    {
        _owner = owner;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarHeadDay);

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    public DateOnly Day { get; private set; }

    public bool IsToday { get; private set; }

    public bool IsWeekend { get; private set; }

    /// <summary>True while the cursor pill shows.</summary>
    public bool IsCursor
    {
        get => _isCursor;
        set
        {
            if (_isCursor != value)
            {
                _isCursor = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>True for the tighter header of calendars below 900 (16px dates in 28px pills).</summary>
    public bool IsTight
    {
        get => _isTight;
        set
        {
            if (_isTight != value)
            {
                _isTight = value;
                InvalidateVisual();
            }
        }
    }

    public void Bind(DateOnly day, bool isToday, bool isWeekend, string name)
    {
        Day = day;
        IsToday = isToday;
        IsWeekend = isWeekend;
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    public void OnThemeChanged()
    {
        _weekday.Reset();
        _date.Reset();
        InvalidateVisual();
    }

    protected override void OnClick()
    {
        base.OnClick();
        _owner.SetCursor(Day, focusCell: true);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, WeekCalendarMetrics.HeaderRowHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var culture = _owner.EffectiveCulture;
        var weekdayBrush = IsToday ? paint.Foreground : IsWeekend ? paint.TextMuted : paint.TextSecondary;
        var weekday = _weekday.Get(WeekCalendarTime.ShortWeekday(Day.DayOfWeek, culture), paint.Body, 12, weekdayBrush,
            size.Width, 1, 14.4);
        var dateSize = IsTight ? 16.0 : 18.0;
        var pillHeight = IsTight ? 28.0 : 32.0;
        var dateBrush = IsToday ? paint.AccentForeground : IsWeekend ? paint.TextSecondary : paint.Foreground;
        var date = _date.Get(Day.Day.ToString(culture), paint.BodyMedium, dateSize, dateBrush, double.PositiveInfinity, 1, dateSize);
        // Weekday (14.4), a 1px gap and the pill, centered in the 60px row.
        var total = 14.4 + 1 + pillHeight;
        var top = (size.Height - total) / 2;
        weekday?.Draw(context, new Point((size.Width - WeekCalendarText.NaturalWidth(weekday)) / 2, top));
        var dateWidth = WeekCalendarText.NaturalWidth(date);
        var pillWidth = Math.Max(pillHeight, dateWidth + 12);
        var pill = new Rect((size.Width - pillWidth) / 2, top + 15.4, pillWidth, pillHeight);
        if (IsToday)
        {
            context.DrawRectangle(paint.Accent, null, new RoundedRect(pill, pillHeight / 2));
        }
        else
        {
            var amount = Math.Max(IsCursor ? 1 : 0, HoverAmount);
            if (amount > 0.001)
            {
                using (context.PushOpacity(amount))
                {
                    context.DrawRectangle(paint.SurfaceMuted, null, new RoundedRect(pill, pillHeight / 2));
                }
            }
        }
        date?.Draw(context, new Point(pill.X + (pill.Width - dateWidth) / 2, pill.Y + (pillHeight - dateSize) / 2));
    }
}

/// <summary>
/// The all-day row of one range (Arc's <c>allDayPane</c>): a column per day (left rule, weekend shade) and the all-day events
/// as bars across their days, packed into lanes. Past the lane limit the last lane shows "+N more" per day until the row is
/// expanded.
/// </summary>
internal sealed class WeekCalendarAllDayPane : Control
{
    private readonly WeekCalendar _owner;
    private readonly Dictionary<WeekCalendarEvent, WeekCalendarAllDayBar> _byEvent = new();
    private readonly Stack<WeekCalendarAllDayBar> _freeBars = new();
    private readonly List<WeekCalendarAllDayBar> _bars = new();
    private readonly List<WeekCalendarMoreChip> _chips = new();
    private List<WeekCalendarAllDayEntry> _entries = new();
    private int[] _hidden = Array.Empty<int>();

    public WeekCalendarAllDayPane(WeekCalendar owner)
    {
        _owner = owner;
        ClipToBounds = false;
    }

    public WeekCalendarDays Days { get; private set; }

    /// <summary>The lanes of the events shown.</summary>
    public WeekCalendarLanes Lanes { get; private set; } = new(Array.Empty<WeekCalendarLanePlacement>(), 0, 0);

    /// <summary>The bars shown.</summary>
    public IReadOnlyList<WeekCalendarAllDayBar> Bars => _bars;

    /// <summary>The "+N more" chips shown.</summary>
    public IReadOnlyList<WeekCalendarMoreChip> Chips => _chips.Where(c => c.IsVisible).ToList();

    private void Add(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    public void Bind(WeekCalendarDays days)
    {
        Days = days;
        var culture = _owner.EffectiveCulture;
        var labels = _owner.Labels;
        _entries = _owner.Index.AllDay(days);
        Lanes = WeekCalendarAllDayLayout.Layout(_entries.Select(e => (e.Start, e.EndExclusive)).ToList(), days.Start, days.Count);
        var limit = _owner.AllDayLaneLimit;
        var expanded = _owner.IsAllDayExpanded;
        _hidden = Lanes.HiddenPerColumn(limit, expanded);
        var keep = new HashSet<WeekCalendarEvent>();
        _bars.Clear();
        var tab = 0;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (!Lanes.IsBarVisible(i, limit, expanded))
            {
                continue;
            }
            var entry = _entries[i];
            keep.Add(entry.Event);
            if (!_byEvent.TryGetValue(entry.Event, out var bar))
            {
                bar = _freeBars.Count > 0 ? _freeBars.Pop() : NewBar();
                bar.IsVisible = true;
                _byEvent[entry.Event] = bar;
            }
            var title = string.IsNullOrWhiteSpace(entry.Event.Title) ? labels.Untitled : entry.Event.Title;
            var first = WeekCalendarTime.DayName(entry.Start, culture);
            var last = entry.LastDay == entry.Start ? null : WeekCalendarTime.DayName(entry.LastDay, culture);
            var name = !string.IsNullOrWhiteSpace(entry.Event.AutomationName)
                ? entry.Event.AutomationName!
                : labels.AllDayEvent(title, first, last, _owner.CalendarOf(entry.Event)?.Name);
            bar.Bind(entry, Lanes.Placements[i], name);
            KeyboardNavigation.SetTabIndex(bar, tab++);
            _bars.Add(bar);
        }
        foreach (var (e, bar) in _byEvent.ToList())
        {
            if (!keep.Contains(e))
            {
                _byEvent.Remove(e);
                bar.IsVisible = false;
                _freeBars.Push(bar);
            }
        }
        while (_chips.Count < days.Count)
        {
            var chip = new WeekCalendarMoreChip(_owner);
            _chips.Add(chip);
            Add(chip);
        }
        for (var c = 0; c < _chips.Count; c++)
        {
            var count = c < days.Count && c < _hidden.Length ? _hidden[c] : 0;
            _chips[c].IsVisible = count > 0;
            if (count > 0)
            {
                _chips[c].Bind(labels.MoreEvents(count));
                KeyboardNavigation.SetTabIndex(_chips[c], tab++);
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private WeekCalendarAllDayBar NewBar()
    {
        var bar = new WeekCalendarAllDayBar(_owner);
        Add(bar);
        return bar;
    }

    public void OnThemeChanged()
    {
        foreach (var bar in _bars)
        {
            bar.OnThemeChanged();
        }
        foreach (var chip in _chips)
        {
            chip.OnThemeChanged();
        }
        InvalidateVisual();
    }

    public void SetActive(bool active)
    {
        IsHitTestVisible = active;
        IsEnabled = active;
    }

    private Rect BarRect(WeekCalendarLanePlacement placement, Size size)
    {
        var left = WeekCalendarColumns.Edge(placement.FirstColumn, Days.Count, size.Width);
        var right = WeekCalendarColumns.Edge(placement.LastColumn + 1, Days.Count, size.Width);
        return new Rect(left, WeekCalendarAllDayLayout.BarTop(placement.Lane), right - left, WeekCalendarAllDayLayout.BarHeight);
    }

    private Rect ChipRect(int column, Size size)
    {
        var col = WeekCalendarColumns.Column(column, Days.Count, size);
        var lane = Math.Max(0, _owner.AllDayLaneLimit - 1);
        return new Rect(col.X, WeekCalendarAllDayLayout.BarTop(lane), col.Width, WeekCalendarAllDayLayout.BarHeight);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 0;
        var size = new Size(width, double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
        foreach (var bar in _bars)
        {
            bar.Measure(BarRect(bar.Placement, size).Size);
        }
        for (var c = 0; c < _chips.Count; c++)
        {
            _chips[c].Measure(c < Days.Count ? ChipRect(c, size).Size : default);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var bar in _bars)
        {
            bar.Arrange(BarRect(bar.Placement, finalSize));
        }
        for (var c = 0; c < _chips.Count; c++)
        {
            _chips[c].Arrange(c < Days.Count ? ChipRect(c, finalSize) : default);
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var size = Bounds.Size;
        for (var i = 0; i < Days.Count; i++)
        {
            var column = WeekCalendarColumns.Column(i, Days.Count, size);
            var day = Days.DayAt(i);
            if (_owner.ShowWeekendShading && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                context.FillRectangle(paint.WeekendShade, column);
            }
            context.FillRectangle(paint.BorderSubtle, new Rect(column.X, 0, 1, size.Height));
        }
    }
}

/// <summary>An all-day event: a bar across its days with the calendar's fill and left stripe and its title (notes §3.4).</summary>
internal sealed class WeekCalendarAllDayBar : AvaloniaButton
{
    /// <summary>0 at rest, 1 under the pointer or keyboard focus (the theme animates it in 160ms).</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarAllDayBar, double>(nameof(HoverAmount));

    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _title = new();

    static WeekCalendarAllDayBar()
    {
        AffectsRender<WeekCalendarAllDayBar>(HoverAmountProperty, IsFocusedProperty);
    }

    public WeekCalendarAllDayBar(WeekCalendar owner)
    {
        _owner = owner;
        ClipToBounds = false;
        ZIndex = 1;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarAllDayBar);

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    public WeekCalendarAllDayEntry? Entry { get; private set; }

    public WeekCalendarEvent? Event => Entry?.Event;

    public WeekCalendarLanePlacement Placement { get; private set; }

    public string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    public void Bind(WeekCalendarAllDayEntry entry, WeekCalendarLanePlacement placement, string name)
    {
        Entry = entry;
        Placement = placement;
        AutomationProperties.SetName(this, name);
        InvalidateVisual();
    }

    public void OnThemeChanged()
    {
        _title.Reset();
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
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, WeekCalendarAllDayLayout.BarHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        if (Event is not { } e)
        {
            return;
        }
        var paint = _owner.Paint;
        var brushes = paint.For(e.CalendarId);
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        // Arc's ::before: inset 0 2 0 3, radius 6, the fill and a 3px calendar stripe.
        var body = new Rect(3, 0, Math.Max(0, size.Width - 5), size.Height);
        var fill = HoverAmount > 0.5 ? brushes.HoverFill : brushes.Fill;
        context.DrawRectangle(fill, null, new RoundedRect(body, 6));
        using (context.PushClip(new RoundedRect(body, 6)))
        {
            context.FillRectangle(brushes.Bar, new Rect(body.X, 0, 3, body.Height));
            var title = string.IsNullOrWhiteSpace(e.Title) ? _owner.Labels.Untitled : e.Title;
            var layout = _title.Get(title, paint.BodyMedium, 12, brushes.Text, Math.Max(0, body.Width - 12), 1, 12);
            layout?.Draw(context, new Point(body.X + 10, (size.Height - 12) / 2));
        }
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, paint.FocusRing, new RoundedRect(body.Inflate(3), 9));
        }
    }
}

/// <summary>"+N more" in the last lane of a collapsed all-day row; pressing it expands the row.</summary>
internal sealed class WeekCalendarMoreChip : AvaloniaButton
{
    /// <summary>0 at rest, 1 under the pointer.</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarMoreChip, double>(nameof(HoverAmount));

    private readonly WeekCalendar _owner;
    private readonly WeekCalendarText _text = new();
    private string _label = string.Empty;

    static WeekCalendarMoreChip()
    {
        AffectsRender<WeekCalendarMoreChip>(HoverAmountProperty, IsFocusedProperty);
    }

    public WeekCalendarMoreChip(WeekCalendar owner)
    {
        _owner = owner;
        ClipToBounds = false;
        ZIndex = 1;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarMoreChip);

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    public string Label => _label;

    public void Bind(string label)
    {
        _label = label;
        AutomationProperties.SetName(this, label);
        InvalidateVisual();
    }

    public void OnThemeChanged()
    {
        _text.Reset();
        InvalidateVisual();
    }

    protected override void OnClick()
    {
        base.OnClick();
        _owner.SetCurrentValue(WeekCalendar.IsAllDayExpandedProperty, true);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, WeekCalendarAllDayLayout.BarHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        paint.EnsureReady();
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var body = new Rect(3, 0, Math.Max(0, size.Width - 5), size.Height);
        context.DrawRectangle(HoverAmount > 0.5 ? paint.Border : paint.SurfaceMuted, null, new RoundedRect(body, 6));
        var layout = _text.Get(_label, paint.BodyMedium, 12, paint.TextSecondary, Math.Max(0, body.Width - 12), 1, 12);
        layout?.Draw(context, new Point(body.X + 8, (size.Height - 12) / 2));
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, paint.FocusRing, new RoundedRect(body.Inflate(3), 9));
        }
    }
}
