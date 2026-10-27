using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Usage forecast: one billing cycle against a budget. Measured usage is drawn as a straight polyline up to today, the
/// forecast continues dashed with a cone of likely range, and a horizontal budget line can be dragged (or moved with the
/// keys) while everything reacts: the headline says when the budget will be reached, the three tiles show usage so far, the
/// forecast for the last day and the overage cost, the part of every path above the budget turns red, a marker follows the
/// crossing along the line, and alert chips (50, 80, 100% of the budget) report when each threshold was or will be reached.
/// A switch changes the metric; every path, tick, number and word morphs to the new one.
/// <code>
/// &lt;arc:UsageForecast Title="Usage outlook" Subtitle="Workspace · Growth plan" PeriodStart="2027-06-01" PeriodDays="30"&gt;
///     &lt;arc:UsageMetric Key="events" Label="Events" Readings="{Binding Events}" StartValue="0" Budget="26" Unit="M" /&gt;
/// &lt;/arc:UsageForecast&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys: the chart is a slider over the days (←/→ or ↓/↑ ±1 day, PageUp / PageDown ±7, Home / End the first and last day, Escape
/// hides the card); the budget band is a slider over the budget (↑/→ one step up, ↓/← one step down, Page keys five steps,
/// Home / End the limits); the alert chips are toggle buttons.
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(MetricSwitchPart, typeof(SegmentedControl))]
[TemplatePart(HeadlinePart, typeof(Control))]
[TemplatePart(DetailPart, typeof(Control))]
[TemplatePart(TilesPart, typeof(Panel))]
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(HandlePart, typeof(Control))]
[TemplatePart(AlertsPart, typeof(Panel))]
[TemplatePart(LegendPart, typeof(Control))]
[TemplatePart(TablePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, OverBudgetPseudoClass, DraggingPseudoClass, FramedPseudoClass)]
public partial class UsageForecast : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string MetricSwitchPart = "PART_MetricSwitch";
    public const string HeadlinePart = "PART_Headline";
    public const string DetailPart = "PART_Detail";
    public const string TilesPart = "PART_Tiles";
    public const string UsedPart = "PART_Used";
    public const string ForecastPart = "PART_Forecast";
    public const string CostPart = "PART_Cost";
    public const string PlotPart = "PART_Plot";
    public const string HandlePart = "PART_Handle";
    public const string AlertsPart = "PART_Alerts";
    public const string AlertLabelPart = "PART_AlertLabel";
    public const string LegendPart = "PART_Legend";
    public const string TablePart = "PART_Table";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block is narrower than 560: the switch goes under the titles, the chart is shorter.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is narrower than 440: the tiles stack and the chips fill their rows.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the forecast ends above the budget.</summary>
    public const string OverBudgetPseudoClass = ":over-budget";

    /// <summary>Set while the budget line is dragged.</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while <see cref="IsFramed"/> is on.</summary>
    public const string FramedPseudoClass = ":framed";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<UsageForecast, string?>(nameof(Title));

    /// <summary>The line under the heading (workspace, plan, renewal); null leaves it out.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<UsageForecast, string?>(nameof(Subtitle));

    /// <summary>The metrics as a list you own (followed when it raises collection changes); it wins over <see cref="Metrics"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MetricsSourceProperty =
        AvaloniaProperty.Register<UsageForecast, IEnumerable?>(nameof(MetricsSource));

    /// <summary>The metric shown (two-way); null shows the first one.</summary>
    public static readonly StyledProperty<UsageMetric?> SelectedMetricProperty =
        AvaloniaProperty.Register<UsageForecast, UsageMetric?>(nameof(SelectedMetric), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the metric shown (two-way).</summary>
    public static readonly StyledProperty<string?> SelectedMetricKeyProperty =
        AvaloniaProperty.Register<UsageForecast, string?>(nameof(SelectedMetricKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The first day of the billing cycle.</summary>
    public static readonly StyledProperty<DateOnly> PeriodStartProperty =
        AvaloniaProperty.Register<UsageForecast, DateOnly>(nameof(PeriodStart), new DateOnly(2000, 1, 1));

    /// <summary>The number of days in the cycle (default 30).</summary>
    public static readonly StyledProperty<int> PeriodDaysProperty =
        AvaloniaProperty.Register<UsageForecast, int>(nameof(PeriodDays), 30);

    /// <summary>Which day is today (1-based); 0 (default) takes the number of readings.</summary>
    public static readonly StyledProperty<int> TodayIndexProperty =
        AvaloniaProperty.Register<UsageForecast, int>(nameof(TodayIndex));

    /// <summary>The day under the crosshair (two-way, 1-based); null for none.</summary>
    public static readonly StyledProperty<int?> InspectedDayProperty =
        AvaloniaProperty.Register<UsageForecast, int?>(nameof(InspectedDay), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How dates are written (default "MMM d").</summary>
    public static readonly StyledProperty<string> DateFormatProperty =
        AvaloniaProperty.Register<UsageForecast, string>(nameof(DateFormat), "MMM d");

    /// <summary>Whether the legend shows (default true).</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<UsageForecast, bool>(nameof(IsLegendVisible), true);

    /// <summary>Whether the alert chips show (default true).</summary>
    public static readonly StyledProperty<bool> IsAlertsVisibleProperty =
        AvaloniaProperty.Register<UsageForecast, bool>(nameof(IsAlertsVisible), true);

    /// <summary>Whether the block has its own frame (border, radius 16, surface): the card version (default true).</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<UsageForecast, bool>(nameof(IsFramed), true);

    /// <summary>The words of the block; assign a new <see cref="UsageForecastLabels"/> to translate it.</summary>
    public static readonly StyledProperty<UsageForecastLabels> LabelsProperty =
        AvaloniaProperty.Register<UsageForecast, UsageForecastLabels>(nameof(Labels), new UsageForecastLabels());

    /// <summary>Run when the budget is settled (let go of, or moved with a key), with the new budget as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> BudgetCommittedCommandProperty =
        AvaloniaProperty.Register<UsageForecast, ICommand?>(nameof(BudgetCommittedCommand));

    /// <summary>How the shown metric stands against its budget (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, UsageOutcome> OutcomeProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, UsageOutcome>(nameof(Outcome), o => o.Outcome);

    /// <summary>The day, as a fraction, where the line reaches the budget; null when it does not (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, double?> CrossingDayProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, double?>(nameof(CrossingDay), o => o.CrossingDay);

    /// <summary>The earliest day the budget could be reached (top of the range); null when not (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, int?> CrossingEarlyProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, int?>(nameof(CrossingEarly), o => o.CrossingEarly);

    /// <summary>The latest day the budget could be reached (bottom of the range); null when not (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, int?> CrossingLateProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, int?>(nameof(CrossingLate), o => o.CrossingLate);

    /// <summary>The forecast for the last day (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, double> ProjectedTotalProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, double>(nameof(ProjectedTotal), o => o.ProjectedTotal);

    /// <summary>The low end of the range on the last day (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, double> ProjectedLowProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, double>(nameof(ProjectedLow), o => o.ProjectedLow);

    /// <summary>The high end of the range on the last day (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, double> ProjectedHighProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, double>(nameof(ProjectedHigh), o => o.ProjectedHigh);

    /// <summary>The cost of the forecast above the budget (read-only).</summary>
    public static readonly DirectProperty<UsageForecast, double> OverageCostProperty =
        AvaloniaProperty.RegisterDirect<UsageForecast, double>(nameof(OverageCost), o => o.OverageCost);

    /// <summary>Raised while the budget changes (every step of a drag) and when it is settled; see <see cref="UsageBudgetChangedEventArgs.IsCommitted"/>.</summary>
    public static readonly RoutedEvent<UsageBudgetChangedEventArgs> BudgetChangedEvent =
        RoutedEvent.Register<UsageForecast, UsageBudgetChangedEventArgs>(nameof(BudgetChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an alert chip is switched.</summary>
    public static readonly RoutedEvent<UsageAlertToggledEventArgs> AlertThresholdToggledEvent =
        RoutedEvent.Register<UsageForecast, UsageAlertToggledEventArgs>(nameof(AlertThresholdToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when another metric is shown.</summary>
    public static readonly RoutedEvent<UsageMetricChangedEventArgs> MetricChangedEvent =
        RoutedEvent.Register<UsageForecast, UsageMetricChangedEventArgs>(nameof(MetricChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the inspected day changes.</summary>
    public static readonly RoutedEvent<UsageInspectedDayChangedEventArgs> InspectedDayChangedEvent =
        RoutedEvent.Register<UsageForecast, UsageInspectedDayChangedEventArgs>(nameof(InspectedDayChanged), RoutingStrategies.Bubble);

    private UsageOutcome _outcome;
    private double? _crossingDay;
    private int? _crossingEarly;
    private int? _crossingLate;
    private double _projectedTotal;
    private double _projectedLow;
    private double _projectedHigh;
    private double _overageCost;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <summary>The metrics (the content of the block); <see cref="MetricsSource"/> wins when set.</summary>
    [Content]
    public AvaloniaList<UsageMetric> Metrics { get; } = new();

    /// <inheritdoc cref="MetricsSourceProperty"/>
    public IEnumerable? MetricsSource { get => GetValue(MetricsSourceProperty); set => SetValue(MetricsSourceProperty, value); }

    /// <inheritdoc cref="SelectedMetricProperty"/>
    public UsageMetric? SelectedMetric { get => GetValue(SelectedMetricProperty); set => SetValue(SelectedMetricProperty, value); }

    /// <inheritdoc cref="SelectedMetricKeyProperty"/>
    public string? SelectedMetricKey { get => GetValue(SelectedMetricKeyProperty); set => SetValue(SelectedMetricKeyProperty, value); }

    /// <inheritdoc cref="PeriodStartProperty"/>
    public DateOnly PeriodStart { get => GetValue(PeriodStartProperty); set => SetValue(PeriodStartProperty, value); }

    /// <inheritdoc cref="PeriodDaysProperty"/>
    public int PeriodDays { get => GetValue(PeriodDaysProperty); set => SetValue(PeriodDaysProperty, value); }

    /// <inheritdoc cref="TodayIndexProperty"/>
    public int TodayIndex { get => GetValue(TodayIndexProperty); set => SetValue(TodayIndexProperty, value); }

    /// <summary>The alert thresholds; when you add none, 50% (off), 80% and 100% (on) are used.</summary>
    public AvaloniaList<UsageAlertThreshold> AlertThresholds { get; } = new();

    /// <inheritdoc cref="InspectedDayProperty"/>
    public int? InspectedDay { get => GetValue(InspectedDayProperty); set => SetValue(InspectedDayProperty, value); }

    /// <inheritdoc cref="DateFormatProperty"/>
    public string DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible { get => GetValue(IsLegendVisibleProperty); set => SetValue(IsLegendVisibleProperty, value); }

    /// <inheritdoc cref="IsAlertsVisibleProperty"/>
    public bool IsAlertsVisible { get => GetValue(IsAlertsVisibleProperty); set => SetValue(IsAlertsVisibleProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public UsageForecastLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="BudgetCommittedCommandProperty"/>
    public ICommand? BudgetCommittedCommand { get => GetValue(BudgetCommittedCommandProperty); set => SetValue(BudgetCommittedCommandProperty, value); }

    /// <inheritdoc cref="OutcomeProperty"/>
    public UsageOutcome Outcome => _outcome;

    /// <inheritdoc cref="CrossingDayProperty"/>
    public double? CrossingDay => _crossingDay;

    /// <inheritdoc cref="CrossingEarlyProperty"/>
    public int? CrossingEarly => _crossingEarly;

    /// <inheritdoc cref="CrossingLateProperty"/>
    public int? CrossingLate => _crossingLate;

    /// <inheritdoc cref="ProjectedTotalProperty"/>
    public double ProjectedTotal => _projectedTotal;

    /// <inheritdoc cref="ProjectedLowProperty"/>
    public double ProjectedLow => _projectedLow;

    /// <inheritdoc cref="ProjectedHighProperty"/>
    public double ProjectedHigh => _projectedHigh;

    /// <inheritdoc cref="OverageCostProperty"/>
    public double OverageCost => _overageCost;

    /// <inheritdoc cref="BudgetChangedEvent"/>
    public event EventHandler<UsageBudgetChangedEventArgs>? BudgetChanged
    {
        add => AddHandler(BudgetChangedEvent, value);
        remove => RemoveHandler(BudgetChangedEvent, value);
    }

    /// <inheritdoc cref="AlertThresholdToggledEvent"/>
    public event EventHandler<UsageAlertToggledEventArgs>? AlertThresholdToggled
    {
        add => AddHandler(AlertThresholdToggledEvent, value);
        remove => RemoveHandler(AlertThresholdToggledEvent, value);
    }

    /// <inheritdoc cref="MetricChangedEvent"/>
    public event EventHandler<UsageMetricChangedEventArgs>? MetricChanged
    {
        add => AddHandler(MetricChangedEvent, value);
        remove => RemoveHandler(MetricChangedEvent, value);
    }

    /// <inheritdoc cref="InspectedDayChangedEvent"/>
    public event EventHandler<UsageInspectedDayChangedEventArgs>? InspectedDayChanged
    {
        add => AddHandler(InspectedDayChangedEvent, value);
        remove => RemoveHandler(InspectedDayChangedEvent, value);
    }

    #endregion
}
