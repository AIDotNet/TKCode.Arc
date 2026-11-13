using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A compact product analytics overview: a header with a range switch (7D / 30D / 90D) and an Export button, a row of
/// metric tabs (total and change for the range; the selected one drives the chart), an inspectable area chart that can
/// overlay the previous period, and two ranked lists. Totals and list values count to their new numbers when the range
/// changes, the chart cross-fades, the range thumb and the tab underline glide. Long ranges are drawn as weekly points.
/// <code>
/// &lt;arc:MetricsDashboard Title="Insights" Source="northwind.dev"
///                       PrimaryListTitle="Top paths" SecondaryListTitle="Referrers" ListValueHeader="Sessions" /&gt;
/// </code>
/// Fill <see cref="Metrics"/>, <see cref="PrimaryItems"/> and <see cref="SecondaryItems"/> from code or bind the
/// <c>*Source</c> properties.
/// </summary>
public partial class MetricsDashboard : TemplatedControl
{
    #region Properties

    /// <summary>The heading.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MetricsDashboard, string?>(nameof(Title), "Analytics");

    /// <summary>What the numbers are about (a site, a product), used by <see cref="SubtitleFormat"/>.</summary>
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MetricsDashboard, string?>(nameof(Source));

    /// <summary>A fixed line under the heading; null builds it from <see cref="SubtitleFormat"/> and follows the range.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<MetricsDashboard, string?>(nameof(Subtitle));

