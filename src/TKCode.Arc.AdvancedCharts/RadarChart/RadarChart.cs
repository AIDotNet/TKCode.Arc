using System.Collections;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A radar chart: several profiles drawn on one web of measures. Replacing a profile morphs its shape on a gently
/// bouncing spring; sweeping the axes with the pointer or the keyboard compares each profile's value at that axis.
/// </summary>
public partial class RadarChart : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>The measures, at least three, clockwise from the top. Add <see cref="RadarAxis"/> items in XAML or bind any list; an observable list is followed.</summary>
    public static readonly StyledProperty<IList<RadarAxis>?> AxesProperty =
        AvaloniaProperty.Register<RadarChart, IList<RadarAxis>?>(nameof(Axes));

    /// <summary>The profiles, in drawing and colour order. Add <see cref="RadarSeries"/> items in XAML or bind any list; an observable list is followed.</summary>
    public static readonly StyledProperty<IList<RadarSeries>?> SeriesProperty =
        AvaloniaProperty.Register<RadarChart, IList<RadarSeries>?>(nameof(Series));

    /// <summary>What the chart compares, such as "Team skills"; names it for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RadarChart, string?>(nameof(Label));

    /// <summary>The value at the outer ring (default 100). <c>RadarAxis.Maximum</c> wins on its axis.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RadarChart, double>(nameof(Maximum), 100);

    /// <summary>Number of rings in the web (default 4).</summary>
    public static readonly StyledProperty<int> RingsProperty =
        AvaloniaProperty.Register<RadarChart, int>(nameof(Rings), 4);

    /// <summary>Draws the rings as circles (default) or as polygons with a corner on each axis.</summary>
    public static readonly StyledProperty<RadarGridShape> GridShapeProperty =
        AvaloniaProperty.Register<RadarChart, RadarGridShape>(nameof(GridShape));

    /// <summary>Opacity of each profile's fill, from 0 to 1 (default 0.18).</summary>
    public static readonly StyledProperty<double> FillOpacityProperty =
        AvaloniaProperty.Register<RadarChart, double>(nameof(FillOpacity), 0.18);

    /// <summary>Largest diameter of the web in pixels (default 320); it shrinks to fit the space.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<RadarChart, double>(nameof(Size), 320);

    /// <summary>Formats a value on its axis for the tooltip and assistive technology (default: grouped, one decimal at most, true minus sign).</summary>
    public static readonly StyledProperty<Func<double, RadarAxis, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<RadarChart, Func<double, RadarAxis, string>?>(nameof(ValueFormatter));

    /// <summary>Key of the axis being read (two-way), or null. A key that matches no axis shows no reading.</summary>
    public static readonly StyledProperty<string?> ActiveAxisProperty =
        AvaloniaProperty.Register<RadarChart, string?>(nameof(ActiveAxis), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the legend: each profile's name and line key (default true).</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<RadarChart, bool>(nameof(IsLegendVisible), true);

    /// <summary>Message shown with fewer than three axes or no series.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<RadarChart, string>(nameof(EmptyLabel), "No data");

    internal static readonly StyledProperty<double> LabelGapProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(LabelGap), 12);
    internal static readonly StyledProperty<double> CompactLabelGapProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(CompactLabelGap), 6);
    internal static readonly StyledProperty<double> LabelWidthProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(LabelWidth), 112);
    internal static readonly StyledProperty<double> CompactLabelWidthProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(CompactLabelWidth), 68);
    internal static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(StrokeThickness), 2);
    internal static readonly StyledProperty<double> MarkerRadiusProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(MarkerRadius), 4);
    internal static readonly StyledProperty<double> LineKeyWidthProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(LineKeyWidth), 22);
    internal static readonly StyledProperty<double> LegendGapProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(LegendGap), 14);
    internal static readonly StyledProperty<double> TooltipGapProperty = AvaloniaProperty.Register<RadarChart, double>(nameof(TooltipGap), 10);

    private const double DefaultMaximum = 100;

    private readonly ChartsBPalette _palette;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly ChartsBItemsWatcher _axesWatcher;
    private readonly ChartsBItemsWatcher _seriesWatcher;
    private readonly ChartsBOverlay _overlay;
    private readonly WrapPanel _legend;
    private readonly AvaloniaList<RadarAxis> _inlineAxes = new();
    private readonly AvaloniaList<RadarSeries> _inlineSeries = new();
    private readonly MotionValue<double> _morph;
    private readonly MotionValue<double> _unfold;
    private readonly MotionValue<double> _axisPosition;
    private readonly MotionValue<double> _markerFade;
    private readonly MotionValue<double> _dim;
    private readonly MotionValue<Point> _tipAt;
    private readonly MotionValue<double> _tipFade;
    private readonly List<ChartsBTipRow> _tipRows = new();
    private readonly List<IPen?> _tipPens = new();
    private readonly List<string> _tipLines = new();
    private List<RadarAxis> _axes = new();
    private List<RadarSeries> _series = new();
    private List<RadarChartShape> _shapes = new();
    private Dictionary<string, int> _axisIndex = new();
    private double[] _unitX = [];
    private double[] _unitY = [];
    private bool _morphing;
    private int _generation;
    private int _forward = -1;
    private RadarChartLegendItem? _forwardItem;
    private int _tipAxis = -1;
    private bool _tipStale = true;
    private ChartsBAutomationPeer? _peer;

    /// <summary>Creates a radar chart.</summary>
    public RadarChart()
    {
        _overlay = new ChartsBOverlay(PaintOverlay);
        _legend = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, ItemSpacing = 6, LineSpacing = 2 };
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _reveal = new ChartsBReveal(this, 0.3, OnRevealed);
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, _overlay.InvalidateVisual) { MinWidth = 170 };
        _morph = new MotionValue<double>(this, 1, MotionVectors.Double);
        _morph.Changed += OnMorph;
        _unfold = Painted(0);
        _axisPosition = Painted(0);
        _markerFade = Painted(0);
        _dim = Painted(0);
        _tipAt = new MotionValue<Point>(this, default, MotionVectors.Point);
        _tipAt.Changed += _ => _overlay.InvalidateVisual();
        _tipFade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _tipFade.Changed += _ => _overlay.InvalidateVisual();
        _axesWatcher = new ChartsBItemsWatcher(Rebuild);
        _seriesWatcher = new ChartsBItemsWatcher(Rebuild);
        _inlineAxes.CollectionChanged += (_, _) =>
        {
            // While attached the watcher reports inline changes itself.
            if (!_axesWatcher.IsSubscribed && ReferenceEquals(_axesWatcher.Source, _inlineAxes))
            {
                Rebuild();
            }
        };
        _inlineSeries.CollectionChanged += (_, _) =>
        {
            if (!_seriesWatcher.IsSubscribed && ReferenceEquals(_seriesWatcher.Source, _inlineSeries))
            {
                Rebuild();
            }
        };
        VisualChildren.Add(_legend);
        LogicalChildren.Add(_legend);
        VisualChildren.Add(_overlay);
        SetCurrentValue(AxesProperty, _inlineAxes);
        SetCurrentValue(SeriesProperty, _inlineSeries);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        UpdateAccessibility();
    }

    static RadarChart()
    {
        AffectsMeasure<RadarChart>(SizeProperty, IsLegendVisibleProperty, LabelGapProperty, CompactLabelGapProperty, LabelWidthProperty,
            CompactLabelWidthProperty, LineKeyWidthProperty, LegendGapProperty, TooltipGapProperty);
        AffectsRender<RadarChart>(RingsProperty, GridShapeProperty, MarkerRadiusProperty);
    }

    private MotionValue<double> Painted(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <inheritdoc cref="AxesProperty"/>
    public IList<RadarAxis>? Axes
    {
        get => GetValue(AxesProperty);
        set => SetValue(AxesProperty, value);
    }

    /// <inheritdoc cref="SeriesProperty"/>
    public IList<RadarSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <inheritdoc cref="RingsProperty"/>
    public int Rings
    {
        get => GetValue(RingsProperty);
        set => SetValue(RingsProperty, value);
    }

    /// <inheritdoc cref="GridShapeProperty"/>
    public RadarGridShape GridShape
    {
        get => GetValue(GridShapeProperty);
        set => SetValue(GridShapeProperty, value);
    }

    /// <inheritdoc cref="FillOpacityProperty"/>
    public double FillOpacity
    {
        get => GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, RadarAxis, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <inheritdoc cref="ActiveAxisProperty"/>
    public string? ActiveAxis
    {
        get => GetValue(ActiveAxisProperty);
        set => SetValue(ActiveAxisProperty, value);
    }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible
    {
        get => GetValue(IsLegendVisibleProperty);
        set => SetValue(IsLegendVisibleProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when the axis being read changes.</summary>
    public event EventHandler<RadarChartActiveAxisChangedEventArgs>? ActiveAxisChanged;

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

    internal double LabelWidth
    {
        get => GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    internal double CompactLabelWidth
    {
        get => GetValue(CompactLabelWidthProperty);
        set => SetValue(CompactLabelWidthProperty, value);
    }

    internal double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    internal double MarkerRadius
    {
        get => GetValue(MarkerRadiusProperty);
        set => SetValue(MarkerRadiusProperty, value);
    }

    internal double LineKeyWidth
    {
        get => GetValue(LineKeyWidthProperty);
        set => SetValue(LineKeyWidthProperty, value);
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

    /// <summary>Accessible summary: every profile's value on every axis.</summary>
    public string Summary
    {
        get
        {
            if (IsEmpty)
            {
                return $"{Title}. {EmptyLabel}.";
            }
            var profiles = _shapes.Select(shape =>
                $"{shape.Series.Label}: {string.Join(", ", _axes.Select((axis, i) => $"{axis.Label} {Spoken(shape.Values[i], axis)}"))}");
            return $"{Title}. {string.Join("; ", profiles)}.";
        }
    }

    private string Title => string.IsNullOrEmpty(Label) ? "Radar chart" : Label;

    private bool IsEmpty => _axes.Count < 3 || _series.Count == 0;

    private int ActiveIndex => ActiveAxis is { } key && !IsEmpty && _axisIndex.TryGetValue(key, out var index) ? index : -1;

    internal string? Reading
    {
        get
        {
            var index = ActiveIndex;
            if (index < 0)
            {
                return null;
            }
            var axis = _axes[index];
            return $"{axis.Label}: {string.Join(", ", _shapes.Select(shape => $"{shape.Series.Label} {Spoken(shape.Values[index], axis)}"))}";
        }
    }

    internal bool IsShowingEmptyLabel => IsEmpty;
    internal Point WebCenter => _center;
    internal double WebRadius => _radius;
    internal bool IsCompact => _compact;
    internal double CurrentLabelGap => _gap;
    internal IReadOnlyList<string> TooltipLines => _tipLines;
    internal bool IsTooltipOpen => _tooltip.IsOpen;
    internal Rect TooltipRect => new(_tipAt.Target, _tooltip.Size);
    internal bool IsMorphing => _morphing;
    internal bool IsRevealed => _reveal.IsRevealed;
    internal WrapPanel Legend => _legend;
    internal string? ForwardSeries => _forward >= 0 && _forward < _shapes.Count && _dim.Target > 0 ? _shapes[_forward].Series.Key : null;
    internal Rect LabelBox(int axis) => _labels[axis].Box;
    internal int LabelLineCount(int axis) => _labels[axis].Rest.TextLines.Count;

    /// <summary>Where a profile's vertex on an axis is drawn, in chart coordinates.</summary>
    internal Point VertexOf(string series, string axis)
    {
        var shape = _shapes.First(s => s.Series.Key == series);
        var index = _axisIndex[axis];
        return new Point(_center.X + shape.NowX[index] * _radius, _center.Y + shape.NowY[index] * _radius);
    }

    /// <summary>The outer polygon of the web as it is drawn with <see cref="RadarGridShape.Polygon"/>.</summary>
    internal Rect OuterPolygonBounds()
    {
        EnsurePolygonRings(Math.Max(1, Rings));
        return _polygonRings[^1].Bounds;
    }

    // ---- data -------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AxesProperty)
        {
            IEnumerable? source = change.GetNewValue<IList<RadarAxis>?>();
            _axesWatcher.SetSource(source ?? _inlineAxes);
        }
        else if (change.Property == SeriesProperty)
        {
            IEnumerable? source = change.GetNewValue<IList<RadarSeries>?>();
            _seriesWatcher.SetSource(source ?? _inlineSeries);
        }
        else if (change.Property == MaximumProperty)
        {
            Rebuild();
        }
        else if (change.Property == ActiveAxisProperty)
        {
            OnActiveAxisChanged();
        }
        else if (change.Property == FillOpacityProperty || change.Property == StrokeThicknessProperty)
        {
            _tipStale = true;
            InvalidateInk();
            InvalidateVisual();
        }
        else if (change.Property == GridShapeProperty || change.Property == RingsProperty)
        {
            _ringsStale = true;
        }
        else if (change.Property == ValueFormatterProperty)
        {
            _tipStale = true;
            SyncTooltip(Bounds.Size);
            UpdateAccessibility();
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
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _axesWatcher.Attach();
        _seriesWatcher.Attach();
        _reveal.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _axesWatcher.Detach();
        _seriesWatcher.Detach();
        _reveal.Detach();
    }

    private void OnPaletteChanged()
    {
        _tipStale = true;
        _emptyText = null;
        InvalidateInk();
        InvalidateMeasure();
        InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    /// <summary>Reads the axes and series again and sends each shape from where it is on screen to its new place.</summary>
    private void Rebuild()
    {
        var axes = new List<RadarAxis>();
        var axisIndex = new Dictionary<string, int>();
        foreach (var item in _axesWatcher.Snapshot())
        {
            if (item is RadarAxis axis && axisIndex.TryAdd(axis.Key, axes.Count))
            {
                axes.Add(axis);
            }
        }
        var series = new List<RadarSeries>();
        var keys = new HashSet<string>();
        foreach (var item in _seriesWatcher.Snapshot())
        {
            if (item is RadarSeries profile && keys.Add(profile.Key))
            {
                series.Add(profile);
            }
        }
        var previous = _shapes;
        var previousIndex = _axisIndex;
        var byKey = previous.ToDictionary(s => s.Series.Key);
        var unitX = new double[axes.Count];
        var unitY = new double[axes.Count];
        for (var i = 0; i < axes.Count; i++)
        {
            var angle = i * 2 * Math.PI / axes.Count;
            unitX[i] = Math.Sin(angle);
            unitY[i] = -Math.Cos(angle);
        }
        var shapes = new List<RadarChartShape>(series.Count);
        for (var s = 0; s < series.Count; s++)
        {
            var shape = new RadarChartShape(series[s], s, axes.Count);
            // A series keeps its own shape; a new one takes over the shape of the series it replaced in that position.
            if (!byKey.TryGetValue(series[s].Key, out var origin) && s < previous.Count && !keys.Contains(previous[s].Series.Key))
            {
                origin = previous[s];
            }
            for (var a = 0; a < axes.Count; a++)
            {
                var value = series[s].ValueOf(axes[a].Key);
                var ratio = Ratio(value, axes[a]);
                shape.Values[a] = value;
                shape.ToX[a] = unitX[a] * ratio;
                shape.ToY[a] = unitY[a] * ratio;
                if (origin is not null && previousIndex.TryGetValue(axes[a].Key, out var from))
                {
                    shape.FromX[a] = origin.NowX[from];
                    shape.FromY[a] = origin.NowY[from];
                }
            }
            shapes.Add(shape);
        }
        _axes = axes;
        _series = series;
        _axisIndex = axisIndex;
        _unitX = unitX;
        _unitY = unitY;
        _shapes = shapes;
        _inkVersion = -1;
        _labelsStale = true;
        _ringsStale = true;
        _tipStale = true;
        Focusable = !IsEmpty;
        if (_forward >= _shapes.Count)
        {
            _forward = -1;
            _forwardItem = null;
            _dim.Jump(0);
        }
        SyncLegend();
        StartMorph();
        SyncMarkers(false);
        UpdateAccessibility();
        InvalidateMeasure();
        InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private double Ratio(double value, RadarAxis axis)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }
        var maximum = axis.Maximum is > 0 ? axis.Maximum.Value : Maximum > 0 ? Maximum : DefaultMaximum;
        return ChartsBMath.Clamp(value / maximum, 0, 1);
    }

    private void SyncLegend()
    {
        var children = _legend.Children;
        while (children.Count > _shapes.Count)
        {
            children.RemoveAt(children.Count - 1);
        }
        for (var i = 0; i < _shapes.Count; i++)
        {
            RadarChartLegendItem item;
            if (i < children.Count)
            {
                item = (RadarChartLegendItem)children[i];
            }
            else
            {
                item = new RadarChartLegendItem(this);
                children.Add(item);
            }
            item.Configure(_shapes[i]);
        }
    }

    private void StartMorph()
    {
        if (!_reveal.IsRevealed || !ArcMotion.IsFull(this) || !IsLoaded)
        {
            FinishMorph();
            return;
        }
        _morphing = true;
        _morph.Jump(0);
        _ = RunMorph(++_generation);
    }

    private async Task RunMorph(int generation)
    {
        if (await _morph.AnimateTo(1, MotionSpec.Spring(ChartsBMath.Physical(0.62, 0.24))) && generation == _generation)
        {
            FinishMorph();
        }
    }

    private void OnMorph(double t)
    {
        if (!_morphing)
        {
            return;
        }
        foreach (var shape in _shapes)
        {
            shape.Advance(t);
            shape.Sync(_center, _radius);
        }
        InvalidateVisual();
    }

    private void FinishMorph()
    {
        _morphing = false;
        _generation++;
        foreach (var shape in _shapes)
        {
            shape.Advance(1);
            shape.Sync(_center, _radius);
        }
        InvalidateVisual();
    }

    private void OnRevealed(bool animate)
    {
        // The web unfolds from the centre as one transform, so nothing has to be rebuilt per frame.
        if (animate)
        {
            _unfold.Jump(0);
            _ = _unfold.AnimateTo(1, MotionSpec.Spring(ChartsBMath.Physical(0.8, 0.06)));
        }
        else
        {
            _unfold.Jump(1);
        }
        InvalidateVisual();
    }

    // ---- reading ----------------------------------------------------------------------------------------------

    private string Format(double value, RadarAxis axis)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value, axis);
        }
        return ChartsBMath.Grouped(value).Replace("-", "−");
    }

    private string Spoken(double value, RadarAxis axis) => double.IsFinite(value) ? Format(value, axis) : "no value";

    private void SetActive(string? key)
    {
        if (ActiveAxis != key)
        {
            SetCurrentValue(ActiveAxisProperty, key);
        }
    }

    private void OnActiveAxisChanged()
    {
        _tipStale = true;
        SyncMarkers(true);
        SyncTooltip(Bounds.Size);
        UpdateAccessibility();
        InvalidateVisual();
        var index = ActiveIndex;
        ActiveAxisChanged?.Invoke(this, new RadarChartActiveAxisChangedEventArgs(ActiveAxis, index >= 0 ? _axes[index] : null));
    }

    /// <summary>Markers appear in place, then glide between axes the short way round.</summary>
    private void SyncMarkers(bool glide)
    {
        var index = ActiveIndex;
        var full = ArcMotion.IsFull(this) && IsLoaded;
        if (index < 0)
        {
            Fade(_markerFade, 0, full);
            return;
        }
        var count = _axes.Count;
        if (!full || !glide || _markerFade.Value <= 0.001)
        {
            _axisPosition.Jump(index);
        }
        else
        {
            var at = _axisPosition.Target;
            var turn = (index - at) % count;
            if (turn > count / 2.0)
            {
                turn -= count;
            }
            else if (turn < -count / 2.0)
            {
                turn += count;
            }
            _ = _axisPosition.AnimateTo(at + turn, MotionSpec.Spring(ArcMotionPresets.Snappy));
        }
        Fade(_markerFade, 1, full);
    }

    private static void Fade(MotionValue<double> value, double target, bool animate)
    {
        if (animate)
        {
            _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            value.Jump(target);
        }
    }

    private void SyncTooltip(Size box)
    {
        var index = ActiveIndex;
        var full = ArcMotion.IsFull(this) && IsLoaded;
        if (index < 0 || box.Width <= 0 || index >= _labels.Count)
        {
            if (_tooltip.IsOpen)
            {
                _tooltip.Hide();
                Fade(_tipFade, 0, full);
            }
            _overlay.InvalidateVisual();
            return;
        }
        if (_tipStale || _tipAxis != index)
        {
            _tipStale = false;
            _tipAxis = index;
            BuildTooltip(index, box.Width);
        }
        var opening = !_tooltip.IsOpen;
        var place = PlaceTooltip(index, box);
        // ChartsBTooltip draws at the origin; the chart moves it, so the line keys ride along.
        _tooltip.Show(default);
        if (opening || !full)
        {
            _tipAt.Jump(place);
            Fade(_tipFade, 1, full);
        }
        else if (_tipAt.Target != place)
        {
            _ = _tipAt.AnimateTo(place, MotionSpec.Spring(ArcMotionPresets.Snappy));
        }
        _overlay.InvalidateVisual();
    }

    private void BuildTooltip(int index, double width)
    {
        var p = _palette;
        EnsureInk();
        var axis = _axes[index];
        var xs = p.TypeXS;
        _tipRows.Clear();
        _tipPens.Clear();
        _tipLines.Clear();
        _tipRows.Add(new ChartsBTipRow { Left = ChartsBText.Run(axis.Label, p, xs, p.TextSecondary), MinHeight = xs * 1.5 });
        _tipPens.Add(null);
        _tipLines.Add(axis.Label);
        foreach (var shape in _shapes)
        {
            var value = shape.Values[index];
            var text = double.IsFinite(value) ? Format(value, axis) : "—";
            _tipRows.Add(new ChartsBTipRow
            {
                Indent = LineKeyWidth + 7,
                MarginTop = 2,
                MinHeight = xs * 1.5,
                Left = ChartsBText.Run(shape.Series.Label, p, xs, p.TextSecondary),
                Right = ChartsBText.Run(text, p, p.TypeSM, p.Foreground, FontWeight.Medium)
            });
            _tipPens.Add(shape.Pen);
            _tipLines.Add($"{shape.Series.Label} {text}");
        }
        _tooltip.MaxWidth = Math.Max(120, Math.Min(260, width));
        _tooltip.MinWidth = Math.Min(170, _tooltip.MaxWidth);
        _tooltip.SetRows(_tipRows);
    }

    /// <summary>
    /// Beyond the axis label, outward from the web. Without room there it moves off the label rather than over it, so the
    /// label and markers being read stay visible, and it never leaves the chart.
    /// </summary>
    private Point PlaceTooltip(int index, Size box)
    {
        var size = _tooltip.Size;
        var label = _labels[index].Box;
        var gap = TooltipGap;
        var side = Side(_unitX[index]);
        var above = label.Y - gap - size.Height;
        var below = label.Bottom + gap;
        var fitsAbove = above >= 0;
        var fitsBelow = below + size.Height <= box.Height;
        double x;
        double y;
        if (side != 0)
        {
            x = side > 0 ? label.Right + gap : label.X - gap - size.Width;
            y = label.Center.Y - size.Height / 2;
            if (x < 0 || x + size.Width > box.Width)
            {
                // No room outward: over or under the label, on the side of the web it points to.
                x = side > 0 ? label.Right - size.Width : label.X;
                y = (_unitY[index] <= 0 && fitsAbove) || !fitsBelow ? above : below;
            }
        }
        else
        {
            x = label.Center.X - size.Width / 2;
            y = _unitY[index] < 0 ? above : below;
            if (_unitY[index] < 0 ? !fitsAbove : !fitsBelow)
            {
                var right = label.Right + gap;
                var left = label.X - gap - size.Width;
                if (right + size.Width <= box.Width)
                {
                    x = right;
                    y = label.Y;
                }
                else if (left >= 0)
                {
                    x = left;
                    y = label.Y;
                }
                else
                {
                    // Too narrow beside it as well: the far edge of the chart leaves this end of the axis clear.
                    y = _unitY[index] < 0 ? box.Height - size.Height : 0;
                }
            }
        }
        return new Point(ChartsBMath.Clamp(x, 0, Math.Max(0, box.Width - size.Width)), ChartsBMath.Clamp(y, 0, Math.Max(0, box.Height - size.Height)));
    }

    /// <summary>Called by a legend entry on hover and focus; a focused entry keeps its shape forward when the pointer leaves.</summary>
    internal void BringForward(RadarChartLegendItem item, bool on)
    {
        var full = ArcMotion.IsFull(this) && IsLoaded;
        if (on)
        {
            _forwardItem = item;
            _forward = item.Shape?.Index ?? -1;
            Fade(_dim, _forward >= 0 ? 1 : 0, full);
        }
        else if (ReferenceEquals(_forwardItem, item))
        {
            _forwardItem = null;
            Fade(_dim, 0, full);
        }
        InvalidateVisual();
    }

    // ---- input ------------------------------------------------------------------------------------------------

    /// <summary>The axis nearest the point, or null when it is outside the web and its labels.</summary>
    private string? AxisAt(Point point)
    {
        var dx = point.X - _center.X;
        var dy = point.Y - _center.Y;
        var reach = _radius + _gap + 22;
        if (dx * dx + dy * dy > reach * reach || !_webRect.Contains(point))
        {
            return null;
        }
        var count = _axes.Count;
        var turn = Math.Atan2(dx, -dy) / (2 * Math.PI);
        var index = (int)Math.Round(turn * count);
        index = ((index % count) + count) % count;
        return _axes[index].Key;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsEmpty && (e.Pointer.Type == PointerType.Mouse || ReferenceEquals(e.Pointer.Captured, this)))
        {
            SetActive(AxisAt(e.GetPosition(this)));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEmpty)
        {
            return;
        }
        // A touch press reads the axis under the finger; holding on to the pointer lets a drag sweep the others.
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(this);
        }
        SetActive(AxisAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Focus events of the legend entries bubble through here and are not about the web.
        if (ReferenceEquals(e.Source, this) && (e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional) && !IsEmpty && ActiveIndex < 0)
        {
            SetActive(_axes[0].Key);
        }
        _overlay.InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (ReferenceEquals(e.Source, this))
        {
            SetActive(null);
        }
        _overlay.InvalidateVisual();
    }

    /// <summary>Right / Down read the next axis clockwise, Left / Up the previous, Home / End the first and last, Escape clears.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Keys pressed on a legend entry bubble through here and are not about the axes.
        if (e.Handled || IsEmpty || !ReferenceEquals(e.Source, this))
        {
            return;
        }
        var count = _axes.Count;
        var at = ActiveIndex;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                SetActive(_axes[at < 0 ? 0 : (at + 1) % count].Key);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Up:
                SetActive(_axes[at < 0 ? count - 1 : (at + count - 1) % count].Key);
                e.Handled = true;
                break;
            case Key.Home:
                SetActive(_axes[0].Key);
                e.Handled = true;
                break;
            case Key.End:
                SetActive(_axes[count - 1].Key);
                e.Handled = true;
                break;
            case Key.Escape when ActiveAxis is not null:
                SetActive(null);
                e.Handled = true;
                break;
        }
    }

    // ---- accessibility ----------------------------------------------------------------------------------------

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, Reading ?? ((IChartsBAccessible)this).ChartName);
        _peer?.Announce();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "radar chart";

    string IChartsBAccessible.ChartName => IsEmpty ? $"{Title}. {EmptyLabel}." : $"{Title}. Arrow keys move between the axes, Home and End go to the first and last, Escape clears.";

    string IChartsBAccessible.ChartHelp => Reading ?? Summary;
}
