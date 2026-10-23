using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaCheckBox = Avalonia.Controls.CheckBox;

namespace TKCode.Arc;

/// <summary>
/// The month in the sidebar (notes §3.3): its title with previous / next month, one-letter weekdays and the days as
/// 28px circles (today: accent; outside the month: muted), with the days shown in the grid marked by a band. It shows the
/// month of the first day shown; the month buttons only page this month (no motion, as Arc). Picking a day moves the
/// calendar to the range around it and keeps the focus on the day. One tab stop; the arrow keys move between days
/// (Arc makes every day a tab stop: 42 of them).
/// </summary>
internal sealed class WeekCalendarMiniMonth : Control
{
    public const double HeaderHeight = 28;
    public const double RowGap = 6;
    public const double WeekdayRowHeight = 24;
    public const double DayRowHeight = 30;
    public const double DayRowGap = 2;

    private readonly TextBlock _title;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly WeekCalendarMiniWeekdays _weekdays;
    private readonly List<WeekCalendarMiniDay> _days = new();
    private WeekCalendar? _owner;
    private DateOnly _month;
    private WeekCalendarDays? _lastRange;

    public WeekCalendarMiniMonth()
    {
        _title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 14,
            LineHeight = 19.6,
            FontWeight = FontWeight.Medium,
        };
        _previous = MonthButton(ArcIcons.ChevronLeft);
        _next = MonthButton(ArcIcons.ChevronRight);
        _previous.Click += (_, _) => ShowMonth(_month.AddMonths(-1));
        _next.Click += (_, _) => ShowMonth(_month.AddMonths(1));
        _weekdays = new WeekCalendarMiniWeekdays();
        foreach (var child in new Control[] { _title, _previous, _next, _weekdays })
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
        for (var i = 0; i < 42; i++)
        {
            var day = new WeekCalendarMiniDay(this);
            _days.Add(day);
            LogicalChildren.Add(day);
            VisualChildren.Add(day);
        }
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
    }

    private static Button MonthButton(Geometry glyph) => new()
    {
        Variant = ButtonVariant.Ghost,
        Content = new ArcGlyph { Data = glyph, Size = 16, StrokeThickness = 1.75 },
        Width = 28,
        Height = 28,
        MinHeight = 0,
        Padding = default,
        CornerRadius = new CornerRadius(10),
    };

    internal WeekCalendar? Owner => _owner;

    /// <summary>The first day of the month shown.</summary>
    public DateOnly DisplayMonth => _month;

    /// <summary>The previous-month button.</summary>
    public Button PreviousButton => _previous;

    /// <summary>The next-month button.</summary>
    public Button NextButton => _next;

    /// <summary>The month title text.</summary>
    public string Title => _title.Text ?? string.Empty;

    /// <summary>The rows of days the month needs (5 or 6).</summary>
    public int Rows { get; private set; } = 6;

    /// <summary>The day cells in use.</summary>
    public IReadOnlyList<WeekCalendarMiniDay> Days => _days.Take(Rows * 7).ToList();

    public void Attach(WeekCalendar owner)
    {
        _owner = owner;
        _lastRange = null;
        Refresh();
    }

    /// <summary>Follows the range: the month of its first day (unless only the cursor moved), the band, today.</summary>
    public void Refresh()
    {
        if (_owner is null)
        {
            return;
        }
        var range = _owner.Range;
        if (_lastRange != range)
        {
            _lastRange = range;
            _month = new DateOnly(range.Start.Year, range.Start.Month, 1);
        }
        Rebuild();
    }

    /// <summary>Pages to another month (the buttons, the arrow keys past the edge).</summary>
    public void ShowMonth(DateOnly month)
    {
        _month = new DateOnly(month.Year, month.Month, 1);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_owner is null)
        {
            return;
        }
        var culture = _owner.EffectiveCulture;
        var labels = _owner.Labels;
        var first = _owner.FirstDayOfWeek;
        _title.Text = WeekCalendarTime.MonthTitle(_month.Year, _month.Month, culture);
        AutomationProperties.SetName(_previous, labels.PreviousMonth);
        AutomationProperties.SetName(_next, labels.NextMonth);
        Tooltip.SetTip(_previous, labels.PreviousMonth);
        Tooltip.SetTip(_next, labels.NextMonth);
        _weekdays.Set(Enumerable.Range(0, 7).Select(i => WeekCalendarTime.WeekdayLetter((DayOfWeek)(((int)first + i) % 7), culture)).ToArray(),
            _owner);
        var gridStart = WeekCalendarRange.WeekStart(_month, first);
        var daysInMonth = DateTime.DaysInMonth(_month.Year, _month.Month);
        var lead = _month.DayNumber - gridStart.DayNumber;
        Rows = (int)Math.Ceiling((lead + daysInMonth) / 7.0);
        var today = _owner.Today;
        var range = _owner.Range;
        var cursor = _owner.Date;
        var tabStop = cursor.Year == _month.Year && cursor.Month == _month.Month ? cursor : _month;
        for (var i = 0; i < _days.Count; i++)
        {
            var cell = _days[i];
            cell.IsVisible = i < Rows * 7;
            if (!cell.IsVisible)
            {
                continue;
            }
            var day = gridStart.AddDays(i);
            var inRange = range.Contains(day);
            cell.Bind(day, day.Month != _month.Month || day.Year != _month.Year, day == today, inRange,
                inRange && day == range.Start, inRange && day == range.End, WeekCalendarTime.DayName(day, culture),
                day == today ? labels.TodayTag : null);
            KeyboardNavigation.SetIsTabStop(cell, day == tabStop);
        }
        InvalidateMeasure();
    }

    public void OnThemeChanged()
    {
        foreach (var day in _days)
        {
            day.OnThemeChanged();
        }
        _weekdays.InvalidateVisual();
    }

    /// <summary>The cell of <paramref name="day"/> in the month shown, or null.</summary>
    public WeekCalendarMiniDay? CellFor(DateOnly day) => _days.Take(Rows * 7).FirstOrDefault(d => d.Day == day);

    internal void OnDayPicked(WeekCalendarMiniDay cell, NavigationMethod method)
    {
        if (_owner is null)
        {
            return;
        }
        var day = cell.Day;
        _owner.PickDay(day);
        // The month may have changed with the range: keep the focus on the picked day (Arc keeps it on the button).
        CellFor(day)?.Focus(method);
    }

    internal void OnDayKey(WeekCalendarMiniDay cell, KeyEventArgs e)
    {
        var delta = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -7,
            Key.Down => 7,
            _ => 0,
        };
        DateOnly target;
        if (delta != 0)
        {
            target = cell.Day.AddDays(delta);
        }
        else if (e.Key == Key.PageUp || e.Key == Key.PageDown)
        {
            target = cell.Day.AddMonths(e.Key == Key.PageUp ? -1 : 1);
        }
        else if (e.Key == Key.Home || e.Key == Key.End)
        {
            var weekStart = WeekCalendarRange.WeekStart(cell.Day, _owner?.FirstDayOfWeek ?? DayOfWeek.Monday);
            target = e.Key == Key.Home ? weekStart : weekStart.AddDays(6);
        }
        else
        {
            return;
        }
        e.Handled = true;
        if (target.Month != _month.Month || target.Year != _month.Year)
        {
            ShowMonth(target);
        }
        foreach (var day in _days)
        {
            KeyboardNavigation.SetIsTabStop(day, day.IsVisible && day.Day == target);
        }
        CellFor(target)?.Focus(NavigationMethod.Directional);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 219;
        var cell = width / 7;
        _title.Measure(new Size(Math.Max(0, width - 64), HeaderHeight));
        _previous.Measure(new Size(28, 28));
        _next.Measure(new Size(28, 28));
        _weekdays.Measure(new Size(width, WeekdayRowHeight));
        foreach (var day in _days)
        {
            day.Measure(new Size(cell, DayRowHeight));
        }
        return new Size(width, HeaderHeight + RowGap + WeekdayRowHeight + Rows * DayRowHeight + (Rows - 1) * DayRowGap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        _title.Arrange(new Rect(0, 0, Math.Max(0, width - 56), HeaderHeight));
        _previous.Arrange(new Rect(width - 56, 0, 28, 28));
        _next.Arrange(new Rect(width - 28, 0, 28, 28));
        var y = HeaderHeight + RowGap;
        _weekdays.Arrange(new Rect(0, y, width, WeekdayRowHeight));
        y += WeekdayRowHeight;
        for (var i = 0; i < _days.Count; i++)
        {
            if (i >= Rows * 7)
            {
                continue;
            }
            var row = i / 7;
            var column = i % 7;
            var left = WeekCalendarColumns.Edge(column, 7, width);
            var right = WeekCalendarColumns.Edge(column + 1, 7, width);
            _days[i].Arrange(new Rect(left, y + row * (DayRowHeight + DayRowGap), right - left, DayRowHeight));
        }
        return finalSize;
    }
}

