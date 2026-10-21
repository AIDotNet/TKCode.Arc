using System.Text;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a Streamgraph stacks its layers.</summary>
public enum StreamOffset
{
    /// <summary>Minimizes layer slopes for a flowing stream (Byron and Wattenberg), leveled so it never tilts.</summary>
    Wiggle,

    /// <summary>Centers the stack around a flat middle.</summary>
    Silhouette,

    /// <summary>Stacks from a flat zero baseline in order.</summary>
    Zero
}

/// <summary>
/// Arc's streamgraph: layers stacked on a wiggle baseline, flowing over time. Hovering a layer isolates it and
/// reads every layer at that moment; switching ranges blends every layer from the shape on screen on a shared
/// 161-point grid, a hidden layer thins to nothing while the others close over it, and the first time the chart
/// is seen the whole stream swells out of a hairline along its center.
/// <para>
/// Motion is code-driven (a per-sample spring array and <see cref="MotionValue{T}"/>) because every layer edge
/// is recomputed from the stack each frame inside one self-drawn plot.
/// </para>
/// </summary>
/// <example><code>
/// &lt;arc:Streamgraph Label="Tickets by topic" ItemsSource="{Binding Weeks}" XBinding="{CompiledBinding Week, DataType=local:Day}"&gt;
///     &lt;arc:ChartSeries Name="Billing" ValueBinding="{CompiledBinding Billing, DataType=local:Day}" /&gt;
///     &lt;arc:ChartSeries Name="Login" ValueBinding="{CompiledBinding Login, DataType=local:Day}" /&gt;
/// &lt;/arc:Streamgraph&gt;
/// </code></example>
public class Streamgraph : ChartBase
{
    private const int Samples = 161;
    private const double Pad = 10;
    private const double TipGap = 14;
    private static readonly SpringSpec Settle = SpringSpec.FromVisual(0.4, 0);
    private static readonly SpringSpec Swell = SpringSpec.FromVisual(0.9, 0);

    /// <summary>How the layers stack.</summary>
    public static readonly StyledProperty<StreamOffset> OffsetProperty =
        AvaloniaProperty.Register<Streamgraph, StreamOffset>(nameof(Offset));

    /// <summary>Names each layer inside its widest stretch when it is thick enough to hold the text.</summary>
    public static readonly StyledProperty<bool> ShowDirectLabelsProperty =
        AvaloniaProperty.Register<Streamgraph, bool>(nameof(ShowDirectLabels), true);

    public static new readonly StyledProperty<bool?> ShowLegendProperty = ChartBase.ShowLegendProperty.AddOwner<Streamgraph>();
    /// <summary>Shows the category (time) axis labels under the plot.</summary>
    public static new readonly StyledProperty<bool> ShowXAxisProperty = ChartBase.ShowXAxisProperty.AddOwner<Streamgraph>();
    /// <summary>Keys of the series hidden from the plot; the legend toggles them (two-way).</summary>
    public static new readonly StyledProperty<IReadOnlyList<string>?> HiddenSeriesProperty = ChartBase.HiddenSeriesProperty.AddOwner<Streamgraph>();

    /// <inheritdoc cref="OffsetProperty"/>
    public StreamOffset Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <inheritdoc cref="ShowDirectLabelsProperty"/>
    public bool ShowDirectLabels { get => GetValue(ShowDirectLabelsProperty); set => SetValue(ShowDirectLabelsProperty, value); }

    /// <summary>Layer toggles above the plot (shown by default when there is more than one layer).</summary>
    public bool? ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowXAxisProperty"/>
    public bool ShowXAxis { get => GetValue(ShowXAxisProperty); set => SetValue(ShowXAxisProperty, value); }

    /// <inheritdoc cref="ChartBase.HiddenSeriesProperty"/>
    public IReadOnlyList<string>? HiddenSeries { get => GetValue(HiddenSeriesProperty); set => SetValue(HiddenSeriesProperty, value); }

    /// <summary>Raised when the legend shows or hides a series.</summary>
    /// <inheritdoc cref="ChartBase.HiddenSeriesChangedEvent"/>
    public event EventHandler<ChartHiddenSeriesChangedEventArgs>? HiddenSeriesChanged
    {
        add => AddHandler(HiddenSeriesChangedEvent, value);
        remove => RemoveHandler(HiddenSeriesChangedEvent, value);
    }

