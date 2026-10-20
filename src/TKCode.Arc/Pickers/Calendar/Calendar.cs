using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Raised when the user picks a day.</summary>
public sealed class CalendarDateSelectedEventArgs : RoutedEventArgs
{
    public CalendarDateSelectedEventArgs(RoutedEvent routedEvent, DateOnly date) : base(routedEvent) => Date = date;

    public DateOnly Date { get; }
}

/// <summary>
/// A single-date month calendar. Months slide on one spring-driven strip (arrows, PageUp/PageDown, Today and
/// a bound <see cref="DisplayMonth"/> all travel the same way), the title slides with them, and the selected
/// disc glides between days while each number flips color as the disc passes it. Every month shows six
/// weeks, so the height never changes. Full keyboard model: arrows, Home/End, PageUp/PageDown (Shift = year),
/// Enter/Space; disabled days are skipped in the direction of travel.
/// </summary>
[TemplatePart(StripPart, typeof(CalendarMonthStrip))]
[TemplatePart(TitlePart, typeof(ArcContentSwap))]
[TemplatePart(PreviousPart, typeof(AvaloniaButton))]
[TemplatePart(NextPart, typeof(AvaloniaButton))]
[TemplatePart(TodayPart, typeof(AvaloniaButton))]
[TemplatePart(WeekdaysPart, typeof(CalendarWeekdayRow))]
public class Calendar : TemplatedControl
{
    public const string StripPart = "PART_Strip";
    public const string TitlePart = "PART_Title";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string TodayPart = "PART_Today";
    public const string WeekdaysPart = "PART_Weekdays";

    /// <summary>Arc renders the calendar at min(100%, 328px).</summary>
    public const double DefaultWidth = 328;

    #region Properties