/// <summary>The month's one-letter weekday row (hidden from screen readers; each day's name says its weekday).</summary>
internal sealed class WeekCalendarMiniWeekdays : Control
{
    private readonly WeekCalendarText[] _texts = Enumerable.Range(0, 7).Select(_ => new WeekCalendarText()).ToArray();
    private string[] _letters = Array.Empty<string>();
    private WeekCalendar? _owner;

    public WeekCalendarMiniWeekdays()
    {
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public void Set(string[] letters, WeekCalendar owner)
    {
        _letters = letters;
        _owner = owner;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is null)
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var width = Bounds.Width;
        for (var i = 0; i < _letters.Length && i < 7; i++)
        {
            var layout = _texts[i].Get(_letters[i], paint.Body, 12, paint.TextMuted, double.PositiveInfinity, 1, 16.8);
            if (layout is null)
            {
                continue;
            }
            var left = WeekCalendarColumns.Edge(i, 7, width);
            var right = WeekCalendarColumns.Edge(i + 1, 7, width);
            layout.Draw(context, new Point(left + (right - left - WeekCalendarText.NaturalWidth(layout)) / 2, (Bounds.Height - 16.8) / 2));
        }
    }
}

/// <summary>A day of the sidebar month: a 28px circle, the range band behind it, today in accent, a keyboard ring.</summary>
internal sealed class WeekCalendarMiniDay : AvaloniaButton
{
    /// <summary>0 at rest, 1 under the pointer (the theme fades the circle in 160ms).</summary>
    internal static readonly StyledProperty<double> HoverAmountProperty =
        AvaloniaProperty.Register<WeekCalendarMiniDay, double>(nameof(HoverAmount));

