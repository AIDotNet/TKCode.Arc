using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How a billing cycle looks against the budget (what the headline says).</summary>
public enum UsageOutcome
{
    /// <summary>Usage already passed the budget on a measured day.</summary>
    Passed,

    /// <summary>The forecast reaches the budget before the last day of the cycle.</summary>
    WillCross,

    /// <summary>The forecast reaches the budget on the last day of the cycle.</summary>
    CrossesOnLastDay,

    /// <summary>The forecast stays under the budget until the cycle closes.</summary>
    StaysUnder
}

/// <summary>Where an alert threshold stands.</summary>
public enum UsageAlertStatus
{
    /// <summary>Already reached on the first day of the cycle.</summary>
    AlreadyPast,

    /// <summary>Reached on a measured day.</summary>
    Reached,

    /// <summary>The forecast reaches it on a day to come.</summary>
    Expected,

    /// <summary>Not reached before the cycle closes.</summary>
    NotExpected
}

/// <summary>
/// One metric a <see cref="UsageForecast"/> can show (calls, storage, seats): the measured readings of the cycle so far,
/// an optional forecast, the fixed axis, the budget with its range and step, and how values and cost are written. The
/// forecast and its likely range are computed from the readings when you leave them out.
/// </summary>
public class UsageMetric : AvaloniaObject
{
    /// <summary>A stable identifier (the switch's value, event arguments).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<UsageMetric, string>(nameof(Key), string.Empty);

    /// <summary>The name on the switch, in the data table and in spoken summaries.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<UsageMetric, string>(nameof(Label), string.Empty);

    /// <summary>
    /// The readings of days 1..today (a running total from the start of the cycle, or a level such as storage in use).
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ReadingsProperty =
        AvaloniaProperty.Register<UsageMetric, IReadOnlyList<double>?>(nameof(Readings));

    /// <summary>The value at the start of the cycle (day 0): 0 for running totals; null takes the first reading.</summary>
    public static readonly StyledProperty<double?> StartValueProperty =
        AvaloniaProperty.Register<UsageMetric, double?>(nameof(StartValue));

    /// <summary>The forecast for every day after today (null computes it from the readings).</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ForecastProperty =
        AvaloniaProperty.Register<UsageMetric, IReadOnlyList<double>?>(nameof(Forecast));

    /// <summary>The lower edge of the likely range for every day after today (null computes it).</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ForecastLowProperty =
        AvaloniaProperty.Register<UsageMetric, IReadOnlyList<double>?>(nameof(ForecastLow));

    /// <summary>The upper edge of the likely range for every day after today (null computes it).</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ForecastHighProperty =
        AvaloniaProperty.Register<UsageMetric, IReadOnlyList<double>?>(nameof(ForecastHigh));

