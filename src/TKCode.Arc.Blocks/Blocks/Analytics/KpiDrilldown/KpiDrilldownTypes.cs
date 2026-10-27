using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a <see cref="KpiMetric"/>'s change between two periods is measured.</summary>
public enum KpiKind
{
    /// <summary>An amount (revenue, accounts): the change is relative, in percent ("+3.3%").</summary>
    Total,

    /// <summary>A rate already in percent (retention, churn): the change is the difference, in points ("+1.1 pts").</summary>
    Rate
}

/// <summary>How a <see cref="KpiMetric"/>'s values are written (cards, detail, axis, tooltip and breakdown).</summary>
public enum KpiValueFormat
{
    /// <summary>Grouped number ("3,186").</summary>
    Number,

    /// <summary>Compact with a unit ("184.3k", "2.4M").</summary>
    Compact,

    /// <summary>Grouped currency ("$64.8").</summary>
    Currency,

    /// <summary>Compact currency ("$184.3k").</summary>
    CurrencyCompact,

    /// <summary>A value in percent units ("27.9%").</summary>
    Percent
}

/// <summary>
/// One KPI of a <see cref="KpiDrilldown"/>: a daily series whose last value falls on <see cref="KpiDrilldown.EndDate"/>,
/// how to write it, whether a fall is good, a sentence for the detail view and its breakdowns per dimension. The series
/// should hold at least twice the longest range so the prior period exists.
/// </summary>
public sealed class KpiMetric : AvaloniaObject
{
    /// <summary>Stable key (<see cref="KpiDrilldown.SelectedKey"/> refers to it).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<KpiMetric, string>(nameof(Key), string.Empty);

    /// <summary>The name on the card and in the detail view.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KpiMetric, string>(nameof(Label), string.Empty);

    /// <summary>The sentence under the detail view's value (null leaves it out).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<KpiMetric, string?>(nameof(Description));

    /// <summary>Daily values, oldest first; the last one is the value on <see cref="KpiDrilldown.EndDate"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<KpiMetric, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>How the change is measured: relative percent (Total, default) or a difference in points (Rate).</summary>
    public static readonly StyledProperty<KpiKind> KindProperty =
        AvaloniaProperty.Register<KpiMetric, KpiKind>(nameof(Kind));

    /// <summary>A fall is good (churn, refunds, latency): the arrow still points down, the colour turns green.</summary>
    public static readonly StyledProperty<bool> IsLowerBetterProperty =
        AvaloniaProperty.Register<KpiMetric, bool>(nameof(IsLowerBetter));

    /// <summary>The built-in value format (Number by default).</summary>
    public static readonly StyledProperty<KpiValueFormat> FormatProperty =
        AvaloniaProperty.Register<KpiMetric, KpiValueFormat>(nameof(Format));

    /// <summary>Fraction digits of the value; −1 (default) uses the format's own (Number and Currency 0, Compact and Percent 1).</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<KpiMetric, int>(nameof(Decimals), -1);

    /// <summary>The symbol of the currency formats ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<KpiMetric, string>(nameof(CurrencySymbol), "$");