    private readonly WeekCalendarMiniMonth _month;
    private readonly WeekCalendarText _text = new();

    static WeekCalendarMiniDay()
    {
        AffectsRender<WeekCalendarMiniDay>(HoverAmountProperty, IsFocusedProperty);
    }

    public WeekCalendarMiniDay(WeekCalendarMiniMonth month)
    {
        _month = month;
        ClipToBounds = false;
    }

    protected override Type StyleKeyOverride => typeof(WeekCalendarMiniDay);

    internal double HoverAmount { get => GetValue(HoverAmountProperty); set => SetValue(HoverAmountProperty, value); }

    public DateOnly Day { get; private set; }

    public bool IsOutside { get; private set; }

    public bool IsToday { get; private set; }

    public bool IsInRange { get; private set; }

    public bool IsRangeStart { get; private set; }

    public bool IsRangeEnd { get; private set; }

    public string AccessibleName => AutomationProperties.GetName(this) ?? string.Empty;

    public void Bind(DateOnly day, bool outside, bool today, bool inRange, bool rangeStart, bool rangeEnd, string name, string? status)
    {
        Day = day;
        IsOutside = outside;
        IsToday = today;
        IsInRange = inRange;
        IsRangeStart = rangeStart;
        IsRangeEnd = rangeEnd;
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetItemStatus(this, status);
        InvalidateVisual();
    }

    public void OnThemeChanged()
    {
        _text.Reset();
        InvalidateVisual();
    }

    private NavigationMethod _lastMethod = NavigationMethod.Pointer;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _lastMethod = NavigationMethod.Directional;
        _month.OnDayKey(this, e);
        if (!e.Handled)
        {
            base.OnKeyDown(e);
        }
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        _lastMethod = NavigationMethod.Pointer;
        base.OnPointerPressed(e);
    }

    protected override void OnClick()
    {
        base.OnClick();
        _month.OnDayPicked(this, _lastMethod);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 31, WeekCalendarMiniMonth.DayRowHeight);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        if (_month.Owner is not { } owner)
        {
            return;
        }
        var paint = owner.Paint;
        paint.EnsureReady();
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (IsInRange)
        {
            // Inset 1 top and bottom; 2 in and a 14 radius at the range's ends.
            var left = IsRangeStart ? 2 : 0;
            var right = IsRangeEnd ? size.Width - 2 : size.Width;
            var band = new Rect(left, 1, Math.Max(0, right - left), size.Height - 2);
            var r = new RoundedRect(band, new CornerRadius(IsRangeStart ? 14 : 0, IsRangeEnd ? 14 : 0, IsRangeEnd ? 14 : 0, IsRangeStart ? 14 : 0));
            context.DrawRectangle(paint.SurfaceMuted, null, r);
        }
        var center = new Point(size.Width / 2, size.Height / 2);
        if (IsToday)
        {
            context.DrawEllipse(paint.Accent, null, center, 14, 14);
        }
        else if (HoverAmount > 0.001)
        {
            using (context.PushOpacity(HoverAmount))
            {
                context.DrawEllipse(paint.HoverCircle, null, center, 14, 14);
            }
        }
        var brush = IsToday ? paint.AccentForeground : IsOutside ? paint.TextMuted : paint.Foreground;
        var layout = _text.Get(Day.Day.ToString(owner.EffectiveCulture), IsToday ? paint.BodyMedium : paint.Body, 12, brush,
            double.PositiveInfinity, 1, 16.8);
        layout?.Draw(context, new Point(center.X - WeekCalendarText.NaturalWidth(layout) / 2, center.Y - 8.4));
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawEllipse(null, paint.FocusRing, center, 17, 17);
        }
    }
}

