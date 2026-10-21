using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Arc's line chart: one or more measures over time with series toggles, a crosshair that glides between
/// points and a tooltip that follows on its own spring. Lines draw in from the left the first time the chart is
/// seen, and a new range morphs every line from the shape on screen (resampled on shared x positions) while
/// the value axis springs to its new range and the gridlines ride it. A hidden series flattens into the
/// baseline as it fades.
/// <para>
/// Motion is code-driven (<see cref="MotionValue{T}"/> and a per-point spring array) because every animated
/// value is a coordinate inside one self-drawn geometry, which ControlTheme transitions cannot express.
/// </para>
/// </summary>
/// <example><code>
/// &lt;arc:LineChart Label="Signups" ItemsSource="{Binding Days}" XBinding="{CompiledBinding Date, DataType=local:Day}"&gt;
///     &lt;arc:ChartSeries Name="This period" ValueBinding="{CompiledBinding Current, DataType=local:Day}" /&gt;
///     &lt;arc:ChartSeries Name="Previous" ValueBinding="{CompiledBinding Previous, DataType=local:Day}" IsDashed="True" /&gt;
/// &lt;/arc:LineChart&gt;
/// </code></example>
public class LineChart : ChartBase
{
    private const double Top = 12;
    private const double TipGap = 14;
    private static readonly SpringSpec Settle = SpringSpec.FromVisual(0.4, 0);
    private static readonly SpringSpec Glide = SpringSpec.FromVisual(0.26, 0.12);

    /// <summary>Series toggles. Null shows the legend when there is more than one series.</summary>
    public static new readonly StyledProperty<bool?> ShowLegendProperty = ChartBase.ShowLegendProperty.AddOwner<LineChart>();
    /// <summary>Horizontal gridlines at the value ticks.</summary>
    public static new readonly StyledProperty<bool> ShowGridProperty = ChartBase.ShowGridProperty.AddOwner<LineChart>();
    /// <summary>The category (time) axis labels under the plot.</summary>
    public static new readonly StyledProperty<bool> ShowXAxisProperty = ChartBase.ShowXAxisProperty.AddOwner<LineChart>();
    /// <summary>The value axis labels in the right-hand gutter.</summary>
    public static new readonly StyledProperty<bool> ShowYAxisProperty = ChartBase.ShowYAxisProperty.AddOwner<LineChart>();
    /// <summary>Fixes the bottom of the value axis (default: zero, or the data minimum when it dips below).</summary>
    public static new readonly StyledProperty<double?> YMinimumProperty = ChartBase.YMinimumProperty.AddOwner<LineChart>();
    /// <summary>Fixes the top of the value axis (default: the next nice tick above the data).</summary>
    public static new readonly StyledProperty<double?> YMaximumProperty = ChartBase.YMaximumProperty.AddOwner<LineChart>();
    /// <summary>Formats value-axis ticks. Defaults to a compact number, such as 1.2K.</summary>
    public static new readonly StyledProperty<Func<double, string>?> TickFormatterProperty = ChartBase.TickFormatterProperty.AddOwner<LineChart>();
    /// <summary>Smooth monotone curves that never swing past the data, or straight segments.</summary>
    public static new readonly StyledProperty<ChartCurve> CurveProperty = ChartBase.CurveProperty.AddOwner<LineChart>();
    /// <summary>Keys of hidden series. Two-way; the legend toggles it.</summary>
    public static new readonly StyledProperty<IReadOnlyList<string>?> HiddenSeriesProperty = ChartBase.HiddenSeriesProperty.AddOwner<LineChart>();
    /// <summary>Keeps the current shapes on screen, dimmed and breathing, while the next range loads.</summary>
    public static new readonly StyledProperty<bool> IsLoadingProperty = ChartBase.IsLoadingProperty.AddOwner<LineChart>();

