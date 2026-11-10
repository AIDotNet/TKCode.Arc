using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="DateReel.SelectedDateChanged"/>.</summary>
public sealed class DateReelSelectedDateChangedEventArgs : RoutedEventArgs
{
    internal DateReelSelectedDateChangedEventArgs(RoutedEvent routedEvent, DateTime oldDate, DateTime newDate) : base(routedEvent)
    {
        OldDate = oldDate;
        NewDate = newDate;
    }

    /// <summary>The date before the change.</summary>
    public DateTime OldDate { get; }

    /// <summary>The date now picked.</summary>
    public DateTime NewDate { get; }
}

/// <summary>
/// A date picker made of turning wheels, one each for the day, the month and the year, as on a phone. Drag or flick a
/// wheel and it coasts and comes to rest on a row; the mouse wheel, a tap on a row above or below, and the arrow keys
/// turn it too. The day and month wheels loop; the year wheel stops at its ends.
/// </summary>
/// <remarks>
/// The day wheel always has as many days as the month: picking February on the 31st moves the day to the 28th or 29th.
/// A date outside <see cref="MinDate"/> and <see cref="MaxDate"/> is drawn faded, and the wheels turn back into the range
/// when one comes to rest there.
/// </remarks>
[TemplatePart(ColumnsPart, typeof(Grid))]
public class DateReel : TemplatedControl
{
    internal const string ColumnsPart = "PART_Columns";

    /// <summary>The picked date, today by default (the time of day is dropped). It is kept between <see cref="MinDate"/> and <see cref="MaxDate"/>.</summary>
    public static readonly StyledProperty<DateTime> SelectedDateProperty =
        AvaloniaProperty.Register<DateReel, DateTime>(nameof(SelectedDate), new DateTime(2026, 1, 1),
            defaultBindingMode: BindingMode.TwoWay, coerce: static (o, v) => ((DateReel)o).Clamp(v));

    /// <summary>The earliest date that can be picked. Its year is the first row of the year wheel. Default 1 January 1900.</summary>
    public static readonly StyledProperty<DateTime> MinDateProperty =
        AvaloniaProperty.Register<DateReel, DateTime>(nameof(MinDate), new DateTime(1900, 1, 1));

    /// <summary>The latest date that can be picked. Its year is the last row of the year wheel. Default 31 December 2100.</summary>
    public static readonly StyledProperty<DateTime> MaxDateProperty =
        AvaloniaProperty.Register<DateReel, DateTime>(nameof(MaxDate), new DateTime(2100, 12, 31));

    /// <summary>The order of the wheels from left to right.</summary>
    public static readonly StyledProperty<DateReelOrder> OrderProperty =
        AvaloniaProperty.Register<DateReel, DateReelOrder>(nameof(Order));

    /// <summary>Culture of the month names. Default en-US.</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<DateReel, string>(nameof(CultureName), "en-US");

    /// <summary>Whether the month wheel shows full names (March) instead of short ones (Mar).</summary>
    public static readonly StyledProperty<bool> UsesFullMonthNamesProperty =
        AvaloniaProperty.Register<DateReel, bool>(nameof(UsesFullMonthNames), true);

    /// <summary>How many rows each wheel shows, the picked one in the middle. Odd numbers from 3 to 9; default 5.</summary>
    public static readonly StyledProperty<int> VisibleItemCountProperty =
        AvaloniaProperty.Register<DateReel, int>(nameof(VisibleItemCount), 5,
            coerce: static (_, v) => Math.Clamp(v % 2 == 0 ? v + 1 : v, 3, 9));

    /// <summary>Accessible name of the day wheel.</summary>
    public static readonly StyledProperty<string> DayLabelProperty =
        AvaloniaProperty.Register<DateReel, string>(nameof(DayLabel), "Day");

    /// <summary>Accessible name of the month wheel.</summary>
    public static readonly StyledProperty<string> MonthLabelProperty =
        AvaloniaProperty.Register<DateReel, string>(nameof(MonthLabel), "Month");

    /// <summary>Accessible name of the year wheel.</summary>
    public static readonly StyledProperty<string> YearLabelProperty =
        AvaloniaProperty.Register<DateReel, string>(nameof(YearLabel), "Year");

