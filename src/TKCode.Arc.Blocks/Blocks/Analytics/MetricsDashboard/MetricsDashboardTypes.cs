using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How a <see cref="MetricsDashboardMetric"/> adds up over a range and how its change is measured.</summary>
public enum MetricsDashboardMetricKind
{
    /// <summary>A count (sessions, sign-ups): a range or a week is the sum of its days; the change is relative, in percent.</summary>
    Count,

    /// <summary>
    /// A rate in percent units (4.8 means 4.8%): a range or a week is the ratio of the summed numerators and denominators
    /// when they are given, otherwise the mean of the daily rates; the change is the difference in points ("0.1 pts").
    /// </summary>
    Ratio
}

/// <summary>The layout a <see cref="MetricsDashboard"/> picks from its own width.</summary>
public enum MetricsDashboardSize
{
    /// <summary>640 wide or more: four metric tabs in a row, the two lists side by side.</summary>
    Large,

    /// <summary>460 to 639 wide: the metric tabs in a 2 × 2 grid, the lists stacked.</summary>
    Medium,

    /// <summary>Under 460 wide: as medium, with the range switch across the full width, 18px values and a 168px plot.</summary>
    Small
}

/// <summary>
/// One metric of a <see cref="MetricsDashboard"/>: a tab with its total and change, and the series the chart draws when
/// the tab is selected. <see cref="Daily"/> holds one value per day, the last one for the dashboard's
/// <see cref="MetricsDashboard.EndDate"/>; give at least twice the longest range so every range has a previous period
/// to compare with.
/// </summary>
public class MetricsDashboardMetric : AvaloniaObject
{
    /// <summary>A stable key for your code (not shown).</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, string?>(nameof(Key));

    /// <summary>The name on the tab, in the legend and in the chart's screen reader name.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, string>(nameof(Label), string.Empty);

    /// <summary>Whether the metric is a count or a rate.</summary>
    public static readonly StyledProperty<MetricsDashboardMetricKind> KindProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, MetricsDashboardMetricKind>(nameof(Kind));

    /// <summary>One value per day, oldest first; the last one is the dashboard's end date.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> DailyProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, IReadOnlyList<double>?>(nameof(Daily));

    /// <summary>For a rate: the daily numerators (conversions), aligned with <see cref="Daily"/>; with <see cref="Denominators"/> they give exact weekly and range rates.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> NumeratorsProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, IReadOnlyList<double>?>(nameof(Numerators));

    /// <summary>For a rate: the daily denominators (sessions), aligned with <see cref="Daily"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> DenominatorsProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, IReadOnlyList<double>?>(nameof(Denominators));

    /// <summary>Whether a rise is good (green). False for churn, bounce or latency: a fall is then shown in the success color.</summary>
    public static readonly StyledProperty<bool> HigherIsBetterProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, bool>(nameof(HigherIsBetter), true);

    /// <summary>Formats the tab's value and the tooltip's values; null writes counts as "36,686" and rates as "4.8%".</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Formats the y axis labels; null writes counts as "0", "500", "1.5K" and rates with the decimals the tick step needs ("42.5%").</summary>
    public static readonly StyledProperty<Func<double, string>?> AxisFormatterProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, Func<double, string>?>(nameof(AxisFormatter));

    /// <summary>Formats the size of the change (always positive; the arrow shows the direction); null writes "11.7%" for counts and "0.1 pts" for rates.</summary>
    public static readonly StyledProperty<Func<double, string>?> DeltaFormatterProperty =
        AvaloniaProperty.Register<MetricsDashboardMetric, Func<double, string>?>(nameof(DeltaFormatter));

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public MetricsDashboardMetricKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="DailyProperty"/>
    public IReadOnlyList<double>? Daily { get => GetValue(DailyProperty); set => SetValue(DailyProperty, value); }

    /// <inheritdoc cref="NumeratorsProperty"/>
    public IReadOnlyList<double>? Numerators { get => GetValue(NumeratorsProperty); set => SetValue(NumeratorsProperty, value); }

    /// <inheritdoc cref="DenominatorsProperty"/>
    public IReadOnlyList<double>? Denominators { get => GetValue(DenominatorsProperty); set => SetValue(DenominatorsProperty, value); }

