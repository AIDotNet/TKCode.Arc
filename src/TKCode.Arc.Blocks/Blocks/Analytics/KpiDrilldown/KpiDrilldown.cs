using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// An overview of KPI cards that open in place: each card shows a metric's value on the last day, its change against the
/// prior period and a sparkline; choosing a card grows it into a detail view (a trend chart with the prior period dashed
/// behind it, a readout under the pointer or keyboard cursor, a sentence about the metric and a breakdown table by
/// dimension) and Escape folds it back. A range switch (7 / 30 / 90 days) morphs every line and rolls every number; the
/// Compare toggle shows or hides the prior period.
/// <para>The block contains no data: give it <see cref="Metrics"/> (each a daily series ending on <see cref="EndDate"/>,
/// at least twice the longest range long) and, for the breakdown, <see cref="Dimensions"/> with matching
/// <see cref="KpiMetric.Breakdowns"/>.</para>
/// <code>
/// &lt;arc:KpiDrilldown Title="Overview" RangeDays="30"&gt;
///     &lt;arc:KpiDrilldown.Dimensions&gt;
///         &lt;arc:KpiDimension Key="tier" Label="Tier" /&gt;
///     &lt;/arc:KpiDrilldown.Dimensions&gt;
///     &lt;arc:KpiMetric Key="arr" Label="Annual run rate" Format="CurrencyCompact" Values="{Binding ArrSeries}" /&gt;
/// &lt;/arc:KpiDrilldown&gt;
/// </code>
/// <para>Keyboard: Tab reaches Compare, the range switch and each card; Enter or Space opens a card and moves focus to
/// the close button; in the detail view Tab reaches the close button, the chart (← / → move the cursor by a day, Home and
/// End jump to the ends) and the dimension switch. Escape closes the detail view from anywhere on the page and returns
/// focus to the card.</para>
/// </summary>
[TemplatePart(HeaderPart, typeof(Avalonia.Controls.Grid))]
[TemplatePart(CompareButtonPart, typeof(KpiCompareButton))]
[TemplatePart(RangeSwitchPart, typeof(AnalyticsSegmentSwitch))]
[TemplatePart(StagePart, typeof(KpiDrilldownStage))]
[TemplatePart(GridPart, typeof(KpiCardGrid))]
[TemplatePart(ChartPart, typeof(KpiTrendChart))]
[TemplatePart(TablePart, typeof(KpiBreakdownTable))]
[PseudoClasses(OpenPseudoClass, CompactPseudoClass, TwoColumnPseudoClass)]
public partial class KpiDrilldown : TemplatedControl
{
    internal const string HeaderPart = "PART_Header";
    internal const string TitlePart = "PART_Title";
    internal const string CompareButtonPart = "PART_Compare";
    internal const string RangeSwitchPart = "PART_Ranges";
    internal const string StagePart = "PART_Stage";
    internal const string GridPart = "PART_Grid";
    internal const string VeilPart = "PART_Veil";
    internal const string FramePart = "PART_Frame";
    internal const string DetailPart = "PART_Detail";
    internal const string DetailTitlePart = "PART_DetailTitle";
    internal const string CloseButtonPart = "PART_Close";
    internal const string DetailValuePart = "PART_DetailValue";
    internal const string DetailPlainValuePart = "PART_DetailPlainValue";
    internal const string DetailDeltaPart = "PART_DetailDelta";
    internal const string VersusPart = "PART_Versus";
    internal const string VersusBeforePart = "PART_VersusBefore";
    internal const string VersusValuePart = "PART_VersusValue";
    internal const string VersusAfterPart = "PART_VersusAfter";
    internal const string BodyPart = "PART_Body";
    internal const string DescriptionPart = "PART_Description";
    internal const string ChartPart = "PART_Chart";
    internal const string LegendPart = "PART_Legend";
    internal const string LegendCurrentPart = "PART_LegendCurrent";
    internal const string LegendPriorItemPart = "PART_LegendPriorItem";
    internal const string LegendPriorPart = "PART_LegendPrior";
    internal const string BreakdownPart = "PART_Breakdown";
    internal const string BreakdownTitlePart = "PART_BreakdownTitle";
    internal const string DimensionSwitchPart = "PART_Dimensions";
    internal const string TablePart = "PART_Table";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string OpenPseudoClass = ":open";
    internal const string CompactPseudoClass = ":compact";
    internal const string TwoColumnPseudoClass = ":two-column";

