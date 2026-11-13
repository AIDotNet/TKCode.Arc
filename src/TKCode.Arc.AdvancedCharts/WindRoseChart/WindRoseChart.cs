using System.Collections;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A wind rose: how often the wind blows from each direction and how hard. Each direction sector is a petal stacked by
/// wind-speed bin from the calm circle outward, on a radial axis of percent (or counts) with the calm share in the centre.
/// New data springs every band to its new length; hovering or the arrow keys read a sector, and a legend entry highlights
/// one speed bin in every petal.
/// </summary>
public partial class WindRoseChart : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>Data items: <see cref="WindRoseRow"/>, <see cref="WindRoseRecord"/>, or any objects read through the member bindings. Null uses <see cref="Items"/>; an observable collection is followed.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<WindRoseChart, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The speed bins, inner to outer by ascending <see cref="WindRoseBin.Minimum"/>. Add bins in XAML or bind any list; empty uses <see cref="WindRoseBinning.DefaultBinMinimums"/>.</summary>
    public static readonly StyledProperty<IList<WindRoseBin>?> BinsProperty =
        AvaloniaProperty.Register<WindRoseChart, IList<WindRoseBin>?>(nameof(Bins));

    /// <summary>Calm amount, in the same unit as the values (a count, or a percent when the data are percentages); shown in the centre.</summary>
    public static readonly StyledProperty<double> CalmProperty =
        AvaloniaProperty.Register<WindRoseChart, double>(nameof(Calm));

    /// <summary>Number of direction sectors (default 16): 8, 16 and 32 get compass labels, any count from 4 to 72 works.</summary>
    public static readonly StyledProperty<int> SectorCountProperty =
        AvaloniaProperty.Register<WindRoseChart, int>(nameof(SectorCount), 16, coerce: static (_, value) => Math.Clamp(value, 4, 72));

    /// <summary>Labels per sector from sector 0; missing or empty entries use the default (compass points in the current language for 4, 8, 16 and 32 sectors, degrees otherwise).</summary>
    public static readonly StyledProperty<IList<string>?> DirectionLabelsProperty =
        AvaloniaProperty.Register<WindRoseChart, IList<string>?>(nameof(DirectionLabels));

    /// <summary>Screen angle, in degrees clockwise from the top, where sector 0 (north) is centred (default 0).</summary>
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<WindRoseChart, double>(nameof(StartAngle));

    /// <summary>Which way the sectors run on screen from sector 0 (default clockwise, as on a compass).</summary>
    public static readonly StyledProperty<SweepDirection> SweepDirectionProperty =
        AvaloniaProperty.Register<WindRoseChart, SweepDirection>(nameof(SweepDirection), SweepDirection.Clockwise);

    /// <summary>Whether a petal's length is its percent of all observations, calm included (default), or the amount as given.</summary>
    public static readonly StyledProperty<WindRoseRadialAxis> RadialAxisProperty =
        AvaloniaProperty.Register<WindRoseChart, WindRoseRadialAxis>(nameof(RadialAxis));

    /// <summary>The value at the outer ring; NaN (default) picks the first ring value at or above the longest petal.</summary>
    public static readonly StyledProperty<double> RadialMaximumProperty =
        AvaloniaProperty.Register<WindRoseChart, double>(nameof(RadialMaximum), double.NaN);

    /// <summary>The distance between rings; NaN (default) picks 1, 2, 2.5 or 5 × 10ⁿ for at most five rings.</summary>
    public static readonly StyledProperty<double> RadialStepProperty =
        AvaloniaProperty.Register<WindRoseChart, double>(nameof(RadialStep), double.NaN);

    /// <summary>Speed unit in the default bin labels (default "m/s").</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<WindRoseChart, string>(nameof(Unit), "m/s");

    /// <summary>What the chart shows, such as "Harbour station, 2025"; names it for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WindRoseChart, string?>(nameof(Label));

    /// <summary>Formats displayed values (a percent from 0 to 100, or an amount) in the rings, tooltip, centre and readings.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<WindRoseChart, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The sector being read (two-way), or -1. A number outside the sectors shows no reading.</summary>
    public static readonly StyledProperty<int> ActiveIndexProperty =
        AvaloniaProperty.Register<WindRoseChart, int>(nameof(ActiveIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected sector (two-way), or -1; it is drawn with an outline.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<WindRoseChart, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether a click, a tap or Enter selects the sector (default false). A sector chosen always raises <see cref="ItemInvoked"/>.</summary>
    public static readonly StyledProperty<bool> IsSelectionEnabledProperty =
        AvaloniaProperty.Register<WindRoseChart, bool>(nameof(IsSelectionEnabled));

    /// <summary>Shows the legend of speed bins under the rose (default true).</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<WindRoseChart, bool>(nameof(IsLegendVisible), true);

    /// <summary>Largest diameter of the rose in pixels (default 320); it shrinks to fit the space.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<WindRoseChart, double>(nameof(Size), 320);

    /// <summary>Message shown when there is no amount and no calm.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<WindRoseChart, string>(nameof(EmptyLabel), "No data");

    /// <summary>Raised when <see cref="SelectedIndex"/> changes, whether a sector was chosen or the index was set in code or through a binding.</summary>
    public static readonly RoutedEvent<WindRoseSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<WindRoseChart, WindRoseSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the user chooses a sector (click, tap, Enter or Space), even one that is already selected.</summary>
    public static readonly RoutedEvent<WindRoseItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<WindRoseChart, WindRoseItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> LabelGapProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(LabelGap), 10);
    internal static readonly StyledProperty<double> CompactLabelGapProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(CompactLabelGap), 6);
    internal static readonly StyledProperty<double> HoleRatioProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(HoleRatio), 0.18);
    internal static readonly StyledProperty<double> SectorFillProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(SectorFill), 0.84);
    internal static readonly StyledProperty<double> SegmentGapProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(SegmentGap), 2);
    internal static readonly StyledProperty<double> SegmentCornerProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(SegmentCorner), 2);
    internal static readonly StyledProperty<double> LegendGapProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(LegendGap), 14);
    internal static readonly StyledProperty<double> TooltipGapProperty = AvaloniaProperty.Register<WindRoseChart, double>(nameof(TooltipGap), 12);

    private static readonly SpringSpec s_morphSpring = ChartsBMath.Physical(0.55, 0.1);
    private static readonly SpringSpec s_axisSpring = ChartsBMath.Physical(0.55, 0);
    private static readonly SpringSpec s_revealSpring = ChartsBMath.Physical(0.8, 0.06);

    private readonly ChartsBPalette _palette;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly ChartsBItemsWatcher _itemsWatcher;
    private readonly ChartsBItemsWatcher _binsWatcher;
    private readonly ChartsBItemsWatcher _labelsWatcher;
    private readonly ChartsBMemberReader _reader = new();
    private readonly ChartsBOverlay _overlay;
    private readonly WrapPanel _legend;
    private readonly AvaloniaList<WindRoseRow> _items = new();
    private readonly AvaloniaList<WindRoseBin> _inlineBins = new();
    private readonly ChartSpringArray _springs;
    private readonly MotionValue<double> _maximum;
    private readonly MotionValue<double> _dim;
    private readonly MotionValue<double> _binDim;
    private readonly List<ChartsBTipRow> _tipRows = new();
    private readonly List<string> _tipLines = new();
    private BindingBase? _directionBinding;
    private BindingBase? _binBinding;
    private BindingBase? _valueBinding;
    private BindingBase? _valuesBinding;
    private WindRoseModel _model = WindRoseModel.Build(16, 0, [], 0, true, double.NaN, double.NaN);
    private List<BinInfo> _bins = new();
    private List<WindRoseSector> _sectors = new();
    private string[] _labels = [];
    private double[] _targets = [];
    private int _activeBin = -1;
    private int _highlightBin = -1;
    private WindRoseLegendItem? _highlightItem;
    private int _pressedSector = -1;
    private int _pressedBin = -1;
    private bool _settingActive;
    private bool _pending;
    private bool _posted;
    private bool _attached;
    private bool _suppressWatcher;
    private bool _tipStale = true;
    private int _tipSector = -1;
    private int _tipBin = -1;
    private ArcLanguageManager? _languages;
    private ChartsBAutomationPeer? _peer;

    private readonly record struct BinInfo(double Minimum, string Label, Color? Color);

    /// <summary>Creates a wind rose chart.</summary>
    public WindRoseChart()
    {
        _overlay = new ChartsBOverlay(PaintOverlay);
        _legend = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, ItemSpacing = 4, LineSpacing = 2 };
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _reveal = new ChartsBReveal(this, 0.3, OnRevealed);
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, _overlay.InvalidateVisual) { MinWidth = 170 };
        _springs = new ChartSpringArray(this);
        _springs.Changed += InvalidateVisual;
        _maximum = Painted(1);
        _dim = Painted(0);
        _binDim = Painted(0);
        _itemsWatcher = new ChartsBItemsWatcher(OnSourceChanged);
        _binsWatcher = new ChartsBItemsWatcher(OnSourceChanged);
        _labelsWatcher = new ChartsBItemsWatcher(OnSourceChanged);
        _items.CollectionChanged += (_, _) =>
        {
            // While attached the watcher reports inline changes itself.
            if (!_itemsWatcher.IsSubscribed && ReferenceEquals(_itemsWatcher.Source, _items))
            {
                RequestRebuild();
            }
        };
        _inlineBins.CollectionChanged += (_, _) =>
        {
            if (!_binsWatcher.IsSubscribed && ReferenceEquals(_binsWatcher.Source, _inlineBins))
            {
                RequestRebuild();
            }
        };
        VisualChildren.Add(_legend);
        LogicalChildren.Add(_legend);
        VisualChildren.Add(_overlay);
        _suppressWatcher = true;
        _itemsWatcher.SetSource(_items);
        _binsWatcher.SetSource(_inlineBins);
        _suppressWatcher = false;
        SetCurrentValue(BinsProperty, _inlineBins);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Rebuild();
    }

    static WindRoseChart()
    {
        AffectsMeasure<WindRoseChart>(SizeProperty, IsLegendVisibleProperty, LabelGapProperty, CompactLabelGapProperty, LegendGapProperty);
        AffectsRender<WindRoseChart>(HoleRatioProperty, SectorFillProperty, SegmentGapProperty, SegmentCornerProperty);
    }

    private MotionValue<double> Painted(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Inline rows, used when <see cref="ItemsSource"/> is not set.</summary>
    [Content]
    public AvaloniaList<WindRoseRow> Items => _items;

    /// <summary>Binding (member path) that reads each item's direction: degrees clockwise from north, or a direction name.</summary>
    [AssignBinding]
    public BindingBase? DirectionMemberBinding
    {
        get => _directionBinding;
        set
        {
            _directionBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each item's speed-bin index (with <see cref="ValueMemberBinding"/>, one amount per item).</summary>
    [AssignBinding]
    public BindingBase? BinMemberBinding
    {
        get => _binBinding;
        set
        {
            _binBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each item's amount for its speed bin.</summary>
    [AssignBinding]
    public BindingBase? ValueMemberBinding
    {
        get => _valueBinding;
        set
        {
            _valueBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each item's amounts per speed bin as a list of numbers (one item per direction); wins over the bin and value bindings.</summary>
    [AssignBinding]
    public BindingBase? ValuesMemberBinding
    {
        get => _valuesBinding;
        set
        {
            _valuesBinding = value;
            Rebuild();
        }
    }

    /// <inheritdoc cref="BinsProperty"/>
    public IList<WindRoseBin>? Bins
    {
        get => GetValue(BinsProperty);
        set => SetValue(BinsProperty, value);
    }

    /// <inheritdoc cref="CalmProperty"/>
    public double Calm
    {
        get => GetValue(CalmProperty);
        set => SetValue(CalmProperty, value);
    }

    /// <inheritdoc cref="SectorCountProperty"/>
    public int SectorCount
    {
        get => GetValue(SectorCountProperty);
        set => SetValue(SectorCountProperty, value);
    }

    /// <inheritdoc cref="DirectionLabelsProperty"/>
    public IList<string>? DirectionLabels
    {
        get => GetValue(DirectionLabelsProperty);
        set => SetValue(DirectionLabelsProperty, value);
    }

    /// <inheritdoc cref="StartAngleProperty"/>
    public double StartAngle
    {
        get => GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    /// <inheritdoc cref="SweepDirectionProperty"/>
    public SweepDirection SweepDirection
    {
        get => GetValue(SweepDirectionProperty);
        set => SetValue(SweepDirectionProperty, value);
    }

    /// <inheritdoc cref="RadialAxisProperty"/>
    public WindRoseRadialAxis RadialAxis
    {
        get => GetValue(RadialAxisProperty);
        set => SetValue(RadialAxisProperty, value);
    }

    /// <inheritdoc cref="RadialMaximumProperty"/>
    public double RadialMaximum
    {
        get => GetValue(RadialMaximumProperty);
        set => SetValue(RadialMaximumProperty, value);
    }

    /// <inheritdoc cref="RadialStepProperty"/>
    public double RadialStep
    {
        get => GetValue(RadialStepProperty);
        set => SetValue(RadialStepProperty, value);
    }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <inheritdoc cref="ActiveIndexProperty"/>
    public int ActiveIndex
    {
        get => GetValue(ActiveIndexProperty);
        set => SetValue(ActiveIndexProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="IsSelectionEnabledProperty"/>
    public bool IsSelectionEnabled
    {
        get => GetValue(IsSelectionEnabledProperty);
        set => SetValue(IsSelectionEnabledProperty, value);
    }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible
    {
        get => GetValue(IsLegendVisibleProperty);
        set => SetValue(IsLegendVisibleProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<WindRoseSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<WindRoseItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    internal double LabelGap
    {
        get => GetValue(LabelGapProperty);
        set => SetValue(LabelGapProperty, value);
    }

    internal double CompactLabelGap
    {
        get => GetValue(CompactLabelGapProperty);
        set => SetValue(CompactLabelGapProperty, value);
    }

    internal double HoleRatio
    {
        get => GetValue(HoleRatioProperty);
        set => SetValue(HoleRatioProperty, value);
    }

    internal double SectorFill
    {
        get => GetValue(SectorFillProperty);
        set => SetValue(SectorFillProperty, value);
    }

    internal double SegmentGap
    {
        get => GetValue(SegmentGapProperty);
        set => SetValue(SegmentGapProperty, value);
    }

    internal double SegmentCorner
    {
        get => GetValue(SegmentCornerProperty);
        set => SetValue(SegmentCornerProperty, value);
    }

    internal double LegendGap
    {
        get => GetValue(LegendGapProperty);
        set => SetValue(LegendGapProperty, value);
    }

    internal double TooltipGap
    {
        get => GetValue(TooltipGapProperty);
        set => SetValue(TooltipGapProperty, value);
    }

    /// <summary>The sectors as drawn, from sector 0: label, angles, amounts per bin, total and share.</summary>
    public IReadOnlyList<WindRoseSector> Sectors
    {
        get
        {
            Flush();
            return _sectors;
        }
    }

    /// <summary>Accessible summary: the calm share and every sector's total.</summary>
    public string Summary
    {
        get
        {
            Flush();
            if (IsEmpty)
            {
                return $"{Title}. {EmptyLabel}.";
            }
            var sectors = string.Join(", ", _sectors.Select(s => $"{s.Label} {Format(_model.SectorStacks[s.Index])}"));
            return $"{Title}. {_sectors.Count} directions, {CalmText} {Format(_model.CalmDisplay)}. {sectors}.";
        }
    }

    private string Title => string.IsNullOrEmpty(Label) ? "Wind rose" : Label;

    private static string CalmText => WindRoseChartLangResources.Get(WindRoseChartLangResourceKind.Calm);

    private bool IsEmpty => _model.IsEmpty;

    /// <summary>The sector being read, or -1 when there is none or it is outside the sectors.</summary>
    private int EffectiveActive => !IsEmpty && ActiveIndex >= 0 && ActiveIndex < _sectors.Count ? ActiveIndex : -1;

    private int EffectiveSelected => !IsEmpty && SelectedIndex >= 0 && SelectedIndex < _sectors.Count ? SelectedIndex : -1;

    internal ChartsBPalette Palette => _palette;
    internal WindRoseModel Model => _model;
    internal bool IsShowingEmptyLabel => IsEmpty;
    internal bool IsRevealed => _reveal.IsRevealed;
    internal bool IsMorphing => _springs.IsAnimating;
    internal int ActiveBin => EffectiveActive >= 0 ? _activeBin : -1;
    internal int HighlightedBin => _highlightBin;
    internal WrapPanel Legend => _legend;
    internal IReadOnlyList<string> TooltipLines => _tipLines;
    internal bool IsTooltipOpen => _tooltip.IsOpen;
    internal IReadOnlyList<string> BinLabels => _bins.Select(b => b.Label).ToList();
    internal IReadOnlyList<string> Labels => _labels;

    /// <summary>How many times the data was read (collection changes within one dispatcher turn count once).</summary>
    internal int RebuildCount { get; private set; }

    /// <summary>The displayed stacked top of a segment as drawn now (it lags the data while a spring runs).</summary>
    internal double CurrentTop(int sector, int bin)
    {
        var values = _springs.Values;
        var at = sector * _model.BinCount + bin;
        return at < values.Length ? values[at] : 0;
    }

    // ---- data -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            SetSource(_itemsWatcher, ItemsSource ?? _items);
        }
        else if (change.Property == BinsProperty)
        {
            SetSource(_binsWatcher, (IEnumerable?)Bins ?? _inlineBins);
        }
        else if (change.Property == DirectionLabelsProperty)
        {
            SetSource(_labelsWatcher, (IEnumerable?)DirectionLabels);
        }
        else if (change.Property == CalmProperty || change.Property == SectorCountProperty || change.Property == RadialAxisProperty ||
                 change.Property == RadialMaximumProperty || change.Property == RadialStepProperty || change.Property == UnitProperty)
        {
            Rebuild();
        }
        else if (change.Property == StartAngleProperty || change.Property == SweepDirectionProperty)
        {
            OnGeometryChanged();
        }
        else if (change.Property == ActiveIndexProperty)
        {
            OnActiveIndexChanged();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            OnSelectedIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == ValueFormatterProperty)
        {
            _ringTextsStale = true;
            _centerStale = true;
            _tipStale = true;
            SyncLegend();
            SyncTooltip();
            UpdateAccessibility();
            InvalidateVisual();
        }
        else if (change.Property == EmptyLabelProperty)
        {
            _emptyText = null;
            UpdateAccessibility();
            InvalidateVisual();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateAccessibility();
        }
        else if (change.Property == HoleRatioProperty || change.Property == SectorFillProperty || change.Property == SegmentGapProperty ||
                 change.Property == SegmentCornerProperty)
        {
            _centerStale = true;
            InvalidateVisual();
        }
    }

    private void SetSource(ChartsBItemsWatcher watcher, IEnumerable? source)
    {
        _suppressWatcher = true;
        watcher.SetSource(source);
        _suppressWatcher = false;
        Rebuild();
    }

    private void OnSourceChanged()
    {
        if (!_suppressWatcher)
        {
            RequestRebuild();
        }
    }

    /// <summary>
    /// A collection change: rebuilt once at the end of this dispatcher turn, so a <c>Clear</c> followed by many <c>Add</c>
    /// calls springs once from what is on screen to the final data instead of passing through an empty chart.
    /// </summary>
    private void RequestRebuild()
    {
        _pending = true;
        if (_attached && !_posted)
        {
            _posted = true;
            Dispatcher.UIThread.Post(() =>
            {
                _posted = false;
                Flush();
            }, DispatcherPriority.Normal);
        }
    }

    private void Flush()
    {
        if (_pending)
        {
            Rebuild();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _itemsWatcher.Attach();
        _binsWatcher.Attach();
        _labelsWatcher.Attach();
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        Flush();
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _itemsWatcher.Detach();
        _binsWatcher.Detach();
        _labelsWatcher.Detach();
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
        _reveal.Detach();
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Rebuild();
        }
        else
        {
            Dispatcher.UIThread.Post(Rebuild);
        }
    }

    private void OnPaletteChanged()
    {
        _tipStale = true;
        _emptyText = null;
        _centerStale = true;
        _ringTextsStale = true;
        foreach (var child in _legend.Children)
        {
            if (child is WindRoseLegendItem item)
            {
                item.InvalidateInk();
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    /// <summary>Reads the data again and springs every band from where it is on screen to its new length.</summary>
    private void Rebuild()
    {
        _pending = false;
        RebuildCount++;
        var count = SectorCount;
        _bins = ResolveBins();
        var binCount = _bins.Count;
        var labels = ResolveLabels(count);
        // Label runs are kept while the text stays, so a data update does not lay out the ring of names again.
        _labelsStale |= !labels.AsSpan().SequenceEqual(_labels);
        _labels = labels;
        var raw = new double[count * binCount];
        foreach (var item in _itemsWatcher.Snapshot())
        {
            Accumulate(item, raw, count, binCount);
        }
        var previous = _model;
        _model = WindRoseModel.Build(count, binCount, raw, Calm, RadialAxis == WindRoseRadialAxis.Percent, RadialMaximum, RadialStep);
        var width = 360.0 / count;
        var sectors = new List<WindRoseSector>(count);
        for (var s = 0; s < count; s++)
        {
            var values = new double[binCount];
            Array.Copy(_model.Raw, s * binCount, values, 0, binCount);
            var total = _model.SectorTotals[s];
            sectors.Add(new WindRoseSector(s, _labels[s], s * width, width, values, total, _model.Total > 0 ? total / _model.Total : 0));
        }
        _sectors = sectors;
        Retarget(previous);
        _ringTextsStale = true;
        _centerStale = true;
        _colorVersion = -1;
        _tipStale = true;
        Focusable = !IsEmpty;
        if (SelectedIndex >= count)
        {
            SetCurrentValue(SelectedIndexProperty, -1);
        }
        if (ActiveIndex >= count)
        {
            SetActive(-1, -1);
        }
        if (_highlightBin >= binCount)
        {
            _highlightBin = -1;
            _highlightItem = null;
            _binDim.Jump(0);
        }
        SyncLegend();
        SyncTooltip();
        UpdateAccessibility();
        InvalidateMeasure();
        InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private List<BinInfo> ResolveBins()
    {
        var minimums = new List<(double Minimum, string? Label, Color? Color)>();
        foreach (var item in _binsWatcher.Snapshot())
        {
            if (item is WindRoseBin bin)
            {
                minimums.Add((bin.Minimum, bin.Label, bin.Color));
            }
        }
        if (minimums.Count == 0)
        {
            foreach (var minimum in WindRoseBinning.DefaultBinMinimums)
            {
                minimums.Add((minimum, null, null));
            }
        }
        var unit = string.IsNullOrWhiteSpace(Unit) ? string.Empty : " " + Unit.Trim();
        var bins = new List<BinInfo>(minimums.Count);
        for (var i = 0; i < minimums.Count; i++)
        {
            var (minimum, label, color) = minimums[i];
            label ??= i + 1 < minimums.Count
                ? $"{ChartsBMath.Grouped(minimum)}–{ChartsBMath.Grouped(minimums[i + 1].Minimum)}{unit}"
                : $"≥{ChartsBMath.Grouped(minimum)}{unit}";
            bins.Add(new BinInfo(minimum, label, color));
        }
        return bins;
    }

    private string[] ResolveLabels(int count)
    {
        var custom = new List<string>();
        foreach (var item in _labelsWatcher.Snapshot())
        {
            custom.Add(item as string ?? item.ToString() ?? string.Empty);
        }
        var labels = new string[count];
        for (var i = 0; i < count; i++)
        {
            labels[i] = i < custom.Count && !string.IsNullOrWhiteSpace(custom[i]) ? custom[i] : WindRoseModel.DefaultLabel(i, count);
        }
        return labels;
    }

    private void Accumulate(object item, double[] raw, int count, int binCount)
    {
        var row = item as WindRoseRow;
        var record = item as WindRoseRecord;
        var direction = _directionBinding is null ? row?.Direction ?? record?.Direction : _reader.Read(item, _directionBinding);
        var sector = WindRoseModel.SectorOf(direction, count, _labels);
        if (sector < 0)
        {
            return;
        }
        var at = sector * binCount;
        if (_valuesBinding is not null)
        {
            if (_reader.Read(item, _valuesBinding) is IEnumerable values)
            {
                var k = 0;
                foreach (var value in values)
                {
                    if (k >= binCount)
                    {
                        break;
                    }
                    raw[at + k] += Clean(ChartsBMemberReader.ToDouble(value));
                    k++;
                }
            }
            return;
        }
        if (row is not null && _binBinding is null && _valueBinding is null)
        {
            for (var k = 0; k < row.Values.Count && k < binCount; k++)
            {
                raw[at + k] += Clean(row.Values[k]);
            }
            return;
        }
        var bin = _binBinding is null ? record?.Bin ?? -1 : WindRoseModel.BinOf(_reader.Read(item, _binBinding));
        if (bin < 0 || bin >= binCount)
        {
            return;
        }
        var amount = _valueBinding is null ? record?.Value ?? double.NaN : ChartsBMemberReader.ToDouble(_reader.Read(item, _valueBinding));
        raw[at + bin] += Clean(amount);
    }

    private static double Clean(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    /// <summary>
    /// Points the springs at the new stacked tops. A band that survives keeps its place and speed; when the sector or bin
    /// count changes, each new sector starts from the old sector that covered its direction, and new bins from its top.
    /// </summary>
    private void Retarget(WindRoseModel previous)
    {
        var model = _model;
        var count = model.SectorCount;
        var bins = model.BinCount;
        _targets = (double[])model.Top.Clone();
        if (_springs.Length != count * bins || previous.SectorCount != count || previous.BinCount != bins)
        {
            var oldCount = previous.SectorCount;
            var oldBins = previous.BinCount;
            var valid = _springs.Length == oldCount * oldBins && oldBins > 0;
            _springs.Reshape(count * bins, (oldValues, oldVelocities, values, velocities) =>
            {
                if (!valid || oldValues.Length == 0)
                {
                    return;
                }
                for (var s = 0; s < count; s++)
                {
                    var from = oldCount == count ? s : WindRoseBinning.SectorOf(s * 360.0 / count, oldCount);
                    for (var b = 0; b < bins; b++)
                    {
                        var keep = b < oldBins;
                        var source = from * oldBins + (keep ? b : oldBins - 1);
                        values[s * bins + b] = oldValues[source];
                        velocities[s * bins + b] = keep ? oldVelocities[source] : 0;
                    }
                }
            });
            _segmentKeys = new SegmentKey[count * bins];
            _segments = new StreamGeometry?[count * bins];
        }
        if (_reveal.IsRevealed)
        {
            _springs.AnimateTo(_targets, s_morphSpring);
            if (ArcMotion.IsFull(this) && IsLoaded)
            {
                _ = _maximum.AnimateTo(model.Maximum, MotionSpec.Spring(s_axisSpring));
            }
            else
            {
                _maximum.Jump(model.Maximum);
            }
        }
        else
        {
            // Bands stay at the calm circle until the first view grows them.
            _maximum.Jump(model.Maximum);
        }
    }

    private void OnRevealed(bool animate)
    {
        if (animate)
        {
            _springs.AnimateTo(_targets, s_revealSpring);
        }
        else
        {
            _springs.Jump(_targets);
        }
        InvalidateVisual();
    }

    private void SyncLegend()
    {
        var children = _legend.Children;
        var bins = _bins.Count;
        while (children.Count > bins)
        {
            children.RemoveAt(children.Count - 1);
        }
        for (var k = 0; k < bins; k++)
        {
            WindRoseLegendItem item;
            if (k < children.Count)
            {
                item = (WindRoseLegendItem)children[k];
            }
            else
            {
                item = new WindRoseLegendItem(this);
                children.Add(item);
            }
            var share = _model.Total > 0 && k < _model.BinTotals.Length ? _model.BinTotals[k] / _model.Total : 0;
            item.Configure(k, _bins[k].Label, $"{_bins[k].Label}, {ChartsBMath.Percent(share, 1)} of observations");
        }
    }

    // ---- reading ----------------------------------------------------------------------------------------------

    /// <summary>A displayed value: a percent with one decimal, or an amount grouped with at most one decimal.</summary>
    internal string Format(double value)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value);
        }
        return _model.IsPercent ? value.ToString("0.0", ChartsBMath.EnUs) + "%" : ChartsBMath.Grouped(value);
    }

    /// <summary>What a screen reader hears for the sector being read.</summary>
    internal string? Reading
    {
        get
        {
            var index = EffectiveActive;
            if (index < 0)
            {
                return null;
            }
            var sector = _sectors[index];
            var bins = _model.BinCount;
            var parts = new List<string>();
            for (var k = 0; k < bins; k++)
            {
                if (_model.Raw[index * bins + k] > 0)
                {
                    parts.Add($"{_bins[k].Label} {Format(_model.Display[index * bins + k])}");
                }
            }
            var text = $"{sector.Label}: {Format(_model.SectorStacks[index])}";
            if (parts.Count > 0)
            {
                text += ". " + string.Join(", ", parts);
            }
            return index == EffectiveSelected ? text + ", selected" : text;
        }
    }

    private void SetActive(int sector, int bin)
    {
        var binChanged = bin != _activeBin;
        _activeBin = sector >= 0 ? bin : -1;
        if (ActiveIndex != sector)
        {
            _settingActive = true;
            SetCurrentValue(ActiveIndexProperty, sector);
            _settingActive = false;
        }
        else if (binChanged)
        {
            SyncTooltip();
            InvalidateLegend();
            InvalidateVisual();
        }
    }

    private void OnActiveIndexChanged()
    {
        if (!_settingActive)
        {
            _activeBin = -1;
        }
        var index = EffectiveActive;
        Fade(_dim, index >= 0 ? 1 : 0);
        SyncTooltip();
        UpdateAccessibility();
        InvalidateLegend();
        InvalidateVisual();
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        var oldItem = oldIndex >= 0 && oldIndex < _sectors.Count ? _sectors[oldIndex] : null;
        var newItem = newIndex >= 0 && newIndex < _sectors.Count ? _sectors[newIndex] : null;
        UpdateAccessibility();
        InvalidateVisual();
        RaiseEvent(new WindRoseSelectionChangedEventArgs(oldIndex, newIndex, oldItem, newItem));
    }

    private void InvalidateLegend()
    {
        foreach (var child in _legend.Children)
        {
            child.InvalidateVisual();
        }
    }

    private void Fade(MotionValue<double> value, double target)
    {
        if (ArcMotion.IsFull(this) && IsLoaded)
        {
            _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            value.Jump(target);
        }
    }

    /// <summary>Whether a bin is emphasised: highlighted from the legend, or the segment under the pointer.</summary>
    internal bool IsBinLit(int bin) => bin == _highlightBin || (bin >= 0 && bin == ActiveBin);

    /// <summary>Called by a legend entry on hover and focus; a focused entry keeps its bin highlighted when the pointer leaves.</summary>
    internal void HighlightBin(WindRoseLegendItem item, bool on)
    {
        if (on)
        {
            _highlightItem = item;
            _highlightBin = item.Bin;
            Fade(_binDim, _highlightBin >= 0 ? 1 : 0);
        }
        else if (ReferenceEquals(_highlightItem, item))
        {
            _highlightItem = null;
            _highlightBin = -1;
            Fade(_binDim, 0);
        }
        InvalidateLegend();
        InvalidateVisual();
    }

    private void SyncTooltip()
    {
        var index = EffectiveActive;
        if (index < 0 || Bounds.Width <= 0 || _radius <= 0)
        {
            _tooltip.Hide();
            if (index < 0)
            {
                _tipLines.Clear();
            }
            _overlay.InvalidateVisual();
            return;
        }
        if (_tipStale || _tipSector != index || _tipBin != _activeBin)
        {
            _tipStale = false;
            _tipSector = index;
            _tipBin = _activeBin;
            BuildTooltip(index);
        }
        _tooltip.Show(PlaceTooltip(index, Bounds.Size));
        _overlay.InvalidateVisual();
    }

    private void BuildTooltip(int index)
    {
        var p = _palette;
        EnsureColors();
        var bins = _model.BinCount;
        var label = _sectors[index].Label;
        var total = Format(_model.SectorStacks[index]);
        _tipRows.Clear();
        _tipLines.Clear();
        _tipRows.Add(new ChartsBTipRow
        {
            Left = ChartsBText.Run(label, p, p.TypeSM, p.Foreground, FontWeight.Medium),
            Right = ChartsBText.Run(total, p, p.TypeSM, p.Foreground, FontWeight.Medium),
            MinHeight = p.TypeSM * 1.5
        });
        _tipLines.Add($"{label} {total}");
        for (var k = 0; k < bins; k++)
        {
            var at = index * bins + k;
            if (_model.Raw[at] <= 0)
            {
                continue;
            }
            var lit = k == _activeBin;
            var value = Format(_model.Display[at]);
            _tipRows.Add(new ChartsBTipRow
            {
                Swatch = _colors[k],
                Shape = ChartsBSwatchShape.Square,
                MarginTop = 2,
                MinHeight = p.TypeXS * 1.5,
                Left = ChartsBText.Run(_bins[k].Label, p, p.TypeXS, lit ? p.Foreground : p.TextSecondary, lit ? FontWeight.Medium : FontWeight.Normal),
                Right = ChartsBText.Run(value, p, p.TypeXS, p.Foreground, lit ? FontWeight.Medium : FontWeight.Normal)
            });
            _tipLines.Add($"{_bins[k].Label} {value}");
        }
        _tooltip.MaxWidth = Math.Max(140, Math.Min(260, Bounds.Width));
        _tooltip.MinWidth = Math.Min(170, _tooltip.MaxWidth);
        _tooltip.SetRows(_tipRows);
    }

    /// <summary>
    /// Beside the end of the petal, so the petal being read stays visible: on the side it points to, else above or below
    /// it on the outward side, else on the other side; always inside the chart. Of the places that do not fit untouched,
    /// the one that covers the least of the petal wins.
    /// </summary>
    private Point PlaceTooltip(int index, Size box)
    {
        var size = _tooltip.Size;
        var (ux, uy) = Ray(index);
        var hole = HoleRadius;
        var reach = Math.Max(RadiusOf(_model.SectorStacks[index], _model.Maximum), hole + (_radius - hole) * 0.3);
        var anchor = new Point(_center.X + ux * reach, _center.Y + uy * reach);
        var gap = TooltipGap;
        var h = ux >= -0.05 ? 1 : -1;
        var v = uy >= 0 ? 1 : -1;
        double X(int side) => side > 0 ? anchor.X + gap : anchor.X - gap - size.Width;
        double Y(int side) => side > 0 ? anchor.Y + gap : anchor.Y - gap - size.Height;
        var midX = anchor.X - size.Width / 2;
        var midY = anchor.Y - size.Height / 2;
        // Outward first (the side the petal points to), then the corners and sides that turn away from it.
        Span<Point> candidates =
        [
            new(X(h), midY), new(X(h), Y(v)), new(midX, Y(v)), new(X(h), Y(-v)),
            new(X(-h), Y(v)), new(X(-h), midY), new(midX, Y(-v)), new(X(-h), Y(-v))
        ];
        var best = default(Point);
        var bestCover = int.MaxValue;
        var turn = CenterTurn(index);
        var spread = ChartsBMath.Clamp(SectorFill, 0.05, 1) * 0.4 / Math.Max(1, _model.SectorCount);
        foreach (var candidate in candidates)
        {
            var place = new Point(ChartsBMath.Clamp(candidate.X, 0, Math.Max(0, box.Width - size.Width)),
                ChartsBMath.Clamp(candidate.Y, 0, Math.Max(0, box.Height - size.Height)));
            var rect = new Rect(place, size);
            // How much of the petal (sampled along its middle and both flanks, out to its end) and its label the bubble would hide.
            var cover = 0;
            for (var side = -1; side <= 1; side++)
            {
                var angle = (turn + side * spread) * 2 * Math.PI - Math.PI / 2;
                var (cx, cy) = (Math.Cos(angle), Math.Sin(angle));
                for (var i = 0; i <= 8; i++)
                {
                    var r = hole + (reach - hole) * i / 8;
                    if (rect.Contains(new Point(_center.X + cx * r, _center.Y + cy * r)))
                    {
                        cover++;
                    }
                }
            }
            if (index < _labelBoxes.Length && rect.Intersects(_labelBoxes[index]))
            {
                cover += 2;
            }
            if (cover < bestCover)
            {
                best = place;
                bestCover = cover;
                if (cover == 0)
                {
                    break;
                }
            }
        }
        return best;
    }

    // ---- input ------------------------------------------------------------------------------------------------

    /// <summary>The sector under a point (its petal's wedge out to the labels) and the bin of the band under it, or -1.</summary>
    internal (int Sector, int Bin) HitTest(Point point)
    {
        if (IsEmpty || _radius <= 0 || _sectors.Count == 0)
        {
            return (-1, -1);
        }
        var dx = point.X - _center.X;
        var dy = point.Y - _center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var hole = HoleRadius;
        if (distance < hole || distance > _radius + _gap + _labelHeight + 2)
        {
            return (-1, -1);
        }
        var count = _sectors.Count;
        var turn = (Math.Atan2(dy, dx) + Math.PI / 2) / (2 * Math.PI);
        var relative = (turn - StartAngle / 360) * count * Direction;
        var sector = (int)Math.Floor(relative + 0.5);
        sector = ((sector % count) + count) % count;
        var bin = -1;
        if (distance <= _radius)
        {
            var bins = _model.BinCount;
            var max = Math.Max(1e-9, _maximum.Value);
            var below = 0.0;
            for (var k = 0; k < bins; k++)
            {
                var top = Math.Max(below, CurrentTop(sector, k));
                if (top > below && distance >= RadiusOf(below, max) && distance < RadiusOf(top, max))
                {
                    bin = k;
                    break;
                }
                below = top;
            }
        }
        return (sector, bin);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsEmpty && (e.Pointer.Type == PointerType.Mouse || ReferenceEquals(e.Pointer.Captured, this)))
        {
            var (sector, bin) = HitTest(e.GetPosition(this));
            SetActive(sector, bin);
            Cursor = sector >= 0 ? ChartsBMath.HandCursor : Cursor.Default;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEmpty || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // A touch press reads the sector under the finger; holding on to the pointer lets a drag sweep the others.
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(this);
        }
        var (sector, bin) = HitTest(e.GetPosition(this));
        _pressedSector = sector;
        _pressedBin = bin;
        SetActive(sector, bin);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pressed = _pressedSector;
        _pressedSector = -1;
        if (pressed < 0 || IsEmpty)
        {
            return;
        }
        var (sector, bin) = HitTest(e.GetPosition(this));
        if (sector == pressed)
        {
            e.Handled = true;
            Invoke(sector, bin == _pressedBin ? bin : -1);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressedSector = -1;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetActive(-1, -1);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Focus events of the legend entries bubble through here and are not about the rose.
        if (ReferenceEquals(e.Source, this) && (e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional) &&
            !IsEmpty && EffectiveActive < 0)
        {
            SetActive(EffectiveSelected >= 0 ? EffectiveSelected : 0, -1);
        }
        _overlay.InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (ReferenceEquals(e.Source, this))
        {
            SetActive(-1, -1);
        }
        _overlay.InvalidateVisual();
    }

    /// <summary>
    /// Right / Down read the next sector clockwise on screen, Left / Up the next one counter-clockwise, Home / End the first
    /// and last, Enter / Space choose the sector, Escape clears the reading and then the selection.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Keys pressed on a legend entry bubble through here and are not about the sectors.
        if (e.Handled || IsEmpty || !ReferenceEquals(e.Source, this))
        {
            return;
        }
        var count = _sectors.Count;
        var at = EffectiveActive;
        var clockwise = Direction;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                SetActive(at < 0 ? 0 : (((at + clockwise) % count) + count) % count, -1);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Up:
                SetActive(at < 0 ? 0 : (((at - clockwise) % count) + count) % count, -1);
                e.Handled = true;
                break;
            case Key.Home:
                SetActive(0, -1);
                e.Handled = true;
                break;
            case Key.End:
                SetActive(count - 1, -1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (at >= 0)
                {
                    Invoke(at, -1);
                    e.Handled = true;
                }
                break;
            case Key.Escape when at >= 0:
                SetActive(-1, -1);
                e.Handled = true;
                break;
            case Key.Escape when IsSelectionEnabled && SelectedIndex >= 0:
                SetCurrentValue(SelectedIndexProperty, -1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>The user chose a sector: report it, and select it when selection is on.</summary>
    private void Invoke(int sector, int bin)
    {
        if (sector < 0 || sector >= _sectors.Count)
        {
            return;
        }
        RaiseEvent(new WindRoseItemInvokedEventArgs(sector, _sectors[sector], bin));
        if (IsSelectionEnabled)
        {
            SetCurrentValue(SelectedIndexProperty, sector);
        }
    }

    // ---- accessibility ----------------------------------------------------------------------------------------

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, Reading ?? ((IChartsBAccessible)this).ChartName);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "wind rose chart";

    string IChartsBAccessible.ChartName => IsEmpty
        ? $"{Title}. {EmptyLabel}."
        : $"{Title}. Arrow keys move between the directions, Home and End go to the first and last, Enter chooses, Escape clears.";

    string IChartsBAccessible.ChartHelp => Reading ?? Summary;
}
