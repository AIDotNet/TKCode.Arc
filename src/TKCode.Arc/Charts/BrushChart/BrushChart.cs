using System.Globalization;
using System.Text;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Arc's brush chart: a long, dense time series with an overview strip. Drag the window to pan, drag a handle
/// to resize, drag across empty track to draw a new window, tap to recenter, double-click to reset. When the
/// view is zoomed far out a seven-point average and its low-to-high band replace the raw line, crossfading by
/// density. Events are marked on both charts and labelled where they fit. The value axis springs to the tallest
/// point in view; the line draws in from the left the first time the chart is seen.
/// <para>
/// Dragging is followed 1:1 by a local tracker (<see cref="ChartDragTracker"/>); presets, resets and bound
/// <see cref="Range"/> changes glide the window on a smooth spring (<see cref="MotionValue{T}"/>), because the
/// window is a coordinate pair inside the self-drawn overview.
/// </para>
/// </summary>
/// <example><code>&lt;arc:BrushChart Label="Daily active users" ItemsSource="{Binding Days}" XBinding="{CompiledBinding Date, DataType=local:Day}" YBinding="{CompiledBinding Users, DataType=local:Day}" /&gt;</code></example>
[TemplatePart(OverviewPart, typeof(Control))]
public class BrushChart : ChartBase
{
    public const string OverviewPart = "PART_Overview";

    internal const double Top = 28;
    private const double TipGap = 14;
    private static readonly SpringSpec Settle = SpringSpec.FromVisual(0.4, 0);
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Events drawn as markers on both charts.</summary>
    public static readonly StyledProperty<IEnumerable<ChartAnnotation>?> AnnotationsProperty =
        AvaloniaProperty.Register<BrushChart, IEnumerable<ChartAnnotation>?>(nameof(Annotations));

    /// <summary>The visible window. A new value glides the window there; drags and keys write it back. Two-way.</summary>
    public static readonly StyledProperty<ChartTimeRange?> RangeProperty =
        AvaloniaProperty.Register<BrushChart, ChartTimeRange?>(nameof(Range), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The smallest window (Arc: seven days).</summary>
    public static readonly StyledProperty<TimeSpan> MinSpanProperty =
        AvaloniaProperty.Register<BrushChart, TimeSpan>(nameof(MinSpan), TimeSpan.FromDays(7));

    /// <summary>Overview strip height in pixels.</summary>
    public static readonly StyledProperty<double> OverviewHeightProperty =
        AvaloniaProperty.Register<BrushChart, double>(nameof(OverviewHeight), 52);

    /// <summary>Formats a date in the tooltip and announcements. Defaults to "Tue, Sep 15, 2026".</summary>
    public static readonly StyledProperty<Func<DateTimeOffset, string>?> DateFormatterProperty =
        AvaloniaProperty.Register<BrushChart, Func<DateTimeOffset, string>?>(nameof(DateFormatter));

    /// <summary>Formats value-axis ticks. Defaults to a compact number, such as 1.2K.</summary>
    public static new readonly StyledProperty<Func<double, string>?> TickFormatterProperty = ChartBase.TickFormatterProperty.AddOwner<BrushChart>();
    /// <summary>Draws the gridlines behind the plot (default true).</summary>
    public static new readonly StyledProperty<bool> ShowGridProperty = ChartBase.ShowGridProperty.AddOwner<BrushChart>();

    /// <summary>Raised while the window is dragged, resized, stepped or reset.</summary>
    public static readonly RoutedEvent<ChartTimeRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<BrushChart, ChartTimeRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="AnnotationsProperty"/>
    public IEnumerable<ChartAnnotation>? Annotations { get => GetValue(AnnotationsProperty); set => SetValue(AnnotationsProperty, value); }

    /// <inheritdoc cref="RangeProperty"/>
    public ChartTimeRange? Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }

    /// <inheritdoc cref="MinSpanProperty"/>
    public TimeSpan MinSpan { get => GetValue(MinSpanProperty); set => SetValue(MinSpanProperty, value); }

    /// <inheritdoc cref="OverviewHeightProperty"/>
    public double OverviewHeight { get => GetValue(OverviewHeightProperty); set => SetValue(OverviewHeightProperty, value); }

    /// <inheritdoc cref="DateFormatterProperty"/>
    public Func<DateTimeOffset, string>? DateFormatter { get => GetValue(DateFormatterProperty); set => SetValue(DateFormatterProperty, value); }

    /// <inheritdoc cref="ChartBase.TickFormatterProperty"/>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    /// <inheritdoc cref="ChartBase.ShowGridProperty"/>
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    /// <inheritdoc cref="RangeChangedEvent"/>
    public event EventHandler<ChartTimeRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    internal readonly record struct Smoothed(double Value, double Low, double High);