    /// <summary>Raised when the picked date changes, by the wheels or by code.</summary>
    public static readonly RoutedEvent<DateReelSelectedDateChangedEventArgs> SelectedDateChangedEvent =
        RoutedEvent.Register<DateReel, DateReelSelectedDateChangedEventArgs>(nameof(SelectedDateChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<DateReel, double>(nameof(ItemHeight), 36);

    internal static readonly StyledProperty<double> ColumnPaddingProperty =
        AvaloniaProperty.Register<DateReel, double>(nameof(ColumnPadding), 10);

    internal static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<DateReel, double>("ColumnSpacing", 4);

    internal static readonly StyledProperty<double> EdgeFadeProperty =
        AvaloniaProperty.Register<DateReel, double>(nameof(EdgeFade), 0.85);

    internal static readonly StyledProperty<double> FlickDecayProperty =
        AvaloniaProperty.Register<DateReel, double>(nameof(FlickDecay), 0.995);

    internal static readonly StyledProperty<int> PageRowsProperty =
        AvaloniaProperty.Register<DateReel, int>(nameof(PageRows), 5);

    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<DateReel, IBrush?>(nameof(FocusBrush));

    internal static readonly StyledProperty<CornerRadius> BandCornerRadiusProperty =
        AvaloniaProperty.Register<DateReel, CornerRadius>(nameof(BandCornerRadius));

    private readonly DateReelColumn _day;
    private readonly DateReelColumn _month;
    private readonly DateReelColumn _year;
    private readonly string[] _monthNames = new string[12];
    private Grid? _columns;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");
    private bool _syncing;

    // Theme-set values, read through these so the XAML setters can name them.
    internal double ColumnSpacing => GetValue(ColumnSpacingProperty);

    public DateReel()
    {
        _day = new DateReelColumn(this, DateReelPart.Day);
        _month = new DateReelColumn(this, DateReelPart.Month);
        _year = new DateReelColumn(this, DateReelPart.Year);
        UpdateMonthNames();
        Sync(animate: false);
        SetCurrentValue(SelectedDateProperty, DateTime.Today);
    }

    /// <inheritdoc cref="SelectedDateProperty"/>
    public DateTime SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <inheritdoc cref="MinDateProperty"/>
    public DateTime MinDate
    {
        get => GetValue(MinDateProperty);
        set => SetValue(MinDateProperty, value);
    }

    /// <inheritdoc cref="MaxDateProperty"/>
    public DateTime MaxDate
    {
        get => GetValue(MaxDateProperty);
        set => SetValue(MaxDateProperty, value);
    }

    /// <inheritdoc cref="OrderProperty"/>
    public DateReelOrder Order
    {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName
    {
        get => GetValue(CultureNameProperty);
        set => SetValue(CultureNameProperty, value);
    }

    /// <inheritdoc cref="UsesFullMonthNamesProperty"/>
    public bool UsesFullMonthNames
    {
        get => GetValue(UsesFullMonthNamesProperty);
        set => SetValue(UsesFullMonthNamesProperty, value);
    }

    /// <inheritdoc cref="VisibleItemCountProperty"/>
    public int VisibleItemCount
    {
        get => GetValue(VisibleItemCountProperty);
        set => SetValue(VisibleItemCountProperty, value);
    }

    /// <inheritdoc cref="DayLabelProperty"/>
    public string DayLabel
    {
        get => GetValue(DayLabelProperty);
        set => SetValue(DayLabelProperty, value);
    }

    /// <inheritdoc cref="MonthLabelProperty"/>
    public string MonthLabel
    {
        get => GetValue(MonthLabelProperty);
        set => SetValue(MonthLabelProperty, value);
    }

    /// <inheritdoc cref="YearLabelProperty"/>
    public string YearLabel
    {
        get => GetValue(YearLabelProperty);
        set => SetValue(YearLabelProperty, value);
    }

    /// <summary>Raised when the picked date changes, by the wheels or by code.</summary>
    public event EventHandler<DateReelSelectedDateChangedEventArgs>? SelectedDateChanged
    {
        add => AddHandler(SelectedDateChangedEvent, value);
        remove => RemoveHandler(SelectedDateChangedEvent, value);
    }

    internal double ItemHeight => GetValue(ItemHeightProperty);

    internal double ColumnPadding => GetValue(ColumnPaddingProperty);

    internal double EdgeFade => GetValue(EdgeFadeProperty);

    internal double FlickDecay => Math.Clamp(GetValue(FlickDecayProperty), 0.5, 0.999);

    internal int PageRows => Math.Max(1, GetValue(PageRowsProperty));

    internal IBrush? FocusBrush => GetValue(FocusBrushProperty);

    internal CornerRadius BandCornerRadius => GetValue(BandCornerRadiusProperty);

    internal Typeface ItemTypeface => new(FontFamily, FontStyle.Normal, FontWeight.Medium);

    internal DateReelColumn DayColumn => _day;

    internal DateReelColumn MonthColumn => _month;

    internal DateReelColumn YearColumn => _year;

    /// <summary>The wheels from left to right, in <see cref="Order"/>.</summary>
    internal IReadOnlyList<DateReelColumn> Columns => Order switch
    {
        DateReelOrder.DayMonthYear => [_day, _month, _year],
        DateReelOrder.YearMonthDay => [_year, _month, _day],
        _ => [_month, _day, _year]
    };

    private int FirstYear => MinDate.Year;

    private int LastYear => Math.Max(MinDate.Year, MaxDate.Year);

    /// <summary>Moves the picked date and turns the wheels to it.</summary>
    public void SetDate(DateTime date) => SetCurrentValue(SelectedDateProperty, date);

    private DateTime Clamp(DateTime value)
    {
        var date = value.Date;
        var min = MinDate.Date;
        var max = MaxDate.Date < min ? min : MaxDate.Date;
        return date < min ? min : date > max ? max : date;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _columns?.Children.Clear();
        _columns = e.NameScope.Find<Grid>(ColumnsPart);
        PlaceColumns();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateProperty)
        {
            var (oldDate, newDate) = change.GetOldAndNewValue<DateTime>();
            Sync(animate: true);
            if (oldDate != newDate)
            {
                RaiseEvent(new DateReelSelectedDateChangedEventArgs(SelectedDateChangedEvent, oldDate, newDate));
            }
        }
        else if (change.Property == MinDateProperty || change.Property == MaxDateProperty)
        {
            CoerceValue(SelectedDateProperty);
            Sync(animate: false);
            _year.ClearText();
            InvalidateColumns();
        }
        else if (change.Property == OrderProperty || change.Property == ColumnSpacingProperty)
        {
            PlaceColumns();
        }
        else if (change.Property == CultureNameProperty || change.Property == UsesFullMonthNamesProperty)
        {
            UpdateMonthNames();
            _month.ClearText();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == ForegroundProperty || change.Property == FocusBrushProperty ||
                 change.Property == BandCornerRadiusProperty)
        {
            _day.ClearText();
            _month.ClearText();
            _year.ClearText();
        }
        else if (change.Property == VisibleItemCountProperty || change.Property == ItemHeightProperty ||
                 change.Property == ColumnPaddingProperty)
        {
            InvalidateColumns();
        }
        else if (change.Property == EdgeFadeProperty)
        {
            _day.InvalidateVisual();
            _month.InvalidateVisual();
            _year.InvalidateVisual();
        }
    }

    private void InvalidateColumns()
    {
        _day.InvalidateMeasure();
        _month.InvalidateMeasure();
        _year.InvalidateMeasure();
        _day.InvalidateVisual();
        _month.InvalidateVisual();
        _year.InvalidateVisual();
    }

    private void PlaceColumns()
    {
        if (_columns is null)
        {
            return;
        }
        _columns.Children.Clear();
        _columns.ColumnDefinitions.Clear();
        var columns = Columns;
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            _columns.ColumnDefinitions.Add(new ColumnDefinition(column.Part == DateReelPart.Month ? 1.6 : 1, GridUnitType.Star));
            Grid.SetColumn(column, i);
            column.Margin = new Thickness(i == 0 ? 0 : GetValue(ColumnSpacingProperty) / 2, 0,
                i == columns.Count - 1 ? 0 : GetValue(ColumnSpacingProperty) / 2, 0);
            _columns.Children.Add(column);
        }
    }

    private void UpdateMonthNames()
    {
        try
        {
            _culture = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
        }
        catch (CultureNotFoundException)
        {
            _culture = CultureInfo.InvariantCulture;
        }
        var format = _culture.DateTimeFormat;
        for (var i = 0; i < 12; i++)
        {
            _monthNames[i] = UsesFullMonthNames ? format.GetMonthName(i + 1) : format.GetAbbreviatedMonthName(i + 1);
        }
    }

    /// <summary>Turns every wheel to the picked date; the day wheel takes the month's length first.</summary>
    private void Sync(bool animate)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        try
        {
            var date = SelectedDate;
            _year.Configure(LastYear - FirstYear + 1, false, date.Year - FirstYear);
            _month.Configure(12, true, date.Month - 1);
            _day.Configure(DateTime.DaysInMonth(date.Year, date.Month), true, date.Day - 1);
            _year.ShowIndex(date.Year - FirstYear, animate);
            _month.ShowIndex(date.Month - 1, animate);
            _day.ShowIndex(date.Day - 1, animate);
            _year.InvalidateVisual();
            _month.InvalidateVisual();
            _day.InvalidateVisual();
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>A wheel came to rest on a row: the date follows, with the day kept inside the month and the date in range.</summary>
    internal void OnColumnPicked(DateReelColumn column, int index)
    {
        if (_syncing)
        {
            return;
        }
        var date = SelectedDate;
        var year = column.Part == DateReelPart.Year ? FirstYear + index : date.Year;
        var month = column.Part == DateReelPart.Month ? index + 1 : date.Month;
        var day = column.Part == DateReelPart.Day ? index + 1 : date.Day;
        day = Math.Min(day, DateTime.DaysInMonth(year, month));
        var picked = new DateTime(year, month, day);
        SetCurrentValue(SelectedDateProperty, picked);
        // The same date (an out-of-range pick clamped back): the wheels still turn back to it.
        if (SelectedDate == date)
        {
            Sync(animate: true);
        }
    }

    /// <summary>Moves keyboard focus to the wheel left (−1) or right (+1) of <paramref name="column"/>.</summary>
    internal void FocusNext(DateReelColumn column, int direction)
    {
        var columns = Columns;
        for (var i = 0; i < columns.Count; i++)
        {
            if (!ReferenceEquals(columns[i], column))
            {
                continue;
            }
            var next = i + direction;
            if (next >= 0 && next < columns.Count)
            {
                columns[next].Focus(NavigationMethod.Tab);
            }
            return;
        }
    }

    internal string LabelOf(DateReelPart part) => part switch
    {
        DateReelPart.Day => DayLabel,
        DateReelPart.Month => MonthLabel,
        _ => YearLabel
    };

    internal string TextOf(DateReelPart part, int index) => part switch
    {
        DateReelPart.Day => (index + 1).ToString(_culture),
        DateReelPart.Month => _monthNames[Math.Clamp(index, 0, 11)],
        _ => (FirstYear + index).ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>The widest text a wheel can show, which sets the wheel's width.</summary>
    internal string WidestText(DateReelPart part)
    {
        switch (part)
        {
            case DateReelPart.Day:
                return "30";
            case DateReelPart.Year:
                return "0000";
            default:
                var widest = _monthNames[0];
                foreach (var name in _monthNames)
                {
                    if (name.Length > widest.Length)
                    {
                        widest = name;
                    }
                }
                return widest;
        }
    }

    /// <summary>Whether a row can be picked with the other wheels where they are now: false when it leads out of range.</summary>
    internal bool IsAllowed(DateReelPart part, int index)
    {
        var date = SelectedDate;
        var min = MinDate.Date;
        var max = MaxDate.Date;
        switch (part)
        {
            case DateReelPart.Year:
                return true;
            case DateReelPart.Month:
            {
                var first = new DateTime(date.Year, index + 1, 1);
                var last = first.AddMonths(1).AddDays(-1);
                return last >= min && first <= max;
            }
            default:
            {
                if (index + 1 > DateTime.DaysInMonth(date.Year, date.Month))
                {
                    return false;
                }
                var day = new DateTime(date.Year, date.Month, index + 1);
                return day >= min && day <= max;
            }
        }
    }
}
