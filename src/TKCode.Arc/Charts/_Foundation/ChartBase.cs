using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Base of Arc's self-drawn charts. It owns the data pipeline (<see cref="ItemsSource"/> with selectors or
/// bindings, and/or a <see cref="Series"/> collection, snapshotted once per change), the token palette, the
/// first-view reveal (once, when the chart is sufficiently visible), the active-point readout
/// (<see cref="ActiveIndex"/> and <see cref="ActiveIndexChanged"/>), the series legend, value formatting and the
/// accessible summary. Each chart draws its plot in a single <c>Render</c> pass of its <c>PART_Plot</c>.
/// </summary>
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(LegendPart, typeof(Panel))]
public abstract class ChartBase : TemplatedControl
{
    public const string PlotPart = "PART_Plot";
    public const string LegendPart = "PART_Legend";

    #region Common properties

    /// <summary>What is measured, such as "Signups". Names the chart, its summary and its data table.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ChartBase, string>(nameof(Label), string.Empty);

    /// <summary>Unit after each value in readouts, such as "ms".</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<ChartBase, string>(nameof(Unit), string.Empty);

    /// <summary>Shown when there is nothing to draw.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<ChartBase, string>(nameof(EmptyLabel), "No data for this range");

    /// <summary>Name of the category column in the data table and the explore label, such as "Date".</summary>
    public static readonly StyledProperty<string> CategoryLabelProperty =
        AvaloniaProperty.Register<ChartBase, string>(nameof(CategoryLabel), "Date");

    /// <summary>.NET format string for values (en-US), such as "N0", "C0" or "0.0'%'". Null uses the chart's default.</summary>
    public static readonly StyledProperty<string?> ValueFormatProperty =
        AvaloniaProperty.Register<ChartBase, string?>(nameof(ValueFormat));

    /// <summary>Formats values with context (series, index); wins over <see cref="ValueFormat"/>.</summary>
    public static readonly StyledProperty<Func<ChartValueContext, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<ChartBase, Func<ChartValueContext, string>?>(nameof(ValueFormatter));

    /// <summary>Plot height in pixels; the width follows the container.</summary>
    public static readonly StyledProperty<double> PlotHeightProperty =
        AvaloniaProperty.Register<ChartBase, double>(nameof(PlotHeight), 220);

    /// <summary>The data items. Values are read with <see cref="YSelector"/>/<see cref="YBinding"/> (or per series), categories with <see cref="XSelector"/>/<see cref="XBinding"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ChartBase, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// Reads each item's category or date: the AOT-safe path form, such as
    /// <c>XBinding="{CompiledBinding Date, DataType=local:Day}"</c> (a reflection <c>{Binding Date}</c> also works).
    /// </summary>
    public static readonly StyledProperty<BindingBase?> XBindingProperty =
        AvaloniaProperty.Register<ChartBase, BindingBase?>(nameof(XBinding));

    /// <summary>Reads each item's category or date (a string, DateTime, DateTimeOffset or DateOnly).</summary>
    public static readonly StyledProperty<Func<object, object?>?> XSelectorProperty =
        AvaloniaProperty.Register<ChartBase, Func<object, object?>?>(nameof(XSelector));

    /// <summary>Reads each item's value for a single-series chart (<c>YBinding="{CompiledBinding Visits, DataType=local:Day}"</c>).</summary>
    public static readonly StyledProperty<BindingBase?> YBindingProperty =
        AvaloniaProperty.Register<ChartBase, BindingBase?>(nameof(YBinding));

    /// <summary>Reads each item's value for a single-series chart.</summary>
    public static readonly StyledProperty<Func<object, double>?> YSelectorProperty =
        AvaloniaProperty.Register<ChartBase, Func<object, double>?>(nameof(YSelector));

    /// <summary>Full label of a category (tooltip, headline, screen readers). Dates default to "Tue, Sep 15".</summary>
    public static readonly StyledProperty<Func<object?, string>?> XLabelFormatterProperty =
        AvaloniaProperty.Register<ChartBase, Func<object?, string>?>(nameof(XLabelFormatter));

    /// <summary>Short axis label of a category; return null to keep the axis quiet there. Dates default to "Sep 15".</summary>
    public static readonly StyledProperty<Func<object?, string?>?> XAxisFormatterProperty =
        AvaloniaProperty.Register<ChartBase, Func<object?, string?>?>(nameof(XAxisFormatter));

