using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Primitives;
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
/// A week of hourly activity as a terrain:
/// 7 × 24 bars standing on a floor in perspective, as tall and as dark as each hour was busy. Drag to orbit, Ctrl/⌘ +
/// wheel or pinch to zoom, hover a bar for its numbers and the day's shape, click it to fly the camera there; switch the
/// metric and the bars rise to the new values in a diagonal wave; switch to the heat map and they press flat. Under the
/// plot, a legend and a summary: the week's total and its change, the peak hours (each flies to its hour) and the
/// busiest day.
/// </summary>
/// <remarks>
/// <para>
/// Data: one <see cref="ActivityTerrainMetric"/> per measure in <see cref="Metrics"/> (168 values, Monday 00:00 first,
/// and optionally the week before). The terrain derives the totals, the change, the peaks, the busiest day and its
/// working-hours share itself.
/// </para>
/// <para>
/// Keys on the plot: the first arrow key highlights the busiest hour, then arrows move by hour and day (stopping at the
/// edges), Home / End jump to the start and end of the day; Enter or Space flies to the highlighted hour; Escape first
/// returns to the whole week, then clears the highlight; Shift + ←/→ turns the view 15°, Shift + ↑/↓ tilts it 8°;
/// + and − zoom by 1.25.
/// </para>
/// </remarks>
[TemplatePart(MetricSwitchPart, typeof(SegmentedControl))]
[TemplatePart(ViewSwitchPart, typeof(SegmentedControl))]
[TemplatePart(BodyPart, typeof(Panel))]
[TemplatePart(PlotPart, typeof(Panel))]
[TemplatePart(CanvasPart, typeof(Control))]
[TemplatePart(CardPart, typeof(ContentControl))]
[TemplatePart(ResetPresencePart, typeof(PresenceHost))]
[TemplatePart(ResetPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(SwatchesPart, typeof(Panel))]
[TemplatePart(StatsPart, typeof(Panel))]
[TemplatePart(SummaryTitlePart, typeof(ArcContentSwap))]
[TemplatePart(TotalPart, typeof(Control))]
[TemplatePart(ChangeGlyphPart, typeof(ArcGlyph))]
[TemplatePart(ChangePart, typeof(Control))]
[TemplatePart(PeaksPart, typeof(StackPanel))]
[TemplatePart(BusiestDayPart, typeof(ArcContentSwap))]
[TemplatePart(BusiestCaptionPart, typeof(Panel))]
[TemplatePart(LiveRegionPart, typeof(Control))]
[TemplatePart(DataTablePart, typeof(Control))]
[TemplatePart(HintPart, typeof(TextBlock))]
[PseudoClasses(NarrowPseudoClass, WidePseudoClass, TopViewPseudoClass, ChangeGoodPseudoClass, ChangeBadPseudoClass,
    NoComparisonPseudoClass, EmptyPseudoClass)]
public partial class ActivityTerrain : TemplatedControl
{
    public const string MetricSwitchPart = "PART_MetricSwitch";
    public const string ViewSwitchPart = "PART_ViewSwitch";
    public const string BodyPart = "PART_Body";
    public const string PlotPart = "PART_Plot";
    public const string CanvasPart = "PART_Canvas";
    public const string CardPart = "PART_Card";
    public const string ResetPresencePart = "PART_ResetPresence";
    public const string ResetPart = "PART_Reset";
    public const string SwatchesPart = "PART_Swatches";
    public const string StatsPart = "PART_Stats";
    public const string SummaryTitlePart = "PART_SummaryTitle";
    public const string TotalPart = "PART_Total";
    public const string ChangeGlyphPart = "PART_ChangeGlyph";
    public const string ChangePart = "PART_Change";
    public const string PeaksPart = "PART_Peaks";
    public const string BusiestDayPart = "PART_BusiestDay";
    public const string BusiestCaptionPart = "PART_BusiestCaption";
    public const string LiveRegionPart = "PART_LiveRegion";
    public const string DataTablePart = "PART_DataTable";
    public const string HintPart = "PART_Hint";

    /// <summary>Set while the block is narrower than 560: the toolbar stacks, the plot is 280 high, the summary takes two columns.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is 880 or wider: the summary sits in a column beside the plot.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set while <see cref="ViewMode"/> is <see cref="ActivityTerrainViewMode.Top"/>.</summary>
    public const string TopViewPseudoClass = ":top-view";

    /// <summary>Set while the week's change is good news (a rise of a metric where higher is better, or a fall otherwise).</summary>
    public const string ChangeGoodPseudoClass = ":change-good";

    /// <summary>Set while the week's change is bad news.</summary>
    public const string ChangeBadPseudoClass = ":change-bad";

    /// <summary>Set while the metric has no previous week to compare with.</summary>
    public const string NoComparisonPseudoClass = ":no-comparison";

    /// <summary>Set while there is no metric or every value is zero.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>The selected metric (two-way); null shows the first of <see cref="Metrics"/>.</summary>
    public static readonly StyledProperty<ActivityTerrainMetric?> SelectedMetricProperty =
        AvaloniaProperty.Register<ActivityTerrain, ActivityTerrainMetric?>(nameof(SelectedMetric), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The <see cref="ActivityTerrainMetric.Key"/> of the selected metric (two-way).</summary>
    public static readonly StyledProperty<string?> SelectedMetricKeyProperty =
        AvaloniaProperty.Register<ActivityTerrain, string?>(nameof(SelectedMetricKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Terrain or heat map (two-way). Default <see cref="ActivityTerrainViewMode.Perspective"/>.</summary>
    public static readonly StyledProperty<ActivityTerrainViewMode> ViewModeProperty =
        AvaloniaProperty.Register<ActivityTerrain, ActivityTerrainViewMode>(nameof(ViewMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The heading over the block; null hides it.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ActivityTerrain, string?>(nameof(Title));

    /// <summary>The line under the heading (what the data is, which week, which time zone); null hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ActivityTerrain, string?>(nameof(Subtitle));

    /// <summary>Full day names from Monday (card, announcements, table); null uses English.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> DayNamesProperty =
        AvaloniaProperty.Register<ActivityTerrain, IReadOnlyList<string>?>(nameof(DayNames));

    /// <summary>Short day names from Monday (axis labels, peak hours); null uses English ("Mon").</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> DayAbbreviationsProperty =
        AvaloniaProperty.Register<ActivityTerrain, IReadOnlyList<string>?>(nameof(DayAbbreviations));

    /// <summary>The hours counted as working hours for the busiest day's share; null means Monday to Friday, 09:00 to 17:59.</summary>
    public static readonly StyledProperty<ActivityTerrainWorkingHours?> WorkingHoursProperty =
        AvaloniaProperty.Register<ActivityTerrain, ActivityTerrainWorkingHours?>(nameof(WorkingHours));

    /// <summary>How many peak hours the summary lists. Default 3.</summary>
    public static readonly StyledProperty<int> PeakCountProperty =
        AvaloniaProperty.Register<ActivityTerrain, int>(nameof(PeakCount), 3);

    /// <summary>The highlighted hour (hover or keyboard; two-way): crosshair, lifted bar, card. Null for none.</summary>
    public static readonly StyledProperty<ActivityTerrainCell?> HighlightedCellProperty =
        AvaloniaProperty.Register<ActivityTerrain, ActivityTerrainCell?>(nameof(HighlightedCell), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The hour the camera flew to (two-way); null shows the whole week. Setting it flies the camera.</summary>
    public static readonly StyledProperty<ActivityTerrainCell?> FocusedCellProperty =
        AvaloniaProperty.Register<ActivityTerrain, ActivityTerrainCell?>(nameof(FocusedCell), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The camera's current turn around the week in degrees (32 at rest; 50 in narrow blocks).</summary>
    public static readonly DirectProperty<ActivityTerrain, double> YawProperty =
        AvaloniaProperty.RegisterDirect<ActivityTerrain, double>(nameof(Yaw), o => o.Yaw);

    /// <summary>The camera's current angle above the floor in degrees (30 at rest, 10 to 84 when orbiting, 90 in the heat map).</summary>
    public static readonly DirectProperty<ActivityTerrain, double> PitchProperty =
        AvaloniaProperty.RegisterDirect<ActivityTerrain, double>(nameof(Pitch), o => o.Pitch);

    /// <summary>The camera's current zoom (1 at rest, 0.7 to 2.8).</summary>
    public static readonly DirectProperty<ActivityTerrain, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<ActivityTerrain, double>(nameof(Zoom), o => o.Zoom);

    /// <summary>True while the camera is (heading to) its starting view; otherwise "Reset view" shows.</summary>
    public static readonly DirectProperty<ActivityTerrain, bool> IsViewDefaultProperty =
        AvaloniaProperty.RegisterDirect<ActivityTerrain, bool>(nameof(IsViewDefault), o => o.IsViewDefault);

    /// <summary>Raised when the highlighted hour changes (hover, arrow keys, or null when it clears).</summary>
    public static readonly RoutedEvent<ActivityTerrainCellEventArgs> CellHoveredEvent =
        RoutedEvent.Register<ActivityTerrain, ActivityTerrainCellEventArgs>(nameof(CellHovered), RoutingStrategies.Bubble);

    /// <summary>Raised when the camera flies to an hour, or returns to the whole week (cell null).</summary>
    public static readonly RoutedEvent<ActivityTerrainCellEventArgs> CellFocusedEvent =
        RoutedEvent.Register<ActivityTerrain, ActivityTerrainCellEventArgs>(nameof(CellFocused), RoutingStrategies.Bubble);

    /// <summary>Raised when the view switches between the terrain and the heat map.</summary>
    public static readonly RoutedEvent<ActivityTerrainViewModeChangedEventArgs> ViewModeChangedEvent =
        RoutedEvent.Register<ActivityTerrain, ActivityTerrainViewModeChangedEventArgs>(nameof(ViewModeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the camera is reset (the "Reset view" button or <see cref="ResetView"/>).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ViewResetEvent =
        RoutedEvent.Register<ActivityTerrain, RoutedEventArgs>(nameof(ViewReset), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SelectedMetricProperty"/>
    public ActivityTerrainMetric? SelectedMetric { get => GetValue(SelectedMetricProperty); set => SetValue(SelectedMetricProperty, value); }

    /// <inheritdoc cref="SelectedMetricKeyProperty"/>
    public string? SelectedMetricKey { get => GetValue(SelectedMetricKeyProperty); set => SetValue(SelectedMetricKeyProperty, value); }

    /// <inheritdoc cref="ViewModeProperty"/>
    public ActivityTerrainViewMode ViewMode { get => GetValue(ViewModeProperty); set => SetValue(ViewModeProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="DayNamesProperty"/>
    public IReadOnlyList<string>? DayNames { get => GetValue(DayNamesProperty); set => SetValue(DayNamesProperty, value); }

    /// <inheritdoc cref="DayAbbreviationsProperty"/>
    public IReadOnlyList<string>? DayAbbreviations { get => GetValue(DayAbbreviationsProperty); set => SetValue(DayAbbreviationsProperty, value); }

    /// <inheritdoc cref="WorkingHoursProperty"/>
    public ActivityTerrainWorkingHours? WorkingHours { get => GetValue(WorkingHoursProperty); set => SetValue(WorkingHoursProperty, value); }

    /// <inheritdoc cref="PeakCountProperty"/>
    public int PeakCount { get => GetValue(PeakCountProperty); set => SetValue(PeakCountProperty, value); }

    /// <inheritdoc cref="HighlightedCellProperty"/>
    public ActivityTerrainCell? HighlightedCell { get => GetValue(HighlightedCellProperty); set => SetValue(HighlightedCellProperty, value); }

    /// <inheritdoc cref="FocusedCellProperty"/>
    public ActivityTerrainCell? FocusedCell { get => GetValue(FocusedCellProperty); set => SetValue(FocusedCellProperty, value); }

    /// <inheritdoc cref="YawProperty"/>
    public double Yaw { get => _yaw; private set => SetAndRaise(YawProperty, ref _yaw, value); }

    /// <inheritdoc cref="PitchProperty"/>
    public double Pitch { get => _pitch; private set => SetAndRaise(PitchProperty, ref _pitch, value); }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom { get => _zoom; private set => SetAndRaise(ZoomProperty, ref _zoom, value); }

    /// <inheritdoc cref="IsViewDefaultProperty"/>
    public bool IsViewDefault { get => _isViewDefault; private set => SetAndRaise(IsViewDefaultProperty, ref _isViewDefault, value); }

    /// <summary>The metrics the switch offers, in order.</summary>
    [Content]
    public AvaloniaList<ActivityTerrainMetric> Metrics { get; } = new();

    /// <summary>Flies the camera to the <see cref="ActivityTerrainCell"/> given as the parameter.</summary>
    public ICommand FlyToCommand { get; }

    /// <summary>Brings the camera back to its starting view (as the "Reset view" button does).</summary>
    public ICommand ResetViewCommand { get; }

    /// <inheritdoc cref="CellHoveredEvent"/>
    public event EventHandler<ActivityTerrainCellEventArgs>? CellHovered
    {
        add => AddHandler(CellHoveredEvent, value);
        remove => RemoveHandler(CellHoveredEvent, value);
    }

    /// <inheritdoc cref="CellFocusedEvent"/>
    public event EventHandler<ActivityTerrainCellEventArgs>? CellFocused
    {
        add => AddHandler(CellFocusedEvent, value);
        remove => RemoveHandler(CellFocusedEvent, value);
    }

    /// <inheritdoc cref="ViewModeChangedEvent"/>
    public event EventHandler<ActivityTerrainViewModeChangedEventArgs>? ViewModeChanged
    {
        add => AddHandler(ViewModeChangedEvent, value);
        remove => RemoveHandler(ViewModeChangedEvent, value);
    }

    /// <inheritdoc cref="ViewResetEvent"/>
    public event EventHandler<RoutedEventArgs>? ViewReset
    {
        add => AddHandler(ViewResetEvent, value);
        remove => RemoveHandler(ViewResetEvent, value);
    }

    private double _yaw = ActivityTerrainMath.RestYawDegrees;
    private double _pitch = ActivityTerrainMath.RestPitchDegrees;
    private double _zoom = 1;
    private bool _isViewDefault = true;

    static ActivityTerrain()
    {
        FocusableProperty.OverrideDefaultValue<ActivityTerrain>(false);
    }

    public ActivityTerrain()
    {
        FlyToCommand = new ActivityTerrainCommand(p =>
        {
            if (p is ActivityTerrainCell cell)
            {
                FlyTo(cell);
            }
        }, p => p is ActivityTerrainCell { IsValid: true });
        ResetViewCommand = new ActivityTerrainCommand(_ => ResetView(), _ => true);
        Metrics.CollectionChanged += OnMetricsChanged;
        ActualThemeVariantChanged += (_, _) => UpdateLegend();
        ResourcesChanged += (_, _) => UpdateLegend();
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnMetricsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ActivityTerrainMetric metric in e.OldItems)
            {
                metric.Changed -= OnMetricChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (ActivityTerrainMetric metric in e.NewItems)
            {
                metric.Changed += OnMetricChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var metric in Metrics)
            {
                metric.Changed -= OnMetricChanged;
                metric.Changed += OnMetricChanged;
            }
        }
        _metricOptionsDirty = true;
        Refresh(animate: true);
    }

    private void OnMetricChanged(object? sender, EventArgs e)
    {
        _metricOptionsDirty = true;
        Refresh(animate: ReferenceEquals(sender, _metric));
    }
}

/// <summary>A small <see cref="ICommand"/> over two delegates (the terrain's FlyTo and ResetView commands).</summary>
internal sealed class ActivityTerrainCommand(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => canExecute(parameter);

    public void Execute(object? parameter)
    {
        if (canExecute(parameter))
        {
            execute(parameter);
        }
    }
}