    /// <summary>The isolated layer (pointer, keyboard or legend preview), or null.</summary>
    public string? ActiveSeriesKey => ActiveKeyOf(_hoverKey ?? _legendKey);

    private ChartSpringArray? _shapes;
    private readonly Dictionary<string, MotionValue<double>> _presence = new();
    private MotionValue<double>? _grow;
    private MotionValue<double>? _crosshair;
    private MotionValue<double>? _shown;
    private MotionValue<double>? _isolate;
    private ChartTooltip? _tooltip;
    private string[] _keys = [];
    private ChartSeriesData[] _layers = [];
    private int[] _stack = [];
    private double[] _scaled = [];
    private double[] _lower = [];
    private double[] _upper = [];
    private double[] _baseline = new double[Samples];
    private double[] _totals = new double[Samples];
    private double[] _centers = new double[Samples];
    private double _lo;
    private double _span = 1;
    private Point[] _top = new Point[Samples];
    private Point[] _bottom = new Point[Samples];
    private StreamGeometry?[] _geometry = [];
    private bool _frameDirty = true;
    private double _frameWidth;
    private readonly Dictionary<string, int> _spots = new();
    private readonly Dictionary<string, (double X, double Y, double Opacity)> _labelPlaces = new();
    private string? _hoverKey;
    private double? _hoverY;
    private string? _legendKey;
    private string? _lastActive;
    private bool _wasActive;
    private readonly List<int> _picks = new();
    private double _picksWidth = -1;
    private ChartData? _picksData;
    private readonly Dictionary<string, Geometry> _halos = new();
    private int _haloVersion = -1;

    static Streamgraph()
    {
        PlotHeightProperty.OverrideDefaultValue<Streamgraph>(260);
        AffectsPlot<Streamgraph>(ShowDirectLabelsProperty);
    }

    private protected override bool UsesSeriesLegend => true;

    private protected override ChartSwatchKind LegendSwatch => ChartSwatchKind.Square;

    private protected override double RevealThreshold => 0.3;

    internal override bool LegendHoverWash => false;

    internal override Color LegendSeriesColor(int order) => Palette.SeriesColor(order, ChartNeutralRamp.Stream);

    private ChartSpringArray Shapes
    {
        get
        {
            if (_shapes is null)
            {
                _shapes = new ChartSpringArray(this);
                _shapes.Changed += OnShapeFrame;
            }
            return _shapes;
        }
    }

    private MotionValue<double> Grow => _grow ??= CreateValue(0, OnShapeFrame);

    private MotionValue<double> Crosshair => _crosshair ??= CreateValue(0, InvalidatePlots);

    private MotionValue<double> Shown => _shown ??= CreateValue(0, InvalidatePlots);

    private MotionValue<double> Isolate => _isolate ??= CreateValue(0, InvalidatePlots);

    private ChartTooltip Tip => _tooltip ??= new ChartTooltip(this, InvalidatePlots);

    private MotionValue<double> CreateValue(double initial, Action changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => changed();
        return value;
    }

    private void OnShapeFrame()
    {
        _frameDirty = true;
        InvalidatePlots();
    }

    #region Data

    private protected override void OnDataChanged(ChartData previous, ChartData current)
    {
        if (!previous.IsEmpty && !current.SameValues(previous))
        {
            ClearReading();
        }
        var count = current.Series.Length;
        var keys = new string[count];
        var targets = new double[count * Samples];
        for (var s = 0; s < count; s++)
        {
            keys[s] = current.Series[s].Key;
            ChartCurves.Resample(current.Series[s].Values, Samples).CopyTo(targets, s * Samples);
            if (!_presence.ContainsKey(keys[s]))
            {
                _presence[keys[s]] = CreateValue(IsHidden(keys[s]) ? 0 : 1, OnShapeFrame);
            }
        }
        // New data blends every layer from the shape on screen; a layer that arrives grows out of nothing.
        var oldKeys = _keys;
        var animate = IsMotionFull && oldKeys.Length > 0;
        Shapes.Reshape(count * Samples, (oldValues, oldVelocities, values, velocities) =>
        {
            for (var s = 0; s < count; s++)
            {
                var from = Array.IndexOf(oldKeys, keys[s]);
                for (var j = 0; j < Samples; j++)
                {
                    values[s * Samples + j] = from >= 0 && animate ? oldValues[from * Samples + j] : targets[s * Samples + j];
                    velocities[s * Samples + j] = from >= 0 && animate ? oldVelocities[from * Samples + j] : 0;
                }
            }
        });
        if (animate)
        {
            Shapes.AnimateTo(targets, Settle);
        }
        else
        {
            Shapes.Jump(targets);
        }
        _keys = keys;
        _layers = current.Series;
        _scaled = new double[count * Samples];
        _lower = new double[count * Samples];
        _upper = new double[count * Samples];
        _geometry = new StreamGeometry?[count];
        _stack = StackOrder(count, Offset);
        _frameDirty = true;
        RebuildTooltip();
    }