    /// <summary>The point being read (crosshair, scrubbed bar, hovered segment), or null at rest. Two-way.</summary>
    public static readonly StyledProperty<int?> ActiveIndexProperty =
        AvaloniaProperty.Register<ChartBase, int?>(nameof(ActiveIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>A one-paragraph description for assistive technology, such as "Signups, Sep 1 to Sep 30. ...".</summary>
    public static readonly DirectProperty<ChartBase, string> SummaryProperty =
        AvaloniaProperty.RegisterDirect<ChartBase, string>(nameof(Summary), o => o.Summary);

    /// <summary>The accessible data table as text: a header line, then one line per row (tab separated).</summary>
    public static readonly DirectProperty<ChartBase, string> DataTableTextProperty =
        AvaloniaProperty.RegisterDirect<ChartBase, string>(nameof(DataTableText), o => o.DataTableText);

    /// <summary>The latest polite announcement (what Arc's live region reads), such as "Loading".</summary>
    public static readonly DirectProperty<ChartBase, string> AnnouncementProperty =
        AvaloniaProperty.RegisterDirect<ChartBase, string>(nameof(Announcement), o => o.Announcement);

    #endregion

    #region Shared properties (exposed by the charts that support them)

    /// <summary>Series toggles. Null shows the legend when there is more than one series.</summary>
    public static readonly StyledProperty<bool?> ShowLegendProperty =
        AvaloniaProperty.Register<ChartBase, bool?>("ShowLegend");

    /// <summary>Horizontal gridlines at the value ticks.</summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<ChartBase, bool>("ShowGrid", true);

    /// <summary>The category (time) axis labels under the plot.</summary>
    public static readonly StyledProperty<bool> ShowXAxisProperty =
        AvaloniaProperty.Register<ChartBase, bool>("ShowXAxis", true);

    /// <summary>The value axis labels in the right-hand gutter.</summary>
    public static readonly StyledProperty<bool> ShowYAxisProperty =
        AvaloniaProperty.Register<ChartBase, bool>("ShowYAxis", true);

    /// <summary>Fixes the bottom of the value axis (default: zero, or the data minimum when it dips below).</summary>
    public static readonly StyledProperty<double?> YMinimumProperty =
        AvaloniaProperty.Register<ChartBase, double?>("YMinimum");

    /// <summary>Fixes the top of the value axis (default: the next nice tick above the data).</summary>
    public static readonly StyledProperty<double?> YMaximumProperty =
        AvaloniaProperty.Register<ChartBase, double?>("YMaximum");

    /// <summary>Formats value-axis ticks. Defaults to a compact number, such as 1.2K.</summary>
    public static readonly StyledProperty<Func<double, string>?> TickFormatterProperty =
        AvaloniaProperty.Register<ChartBase, Func<double, string>?>("TickFormatter");

    /// <summary>Smooth monotone curves that never swing past the data, or straight segments.</summary>
    public static readonly StyledProperty<ChartCurve> CurveProperty =
        AvaloniaProperty.Register<ChartBase, ChartCurve>("Curve");

    /// <summary>Keys of hidden series. Two-way; the legend toggles it.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> HiddenSeriesProperty =
        AvaloniaProperty.Register<ChartBase, IReadOnlyList<string>?>("HiddenSeries", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Keeps the current shapes on screen, dimmed and breathing, while the next range loads.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ChartBase, bool>("IsLoading");

    #endregion

    #region Events

    /// <summary>Raised as the readout moves between points, and with a null index when it is released.</summary>
    public static readonly RoutedEvent<ChartActiveIndexChangedEventArgs> ActiveIndexChangedEvent =
        RoutedEvent.Register<ChartBase, ChartActiveIndexChangedEventArgs>(nameof(ActiveIndexChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the legend shows or hides a series.</summary>
    public static readonly RoutedEvent<ChartHiddenSeriesChangedEventArgs> HiddenSeriesChangedEvent =
        RoutedEvent.Register<ChartBase, ChartHiddenSeriesChangedEventArgs>("HiddenSeriesChanged", RoutingStrategies.Bubble);

    #endregion

    #region Theme-set properties (ArcDesign tokens)

    internal static readonly StyledProperty<IBrush?> SecondaryBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("SecondaryBrush");
    internal static readonly StyledProperty<IBrush?> MutedBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("MutedBrush");
    internal static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("GridBrush");
    internal static readonly StyledProperty<IBrush?> SubtleBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("SubtleBrush");
    internal static readonly StyledProperty<IBrush?> StrongBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("StrongBrush");
    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("SurfaceBrush");
    internal static readonly StyledProperty<IBrush?> RaisedBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("RaisedBrush");
    internal static readonly StyledProperty<IBrush?> SurfaceMutedBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("SurfaceMutedBrush");
    internal static readonly StyledProperty<IBrush?> AccentBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("AccentBrush");
    internal static readonly StyledProperty<IBrush?> AccentStrongBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("AccentStrongBrush");
    internal static readonly StyledProperty<IBrush?> SuccessBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("SuccessBrush");
    internal static readonly StyledProperty<IBrush?> WarningBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("WarningBrush");
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("DangerBrush");
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("FocusRingBrush");
    internal static readonly StyledProperty<IBrush?> Series1BrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("Series1Brush");
    internal static readonly StyledProperty<IBrush?> Series2BrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("Series2Brush");
    internal static readonly StyledProperty<IBrush?> Series3BrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("Series3Brush");
    internal static readonly StyledProperty<IBrush?> Series4BrushProperty = AvaloniaProperty.Register<ChartBase, IBrush?>("Series4Brush");

    internal static readonly StyledProperty<BoxShadows> FloatingShadowProperty =
        AvaloniaProperty.Register<ChartBase, BoxShadows>("FloatingShadow");

    internal static readonly StyledProperty<BoxShadows> RestingShadowProperty =
        AvaloniaProperty.Register<ChartBase, BoxShadows>("RestingShadow");

    internal static readonly StyledProperty<FontFamily?> DisplayFontFamilyProperty =
        AvaloniaProperty.Register<ChartBase, FontFamily?>("DisplayFontFamily");

    internal static readonly StyledProperty<FontWeight> MediumWeightProperty =
        AvaloniaProperty.Register<ChartBase, FontWeight>("MediumWeight", FontWeight.Medium);

    internal static readonly StyledProperty<double> TextSizeXSProperty = AvaloniaProperty.Register<ChartBase, double>("TextSizeXS", 12);
    internal static readonly StyledProperty<double> TextSizeSMProperty = AvaloniaProperty.Register<ChartBase, double>("TextSizeSM", 14);
    internal static readonly StyledProperty<double> TextSizeBaseProperty = AvaloniaProperty.Register<ChartBase, double>("TextSizeBase", 16);
    internal static readonly StyledProperty<double> TextSizeLGProperty = AvaloniaProperty.Register<ChartBase, double>("TextSizeLG", 18);
    internal static readonly StyledProperty<double> TextSize2XLProperty = AvaloniaProperty.Register<ChartBase, double>("TextSize2XL", 28);

    /// <summary>Width of the right-hand value gutter (chart token).</summary>
    internal static readonly StyledProperty<double> GutterWidthProperty = AvaloniaProperty.Register<ChartBase, double>("GutterWidth", 48);

    /// <summary>Height of the category axis row under the plot (chart token).</summary>
    internal static readonly StyledProperty<double> AxisRowHeightProperty = AvaloniaProperty.Register<ChartBase, double>("AxisRowHeight", 26);

    /// <summary>Stroke width of the main line or outline (chart token).</summary>
    internal static readonly StyledProperty<double> StrokeWidthProperty = AvaloniaProperty.Register<ChartBase, double>("StrokeWidth", 2);


    internal IBrush? SecondaryBrush => GetValue(SecondaryBrushProperty);
    internal IBrush? MutedBrush => GetValue(MutedBrushProperty);
    internal IBrush? GridBrush => GetValue(GridBrushProperty);
    internal IBrush? SubtleBrush => GetValue(SubtleBrushProperty);
    internal IBrush? StrongBrush => GetValue(StrongBrushProperty);
    internal IBrush? SurfaceBrush => GetValue(SurfaceBrushProperty);
    internal IBrush? RaisedBrush => GetValue(RaisedBrushProperty);
    internal IBrush? SurfaceMutedBrush => GetValue(SurfaceMutedBrushProperty);
    internal IBrush? AccentBrush => GetValue(AccentBrushProperty);
    internal IBrush? AccentStrongBrush => GetValue(AccentStrongBrushProperty);
    internal IBrush? SuccessBrush => GetValue(SuccessBrushProperty);
    internal IBrush? WarningBrush => GetValue(WarningBrushProperty);
    internal IBrush? DangerBrush => GetValue(DangerBrushProperty);
    internal IBrush? FocusRingBrush => GetValue(FocusRingBrushProperty);
    internal IBrush? Series1Brush => GetValue(Series1BrushProperty);
    internal IBrush? Series2Brush => GetValue(Series2BrushProperty);
    internal IBrush? Series3Brush => GetValue(Series3BrushProperty);
    internal IBrush? Series4Brush => GetValue(Series4BrushProperty);
    internal BoxShadows FloatingShadow => GetValue(FloatingShadowProperty);
    internal BoxShadows RestingShadow => GetValue(RestingShadowProperty);
    internal FontFamily? DisplayFontFamily => GetValue(DisplayFontFamilyProperty);
    internal FontWeight MediumWeight => GetValue(MediumWeightProperty);
    internal double TextSizeXS => GetValue(TextSizeXSProperty);
    internal double TextSizeSM => GetValue(TextSizeSMProperty);
    internal double TextSizeBase => GetValue(TextSizeBaseProperty);
    internal double TextSizeLG => GetValue(TextSizeLGProperty);
    internal double TextSize2XL => GetValue(TextSize2XLProperty);
    internal double GutterWidth => GetValue(GutterWidthProperty);
    internal double AxisRowHeight => GetValue(AxisRowHeightProperty);
    internal double StrokeWidth => GetValue(StrokeWidthProperty);

    #endregion

    private ChartSeriesCollection _series = new();
    private ChartData _data = ChartData.Empty;
    private bool _dataDirty = true;
    private bool _dataPosted;
    private bool _paletteDirty = true;
    private INotifyCollectionChanged? _observedItems;
    private ViewportVisibility? _visibility;
    private bool _inView;
    private bool _revealed;
    private MotionValue<double>? _revealProgress;
    private MotionValue<double>? _revealFade;
    private string _summary = string.Empty;
    private string _dataTable = string.Empty;
    private string _announcement = string.Empty;
    private ChartLegend? _legend;
    private readonly List<ChartPlot> _plots = new();

    private protected ChartBase()
    {
        _series.Invalidated += OnSeriesInvalidated;
    }

    static ChartBase()
    {
        FocusableProperty.OverrideDefaultValue<ChartBase>(false);
        AffectsPlot<ChartBase>(ShowGridProperty, StrokeWidthProperty);
        AffectsPlotLayout<ChartBase>(ShowXAxisProperty, ShowYAxisProperty, GutterWidthProperty, AxisRowHeightProperty, PlotHeightProperty);
    }

    /// <summary>Repaints the plot surfaces when any of <paramref name="properties"/> changes.</summary>
    private protected static void AffectsPlot<T>(params AvaloniaProperty[] properties) where T : ChartBase
    {
        foreach (var property in properties)
        {
            property.Changed.AddClassHandler<T>((chart, _) => chart.InvalidatePlots());
        }
    }

    /// <summary>Re-measures and repaints the plot surfaces when any of <paramref name="properties"/> changes.</summary>
    private protected static void AffectsPlotLayout<T>(params AvaloniaProperty[] properties) where T : ChartBase
    {
        foreach (var property in properties)
        {
            property.Changed.AddClassHandler<T>((chart, _) =>
            {
                chart.InvalidatePlotLayout();
                chart.InvalidatePlots();
            });
        }
    }

    #region CLR accessors

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel { get => GetValue(EmptyLabelProperty); set => SetValue(EmptyLabelProperty, value); }

    /// <inheritdoc cref="CategoryLabelProperty"/>
    public string CategoryLabel { get => GetValue(CategoryLabelProperty); set => SetValue(CategoryLabelProperty, value); }

    /// <inheritdoc cref="ValueFormatProperty"/>
    public string? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<ChartValueContext, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="PlotHeightProperty"/>
    public double PlotHeight { get => GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="XBindingProperty"/>
    [AssignBinding]
    public BindingBase? XBinding { get => GetValue(XBindingProperty); set => SetValue(XBindingProperty, value); }

    /// <inheritdoc cref="XSelectorProperty"/>
    public Func<object, object?>? XSelector { get => GetValue(XSelectorProperty); set => SetValue(XSelectorProperty, value); }

    /// <inheritdoc cref="YBindingProperty"/>
    [AssignBinding]
    public BindingBase? YBinding { get => GetValue(YBindingProperty); set => SetValue(YBindingProperty, value); }

    /// <inheritdoc cref="YSelectorProperty"/>
    public Func<object, double>? YSelector { get => GetValue(YSelectorProperty); set => SetValue(YSelectorProperty, value); }

    /// <inheritdoc cref="XLabelFormatterProperty"/>
    public Func<object?, string>? XLabelFormatter { get => GetValue(XLabelFormatterProperty); set => SetValue(XLabelFormatterProperty, value); }

    /// <inheritdoc cref="XAxisFormatterProperty"/>
    public Func<object?, string?>? XAxisFormatter { get => GetValue(XAxisFormatterProperty); set => SetValue(XAxisFormatterProperty, value); }

    /// <inheritdoc cref="ActiveIndexProperty"/>
    public int? ActiveIndex { get => GetValue(ActiveIndexProperty); set => SetValue(ActiveIndexProperty, value); }

    /// <summary>The series. Inline <c>&lt;arc:ChartSeries/&gt;</c> children land here.</summary>
    [Content]
    public ChartSeriesCollection Series
    {
        get => _series;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_series, value))
            {
                return;
            }
            _series.Invalidated -= OnSeriesInvalidated;
            _series = value;
            _series.Invalidated += OnSeriesInvalidated;
            InvalidateData();
        }
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e) => InvalidateData();

    /// <inheritdoc cref="SummaryProperty"/>
    public string Summary
    {
        get => _summary;
        private set => SetAndRaise(SummaryProperty, ref _summary, value);
    }

    /// <inheritdoc cref="DataTableTextProperty"/>
    public string DataTableText
    {
        get => _dataTable;
        private set => SetAndRaise(DataTableTextProperty, ref _dataTable, value);
    }

    /// <inheritdoc cref="AnnouncementProperty"/>
    public string Announcement
    {
        get => _announcement;
        private protected set
        {
            if (SetAndRaise(AnnouncementProperty, ref _announcement, value))
            {
                MainPlot?.Announce(value);
            }
        }
    }

    /// <inheritdoc cref="ActiveIndexChangedEvent"/>
    public event EventHandler<ChartActiveIndexChangedEventArgs>? ActiveIndexChanged
    {
        add => AddHandler(ActiveIndexChangedEvent, value);
        remove => RemoveHandler(ActiveIndexChangedEvent, value);
    }

    #endregion

    #region Internal state for charts

    internal ChartPalette Palette { get; } = new();

    internal ChartTextCache Texts { get; } = new();

    /// <summary>The current data snapshot (rebuilt once per change).</summary>
    internal ChartData Data
    {
        get
        {
            // Never rebuild inside a render pass (a rebuild invalidates visuals); the next layout catches up.
            if (!_rendering)
            {
                EnsureData();
            }
            return _data;
        }
    }

    private bool _rendering;

    /// <summary>Plots in the template (most charts have one).</summary>
    internal IReadOnlyList<ChartPlot> Plots => _plots;

    internal ChartPlot? MainPlot => _plots.Count > 0 ? _plots[0] : null;

    /// <summary>Draw-in progress of the first reveal (0..1).</summary>
    internal double RevealProgress => _revealProgress?.Value ?? (_revealed ? 1 : 0);

    /// <summary>Opacity of the first reveal under reduced motion (1 otherwise).</summary>
    internal double RevealFade => _revealFade?.Value ?? 1;

    internal bool IsRevealed => _revealed;

    internal ArcMotionLevel MotionLevel => ArcMotion.GetEffectiveLevel(this);

    internal bool IsMotionFull => MotionLevel == ArcMotionLevel.Full;

    /// <summary>Share of the chart that must be visible before the first reveal plays (each chart uses Arc's own ratio).</summary>
    private protected virtual double RevealThreshold => 0.5;

    /// <summary>How the first draw-in runs: a long, bounce-free smooth spring (P15).</summary>
    private protected virtual MotionSpec RevealSpec => MotionSpec.Spring(LongSmooth);

    /// <summary>The long smooth spring of first reveals (visualDuration 0.9, no bounce).</summary>
    internal static readonly SpringSpec LongSmooth = SpringSpec.FromVisual(0.9, 0);

    /// <summary>Arc's pen stroke for line charts: considered × 1.75 (840ms) on the in-out curve, slow at both ends.</summary>
    internal static readonly MotionSpec PenStroke = MotionSpec.Tween(TimeSpan.FromMilliseconds(840), ArcEasings.InOut);

    /// <summary>Default .NET format for values (Arc: grouped, at most one decimal).</summary>
    private protected virtual string DefaultValueFormat => "#,##0.#";

    /// <summary>Whether the template legend lists the series (multi-series charts).</summary>
    private protected virtual bool UsesSeriesLegend => false;

    private protected virtual ChartSwatchKind LegendSwatch => ChartSwatchKind.Line;

    #endregion

    #region Template and lifecycle

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var plot in _plots)
        {
            plot.Owner = null;
        }
        _plots.Clear();
        if (e.NameScope.Find<ChartPlot>(PlotPart) is { } main)
        {
            main.Owner = this;
            _plots.Add(main);
        }
        OnTemplatePlots(e.NameScope, _plots);
        _legend = e.NameScope.Find<ChartLegend>(LegendPart);
        if (_legend is not null)
        {
            _legend.Owner = this;
        }
        OnTemplateApplied(e.NameScope);
        SyncLegend();
        InvalidatePlots();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Rebuild the data (and the legend it feeds) before the template parts measure.
        EnsureData();
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Lets charts with several surfaces register them (BrushChart's overview).</summary>
    private protected virtual void OnTemplatePlots(INameScope scope, List<ChartPlot> plots)
    {
    }

    /// <summary>Finds chart-specific template parts.</summary>
    private protected virtual void OnTemplateApplied(INameScope scope)
    {
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _paletteDirty = true;
        if (!_revealed)
        {
            _visibility?.Dispose();
            _visibility = new ViewportVisibility(this, RevealThreshold, once: true);
            _visibility.VisibilityChanged += OnVisibilityChanged;
            if (MotionLevel == ArcMotionLevel.Off)
            {
                _inView = true;
                Dispatcher.UIThread.Post(TryReveal, DispatcherPriority.Loaded);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (!visible)
        {
            return;
        }
        _inView = true;
        TryReveal();
    }

    /// <summary>Plays the first reveal once the chart has been seen with data.</summary>
    private protected void TryReveal()
    {
        if (_revealed || !_inView)
        {
            return;
        }
        // Reading the data may rebuild it, which re-enters here; check again afterwards.
        var empty = Data.IsEmpty;
        if (_revealed || empty || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        _revealed = true;
        _revealProgress ??= CreateRevealValue(0);
        _revealFade ??= CreateRevealValue(1);
        switch (MotionLevel)
        {
            case ArcMotionLevel.Full:
                _ = CompleteReveal(_revealProgress.AnimateTo(1, RevealSpec));
                break;
            case ArcMotionLevel.Reduced:
                // Reduced motion: no draw-in, the finished chart fades in.
                _revealProgress.Jump(1);
                _revealFade.Jump(0);
                _ = _revealFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                break;
            default:
                _revealProgress.Jump(1);
                break;
        }
        OnRevealed();
        if (MotionLevel != ArcMotionLevel.Full)
        {
            OnRevealCompleted();
        }
    }

    private async Task CompleteReveal(Task<bool> reveal)
    {
        await reveal;
        OnRevealCompleted();
    }

    /// <summary>Called once the first draw-in has finished (Sparkline lands its end dot here).</summary>
    private protected virtual void OnRevealCompleted()
    {
    }

    /// <summary>Called once when the first reveal starts (charts start their own staggered growth here).</summary>
    private protected virtual void OnRevealed()
    {
    }

    private MotionValue<double> CreateRevealValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => OnRevealFrame();
        return value;
    }

    /// <summary>Each frame of the reveal; the default repaints the plots.</summary>
    private protected virtual void OnRevealFrame() => InvalidatePlots();

    #endregion

    #region Data pipeline

    private void InvalidateData()
    {
        _dataDirty = true;
        InvalidateMeasure();
        foreach (var plot in _plots)
        {
            plot.InvalidateMeasure();
        }
        if (!_dataPosted)
        {
            _dataPosted = true;
            Dispatcher.UIThread.Post(() =>
            {
                _dataPosted = false;
                EnsureData();
            }, DispatcherPriority.Normal);
        }
    }

    /// <summary>Rebuilds the data snapshot if anything changed since the last build.</summary>
    internal void EnsureData()
    {
        if (!_dataDirty)
        {
            return;
        }
        _dataDirty = false;
        var previous = _data;
        _data = ChartDataBuilder.Build(this);
        if (ActiveIndex is { } active && active >= _data.Count)
        {
            SetCurrentValue(ActiveIndexProperty, null);
        }
        OnDataChanged(previous, _data);
        SyncLegend();
        RefreshText();
        InvalidatePlots();
        TryReveal();
    }

    /// <summary>Called once per data change with the previous and the new snapshot (start morphs here).</summary>
    private protected abstract void OnDataChanged(ChartData previous, ChartData current);

    /// <summary>Recomputes the summary and data table (after data or visibility changes).</summary>
    private protected void RefreshText()
    {
        Summary = BuildSummary();
        DataTableText = BuildDataTable();
        AutomationProperties.SetHelpText(this, Summary);
    }

    private protected virtual string BuildSummary()
    {
        var data = _data;
        if (data.IsEmpty)
        {
            return $"{Label}. {EmptyLabel}.";
        }
        var builder = new StringBuilder();
        builder.Append(Label).Append(", ").Append(data.Labels[0]).Append(" to ").Append(data.Labels[^1]).Append('.');
        foreach (var series in VisibleSeries())
        {
            builder.Append(' ').Append(series.Name).Append(", latest ")
                   .Append(FormatWithUnit(series.Values[^1], data.Count - 1, series)).Append('.');
        }
        return builder.ToString();
    }

    private protected virtual string BuildDataTable()
    {
        var data = _data;
        if (data.IsEmpty)
        {
            return string.Empty;
        }
        var builder = new StringBuilder();
        builder.Append(CategoryLabel);
        foreach (var series in data.Series)
        {
            builder.Append('\t').Append(series.Name);
        }
        for (var i = 0; i < data.Count; i++)
        {
            builder.Append('\n').Append(data.Labels[i]);
            foreach (var series in data.Series)
            {
                builder.Append('\t').Append(FormatWithUnit(series.Values[i], i, series));
            }
        }
        return builder.ToString();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateData();

    #endregion

    #region Formatting

    internal static CultureInfo Culture => ChartDataBuilder.Culture;

    /// <summary>Formats a value with <see cref="ValueFormatter"/>, <see cref="ValueFormat"/> or the chart default.</summary>
    internal string FormatValue(double value, int index = -1, ChartSeriesData? series = null)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(new ChartValueContext(value, index, series?.Key, series?.Name));
        }
        return value.ToString(ValueFormat ?? DefaultValueFormat, Culture);
    }

    /// <summary>The value followed by the unit, such as "1,240 ms".</summary>
    internal string FormatWithUnit(double value, int index = -1, ChartSeriesData? series = null)
        => string.IsNullOrEmpty(Unit) ? FormatValue(value, index, series) : $"{FormatValue(value, index, series)} {Unit}";

    /// <summary>Formats an axis tick (<c>TickFormatter</c>, else compact: 1.2K).</summary>
    internal string FormatTick(double value)
        => GetValue(TickFormatterProperty) is { } formatter ? formatter(value) : DefaultTick(value);

    private protected virtual string DefaultTick(double value) => ArcFormatting.Compact(value, Culture);

    #endregion

    #region Palette

    internal void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        Palette.Capture(this);
        OnPaletteChanged();
    }

    /// <summary>Called when a theme token changes, before the palette is recaptured (repaint extra surfaces here).</summary>
    private protected virtual void OnThemeInvalidated()
    {
    }

    /// <summary>Called after the token palette was recaptured (theme or accent switch).</summary>
    private protected virtual void OnPaletteChanged()
    {
    }

    /// <summary>Default color of the series at palette position <paramref name="order"/>.</summary>
    internal virtual Color LegendSeriesColor(int order) => Palette.SeriesColor(order);

    /// <summary>Resolved color of a series (explicit color or palette position).</summary>
    internal Color SeriesColor(ChartSeriesData series) => series.Color ?? LegendSeriesColor(series.Order);

    #endregion

    #region Hidden series and legend

    internal IReadOnlyList<string> HiddenSeriesKeys => GetValue(HiddenSeriesProperty) ?? Array.Empty<string>();

    internal bool IsHidden(string key)
    {
        foreach (var hidden in HiddenSeriesKeys)
        {
            if (hidden == key)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The series that are not hidden, in order.</summary>
    internal IEnumerable<ChartSeriesData> VisibleSeries()
    {
        foreach (var series in _data.Series)
        {
            if (!IsHidden(series.Key))
            {
                yield return series;
            }
        }
    }

    internal int VisibleSeriesCount()
    {
        var count = 0;
        foreach (var series in _data.Series)
        {
            if (!IsHidden(series.Key))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Shows or hides a series from the legend (raises <c>HiddenSeriesChanged</c>).</summary>
    internal virtual void ToggleLegendSeries(string key)
    {
        var current = HiddenSeriesKeys;
        var next = new List<string>(current.Count + 1);
        var wasHidden = false;
        foreach (var hidden in current)
        {
            if (hidden == key)
            {
                wasHidden = true;
            }
            else
            {
                next.Add(hidden);
            }
        }
        if (!wasHidden)
        {
            next.Add(key);
        }
        SetCurrentValue(HiddenSeriesProperty, next.AsReadOnly());
        RaiseEvent(new ChartHiddenSeriesChangedEventArgs(HiddenSeriesChangedEvent, next.AsReadOnly()));
    }

    internal virtual bool IsLegendSeriesHidden(string key) => IsHidden(key);

    /// <summary>1 when a legend item is emphasized, -1 when dimmed, 0 normally.</summary>
    internal virtual double LegendEmphasis(string key) => 0;

    /// <summary>Called when a legend item is hovered (mouse) or keyboard focused, and when that ends.</summary>
    internal virtual void OnLegendPreview(string key, bool on)
    {
    }

    /// <summary>Whether hovering a legend item washes its pill (LineChart) or only lifts its text (Streamgraph).</summary>
    internal virtual bool LegendHoverWash => true;

    internal virtual bool LegendHoverText => true;

    private protected bool IsLegendShown => GetValue(ShowLegendProperty) ?? _data.Series.Length > 1;

    private protected void SyncLegend()
    {
        if (_legend is null)
        {
            return;
        }
        if (!UsesSeriesLegend || !IsLegendShown)
        {
            _legend.IsVisible = false;
            _legend.Children.Clear();
            return;
        }
        _legend.IsVisible = true;
        var items = new List<(string, string, Color?, int, bool)>(_data.Series.Length);
        foreach (var series in _data.Series)
        {
            items.Add((series.Key, series.Name, series.Color, series.Order, series.IsDashed));
        }
        _legend.Sync(items, LegendSwatch);
    }

    private protected void RefreshLegend() => _legend?.Refresh();

    #endregion

    #region Plot hooks

    internal void InvalidatePlots()
    {
        foreach (var plot in _plots)
        {
            plot.InvalidateVisual();
        }
    }

    internal void InvalidatePlotLayout()
    {
        foreach (var plot in _plots)
        {
            plot.InvalidateMeasure();
        }
    }

    internal virtual double MeasurePlotHeight(ChartPlot plot, double width) => PlotHeight;

    internal virtual double MeasurePlotWidth(ChartPlot plot, double available) => available;

    internal void RenderPlotSurface(ChartPlot plot, DrawingContext context, Size size)
    {
        EnsurePalette();
        _rendering = true;
        try
        {
            RenderPlot(plot, context, size);
        }
        finally
        {
            _rendering = false;
        }
    }

    private protected abstract void RenderPlot(ChartPlot plot, DrawingContext context, Size size);

    internal virtual void OnPlotResized(ChartPlot plot, Size size)
    {
    }

    internal virtual void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
    }

    internal virtual void OnPlotPointerPressed(ChartPlot plot, PointerPressedEventArgs e)
    {
    }

    internal virtual void OnPlotPointerReleased(ChartPlot plot, PointerReleasedEventArgs e)
    {
    }

    internal virtual void OnPlotPointerExited(ChartPlot plot, PointerEventArgs e)
    {
    }

    internal virtual void OnPlotPointerCaptureLost(ChartPlot plot, PointerCaptureLostEventArgs e)
    {
    }

    internal virtual void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
    }

    internal virtual void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
    }

    internal virtual AutomationControlType GetPlotControlType(ChartPlot plot) => AutomationControlType.Slider;

    internal virtual string GetPlotAutomationName(ChartPlot plot)
        => $"{Label}, explore by {CategoryLabel.ToLower(Culture)}";

    internal virtual string GetPlotValueText(ChartPlot plot)
    {
        var data = Data;
        if (data.IsEmpty)
        {
            return EmptyLabel;
        }
        if (ActiveIndex is not { } index || index >= data.Count)
        {
            return $"{VisibleSeriesCount()} of {data.Series.Length} series shown";
        }
        var builder = new StringBuilder(data.Labels[index]).Append(':');
        var first = true;
        foreach (var series in VisibleSeries())
        {
            builder.Append(first ? " " : ", ").Append(series.Name).Append(' ').Append(FormatWithUnit(series.Values[index], index, series));
            first = false;
        }
        return builder.ToString();
    }

    internal virtual (double Minimum, double Maximum, double Value) GetPlotRange(ChartPlot plot)
    {
        var count = Math.Max(1, Data.Count);
        return (1, count, (ActiveIndex ?? count - 1) + 1);
    }

    /// <summary>Moves the readout (pointer or keyboard). Use this rather than setting <see cref="ActiveIndex"/> directly.</summary>
    private protected void SetActive(int? index) => SetCurrentValue(ActiveIndexProperty, index);

    /// <summary>Reacts to <see cref="ActiveIndex"/> changes (springs the cursor, rebuilds the tooltip).</summary>
    private protected virtual void OnActiveIndexChanged(int? previous, int? current)
    {
    }

    /// <summary>The values reported by <see cref="ActiveIndexChanged"/>.</summary>
    private protected virtual IReadOnlyList<ChartSeriesValue> ActiveValues(int index)
    {
        var list = new List<ChartSeriesValue>();
        foreach (var series in VisibleSeries())
        {
            list.Add(new ChartSeriesValue(series.Key, series.Name, series.Values[index]));
        }
        return list;
    }

    /// <summary>The isolated series reported with <see cref="ActiveIndexChanged"/> (Streamgraph layers).</summary>
    private protected virtual string? EventSeriesKey => null;

    private protected void RaiseActiveIndexChanged()
    {
        var data = Data;
        var index = ActiveIndex is { } i && i < data.Count ? i : (int?)null;
        RaiseEvent(new ChartActiveIndexChangedEventArgs(ActiveIndexChangedEvent, index,
            index is { } at ? data.Items[at] : null,
            index is { } l ? data.Labels[l] : null,
            index is { } v ? ActiveValues(v) : Array.Empty<ChartSeriesValue>(),
            EventSeriesKey));
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemsSourceProperty)
        {
            if (_observedItems is not null)
            {
                _observedItems.CollectionChanged -= OnItemsCollectionChanged;
            }
            _observedItems = change.NewValue as INotifyCollectionChanged;
            if (_observedItems is not null)
            {
                _observedItems.CollectionChanged += OnItemsCollectionChanged;
            }
            InvalidateData();
        }
        else if (property == XBindingProperty || property == XSelectorProperty || property == YBindingProperty ||
                 property == YSelectorProperty || property == XLabelFormatterProperty || property == XAxisFormatterProperty)
        {
            InvalidateData();
        }
        else if (property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
            if (_data.Series.Length == 1 && Series.Count == 0)
            {
                InvalidateData();
            }
            else
            {
                RefreshText();
            }
        }
        else if (property == UnitProperty || property == ValueFormatProperty || property == ValueFormatterProperty ||
                 property == EmptyLabelProperty || property == CategoryLabelProperty || property == TickFormatterProperty)
        {
            RefreshText();
            OnFormattingChanged();
            InvalidatePlots();
        }
        else if (property == ShowLegendProperty)
        {
            SyncLegend();
        }
        else if (property == HiddenSeriesProperty)
        {
            OnHiddenSeriesChanged();
            RefreshLegend();
            RefreshText();
        }
        else if (property == ActiveIndexProperty)
        {
            var previous = change.GetOldValue<int?>();
            var current = change.GetNewValue<int?>();
            OnActiveIndexChanged(previous, current);
            RaiseActiveIndexChanged();
            if (MainPlot is { } plot)
            {
                plot.NotifyValueChanged((previous ?? -1) + 1, (current ?? -1) + 1);
            }
        }
        else if (property == ForegroundProperty || property == FontFamilyProperty || property.OwnerType == typeof(ChartBase) && IsThemeProperty(property))
        {
            _paletteDirty = true;
            Texts.Clear();
            InvalidatePlots();
            OnThemeInvalidated();
            if (_legend is not null)
            {
                _legend.InvalidateMeasure();
                foreach (var child in _legend.Children)
                {
                    child.InvalidateMeasure();
                    child.InvalidateVisual();
                }
            }
        }
    }

    private static bool IsThemeProperty(AvaloniaProperty property)
        => property.PropertyType == typeof(IBrush) || property == FloatingShadowProperty || property == RestingShadowProperty ||
           property == DisplayFontFamilyProperty || property == MediumWeightProperty || property == TextSizeXSProperty ||
           property == TextSizeSMProperty || property == TextSizeBaseProperty || property == TextSizeLGProperty ||
           property == TextSize2XLProperty;

    /// <summary>Called when hidden series change (flatten, rescale).</summary>
    private protected virtual void OnHiddenSeriesChanged()
    {
    }

    /// <summary>Called when value formatting changes (tick labels).</summary>
    private protected virtual void OnFormattingChanged()
    {
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartAutomationPeer(this);
}

/// <summary>The chart as a figure: named by its label, described by its summary.</summary>
internal sealed class ChartAutomationPeer : ControlAutomationPeer
{
    public ChartAutomationPeer(ChartBase owner) : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((ChartBase)Owner).Label;

    protected override string? GetHelpTextCore() => ((ChartBase)Owner).Summary;
}
