using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A status page: the overall state of a set of services at the top, each service's last 90 days as one bar per day with its
/// uptime, the response time of the chosen service over the last day (p50 and p95 against a target) and the incident
/// timeline. The page is derived from the data: give it <see cref="Services"/> (each with half-hourly
/// <see cref="UptimeService.Latency"/> samples and a target) and <see cref="Incidents"/> (each naming the services it hit,
/// when it began and how long it lasted); every day's bar, every uptime figure, the chart domain and the counts follow.
/// <para>Pointing at a day's bar (or moving the keyboard cursor over it) dims the others and opens a card with the day's uptime,
/// status and incidents; choosing a service (its name, or Enter on its bars) glides the highlight to it and morphs the chart.
/// Changing the data (a new incident, a resolution) fades the bars, rolls the numbers, swaps the header and grows or removes
/// incident entries.</para>
/// <code>
/// &lt;arc:UptimeStatus PageName="Larkspur status" UpdatedAt="2027-03-14T10:05Z"&gt;
///     &lt;arc:UptimeStatus.Services&gt;
///         &lt;arc:UptimeService Key="edge" Name="Edge" Description="Routing and TLS" TargetP95="350" /&gt;
///     &lt;/arc:UptimeStatus.Services&gt;
/// &lt;/arc:UptimeStatus&gt;
/// </code>
/// <para>Keyboard: Tab reaches the header action, then each service's name button and its bars, then the chart and each incident.
/// A focused bar band inspects the newest day; ← → move by a day, PageUp and PageDown by seven, Home and End jump to the ends,
/// Escape clears and Enter or Space charts the service. The focused chart moves a cursor by half hour with ← → (the first →
/// shows the newest sample), Home and End, and Escape clears it.</para>
/// </summary>
[TemplatePart(HeaderPart, typeof(Grid))]
[TemplatePart(IconPart, typeof(UptimeStatusIcon))]
[TemplatePart(ServiceListPart, typeof(UptimeServiceList))]
[TemplatePart(TipPart, typeof(UptimeTipLayer))]
[TemplatePart(ChartPart, typeof(UptimeLatencyChart))]
[TemplatePart(IncidentListPart, typeof(UptimeIncidentList))]
[PseudoClasses(OperationalPseudoClass, DegradedPseudoClass, OutagePseudoClass, StackedPseudoClass, CompactPseudoClass, EmptyPseudoClass)]
public partial class UptimeStatus : TemplatedControl
{
    internal const string HeaderPart = "PART_Header";
    internal const string IconPart = "PART_Icon";
    internal const string TitlePart = "PART_Title";
    internal const string PageNamePart = "PART_PageName";
    internal const string SeparatorPart = "PART_Separator";
    internal const string SummaryPart = "PART_Summary";
    internal const string ActionPart = "PART_Action";
    internal const string UptimeFramePart = "PART_UptimeFrame";
    internal const string UptimeHeadingPart = "PART_UptimeHeading";
    internal const string UptimeHintPart = "PART_UptimeHint";
    internal const string LegendPart = "PART_Legend";
    internal const string ServiceListPart = "PART_Services";
    internal const string StartDatePart = "PART_StartDate";
    internal const string TodayPart = "PART_Today";
    internal const string TipPart = "PART_Tip";
    internal const string LowerPart = "PART_Lower";
    internal const string ResponseFramePart = "PART_ResponseFrame";
    internal const string ResponseHeadingPart = "PART_ResponseHeading";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string LatestPart = "PART_Latest";
    internal const string P95NamePart = "PART_P95Name";
    internal const string P50NamePart = "PART_P50Name";
    internal const string TargetNamePart = "PART_TargetName";
    internal const string P95ValuePart = "PART_P95Value";
    internal const string P50ValuePart = "PART_P50Value";
    internal const string TargetValuePart = "PART_TargetValue";
    internal const string UnitPart = "PART_Unit";
    internal const string ChartPart = "PART_Chart";
    internal const string TablePart = "PART_Table";
    internal const string IncidentsFramePart = "PART_IncidentsFrame";
    internal const string IncidentsHeadingPart = "PART_IncidentsHeading";
    internal const string CountBeforePart = "PART_CountBefore";
    internal const string CountValuePart = "PART_CountValue";
    internal const string CountAfterPart = "PART_CountAfter";
    internal const string IncidentListPart = "PART_Incidents";
    internal const string NoIncidentsPart = "PART_NoIncidents";
    internal const string EmptyPart = "PART_Empty";
    internal const string StatusAnnouncerPart = "PART_StatusAnnouncer";
    internal const string InspectAnnouncerPart = "PART_InspectAnnouncer";