    public static readonly StyledProperty<DateOnly?> SelectedDateProperty =
        AvaloniaProperty.Register<Calendar, DateOnly?>(nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Any date in the displayed month (normalized to its first day). Defaults to the selection's month, else today's.</summary>
    public static readonly StyledProperty<DateOnly?> DisplayMonthProperty =
        AvaloniaProperty.Register<Calendar, DateOnly?>(nameof(DisplayMonth), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateOnly?> MinDateProperty =
        AvaloniaProperty.Register<Calendar, DateOnly?>(nameof(MinDate));

    public static readonly StyledProperty<DateOnly?> MaxDateProperty =
        AvaloniaProperty.Register<Calendar, DateOnly?>(nameof(MaxDate));

    /// <summary>Extra disabled days (weekends, booked dates...).</summary>
    public static readonly StyledProperty<Func<DateOnly, bool>?> DisabledDatesProperty =
        AvaloniaProperty.Register<Calendar, Func<DateOnly, bool>?>(nameof(DisabledDates));

    /// <summary>Adds a Today button that slides back to the current month and selects today when it is available.</summary>
    public static readonly StyledProperty<bool> ShowTodayProperty =
        AvaloniaProperty.Register<Calendar, bool>(nameof(ShowToday));

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<Calendar, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Sunday);

    /// <summary>Culture for month and weekday names (null = current UI culture).</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<Calendar, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<string> TodayLabelProperty =
        AvaloniaProperty.Register<Calendar, string>(nameof(TodayLabel), "Today");

    public static readonly StyledProperty<string> PreviousMonthLabelProperty =
        AvaloniaProperty.Register<Calendar, string>(nameof(PreviousMonthLabel), "Previous month");

    public static readonly StyledProperty<string> NextMonthLabelProperty =
        AvaloniaProperty.Register<Calendar, string>(nameof(NextMonthLabel), "Next month");

    public static readonly RoutedEvent<CalendarDateSelectedEventArgs> DateSelectedEvent =
        RoutedEvent.Register<Calendar, CalendarDateSelectedEventArgs>(nameof(DateSelected), RoutingStrategies.Bubble);

    // Day colors and font, set by the theme from Arc tokens.
    internal static readonly StyledProperty<IBrush?> DayForegroundProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(DayForeground));
    internal static readonly StyledProperty<IBrush?> DayMutedForegroundProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(DayMutedForeground));
    internal static readonly StyledProperty<IBrush?> DaySelectedForegroundProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(DaySelectedForeground));
    internal static readonly StyledProperty<IBrush?> DayHoverFillProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(DayHoverFill));
    internal static readonly StyledProperty<IBrush?> SelectionFillProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(SelectionFill));
    internal static readonly StyledProperty<IBrush?> SelectionHoverFillProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(SelectionHoverFill));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<Calendar, IBrush?>(nameof(FocusRingBrush));
    internal static readonly StyledProperty<double> DayFontSizeProperty =
        AvaloniaProperty.Register<Calendar, double>(nameof(DayFontSize), 14);

    internal static readonly DirectProperty<Calendar, string> MonthTitleProperty =
        AvaloniaProperty.RegisterDirect<Calendar, string>(nameof(MonthTitle), o => o.MonthTitle);

    internal static readonly DirectProperty<Calendar, int> TitleDirectionProperty =
        AvaloniaProperty.RegisterDirect<Calendar, int>(nameof(TitleDirection), o => o.TitleDirection);

    public DateOnly? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public DateOnly? DisplayMonth { get => GetValue(DisplayMonthProperty); set => SetValue(DisplayMonthProperty, value); }
    public DateOnly? MinDate { get => GetValue(MinDateProperty); set => SetValue(MinDateProperty, value); }
    public DateOnly? MaxDate { get => GetValue(MaxDateProperty); set => SetValue(MaxDateProperty, value); }
    public Func<DateOnly, bool>? DisabledDates { get => GetValue(DisabledDatesProperty); set => SetValue(DisabledDatesProperty, value); }
    public bool ShowToday { get => GetValue(ShowTodayProperty); set => SetValue(ShowTodayProperty, value); }
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    public string TodayLabel { get => GetValue(TodayLabelProperty); set => SetValue(TodayLabelProperty, value); }
    public string PreviousMonthLabel { get => GetValue(PreviousMonthLabelProperty); set => SetValue(PreviousMonthLabelProperty, value); }
    public string NextMonthLabel { get => GetValue(NextMonthLabelProperty); set => SetValue(NextMonthLabelProperty, value); }

    /// <summary>Raised when the user picks a day (click, Enter / Space, or Today).</summary>
    public event EventHandler<CalendarDateSelectedEventArgs>? DateSelected
    {
        add => AddHandler(DateSelectedEvent, value);
        remove => RemoveHandler(DateSelectedEvent, value);
    }

    internal IBrush? DayForeground { get => GetValue(DayForegroundProperty); set => SetValue(DayForegroundProperty, value); }
    internal IBrush? DayMutedForeground { get => GetValue(DayMutedForegroundProperty); set => SetValue(DayMutedForegroundProperty, value); }
    internal IBrush? DaySelectedForeground { get => GetValue(DaySelectedForegroundProperty); set => SetValue(DaySelectedForegroundProperty, value); }
    internal IBrush? DayHoverFill { get => GetValue(DayHoverFillProperty); set => SetValue(DayHoverFillProperty, value); }
    internal IBrush? SelectionFill { get => GetValue(SelectionFillProperty); set => SetValue(SelectionFillProperty, value); }
    internal IBrush? SelectionHoverFill { get => GetValue(SelectionHoverFillProperty); set => SetValue(SelectionHoverFillProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    internal double DayFontSize { get => GetValue(DayFontSizeProperty); set => SetValue(DayFontSizeProperty, value); }

    private string _monthTitle = string.Empty;
    internal string MonthTitle { get => _monthTitle; private set => SetAndRaise(MonthTitleProperty, ref _monthTitle, value); }

    private int _titleDirection = 1;
    internal int TitleDirection { get => _titleDirection; private set => SetAndRaise(TitleDirectionProperty, ref _titleDirection, value); }

    #endregion

    private CalendarMonthStrip? _strip;
    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private AvaloniaButton? _todayButton;
    private CalendarWeekdayRow? _weekdays;
    private DateOnly _month;
    private DateOnly? _focusedDate;
    private bool _syncingMonth;

    internal CalendarPalette Palette { get; } = new();

    internal DateOnly Today { get; private set; } = ArcToday.Today;

    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentUICulture;

    public Calendar()
    {
        _month = CalendarMath.MonthStart(Today);
    }

    internal bool IsDateDisabled(DateOnly date) =>
        (MinDate is { } min && date < min) || (MaxDate is { } max && date > max) || DisabledDates?.Invoke(date) == true;

    /// <summary>Roving tab stop: the day last focused, else the selection, else today, else the first open day.</summary>
    internal DateOnly? TabbableDate(DateOnly month)
    {
        DateOnly? Open(DateOnly? date) => date is { } d && CalendarMath.SameMonth(d, month) && !IsDateDisabled(d) ? d : null;
        var result = Open(_focusedDate) ?? Open(SelectedDate) ?? Open(Today);
        if (result is not null)
        {
            return result;
        }
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        for (var day = 1; day <= days; day++)
        {
            var date = new DateOnly(month.Year, month.Month, day);
            if (!IsDateDisabled(date))
            {
                return date;
            }
        }
        return null;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_todayButton is not null)
        {
            _todayButton.Click -= OnTodayClick;
        }
        _strip = e.NameScope.Find<CalendarMonthStrip>(StripPart);
        _previous = e.NameScope.Find<AvaloniaButton>(PreviousPart);
        _next = e.NameScope.Find<AvaloniaButton>(NextPart);
        _todayButton = e.NameScope.Find<AvaloniaButton>(TodayPart);
        _weekdays = e.NameScope.Find<CalendarWeekdayRow>(WeekdaysPart);
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_todayButton is not null)
        {
            _todayButton.Click += OnTodayClick;
        }
        UpdatePalette();
        if (_strip is not null)
        {
            _strip.Owner = this;
            _strip.NavigateTo(CalendarMath.MonthIndex(_month), animate: false);
        }
        UpdateHeader(direction: 0);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ArcToday.Changed += OnTodayChanged;
        OnTodayChanged(null, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ArcToday.Changed -= OnTodayChanged;
    }

    private void OnTodayChanged(object? sender, EventArgs e)
    {
        var today = ArcToday.Today;
        if (today == Today)
        {
            return;
        }
        Today = today;
        _strip?.Refresh(null);
        UpdateHeader(direction: 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateProperty)
        {
            var oldValue = change.GetOldValue<DateOnly?>();
            var newValue = change.GetNewValue<DateOnly?>();
            if (newValue is { } date && DisplayMonth is null && _strip is null)
            {
                _month = CalendarMath.MonthStart(date);
            }
            _strip?.Refresh(CalendarSelectionHandoff.Create(oldValue, newValue, FirstDayOfWeek));
            UpdateHeader(direction: 0);
        }
        else if (change.Property == DisplayMonthProperty)
        {
            if (!_syncingMonth && change.GetNewValue<DateOnly?>() is { } month)
            {
                GoToMonth(month, raise: false);
            }
        }
        else if (change.Property == MinDateProperty || change.Property == MaxDateProperty ||
                 change.Property == DisabledDatesProperty || change.Property == ShowTodayProperty)
        {
            _strip?.Refresh(null);
            UpdateHeader(direction: 0);
        }
        else if (change.Property == FirstDayOfWeekProperty || change.Property == CultureProperty)
        {
            _strip?.Refresh(null);
            UpdateHeader(direction: 0);
        }
        else if (change.Property == DayForegroundProperty || change.Property == DayMutedForegroundProperty ||
                 change.Property == DaySelectedForegroundProperty || change.Property == DayHoverFillProperty ||
                 change.Property == SelectionFillProperty || change.Property == SelectionHoverFillProperty ||
                 change.Property == FocusRingBrushProperty || change.Property == DayFontSizeProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontWeightProperty)
        {
            UpdatePalette();
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _month = CalendarMath.MonthStart(DisplayMonth ?? SelectedDate ?? Today);
    }

    private void UpdatePalette()
    {
        static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        Palette.ForegroundColor = ColorOf(DayForeground);
        Palette.MutedColor = ColorOf(DayMutedForeground);
        Palette.SelectedForegroundColor = ColorOf(DaySelectedForeground);
        Palette.HoverFill = DayHoverFill ?? Brushes.Transparent;
        Palette.SelectionFill = SelectionFill ?? Brushes.Transparent;
        Palette.SelectionHoverFill = SelectionHoverFill ?? Brushes.Transparent;
        Palette.FocusRing = FocusRingBrush is { } ring ? new Pen(ring, 2) : null;
        Palette.FontFamily = FontFamily;
        Palette.FontSize = DayFontSize;
        Palette.Version++;
        if (_strip is not null)
        {
            foreach (var pane in _strip.Panes)
            {
                pane.InvalidateVisual();
                foreach (var cell in pane.DayCells)
                {
                    cell.InvalidateVisual();
                }
            }
        }
    }

    private void UpdateHeader(int direction)
    {
        var culture = EffectiveCulture;
        if (direction != 0)
        {
            TitleDirection = direction;
        }
        MonthTitle = _month.ToString(culture.DateTimeFormat.YearMonthPattern, culture);
        _weekdays?.Update(culture, FirstDayOfWeek);
        var previous = CalendarMath.AddMonths(_month, -1);
        var next = CalendarMath.AddMonths(_month, 1);
        var previousDisabled = MinDate is { } min && previous < CalendarMath.MonthStart(min);
        var nextDisabled = MaxDate is { } max && next > CalendarMath.MonthStart(max);
        if (_previous is not null)
        {
            _previous.IsEnabled = !previousDisabled;
            AutomationProperties.SetName(_previous, PreviousMonthLabel);
        }
        if (_next is not null)
        {
            _next.IsEnabled = !nextDisabled;
            AutomationProperties.SetName(_next, NextMonthLabel);
        }
        if (_todayButton is not null)
        {
            _todayButton.IsEnabled = !IsTodayIdle();
            AutomationProperties.SetName(_todayButton, $"{TodayLabel}, {Today.ToString("D", culture)}");
        }
        AutomationProperties.SetName(this, MonthTitle);
    }

    private bool TodaySelectable => !IsDateDisabled(Today);

    private bool IsTodayIdle() => CalendarMath.SameMonth(_month, Today) && (!TodaySelectable || SelectedDate == Today);

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => GoToMonth(CalendarMath.AddMonths(_month, -1), raise: true);

    private void OnNextClick(object? sender, RoutedEventArgs e) => GoToMonth(CalendarMath.AddMonths(_month, 1), raise: true);

    private void OnTodayClick(object? sender, RoutedEventArgs e)
    {
        if (IsTodayIdle())
        {
            return;
        }
        _focusedDate = Today;
        if (!CalendarMath.SameMonth(Today, _month))
        {
            GoToMonth(Today, raise: true);
        }
        if (TodaySelectable && SelectedDate != Today)
        {
            Select(Today);
        }
    }

    /// <summary>Slides to the month containing <paramref name="date"/>.</summary>
    public void GoToMonth(DateOnly date, bool raise = true)
    {
        var month = CalendarMath.MonthStart(date);
        if (month == _month && _strip?.PresentPane is not null)
        {
            return;
        }
        var direction = month > _month ? 1 : -1;
        _month = month;
        _strip?.NavigateTo(CalendarMath.MonthIndex(month), animate: true);
        UpdateHeader(direction);
        if (raise)
        {
            _syncingMonth = true;
            try
            {
                SetCurrentValue(DisplayMonthProperty, month);
            }
            finally
            {
                _syncingMonth = false;
            }
        }
    }

    private void Select(DateOnly date)
    {
        if (IsDateDisabled(date))
        {
            return;
        }
        _focusedDate = date;
        SetCurrentValue(SelectedDateProperty, date);
        RaiseEvent(new CalendarDateSelectedEventArgs(DateSelectedEvent, date));
    }

    internal void OnDayActivated(DateOnly date) => Select(date);

    internal void OnDayFocused(DateOnly date) => _focusedDate = date;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not CalendarDayCell cell)
        {
            return;
        }
        var date = cell.Date;
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Select(date);
            return;
        }
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var firstDay = FirstDayOfWeek;
        (DateOnly Target, int Step)? move = e.Key switch
        {
            Key.Left => (date.AddDays(-1), -1),
            Key.Right => (date.AddDays(1), 1),
            Key.Up => (date.AddDays(-7), -1),
            Key.Down => (date.AddDays(7), 1),
            Key.Home => (date.AddDays(-CalendarMath.Column(date, firstDay)), 1),
            Key.End => (date.AddDays(6 - CalendarMath.Column(date, firstDay)), -1),
            Key.PageUp => (CalendarMath.ShiftMonths(date, shift ? -12 : -1), -1),
            Key.PageDown => (CalendarMath.ShiftMonths(date, shift ? 12 : 1), 1),
            _ => null
        };
        if (move is not { } m)
        {
            return;
        }
        e.Handled = true;
        MoveFocus(date, m.Target, m.Step);
    }

    /// <summary>Clamps a keyboard move to the allowed range and steps past blocked days in the direction of travel.</summary>
    private void MoveFocus(DateOnly from, DateOnly target, int step)
    {
        var next = MinDate is { } min && target < min ? min : MaxDate is { } max && target > max ? max : target;
        for (var tries = 0; tries < 42 && IsDateDisabled(next); tries++)
        {
            next = next.AddDays(step);
        }
        if (IsDateDisabled(next) || next == from)
        {
            return;
        }
        _focusedDate = next;
        if (!CalendarMath.SameMonth(next, _month))
        {
            GoToMonth(next, raise: true);
        }
        else
        {
            _strip?.Refresh(null);
        }
        // Keyboard focus lands on the new day in the same frame the month starts to slide.
        _strip?.PresentPane?.FindCell(next)?.Focus(NavigationMethod.Directional);
    }
}