    /// <summary>The line under the heading when <see cref="Subtitle"/> is null; {0} is <see cref="Source"/>, {1} the range in days.</summary>
    public static readonly StyledProperty<string> SubtitleFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(SubtitleFormat), "{0} · Last {1} days");

    /// <summary>The line under the heading when there is no <see cref="Source"/>; {0} is the range in days.</summary>
    public static readonly StyledProperty<string> SubtitleRangeFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(SubtitleRangeFormat), "Last {0} days");

    /// <summary>The metrics, when you give them as a list to follow; it wins over <see cref="Metrics"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MetricsSourceProperty =
        AvaloniaProperty.Register<MetricsDashboard, IEnumerable?>(nameof(MetricsSource));

    /// <summary>The selected metric's index (two-way): its tab is underlined and the chart draws it.</summary>
    public static readonly StyledProperty<int> SelectedMetricIndexProperty =
        AvaloniaProperty.Register<MetricsDashboard, int>(nameof(SelectedMetricIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected range in days (two-way); one of <see cref="Ranges"/>.</summary>
    public static readonly StyledProperty<int> SelectedRangeProperty =
        AvaloniaProperty.Register<MetricsDashboard, int>(nameof(SelectedRange), 30, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Ranges longer than this many days are drawn as weekly points (60: 90D gives 13 weeks).</summary>
    public static readonly StyledProperty<int> WeeklyThresholdProperty =
        AvaloniaProperty.Register<MetricsDashboard, int>(nameof(WeeklyThreshold), 60);

    /// <summary>The day of the last value of every series; null is today.</summary>
    public static readonly StyledProperty<DateOnly?> EndDateProperty =
        AvaloniaProperty.Register<MetricsDashboard, DateOnly?>(nameof(EndDate));

    /// <summary>The range buttons' text; {0} is the number of days.</summary>
    public static readonly StyledProperty<string> RangeLabelFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(RangeLabelFormat), "{0}D");

    /// <summary>The range buttons' screen reader names; {0} is the number of days.</summary>
    public static readonly StyledProperty<string> RangeNameFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(RangeNameFormat), "Last {0} days");

    /// <summary>The screen reader name of the range switch.</summary>
    public static readonly StyledProperty<string> RangeGroupNameProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(RangeGroupName), "Date range");

    /// <summary>The screen reader name of the metric tabs.</summary>
    public static readonly StyledProperty<string> MetricGroupNameProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(MetricGroupName), "Metric");

    /// <summary>Whether the chart overlays the previous period as a dashed line (two-way).</summary>
    public static readonly StyledProperty<bool> IsCompareEnabledProperty =
        AvaloniaProperty.Register<MetricsDashboard, bool>(nameof(IsCompareEnabled), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the Compare check box is shown.</summary>
    public static readonly StyledProperty<bool> IsCompareVisibleProperty =
        AvaloniaProperty.Register<MetricsDashboard, bool>(nameof(IsCompareVisible), true);

    /// <summary>The Compare check box's text.</summary>
    public static readonly StyledProperty<string> CompareTextProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(CompareText), "Compare");

    /// <summary>The legend's name of the previous period.</summary>
    public static readonly StyledProperty<string> PreviousLegendTextProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(PreviousLegendText), "Previous period");

    /// <summary>The tooltip's previous-period line; {0} is the formatted value.</summary>
    public static readonly StyledProperty<string> PreviousValueFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(PreviousValueFormat), "{0} previous");

    /// <summary>The tooltip's date pattern (.NET, en-US) for daily points and week starts.</summary>
    public static readonly StyledProperty<string> DateFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(DateFormat), "MMM d");

    /// <summary>The tooltip's title for a weekly point; {0} is the week's first day.</summary>
    public static readonly StyledProperty<string> WeekTitleFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(WeekTitleFormat), "Week of {0}");

    /// <summary>The tooltip's title for the last week when it is shorter than seven days; {0} is its first day, {1} its length.</summary>
    public static readonly StyledProperty<string> PartialWeekTitleFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(PartialWeekTitleFormat), "Week of {0} ({1} days)");

    /// <summary>The chart's screen reader name; {0} is the metric, {1} the range in days.</summary>
    public static readonly StyledProperty<string> ChartNameFormatProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(ChartNameFormat), "{0}, last {1} days");

    /// <summary>The chart's screen reader help text.</summary>
    public static readonly StyledProperty<string> ChartHelpTextProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(ChartHelpText), "Use the arrow keys to inspect each point.");

    /// <summary>Spoken after a change, so its meaning does not rest on color ("up 11.7% versus the previous period").</summary>
    public static readonly StyledProperty<string> ChangeContextTextProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(ChangeContextText), "versus the previous period");

    /// <summary>The first list, when you give it as a list to follow; it wins over <see cref="PrimaryItems"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> PrimaryItemsSourceProperty =
        AvaloniaProperty.Register<MetricsDashboard, IEnumerable?>(nameof(PrimaryItemsSource));

    /// <summary>The second list, when you give it as a list to follow; it wins over <see cref="SecondaryItems"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> SecondaryItemsSourceProperty =
        AvaloniaProperty.Register<MetricsDashboard, IEnumerable?>(nameof(SecondaryItemsSource));

    /// <summary>The first list's heading.</summary>
    public static readonly StyledProperty<string?> PrimaryListTitleProperty =
        AvaloniaProperty.Register<MetricsDashboard, string?>(nameof(PrimaryListTitle), "Top pages");

    /// <summary>The second list's heading.</summary>
    public static readonly StyledProperty<string?> SecondaryListTitleProperty =
        AvaloniaProperty.Register<MetricsDashboard, string?>(nameof(SecondaryListTitle), "Sources");

    /// <summary>The column heading over the lists' values.</summary>
    public static readonly StyledProperty<string?> ListValueHeaderProperty =
        AvaloniaProperty.Register<MetricsDashboard, string?>(nameof(ListValueHeader), "Visitors");

    /// <summary>Formats the lists' values; null writes grouped whole numbers ("11,373").</summary>
    public static readonly StyledProperty<Func<double, string>?> ListValueFormatterProperty =
        AvaloniaProperty.Register<MetricsDashboard, Func<double, string>?>(nameof(ListValueFormatter));

    /// <summary>Whether the Export button is shown.</summary>
    public static readonly StyledProperty<bool> IsExportVisibleProperty =
        AvaloniaProperty.Register<MetricsDashboard, bool>(nameof(IsExportVisible), true);

    /// <summary>The Export button's text.</summary>
    public static readonly StyledProperty<string> ExportTextProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(ExportText), "Export");

    /// <summary>The Export button's text while it confirms an export.</summary>
    public static readonly StyledProperty<string> ExportedTextProperty =
        AvaloniaProperty.Register<MetricsDashboard, string>(nameof(ExportedText), "Exported");

    /// <summary>Run when Export is pressed, with a <see cref="MetricsDashboardExportRequest"/> as its parameter; when it cannot execute nothing happens.</summary>
    public static readonly StyledProperty<ICommand?> ExportCommandProperty =
        AvaloniaProperty.Register<MetricsDashboard, ICommand?>(nameof(ExportCommand));

    /// <summary>How long the button says <see cref="ExportedText"/> before it returns to <see cref="ExportText"/> (1.8 s).</summary>
    public static readonly StyledProperty<TimeSpan> ExportConfirmDurationProperty =
        AvaloniaProperty.Register<MetricsDashboard, TimeSpan>(nameof(ExportConfirmDuration), TimeSpan.FromMilliseconds(1800));

    /// <summary>The point being inspected (−1 for none).</summary>
    public static readonly DirectProperty<MetricsDashboard, int> ActiveIndexProperty =
        AvaloniaProperty.RegisterDirect<MetricsDashboard, int>(nameof(ActiveIndex), o => o.ActiveIndex);

    /// <summary>The layout the block picked from its own width.</summary>
    public static readonly DirectProperty<MetricsDashboard, MetricsDashboardSize> LayoutSizeProperty =
        AvaloniaProperty.RegisterDirect<MetricsDashboard, MetricsDashboardSize>(nameof(LayoutSize), o => o.LayoutSize);

    /// <summary>True while the Export button shows its confirmation.</summary>
    public static readonly DirectProperty<MetricsDashboard, bool> IsExportConfirmedProperty =
        AvaloniaProperty.RegisterDirect<MetricsDashboard, bool>(nameof(IsExportConfirmed), o => o.IsExportConfirmed);

    #endregion

    #region Events

    /// <summary>Raised when <see cref="SelectedRange"/> changes, by the person or by code.</summary>
    public static readonly RoutedEvent<MetricsDashboardRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<MetricsDashboard, MetricsDashboardRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedMetricIndex"/> changes.</summary>
    public static readonly RoutedEvent<MetricsDashboardMetricChangedEventArgs> SelectedMetricChangedEvent =
        RoutedEvent.Register<MetricsDashboard, MetricsDashboardMetricChangedEventArgs>(nameof(SelectedMetricChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="IsCompareEnabled"/> changes.</summary>
    public static readonly RoutedEvent<MetricsDashboardCompareChangedEventArgs> CompareChangedEvent =
        RoutedEvent.Register<MetricsDashboard, MetricsDashboardCompareChangedEventArgs>(nameof(CompareChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a point starts being inspected or the inspection moves to another point.</summary>
    public static readonly RoutedEvent<MetricsDashboardPointEventArgs> PointInspectedEvent =
        RoutedEvent.Register<MetricsDashboard, MetricsDashboardPointEventArgs>(nameof(PointInspected), RoutingStrategies.Bubble);

    /// <summary>Raised when Export is pressed (or <see cref="Export"/> is called), after <see cref="ExportCommand"/> ran.</summary>
    public static readonly RoutedEvent<MetricsDashboardExportEventArgs> ExportRequestedEvent =
        RoutedEvent.Register<MetricsDashboard, MetricsDashboardExportEventArgs>(nameof(ExportRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedRange"/> changes, by the person or by code.</summary>
    public event EventHandler<MetricsDashboardRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    /// <summary>Raised when <see cref="SelectedMetricIndex"/> changes.</summary>
    public event EventHandler<MetricsDashboardMetricChangedEventArgs>? SelectedMetricChanged
    {
        add => AddHandler(SelectedMetricChangedEvent, value);
        remove => RemoveHandler(SelectedMetricChangedEvent, value);
    }

    /// <summary>Raised when <see cref="IsCompareEnabled"/> changes.</summary>
    public event EventHandler<MetricsDashboardCompareChangedEventArgs>? CompareChanged
    {
        add => AddHandler(CompareChangedEvent, value);
        remove => RemoveHandler(CompareChangedEvent, value);
    }

    /// <summary>Raised when a point starts being inspected or the inspection moves to another point.</summary>
    public event EventHandler<MetricsDashboardPointEventArgs>? PointInspected
    {
        add => AddHandler(PointInspectedEvent, value);
        remove => RemoveHandler(PointInspectedEvent, value);
    }

    /// <summary>Raised when Export is pressed (or <see cref="Export"/> is called), after <see cref="ExportCommand"/> ran.</summary>
    public event EventHandler<MetricsDashboardExportEventArgs>? ExportRequested
    {
        add => AddHandler(ExportRequestedEvent, value);
        remove => RemoveHandler(ExportRequestedEvent, value);
    }

    #endregion

    private int _activeIndex = -1;
    private MetricsDashboardSize _layoutSize;
    private bool _isExportConfirmed;

    /// <summary>The metrics, one tab each, when no <see cref="MetricsSource"/> is set. Four fill the row; the medium layout puts them two by two.</summary>
    [Content]
    public AvaloniaList<MetricsDashboardMetric> Metrics { get; } = new();

    /// <summary>The ranges in days, in order (7, 30, 90).</summary>
    public AvaloniaList<int> Ranges { get; } = new() { 7, 30, 90 };

    /// <summary>The first ranked list (pages), largest first, when no <see cref="PrimaryItemsSource"/> is set.</summary>
    public AvaloniaList<MetricsDashboardItem> PrimaryItems { get; } = new();

    /// <summary>The second ranked list (sources), largest first, when no <see cref="SecondaryItemsSource"/> is set.</summary>
    public AvaloniaList<MetricsDashboardItem> SecondaryItems { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SourceProperty"/>
    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="SubtitleFormatProperty"/>
    public string SubtitleFormat { get => GetValue(SubtitleFormatProperty); set => SetValue(SubtitleFormatProperty, value); }

    /// <inheritdoc cref="SubtitleRangeFormatProperty"/>
    public string SubtitleRangeFormat { get => GetValue(SubtitleRangeFormatProperty); set => SetValue(SubtitleRangeFormatProperty, value); }

    /// <inheritdoc cref="MetricsSourceProperty"/>
    public IEnumerable? MetricsSource { get => GetValue(MetricsSourceProperty); set => SetValue(MetricsSourceProperty, value); }

    /// <inheritdoc cref="SelectedMetricIndexProperty"/>
    public int SelectedMetricIndex { get => GetValue(SelectedMetricIndexProperty); set => SetValue(SelectedMetricIndexProperty, value); }

    /// <inheritdoc cref="SelectedRangeProperty"/>
    public int SelectedRange { get => GetValue(SelectedRangeProperty); set => SetValue(SelectedRangeProperty, value); }

    /// <inheritdoc cref="WeeklyThresholdProperty"/>
    public int WeeklyThreshold { get => GetValue(WeeklyThresholdProperty); set => SetValue(WeeklyThresholdProperty, value); }

    /// <inheritdoc cref="EndDateProperty"/>
    public DateOnly? EndDate { get => GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }

    /// <inheritdoc cref="RangeLabelFormatProperty"/>
    public string RangeLabelFormat { get => GetValue(RangeLabelFormatProperty); set => SetValue(RangeLabelFormatProperty, value); }

    /// <inheritdoc cref="RangeNameFormatProperty"/>
    public string RangeNameFormat { get => GetValue(RangeNameFormatProperty); set => SetValue(RangeNameFormatProperty, value); }

    /// <inheritdoc cref="RangeGroupNameProperty"/>
    public string RangeGroupName { get => GetValue(RangeGroupNameProperty); set => SetValue(RangeGroupNameProperty, value); }

    /// <inheritdoc cref="MetricGroupNameProperty"/>
    public string MetricGroupName { get => GetValue(MetricGroupNameProperty); set => SetValue(MetricGroupNameProperty, value); }

    /// <inheritdoc cref="IsCompareEnabledProperty"/>
    public bool IsCompareEnabled { get => GetValue(IsCompareEnabledProperty); set => SetValue(IsCompareEnabledProperty, value); }

    /// <inheritdoc cref="IsCompareVisibleProperty"/>
    public bool IsCompareVisible { get => GetValue(IsCompareVisibleProperty); set => SetValue(IsCompareVisibleProperty, value); }

    /// <inheritdoc cref="CompareTextProperty"/>
    public string CompareText { get => GetValue(CompareTextProperty); set => SetValue(CompareTextProperty, value); }

    /// <inheritdoc cref="PreviousLegendTextProperty"/>
    public string PreviousLegendText { get => GetValue(PreviousLegendTextProperty); set => SetValue(PreviousLegendTextProperty, value); }

    /// <inheritdoc cref="PreviousValueFormatProperty"/>
    public string PreviousValueFormat { get => GetValue(PreviousValueFormatProperty); set => SetValue(PreviousValueFormatProperty, value); }

    /// <inheritdoc cref="DateFormatProperty"/>
    public string DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <inheritdoc cref="WeekTitleFormatProperty"/>
    public string WeekTitleFormat { get => GetValue(WeekTitleFormatProperty); set => SetValue(WeekTitleFormatProperty, value); }

    /// <inheritdoc cref="PartialWeekTitleFormatProperty"/>
    public string PartialWeekTitleFormat { get => GetValue(PartialWeekTitleFormatProperty); set => SetValue(PartialWeekTitleFormatProperty, value); }

    /// <inheritdoc cref="ChartNameFormatProperty"/>
    public string ChartNameFormat { get => GetValue(ChartNameFormatProperty); set => SetValue(ChartNameFormatProperty, value); }

    /// <inheritdoc cref="ChartHelpTextProperty"/>
    public string ChartHelpText { get => GetValue(ChartHelpTextProperty); set => SetValue(ChartHelpTextProperty, value); }

    /// <inheritdoc cref="ChangeContextTextProperty"/>
    public string ChangeContextText { get => GetValue(ChangeContextTextProperty); set => SetValue(ChangeContextTextProperty, value); }

    /// <inheritdoc cref="PrimaryItemsSourceProperty"/>
    public IEnumerable? PrimaryItemsSource { get => GetValue(PrimaryItemsSourceProperty); set => SetValue(PrimaryItemsSourceProperty, value); }

    /// <inheritdoc cref="SecondaryItemsSourceProperty"/>
    public IEnumerable? SecondaryItemsSource { get => GetValue(SecondaryItemsSourceProperty); set => SetValue(SecondaryItemsSourceProperty, value); }

    /// <inheritdoc cref="PrimaryListTitleProperty"/>
    public string? PrimaryListTitle { get => GetValue(PrimaryListTitleProperty); set => SetValue(PrimaryListTitleProperty, value); }

    /// <inheritdoc cref="SecondaryListTitleProperty"/>
    public string? SecondaryListTitle { get => GetValue(SecondaryListTitleProperty); set => SetValue(SecondaryListTitleProperty, value); }

    /// <inheritdoc cref="ListValueHeaderProperty"/>
    public string? ListValueHeader { get => GetValue(ListValueHeaderProperty); set => SetValue(ListValueHeaderProperty, value); }

    /// <inheritdoc cref="ListValueFormatterProperty"/>
    public Func<double, string>? ListValueFormatter { get => GetValue(ListValueFormatterProperty); set => SetValue(ListValueFormatterProperty, value); }

    /// <inheritdoc cref="IsExportVisibleProperty"/>
    public bool IsExportVisible { get => GetValue(IsExportVisibleProperty); set => SetValue(IsExportVisibleProperty, value); }

    /// <inheritdoc cref="ExportTextProperty"/>
    public string ExportText { get => GetValue(ExportTextProperty); set => SetValue(ExportTextProperty, value); }

    /// <inheritdoc cref="ExportedTextProperty"/>
    public string ExportedText { get => GetValue(ExportedTextProperty); set => SetValue(ExportedTextProperty, value); }

    /// <inheritdoc cref="ExportCommandProperty"/>
    public ICommand? ExportCommand { get => GetValue(ExportCommandProperty); set => SetValue(ExportCommandProperty, value); }

    /// <inheritdoc cref="ExportConfirmDurationProperty"/>
    public TimeSpan ExportConfirmDuration { get => GetValue(ExportConfirmDurationProperty); set => SetValue(ExportConfirmDurationProperty, value); }

    /// <inheritdoc cref="ActiveIndexProperty"/>
    public int ActiveIndex { get => _activeIndex; private set => SetAndRaise(ActiveIndexProperty, ref _activeIndex, value); }

    /// <inheritdoc cref="LayoutSizeProperty"/>
    public MetricsDashboardSize LayoutSize { get => _layoutSize; private set => SetAndRaise(LayoutSizeProperty, ref _layoutSize, value); }

    /// <inheritdoc cref="IsExportConfirmedProperty"/>
    public bool IsExportConfirmed { get => _isExportConfirmed; private set => SetAndRaise(IsExportConfirmedProperty, ref _isExportConfirmed, value); }
}