/// <summary>The calendars list: a colored toggle per calendar (role checkbox), in the sidebar or the Calendars popover.</summary>
internal sealed class WeekCalendarSourceList : StackPanel
{
    private readonly List<WeekCalendarSourceToggle> _toggles = new();
    private WeekCalendar? _owner;

    public WeekCalendarSourceList()
    {
        Spacing = 2;
        Orientation = Orientation.Vertical;
    }

    /// <summary>The toggles shown.</summary>
    public IReadOnlyList<WeekCalendarSourceToggle> Toggles => _toggles.Where(t => t.IsVisible).ToList();

    public void Attach(WeekCalendar owner)
    {
        _owner = owner;
        Refresh();
    }

    public void Refresh()
    {
        if (_owner is null)
        {
            return;
        }
        var calendars = _owner.EffectiveCalendars;
        while (_toggles.Count < calendars.Count)
        {
            var toggle = new WeekCalendarSourceToggle();
            toggle.IsCheckedChanged += OnToggled;
            _toggles.Add(toggle);
            Children.Add(toggle);
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        for (var i = 0; i < _toggles.Count; i++)
        {
            var toggle = _toggles[i];
            toggle.IsVisible = i < calendars.Count;
            if (i >= calendars.Count)
            {
                toggle.Source = null;
                continue;
            }
            var source = calendars[i];
            toggle.Source = null;
            toggle.Label = source.Name;
            toggle.SwatchBrush = paint.BarOf(source);
            toggle.IsChecked = source.IsVisible;
            toggle.Source = source;
            AutomationProperties.SetName(toggle, source.Name);
        }
    }

    private void OnToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is WeekCalendarSourceToggle { Source: { } source } toggle && toggle.IsChecked is { } isChecked && source.IsVisible != isChecked)
        {
            source.IsVisible = isChecked;
        }
    }
}

/// <summary>One calendar's toggle row: a 16px box in the calendar's color (filled with a check when shown) and its name.</summary>
internal sealed class WeekCalendarSourceToggle : AvaloniaCheckBox
{
    /// <summary>The calendar's name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WeekCalendarSourceToggle, string?>(nameof(Label));

    /// <summary>The calendar's color (the box's border and fill).</summary>
    public static readonly StyledProperty<IBrush?> SwatchBrushProperty =
        AvaloniaProperty.Register<WeekCalendarSourceToggle, IBrush?>(nameof(SwatchBrush));

    protected override Type StyleKeyOverride => typeof(WeekCalendarSourceToggle);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public IBrush? SwatchBrush { get => GetValue(SwatchBrushProperty); set => SetValue(SwatchBrushProperty, value); }

    /// <summary>The calendar toggled.</summary>
    public WeekCalendarSource? Source { get; set; }
}

/// <summary>The Calendars button's swatches: a 14px dot per calendar (overlapping by 3); hidden calendars at 25%.</summary>
internal sealed class WeekCalendarSwatches : Control
{
    private WeekCalendar? _owner;

    public WeekCalendarSwatches()
    {
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        VerticalAlignment = VerticalAlignment.Center;
    }

    public void Attach(WeekCalendar owner)
    {
        _owner = owner;
        Refresh();
    }

    public void Refresh()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var n = _owner?.EffectiveCalendars.Count ?? 0;
        return n == 0 ? default : new Size(n * 14 - (n - 1) * 3, 14);
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is null)
        {
            return;
        }
        var paint = _owner.Paint;
        paint.EnsureReady();
        var calendars = _owner.EffectiveCalendars;
        for (var i = 0; i < calendars.Count; i++)
        {
            var center = new Point(7 + i * 11, Bounds.Height / 2);
            using (context.PushOpacity(calendars[i].IsVisible ? 1 : 0.25))
            {
                context.DrawEllipse(paint.Surface, null, center, 7, 7);
                context.DrawEllipse(paint.BarOf(calendars[i]), null, center, 5, 5);
            }
        }
    }
}