    internal readonly record struct Event(double Time, string Label, string? Description);

    private double[] _times = [];
    private double[] _values = [];
    private Smoothed[] _smooth = [];
    private Event[] _events = [];
    private MotionValue<Vector>? _window;
    private MotionValue<double>? _top;
    private MotionValue<double>? _shown;
    private ChartTooltip? _tooltip;
    private BrushChartOverview? _overview;
    private ChartTickSet _ticks = ChartTickSet.Default;
    private readonly Dictionary<double, string> _tickLabels = new();

    private string TickLabel(double tick)
    {
        if (!_tickLabels.TryGetValue(tick, out var label))
        {
            if (_tickLabels.Count > 64)
            {
                _tickLabels.Clear();
            }
            label = FormatTick(tick);
            _tickLabels[tick] = label;
        }
        return label;
    }

    private protected override void OnFormattingChanged() => _tickLabels.Clear();
    private int? _hoverEvent;
    private bool _wasOn;
    private bool _syncingRange;
    private bool _initialized;
    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private StreamGeometry? _trend;
    private StreamGeometry? _band;
    private (double Start, double End, double Width, double Top) _pathKey;
    private readonly List<Point> _buffer = new();
    private readonly List<Point> _buffer2 = new();
    private readonly List<(double Time, string Label)> _timeTicks = new();
    private readonly Dictionary<double, bool> _labelled = new();
    private string _lastAnnounced = string.Empty;

    private readonly Func<int, double> _readValue;
    private readonly Func<int, double> _readTrend;
    private readonly Func<int, double> _readHigh;
    private readonly Func<int, double> _readLow;
    private (double, double, double) _timeTicksKey;

    public BrushChart()
    {
        _readValue = i => _values[i];
        _readTrend = i => _smooth[i].Value;
        _readHigh = i => _smooth[i].High;
        _readLow = i => _smooth[i].Low;
    }

    static BrushChart()
    {
        PlotHeightProperty.OverrideDefaultValue<BrushChart>(240);
        EmptyLabelProperty.OverrideDefaultValue<BrushChart>("No data yet");
        GutterWidthProperty.OverrideDefaultValue<BrushChart>(44);
        AxisRowHeightProperty.OverrideDefaultValue<BrushChart>(28);
    }

    private protected override double RevealThreshold => 0.3;


    internal IReadOnlyList<double> Times => _times;

    internal Smoothed[] Smooth => _smooth;

    internal Event[] Events => _events;

    internal double First => _times.Length > 0 ? _times[0] : 0;

    internal double Last => _times.Length > 0 ? _times[^1] : 1;

    internal bool IsEmptySeries => _times.Length < 2;

    /// <summary>The window on screen (epoch milliseconds).</summary>
    internal (double Start, double End) View
    {
        get
        {
            var window = Window.Value;
            return (window.X, window.Y);
        }
    }

    private MotionValue<Vector> Window
    {
        get
        {
            if (_window is null)
            {
                _window = new MotionValue<Vector>(this, new Vector(0, 1), MotionVectors.Vector);
                _window.Changed += _ => OnWindowFrame();
            }
            return _window;
        }
    }

    private MotionValue<double> TopValue => _top ??= CreateValue(100);

    private MotionValue<double> Shown => _shown ??= CreateValue(0);

