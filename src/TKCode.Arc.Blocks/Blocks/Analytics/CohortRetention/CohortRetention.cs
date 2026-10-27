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
/// Cohort retention: how many of each starting group (a month of sign-ups, new workspaces, new customers) were still active
/// in each month after they started. One dataset, two views: a heatmap triangle (one row per cohort, darker where more
/// stayed, with the size-weighted average row under it) and retention curves (one line per cohort and the average); the
/// cells shrink into points and the rows unfold into lines when the view changes. Above the chart, three headline numbers:
/// two months' average retention with the change between the newest and the oldest cohorts, and the total that started.
/// A segment switch filters the whole block (all, one plan, one channel): every number rolls and every cell repaints in a
/// wave from the top-left corner. Point at a cell, or use the arrow keys, for its exact numbers.
/// <code>
/// &lt;arc:CohortRetention Title="Space retention" Subtitle="Team spaces still editing each month after creation."&gt;
///     &lt;arc:RetentionSegment Label="All"&gt;
///         &lt;arc:RetentionCohort Label="Jun 2026" Size="412" Retained="{Binding JuneRetained}" /&gt;
///     &lt;/arc:RetentionSegment&gt;
/// &lt;/arc:CohortRetention&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys on the chart: Tab lands on the first cohort's first month; ←/→ move along the cohort, ↑/↓ between cohorts (↓ from
/// the newest cohort reaches the average row), Home / End jump to the cohort's first and last measured month, Escape clears,
/// Enter or Space raises <see cref="CellInvoked"/>. The two switches move their selection with the arrow keys.
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(SegmentSwitchPart, typeof(SegmentedControl))]
[TemplatePart(MetricsPart, typeof(Control))]
[TemplatePart(ToolbarPart, typeof(Panel))]
[TemplatePart(LegendPart, typeof(Control))]
[TemplatePart(ViewSwitchPart, typeof(SegmentedControl))]
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(TablePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(HeatmapPseudoClass, CurvesPseudoClass, CompactPseudoClass, NarrowPseudoClass, StackedPseudoClass, EmptyPseudoClass)]
public partial class CohortRetention : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string SegmentSwitchPart = "PART_SegmentSwitch";
    public const string MetricsPart = "PART_Metrics";
    public const string ToolbarPart = "PART_Toolbar";
    public const string LegendPart = "PART_Legend";
    public const string ViewSwitchPart = "PART_ViewSwitch";
    public const string PlotPart = "PART_Plot";
    public const string TablePart = "PART_Table";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the heatmap is the view.</summary>
    public const string HeatmapPseudoClass = ":heatmap";

    /// <summary>Set while the curves are the view.</summary>
    public const string CurvesPseudoClass = ":curves";

    /// <summary>Set while the chart is narrower than 600: shorter labels, no size column, smaller rows.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is narrower than 560: paddings 16, the segment switch under the titles.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is narrower than 440: the metrics stack, the view switch goes above the legend.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the selected segment has no cohorts.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CohortRetention, string?>(nameof(Title));

    /// <summary>The line under the heading (what is counted, data through when); null leaves it out.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<CohortRetention, string?>(nameof(Subtitle));

    /// <summary>The segments as a list you own (followed when it raises collection changes); it wins over <see cref="Segments"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> SegmentsSourceProperty =
        AvaloniaProperty.Register<CohortRetention, IEnumerable?>(nameof(SegmentsSource));

    /// <summary>The segment shown (two-way); null shows the first one.</summary>
    public static readonly StyledProperty<RetentionSegment?> SelectedSegmentProperty =
        AvaloniaProperty.Register<CohortRetention, RetentionSegment?>(nameof(SelectedSegment), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Heatmap or curves (two-way). Default heatmap.</summary>
    public static readonly StyledProperty<CohortRetentionView> ViewProperty =
        AvaloniaProperty.Register<CohortRetention, CohortRetentionView>(nameof(View), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The inspected cell (pointer or keys; two-way): cursor box or ring, dimming, card. Null for none.</summary>
    public static readonly StyledProperty<CohortCell?> CursorCellProperty =
        AvaloniaProperty.Register<CohortRetention, CohortCell?>(nameof(CursorCell), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many month columns to draw; 0 (default) takes the longest cohort.</summary>
    public static readonly StyledProperty<int> MonthsProperty =
        AvaloniaProperty.Register<CohortRetention, int>(nameof(Months));

    /// <summary>Where the heat steps begin (rates 0–1, ascending); null uses 0.20, 0.35, 0.50, 0.65 and 0.80 (six steps).</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ThresholdsProperty =
        AvaloniaProperty.Register<CohortRetention, IReadOnlyList<double>?>(nameof(Thresholds));

    /// <summary>
    /// How much of the accent each step mixes into the surface (0–1, one more than <see cref="Thresholds"/>); null uses 8, 18,
    /// 32, 48, 66 and 86%. Steps of 50% or more take the accent's foreground for their numbers.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> RampMixProperty =
        AvaloniaProperty.Register<CohortRetention, IReadOnlyList<double>?>(nameof(RampMix));

    /// <summary>The month of the first headline number (default 1).</summary>
    public static readonly StyledProperty<int> FirstMetricMonthProperty =
        AvaloniaProperty.Register<CohortRetention, int>(nameof(FirstMetricMonth), 1);

    /// <summary>The month of the second headline number (default 6).</summary>
    public static readonly StyledProperty<int> SecondMetricMonthProperty =
        AvaloniaProperty.Register<CohortRetention, int>(nameof(SecondMetricMonth), 6);

    /// <summary>How many of the newest and the oldest cohorts the headline changes compare (default 3).</summary>
    public static readonly StyledProperty<int> CompareCohortCountProperty =
        AvaloniaProperty.Register<CohortRetention, int>(nameof(CompareCohortCount), 3);

    /// <summary>Whether the heatmap shows each cohort's size beside its name (wide layouts only).</summary>
    public static readonly StyledProperty<bool> ShowSizeColumnProperty =
        AvaloniaProperty.Register<CohortRetention, bool>(nameof(ShowSizeColumn), true);

    /// <summary>Whether the size-weighted average row (and the average curve) is drawn.</summary>
    public static readonly StyledProperty<bool> ShowAverageRowProperty =
        AvaloniaProperty.Register<CohortRetention, bool>(nameof(ShowAverageRow), true);

    /// <summary>Formats a rate (0–1) for the headline numbers, the card and the y tag; null gives one decimal ("48.5%").</summary>
    public static readonly StyledProperty<Func<double, string>?> PercentFormatterProperty =
        AvaloniaProperty.Register<CohortRetention, Func<double, string>?>(nameof(PercentFormatter));

    /// <summary>Formats counts (sizes, the total, "a of b"); null groups thousands ("5,199").</summary>
    public static readonly StyledProperty<Func<double, string>?> CountFormatterProperty =
        AvaloniaProperty.Register<CohortRetention, Func<double, string>?>(nameof(CountFormatter));

    /// <summary>Formats a change in percentage points; null gives a signed one-decimal value ("+5.2 pts", "−2.2 pts").</summary>
    public static readonly StyledProperty<Func<double, string>?> DeltaFormatterProperty =
        AvaloniaProperty.Register<CohortRetention, Func<double, string>?>(nameof(DeltaFormatter));

    /// <summary>The first headline's label; null formats <see cref="MetricLabelFormat"/> with <see cref="FirstMetricMonth"/>.</summary>
    public static readonly StyledProperty<string?> FirstMetricLabelProperty =
        AvaloniaProperty.Register<CohortRetention, string?>(nameof(FirstMetricLabel));

    /// <summary>The second headline's label; null formats <see cref="MetricLabelFormat"/> with <see cref="SecondMetricMonth"/>.</summary>
    public static readonly StyledProperty<string?> SecondMetricLabelProperty =
        AvaloniaProperty.Register<CohortRetention, string?>(nameof(SecondMetricLabel));

    /// <summary>The headline label of a month; {0} is the month.</summary>
    public static readonly StyledProperty<string> MetricLabelFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(MetricLabelFormat), "Month {0} retention");

    /// <summary>The third headline's label (everyone who started).</summary>
    public static readonly StyledProperty<string> TotalLabelProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(TotalLabel), "Started");

    /// <summary>The muted text after a headline change; {0} is <see cref="CompareCohortCount"/>.</summary>
    public static readonly StyledProperty<string> CompareFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CompareFormat), "newest {0} vs oldest {0} cohorts");

    /// <summary>The text under the total; {0} is the first cohort's name, {1} the last one's.</summary>
    public static readonly StyledProperty<string> TotalContextFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(TotalContextFormat), "{0} to {1}");

    /// <summary>The average row's name.</summary>
    public static readonly StyledProperty<string> AverageLabelProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(AverageLabel), "Average");

    /// <summary>The x axis title.</summary>
    public static readonly StyledProperty<string> AxisTitleProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(AxisTitle), "Months since start");

    /// <summary>The x axis title in compact charts.</summary>
    public static readonly StyledProperty<string> AxisTitleShortProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(AxisTitleShort), "Month");

    /// <summary>The heatmap legend's title.</summary>
    public static readonly StyledProperty<string> LegendTitleProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(LegendTitle), "% retained");

    /// <summary>The curves legend's text for the average line.</summary>
    public static readonly StyledProperty<string> AverageLegendTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(AverageLegendText), "Average, weighted by size");

    /// <summary>The curves legend's text for the cohort lines.</summary>
    public static readonly StyledProperty<string> CohortLegendTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CohortLegendText), "One line per cohort");

    /// <summary>The view switch's heatmap button.</summary>
    public static readonly StyledProperty<string> HeatmapTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(HeatmapText), "Heatmap");

    /// <summary>The view switch's curves button.</summary>
    public static readonly StyledProperty<string> CurvesTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CurvesText), "Curves");

    /// <summary>The screen reader name of the segment switch.</summary>
    public static readonly StyledProperty<string> SegmentSwitchLabelProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(SegmentSwitchLabel), "Segment");

    /// <summary>The screen reader name of the view switch.</summary>
    public static readonly StyledProperty<string> ViewSwitchLabelProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(ViewSwitchLabel), "Chart view");

    /// <summary>The card's title for a cohort; {0} is the cohort's name.</summary>
    public static readonly StyledProperty<string> CohortTitleFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CohortTitleFormat), "{0} cohort");

    /// <summary>A month's name in the card; {0} is the month.</summary>
    public static readonly StyledProperty<string> MonthFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(MonthFormat), "Month {0}");

    /// <summary>The card's first row label.</summary>
    public static readonly StyledProperty<string> StillActiveTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(StillActiveText), "Still active");

    /// <summary>"a of b" in the card; {0} retained, {1} started.</summary>
    public static readonly StyledProperty<string> OfFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(OfFormat), "{0} of {1}");

    /// <summary>The card's second row label for a cohort (its difference from the month's average).</summary>
    public static readonly StyledProperty<string> VsAverageTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(VsAverageText), "Vs average");

    /// <summary>The card's second row label for the average row (how many cohorts it covers).</summary>
    public static readonly StyledProperty<string> WeightedOverTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(WeightedOverText), "Weighted over");

    /// <summary>A number of cohorts above one; {0} is the number.</summary>
    public static readonly StyledProperty<string> CohortCountFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CohortCountFormat), "{0} cohorts");

    /// <summary>One cohort.</summary>
    public static readonly StyledProperty<string> OneCohortTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(OneCohortText), "1 cohort");

    /// <summary>The screen reader table's text for a month a cohort has not reached.</summary>
    public static readonly StyledProperty<string> NotMeasuredTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(NotMeasuredText), "not measured yet");

    /// <summary>Shown in the chart when the segment has no cohorts.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(EmptyText), "No cohorts to show yet.");

    /// <summary>The chart's screen reader help text (how to use the keys).</summary>
    public static readonly StyledProperty<string> ChartHintProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(ChartHint),
            "Arrow keys move between cohorts and months. Home and End jump to a cohort's first and last month. Escape clears.");

    /// <summary>Said when the heatmap is chosen.</summary>
    public static readonly StyledProperty<string> HeatmapAnnouncementProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(HeatmapAnnouncement), "Heatmap: darker cells kept more of their cohort.");

    /// <summary>Said when the curves are chosen.</summary>
    public static readonly StyledProperty<string> CurvesAnnouncementProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CurvesAnnouncement), "Curves: one line per cohort and the size-weighted average.");

    /// <summary>Said when a segment is chosen; {0} the segment, {1} and {2} the first headline, {3} and {4} the second.</summary>
    public static readonly StyledProperty<string> SegmentAnnouncementFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(SegmentAnnouncementFormat), "Showing {0}. {1}: {2}. {3}: {4}.");

    /// <summary>Said for a cohort's cell; {0} cohort, {1} month, {2} rate, {3} retained, {4} size.</summary>
    public static readonly StyledProperty<string> CellAnnouncementFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(CellAnnouncementFormat), "{0}, month {1}: {2} still active, {3} of {4}.");

    /// <summary>Said for an average cell; {0} the number of cohorts, {1} month, {2} rate.</summary>
    public static readonly StyledProperty<string> AverageAnnouncementFormatProperty =
        AvaloniaProperty.Register<CohortRetention, string>(nameof(AverageAnnouncementFormat), "Average of {0}, month {1}: {2} still active.");

    /// <summary>Run when a cell is clicked or Enter / Space is pressed on it, with its <see cref="CohortCellEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> CellCommandProperty =
        AvaloniaProperty.Register<CohortRetention, ICommand?>(nameof(CellCommand));

    /// <summary>The size-weighted average retention of <see cref="FirstMetricMonth"/> (0–1); null when no cohort reached it.</summary>
    public static readonly DirectProperty<CohortRetention, double?> FirstMetricProperty =
        AvaloniaProperty.RegisterDirect<CohortRetention, double?>(nameof(FirstMetric), o => o.FirstMetric);

    /// <summary>The size-weighted average retention of <see cref="SecondMetricMonth"/> (0–1); null when no cohort reached it.</summary>
    public static readonly DirectProperty<CohortRetention, double?> SecondMetricProperty =
        AvaloniaProperty.RegisterDirect<CohortRetention, double?>(nameof(SecondMetric), o => o.SecondMetric);

    /// <summary>Everyone who started, over the selected segment's cohorts.</summary>
    public static readonly DirectProperty<CohortRetention, long> TotalSizeProperty =
        AvaloniaProperty.RegisterDirect<CohortRetention, long>(nameof(TotalSize), o => o.TotalSize);

    #endregion

    #region Events

    /// <summary>Raised when <see cref="CursorCell"/> changes (pointer, keys or code).</summary>
    public static readonly RoutedEvent<CohortCellEventArgs> CursorChangedEvent =
        RoutedEvent.Register<CohortRetention, CohortCellEventArgs>(nameof(CursorChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a cell is clicked, or Enter / Space is pressed on the cursor's cell (a drill-down hook).</summary>
    public static readonly RoutedEvent<CohortCellEventArgs> CellInvokedEvent =
        RoutedEvent.Register<CohortRetention, CohortCellEventArgs>(nameof(CellInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="View"/> changes.</summary>
    public static readonly RoutedEvent<CohortRetentionViewChangedEventArgs> ViewChangedEvent =
        RoutedEvent.Register<CohortRetention, CohortRetentionViewChangedEventArgs>(nameof(ViewChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the shown segment changes.</summary>
    public static readonly RoutedEvent<CohortRetentionSegmentChangedEventArgs> SegmentChangedEvent =
        RoutedEvent.Register<CohortRetention, CohortRetentionSegmentChangedEventArgs>(nameof(SegmentChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="CursorChangedEvent"/>
    public event EventHandler<CohortCellEventArgs>? CursorChanged
    {
        add => AddHandler(CursorChangedEvent, value);
        remove => RemoveHandler(CursorChangedEvent, value);
    }

    /// <inheritdoc cref="CellInvokedEvent"/>
    public event EventHandler<CohortCellEventArgs>? CellInvoked
    {
        add => AddHandler(CellInvokedEvent, value);
        remove => RemoveHandler(CellInvokedEvent, value);
    }

    /// <inheritdoc cref="ViewChangedEvent"/>
    public event EventHandler<CohortRetentionViewChangedEventArgs>? ViewChanged
    {
        add => AddHandler(ViewChangedEvent, value);
        remove => RemoveHandler(ViewChangedEvent, value);
    }

    /// <inheritdoc cref="SegmentChangedEvent"/>
    public event EventHandler<CohortRetentionSegmentChangedEventArgs>? SegmentChanged
    {
        add => AddHandler(SegmentChangedEvent, value);
        remove => RemoveHandler(SegmentChangedEvent, value);
    }

    #endregion

    #region Accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="SegmentsSourceProperty"/>
    public IEnumerable? SegmentsSource { get => GetValue(SegmentsSourceProperty); set => SetValue(SegmentsSourceProperty, value); }

    /// <summary>The segments the switch offers, in order (ignored while <see cref="SegmentsSource"/> is set).</summary>
    [Content]
    public AvaloniaList<RetentionSegment> Segments { get; } = new();

    /// <inheritdoc cref="SelectedSegmentProperty"/>
    public RetentionSegment? SelectedSegment { get => GetValue(SelectedSegmentProperty); set => SetValue(SelectedSegmentProperty, value); }

    /// <inheritdoc cref="ViewProperty"/>
    public CohortRetentionView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <inheritdoc cref="CursorCellProperty"/>
    public CohortCell? CursorCell { get => GetValue(CursorCellProperty); set => SetValue(CursorCellProperty, value); }

    /// <inheritdoc cref="MonthsProperty"/>
    public int Months { get => GetValue(MonthsProperty); set => SetValue(MonthsProperty, value); }

    /// <inheritdoc cref="ThresholdsProperty"/>
    public IReadOnlyList<double>? Thresholds { get => GetValue(ThresholdsProperty); set => SetValue(ThresholdsProperty, value); }

    /// <inheritdoc cref="RampMixProperty"/>
    public IReadOnlyList<double>? RampMix { get => GetValue(RampMixProperty); set => SetValue(RampMixProperty, value); }

    /// <inheritdoc cref="FirstMetricMonthProperty"/>
    public int FirstMetricMonth { get => GetValue(FirstMetricMonthProperty); set => SetValue(FirstMetricMonthProperty, value); }

    /// <inheritdoc cref="SecondMetricMonthProperty"/>
    public int SecondMetricMonth { get => GetValue(SecondMetricMonthProperty); set => SetValue(SecondMetricMonthProperty, value); }

    /// <inheritdoc cref="CompareCohortCountProperty"/>
    public int CompareCohortCount { get => GetValue(CompareCohortCountProperty); set => SetValue(CompareCohortCountProperty, value); }

    /// <inheritdoc cref="ShowSizeColumnProperty"/>
    public bool ShowSizeColumn { get => GetValue(ShowSizeColumnProperty); set => SetValue(ShowSizeColumnProperty, value); }

    /// <inheritdoc cref="ShowAverageRowProperty"/>
    public bool ShowAverageRow { get => GetValue(ShowAverageRowProperty); set => SetValue(ShowAverageRowProperty, value); }

    /// <inheritdoc cref="PercentFormatterProperty"/>
    public Func<double, string>? PercentFormatter { get => GetValue(PercentFormatterProperty); set => SetValue(PercentFormatterProperty, value); }

    /// <inheritdoc cref="CountFormatterProperty"/>
    public Func<double, string>? CountFormatter { get => GetValue(CountFormatterProperty); set => SetValue(CountFormatterProperty, value); }

    /// <inheritdoc cref="DeltaFormatterProperty"/>
    public Func<double, string>? DeltaFormatter { get => GetValue(DeltaFormatterProperty); set => SetValue(DeltaFormatterProperty, value); }

    /// <inheritdoc cref="FirstMetricLabelProperty"/>
    public string? FirstMetricLabel { get => GetValue(FirstMetricLabelProperty); set => SetValue(FirstMetricLabelProperty, value); }

    /// <inheritdoc cref="SecondMetricLabelProperty"/>
    public string? SecondMetricLabel { get => GetValue(SecondMetricLabelProperty); set => SetValue(SecondMetricLabelProperty, value); }

    /// <inheritdoc cref="MetricLabelFormatProperty"/>
    public string MetricLabelFormat { get => GetValue(MetricLabelFormatProperty); set => SetValue(MetricLabelFormatProperty, value); }

    /// <inheritdoc cref="TotalLabelProperty"/>
    public string TotalLabel { get => GetValue(TotalLabelProperty); set => SetValue(TotalLabelProperty, value); }

    /// <inheritdoc cref="CompareFormatProperty"/>
    public string CompareFormat { get => GetValue(CompareFormatProperty); set => SetValue(CompareFormatProperty, value); }

    /// <inheritdoc cref="TotalContextFormatProperty"/>
    public string TotalContextFormat { get => GetValue(TotalContextFormatProperty); set => SetValue(TotalContextFormatProperty, value); }

    /// <inheritdoc cref="AverageLabelProperty"/>
    public string AverageLabel { get => GetValue(AverageLabelProperty); set => SetValue(AverageLabelProperty, value); }

    /// <inheritdoc cref="AxisTitleProperty"/>
    public string AxisTitle { get => GetValue(AxisTitleProperty); set => SetValue(AxisTitleProperty, value); }

    /// <inheritdoc cref="AxisTitleShortProperty"/>
    public string AxisTitleShort { get => GetValue(AxisTitleShortProperty); set => SetValue(AxisTitleShortProperty, value); }

    /// <inheritdoc cref="LegendTitleProperty"/>
    public string LegendTitle { get => GetValue(LegendTitleProperty); set => SetValue(LegendTitleProperty, value); }

    /// <inheritdoc cref="AverageLegendTextProperty"/>
    public string AverageLegendText { get => GetValue(AverageLegendTextProperty); set => SetValue(AverageLegendTextProperty, value); }

    /// <inheritdoc cref="CohortLegendTextProperty"/>
    public string CohortLegendText { get => GetValue(CohortLegendTextProperty); set => SetValue(CohortLegendTextProperty, value); }

    /// <inheritdoc cref="HeatmapTextProperty"/>
    public string HeatmapText { get => GetValue(HeatmapTextProperty); set => SetValue(HeatmapTextProperty, value); }

    /// <inheritdoc cref="CurvesTextProperty"/>
    public string CurvesText { get => GetValue(CurvesTextProperty); set => SetValue(CurvesTextProperty, value); }

    /// <inheritdoc cref="SegmentSwitchLabelProperty"/>
    public string SegmentSwitchLabel { get => GetValue(SegmentSwitchLabelProperty); set => SetValue(SegmentSwitchLabelProperty, value); }

    /// <inheritdoc cref="ViewSwitchLabelProperty"/>
    public string ViewSwitchLabel { get => GetValue(ViewSwitchLabelProperty); set => SetValue(ViewSwitchLabelProperty, value); }

    /// <inheritdoc cref="CohortTitleFormatProperty"/>
    public string CohortTitleFormat { get => GetValue(CohortTitleFormatProperty); set => SetValue(CohortTitleFormatProperty, value); }

    /// <inheritdoc cref="MonthFormatProperty"/>
    public string MonthFormat { get => GetValue(MonthFormatProperty); set => SetValue(MonthFormatProperty, value); }

    /// <inheritdoc cref="StillActiveTextProperty"/>
    public string StillActiveText { get => GetValue(StillActiveTextProperty); set => SetValue(StillActiveTextProperty, value); }

    /// <inheritdoc cref="OfFormatProperty"/>
    public string OfFormat { get => GetValue(OfFormatProperty); set => SetValue(OfFormatProperty, value); }

    /// <inheritdoc cref="VsAverageTextProperty"/>
    public string VsAverageText { get => GetValue(VsAverageTextProperty); set => SetValue(VsAverageTextProperty, value); }

    /// <inheritdoc cref="WeightedOverTextProperty"/>
    public string WeightedOverText { get => GetValue(WeightedOverTextProperty); set => SetValue(WeightedOverTextProperty, value); }

    /// <inheritdoc cref="CohortCountFormatProperty"/>
    public string CohortCountFormat { get => GetValue(CohortCountFormatProperty); set => SetValue(CohortCountFormatProperty, value); }

    /// <inheritdoc cref="OneCohortTextProperty"/>
    public string OneCohortText { get => GetValue(OneCohortTextProperty); set => SetValue(OneCohortTextProperty, value); }

    /// <inheritdoc cref="NotMeasuredTextProperty"/>
    public string NotMeasuredText { get => GetValue(NotMeasuredTextProperty); set => SetValue(NotMeasuredTextProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="ChartHintProperty"/>
    public string ChartHint { get => GetValue(ChartHintProperty); set => SetValue(ChartHintProperty, value); }

    /// <inheritdoc cref="HeatmapAnnouncementProperty"/>
    public string HeatmapAnnouncement { get => GetValue(HeatmapAnnouncementProperty); set => SetValue(HeatmapAnnouncementProperty, value); }

    /// <inheritdoc cref="CurvesAnnouncementProperty"/>
    public string CurvesAnnouncement { get => GetValue(CurvesAnnouncementProperty); set => SetValue(CurvesAnnouncementProperty, value); }

    /// <inheritdoc cref="SegmentAnnouncementFormatProperty"/>
    public string SegmentAnnouncementFormat { get => GetValue(SegmentAnnouncementFormatProperty); set => SetValue(SegmentAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="CellAnnouncementFormatProperty"/>
    public string CellAnnouncementFormat { get => GetValue(CellAnnouncementFormatProperty); set => SetValue(CellAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="AverageAnnouncementFormatProperty"/>
    public string AverageAnnouncementFormat { get => GetValue(AverageAnnouncementFormatProperty); set => SetValue(AverageAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="CellCommandProperty"/>
    public ICommand? CellCommand { get => GetValue(CellCommandProperty); set => SetValue(CellCommandProperty, value); }

    /// <inheritdoc cref="FirstMetricProperty"/>
    public double? FirstMetric { get => _firstMetric; private set => SetAndRaise(FirstMetricProperty, ref _firstMetric, value); }

    /// <inheritdoc cref="SecondMetricProperty"/>
    public double? SecondMetric { get => _secondMetric; private set => SetAndRaise(SecondMetricProperty, ref _secondMetric, value); }

    /// <inheritdoc cref="TotalSizeProperty"/>
    public long TotalSize { get => _totalSize; private set => SetAndRaise(TotalSizeProperty, ref _totalSize, value); }

    #endregion

    private double? _firstMetric;
    private double? _secondMetric;
    private long _totalSize;
}