    /// <summary>The bottom of the value axis (default 0).</summary>
    public static readonly StyledProperty<double> AxisMinimumProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(AxisMinimum), 0);

    /// <summary>The top of the value axis; NaN (default) picks a round top that holds the data and the budget range.</summary>
    public static readonly StyledProperty<double> AxisMaximumProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(AxisMaximum), double.NaN);

    /// <summary>The lowest budget the line can be dragged to (default: the axis minimum).</summary>
    public static readonly StyledProperty<double> BudgetMinimumProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(BudgetMinimum), double.NaN);

    /// <summary>The highest budget the line can be dragged to (default: the axis maximum).</summary>
    public static readonly StyledProperty<double> BudgetMaximumProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(BudgetMaximum), double.NaN);

    /// <summary>The step a drag snaps to and an arrow key moves by (default 1).</summary>
    public static readonly StyledProperty<double> BudgetStepProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(BudgetStep), 1);

    /// <summary>The budget (two-way): where the horizontal line sits.</summary>
    public static readonly StyledProperty<double> BudgetProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(Budget), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The price of one unit above the budget; 0 hides the cost tile.</summary>
    public static readonly StyledProperty<double> UnitPriceProperty =
        AvaloniaProperty.Register<UsageMetric, double>(nameof(UnitPrice), 0);

    /// <summary>The small text under the cost ("At $0.20 per 1k calls").</summary>
    public static readonly StyledProperty<string?> PriceNoteProperty =
        AvaloniaProperty.Register<UsageMetric, string?>(nameof(PriceNote));

    /// <summary>Writes a value without its unit; null gives one decimal.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<UsageMetric, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The unit written after values ("M", "GB").</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<UsageMetric, string>(nameof(Unit), string.Empty);

    /// <summary>What sits between a value and its unit ("" for "38M", " " for "640 GB").</summary>
    public static readonly StyledProperty<string> UnitSeparatorProperty =
        AvaloniaProperty.Register<UsageMetric, string>(nameof(UnitSeparator), string.Empty);

    /// <summary>Writes the cost; null gives whole dollars ("$1,449").</summary>
    public static readonly StyledProperty<Func<double, string>?> CurrencyFormatterProperty =
        AvaloniaProperty.Register<UsageMetric, Func<double, string>?>(nameof(CurrencyFormatter));

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ReadingsProperty"/>
    public IReadOnlyList<double>? Readings { get => GetValue(ReadingsProperty); set => SetValue(ReadingsProperty, value); }

    /// <inheritdoc cref="StartValueProperty"/>
    public double? StartValue { get => GetValue(StartValueProperty); set => SetValue(StartValueProperty, value); }

    /// <inheritdoc cref="ForecastProperty"/>
    public IReadOnlyList<double>? Forecast { get => GetValue(ForecastProperty); set => SetValue(ForecastProperty, value); }

    /// <inheritdoc cref="ForecastLowProperty"/>
    public IReadOnlyList<double>? ForecastLow { get => GetValue(ForecastLowProperty); set => SetValue(ForecastLowProperty, value); }

    /// <inheritdoc cref="ForecastHighProperty"/>
    public IReadOnlyList<double>? ForecastHigh { get => GetValue(ForecastHighProperty); set => SetValue(ForecastHighProperty, value); }

    /// <inheritdoc cref="AxisMinimumProperty"/>
    public double AxisMinimum { get => GetValue(AxisMinimumProperty); set => SetValue(AxisMinimumProperty, value); }

    /// <inheritdoc cref="AxisMaximumProperty"/>
    public double AxisMaximum { get => GetValue(AxisMaximumProperty); set => SetValue(AxisMaximumProperty, value); }

    /// <inheritdoc cref="BudgetMinimumProperty"/>
    public double BudgetMinimum { get => GetValue(BudgetMinimumProperty); set => SetValue(BudgetMinimumProperty, value); }

    /// <inheritdoc cref="BudgetMaximumProperty"/>
    public double BudgetMaximum { get => GetValue(BudgetMaximumProperty); set => SetValue(BudgetMaximumProperty, value); }

    /// <inheritdoc cref="BudgetStepProperty"/>
    public double BudgetStep { get => GetValue(BudgetStepProperty); set => SetValue(BudgetStepProperty, value); }

    /// <inheritdoc cref="BudgetProperty"/>
    public double Budget { get => GetValue(BudgetProperty); set => SetValue(BudgetProperty, value); }

    /// <inheritdoc cref="UnitPriceProperty"/>
    public double UnitPrice { get => GetValue(UnitPriceProperty); set => SetValue(UnitPriceProperty, value); }

    /// <inheritdoc cref="PriceNoteProperty"/>
    public string? PriceNote { get => GetValue(PriceNoteProperty); set => SetValue(PriceNoteProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <inheritdoc cref="UnitSeparatorProperty"/>
    public string UnitSeparator { get => GetValue(UnitSeparatorProperty); set => SetValue(UnitSeparatorProperty, value); }

    /// <inheritdoc cref="CurrencyFormatterProperty"/>
    public Func<double, string>? CurrencyFormatter { get => GetValue(CurrencyFormatterProperty); set => SetValue(CurrencyFormatterProperty, value); }

    /// <summary>The lowest budget in effect.</summary>
    internal double EffectiveBudgetMinimum => double.IsNaN(BudgetMinimum) ? AxisMinimum : BudgetMinimum;

    /// <summary>A value without its unit.</summary>
    internal string FormatPlain(double value) => ValueFormatter?.Invoke(value) ?? AnalyticsFormat.Number(value, 1, false);

    /// <summary>A value with its unit ("38.0M").</summary>
    internal string FormatValue(double value) => FormatPlain(value) + UnitSeparator + Unit;

    /// <summary>The cost of <paramref name="amount"/>.</summary>
    internal string FormatCost(double amount) => CurrencyFormatter?.Invoke(amount) ?? AnalyticsFormat.Currency(amount);
}

/// <summary>An alert threshold: a share of the budget the person wants a notification at.</summary>
public class UsageAlertThreshold : AvaloniaObject
{
    /// <summary>The share of the budget in percent (50, 80, 100).</summary>
    public static readonly StyledProperty<double> PercentProperty =
        AvaloniaProperty.Register<UsageAlertThreshold, double>(nameof(Percent), 100);

    /// <summary>Whether the alert is on (two-way).</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<UsageAlertThreshold, bool>(nameof(IsEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Where the threshold stands against the current budget (read-only, kept up to date by the block).</summary>
    public static readonly DirectProperty<UsageAlertThreshold, UsageAlertStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<UsageAlertThreshold, UsageAlertStatus>(nameof(Status), o => o.Status);

    /// <summary>The day the threshold was or will be reached (null when it is not expected).</summary>
    public static readonly DirectProperty<UsageAlertThreshold, DateOnly?> DateProperty =
        AvaloniaProperty.RegisterDirect<UsageAlertThreshold, DateOnly?>(nameof(Date), o => o.Date);

    private UsageAlertStatus _status = UsageAlertStatus.NotExpected;
    private DateOnly? _date;

    /// <inheritdoc cref="PercentProperty"/>
    public double Percent { get => GetValue(PercentProperty); set => SetValue(PercentProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public UsageAlertStatus Status => _status;

    /// <inheritdoc cref="DateProperty"/>
    public DateOnly? Date => _date;

    /// <summary>The day number (1-based) behind <see cref="Date"/>; 0 when none.</summary>
    internal int Day { get; private set; }

    internal void SetState(UsageAlertStatus status, DateOnly? date, int day)
    {
        Day = day;
        SetAndRaise(StatusProperty, ref _status, status);
        SetAndRaise(DateProperty, ref _date, date);
    }
}

/// <summary>Arguments of <see cref="UsageForecast.BudgetChangedEvent"/>.</summary>
public sealed class UsageBudgetChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsageBudgetChangedEventArgs(RoutedEvent routedEvent, UsageMetric metric, double oldValue, double newValue, bool isCommitted)
        : base(routedEvent)
    {
        Metric = metric;
        OldValue = oldValue;
        NewValue = newValue;
        IsCommitted = isCommitted;
    }

    /// <summary>The metric whose budget changed.</summary>
    public UsageMetric Metric { get; }

    /// <summary>The budget before.</summary>
    public double OldValue { get; }

    /// <summary>The budget now.</summary>
    public double NewValue { get; }

    /// <summary>False while the line is being dragged (every step), true when the person lets go or uses a key.</summary>
    public bool IsCommitted { get; }
}

/// <summary>Arguments of <see cref="UsageForecast.AlertThresholdToggledEvent"/>.</summary>
public sealed class UsageAlertToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsageAlertToggledEventArgs(RoutedEvent routedEvent, UsageAlertThreshold threshold)
        : base(routedEvent)
    {
        Threshold = threshold;
    }

    /// <summary>The threshold that was switched.</summary>
    public UsageAlertThreshold Threshold { get; }

    /// <summary>Whether it is on now.</summary>
    public bool IsEnabled => Threshold.IsEnabled;
}

/// <summary>Arguments of <see cref="UsageForecast.MetricChangedEvent"/>.</summary>
public sealed class UsageMetricChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsageMetricChangedEventArgs(RoutedEvent routedEvent, UsageMetric? metric)
        : base(routedEvent)
    {
        Metric = metric;
    }

    /// <summary>The metric now shown.</summary>
    public UsageMetric? Metric { get; }
}

/// <summary>Arguments of <see cref="UsageForecast.InspectedDayChangedEvent"/>.</summary>
public sealed class UsageInspectedDayChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsageInspectedDayChangedEventArgs(RoutedEvent routedEvent, int? day)
        : base(routedEvent)
    {
        Day = day;
    }

    /// <summary>The day (1-based) under the crosshair, or null when nothing is inspected.</summary>
    public int? Day { get; }
}
