using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How an <see cref="ExplorerMetric"/>'s values are written (cards, tabs, readout, y axis, announcements).</summary>
public enum ExplorerValueFormat
{
    /// <summary>A count with thousands grouping: "5,874"; axis labels are whole numbers.</summary>
    Count,

    /// <summary>Money written in full: "$12,480"; axis labels are whole amounts.</summary>
    Currency,

    /// <summary>Money in compact form with two decimals: "$4.62M", "$412.40K"; axis labels keep the two decimals.</summary>
    CurrencyCompact,

    /// <summary>A value in percent units: 27.3 is "27.3%"; axis labels show as many decimals as their step needs.</summary>
    Percent,

    /// <summary>A multiple with two decimals: 1.12 is "1.12×".</summary>
    Ratio,
}

/// <summary>How a change between two days is expressed.</summary>
public enum ExplorerChangeMode
{
    /// <summary>Relative to the earlier day: "+1.6%".</summary>
    Relative,

    /// <summary>The difference in the value's own units, for rates: "+1.2 pts".</summary>
    Points,
}

/// <summary>
/// One metric of a <see cref="MetricExplorer"/>: a card in the row, a tab in the open chart. <see cref="Values"/> is a daily
/// series that ends on <see cref="EndDate"/>; the chart shows the last days of the selected range against the same number
/// of days before them, so the series needs at least twice the longest range plus one day.
/// </summary>
public sealed class ExplorerMetric : AvaloniaObject
{
    /// <summary>A stable key (events, automation ids); optional.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ExplorerMetric, string?>(nameof(Key));

    /// <summary>The card's label and the readout's heading ("Weekly active teams").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ExplorerMetric, string>(nameof(Title), string.Empty);

    /// <summary>The tab's short name ("Teams"); null uses <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> ShortTitleProperty =
        AvaloniaProperty.Register<ExplorerMetric, string?>(nameof(ShortTitle));

    /// <summary>One sentence under the chart saying what the metric counts; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ExplorerMetric, string?>(nameof(Description));

    /// <summary>The daily values, oldest first; the last one belongs to <see cref="EndDate"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<ExplorerMetric, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>The day of the last value; null means today.</summary>
    public static readonly StyledProperty<DateTime?> EndDateProperty =
        AvaloniaProperty.Register<ExplorerMetric, DateTime?>(nameof(EndDate));

    /// <summary>How values are written (default <see cref="ExplorerValueFormat.Count"/>).</summary>
    public static readonly StyledProperty<ExplorerValueFormat> ValueFormatProperty =
        AvaloniaProperty.Register<ExplorerMetric, ExplorerValueFormat>(nameof(ValueFormat));

    /// <summary>The currency symbol of the currency formats ("$", "€").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ExplorerMetric, string>(nameof(CurrencySymbol), "$");

    /// <summary>Writes a value (cards, tabs, readout, announcements); null uses <see cref="ValueFormat"/>.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<ExplorerMetric, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Writes a y axis label from the tick value and the step between ticks; null uses <see cref="ValueFormat"/>.</summary>
    public static readonly StyledProperty<Func<double, double, string>?> AxisFormatterProperty =
        AvaloniaProperty.Register<ExplorerMetric, Func<double, double, string>?>(nameof(AxisFormatter));

    /// <summary>Whether changes are relative ("+1.6%", default) or differences in points ("+1.2 pts", for rates).</summary>
    public static readonly StyledProperty<ExplorerChangeMode> ChangeModeProperty =
        AvaloniaProperty.Register<ExplorerMetric, ExplorerChangeMode>(nameof(ChangeMode));

    /// <summary>Whether a rise is good (green). False for costs and churn: a rise turns red, a fall green.</summary>
    public static readonly StyledProperty<bool> IsHigherBetterProperty =
        AvaloniaProperty.Register<ExplorerMetric, bool>(nameof(IsHigherBetter), true);

    /// <summary>
    /// The most decimals a y axis step may have, in the units the labels show (millions for "$1.22M"); −1 (default) takes the
    /// format's: 2 for compact money and ratios, 1 for percentages, 0 for counts and full money.
    /// </summary>
    public static readonly StyledProperty<int> MaxAxisDecimalsProperty =
        AvaloniaProperty.Register<ExplorerMetric, int>(nameof(MaxAxisDecimals), -1);

    /// <summary>Creates an empty metric (XAML).</summary>
    public ExplorerMetric()
    {
    }

    /// <summary>Creates a metric.</summary>
    /// <param name="title">The card's label and the readout's heading.</param>
    /// <param name="values">The daily values, oldest first.</param>
    /// <param name="format">How values are written.</param>
    public ExplorerMetric(string title, IReadOnlyList<double> values, ExplorerValueFormat format = ExplorerValueFormat.Count)
    {
        Title = title;
        Values = values;
        ValueFormat = format;
    }