    private ChartTooltip Tip => _tooltip ??= new ChartTooltip(this, InvalidatePlots) { LineGap = 2 };

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidatePlots();
        return value;
    }

    private protected override void OnTemplateApplied(INameScope scope)
    {
        _overview = scope.Find<BrushChartOverview>(OverviewPart);
        if (_overview is not null)
        {
            _overview.Owner = this;
        }
    }

    #region Data

    private protected override void OnDataChanged(ChartData previous, ChartData current)
    {
        var values = current.Series.Length > 0 ? current.Series[0].Values : [];
        var points = new List<(double Time, double Value)>(current.Count);
        for (var i = 0; i < current.Count; i++)
        {
            if (ChartTime.TryToMs(current.X[i], out var ms))
            {
                points.Add((ms, i < values.Length ? values[i] : 0));
            }
        }
        points.Sort((a, b) => a.Time.CompareTo(b.Time));
        _times = points.Select(p => p.Time).ToArray();
        _values = points.Select(p => p.Value).ToArray();
        _smooth = SmoothSeries(_values);
        RebuildEvents();
        _pathKey = default;
        if (IsEmptySeries)
        {
            InvalidatePlots();
            _overview?.Rebuild();
            return;
        }
        // The window: the bound range, else the whole series; data that no longer covers it pulls it back inside.
        var target = Range is { } range ? Fit(ChartTime.ToMs(range.Start), ChartTime.ToMs(range.End)) : _initialized ? Fit(View.Start, View.End) : (First, Last);
        Window.Jump(new Vector(target.Item1, target.Item2));
        _initialized = true;
        UpdateScale(false);
        _overview?.Rebuild();
        SetActive(null);
    }

    /// <summary>A centred seven-point average and its low-to-high band, the band averaged once more so its edges ease.</summary>
    private static Smoothed[] SmoothSeries(double[] values)
    {
        var count = values.Length;
        var raw = new Smoothed[count];
        for (var i = 0; i < count; i++)
        {
            double sum = 0, low = double.PositiveInfinity, high = double.NegativeInfinity;
            var n = 0;
            for (var at = Math.Max(0, i - 3); at <= Math.Min(count - 1, i + 3); at++)
            {
                sum += values[at];
                low = Math.Min(low, values[at]);
                high = Math.Max(high, values[at]);
                n++;
            }
            raw[i] = new Smoothed(sum / n, low, high);
        }
        var result = new Smoothed[count];
        for (var i = 0; i < count; i++)
        {
            double low = 0, high = 0;
            var n = 0;
            for (var at = Math.Max(0, i - 3); at <= Math.Min(count - 1, i + 3); at++)
            {
                low += raw[at].Low;
                high += raw[at].High;
                n++;
            }
            result[i] = new Smoothed(raw[i].Value, low / n, high / n);
        }
        return result;
    }

    private void RebuildEvents()
    {
        _events = (Annotations ?? Array.Empty<ChartAnnotation>())
                  .Select(a => new Event(ChartTime.ToMs(a.Date), a.Label, a.Description))
                  .OrderBy(e => e.Time)
                  .ToArray();
        _hoverEvent = null;
    }

    /// <summary>Clamps a window into the data and to at least the minimum span.</summary>
    internal (double, double) Fit(double start, double end)
    {
        var whole = Last - First;
        var least = Math.Min(MinSpan.TotalMilliseconds, whole);
        var span = Math.Clamp(end - start, least, Math.Max(least, whole));
        var s = Math.Clamp(start, First, Math.Max(First, Last - span));
        return (s, s + span);
    }

    /// <summary>Sets the window: 1:1 for drags and keys, gliding on a spring for presets, resets and bound changes.</summary>
    internal void Commit(double start, double end, bool smooth)
    {
        if (IsEmptySeries)
        {
            return;
        }
        var (s, e) = Fit(start, end);
        if (smooth && IsMotionFull)
        {
            _ = Window.AnimateTo(new Vector(s, e), MotionSpec.Spring(Settle));
        }
        else
        {
            Window.Jump(new Vector(s, e));
        }
        var range = new ChartTimeRange(ChartTime.FromMs(s), ChartTime.FromMs(e));
        _syncingRange = true;
        SetCurrentValue(RangeProperty, range);
        _syncingRange = false;
        RaiseEvent(new ChartTimeRangeChangedEventArgs(RangeChangedEvent, range));
        RefreshText();
    }

    internal void Reset() => Commit(First, Last, true);

    private void OnWindowFrame()
    {
        UpdateScale(true);
        if (ActiveIndex is { } index && (_times.Length <= index || _times[index] < View.Start - 1 || _times[index] > View.End + 1))
        {
            SetActive(null);
        }
        PlaceTooltip();
        InvalidatePlots();
        _overview?.InvalidateVisual();
        _overview?.InvalidateArrange();
    }

    /// <summary>The value axis springs to the tallest point in view, so zooming into a quiet stretch fills the plot.</summary>
    private void UpdateScale(bool animate)
    {
        var (from, to) = VisibleRange();
        double peak = 0;
        for (var i = from; i < to; i++)
        {
            peak = Math.Max(peak, _values[i]);
        }
        var ticks = ChartTicks.TopFromZero(peak * 1.04);
        if (Math.Abs(ticks.Max - _ticks.Max) < 1e-9 && _top is not null && animate)
        {
            return;
        }
        _ticks = ticks;
        if (animate && IsMotionFull && IsRevealed)
        {
            if (Math.Abs(TopValue.Target - ticks.Max) > 1e-9)
            {
                _ = TopValue.AnimateTo(ticks.Max, MotionSpec.Spring(Settle));
            }
        }
        else
        {
            TopValue.Jump(ticks.Max);
        }
    }

    /// <summary>Indices of the points in view plus one on each side.</summary>
    internal (int From, int To) VisibleRange()
    {
        if (_times.Length == 0)
        {
            return (0, 0);
        }
        var (start, end) = View;
        var from = Math.Max(0, LowerBound(_times, start) - 1);
        var to = Math.Min(_times.Length, LowerBound(_times, end) + 1);
        return (from, to);
    }

    internal static int LowerBound(IReadOnlyList<double> times, double t)
    {
        int low = 0, high = times.Count;
        while (low < high)
        {
            var middle = (low + high) >> 1;
            if (times[middle] < t)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RangeProperty && !_syncingRange && Range is { } range && !IsEmptySeries)
        {
            var (s, e) = Fit(ChartTime.ToMs(range.Start), ChartTime.ToMs(range.End));
            var (cs, ce) = View;
            if (Math.Abs(s - cs) + Math.Abs(e - ce) > 1000)
            {
                if (IsMotionFull && IsLoaded)
                {
                    _ = Window.AnimateTo(new Vector(s, e), MotionSpec.Spring(Settle));
                }
                else
                {
                    Window.Jump(new Vector(s, e));
                }
            }
            RefreshText();
        }
        else if (change.Property == AnnotationsProperty)
        {
            RebuildEvents();
            _overview?.Rebuild();
            InvalidatePlots();
        }
        else if (change.Property == OverviewHeightProperty)
        {
            _overview?.InvalidateMeasure();
        }
        else if (change.Property == MinSpanProperty && !IsEmptySeries)
        {
            Commit(View.Start, View.End, false);
        }
    }

    #endregion

    private protected override void OnThemeInvalidated()
    {
        _overview?.InvalidateVisual();
        if (_overview is not null)
        {
            foreach (var child in _overview.GetVisualChildren())
            {
                if (child is Visual visual)
                {
                    visual.InvalidateVisual();
                }
            }
        }
    }

    #region Formatting and text

    internal string FormatDate(double ms) => DateFormatter is { } formatter
        ? formatter(ChartTime.FromMs(ms))
        : ChartTime.FromMs(ms).UtcDateTime.ToString("ddd, MMM d, yyyy", s_culture);

    internal static string ShortDate(double ms) => ChartTime.FromMs(ms).UtcDateTime.ToString("MMM d, yyyy", s_culture);

    internal string WindowText
    {
        get
        {
            var (start, end) = View;
            return $"{ShortDate(start)} to {ShortDate(end)}";
        }
    }

    private Event? EventNear(double time)
    {
        foreach (var e in _events)
        {
            if (Math.Abs(e.Time - time) < ChartTime.Day / 2)
            {
                return e;
            }
        }
        return null;
    }

    private protected override string BuildSummary() => IsEmptySeries
        ? $"{Label}. {EmptyLabel}."
        : $"{Label}, {ShortDate(First)} to {ShortDate(Last)}. Showing {WindowText}.";

    private protected override string BuildDataTable()
    {
        if (IsEmptySeries)
        {
            return string.Empty;
        }
        var builder = new StringBuilder($"Date\t{Label}\tEvent");
        var (start, end) = View;
        for (var i = LowerBound(_times, start); i < LowerBound(_times, end + 1); i++)
        {
            var e = EventNear(_times[i]);
            builder.Append('\n').Append(ShortDate(_times[i])).Append('\t').Append(FormatWithUnit(_values[i], i)).Append('\t')
                   .Append(e is { } ev ? ev.Description is { } d ? $"{ev.Label}. {d}" : ev.Label : string.Empty);
        }
        return builder.ToString();
    }

    #endregion

    #region Readout

    /// <summary>How much of the raw line shows: all of it past about four pixels a point, none below about three.</summary>
    private double Detail(double width)
    {
        if (IsEmptySeries)
        {
            return 1;
        }
        var (start, end) = View;
        var pxPerPoint = width * (Last - First) / (_times.Length - 1) / Math.Max(1, end - start);
        return Math.Clamp((pxPerPoint - 2.75) / 1.5, 0, 1);
    }

    private double PlotWidth(double width) => Math.Max(0, width - GutterWidth);

    private double X(double time, double width)
    {
        var (start, end) = View;
        return (time - start) / Math.Max(1, end - start) * width;
    }

    private double Y(double value) => Top + (1 - value / Math.Max(1e-9, TopValue.Value)) * (PlotHeight - Top);

    private protected override void OnActiveIndexChanged(int? previous, int? current)
    {
        var on = current is not null || _hoverEvent is not null;
        _ = Shown.AnimateTo(current is not null ? 1 : 0, MotionLevel == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        Tip.SetVisible(on);
        if (!on)
        {
            _wasOn = false;
        }
        RebuildTooltip();
        if (current is { } index && index < _times.Length)
        {
            var e = EventNear(_times[index]);
            Announcement = $"{FormatDate(_times[index])}, {FormatWithUnit(_values[index], index)}{(e is { } ev ? $". {ev.Label}" : string.Empty)}";
        }
        InvalidatePlots();
    }

    internal override string GetPlotValueText(ChartPlot plot) => string.IsNullOrEmpty(Announcement) ? WindowText : Announcement;

    private void RebuildTooltip()
    {
        var tip = Tip;
        tip.Begin(132, 220);
        if (_hoverEvent is { } h && h < _events.Length)
        {
            var e = _events[h];
            tip.AddTitle(FormatDate(e.Time));
            tip.AddValue(e.Label);
            if (e.Description is { } description)
            {
                tip.AddNote(description);
            }
        }
        else if (ActiveIndex is { } index && index < _times.Length)
        {
            tip.AddTitle(FormatDate(_times[index]));
            tip.AddValue(FormatWithUnit(_values[index], index));
            if (MainPlot is { } plot && Detail(PlotWidth(plot.Bounds.Width)) < 1)
            {
                tip.AddNote($"{FormatValue(Math.Round(_smooth[index].Value))} seven day average");
            }
            if (index >= 7 && _values[index - 7] != 0)
            {
                var change = (_values[index] - _values[index - 7]) / _values[index - 7];
                tip.AddNote($"{(change >= 0 ? "+" : "−")}{Math.Abs(change * 100).ToString("0.0", s_culture)}% vs a week earlier");
            }
            if (EventNear(_times[index]) is { } e)
            {
                EnsurePalette();
                tip.AddEvent(Palette.Series[1], e.Label);
            }
        }
        PlaceTooltip();
    }

    /// <summary>The tooltip sits above the point (below when there is no room), beside it horizontally, and never leaves the plot.</summary>
    private void PlaceTooltip()
    {
        if (_tooltip is null || MainPlot is not { } plot || plot.Bounds.Width <= 0)
        {
            return;
        }
        double ax, ay;
        var width = PlotWidth(plot.Bounds.Width);
        if (_hoverEvent is { } h && h < _events.Length)
        {
            ax = X(_events[h].Time, width);
            ay = Top;
        }
        else if (ActiveIndex is { } index && index < _times.Length)
        {
            ax = X(_times[index], width);
            ay = Y(PointValue(index, width));
        }
        else
        {
            return;
        }
        EnsurePalette();
        var size = _tooltip.Measure(Palette, Texts, width);
        var left = ax + TipGap;
        if (left + size.Width > width)
        {
            left = ax - TipGap - size.Width;
        }
        var top = ay - size.Height - TipGap;
        if (top < 0)
        {
            top = ay + TipGap;
        }
        var target = new Point(Math.Clamp(left, 0, Math.Max(0, width - size.Width)), Math.Clamp(top, 0, Math.Max(0, PlotHeight - size.Height)));
        _tooltip.MoveTo(target, !_wasOn);
        _wasOn = true;
    }

    private double PointValue(int index, double width)
    {
        var detail = Detail(width);
        return _smooth[index].Value + (_values[index] - _smooth[index].Value) * detail;
    }

    private int? Nearest(double x, double width)
    {
        if (IsEmptySeries || width <= 0)
        {
            return null;
        }
        var (start, end) = View;
        var (from, to) = VisibleRange();
        var t = start + x / width * (end - start);
        var at = Math.Clamp(LowerBound(_times, t), 1, _times.Length - 1);
        var nearest = t - _times[at - 1] < _times[at] - t ? at - 1 : at;
        return Math.Clamp(nearest, Math.Max(0, from), Math.Max(0, to - 1));
    }

    #endregion

    #region Input

    internal override double MeasurePlotHeight(ChartPlot plot, double width) => PlotHeight + AxisRowHeight;

    internal override void OnPlotResized(ChartPlot plot, Size size)
    {
        _pathKey = default;
        PlaceTooltip();
    }

    private int? EventAt(ChartPlot plot, Point point)
    {
        var width = PlotWidth(plot.Bounds.Width);
        var (start, end) = View;
        for (var i = 0; i < _events.Length; i++)
        {
            var e = _events[i];
            if (e.Time < start || e.Time > end)
            {
                continue;
            }
            var x = X(e.Time, width);
            if (Math.Abs(point.X - x) <= 12 && point.Y >= 6 && point.Y <= 30)
            {
                return i;
            }
        }
        return null;
    }

    internal override void OnPlotPointerMoved(ChartPlot plot, PointerEventArgs e)
    {
        var point = e.GetPosition(plot);
        var mouse = e.Pointer.Type == PointerType.Mouse;
        var hover = mouse ? EventAt(plot, point) : null;
        if (hover != _hoverEvent)
        {
            _hoverEvent = hover;
            Tip.SetVisible(hover is not null || ActiveIndex is not null);
            RebuildTooltip();
            InvalidatePlots();
        }
        if (mouse && (point.X > PlotWidth(plot.Bounds.Width) || point.Y > PlotHeight))
        {
            SetActive(null);
            return;
        }
        if (mouse || e.GetCurrentPoint(plot).Properties.IsLeftButtonPressed)
        {
            SetActive(Nearest(point.X, PlotWidth(plot.Bounds.Width)));
        }
    }

    internal override void OnPlotPointerPressed(ChartPlot plot, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Reset();
            return;
        }
        if (e.Pointer.Type != PointerType.Mouse)
        {
            e.Pointer.Capture(plot);
            SetActive(Nearest(e.GetPosition(plot).X, PlotWidth(plot.Bounds.Width)));
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
            _hoverEvent = null;
            SetActive(null);
            Tip.SetVisible(false);
        }
    }

    internal override void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
        if (IsEmptySeries)
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
        var (from, to) = VisibleRange();
        var page = Math.Max(1, (int)Math.Round((to - from) / 8.0));
        var current = ActiveIndex ?? to - 1;
        int? next = e.Key switch
        {
            Key.Left => current - 1,
            Key.Right => ActiveIndex is null ? to - 1 : current + 1,
            Key.PageDown => current - page,
            Key.PageUp => current + page,
            Key.Home => from,
            Key.End => to - 1,
            _ => null
        };
        if (next is not { } index)
        {
            return;
        }
        e.Handled = true;
        SetActive(Math.Clamp(index, Math.Max(0, from), to - 1));
    }

    internal override void OnPlotFocusChanged(ChartPlot plot, bool focused, bool keyboard)
    {
        if (!focused)
        {
            SetActive(null);
        }
        else if (keyboard && !IsEmptySeries)
        {
            SetActive(ActiveIndex ?? VisibleRange().To - 1);
        }
    }

    internal override AutomationControlType GetPlotControlType(ChartPlot plot) => AutomationControlType.Group;

    internal override string GetPlotAutomationName(ChartPlot plot) => $"{Label}, {WindowText}. Use left and right arrows to read values.";

    /// <summary>Keyboard model of the window and its handles (Arc).</summary>
    internal bool HandleKey(BrushChartThumbKind edge, KeyEventArgs e)
    {
        if (IsEmptySeries)
        {
            return false;
        }
        var (s, en) = View;
        var win = en - s;
        var whole = Last - First;
        var least = Math.Min(MinSpan.TotalMilliseconds, whole);
        var big = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Key is Key.PageUp or Key.PageDown;
        var delta = e.Key switch
        {
            Key.Left or Key.Down or Key.PageDown => -1,
            Key.Right or Key.Up or Key.PageUp => 1,
            _ => 0
        };
        var stepSize = Math.Max(ChartTime.Day, whole / Math.Max(1, _times.Length - 1));
        (double, double)? next = null;
        if (e.Key is Key.Escape or Key.D0 or Key.NumPad0)
        {
            next = (First, Last);
        }
        else if (edge == BrushChartThumbKind.Window && e.Key is Key.OemPlus or Key.Add)
        {
            next = (s + win / 4, en - win / 4);
        }
        else if (edge == BrushChartThumbKind.Window && e.Key is Key.OemMinus or Key.Subtract)
        {
            next = (s - win / 2, en + win / 2);
        }
        else if (e.Key == Key.Home)
        {
            next = edge == BrushChartThumbKind.End ? (s, s + least) : (First, edge == BrushChartThumbKind.Window ? First + win : en);
        }
        else if (e.Key == Key.End)
        {
            next = edge == BrushChartThumbKind.Start ? (en - least, en) : (edge == BrushChartThumbKind.Window ? Last - win : s, Last);
        }
        else if (delta != 0)
        {
            var move = delta * (edge == BrushChartThumbKind.Window ? win * (big ? 0.5 : 0.1) : stepSize * (big ? 10 : 1));
            next = edge switch
            {
                BrushChartThumbKind.Window => (s + move, en + move),
                BrushChartThumbKind.Start => (Math.Min(s + move, en - least), en),
                _ => (s, Math.Max(en + move, s + least))
            };
        }
        if (next is not { } value)
        {
            return false;
        }
        Commit(value.Item1, value.Item2, false);
        return true;
    }

    #endregion

    #region Rendering

    private void RebuildPaths(double width, double height)
    {
        var (start, end) = View;
        var key = (start, end, width, TopValue.Value);
        if (key == _pathKey && _line is not null)
        {
            return;
        }
        _pathKey = key;
        var (from, to) = VisibleRange();
        LinePath(from, to, width, _readValue, _buffer, width * 2);
        _line = ChartGeometry.Line(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer));
        _area = _buffer.Count > 0 ? ChartGeometry.Area(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer), height) : null;
        LinePath(from, to, width, _readTrend, _buffer, width * 8);
        _trend = ChartGeometry.Line(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer));
        LinePath(from, to, width, _readHigh, _buffer, width * 8);
        LinePath(from, to, width, _readLow, _buffer2, width * 8);
        _band = _buffer.Count > 0
            ? ChartGeometry.Band(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer), System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer2))
            : null;
    }

    /// <summary>A line through the points in view. Past two points per pixel, each pixel column keeps only its low and high so spikes survive.</summary>
    private void LinePath(int from, int to, double width, Func<int, double> read, List<Point> output, double budget)
    {
        output.Clear();
        var count = to - from;
        if (count <= 0)
        {
            return;
        }
        if (count <= budget)
        {
            for (var i = from; i < to; i++)
            {
                output.Add(new Point(X(_times[i], width), Y(read(i))));
            }
            return;
        }
        var column = int.MinValue;
        int low = -1, high = -1;
        for (var i = from; i < to; i++)
        {
            var at = (int)Math.Floor(X(_times[i], width));
            if (at != column)
            {
                Flush(low, high, width, read, output);
                column = at;
                low = high = i;
                continue;
            }
            if (read(i) < read(low))
            {
                low = i;
            }
            if (read(i) > read(high))
            {
                high = i;
            }
        }
        Flush(low, high, width, read, output);
    }

    private void Flush(int low, int high, double width, Func<int, double> read, List<Point> output)
    {
        if (low < 0)
        {
            return;
        }
        var (a, b) = low < high ? (low, high) : (high, low);
        output.Add(new Point(X(_times[a], width), Y(read(a))));
        if (b != a)
        {
            output.Add(new Point(X(_times[b], width), Y(read(b))));
        }
    }

    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var palette = Palette;
        var width = PlotWidth(size.Width);
        var height = PlotHeight;
        if (width <= 0)
        {
            return;
        }
        var series = palette.Series[0];
        var event2 = palette.Series[1];
        var top = TopValue.Value;
        if (ShowGrid)
        {
            var gridPen = palette.Pen(palette.Border, 1);
            for (var i = 1; i < _ticks.Ticks.Length; i++)
            {
                var y = Math.Round(Y(_ticks.Ticks[i])) + 0.5;
                if (y >= 0)
                {
                    context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
                }
            }
        }
        context.DrawLine(palette.Pen(palette.BorderStrong, 1), new Point(0, height - 0.5), new Point(width, height - 0.5));
        foreach (var tick in _ticks.Ticks)
        {
            var y = Y(tick);
            if (y < -6)
            {
                continue;
            }
            var label = Texts.Get(palette, TickLabel(tick), palette.SizeXS, palette.TextMuted);
            context.DrawText(label, new Point(width + 10, y - label.Height / 2));
        }
        if (IsEmptySeries)
        {
            var message = Texts.Get(palette, EmptyLabel, palette.SizeSM, palette.TextMuted);
            context.DrawText(message, new Point((width - message.WidthIncludingTrailingWhitespace) / 2, (height - message.Height) / 2));
            return;
        }
        var (start, end) = View;
        var index = ActiveIndex is { } a && a < _times.Length ? a : (int?)null;
        var eventAt = index is { } ix ? EventNear(_times[ix]) : null;
        var hovered = _hoverEvent is { } h && h < _events.Length ? _events[h] : (Event?)null;

        // Event rules under the line.
        foreach (var e in _events)
        {
            if (e.Time < start || e.Time > end)
            {
                continue;
            }
            var x = Math.Round(X(e.Time, width)) + 0.5;
            var active = hovered?.Time == e.Time || eventAt?.Time == e.Time;
            context.DrawLine(palette.Pen(active ? event2 : palette.BorderStrong, 1), new Point(x, Top - 10), new Point(x, height));
        }

        RebuildPaths(width, height);
        var detail = Detail(width);
        using (context.PushClip(new Rect(0, -8, Math.Max(0, width * RevealProgress), height + 16)))
        using (context.PushOpacity(RevealFade))
        {
            if (_band is not null && detail < 1)
            {
                using (context.PushOpacity(1 - detail))
                {
                    context.DrawGeometry(palette.Brush(Oklch.WithAlpha(series, 0.14)), null, _band);
                }
            }
            if (_area is not null && detail > 0)
            {
                using (context.PushOpacity(detail))
                {
                    context.DrawGeometry(palette.Brush(Oklch.WithAlpha(series, 0.10)), null, _area);
                }
            }
            var pen = palette.Pen(series, 1.75, 0, PenLineCap.Round);
            if (_trend is not null && detail < 1)
            {
                using (context.PushOpacity(1 - detail))
                {
                    context.DrawGeometry(null, pen, _trend);
                }
            }
            if (_line is not null && detail > 0)
            {
                using (context.PushOpacity(detail))
                {
                    context.DrawGeometry(null, pen, _line);
                }
            }
        }

        var shown = Shown.Value;
        if (shown > 0.001 && index is { } i2)
        {
            var cx = X(_times[i2], width);
            var cy = Y(PointValue(i2, width));
            using (context.PushOpacity(shown))
            {
                var x = Math.Round(cx) + 0.5;
                context.DrawLine(palette.Pen(Oklch.WithAlpha(palette.Foreground, 0.45), 1), new Point(x, Top - 10), new Point(x, height));
                context.DrawEllipse(palette.Brush(series), palette.Pen(palette.Surface, 2), new Point(cx, cy), 4, 4);
            }
        }

        DrawEvents(context, palette, width, start, end, hovered, eventAt, event2);

        // Date axis: calendar ticks that fit; the outermost labels stay inside the plot.
        if (_timeTicksKey != (start, end, width))
        {
            _timeTicksKey = (start, end, width);
            ChartTime.Ticks(start, end, width, _timeTicks);
        }
        foreach (var (time, text) in _timeTicks)
        {
            var x = X(time, width);
            var label = Texts.Get(palette, text, palette.SizeXS, palette.TextMuted);
            var w = label.WidthIncludingTrailingWhitespace;
            var shift = x < 24 ? 0 : x > width - 24 ? w : w / 2;
            context.DrawText(label, new Point(Math.Round(x - shift), height + 9));
        }
        _tooltip?.Render(context, palette, Texts);
    }

    /// <summary>Event markers: a dot in the second series color on a hairline, with a label placed left to right and skipped when it would collide.</summary>
    private void DrawEvents(DrawingContext context, ChartPalette palette, double width, double start, double end, Event? hovered, Event? eventAt, Color dot)
    {
        _labelled.Clear();
        var edge = double.NegativeInfinity;
        var inWindow = 0;
        for (var at = 0; at < _events.Length; at++)
        {
            var e = _events[at];
            if (e.Time < start || e.Time > end)
            {
                continue;
            }
            inWindow++;
            var px = X(e.Time, width);
            var text = Texts.Get(palette, e.Label, palette.SizeXS, palette.TextSecondary);
            var w = text.WidthIncludingTrailingWhitespace + 4;
            var next = width;
            for (var k = at + 1; k < _events.Length; k++)
            {
                if (_events[k].Time >= start && _events[k].Time <= end)
                {
                    next = X(_events[k].Time, width) - 10;
                    break;
                }
            }
            if (px + 14 >= edge && px + 14 + w <= next)
            {
                _labelled[e.Time] = false;
                edge = px + 14 + w + 8;
                continue;
            }
            var previous = double.NegativeInfinity;
            for (var k = at - 1; k >= 0; k--)
            {
                if (_events[k].Time >= start && _events[k].Time <= end)
                {
                    previous = X(_events[k].Time, width);
                    break;
                }
            }
            if (px - 14 - w >= Math.Max(0, edge) && (double.IsNegativeInfinity(previous) || px - 14 - w >= previous + 12))
            {
                _labelled[e.Time] = true;
                edge = px + 12;
                continue;
            }
            edge = Math.Max(edge, px + 12);
        }
        foreach (var e in _events)
        {
            if (e.Time < start || e.Time > end)
            {
                continue;
            }
            var px = X(e.Time, width);
            var active = hovered?.Time == e.Time || eventAt?.Time == e.Time;
            var center = new Point(px, 6 + 12);
            var r = active ? 3.5 * 1.3 : 3.5;
            context.DrawEllipse(palette.Brush(dot), palette.Pen(palette.Surface, 2), center, r, r);
            if (_labelled.TryGetValue(e.Time, out var flip))
            {
                var label = Texts.Get(palette, e.Label, palette.SizeXS, active ? palette.Foreground : palette.TextSecondary);
                var x = flip ? px - 12 - 6 - label.WidthIncludingTrailingWhitespace : px + 12;
                context.DrawText(label, new Point(Math.Round(x), center.Y - label.Height / 2));
            }
        }
    }

    #endregion
}