    /// <inheritdoc cref="ChartBase.ShowLegendProperty"/>
    public bool? ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowGridProperty"/>
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowXAxisProperty"/>
    public bool ShowXAxis { get => GetValue(ShowXAxisProperty); set => SetValue(ShowXAxisProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowYAxisProperty"/>
    public bool ShowYAxis { get => GetValue(ShowYAxisProperty); set => SetValue(ShowYAxisProperty, value); }

    /// <inheritdoc cref="ChartBase.YMinimumProperty"/>
    public double? YMinimum { get => GetValue(YMinimumProperty); set => SetValue(YMinimumProperty, value); }

    /// <inheritdoc cref="ChartBase.YMaximumProperty"/>
    public double? YMaximum { get => GetValue(YMaximumProperty); set => SetValue(YMaximumProperty, value); }

    /// <inheritdoc cref="ChartBase.TickFormatterProperty"/>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    /// <inheritdoc cref="ChartBase.CurveProperty"/>
    public ChartCurve Curve { get => GetValue(CurveProperty); set => SetValue(CurveProperty, value); }

    /// <inheritdoc cref="ChartBase.HiddenSeriesProperty"/>
    public IReadOnlyList<string>? HiddenSeries { get => GetValue(HiddenSeriesProperty); set => SetValue(HiddenSeriesProperty, value); }

    /// <inheritdoc cref="ChartBase.IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>Raised when the legend shows or hides a series; the argument carries the hidden series keys.</summary>
    /// <inheritdoc cref="ChartBase.HiddenSeriesChangedEvent"/>
    public event EventHandler<ChartHiddenSeriesChangedEventArgs>? HiddenSeriesChanged
    {
        add => AddHandler(HiddenSeriesChangedEvent, value);
        remove => RemoveHandler(HiddenSeriesChangedEvent, value);
    }

    private ChartLineMorph? _morph;
    private ChartValueAxis? _axis;
    private ChartTooltip? _tooltip;
    private ChartBreath? _breath;
    private MotionValue<double>? _cursor;
    private MotionValue<double>? _scrub;
    private readonly Dictionary<string, MotionValue<double>> _presence = new();
    private StreamGeometry?[] _lines = [];
    private StreamGeometry?[] _areas = [];
    private ChartSeriesData[] _drawn = [];
    private Point[] _points = [];
    private bool _geometryDirty = true;
    private readonly List<int> _picks = new();
    private double _picksWidth = -1;
    private ChartData? _picksData;
    private bool _wasScrubbing;
    private bool _scaleInitialized;
    private IImmutableBrush? _skeleton;
    private int _skeletonVersion = -1;

    static LineChart()
    {
        PlotHeightProperty.OverrideDefaultValue<LineChart>(220);
        AffectsPlot<LineChart>(CurveProperty);
    }

    public LineChart()
    {
    }

    private protected override bool UsesSeriesLegend => true;

    private protected override double RevealThreshold => 0.3;

    /// <summary>Arc draws the lines like a pen: 840ms on the in-out curve.</summary>
    private protected override MotionSpec RevealSpec => PenStroke;

    private ChartLineMorph Morph => _morph ??= CreateMorph();

    private ChartValueAxis Axis => _axis ??= CreateAxis();

    private ChartLineMorph CreateMorph()
    {
        var morph = new ChartLineMorph(this);
        morph.Changed += OnShapeFrame;
        return morph;
    }

    private ChartValueAxis CreateAxis()
    {
        var axis = new ChartValueAxis(this, InvalidatePlots);
        axis.ScaleChanged += OnShapeFrame;
        return axis;
    }

    private ChartTooltip Tip => _tooltip ??= new ChartTooltip(this, InvalidatePlots);

    private MotionValue<double> Crosshair => _cursor ??= CreateValue(1, OnCursorFrame);

    private MotionValue<double> Scrub => _scrub ??= CreateValue(0, InvalidatePlots);

    private ChartBreath Breath => _breath ??= new ChartBreath(this, InvalidatePlots);

    private MotionValue<double> CreateValue(double initial, Action changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => changed();
        return value;
    }

    private void OnShapeFrame()
    {
        _geometryDirty = true;
        UpdateTooltipTarget(false);
        InvalidatePlots();
    }

    private void OnCursorFrame()
    {
        UpdateTooltipTarget(false);
        InvalidatePlots();
    }

    #region Layout

    private double PlotWidth(double width) => Math.Max(0, width - (ShowYAxis ? GutterWidth : 0));

    internal override double MeasurePlotHeight(ChartPlot plot, double width) => PlotHeight + (ShowXAxis ? AxisRowHeight : 0);

    internal override void OnPlotResized(ChartPlot plot, Size size)
    {
        _geometryDirty = true;
        UpdateTooltipTarget(true);
    }

    #endregion

    #region Data

    private protected override void OnDataChanged(ChartData previous, ChartData current)
    {
        if (!previous.IsEmpty && !current.SameValues(previous))
        {
            // A new range lets go of the crosshair.
            SetActive(null);
        }
        var smooth = Curve == ChartCurve.Smooth;
        var keys = new string[current.Series.Length];
        var xs = new double[current.Series.Length][];
        var ys = new double[current.Series.Length][];
        for (var s = 0; s < current.Series.Length; s++)
        {
            keys[s] = current.Series[s].Key;
            (xs[s], ys[s]) = ChartCurves.SampleLine(current.Series[s].Values, smooth);
            if (!_presence.ContainsKey(keys[s]))
            {
                _presence[keys[s]] = CreateValue(IsHidden(keys[s]) ? 0 : 1, OnShapeFrame);
            }
        }
        Morph.SetTargets(keys, xs, ys, 241, IsMotionFull, Settle);
        _drawn = current.Series;
        _lines = new StreamGeometry?[_drawn.Length];
        _areas = new StreamGeometry?[_drawn.Length];
        _geometryDirty = true;
        UpdateScale(animate: !previous.IsEmpty);
        RebuildTooltip();
    }

    private void UpdateScale(bool animate)
    {
        double low = double.PositiveInfinity, high = double.NegativeInfinity;
        foreach (var series in VisibleSeries())
        {
            foreach (var value in series.Values)
            {
                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }
        }
        if (double.IsInfinity(low))
        {
            // Nothing visible: keep the current scale so the lines flatten in place (0 to 100 before any data).
            if (!_scaleInitialized)
            {
                _scaleInitialized = true;
                Axis.SetTicks(ChartTickSet.Default, FormatTick, false);
            }
            return;
        }
        _scaleInitialized = true;
        var ticks = ChartTicks.Nice(YMinimum ?? low, YMaximum ?? high);
        if (YMinimum.HasValue || YMaximum.HasValue)
        {
            ticks = Fix(ticks, YMinimum, YMaximum);
        }
        Axis.SetTicks(ticks, FormatTick, animate && IsRevealed);
    }

    private static ChartTickSet Fix(ChartTickSet ticks, double? min, double? max)
    {
        var lo = min ?? ticks.Min;
        var hi = max ?? ticks.Max;
        if (hi <= lo)
        {
            return ticks;
        }
        var list = new List<double>();
        foreach (var tick in ticks.Ticks)
        {
            if (tick >= lo - 1e-9 && tick <= hi + 1e-9)
            {
                list.Add(tick);
            }
        }
        return new ChartTickSet(lo, hi, ticks.Step, list.ToArray());
    }

    private protected override void OnHiddenSeriesChanged()
    {
        foreach (var series in Data.Series)
        {
            if (_presence.TryGetValue(series.Key, out var presence))
            {
                var to = IsHidden(series.Key) ? 0 : 1;
                _ = presence.AnimateTo(to, MotionSpec.Spring(Settle));
            }
        }
        UpdateScale(animate: true);
        RebuildTooltip();
        InvalidatePlots();
    }

    private protected override void OnFormattingChanged()
    {
        UpdateScale(animate: false);
        RebuildTooltip();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurveProperty || change.Property == YMinimumProperty || change.Property == YMaximumProperty)
        {
            OnDataChanged(Data, Data);
            InvalidatePlots();
        }
        else if (change.Property == IsLoadingProperty)
        {
            Breath.SetLoading(IsLoading);
            Announcement = IsLoading ? "Loading" : string.Empty;
            InvalidatePlots();
        }
    }

    #endregion

    #region Readout

    private protected override void OnActiveIndexChanged(int? previous, int? current)
    {
        var data = Data;
        var level = MotionLevel;
        if (current is { } index && index < data.Count)
        {
            var last = data.Count - 1;
            var to = last > 0 ? (double)index / last : 0.5;
            RebuildTooltip();
            if (!_wasScrubbing || level != ArcMotionLevel.Full)
            {
                Crosshair.Jump(to);
                UpdateTooltipTarget(true);
            }
            else
            {
                _ = Crosshair.AnimateTo(to, MotionSpec.Spring(Glide));
            }
            _wasScrubbing = true;
            _ = Scrub.AnimateTo(1, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            Tip.SetVisible(true);
        }
        else
        {
            _wasScrubbing = false;
            _ = Scrub.AnimateTo(0, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            Tip.SetVisible(false);
        }
        InvalidatePlots();
    }

    private void RebuildTooltip()
    {
        var data = Data;
        if (data.IsEmpty)
        {
            return;
        }
        var index = ActiveIndex is { } i && i < data.Count ? i : data.Count - 1;
        var tooltip = Tip;
        tooltip.Begin(140, 220);
        tooltip.AddTitle(data.Labels[index]);
        foreach (var series in VisibleSeries())
        {
            tooltip.AddRow(SeriesColor(series), series.IsDashed, series.Name, FormatWithUnit(series.Values[index], index, series));
        }
        UpdateTooltipTarget(false);
    }

    /// <summary>The tooltip sits beside the crosshair, flips before it would leave the plot, and centers on the points it describes.</summary>
    private void UpdateTooltipTarget(bool jump)
    {
        if (_tooltip is null || MainPlot is not { } plot || plot.Bounds.Width <= 0 || !_morph?.HasShape == true)
        {
            return;
        }
        EnsurePalette();
        var width = PlotWidth(plot.Bounds.Width);
        var height = PlotHeight;
        var size = _tooltip.Measure(Palette, Texts, width);
        var at = Crosshair.Value;
        var cx = Math.Round(at * width) + 0.5;
        double sum = 0;
        var count = 0;
        var morph = Morph;
        foreach (var series in Data.Series)
        {
            var s = morph.IndexOf(series.Key);
            var p = Presence(series.Key);
            if (s < 0 || p <= 0.5)
            {
                continue;
            }
            sum += ValueY(morph.ValueAt(s, at), p, height);
            count++;
        }
        var middle = count > 0 ? sum / count : height / 2;
        var target = ChartTooltip.Beside(size, cx, middle, new Rect(0, 0, width, height), TipGap);
        _tooltip.MoveTo(target, jump || !_wasScrubbing);
    }

    private double Presence(string key) => _presence.TryGetValue(key, out var value) ? value.Value : 1;

    private double ValueY(double value, double presence, double height)
    {
        var axis = Axis;
        var lo = axis.Min;
        return axis.Map(lo + (value - lo) * presence, Top, height);
    }

    #endregion

    #region Input

    private int PointAt(ChartPlot plot, double x)
    {
        var last = Data.Count - 1;
        var width = PlotWidth(plot.Bounds.Width);
        if (width <= 0 || last < 1)
        {
            return Math.Max(0, last);
        }
        return Math.Clamp((int)Math.Round(x / width * last), 0, last);
    }

    internal override void OnPlotPointerPressed(ChartPlot plot, PointerPressedEventArgs e)
    {
        if (Data.IsEmpty)
        {
            return;
        }
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(plot);
        }
        SetActive(PointAt(plot, e.GetPosition(plot).X));
    }

    internal override void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
        if (Data.IsEmpty)
        {
            return;
        }
        var position = e.GetPosition(plot);
        var mouse = e.Pointer.Type == PointerType.Mouse;
        if (mouse && (position.X > PlotWidth(plot.Bounds.Width) || position.Y > PlotHeight))
        {
            // The value gutter and the date axis are outside the scrubbable plot.
            SetActive(null);
            return;
        }
        if (mouse || e.GetCurrentPoint(plot).Properties.IsLeftButtonPressed)
        {
            SetActive(PointAt(plot, position.X));
        }
    }

    internal override void OnPlotPointerReleased(ChartPlot plot, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(null);
            SetActive(null);
        }
    }