    #region Properties

    /// <summary>The metrics, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Metrics"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MetricsSourceProperty =
        AvaloniaProperty.Register<KpiDrilldown, IEnumerable?>(nameof(MetricsSource));

    /// <summary>The heading at the top left.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<KpiDrilldown, string?>(nameof(Title), "Overview");

    /// <summary>The date of every series' last value; null uses today.</summary>
    public static readonly StyledProperty<DateOnly?> EndDateProperty =
        AvaloniaProperty.Register<KpiDrilldown, DateOnly?>(nameof(EndDate));

    /// <summary>The ranges the switch offers, in days (7, 30, 90).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>> RangesProperty =
        AvaloniaProperty.Register<KpiDrilldown, IReadOnlyList<int>>(nameof(Ranges), [7, 30, 90]);

    /// <summary>The range shown, in days (two-way); a value that is not in <see cref="Ranges"/> still works but selects no segment.</summary>
    public static readonly StyledProperty<int> RangeDaysProperty =
        AvaloniaProperty.Register<KpiDrilldown, int>(nameof(RangeDays), 30, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The prior period is drawn behind the current one and listed in the readout (two-way).</summary>
    public static readonly StyledProperty<bool> IsCompareEnabledProperty =
        AvaloniaProperty.Register<KpiDrilldown, bool>(nameof(IsCompareEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the metric whose detail view is open (two-way); null shows the cards.</summary>
    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<KpiDrilldown, string?>(nameof(SelectedKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the breakdown dimension (two-way); null or an unknown key uses the first.</summary>
    public static readonly StyledProperty<string?> SelectedDimensionKeyProperty =
        AvaloniaProperty.Register<KpiDrilldown, string?>(nameof(SelectedDimensionKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The date pattern of the axis and the readout ("MMM d").</summary>
    public static readonly StyledProperty<string> DateFormatProperty =
        AvaloniaProperty.Register<KpiDrilldown, string>(nameof(DateFormat), "MMM d");

    /// <summary>Every word the block writes or speaks besides the data.</summary>
    public static readonly StyledProperty<KpiDrilldownLabels> LabelsProperty =
        AvaloniaProperty.Register<KpiDrilldown, KpiDrilldownLabels>(nameof(Labels), new KpiDrilldownLabels());

    /// <summary>Run when a detail view opens (parameter: the metric) or closes (parameter: null), after <see cref="SelectedChanged"/>.</summary>
    public static readonly StyledProperty<ICommand?> SelectedCommandProperty =
        AvaloniaProperty.Register<KpiDrilldown, ICommand?>(nameof(SelectedCommand));

    /// <summary>The metric whose detail view is open (resolved from <see cref="SelectedKey"/>).</summary>
    public static readonly DirectProperty<KpiDrilldown, KpiMetric?> SelectedMetricProperty =
        AvaloniaProperty.RegisterDirect<KpiDrilldown, KpiMetric?>(nameof(SelectedMetric), o => o.SelectedMetric);

    /// <summary>True while a detail view is open or folding back.</summary>
    public static readonly DirectProperty<KpiDrilldown, bool> IsDetailOpenProperty =
        AvaloniaProperty.RegisterDirect<KpiDrilldown, bool>(nameof(IsDetailOpen), o => o.IsDetailOpen);

    #endregion

    #region Events

    /// <summary>Raised when a detail view starts to open (new key) or to close (new key null).</summary>
    public static readonly RoutedEvent<KpiSelectionChangedEventArgs> SelectedChangedEvent =
        RoutedEvent.Register<KpiDrilldown, KpiSelectionChangedEventArgs>(nameof(SelectedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the range changes (by the switch or by code).</summary>
    public static readonly RoutedEvent<KpiRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<KpiDrilldown, KpiRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when Compare is turned on or off.</summary>
    public static readonly RoutedEvent<KpiCompareChangedEventArgs> CompareChangedEvent =
        RoutedEvent.Register<KpiDrilldown, KpiCompareChangedEventArgs>(nameof(CompareChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the breakdown dimension changes.</summary>
    public static readonly RoutedEvent<KpiSelectionChangedEventArgs> DimensionChangedEvent =
        RoutedEvent.Register<KpiDrilldown, KpiSelectionChangedEventArgs>(nameof(DimensionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a detail view starts to open (new key) or to close (new key null).</summary>
    public event EventHandler<KpiSelectionChangedEventArgs>? SelectedChanged
    {
        add => AddHandler(SelectedChangedEvent, value);
        remove => RemoveHandler(SelectedChangedEvent, value);
    }

    /// <summary>Raised when the range changes (by the switch or by code).</summary>
    public event EventHandler<KpiRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    /// <summary>Raised when Compare is turned on or off.</summary>
    public event EventHandler<KpiCompareChangedEventArgs>? CompareChanged
    {
        add => AddHandler(CompareChangedEvent, value);
        remove => RemoveHandler(CompareChangedEvent, value);
    }

    /// <summary>Raised when the breakdown dimension changes.</summary>
    public event EventHandler<KpiSelectionChangedEventArgs>? DimensionChanged
    {
        add => AddHandler(DimensionChangedEvent, value);
        remove => RemoveHandler(DimensionChangedEvent, value);
    }

    #endregion

    /// <summary>The detail value's font size (28; compact 22), set by the theme.</summary>
    internal static readonly StyledProperty<double> DetailValueSizeProperty =
        AvaloniaProperty.Register<KpiDrilldown, double>(nameof(DetailValueSize), 28);

    private KpiMetric? _selectedMetric;
    private bool _isDetailOpen;

    /// <summary>The metrics, one card each in this order (six fill two rows of three), when no <see cref="MetricsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<KpiMetric> Metrics { get; } = new();

    /// <summary>The dimensions of the breakdown switch; none hides the breakdown.</summary>
    public AvaloniaList<KpiDimension> Dimensions { get; } = new();

    /// <inheritdoc cref="MetricsSourceProperty"/>
    public IEnumerable? MetricsSource { get => GetValue(MetricsSourceProperty); set => SetValue(MetricsSourceProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="EndDateProperty"/>
    public DateOnly? EndDate { get => GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }

    /// <inheritdoc cref="RangesProperty"/>
    public IReadOnlyList<int> Ranges { get => GetValue(RangesProperty); set => SetValue(RangesProperty, value); }

    /// <inheritdoc cref="RangeDaysProperty"/>
    public int RangeDays { get => GetValue(RangeDaysProperty); set => SetValue(RangeDaysProperty, value); }

    /// <inheritdoc cref="IsCompareEnabledProperty"/>
    public bool IsCompareEnabled { get => GetValue(IsCompareEnabledProperty); set => SetValue(IsCompareEnabledProperty, value); }

    /// <inheritdoc cref="SelectedKeyProperty"/>
    public string? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }

    /// <inheritdoc cref="SelectedDimensionKeyProperty"/>
    public string? SelectedDimensionKey { get => GetValue(SelectedDimensionKeyProperty); set => SetValue(SelectedDimensionKeyProperty, value); }

    /// <inheritdoc cref="DateFormatProperty"/>
    public string DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public KpiDrilldownLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SelectedCommandProperty"/>
    public ICommand? SelectedCommand { get => GetValue(SelectedCommandProperty); set => SetValue(SelectedCommandProperty, value); }

    /// <inheritdoc cref="SelectedMetricProperty"/>
    public KpiMetric? SelectedMetric { get => _selectedMetric; private set => SetAndRaise(SelectedMetricProperty, ref _selectedMetric, value); }

    internal double DetailValueSize { get => GetValue(DetailValueSizeProperty); set => SetValue(DetailValueSizeProperty, value); }

    /// <inheritdoc cref="IsDetailOpenProperty"/>
    public bool IsDetailOpen { get => _isDetailOpen; private set => SetAndRaise(IsDetailOpenProperty, ref _isDetailOpen, value); }
}