    /// <summary>The first layer sits in the middle of the stream and the rest alternate above and below it; a flat baseline stacks them in order.</summary>
    private static int[] StackOrder(int count, StreamOffset mode)
    {
        var order = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            if (mode == StreamOffset.Zero || i == 0 || i % 2 == 1)
            {
                order.Add(i);
            }
            else
            {
                order.Insert(0, i);
            }
        }
        return order.ToArray();
    }

    private protected override void OnRevealed()
    {
        if (MotionLevel == ArcMotionLevel.Full)
        {
            _ = Grow.AnimateTo(1, MotionSpec.Spring(Swell));
        }
        else
        {
            Grow.Jump(1);
        }
    }

    private protected override void OnHiddenSeriesChanged()
    {
        foreach (var key in _keys)
        {
            if (_presence.TryGetValue(key, out var presence))
            {
                _ = presence.AnimateTo(IsHidden(key) ? 0 : 1, MotionSpec.Spring(Settle));
            }
        }
        RebuildTooltip();
        RefreshLegend();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowDirectLabelsProperty)
        {
            _frameDirty = true;
        }
        if (change.Property == OffsetProperty)
        {
            _stack = StackOrder(_keys.Length, Offset);
            _frameDirty = true;
            RebuildTooltip();
            InvalidatePlots();
        }
    }

    #endregion

    #region Frame

    private double Presence(string key) => _presence.TryGetValue(key, out var value) ? value.Value : 1;

    /// <summary>Recomputes the stack (baseline, lower and upper edges) from the on-screen shapes; arrays are reused.</summary>
    private void ComputeFrame(double width, double height)
    {
        _frameDirty = false;
        _frameWidth = width;
        var count = _keys.Length;
        var shapes = Shapes.Values;
        if (shapes.Length < count * Samples)
        {
            return;
        }
        for (var s = 0; s < count; s++)
        {
            var p = Presence(_keys[s]);
            for (var j = 0; j < Samples; j++)
            {
                _scaled[s * Samples + j] = Math.Max(0, shapes[s * Samples + j]) * p;
            }
        }
        Array.Clear(_totals);
        for (var j = 0; j < Samples; j++)
        {
            for (var s = 0; s < count; s++)
            {
                _totals[j] += _scaled[s * Samples + j];
            }
        }
        Baseline(count);
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        for (var j = 0; j < Samples; j++)
        {
            var running = _baseline[j];
            lo = Math.Min(lo, running);
            foreach (var s in _stack)
            {
                _lower[s * Samples + j] = running;
                running += _scaled[s * Samples + j];
                _upper[s * Samples + j] = running;
            }
            hi = Math.Max(hi, running);
            _centers[j] = (_baseline[j] + running) / 2;
        }
        if (double.IsInfinity(lo) || hi - lo < 1e-9)
        {
            lo = -1;
            hi = 1;
        }
        _lo = lo;
        _span = hi - lo;
        var grow = Grow.Value;
        for (var s = 0; s < count; s++)
        {
            for (var j = 0; j < Samples; j++)
            {
                var x = (double)j / (Samples - 1) * width;
                _top[j] = new Point(x, YAt(_upper[s * Samples + j], j, height, grow));
                _bottom[j] = new Point(x, YAt(_lower[s * Samples + j], j, height, grow));
            }
            _geometry[s] = ChartGeometry.Band(_top, _bottom);
        }
        PlaceLabels(width, height, grow);
    }

    private double ToY(double value, double height) => Pad + (1 - (value - _lo) / _span) * (height - Pad * 2);

    private double YAt(double value, int j, double height, double grow) => ToY(_centers[j] + (value - _centers[j]) * grow, height);

    /// <summary>The baseline under the stack. Wiggle follows Byron and Wattenberg, then removes its linear drift so the stream stays level.</summary>
    private void Baseline(int count)
    {
        Array.Clear(_baseline);
        var mode = Offset;
        if (mode == StreamOffset.Zero || count == 0)
        {
            return;
        }
        if (mode == StreamOffset.Silhouette)
        {
            for (var j = 0; j < Samples; j++)
            {
                _baseline[j] = -_totals[j] / 2;
            }
            return;
        }
        for (var j = 1; j < Samples; j++)
        {
            double s1 = 0, s2 = 0, below = 0;
            foreach (var s in _stack)
            {
                var value = _scaled[s * Samples + j];
                var change = value - _scaled[s * Samples + j - 1];
                s1 += value;
                s2 += (below + change / 2) * value;
                below += change;
            }
            _baseline[j] = _baseline[j - 1] - (s1 != 0 ? s2 / s1 : 0);
        }
        // Least squares line through the centre of the stream, subtracted so it never tilts off the plot.
        double mx = (Samples - 1) / 2.0, my = 0;
        for (var j = 0; j < Samples; j++)
        {
            my += _baseline[j] + _totals[j] / 2;
        }
        my /= Samples;
        double num = 0, den = 0;
        for (var j = 0; j < Samples; j++)
        {
            var center = _baseline[j] + _totals[j] / 2;
            num += (j - mx) * (center - my);
            den += (j - mx) * (j - mx);
        }
        var slope = den != 0 ? num / den : 0;
        for (var j = 0; j < Samples; j++)
        {
            _baseline[j] -= my + slope * (j - mx);
        }
    }

    /// <summary>Each label sits where its layer is thickest and holds its spot until another stretch is clearly thicker.</summary>
    private void PlaceLabels(double width, double height, double grow)
    {
        if (!ShowDirectLabels)
        {
            return;
        }
        EnsurePalette();
        for (var s = 0; s < _keys.Length; s++)
        {
            var key = _keys[s];
            var best = 4;
            for (var j = 4; j < Samples - 4; j++)
            {
                if (Thickness(s, j) > Thickness(s, best))
                {
                    best = j;
                }
            }
            var j0 = _spots.TryGetValue(key, out var held) && Thickness(s, held) >= Thickness(s, best) * 0.8 ? held : best;
            _spots[key] = j0;
            var thickness = Thickness(s, j0) / _span * (height - Pad * 2) * grow;
            var text = Texts.Get(Palette, _layers[s].Name, Palette.SizeXS, Palette.Foreground, medium: true);
            var room = text.WidthIncludingTrailingWhitespace;
            var x = Math.Clamp((double)j0 / (Samples - 1) * width, room / 2 + 6, Math.Max(room / 2 + 6, width - room / 2 - 6));
            var y = YAt((_upper[s * Samples + j0] + _lower[s * Samples + j0]) / 2, j0, height, grow);
            _labelPlaces[key] = (x, y, thickness >= 19 && room + 16 < width ? 1 : 0);
        }
    }

    private double Thickness(int s, int j) => _upper[s * Samples + j] - _lower[s * Samples + j];

    #endregion

    #region Reading

    private string? ActiveKeyOf(string? wanted)
    {
        if (wanted is null || IsHidden(wanted))
        {
            return null;
        }
        return Array.IndexOf(_keys, wanted) >= 0 ? wanted : null;
    }

    private void ClearReading()
    {
        _hoverKey = null;
        _hoverY = null;
        SetActive(null);
        UpdateIsolation();
    }

    internal override void OnLegendPreview(string key, bool on)
    {
        _legendKey = on ? key : _legendKey == key ? null : _legendKey;
        UpdateIsolation();
    }

    internal override double LegendEmphasis(string key)
    {
        var active = ActiveSeriesKey;
        return active is null ? 0 : active == key ? 1 : -1;
    }

    private void UpdateIsolation()
    {
        var active = ActiveSeriesKey;
        _ = Isolate.AnimateTo(active is null ? 0 : 1, Fade(ArcMotionPresets.Fast));
        if (active != _lastActive)
        {
            _lastActive = active;
            RebuildTooltip();
            RaiseActiveIndexChanged();
            RefreshLegend();
            Announce();
        }
        InvalidatePlots();
    }

    private protected override string? EventSeriesKey => ActiveSeriesKey;

    private MotionSpec Fade(TimeSpan duration) => MotionLevel == ArcMotionLevel.Off
        ? MotionSpec.Instant
        : MotionSpec.Tween(duration, ArcEasings.Standard);

    private protected override void OnActiveIndexChanged(int? previous, int? current)
    {
        var level = MotionLevel;
        if (current is not null)
        {
            _ = Shown.AnimateTo(1, Fade(ArcMotionPresets.Instant));
            Tip.SetVisible(true);
        }
        else
        {
            _wasActive = false;
            _ = Shown.AnimateTo(0, Fade(ArcMotionPresets.Instant));
            Tip.SetVisible(false);
        }
        RebuildTooltip();
        Announce();
        if (level != ArcMotionLevel.Full)
        {
            InvalidatePlots();
        }
    }

    private void Announce()
    {
        var data = Data;
        if (ActiveIndex is not { } index || index >= data.Count)
        {
            Announcement = string.Empty;
            return;
        }
        var builder = new StringBuilder(data.Labels[index]).Append(". ");
        if (ActiveSeriesKey is { } key && data.FindSeries(key) is { } series)
        {
            builder.Append(series.Name).Append(' ').Append(FormatWithUnit(series.Values[index], index, series)).Append(", ");
        }
        double total = 0;
        foreach (var visible in VisibleSeries())
        {
            total += Math.Max(0, visible.Values[index]);
        }
        builder.Append(VisibleSeriesCount()).Append(" layers, ").Append(FormatWithUnit(total)).Append(" in total");
        Announcement = builder.ToString();
    }

    private void RebuildTooltip()
    {
        var data = Data;
        if (data.IsEmpty)
        {
            return;
        }
        var index = ActiveIndex is { } i && i < data.Count ? i : data.Count - 1;
        var tip = Tip;
        tip.Begin(156, 256);
        tip.AddTitle(data.Labels[index]);
        var active = ActiveSeriesKey;
        for (var k = _stack.Length - 1; k >= 0; k--)
        {
            var s = _stack[k];
            if (s >= data.Series.Length)
            {
                continue;
            }
            var series = data.Series[s];
            if (IsHidden(series.Key))
            {
                continue;
            }
            tip.AddRow(SeriesColor(series), false, series.Name, FormatWithUnit(series.Values[index], index, series),
                active is null ? 0 : active == series.Key ? 1 : -1);
        }
        PlaceTooltip();
    }

    /// <summary>The crosshair glides between points; the tooltip follows on its own spring and never leaves the plot.</summary>
    private void PlaceTooltip()
    {
        var data = Data;
        if (_tooltip is null || MainPlot is not { } plot || plot.Bounds.Width <= 0 || ActiveIndex is not { } index || index >= data.Count)
        {
            return;
        }
        EnsurePalette();
        var width = plot.Bounds.Width;
        var height = PlotHeight;
        var last = data.Count - 1;
        var x = last > 0 ? (double)index / last * width : width / 2;
        var y = _hoverY;
        if (y is null && ActiveSeriesKey is { } key)
        {
            if (_frameDirty)
            {
                ComputeFrame(width, height);
            }
            var s = Array.IndexOf(_keys, key);
            var j = (int)Math.Round(x / width * (Samples - 1));
            if (s >= 0)
            {
                y = YAt((_upper[s * Samples + j] + _lower[s * Samples + j]) / 2, j, height, Grow.Value);
            }
        }
        var size = _tooltip.Measure(Palette, Texts, width);
        var target = ChartTooltip.Beside(size, x, y ?? height / 2, new Rect(0, 0, width, height), TipGap);
        var instant = !_wasActive || MotionLevel != ArcMotionLevel.Full;
        if (instant)
        {
            Crosshair.Jump(x);
        }
        else
        {
            _ = Crosshair.AnimateTo(x, MotionSpec.Spring(ChartTooltip.Follow));
        }
        _tooltip.MoveTo(target, instant);
        _wasActive = true;
    }

    /// <summary>The layer under the pointer, found in the frame that is on screen.</summary>
    private void Read(ChartPlot plot, Point point)
    {
        var data = Data;
        if (data.IsEmpty || plot.Bounds.Width <= 0)
        {
            return;
        }
        var width = plot.Bounds.Width;
        var height = PlotHeight;
        if (_frameDirty)
        {
            ComputeFrame(width, height);
        }
        var share = Math.Clamp(point.X / width, 0, 1);
        var last = data.Count - 1;
        var index = last > 0 ? (int)Math.Round(share * last) : 0;
        var j = (int)Math.Round(share * (Samples - 1));
        string? key = null;
        var grow = Grow.Value;
        for (var s = 0; s < _keys.Length; s++)
        {
            if (IsHidden(_keys[s]))
            {
                continue;
            }
            var top = YAt(_upper[s * Samples + j], j, height, grow);
            var bottom = YAt(_lower[s * Samples + j], j, height, grow);
            if (point.Y >= top - 1 && point.Y <= bottom + 1)
            {
                key = _keys[s];
            }
        }
        _hoverKey = key;
        _hoverY = point.Y;
        if (ActiveIndex != index)
        {
            SetActive(index);
        }
        UpdateIsolation();
        PlaceTooltip();
    }

    #endregion

    #region Input

    internal override double MeasurePlotHeight(ChartPlot plot, double width) => PlotHeight + (ShowXAxis ? AxisRowHeight : 0);

    internal override void OnPlotResized(ChartPlot plot, Size size)
    {
        _frameDirty = true;
        PlaceTooltip();
    }

    internal override void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse || e.GetCurrentPoint(plot).Properties.IsLeftButtonPressed)
        {
            var point = e.GetPosition(plot);
            if (e.Pointer.Type == PointerType.Mouse && point.Y > PlotHeight)
            {
                ClearReading();
                return;
            }
            Read(plot, point);
        }
    }

    internal override void OnPlotPointerPressed(ChartPlot plot, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(plot);
        }
        Read(plot, e.GetPosition(plot));
    }

    internal override void OnPlotPointerReleased(ChartPlot plot, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(null);
            ClearReading();
        }
    }

    internal override void OnPlotPointerExited(ChartPlot plot, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            ClearReading();
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
                ClearReading();
            }
            return;
        }
        var last = data.Count - 1;
        var visible = new List<string>();
        foreach (var series in VisibleSeries())
        {
            visible.Add(series.Key);
        }
        var active = ActiveSeriesKey;
        var at = active is null ? -1 : visible.IndexOf(active);
        var from = ActiveIndex ?? last;
        int? index = null;
        var key = active;
        switch (e.Key)
        {
            case Key.Left:
                index = Math.Clamp(from - 1, 0, last);
                break;
            case Key.Right:
                index = Math.Clamp(ActiveIndex is null ? last : from + 1, 0, last);
                break;
            case Key.PageDown:
                index = Math.Clamp(from - Math.Max(1, (int)Math.Round(data.Count / 6.0)), 0, last);
                break;
            case Key.PageUp:
                index = Math.Clamp(from + Math.Max(1, (int)Math.Round(data.Count / 6.0)), 0, last);
                break;
            case Key.Home:
                index = 0;
                break;
            case Key.End:
                index = last;
                break;
            case Key.Up when visible.Count > 0:
                index = from;
                key = visible[at < 0 ? visible.Count - 1 : Math.Min(visible.Count - 1, at + 1)];
                break;
            case Key.Down when visible.Count > 0:
                index = from;
                key = visible[at < 0 ? 0 : Math.Max(0, at - 1)];
                break;
        }
        if (index is not { } next)
        {
            return;
        }
        e.Handled = true;
        _hoverKey = key;
        _hoverY = null;
        if (ActiveIndex != next)
        {
            SetActive(next);
        }
        UpdateIsolation();
        PlaceTooltip();
    }

    internal override void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
        if (!focused)
        {
            ClearReading();
        }
        else if (keyboard && !Data.IsEmpty && ActiveIndex is null)
        {
            _hoverKey = null;
            _hoverY = null;
            SetActive(Data.Count - 1);
            PlaceTooltip();
        }
    }

    internal override AutomationControlType GetPlotControlType(ChartPlot plot) => AutomationControlType.Group;

    internal override string GetPlotAutomationName(ChartPlot plot)
        => $"{Label}. Use left and right arrows to move through time, up and down to change layer.";

    internal override string GetPlotValueText(ChartPlot plot) => string.IsNullOrEmpty(Announcement) ? Summary : Announcement;

    #endregion

    #region Rendering

    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var palette = Palette;
        var data = Data;
        var width = size.Width;
        var height = PlotHeight;
        if (width <= 0)
        {
            return;
        }
        if (!data.IsEmpty)
        {
            if (_frameDirty || Math.Abs(_frameWidth - width) > 0.5)
            {
                ComputeFrame(width, height);
            }
            var active = ActiveSeriesKey;
            var isolate = Isolate.Value;
            var gap = palette.Pen(palette.Surface, 2);
            using (context.PushOpacity(RevealFade))
            {
                for (var s = 0; s < _keys.Length; s++)
                {
                    if (_geometry[s] is not { } geometry)
                    {
                        continue;
                    }
                    var series = _layers[s];
                    var color = SeriesColor(series);
                    var isActive = active == series.Key;
                    var fill = isActive ? color : ChartPalette.Mix(color, 0.82, palette.Surface);
                    using (context.PushOpacity(isActive ? 1 : 1 - 0.66 * isolate))
                    {
                        context.DrawGeometry(palette.Brush(fill), gap, geometry);
                    }
                }
            }
            var shown = Shown.Value;
            if (shown > 0.001)
            {
                var x = Math.Round(Crosshair.Value) + 0.5;
                using (context.PushOpacity(shown))
                {
                    context.DrawLine(palette.Pen(Oklch.WithAlpha(palette.Foreground, 0.5), 1), new Point(x, 0), new Point(x, height));
                }
            }
            if (ShowDirectLabels)
            {
                DrawLabels(context, palette, active, isolate);
            }
            if (ShowXAxis)
            {
                if (!ReferenceEquals(_picksData, data) || Math.Abs(_picksWidth - width) > 0.5)
                {
                    ChartCategoryAxis.Picks(data, width, 72, _picks);
                    _picksData = data;
                    _picksWidth = width;
                }
                ChartCategoryAxis.Draw(context, palette, Texts, data, _picks, 0, width, height + 8);
            }
        }
        if (data.IsEmpty || VisibleSeriesCount() == 0)
        {
            var message = Texts.Get(palette, data.IsEmpty ? EmptyLabel : "Choose a layer to show", palette.SizeSM, palette.TextMuted);
            context.DrawText(message, new Point((width - message.WidthIncludingTrailingWhitespace) / 2, (height - message.Height) / 2));
        }
        _tooltip?.Render(context, palette, Texts);
    }

    /// <summary>Direct labels with a surface halo, so they read over any layer.</summary>
    private void DrawLabels(DrawingContext context, ChartPalette palette, string? active, double isolate)
    {
        if (_haloVersion != palette.Version)
        {
            _halos.Clear();
            _haloVersion = palette.Version;
        }
        var halo = palette.Pen(Oklch.WithAlpha(palette.Surface, 0.85), 3);
        foreach (var series in _layers)
        {
            if (IsHidden(series.Key) || !_labelPlaces.TryGetValue(series.Key, out var place) || place.Opacity <= 0)
            {
                continue;
            }
            var dim = active is not null && active != series.Key ? isolate : 0;
            var opacity = place.Opacity * (1 - dim) * Presence(series.Key);
            if (opacity <= 0.01)
            {
                continue;
            }
            var text = Texts.Get(palette, series.Name, palette.SizeXS, palette.Foreground, medium: true);
            if (!_halos.TryGetValue(series.Name, out var outline))
            {
                outline = text.BuildGeometry(default) ?? new StreamGeometry();
                _halos[series.Name] = outline;
            }
            var origin = new Point(Math.Round(place.X - text.WidthIncludingTrailingWhitespace / 2), Math.Round(place.Y - text.Height / 2));
            using (context.PushOpacity(opacity))
            using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                context.DrawGeometry(null, halo, outline);
                context.DrawText(text, default);
            }
        }
    }

    #endregion
}
