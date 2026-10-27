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
/// Revenue explorer: monthly recurring revenue as three stacked series, new and expansion above the zero line and churn
/// below it, with three headline numbers (closing revenue, average net new, churn rate) and an overview strip of every
/// month under the chart. Drag the overview window, or its handles, to zoom the chart: the paths, the axes and the numbers
/// slide to the new range. The range buttons pick the newest 3, 6, 12 months or everything, the chips switch series on and
/// off, "compare" draws the previous period of the same length as dashed lines, and a crosshair with a readout panel
/// inspects one month for the pointer and the keyboard.
/// <code>
/// &lt;arc:RevenueExplorer Title="Subscription revenue" OpeningMrr="1050000"&gt;
///     &lt;arc:RevenueMonth Month="2025-03-01" New="41200" Expansion="14800" Churn="-17600" /&gt;
/// &lt;/arc:RevenueExplorer&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys: Tab visits the range buttons (one stop, ← → change the range), the three chips, the compare switch, the chart
/// (the readout opens on the last month; ← → step, Home / End jump, Escape closes), then the overview window and its two
/// handles (← → one month, Shift three, Home / End to the ends).
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(Control))]
[TemplatePart(PresetsPart, typeof(Control))]
[TemplatePart(MetricsPart, typeof(Control))]
[TemplatePart(ToolbarPart, typeof(Panel))]
[TemplatePart(NewChipPart, typeof(Control))]
[TemplatePart(ExpansionChipPart, typeof(Control))]
[TemplatePart(ChurnChipPart, typeof(Control))]
[TemplatePart(CompareSwitchPart, typeof(Control))]
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(OverviewPart, typeof(Control))]
[TemplatePart(TablePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(ComparePseudoClass, EmptySeriesPseudoClass, DraggingPseudoClass, CompactPseudoClass, TightPseudoClass, StackedPseudoClass)]
public partial class RevenueExplorer : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string PresetsPart = "PART_Presets";
    public const string MetricsPart = "PART_Metrics";
    public const string ToolbarPart = "PART_Toolbar";
    public const string NewChipPart = "PART_NewChip";
    public const string ExpansionChipPart = "PART_ExpansionChip";
    public const string ChurnChipPart = "PART_ChurnChip";
    public const string CompareSwitchPart = "PART_Compare";
    public const string PlotPart = "PART_Plot";
    public const string OverviewPart = "PART_Overview";
    public const string TablePart = "PART_Table";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the previous period is compared.</summary>
    public const string ComparePseudoClass = ":compare";

    /// <summary>Set while every series is off.</summary>
    public const string EmptySeriesPseudoClass = ":empty-series";

    /// <summary>Set while the overview window or a handle is held.</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while the chart is narrower than 600: a shorter plot.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is narrower than 560: paddings 16.</summary>
    public const string TightPseudoClass = ":tight";

    /// <summary>Set while the block is narrower than 440: the range buttons take a row, the numbers stack, the chips shrink.</summary>
    public const string StackedPseudoClass = ":stacked";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RevenueExplorer, string?>(nameof(Title));

    /// <summary>The months as a list you own (followed when it raises collection changes); it wins over <see cref="Months"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MonthsSourceProperty =
        AvaloniaProperty.Register<RevenueExplorer, IEnumerable?>(nameof(MonthsSource));

    /// <summary>Recurring revenue before the first month: the base of the first month's churn rate and of derived closing revenue. NaN when unknown.</summary>
    public static readonly StyledProperty<double> OpeningMrrProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(OpeningMrr), double.NaN);

    /// <summary>Index of the first month of the range (two-way). Negative until a range is chosen: the newest <see cref="DefaultRangeMonths"/> months.</summary>
    public static readonly StyledProperty<int> RangeStartProperty =
        AvaloniaProperty.Register<RevenueExplorer, int>(nameof(RangeStart), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Index of the last month of the range, included (two-way). Negative until a range is chosen.</summary>
    public static readonly StyledProperty<int> RangeEndProperty =
        AvaloniaProperty.Register<RevenueExplorer, int>(nameof(RangeEnd), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many of the newest months show before a range is chosen (12).</summary>
    public static readonly StyledProperty<int> DefaultRangeMonthsProperty =
        AvaloniaProperty.Register<RevenueExplorer, int>(nameof(DefaultRangeMonths), 12);

    /// <summary>The fewest months the window can hold (3).</summary>
    public static readonly StyledProperty<int> MinRangeMonthsProperty =
        AvaloniaProperty.Register<RevenueExplorer, int>(nameof(MinRangeMonths), 3);

    /// <summary>Whether the new series is shown (two-way).</summary>
    public static readonly StyledProperty<bool> IsNewVisibleProperty =
        AvaloniaProperty.Register<RevenueExplorer, bool>(nameof(IsNewVisible), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the expansion series is shown (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpansionVisibleProperty =
        AvaloniaProperty.Register<RevenueExplorer, bool>(nameof(IsExpansionVisible), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the churn series is shown (two-way).</summary>
    public static readonly StyledProperty<bool> IsChurnVisibleProperty =
        AvaloniaProperty.Register<RevenueExplorer, bool>(nameof(IsChurnVisible), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the previous period of the same length is drawn as dashed lines (two-way).</summary>
    public static readonly StyledProperty<bool> IsCompareEnabledProperty =
        AvaloniaProperty.Register<RevenueExplorer, bool>(nameof(IsCompareEnabled), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The inspected month (crosshair and readout): its index, or null for none (two-way).</summary>
    public static readonly StyledProperty<int?> ScrubIndexProperty =
        AvaloniaProperty.Register<RevenueExplorer, int?>(nameof(ScrubIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the range changes, with the <see cref="RevenueRangeChangedEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> RangeCommandProperty =
        AvaloniaProperty.Register<RevenueExplorer, ICommand?>(nameof(RangeCommand));

    /// <summary>The new series' name (chip, readout, table).</summary>
    public static readonly StyledProperty<string> NewLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(NewLabel), "New");

    /// <summary>The expansion series' name.</summary>
    public static readonly StyledProperty<string> ExpansionLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(ExpansionLabel), "Expansion");

    /// <summary>The churn series' name.</summary>
    public static readonly StyledProperty<string> ChurnLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(ChurnLabel), "Churn");

    /// <summary>The readout's total row and the table's net column.</summary>
    public static readonly StyledProperty<string> NetNewLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(NetNewLabel), "Net new");

    /// <summary>The first headline number's label (closing recurring revenue); also the table's last column.</summary>
    public static readonly StyledProperty<string> MrrLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(MrrLabel), "MRR");

    /// <summary>The second headline number's label.</summary>
    public static readonly StyledProperty<string> NetNewKpiLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(NetNewKpiLabel), "Net new per month");

    /// <summary>The third headline number's label.</summary>
    public static readonly StyledProperty<string> ChurnRateLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(ChurnRateLabel), "Churn rate");

    /// <summary>The compare switch's text.</summary>
    public static readonly StyledProperty<string> CompareTextProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(CompareText), "Compare to previous period");

    /// <summary>Shown in the chart when every series is off.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(EmptyText), "Turn on a series to see revenue");

    /// <summary>The text after the closing revenue's change; {0} is the month before the range.</summary>
    public static readonly StyledProperty<string> SinceFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(SinceFormat), "since {0}");

    /// <summary>The text after the other two changes; {0} is the number of months of the range.</summary>
    public static readonly StyledProperty<string> VersusFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(VersusFormat), "vs previous {0} months");

    /// <summary>The readout's comparison row while comparing; {0} is the previous period's month.</summary>
    public static readonly StyledProperty<string> PreviousRowFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(PreviousRowFormat), "vs {0}");

    /// <summary>The screen reader name of the range buttons.</summary>
    public static readonly StyledProperty<string> PresetGroupLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(PresetGroupLabel), "Range");

    /// <summary>The accessible name of a preset button; {0} is its number of months.</summary>
    public static readonly StyledProperty<string> PresetNameFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(PresetNameFormat), "Last {0} months");

    /// <summary>The accessible name of the preset that selects everything; {0} is the number of months.</summary>
    public static readonly StyledProperty<string> AllPresetNameFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(AllPresetNameFormat), "All {0} months");

    /// <summary>The screen reader name of the series chips.</summary>
    public static readonly StyledProperty<string> SeriesGroupLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(SeriesGroupLabel), "Series");

    /// <summary>The screen reader name of the chart (a slider over the months of the range).</summary>
    public static readonly StyledProperty<string> PlotLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(PlotLabel), "Inspect a month");

    /// <summary>The screen reader name of the overview window.</summary>
    public static readonly StyledProperty<string> WindowLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(WindowLabel), "Selected range");

    /// <summary>The screen reader name of the left handle.</summary>
    public static readonly StyledProperty<string> StartHandleLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(StartHandleLabel), "Range start");

    /// <summary>The screen reader name of the right handle.</summary>
    public static readonly StyledProperty<string> EndHandleLabelProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(EndHandleLabel), "Range end");

    /// <summary>The screen reader table's caption; {0} is the range text.</summary>
    public static readonly StyledProperty<string> TableCaptionFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(TableCaptionFormat), "Monthly revenue movements, {0}");

    /// <summary>The chart's screen reader summary; {0} range, {1} closing label, {2} closing value, {3} average net new.</summary>
    public static readonly StyledProperty<string> ChartSummaryFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(ChartSummaryFormat), "{0}. Closing {1}: {2}. Average net new per month: {3}.");

    /// <summary>Said when the range changes by keyboard; {0} is the range text.</summary>
    public static readonly StyledProperty<string> RangeAnnouncementFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(RangeAnnouncementFormat), "Showing {0}");

    /// <summary>What a screen reader hears for the inspected month.</summary>
    public static readonly StyledProperty<string> ScrubFormatProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(ScrubFormat), "{0}: new {1}, expansion {2}, churn {3}, net new {4}");

    /// <summary>The currency symbol of the default amount formats and the headline numbers.</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(CurrencySymbol), "$");

    /// <summary>The culture of month names and numbers.</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<RevenueExplorer, string>(nameof(CultureName), "en-US");

    /// <summary>Formats an amount in the readout, the table and the announcements; null gives compact currency ("$63.3k").</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<RevenueExplorer, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Formats the y axis labels; null gives compact currency with a true minus ("−$25k").</summary>
    public static readonly StyledProperty<Func<double, string>?> AxisFormatterProperty =
        AvaloniaProperty.Register<RevenueExplorer, Func<double, string>?>(nameof(AxisFormatter));

    /// <summary>Formats a month on the x axis; null gives the short month name, and the year for January.</summary>
    public static readonly StyledProperty<Func<DateOnly, string>?> MonthFormatterProperty =
        AvaloniaProperty.Register<RevenueExplorer, Func<DateOnly, string>?>(nameof(MonthFormatter));

    /// <summary>Formats a month with its year (readout title, table rows, "since" text); null gives "Mar 2026".</summary>
    public static readonly StyledProperty<Func<DateOnly, string>?> MonthYearFormatterProperty =
        AvaloniaProperty.Register<RevenueExplorer, Func<DateOnly, string>?>(nameof(MonthYearFormatter));

    /// <summary>Formats the range under the title; null gives "Sep 2025 – Aug 2026" ("Mar – Aug 2026" inside one year).</summary>
    public static readonly StyledProperty<Func<DateOnly, DateOnly, string>?> RangeFormatterProperty =
        AvaloniaProperty.Register<RevenueExplorer, Func<DateOnly, DateOnly, string>?>(nameof(RangeFormatter));

    #endregion

    #region Events

    /// <summary>Raised when the range changes (a preset, a drag, keys, a click or code).</summary>
    public static readonly RoutedEvent<RevenueRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<RevenueExplorer, RevenueRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a series is switched on or off.</summary>
    public static readonly RoutedEvent<RevenueSeriesChangedEventArgs> SeriesVisibilityChangedEvent =
        RoutedEvent.Register<RevenueExplorer, RevenueSeriesChangedEventArgs>(nameof(SeriesVisibilityChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the comparison with the previous period is switched.</summary>
    public static readonly RoutedEvent<RevenueCompareChangedEventArgs> CompareChangedEvent =
        RoutedEvent.Register<RevenueExplorer, RevenueCompareChangedEventArgs>(nameof(CompareChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the inspected month changes or closes.</summary>
    public static readonly RoutedEvent<RevenueScrubChangedEventArgs> ScrubChangedEvent =
        RoutedEvent.Register<RevenueExplorer, RevenueScrubChangedEventArgs>(nameof(ScrubChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="RangeChangedEvent"/>
    public event EventHandler<RevenueRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    /// <inheritdoc cref="SeriesVisibilityChangedEvent"/>
    public event EventHandler<RevenueSeriesChangedEventArgs>? SeriesVisibilityChanged
    {
        add => AddHandler(SeriesVisibilityChangedEvent, value);
        remove => RemoveHandler(SeriesVisibilityChangedEvent, value);
    }

    /// <inheritdoc cref="CompareChangedEvent"/>
    public event EventHandler<RevenueCompareChangedEventArgs>? CompareChanged
    {
        add => AddHandler(CompareChangedEvent, value);
        remove => RemoveHandler(CompareChangedEvent, value);
    }

    /// <inheritdoc cref="ScrubChangedEvent"/>
    public event EventHandler<RevenueScrubChangedEventArgs>? ScrubChanged
    {
        add => AddHandler(ScrubChangedEvent, value);
        remove => RemoveHandler(ScrubChangedEvent, value);
    }

    #endregion

    #region Accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="MonthsSourceProperty"/>
    public IEnumerable? MonthsSource { get => GetValue(MonthsSourceProperty); set => SetValue(MonthsSourceProperty, value); }

    /// <summary>The months, oldest first, one per calendar month without gaps (ignored while <see cref="MonthsSource"/> is set).</summary>
    [Content]
    public AvaloniaList<RevenueMonth> Months { get; } = new();

    /// <summary>The ranges the buttons select; empty uses 3, 6 and 12 months and "All".</summary>
    public AvaloniaList<RevenueRangePreset> Presets { get; } = new();

    /// <inheritdoc cref="OpeningMrrProperty"/>
    public double OpeningMrr { get => GetValue(OpeningMrrProperty); set => SetValue(OpeningMrrProperty, value); }

    /// <inheritdoc cref="RangeStartProperty"/>
    public int RangeStart { get => GetValue(RangeStartProperty); set => SetValue(RangeStartProperty, value); }

    /// <inheritdoc cref="RangeEndProperty"/>
    public int RangeEnd { get => GetValue(RangeEndProperty); set => SetValue(RangeEndProperty, value); }

    /// <inheritdoc cref="DefaultRangeMonthsProperty"/>
    public int DefaultRangeMonths { get => GetValue(DefaultRangeMonthsProperty); set => SetValue(DefaultRangeMonthsProperty, value); }

    /// <inheritdoc cref="MinRangeMonthsProperty"/>
    public int MinRangeMonths { get => GetValue(MinRangeMonthsProperty); set => SetValue(MinRangeMonthsProperty, value); }

    /// <inheritdoc cref="IsNewVisibleProperty"/>
    public bool IsNewVisible { get => GetValue(IsNewVisibleProperty); set => SetValue(IsNewVisibleProperty, value); }

    /// <inheritdoc cref="IsExpansionVisibleProperty"/>
    public bool IsExpansionVisible { get => GetValue(IsExpansionVisibleProperty); set => SetValue(IsExpansionVisibleProperty, value); }

    /// <inheritdoc cref="IsChurnVisibleProperty"/>
    public bool IsChurnVisible { get => GetValue(IsChurnVisibleProperty); set => SetValue(IsChurnVisibleProperty, value); }

    /// <inheritdoc cref="IsCompareEnabledProperty"/>
    public bool IsCompareEnabled { get => GetValue(IsCompareEnabledProperty); set => SetValue(IsCompareEnabledProperty, value); }

    /// <inheritdoc cref="ScrubIndexProperty"/>
    public int? ScrubIndex { get => GetValue(ScrubIndexProperty); set => SetValue(ScrubIndexProperty, value); }

    /// <inheritdoc cref="RangeCommandProperty"/>
    public ICommand? RangeCommand { get => GetValue(RangeCommandProperty); set => SetValue(RangeCommandProperty, value); }

    /// <inheritdoc cref="NewLabelProperty"/>
    public string NewLabel { get => GetValue(NewLabelProperty); set => SetValue(NewLabelProperty, value); }

    /// <inheritdoc cref="ExpansionLabelProperty"/>
    public string ExpansionLabel { get => GetValue(ExpansionLabelProperty); set => SetValue(ExpansionLabelProperty, value); }

    /// <inheritdoc cref="ChurnLabelProperty"/>
    public string ChurnLabel { get => GetValue(ChurnLabelProperty); set => SetValue(ChurnLabelProperty, value); }

    /// <inheritdoc cref="NetNewLabelProperty"/>
    public string NetNewLabel { get => GetValue(NetNewLabelProperty); set => SetValue(NetNewLabelProperty, value); }

    /// <inheritdoc cref="MrrLabelProperty"/>
    public string MrrLabel { get => GetValue(MrrLabelProperty); set => SetValue(MrrLabelProperty, value); }

    /// <inheritdoc cref="NetNewKpiLabelProperty"/>
    public string NetNewKpiLabel { get => GetValue(NetNewKpiLabelProperty); set => SetValue(NetNewKpiLabelProperty, value); }

    /// <inheritdoc cref="ChurnRateLabelProperty"/>
    public string ChurnRateLabel { get => GetValue(ChurnRateLabelProperty); set => SetValue(ChurnRateLabelProperty, value); }

    /// <inheritdoc cref="CompareTextProperty"/>
    public string CompareText { get => GetValue(CompareTextProperty); set => SetValue(CompareTextProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="SinceFormatProperty"/>
    public string SinceFormat { get => GetValue(SinceFormatProperty); set => SetValue(SinceFormatProperty, value); }

    /// <inheritdoc cref="VersusFormatProperty"/>
    public string VersusFormat { get => GetValue(VersusFormatProperty); set => SetValue(VersusFormatProperty, value); }

    /// <inheritdoc cref="PreviousRowFormatProperty"/>
    public string PreviousRowFormat { get => GetValue(PreviousRowFormatProperty); set => SetValue(PreviousRowFormatProperty, value); }

    /// <inheritdoc cref="PresetGroupLabelProperty"/>
    public string PresetGroupLabel { get => GetValue(PresetGroupLabelProperty); set => SetValue(PresetGroupLabelProperty, value); }

    /// <inheritdoc cref="PresetNameFormatProperty"/>
    public string PresetNameFormat { get => GetValue(PresetNameFormatProperty); set => SetValue(PresetNameFormatProperty, value); }

    /// <inheritdoc cref="AllPresetNameFormatProperty"/>
    public string AllPresetNameFormat { get => GetValue(AllPresetNameFormatProperty); set => SetValue(AllPresetNameFormatProperty, value); }

    /// <inheritdoc cref="SeriesGroupLabelProperty"/>
    public string SeriesGroupLabel { get => GetValue(SeriesGroupLabelProperty); set => SetValue(SeriesGroupLabelProperty, value); }

    /// <inheritdoc cref="PlotLabelProperty"/>
    public string PlotLabel { get => GetValue(PlotLabelProperty); set => SetValue(PlotLabelProperty, value); }

    /// <inheritdoc cref="WindowLabelProperty"/>
    public string WindowLabel { get => GetValue(WindowLabelProperty); set => SetValue(WindowLabelProperty, value); }

    /// <inheritdoc cref="StartHandleLabelProperty"/>
    public string StartHandleLabel { get => GetValue(StartHandleLabelProperty); set => SetValue(StartHandleLabelProperty, value); }

    /// <inheritdoc cref="EndHandleLabelProperty"/>
    public string EndHandleLabel { get => GetValue(EndHandleLabelProperty); set => SetValue(EndHandleLabelProperty, value); }

    /// <inheritdoc cref="TableCaptionFormatProperty"/>
    public string TableCaptionFormat { get => GetValue(TableCaptionFormatProperty); set => SetValue(TableCaptionFormatProperty, value); }

    /// <inheritdoc cref="ChartSummaryFormatProperty"/>
    public string ChartSummaryFormat { get => GetValue(ChartSummaryFormatProperty); set => SetValue(ChartSummaryFormatProperty, value); }

    /// <inheritdoc cref="RangeAnnouncementFormatProperty"/>
    public string RangeAnnouncementFormat { get => GetValue(RangeAnnouncementFormatProperty); set => SetValue(RangeAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="ScrubFormatProperty"/>
    public string ScrubFormat { get => GetValue(ScrubFormatProperty); set => SetValue(ScrubFormatProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="AxisFormatterProperty"/>
    public Func<double, string>? AxisFormatter { get => GetValue(AxisFormatterProperty); set => SetValue(AxisFormatterProperty, value); }

    /// <inheritdoc cref="MonthFormatterProperty"/>
    public Func<DateOnly, string>? MonthFormatter { get => GetValue(MonthFormatterProperty); set => SetValue(MonthFormatterProperty, value); }

    /// <inheritdoc cref="MonthYearFormatterProperty"/>
    public Func<DateOnly, string>? MonthYearFormatter { get => GetValue(MonthYearFormatterProperty); set => SetValue(MonthYearFormatterProperty, value); }

    /// <inheritdoc cref="RangeFormatterProperty"/>
    public Func<DateOnly, DateOnly, string>? RangeFormatter { get => GetValue(RangeFormatterProperty); set => SetValue(RangeFormatterProperty, value); }

    #endregion
}