    /// <summary>Raised when anything that changes what the block shows changes.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ShortTitleProperty"/>
    public string? ShortTitle { get => GetValue(ShortTitleProperty); set => SetValue(ShortTitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="EndDateProperty"/>
    public DateTime? EndDate { get => GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }

    /// <inheritdoc cref="ValueFormatProperty"/>
    public ExplorerValueFormat ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="AxisFormatterProperty"/>
    public Func<double, double, string>? AxisFormatter { get => GetValue(AxisFormatterProperty); set => SetValue(AxisFormatterProperty, value); }

    /// <inheritdoc cref="ChangeModeProperty"/>
    public ExplorerChangeMode ChangeMode { get => GetValue(ChangeModeProperty); set => SetValue(ChangeModeProperty, value); }

    /// <inheritdoc cref="IsHigherBetterProperty"/>
    public bool IsHigherBetter { get => GetValue(IsHigherBetterProperty); set => SetValue(IsHigherBetterProperty, value); }

    /// <inheritdoc cref="MaxAxisDecimalsProperty"/>
    public int MaxAxisDecimals { get => GetValue(MaxAxisDecimalsProperty); set => SetValue(MaxAxisDecimalsProperty, value); }

    /// <summary>The tab's text: <see cref="ShortTitle"/> or <see cref="Title"/>.</summary>
    public string TabTitle => string.IsNullOrEmpty(ShortTitle) ? Title : ShortTitle!;

    /// <summary>The day of the last value (<see cref="EndDate"/> or today).</summary>
    public DateTime EffectiveEndDate => (EndDate ?? DateTime.Today).Date;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One window of the range switch: how many days the chart shows (against the same number of days before them), the
/// button text, and the words the cards and the readout use for it.
/// </summary>
public sealed class ExplorerRange : AvaloniaObject
{
    /// <summary>The switch button's text ("30D").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ExplorerRange, string>(nameof(Label), string.Empty);

    /// <summary>The number of days shown (30, 90, 364); at least 2.</summary>
    public static readonly StyledProperty<int> DaysProperty =
        AvaloniaProperty.Register<ExplorerRange, int>(nameof(Days), 30);

    /// <summary>The span written on the cards ("30 days", "12 months").</summary>
    public static readonly StyledProperty<string> CardTextProperty =
        AvaloniaProperty.Register<ExplorerRange, string>(nameof(CardText), string.Empty);

    /// <summary>The comparison after the readout's change ("vs 30 days ago").</summary>
    public static readonly StyledProperty<string> CompareTextProperty =
        AvaloniaProperty.Register<ExplorerRange, string>(nameof(CompareText), string.Empty);

    /// <summary>The spoken name of the button ("Last 30 days"); null uses <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<ExplorerRange, string?>(nameof(AccessibleName));

    /// <summary>Days between the card sparkline's points (1 = daily; 7 = weekly, used by the 12-month range).</summary>
    public static readonly StyledProperty<int> SparklineStepProperty =
        AvaloniaProperty.Register<ExplorerRange, int>(nameof(SparklineStep), 1);

    /// <summary>Creates an empty range (XAML).</summary>
    public ExplorerRange()
    {
    }

    /// <summary>Creates a range.</summary>
    public ExplorerRange(string label, int days, string cardText, string compareText, int sparklineStep = 1)
    {
        Label = label;
        Days = days;
        CardText = cardText;
        CompareText = compareText;
        SparklineStep = sparklineStep;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DaysProperty"/>
    public int Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }

    /// <inheritdoc cref="CardTextProperty"/>
    public string CardText { get => GetValue(CardTextProperty); set => SetValue(CardTextProperty, value); }

    /// <inheritdoc cref="CompareTextProperty"/>
    public string CompareText { get => GetValue(CompareTextProperty); set => SetValue(CompareTextProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="SparklineStepProperty"/>
    public int SparklineStep { get => GetValue(SparklineStepProperty); set => SetValue(SparklineStepProperty, value); }

    /// <summary>The three default ranges: 30 days, 90 days and 12 months (364 days, weekly sparkline).</summary>
    public static ExplorerRange[] CreateDefaults() =>
    [
        new("30D", 30, "30 days", "vs 30 days ago") { AccessibleName = "Last 30 days" },
        new("90D", 90, "90 days", "vs 90 days ago") { AccessibleName = "Last 90 days" },
        new("12M", 364, "12 months", "vs 12 months ago", 7) { AccessibleName = "Last 12 months" },
    ];

    internal event EventHandler? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Arguments of <see cref="MetricExplorer.MetricOpened"/>, <see cref="MetricExplorer.MetricClosed"/> and <see cref="MetricExplorer.SelectedMetricChanged"/>.</summary>
public sealed class MetricExplorerMetricEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricExplorerMetricEventArgs(RoutedEvent routedEvent, int index, ExplorerMetric? metric) : base(routedEvent)
    {
        Index = index;
        Metric = metric;
    }

    /// <summary>The metric's index in the effective list (−1 for none).</summary>
    public int Index { get; }

    /// <summary>The metric.</summary>
    public ExplorerMetric? Metric { get; }
}

/// <summary>Arguments of <see cref="MetricExplorer.SelectedRangeChanged"/>.</summary>
public sealed class MetricExplorerRangeEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricExplorerRangeEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex, ExplorerRange? range) : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Range = range;
    }

    /// <summary>The range before.</summary>
    public int OldIndex { get; }

    /// <summary>The range now.</summary>
    public int NewIndex { get; }

    /// <summary>The range now.</summary>
    public ExplorerRange? Range { get; }
}

/// <summary>Arguments of <see cref="MetricExplorer.ScrubChanged"/>: the inspected day, or none.</summary>
public sealed class MetricExplorerScrubEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MetricExplorerScrubEventArgs(RoutedEvent routedEvent, int? index, DateTime? date, double? value, DateTime? previousDate,
        double? previousValue) : base(routedEvent)
    {
        Index = index;
        Date = date;
        Value = value;
        PreviousDate = previousDate;
        PreviousValue = previousValue;
    }

    /// <summary>The day's index within the range (0 = first day shown); null when the scrub was cleared.</summary>
    public int? Index { get; }

    /// <summary>The inspected day.</summary>
    public DateTime? Date { get; }

    /// <summary>The metric's value that day.</summary>
    public double? Value { get; }

    /// <summary>The matching day of the previous period.</summary>
    public DateTime? PreviousDate { get; }

    /// <summary>The metric's value on <see cref="PreviousDate"/>.</summary>
    public double? PreviousValue { get; }
}