    /// <inheritdoc cref="HigherIsBetterProperty"/>
    public bool HigherIsBetter { get => GetValue(HigherIsBetterProperty); set => SetValue(HigherIsBetterProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="AxisFormatterProperty"/>
    public Func<double, string>? AxisFormatter { get => GetValue(AxisFormatterProperty); set => SetValue(AxisFormatterProperty, value); }

    /// <inheritdoc cref="DeltaFormatterProperty"/>
    public Func<double, string>? DeltaFormatter { get => GetValue(DeltaFormatterProperty); set => SetValue(DeltaFormatterProperty, value); }
}

/// <summary>
/// One row of a ranked list under the chart (a page, a referrer): a name and one value per range. The row's bar shows the
/// value as a share of the list's first row, so give the rows largest first.
/// </summary>
public class MetricsDashboardItem : AvaloniaObject
{
    /// <summary>The name; long names end in an ellipsis and show in full as a tooltip.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MetricsDashboardItem, string>(nameof(Label), string.Empty);

    /// <summary>The value for each range, in the order of <see cref="MetricsDashboard.Ranges"/>; a missing entry uses <see cref="Value"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<MetricsDashboardItem, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>The value when <see cref="Values"/> has none for the selected range.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MetricsDashboardItem, double>(nameof(Value));

    /// <summary>Anything of yours that belongs to the row.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<MetricsDashboardItem, object?>(nameof(Tag));

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The value shown for the range at <paramref name="rangeIndex"/>.</summary>
    public double ValueAt(int rangeIndex)
    {
        var values = Values;
        return values is not null && rangeIndex >= 0 && rangeIndex < values.Count ? values[rangeIndex] : Value;
    }
}

/// <summary>What an export was asked for: passed to <see cref="MetricsDashboard.ExportCommand"/> and <see cref="MetricsDashboard.ExportRequested"/>.</summary>
public sealed class MetricsDashboardExportRequest
{
    /// <summary>Creates a request.</summary>
    /// <param name="rangeDays">The selected range in days.</param>
    /// <param name="startDate">The first day of the range.</param>
    /// <param name="endDate">The last day of the range.</param>
    /// <param name="metric">The selected metric, if any.</param>
    /// <param name="metricIndex">Its index (−1 for none).</param>
    /// <param name="isCompareEnabled">Whether the previous period was shown.</param>
    public MetricsDashboardExportRequest(int rangeDays, DateOnly startDate, DateOnly endDate, MetricsDashboardMetric? metric, int metricIndex,
        bool isCompareEnabled)
    {
        RangeDays = rangeDays;
        StartDate = startDate;
        EndDate = endDate;
        Metric = metric;
        MetricIndex = metricIndex;
        IsCompareEnabled = isCompareEnabled;
    }

    /// <summary>The selected range in days.</summary>
    public int RangeDays { get; }

    /// <summary>The first day of the range.</summary>
    public DateOnly StartDate { get; }

    /// <summary>The last day of the range.</summary>
    public DateOnly EndDate { get; }

    /// <summary>The selected metric, if any.</summary>
    public MetricsDashboardMetric? Metric { get; }

    /// <summary>Its index in <see cref="MetricsDashboard.Metrics"/> (−1 for none).</summary>
    public int MetricIndex { get; }

    /// <summary>Whether the previous period was shown.</summary>
    public bool IsCompareEnabled { get; }
}

/// <summary>Arguments of <c>MetricsDashboard.RangeChanged</c>.</summary>
public class MetricsDashboardRangeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricsDashboardRangeChangedEventArgs(RoutedEvent routedEvent, object source, int oldRange, int newRange)
        : base(routedEvent, source)
    {
        OldRange = oldRange;
        NewRange = newRange;
    }

    /// <summary>The previous range in days.</summary>
    public int OldRange { get; }

    /// <summary>The new range in days.</summary>
    public int NewRange { get; }
}

/// <summary>Arguments of <c>MetricsDashboard.SelectedMetricChanged</c>.</summary>
public class MetricsDashboardMetricChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricsDashboardMetricChangedEventArgs(RoutedEvent routedEvent, object source, int oldIndex, int newIndex, MetricsDashboardMetric? metric)
        : base(routedEvent, source)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Metric = metric;
    }

    /// <summary>The previous index.</summary>
    public int OldIndex { get; }

    /// <summary>The new index.</summary>
    public int NewIndex { get; }

    /// <summary>The metric now selected (null when the index is out of range).</summary>
    public MetricsDashboardMetric? Metric { get; }
}

/// <summary>Arguments of <c>MetricsDashboard.CompareChanged</c>.</summary>
public class MetricsDashboardCompareChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricsDashboardCompareChangedEventArgs(RoutedEvent routedEvent, object source, bool isEnabled)
        : base(routedEvent, source)
    {
        IsEnabled = isEnabled;
    }

    /// <summary>Whether the previous period is now shown.</summary>
    public bool IsEnabled { get; }
}

/// <summary>Arguments of <c>MetricsDashboard.PointInspected</c>: a point of the chart is being inspected (pointer or keys).</summary>
public class MetricsDashboardPointEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricsDashboardPointEventArgs(RoutedEvent routedEvent, object source, int index, DateOnly date, int days, double value,
        double? previousValue)
        : base(routedEvent, source)
    {
        Index = index;
        Date = date;
        Days = days;
        Value = value;
        PreviousValue = previousValue;
    }

    /// <summary>The point's index in the chart.</summary>
    public int Index { get; }

    /// <summary>The point's day (for a weekly point, the first day of its week).</summary>
    public DateOnly Date { get; }

    /// <summary>How many days the point covers (1 for daily points, up to 7 for weeks).</summary>
    public int Days { get; }

    /// <summary>The point's value.</summary>
    public double Value { get; }

    /// <summary>The value of the matching point of the previous period, when there is one.</summary>
    public double? PreviousValue { get; }
}

/// <summary>Arguments of <c>MetricsDashboard.ExportRequested</c>.</summary>
public class MetricsDashboardExportEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricsDashboardExportEventArgs(RoutedEvent routedEvent, object source, MetricsDashboardExportRequest request)
        : base(routedEvent, source)
    {
        Request = request;
    }

    /// <summary>What was asked for.</summary>
    public MetricsDashboardExportRequest Request { get; }
}