    internal const string OperationalPseudoClass = ":operational";
    internal const string DegradedPseudoClass = ":degraded";
    internal const string OutagePseudoClass = ":outage";
    internal const string StackedPseudoClass = ":stacked";
    internal const string CompactPseudoClass = ":compact";
    internal const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The services, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Services"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ServicesSourceProperty =
        AvaloniaProperty.Register<UptimeStatus, IEnumerable?>(nameof(ServicesSource));

    /// <summary>The incidents, when you give them as a list to follow; it wins over <see cref="Incidents"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> IncidentsSourceProperty =
        AvaloniaProperty.Register<UptimeStatus, IEnumerable?>(nameof(IncidentsSource));

    /// <summary>The last day of the window (UTC); null uses the day of the newest latency sample.</summary>
    public static readonly StyledProperty<DateTimeOffset?> TodayProperty =
        AvaloniaProperty.Register<UptimeStatus, DateTimeOffset?>(nameof(Today));

    /// <summary>The number of day bars (90).</summary>
    public static readonly StyledProperty<int> DayCountProperty =
        AvaloniaProperty.Register<UptimeStatus, int>(nameof(DayCount), 90, coerce: (_, v) => Math.Clamp(v, 7, 365));

    /// <summary>The name of the status page, the left part of the line under the title.</summary>
    public static readonly StyledProperty<string?> PageNameProperty =
        AvaloniaProperty.Register<UptimeStatus, string?>(nameof(PageName));

    /// <summary>When the page was last updated: the right part of the line under the title while no summary is set.</summary>
    public static readonly StyledProperty<DateTimeOffset?> UpdatedAtProperty =
        AvaloniaProperty.Register<UptimeStatus, DateTimeOffset?>(nameof(UpdatedAt));

    /// <summary>The right part of the line under the title (an incident's summary); null shows "Updated …" from <see cref="UpdatedAt"/>.</summary>
    public static readonly StyledProperty<string?> StatusSummaryProperty =
        AvaloniaProperty.Register<UptimeStatus, string?>(nameof(StatusSummary));

    /// <summary>The title while every service is operational.</summary>
    public static readonly StyledProperty<string> OperationalTitleProperty =
        AvaloniaProperty.Register<UptimeStatus, string>(nameof(OperationalTitle), "All systems operational");

    /// <summary>The title while a service is degraded.</summary>
    public static readonly StyledProperty<string> DegradedTitleProperty =
        AvaloniaProperty.Register<UptimeStatus, string>(nameof(DegradedTitle), "Degraded performance");

    /// <summary>The title while a service is in outage.</summary>
    public static readonly StyledProperty<string> OutageTitleProperty =
        AvaloniaProperty.Register<UptimeStatus, string>(nameof(OutageTitle), "Major outage");