    internal override void OnPlotPointerExited(ChartPlot plot, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    internal override void OnPlotPointerCaptureLost(ChartPlot plot, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            SetActive(null);
        }
    }

    internal override void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
        var data = Data;
        if (data.IsEmpty)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (ActiveIndex is not null)
            {
                e.Handled = true;
                SetActive(null);
            }
            return;
        }
        var page = Math.Max(1, (int)Math.Round(data.Count / 6.0));
        if (ChartKeyboard.TryStep(e.Key, ActiveIndex, 0, data.Count - 1, page, true, out var next))
        {
            e.Handled = true;
            SetActive(next);
        }
    }

    internal override void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
        if (!focused)
        {
            SetActive(null);
        }
        else if (keyboard && !Data.IsEmpty)
        {
            SetActive(ActiveIndex ?? Data.Count - 1);
        }
    }

    #endregion

    #region Rendering

    private void RebuildGeometry(double width, double height)
    {
        _geometryDirty = false;
        var morph = Morph;
        var grid = morph.Grid;
        if (_points.Length < grid.Length)
        {
            _points = new Point[grid.Length];
        }
        for (var s = 0; s < _drawn.Length; s++)
        {
            var series = _drawn[s];
            var index = morph.IndexOf(series.Key);
            if (index < 0 || grid.Length == 0)
            {
                _lines[s] = null;
                _areas[s] = null;
                continue;
            }
            var values = morph.Values(index);
            var presence = Presence(series.Key);
            for (var k = 0; k < grid.Length; k++)
            {
                _points[k] = new Point(grid[k] * width, ValueY(values[k], presence, height));
            }
            var span = _points.AsSpan(0, grid.Length);
            _lines[s] = ChartGeometry.Line(span);
            _areas[s] = series.HasArea ?? series.Order == 0 ? ChartGeometry.Area(span, height) : null;
        }
    }

    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var palette = Palette;
        var data = Data;
        var width = PlotWidth(size.Width);
        var height = PlotHeight;
        if (width <= 0)
        {
            return;
        }
        var plotRect = new Rect(0, 0, width, height);
        var axis = Axis;
        axis.Render(context, palette, Texts, plotRect, Top, ShowGrid, ShowYAxis, width + 10);
        var baseline = height - 0.5;
        context.DrawLine(palette.Pen(palette.BorderStrong, 1), new Point(0, baseline), new Point(width, baseline));

        var scrub = _scrub?.Value ?? 0;
        var at = _cursor?.Value ?? 1;
        if (scrub > 0.001 && !data.IsEmpty)
        {
            var cx = Math.Round(at * width) + 0.5;
            using (context.PushOpacity(scrub))
            {
                context.DrawLine(palette.Pen(palette.BorderStrong, 1), new Point(cx, Top - 4), new Point(cx, height));
            }
        }

        if (!data.IsEmpty)
        {
            if (_geometryDirty)
            {
                RebuildGeometry(width, height);
            }
            var reveal = RevealProgress;
            var clip = new Rect(-8, -16, Math.Max(0, reveal * (width + 16)), height + 32);
            using (context.PushClip(clip))
            using (context.PushOpacity(RevealFade * (_breath?.Opacity ?? 1)))
            {
                var stroke = StrokeWidth;
                for (var s = 0; s < _drawn.Length; s++)
                {
                    var series = _drawn[s];
                    var presence = Presence(series.Key);
                    if (presence <= 0.001 || _lines[s] is not { } line)
                    {
                        continue;
                    }
                    var color = SeriesColor(series);
                    using (context.PushOpacity(Math.Clamp(presence, 0, 1)))
                    {
                        if (_areas[s] is { } area)
                        {
                            context.DrawGeometry(palette.Brush(Oklch.WithAlpha(color, 0.09 * color.A / 255.0)), null, area);
                        }
                        context.DrawGeometry(null, series.IsDashed
                            ? palette.Pen(color, stroke - 0.25, 1, PenLineCap.Round)
                            : palette.Pen(color, stroke, 0, PenLineCap.Round), line);
                    }
                }
            }

            // Dots ride the crosshair on each visible line.
            if (scrub > 0.001)
            {
                using (context.PushOpacity(scrub))
                {
                    var morph = Morph;
                    var surfacePen = palette.Pen(palette.Surface, 2);
                    foreach (var series in _drawn)
                    {
                        var index = morph.IndexOf(series.Key);
                        if (index < 0 || IsHidden(series.Key))
                        {
                            continue;
                        }
                        var y = ValueY(morph.ValueAt(index, at), Presence(series.Key), height);
                        context.DrawEllipse(palette.Brush(SeriesColor(series)), surfacePen, new Point(at * width, y), 4, 4);
                    }
                }
            }
        }

        if (ShowXAxis && !data.IsEmpty)
        {
            if (!ReferenceEquals(_picksData, data) || Math.Abs(_picksWidth - width) > 0.5)
            {
                ChartCategoryAxis.Picks(data, width, 76, _picks);
                _picksData = data;
                _picksWidth = width;
            }
            ChartCategoryAxis.Draw(context, palette, Texts, data, _picks, 0, width, height + 8);
        }

        if (data.IsEmpty || VisibleSeriesCount() == 0)
        {
            if (IsLoading && data.IsEmpty)
            {
                DrawSkeleton(context, palette, width, height);
            }
            else
            {
                var message = Texts.Get(palette, data.IsEmpty ? EmptyLabel : "Choose a series to show", palette.SizeSM, palette.TextMuted);
                context.DrawText(message, new Point((width - message.WidthIncludingTrailingWhitespace) / 2, (height - message.Height) / 2));
            }
        }

        _tooltip?.Render(context, palette, Texts);
    }

    private void DrawSkeleton(DrawingContext context, ChartPalette palette, double width, double height)
    {
        if (_skeleton is null || _skeletonVersion != palette.Version)
        {
            _skeletonVersion = palette.Version;
            _skeleton = new ImmutableLinearGradientBrush(
                [new ImmutableGradientStop(0, Colors.Transparent), new ImmutableGradientStop(1, Oklch.WithAlpha(palette.Foreground, 0.05))],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        }
        var rect = new Rect(0, height * 0.3, width, height * 0.45);
        using (context.PushOpacity(_breath?.Opacity ?? 0.32))
        {
            context.DrawRectangle(_skeleton, null, new RoundedRect(rect, 12));
        }
    }

    #endregion
}