    /// <summary>Writes a value instead of <see cref="Format"/>; numbers it writes are replaced rather than rolled.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<KpiMetric, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Creates an empty metric.</summary>
    public KpiMetric()
    {
        Breakdowns.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates a metric with a key, a label and a series.</summary>
    public KpiMetric(string key, string label, IReadOnlyList<double>? values = null) : this()
    {
        Key = key;
        Label = label;
        Values = values;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public KpiKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="IsLowerBetterProperty"/>
    public bool IsLowerBetter { get => GetValue(IsLowerBetterProperty); set => SetValue(IsLowerBetterProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public KpiValueFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <summary>The metric split by dimension: one entry per <see cref="KpiDimension.Key"/>.</summary>
    [Content]
    public AvaloniaList<KpiBreakdown> Breakdowns { get; } = new();

    /// <summary>Raised when a property or a breakdown changes (the block re-reads the metric).</summary>
    internal event EventHandler? Changed;

    /// <summary>The breakdown for <paramref name="dimensionKey"/>, or null.</summary>
    public KpiBreakdown? BreakdownFor(string? dimensionKey)
    {
        foreach (var breakdown in Breakdowns)
        {
            if (string.Equals(breakdown.DimensionKey, dimensionKey, StringComparison.Ordinal))
            {
                return breakdown;
            }
        }
        return null;
    }

    /// <summary>The value format as the Analytics parts use it (rolling digits and text agree).</summary>
    internal AnalyticsNumberFormat NumberFormat
    {
        get
        {
            var d = Decimals;
            return Format switch
            {
                KpiValueFormat.Compact => AnalyticsNumberFormat.CompactFixed(d < 0 ? 1 : d),
                KpiValueFormat.Currency => AnalyticsNumberFormat.Currency(d < 0 ? 0 : d, CurrencySymbol),
                KpiValueFormat.CurrencyCompact => AnalyticsNumberFormat.CompactFixed(d < 0 ? 1 : d, CurrencySymbol),
                KpiValueFormat.Percent => AnalyticsNumberFormat.Percent(d < 0 ? 1 : d),
                _ => AnalyticsNumberFormat.Number(d < 0 ? 0 : d)
            };
        }
    }

    /// <summary>The change format: signed one-decimal percent (Total) or points (Rate), with a true minus.</summary>
    internal AnalyticsNumberFormat ChangeFormat =>
        Kind == KpiKind.Rate ? AnalyticsNumberFormat.Points(1, signed: true) : AnalyticsNumberFormat.Percent(1, signed: true);

    /// <summary>Writes a value with <see cref="ValueFormatter"/> or the format.</summary>
    public string FormatValue(double value) => ValueFormatter is { } formatter ? formatter(value) : NumberFormat.Format(value);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The rows of one metric split by one dimension (<see cref="KpiMetric.Breakdowns"/>).</summary>
public sealed class KpiBreakdown : AvaloniaObject
{
    /// <summary>The <see cref="KpiDimension.Key"/> these rows belong to.</summary>
    public static readonly StyledProperty<string> DimensionKeyProperty =
        AvaloniaProperty.Register<KpiBreakdown, string>(nameof(DimensionKey), string.Empty);

    /// <summary>Creates an empty breakdown.</summary>
    public KpiBreakdown()
    {
    }

    /// <summary>Creates a breakdown for <paramref name="dimensionKey"/> with <paramref name="rows"/>.</summary>
    public KpiBreakdown(string dimensionKey, IEnumerable<KpiBreakdownRow> rows)
    {
        DimensionKey = dimensionKey;
        Rows.AddRange(rows);
    }

    /// <inheritdoc cref="DimensionKeyProperty"/>
    public string DimensionKey { get => GetValue(DimensionKeyProperty); set => SetValue(DimensionKeyProperty, value); }

    /// <summary>The rows, in the order shown (largest first reads best).</summary>
    [Content]
    public AvaloniaList<KpiBreakdownRow> Rows { get; } = new();
}

/// <summary>
/// One row of a breakdown: a name and either a daily series (<see cref="Values"/>, read per range like the metric: the
/// value on the last day of each period) or two fixed numbers (<see cref="Value"/> and <see cref="PreviousValue"/>).
/// </summary>
public sealed class KpiBreakdownRow : AvaloniaObject
{
    /// <summary>The row's name ("Enterprise", "Europe").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KpiBreakdownRow, string>(nameof(Label), string.Empty);

    /// <summary>Daily values aligned with the metric's series (last = end date); wins over the fixed numbers.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<KpiBreakdownRow, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>The value in the current period when there is no series.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<KpiBreakdownRow, double>(nameof(Value));

    /// <summary>The value in the prior period when there is no series (NaN hides the change).</summary>
    public static readonly StyledProperty<double> PreviousValueProperty =
        AvaloniaProperty.Register<KpiBreakdownRow, double>(nameof(PreviousValue), double.NaN);

    /// <summary>Creates an empty row.</summary>
    public KpiBreakdownRow()
    {
    }

    /// <summary>Creates a row with a series.</summary>
    public KpiBreakdownRow(string label, IReadOnlyList<double> values)
    {
        Label = label;
        Values = values;
    }

    /// <summary>Creates a row with fixed numbers.</summary>
    public KpiBreakdownRow(string label, double value, double previousValue)
    {
        Label = label;
        Value = value;
        PreviousValue = previousValue;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="PreviousValueProperty"/>
    public double PreviousValue { get => GetValue(PreviousValueProperty); set => SetValue(PreviousValueProperty, value); }
}

/// <summary>A dimension the detail view can split a metric by (the breakdown switch).</summary>
public sealed class KpiDimension : AvaloniaObject
{
    /// <summary>Stable key (matches <see cref="KpiBreakdown.DimensionKey"/>).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<KpiDimension, string>(nameof(Key), string.Empty);

    /// <summary>The switch text and the table's first column heading ("Plan").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KpiDimension, string>(nameof(Label), string.Empty);

    /// <summary>Creates an empty dimension.</summary>
    public KpiDimension()
    {
    }

    /// <summary>Creates a dimension.</summary>
    public KpiDimension(string key, string label)
    {
        Key = key;
        Label = label;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}

/// <summary>Arguments of <see cref="KpiDrilldown.SelectedChanged"/> and <see cref="KpiDrilldown.DimensionChanged"/>.</summary>
public class KpiSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KpiSelectionChangedEventArgs(RoutedEvent routedEvent, string? oldKey, string? newKey) : base(routedEvent)
    {
        OldKey = oldKey;
        NewKey = newKey;
    }

    /// <summary>The key before (null: the grid / no dimension).</summary>
    public string? OldKey { get; }

    /// <summary>The key now (null: back to the grid).</summary>
    public string? NewKey { get; }
}

/// <summary>Arguments of <see cref="KpiDrilldown.RangeChanged"/>.</summary>
public class KpiRangeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KpiRangeChangedEventArgs(RoutedEvent routedEvent, int oldDays, int newDays) : base(routedEvent)
    {
        OldDays = oldDays;
        NewDays = newDays;
    }

    /// <summary>The range before, in days.</summary>
    public int OldDays { get; }

    /// <summary>The range now, in days.</summary>
    public int NewDays { get; }
}

/// <summary>Arguments of <see cref="KpiDrilldown.CompareChanged"/>.</summary>
public class KpiCompareChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KpiCompareChangedEventArgs(RoutedEvent routedEvent, bool isEnabled) : base(routedEvent)
    {
        IsEnabled = isEnabled;
    }

    /// <summary>True when the prior period is now shown.</summary>
    public bool IsEnabled { get; }
}