    /// <summary>The service whose response time is charted (two-way); null or an unknown service uses the first.</summary>
    public static readonly StyledProperty<UptimeService?> SelectedServiceProperty =
        AvaloniaProperty.Register<UptimeStatus, UptimeService?>(nameof(SelectedService), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>What sits at the right of the header (a button, a link); the block draws no data into it.</summary>
    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<UptimeStatus, object?>(nameof(ActionContent));

    /// <summary>Formats a response time for the hover texts and announcements (default grouped integer).</summary>
    public static readonly StyledProperty<Func<double, string>?> LatencyFormatterProperty =
        AvaloniaProperty.Register<UptimeStatus, Func<double, string>?>(nameof(LatencyFormatter));

    /// <summary>Formats an uptime fraction (0 to 1) for the hover card and announcements (default "99.95%").</summary>
    public static readonly StyledProperty<Func<double, string>?> PercentFormatterProperty =
        AvaloniaProperty.Register<UptimeStatus, Func<double, string>?>(nameof(PercentFormatter));

    /// <summary>Formats a day for the hover card and the bars' first date (default "ddd, MMM d" and "MMM d").</summary>
    public static readonly StyledProperty<Func<DateOnly, string>?> DateFormatterProperty =
        AvaloniaProperty.Register<UptimeStatus, Func<DateOnly, string>?>(nameof(DateFormatter));

    /// <summary>The chart's domain is the nice number above the larger of the target and the highest p95 times this (1.2).</summary>
    public static readonly StyledProperty<double> ChartHeadroomProperty =
        AvaloniaProperty.Register<UptimeStatus, double>(nameof(ChartHeadroom), 1.2, coerce: (_, v) => Math.Max(1, v));

    /// <summary>Every word the block writes or speaks besides the data and the three titles.</summary>
    public static readonly StyledProperty<UptimeStatusLabels> LabelsProperty =
        AvaloniaProperty.Register<UptimeStatus, UptimeStatusLabels>(nameof(Labels), new UptimeStatusLabels());

    /// <summary>The worst status among the services that have an unresolved incident (read-only).</summary>
    public static readonly DirectProperty<UptimeStatus, ServiceStatus> OverallStatusProperty =
        AvaloniaProperty.RegisterDirect<UptimeStatus, ServiceStatus>(nameof(OverallStatus), o => o.OverallStatus);

    /// <summary>The index of the inspected day of the bars (read-only; null when none).</summary>
    public static readonly DirectProperty<UptimeStatus, int?> InspectedDayProperty =
        AvaloniaProperty.RegisterDirect<UptimeStatus, int?>(nameof(InspectedDay), o => o.InspectedDay);

    /// <summary>The index of the inspected sample of the chart (read-only; null when none).</summary>
    public static readonly DirectProperty<UptimeStatus, int?> InspectedPointProperty =
        AvaloniaProperty.RegisterDirect<UptimeStatus, int?>(nameof(InspectedPoint), o => o.InspectedPoint);

    #endregion

    #region Events

    /// <summary>Raised when another service is charted.</summary>
    public static readonly RoutedEvent<UptimeServiceEventArgs> SelectedServiceChangedEvent =
        RoutedEvent.Register<UptimeStatus, UptimeServiceEventArgs>(nameof(SelectedServiceChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a day of a service's bars is inspected or the inspection ends.</summary>
    public static readonly RoutedEvent<UptimeDayEventArgs> DayInspectedEvent =
        RoutedEvent.Register<UptimeStatus, UptimeDayEventArgs>(nameof(DayInspected), RoutingStrategies.Bubble);

    /// <summary>Raised when a sample of the chart is inspected or the inspection ends.</summary>
    public static readonly RoutedEvent<UptimePointEventArgs> PointInspectedEvent =
        RoutedEvent.Register<UptimeStatus, UptimePointEventArgs>(nameof(PointInspected), RoutingStrategies.Bubble);

    /// <summary>Raised when an incident's timeline is opened or closed by the user.</summary>
    public static readonly RoutedEvent<UptimeIncidentEventArgs> IncidentExpandedChangedEvent =
        RoutedEvent.Register<UptimeStatus, UptimeIncidentEventArgs>(nameof(IncidentExpandedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when another service is charted.</summary>
    public event EventHandler<UptimeServiceEventArgs>? SelectedServiceChanged
    {
        add => AddHandler(SelectedServiceChangedEvent, value);
        remove => RemoveHandler(SelectedServiceChangedEvent, value);
    }

    /// <summary>Raised when a day of a service's bars is inspected or the inspection ends.</summary>
    public event EventHandler<UptimeDayEventArgs>? DayInspected
    {
        add => AddHandler(DayInspectedEvent, value);
        remove => RemoveHandler(DayInspectedEvent, value);
    }

    /// <summary>Raised when a sample of the chart is inspected or the inspection ends.</summary>
    public event EventHandler<UptimePointEventArgs>? PointInspected
    {
        add => AddHandler(PointInspectedEvent, value);
        remove => RemoveHandler(PointInspectedEvent, value);
    }

    /// <summary>Raised when an incident's timeline is opened or closed by the user.</summary>
    public event EventHandler<UptimeIncidentEventArgs>? IncidentExpandedChanged
    {
        add => AddHandler(IncidentExpandedChangedEvent, value);
        remove => RemoveHandler(IncidentExpandedChangedEvent, value);
    }

    #endregion

    private ServiceStatus _overallStatus;
    private int? _inspectedDay;
    private int? _inspectedPoint;

    /// <summary>The services, one row each in this order, when no <see cref="ServicesSource"/> is set.</summary>
    [Content]
    public AvaloniaList<UptimeService> Services { get; } = new();

    /// <summary>The incidents in any order (the list shows the newest first), when no <see cref="IncidentsSource"/> is set.</summary>
    public AvaloniaList<UptimeIncident> Incidents { get; } = new();

    /// <inheritdoc cref="ServicesSourceProperty"/>
    public IEnumerable? ServicesSource { get => GetValue(ServicesSourceProperty); set => SetValue(ServicesSourceProperty, value); }

    /// <inheritdoc cref="IncidentsSourceProperty"/>
    public IEnumerable? IncidentsSource { get => GetValue(IncidentsSourceProperty); set => SetValue(IncidentsSourceProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateTimeOffset? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="DayCountProperty"/>
    public int DayCount { get => GetValue(DayCountProperty); set => SetValue(DayCountProperty, value); }

    /// <inheritdoc cref="PageNameProperty"/>
    public string? PageName { get => GetValue(PageNameProperty); set => SetValue(PageNameProperty, value); }

    /// <inheritdoc cref="UpdatedAtProperty"/>
    public DateTimeOffset? UpdatedAt { get => GetValue(UpdatedAtProperty); set => SetValue(UpdatedAtProperty, value); }

    /// <inheritdoc cref="StatusSummaryProperty"/>
    public string? StatusSummary { get => GetValue(StatusSummaryProperty); set => SetValue(StatusSummaryProperty, value); }

    /// <inheritdoc cref="OperationalTitleProperty"/>
    public string OperationalTitle { get => GetValue(OperationalTitleProperty); set => SetValue(OperationalTitleProperty, value); }

    /// <inheritdoc cref="DegradedTitleProperty"/>
    public string DegradedTitle { get => GetValue(DegradedTitleProperty); set => SetValue(DegradedTitleProperty, value); }

    /// <inheritdoc cref="OutageTitleProperty"/>
    public string OutageTitle { get => GetValue(OutageTitleProperty); set => SetValue(OutageTitleProperty, value); }

    /// <inheritdoc cref="SelectedServiceProperty"/>
    public UptimeService? SelectedService { get => GetValue(SelectedServiceProperty); set => SetValue(SelectedServiceProperty, value); }

    /// <inheritdoc cref="ActionContentProperty"/>
    public object? ActionContent { get => GetValue(ActionContentProperty); set => SetValue(ActionContentProperty, value); }

    /// <inheritdoc cref="LatencyFormatterProperty"/>
    public Func<double, string>? LatencyFormatter { get => GetValue(LatencyFormatterProperty); set => SetValue(LatencyFormatterProperty, value); }

    /// <inheritdoc cref="PercentFormatterProperty"/>
    public Func<double, string>? PercentFormatter { get => GetValue(PercentFormatterProperty); set => SetValue(PercentFormatterProperty, value); }

    /// <inheritdoc cref="DateFormatterProperty"/>
    public Func<DateOnly, string>? DateFormatter { get => GetValue(DateFormatterProperty); set => SetValue(DateFormatterProperty, value); }

    /// <inheritdoc cref="ChartHeadroomProperty"/>
    public double ChartHeadroom { get => GetValue(ChartHeadroomProperty); set => SetValue(ChartHeadroomProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public UptimeStatusLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="OverallStatusProperty"/>
    public ServiceStatus OverallStatus => _overallStatus;

    /// <inheritdoc cref="InspectedDayProperty"/>
    public int? InspectedDay => _inspectedDay;

    /// <inheritdoc cref="InspectedPointProperty"/>
    public int? InspectedPoint => _inspectedPoint;
}
