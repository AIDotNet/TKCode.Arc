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
/// Metric explorer: a row of KPI cards (label, big number, change, sparkline) where any card opens into a chart you can
/// inspect. The card's surface grows into the chart's panel: metric tabs with their values, the readout (name, big number,
/// change with rolling digits), the range switch, a legend with the date span, and the chart itself: this period as a line
/// with the period before it dashed beside it. Pointing at the chart, dragging, or using the arrow keys shows the nearest day
/// with a crosshair and a date capsule, and swaps the readout to that day against the same day one period earlier. Switching
/// metric morphs the line point by point; switching range zooms the time window like a camera. Escape clears the
/// inspection, then closes the chart.
/// <code>
/// &lt;arc:MetricExplorer Title="Workspace health" Caption="Updated Oct 14, 2026"&gt;
///     &lt;arc:ExplorerMetric Title="Annual run-rate" ShortTitle="ARR" ValueFormat="CurrencyCompact" Values="{Binding Arr}" /&gt;
/// &lt;/arc:MetricExplorer&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys: Enter or Space on a card opens it (focus moves to its tab); on the tabs and the range switch ←/→ select (wrapping),
/// Home / End jump; on the chart ←/↓ and →/↑ step a day, Home / End jump to the first and last day, Escape clears the
/// inspection and then closes the chart (focus returns to the card).
/// </remarks>
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(StagePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(OpenPseudoClass, NarrowPseudoClass, CompactPseudoClass, TwoColumnPseudoClass, ScrubbingPseudoClass)]
public partial class MetricExplorer : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string StagePart = "PART_Stage";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the chart is open.</summary>
    public const string OpenPseudoClass = ":open";

    /// <summary>Set while the block is narrower than 640: paddings 16, no tab values, the range switch under the readout.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is 440 wide or less.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is narrower than 760: the cards take two columns.</summary>
    public const string TwoColumnPseudoClass = ":two-column";

    /// <summary>Set while a day of the chart is inspected.</summary>
    public const string ScrubbingPseudoClass = ":scrubbing";

    #region Properties

    /// <summary>The heading above the cards; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MetricExplorer, string?>(nameof(Title));

    /// <summary>The small text on the heading's right (workspace and refresh time); null leaves it out.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<MetricExplorer, string?>(nameof(Caption));

    /// <summary>The metrics as a list you own (followed when it raises collection changes); it wins over <see cref="Metrics"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MetricsSourceProperty =
        AvaloniaProperty.Register<MetricExplorer, IEnumerable?>(nameof(MetricsSource));

    /// <summary>The ranges as a list you own; it wins over <see cref="Ranges"/>. Empty means 30 days, 90 days and 12 months.</summary>
    public static readonly StyledProperty<IEnumerable?> RangesSourceProperty =
        AvaloniaProperty.Register<MetricExplorer, IEnumerable?>(nameof(RangesSource));

    /// <summary>The metric the chart shows or opens on (two-way, index into the effective list).</summary>
    public static readonly StyledProperty<int> SelectedMetricIndexProperty =
        AvaloniaProperty.Register<MetricExplorer, int>(nameof(SelectedMetricIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The range the cards and the chart use (two-way, index into the effective ranges).</summary>
    public static readonly StyledProperty<int> SelectedRangeIndexProperty =
        AvaloniaProperty.Register<MetricExplorer, int>(nameof(SelectedRangeIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the chart is open (two-way): the cards open into it and it shrinks back into the card.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MetricExplorer, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the previous period's dashed line (and its inspection dot) is drawn.</summary>
    public static readonly StyledProperty<bool> ShowPreviousPeriodProperty =
        AvaloniaProperty.Register<MetricExplorer, bool>(nameof(ShowPreviousPeriod), true);

    /// <summary>The legend's text for the solid line.</summary>
    public static readonly StyledProperty<string> ThisPeriodTextProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(ThisPeriodText), "This period");

    /// <summary>The legend's text for the dashed line.</summary>
    public static readonly StyledProperty<string> PreviousPeriodTextProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(PreviousPeriodText), "Previous period");

    /// <summary>The close button's screen reader name.</summary>
    public static readonly StyledProperty<string> CloseTextProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(CloseText), "Close chart");

    /// <summary>The screen reader name of the metric tabs.</summary>
    public static readonly StyledProperty<string> TabsLabelProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(TabsLabel), "Metric");

    /// <summary>The screen reader name of the range switch.</summary>
    public static readonly StyledProperty<string> RangeLabelProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(RangeLabel), "Chart range");

    /// <summary>The start of the chart's screen reader name ("Scrub" + the metric).</summary>
    public static readonly StyledProperty<string> ScrubLabelProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(ScrubLabel), "Scrub");

    /// <summary>The chart's value text for the inspected day: {0} its date, {1} its value, {2} the date a period earlier, {3} that value.</summary>
    public static readonly StyledProperty<string> ScrubValueFormatProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(ScrubValueFormat), "{0}: {1}. {2}: {3}");

    /// <summary>The chart's summary for screen readers: {0} metric, {1} days, {2} first value, {3} last value.</summary>
    public static readonly StyledProperty<string> SummaryFormatProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(SummaryFormat), "{0} over the last {1} days, from {2} to {3}.");

    /// <summary>The comparison in the readout while a day is inspected: {0} that day, {1} the day a period earlier ("Sep 6 vs Aug 7").</summary>
    public static readonly StyledProperty<string> ScrubCompareFormatProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(ScrubCompareFormat), "{0} vs {1}");

    /// <summary>A card's spoken name: {0} title, {1} value, {2} up / down, {3} change, {4} the range ("30 days").</summary>
    public static readonly StyledProperty<string> CardNameFormatProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(CardNameFormat), "{0}: {1}, {2} {3} over {4}. Open chart");

    /// <summary>The unit spoken after a change in points.</summary>
    public static readonly StyledProperty<string> PointsUnitProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(PointsUnit), " points");

    /// <summary>Said when the metric or the range changes in the open chart: {0} metric, {1} range, {2} value, {3} up / down, {4} change, {5} comparison.</summary>
    public static readonly StyledProperty<string> AnnouncementFormatProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(AnnouncementFormat), "{0}, last {1}: {2}, {3} {4} {5}");

    /// <summary>Said when the chart closes: {0} the number of metrics.</summary>
    public static readonly StyledProperty<string> ClosedAnnouncementFormatProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(ClosedAnnouncementFormat), "Chart closed. {0} metrics shown.");

    /// <summary>The spoken word for a rise.</summary>
    public static readonly StyledProperty<string> UpTextProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(UpText), "up");

    /// <summary>The spoken word for a fall.</summary>
    public static readonly StyledProperty<string> DownTextProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(DownText), "down");

    /// <summary>The spoken word for no change.</summary>
    public static readonly StyledProperty<string> FlatTextProperty =
        AvaloniaProperty.Register<MetricExplorer, string>(nameof(FlatText), "unchanged");

    /// <summary>Run when a chart opens, with its <see cref="MetricExplorerMetricEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> OpenedCommandProperty =
        AvaloniaProperty.Register<MetricExplorer, ICommand?>(nameof(OpenedCommand));

    /// <summary>Run when the chart closes, with its <see cref="MetricExplorerMetricEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> ClosedCommandProperty =
        AvaloniaProperty.Register<MetricExplorer, ICommand?>(nameof(ClosedCommand));

    /// <summary>Run when the range changes, with its <see cref="MetricExplorerRangeEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> RangeChangedCommandProperty =
        AvaloniaProperty.Register<MetricExplorer, ICommand?>(nameof(RangeChangedCommand));

    /// <summary>The inspected day of the chart (0 = first day of the range); null when none.</summary>
    public static readonly DirectProperty<MetricExplorer, int?> ScrubIndexProperty =
        AvaloniaProperty.RegisterDirect<MetricExplorer, int?>(nameof(ScrubIndex), o => o.ScrubIndex);

    #endregion

    #region Events

    /// <summary>Raised when the chart opens.</summary>
    public static readonly RoutedEvent<MetricExplorerMetricEventArgs> MetricOpenedEvent =
        RoutedEvent.Register<MetricExplorer, MetricExplorerMetricEventArgs>(nameof(MetricOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the chart closes.</summary>
    public static readonly RoutedEvent<MetricExplorerMetricEventArgs> MetricClosedEvent =
        RoutedEvent.Register<MetricExplorer, MetricExplorerMetricEventArgs>(nameof(MetricClosed), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedMetricIndex"/> changes.</summary>
    public static readonly RoutedEvent<MetricExplorerMetricEventArgs> SelectedMetricChangedEvent =
        RoutedEvent.Register<MetricExplorer, MetricExplorerMetricEventArgs>(nameof(SelectedMetricChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedRangeIndex"/> changes.</summary>
    public static readonly RoutedEvent<MetricExplorerRangeEventArgs> SelectedRangeChangedEvent =
        RoutedEvent.Register<MetricExplorer, MetricExplorerRangeEventArgs>(nameof(SelectedRangeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the inspected day changes or is cleared.</summary>
    public static readonly RoutedEvent<MetricExplorerScrubEventArgs> ScrubChangedEvent =
        RoutedEvent.Register<MetricExplorer, MetricExplorerScrubEventArgs>(nameof(ScrubChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="MetricOpenedEvent"/>
    public event EventHandler<MetricExplorerMetricEventArgs>? MetricOpened
    {
        add => AddHandler(MetricOpenedEvent, value);
        remove => RemoveHandler(MetricOpenedEvent, value);
    }

    /// <inheritdoc cref="MetricClosedEvent"/>
    public event EventHandler<MetricExplorerMetricEventArgs>? MetricClosed
    {
        add => AddHandler(MetricClosedEvent, value);
        remove => RemoveHandler(MetricClosedEvent, value);
    }

    /// <inheritdoc cref="SelectedMetricChangedEvent"/>
    public event EventHandler<MetricExplorerMetricEventArgs>? SelectedMetricChanged
    {
        add => AddHandler(SelectedMetricChangedEvent, value);
        remove => RemoveHandler(SelectedMetricChangedEvent, value);
    }

    /// <inheritdoc cref="SelectedRangeChangedEvent"/>
    public event EventHandler<MetricExplorerRangeEventArgs>? SelectedRangeChanged
    {
        add => AddHandler(SelectedRangeChangedEvent, value);
        remove => RemoveHandler(SelectedRangeChangedEvent, value);
    }

    /// <inheritdoc cref="ScrubChangedEvent"/>
    public event EventHandler<MetricExplorerScrubEventArgs>? ScrubChanged
    {
        add => AddHandler(ScrubChangedEvent, value);
        remove => RemoveHandler(ScrubChangedEvent, value);
    }

    #endregion

    #region Accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="CaptionProperty"/>
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <inheritdoc cref="MetricsSourceProperty"/>
    public IEnumerable? MetricsSource { get => GetValue(MetricsSourceProperty); set => SetValue(MetricsSourceProperty, value); }

    /// <summary>The metrics, in order (ignored while <see cref="MetricsSource"/> is set).</summary>
    [Content]
    public AvaloniaList<ExplorerMetric> Metrics { get; } = new();

    /// <inheritdoc cref="RangesSourceProperty"/>
    public IEnumerable? RangesSource { get => GetValue(RangesSourceProperty); set => SetValue(RangesSourceProperty, value); }

    /// <summary>The ranges the switch offers, in order (ignored while <see cref="RangesSource"/> is set); empty means the three defaults.</summary>
    public AvaloniaList<ExplorerRange> Ranges { get; } = new();

    /// <inheritdoc cref="SelectedMetricIndexProperty"/>
    public int SelectedMetricIndex { get => GetValue(SelectedMetricIndexProperty); set => SetValue(SelectedMetricIndexProperty, value); }

    /// <inheritdoc cref="SelectedRangeIndexProperty"/>
    public int SelectedRangeIndex { get => GetValue(SelectedRangeIndexProperty); set => SetValue(SelectedRangeIndexProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="ShowPreviousPeriodProperty"/>
    public bool ShowPreviousPeriod { get => GetValue(ShowPreviousPeriodProperty); set => SetValue(ShowPreviousPeriodProperty, value); }

    /// <inheritdoc cref="ThisPeriodTextProperty"/>
    public string ThisPeriodText { get => GetValue(ThisPeriodTextProperty); set => SetValue(ThisPeriodTextProperty, value); }

    /// <inheritdoc cref="PreviousPeriodTextProperty"/>
    public string PreviousPeriodText { get => GetValue(PreviousPeriodTextProperty); set => SetValue(PreviousPeriodTextProperty, value); }

    /// <inheritdoc cref="CloseTextProperty"/>
    public string CloseText { get => GetValue(CloseTextProperty); set => SetValue(CloseTextProperty, value); }

    /// <inheritdoc cref="TabsLabelProperty"/>
    public string TabsLabel { get => GetValue(TabsLabelProperty); set => SetValue(TabsLabelProperty, value); }

    /// <inheritdoc cref="RangeLabelProperty"/>
    public string RangeLabel { get => GetValue(RangeLabelProperty); set => SetValue(RangeLabelProperty, value); }

    /// <inheritdoc cref="ScrubLabelProperty"/>
    public string ScrubLabel { get => GetValue(ScrubLabelProperty); set => SetValue(ScrubLabelProperty, value); }

    /// <inheritdoc cref="ScrubValueFormatProperty"/>
    public string ScrubValueFormat { get => GetValue(ScrubValueFormatProperty); set => SetValue(ScrubValueFormatProperty, value); }

    /// <inheritdoc cref="SummaryFormatProperty"/>
    public string SummaryFormat { get => GetValue(SummaryFormatProperty); set => SetValue(SummaryFormatProperty, value); }

    /// <inheritdoc cref="ScrubCompareFormatProperty"/>
    public string ScrubCompareFormat { get => GetValue(ScrubCompareFormatProperty); set => SetValue(ScrubCompareFormatProperty, value); }

    /// <inheritdoc cref="CardNameFormatProperty"/>
    public string CardNameFormat { get => GetValue(CardNameFormatProperty); set => SetValue(CardNameFormatProperty, value); }

    /// <inheritdoc cref="PointsUnitProperty"/>
    public string PointsUnit { get => GetValue(PointsUnitProperty); set => SetValue(PointsUnitProperty, value); }

    /// <inheritdoc cref="AnnouncementFormatProperty"/>
    public string AnnouncementFormat { get => GetValue(AnnouncementFormatProperty); set => SetValue(AnnouncementFormatProperty, value); }

    /// <inheritdoc cref="ClosedAnnouncementFormatProperty"/>
    public string ClosedAnnouncementFormat { get => GetValue(ClosedAnnouncementFormatProperty); set => SetValue(ClosedAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="UpTextProperty"/>
    public string UpText { get => GetValue(UpTextProperty); set => SetValue(UpTextProperty, value); }

    /// <inheritdoc cref="DownTextProperty"/>
    public string DownText { get => GetValue(DownTextProperty); set => SetValue(DownTextProperty, value); }

    /// <inheritdoc cref="FlatTextProperty"/>
    public string FlatText { get => GetValue(FlatTextProperty); set => SetValue(FlatTextProperty, value); }

    /// <inheritdoc cref="OpenedCommandProperty"/>
    public ICommand? OpenedCommand { get => GetValue(OpenedCommandProperty); set => SetValue(OpenedCommandProperty, value); }

    /// <inheritdoc cref="ClosedCommandProperty"/>
    public ICommand? ClosedCommand { get => GetValue(ClosedCommandProperty); set => SetValue(ClosedCommandProperty, value); }

    /// <inheritdoc cref="RangeChangedCommandProperty"/>
    public ICommand? RangeChangedCommand { get => GetValue(RangeChangedCommandProperty); set => SetValue(RangeChangedCommandProperty, value); }

    /// <inheritdoc cref="ScrubIndexProperty"/>
    public int? ScrubIndex { get => _scrubIndex; private set => SetAndRaise(ScrubIndexProperty, ref _scrubIndex, value); }

    #endregion

    private int? _scrubIndex;
}
