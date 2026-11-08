using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A live line chart that scrolls continuously as readings arrive, holds still under the pointer so a point can be read,
/// and marks readings flagged as anomalies. Feed it through <see cref="Points"/> or <see cref="Append(DateTimeOffset, double, string?)"/>.
/// </summary>
/// <remarks>
/// The motion is code-driven because it follows the clock and the feed, which a ControlTheme transition cannot express:
/// one <see cref="LiveLoop"/> on the plot moves the view, eases the value axis and pops the markers. Under reduced motion
/// the loop does not run and the plot redraws only when a reading arrives.
/// </remarks>
[TemplatePart(PlotPart, typeof(RealtimeStreamPlot))]
[TemplatePart(StatusPart, typeof(Button))]
[TemplatePart(ValuePart, typeof(RollingDigits))]
[PseudoClasses(PausedPseudoClass, EmptyPseudoClass)]
public class RealtimeStream : TemplatedControl
{
    private const string PlotPart = "PART_Plot";
    private const string StatusPart = "PART_Status";
    private const string ValuePart = "PART_Value";

    /// <summary>Set while the view holds still (<see cref="IsHeld"/>).</summary>
    public const string PausedPseudoClass = ":paused";

    /// <summary>Set while there are no readings.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Readings in ascending time. An <see cref="INotifyCollectionChanged"/> source is watched, and its new items are appended; removing the oldest items from it does not change the view.</summary>
    public static readonly StyledProperty<IEnumerable<RealtimeStreamPoint>?> PointsProperty =
        AvaloniaProperty.Register<RealtimeStream, IEnumerable<RealtimeStreamPoint>?>(nameof(Points));

    /// <summary>What is measured, such as "Checkout latency"; shown above the value and names the chart for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RealtimeStream, string?>(nameof(Label));

    /// <summary>Unit after each value, such as "ms".</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<RealtimeStream, string>(nameof(Unit), string.Empty);

    /// <summary>How much time the plot shows (default 30 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> WindowDurationProperty =
        AvaloniaProperty.Register<RealtimeStream, TimeSpan>(nameof(WindowDuration), TimeSpan.FromSeconds(30));

    /// <summary>The usual time between readings (default 1 second). The view trails the newest reading by this, so the line has somewhere to go until the next one arrives.</summary>
    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<RealtimeStream, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(1));

    /// <summary>Height of the plot in pixels, without the time labels (default 200).</summary>
    public static readonly StyledProperty<double> PlotHeightProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(PlotHeight), 200);

    /// <summary>Formats values (default: grouped, one decimal at most, true minus sign, then <see cref="Unit"/>). With a formatter the header shows its text instead of rolling digits.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<RealtimeStream, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Formats times (default: HH:mm:ss, in the offset the readings carry).</summary>
    public static readonly StyledProperty<Func<DateTimeOffset, string>?> TimeFormatterProperty =
        AvaloniaProperty.Register<RealtimeStream, Func<DateTimeOffset, string>?>(nameof(TimeFormatter));

    /// <summary>Holds the view still; readings that arrive meanwhile are kept (two-way). Set by the status button, Space and Enter.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<RealtimeStream, bool>(nameof(IsPaused), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Holds the view still while the pointer is over the plot (default true).</summary>
    public static readonly StyledProperty<bool> IsPauseOnHoverProperty =
        AvaloniaProperty.Register<RealtimeStream, bool>(nameof(IsPauseOnHover), true);

    /// <summary>Fixed lower end of the value axis (null: follow the data in view).</summary>
    public static readonly StyledProperty<double?> MinimumProperty =
        AvaloniaProperty.Register<RealtimeStream, double?>(nameof(Minimum));

    /// <summary>Fixed upper end of the value axis (null: follow the data in view).</summary>
    public static readonly StyledProperty<double?> MaximumProperty =
        AvaloniaProperty.Register<RealtimeStream, double?>(nameof(Maximum));

    /// <summary>Message shown before the first reading.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<RealtimeStream, string>(nameof(EmptyLabel), "Waiting for data");

    /// <summary>Text of the status button while the view follows the feed.</summary>
    public static readonly StyledProperty<string> LiveLabelProperty =
        AvaloniaProperty.Register<RealtimeStream, string>(nameof(LiveLabel), "Live");

    /// <summary>Text of the status button while the view holds still.</summary>
    public static readonly StyledProperty<string> PausedLabelProperty =
        AvaloniaProperty.Register<RealtimeStream, string>(nameof(PausedLabel), "Paused");

    /// <summary>True while the view holds still for any reason: <see cref="IsPaused"/>, the pointer or the keyboard.</summary>
    public static readonly DirectProperty<RealtimeStream, bool> IsHeldProperty =
        AvaloniaProperty.RegisterDirect<RealtimeStream, bool>(nameof(IsHeld), o => o.IsHeld);

    internal static readonly StyledProperty<double> GutterProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(Gutter), 44);

    internal static readonly StyledProperty<double> NarrowGutterProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(NarrowGutter), 32);

    internal static readonly StyledProperty<double> TimeAxisHeightProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(TimeAxisHeight), 26);

    internal static readonly StyledProperty<double> TimeLabelGapProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(TimeLabelGap), 28);

    internal static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(LineThickness), 2);

    internal static readonly StyledProperty<double> MarkerRadiusProperty =
        AvaloniaProperty.Register<RealtimeStream, double>(nameof(MarkerRadius), 6);

    /// <summary>How fast the view closes its distance to the live edge (per second); this is also the glide after a pause.</summary>
    private const double GlideRate = 5;

    /// <summary>How fast the value axis closes its distance to its target (per second).</summary>
    private const double AxisRate = 7;

    /// <summary>How long a marker takes to pop in, in seconds.</summary>
    private const double PopSeconds = 0.42;

    /// <summary>The most readings kept while the view holds still and the feed keeps coming.</summary>
    internal const int MaxRetained = 200_000;

    private static readonly double s_ticksPerStamp = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

    private readonly RealtimeStreamBuffer _buffer = new();
    private readonly List<RealtimeStreamMarker> _markers = new();
    private INotifyCollectionChanged? _subscribed;
    private bool _wasDetached;
    private RealtimeStreamPlot? _plot;
    private Button? _status;
    private RollingDigits? _digits;
    private TextBlock? _labelText;
    private TextBlock? _valueText;
    private TextBlock? _unitText;
    private TextBlock? _statusText;
    private StackPanel? _valueRow;
    private Control? _liveDot;
    private Control? _pausedGlyph;
    private TimeSpan _offset;
    private long _headTicks;
    private long _arrivalTicks;
    private int _decimals;
    private bool _isHeld;
    private bool _hoverHold;
    private bool _touchHold;
    private bool _keyHold;
    private bool _gliding;
    private long _cursorSequence = -1;
    private double? _pointerX;
    private int _rangeFirst = -1;
    private int _rangeLast = -1;
    private long _rangeDropped = -1;
    private double _axisLow;
    private double _axisHigh = 1;
    private double _targetLow;
    private double _targetHigh = 1;
    private bool _hasAxis;
    private string? _spoken;

    static RealtimeStream()
    {
        AffectsRender<RealtimeStream>(TimeLabelGapProperty, MarkerRadiusProperty);
    }

    /// <summary>Creates a realtime stream.</summary>
    public RealtimeStream()
    {
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    /// <inheritdoc cref="PointsProperty"/>
    public IEnumerable<RealtimeStreamPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <inheritdoc cref="WindowDurationProperty"/>
    public TimeSpan WindowDuration
    {
        get => GetValue(WindowDurationProperty);
        set => SetValue(WindowDurationProperty, value);
    }

    /// <inheritdoc cref="IntervalProperty"/>
    public TimeSpan Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    /// <inheritdoc cref="PlotHeightProperty"/>
    public double PlotHeight
    {
        get => GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <inheritdoc cref="TimeFormatterProperty"/>
    public Func<DateTimeOffset, string>? TimeFormatter
    {
        get => GetValue(TimeFormatterProperty);
        set => SetValue(TimeFormatterProperty, value);
    }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <inheritdoc cref="IsPauseOnHoverProperty"/>
    public bool IsPauseOnHover
    {
        get => GetValue(IsPauseOnHoverProperty);
        set => SetValue(IsPauseOnHoverProperty, value);
    }

    /// <inheritdoc cref="MinimumProperty"/>
    public double? Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <inheritdoc cref="MaximumProperty"/>
    public double? Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <inheritdoc cref="LiveLabelProperty"/>
    public string LiveLabel
    {
        get => GetValue(LiveLabelProperty);
        set => SetValue(LiveLabelProperty, value);
    }

    /// <inheritdoc cref="PausedLabelProperty"/>
    public string PausedLabel
    {
        get => GetValue(PausedLabelProperty);
        set => SetValue(PausedLabelProperty, value);
    }

    /// <inheritdoc cref="IsHeldProperty"/>
    public bool IsHeld => _isHeld;

    /// <summary>Raised when the view starts or stops holding still, for any reason.</summary>
    public event EventHandler<RealtimeStreamPausedChangedEventArgs>? PausedChanged;

    internal double Gutter
    {
        get => GetValue(GutterProperty);
        set => SetValue(GutterProperty, value);
    }

    internal double NarrowGutter
    {
        get => GetValue(NarrowGutterProperty);
        set => SetValue(NarrowGutterProperty, value);
    }

    internal double TimeAxisHeight
    {
        get => GetValue(TimeAxisHeightProperty);
        set => SetValue(TimeAxisHeightProperty, value);
    }

    internal double TimeLabelGap
    {
        get => GetValue(TimeLabelGapProperty);
        set => SetValue(TimeLabelGapProperty, value);
    }

    internal double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    internal double MarkerRadius
    {
        get => GetValue(MarkerRadiusProperty);
        set => SetValue(MarkerRadiusProperty, value);
    }

    /// <summary>The time source, in ticks. Tests replace it; only differences between two readings of it are used.</summary>
    internal Func<long> Clock { get; set; } = static () => (long)(Stopwatch.GetTimestamp() * s_ticksPerStamp);

    /// <summary>Everything said to assistive technology, as it is said (tests).</summary>
    internal Action<string>? SpokenSink { get; set; }

    internal RealtimeStreamBuffer Buffer => _buffer;

    internal IReadOnlyList<RealtimeStreamMarker> Markers => _markers;

    internal RealtimeStreamPlot? Plot => _plot;

    internal long HeadTicks => _headTicks;

    internal long WindowTicks => Math.Max(TimeSpan.TicksPerMillisecond * 100, WindowDuration.Ticks);

    /// <summary>The offset the readings carry, in ticks: time labels fall on round moments of that clock.</summary>
    internal long OffsetTicks => _offset.Ticks;

    internal double AxisLow => _axisLow;

    internal double AxisHigh => _axisHigh;

    internal double TargetLow => _targetLow;

    internal double TargetHigh => _targetHigh;

    internal bool IsEmpty => _buffer.Count == 0;

    internal string? Spoken => _spoken;

    /// <summary>Index of the reading being read, or -1.</summary>
    internal int CursorIndex
    {
        get
        {
            if (_cursorSequence < 0)
            {
                return -1;
            }
            var index = _cursorSequence - _buffer.Dropped;
            return index >= 0 && index < _buffer.Count ? (int)index : -1;
        }
    }

    /// <summary>The first and last readings in view, found by binary search (first &gt; last when none is).</summary>
    internal (int First, int Last) VisibleRange
    {
        get
        {
            if (IsEmpty)
            {
                return (0, -1);
            }
            return (_buffer.LowerBound(_headTicks - WindowTicks), _buffer.UpperBound(_headTicks) - 1);
        }
    }

    /// <summary>The value of the line at the right edge of the plot: between the two readings around that moment.</summary>
    internal double LeadingValue
    {
        get
        {
            if (IsEmpty)
            {
                return 0;
            }
            var after = _buffer.UpperBound(_headTicks);
            if (after == 0)
            {
                return _buffer.ValueAt(0);
            }
            if (after >= _buffer.Count)
            {
                return _buffer.ValueAt(_buffer.Count - 1);
            }
            var fromTicks = _buffer.TicksAt(after - 1);
            var toTicks = _buffer.TicksAt(after);
            var t = toTicks == fromTicks ? 1 : (double)(_headTicks - fromTicks) / (toTicks - fromTicks);
            return ChartsBMath.Mix(_buffer.ValueAt(after - 1), _buffer.ValueAt(after), t);
        }
    }

    /// <summary>Adds a reading; the cheap way to feed the chart when there is no <see cref="Points"/> collection. A time older than the newest reading and a value that is not finite are ignored.</summary>
    public void Append(RealtimeStreamPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        Append(point.Time, point.Value, point.Anomaly);
    }

    /// <summary>Adds a reading without allocating a point; a non-null <paramref name="anomaly"/> becomes the marker text.</summary>
    public void Append(DateTimeOffset time, double value, string? anomaly = null)
    {
        if (AddReading(time, value, anomaly, live: true))
        {
            AfterData(live: true);
        }
    }

    /// <summary>Forgets every reading.</summary>
    public void Clear()
    {
        ClearData();
        AfterData(live: false);
    }

    // ---- data ----------------------------------------------------------------------------------------------------

    private void ClearData()
    {
        _buffer.Clear();
        _markers.Clear();
        _decimals = 0;
        _cursorSequence = -1;
        _spoken = null;
        _hasAxis = false;
        _rangeFirst = _rangeLast = -1;
        _rangeDropped = -1;
    }

    private bool AddReading(DateTimeOffset time, double value, string? anomaly, bool live)
    {
        var ticks = time.UtcTicks;
        if (!_buffer.Add(ticks, value, anomaly))
        {
            return false;
        }
        _offset = time.Offset;
        if (_decimals == 0 && Math.Round(value) != value)
        {
            _decimals = 1;
        }
        if (anomaly is not null)
        {
            var sequence = _buffer.Dropped + _buffer.Count - 1;
            _markers.Add(new RealtimeStreamMarker(sequence, live && IsFullMotion ? 0 : 1));
            if (live)
            {
                Say($"Anomaly at {FormatTime(ticks)}: {Format(value)}, {anomaly}");
            }
        }
        return true;
    }

    private bool IsFullMotion => _plot is { } plot && plot.IsLoaded && ArcMotion.IsFull(plot);

    private void AfterData(bool live)
    {
        var now = Clock();
        _arrivalTicks = now;
        if (IsEmpty)
        {
            _headTicks = 0;
            ClearReading();
        }
        else if (!_isHeld)
        {
            if (!IsFullMotion)
            {
                _headTicks = _buffer.LastTicks;
            }
            else if (!live || _buffer.Count == 1)
            {
                _headTicks = LiveEdge(now);
            }
        }
        else
        {
            _headTicks = Math.Clamp(_headTicks, _buffer.FirstTicks, _buffer.LastTicks);
        }
        Trim();
        PseudoClasses.Set(EmptyPseudoClass, IsEmpty);
        if (_plot is not null)
        {
            _plot.Focusable = !IsEmpty;
        }
        UpdateHeader();
        // A view holding still does not change for a reading that arrives meanwhile.
        if (!_isHeld || !live)
        {
            UpdateRange(force: !live);
            _plot?.MarkGeometryDirty();
            _plot?.Wake();
        }
    }

    /// <summary>Drops readings well behind the view, so an endless feed uses a steady amount of memory.</summary>
    private void Trim()
    {
        if (_buffer.Count == 0)
        {
            return;
        }
        var keep = _buffer.LowerBound(_headTicks - WindowTicks * 2);
        // One reading before the kept time still draws the segment entering the plot from the left.
        var drop = Math.Max(0, keep - 1);
        if (_buffer.Count - drop > MaxRetained)
        {
            drop = _buffer.Count - MaxRetained;
        }
        // Dropping one at a time is cheap, but markers and ranges are simpler to update in small batches.
        if (drop < 32 && _buffer.Count < MaxRetained)
        {
            return;
        }
        _buffer.DropFirst(drop);
        var gone = 0;
        while (gone < _markers.Count && _markers[gone].Sequence < _buffer.Dropped)
        {
            gone++;
        }
        if (gone > 0)
        {
            _markers.RemoveRange(0, gone);
        }
        _headTicks = Math.Max(_headTicks, _buffer.FirstTicks);
    }

    private void Reload()
    {
        ClearData();
        if (Points is { } points)
        {
            foreach (var point in points)
            {
                if (point is not null)
                {
                    AddReading(point.Time, point.Value, point.Anomaly, live: false);
                }
            }
        }
        AfterData(live: false);
    }

    private void Subscribe()
    {
        if (_subscribed is null && IsAttachedToVisualTree() && Points is INotifyCollectionChanged source)
        {
            _subscribed = source;
            source.CollectionChanged += OnPointsChanged;
        }
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.CollectionChanged -= OnPointsChanged;
            _subscribed = null;
        }
    }

    private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems is { } items:
                var added = false;
                for (var i = 0; i < items.Count; i++)
                {
                    if (items[i] is RealtimeStreamPoint point)
                    {
                        added |= AddReading(point.Time, point.Value, point.Anomaly, live: true);
                    }
                }
                if (added)
                {
                    AfterData(live: true);
                }
                break;
            case NotifyCollectionChangedAction.Remove when e.OldStartingIndex == 0:
                // A feed that trims its own history: the chart keeps what it needs and trims on its own.
                break;
            default:
                Reload();
                break;
        }
    }

    // ---- the view ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Where the right edge of the plot belongs at <paramref name="now"/>: the newest reading's time, plus what has passed
    /// since it arrived, minus <see cref="Interval"/>, and never past the newest reading.
    /// </summary>
    internal long LiveEdge(long now)
    {
        if (IsEmpty)
        {
            return 0;
        }
        var last = _buffer.LastTicks;
        var edge = last + Math.Max(0, now - _arrivalTicks) - Math.Max(0, Interval.Ticks);
        return Math.Clamp(edge, _buffer.FirstTicks, last);
    }

    /// <summary>One frame of motion: moves the view, eases the axis and pops markers. Returns whether more frames are needed.</summary>
    internal bool AdvanceFrame(double dt)
    {
        if (IsEmpty)
        {
            return false;
        }
        var changed = false;
        var moving = false;
        if (!_isHeld)
        {
            var target = LiveEdge(Clock());
            var head = _headTicks + (long)(dt * TimeSpan.TicksPerSecond);
            var error = target - head;
            head += (long)(error * (1 - Math.Exp(-dt * GlideRate)));
            // The view never runs backward: arriving a little early is absorbed by the next frames.
            head = Math.Clamp(head, _headTicks, _buffer.LastTicks);
            if (head != _headTicks)
            {
                _headTicks = head;
                changed = true;
            }
            moving = _headTicks < _buffer.LastTicks;
            if (Math.Abs(target - _headTicks) < TimeSpan.TicksPerMillisecond)
            {
                _gliding = false;
            }
            UpdateRange();
            if (_pointerX is { } x && _plot is not null)
            {
                PickAt(x);
            }
        }
        if (EaseAxis(dt))
        {
            changed = true;
            moving = true;
            _plot?.MarkGeometryDirty();
        }
        if (PopMarkers(dt))
        {
            changed = true;
            moving = true;
        }
        if (changed)
        {
            _plot?.InvalidateVisual();
        }
        return moving;
    }

    /// <summary>The loop came back after being gated: a view left far behind (off screen, say) jumps instead of racing.</summary>
    internal void OnLoopStarted()
    {
        if (_isHeld || IsEmpty || _gliding)
        {
            return;
        }
        var target = LiveEdge(Clock());
        if (target - _headTicks > WindowTicks)
        {
            _headTicks = target;
            UpdateRange();
            _plot?.MarkGeometryDirty();
        }
    }

    /// <summary>The loop stopped because motion was reduced: show the newest reading at rest.</summary>
    internal void OnLoopGated()
    {
        if (_isHeld || IsEmpty || _plot is null || ArcMotion.IsFull(_plot))
        {
            return;
        }
        _headTicks = _buffer.LastTicks;
        UpdateRange();
        SnapMotion();
        _plot.MarkGeometryDirty();
    }

    private void SnapMotion()
    {
        _axisLow = _targetLow;
        _axisHigh = _targetHigh;
        foreach (var marker in _markers)
        {
            marker.Progress = 1;
        }
    }

    private bool PopMarkers(double dt)
    {
        var popping = false;
        for (var i = _markers.Count - 1; i >= 0; i--)
        {
            var marker = _markers[i];
            if (marker.Progress >= 1)
            {
                // Markers pop in order, so everything older has finished too.
                break;
            }
            var index = marker.Sequence - _buffer.Dropped;
            if (index < _buffer.Count && _buffer.TicksAt((int)index) <= _headTicks)
            {
                marker.Progress = Math.Min(1, marker.Progress + dt / PopSeconds);
                popping = true;
            }
            else if (!_isHeld)
            {
                popping = true;
            }
        }
        return popping;
    }

    private bool EaseAxis(double dt)
    {
        var span = Math.Max(1e-9, _targetHigh - _targetLow);
        if (Math.Abs(_axisLow - _targetLow) < span * 0.001 && Math.Abs(_axisHigh - _targetHigh) < span * 0.001)
        {
            if (_axisLow == _targetLow && _axisHigh == _targetHigh)
            {
                return false;
            }
            _axisLow = _targetLow;
            _axisHigh = _targetHigh;
            return true;
        }
        var k = 1 - Math.Exp(-dt * AxisRate);
        _axisLow += (_targetLow - _axisLow) * k;
        _axisHigh += (_targetHigh - _axisHigh) * k;
        return true;
    }

    /// <summary>Recomputes the axis target when another reading entered or left the view. Returns whether it did.</summary>
    private bool UpdateRange(bool force = false)
    {
        if (IsEmpty)
        {
            _targetLow = _axisLow = Minimum ?? 0;
            _targetHigh = _axisHigh = Maximum ?? 1;
            _hasAxis = false;
            _plot?.OnAxisTargetChanged();
            return true;
        }
        var (first, last) = VisibleRange;
        // Include the neighbours whose segments reach into the plot, so the line never leaves it between two readings.
        first = Math.Max(0, first - 1);
        last = Math.Min(_buffer.Count - 1, last + 1);
        if (!force && first == _rangeFirst && last == _rangeLast && _buffer.Dropped == _rangeDropped)
        {
            return false;
        }
        _rangeFirst = first;
        _rangeLast = last;
        _rangeDropped = _buffer.Dropped;
        var low = double.PositiveInfinity;
        var high = double.NegativeInfinity;
        for (var i = first; i <= last; i++)
        {
            var value = _buffer.ValueAt(i);
            low = Math.Min(low, value);
            high = Math.Max(high, value);
        }
        var (targetLow, targetHigh) = AxisFor(low, high, Minimum, Maximum);
        if (targetLow != _targetLow || targetHigh != _targetHigh || !_hasAxis)
        {
            _targetLow = targetLow;
            _targetHigh = targetHigh;
            if (!_hasAxis || !IsFullMotion)
            {
                _axisLow = targetLow;
                _axisHigh = targetHigh;
                _hasAxis = true;
            }
            _plot?.OnAxisTargetChanged();
        }
        return true;
    }

    /// <summary>The value axis for the readings in view: fixed ends stay, free ends get a little room and a round number.</summary>
    internal static (double Low, double High) AxisFor(double low, double high, double? minimum, double? maximum)
    {
        if (minimum is { } fixedLow && maximum is { } fixedHigh)
        {
            return fixedHigh > fixedLow ? (fixedLow, fixedHigh) : (fixedLow, fixedLow + 1);
        }
        var bottom = minimum ?? low;
        var top = maximum ?? high;
        if (!(top > bottom))
        {
            var pad = Math.Max(1, Math.Abs(bottom) * 0.1);
            if (minimum is null)
            {
                bottom -= pad;
            }
            if (maximum is null)
            {
                top += pad;
            }
        }
        var room = (top - bottom) * 0.1;
        var step = ChartsBMath.NiceStep(top - bottom + 2 * room, 4);
        var niceLow = minimum ?? Math.Floor((bottom - room) / step) * step;
        var niceHigh = maximum ?? Math.Ceiling((top + room) / step) * step;
        // Readings that never go below zero do not get an axis that does.
        if (minimum is null && low >= 0 && niceLow < 0)
        {
            niceLow = 0;
        }
        return niceHigh > niceLow ? (niceLow, niceHigh) : (niceLow, niceLow + step);
    }

    // ---- holding still -------------------------------------------------------------------------------------------

    private void SyncHold(RealtimeStreamHoldReason reason)
    {
        var held = IsPaused || _hoverHold || _touchHold || _keyHold;
        if (held == _isHeld)
        {
            return;
        }
        SetAndRaise(IsHeldProperty, ref _isHeld, held);
        PseudoClasses.Set(PausedPseudoClass, held);
        UpdateStatus();
        if (!held)
        {
            // Back to live: the loop glides there; without full motion the view is simply there.
            if (IsFullMotion)
            {
                _gliding = true;
            }
            else if (!IsEmpty)
            {
                _headTicks = _buffer.LastTicks;
                UpdateRange();
            }
            _plot?.MarkGeometryDirty();
        }
        _plot?.Wake();
        PausedChanged?.Invoke(this, new RealtimeStreamPausedChangedEventArgs(held, reason));
    }

    /// <summary>Hover or touch over the plot at <paramref name="x"/>.</summary>
    internal void PointerAt(double x, bool touch)
    {
        if (IsEmpty)
        {
            return;
        }
        _pointerX = x;
        if (touch)
        {
            _touchHold = true;
        }
        else if (IsPauseOnHover)
        {
            _hoverHold = true;
        }
        SyncHold(RealtimeStreamHoldReason.Pointer);
        PickAt(x);
    }

    internal void PointerGone(bool touch)
    {
        if (touch)
        {
            _touchHold = false;
        }
        else
        {
            _hoverHold = false;
        }
        _pointerX = null;
        if (!_keyHold)
        {
            ClearReading();
        }
        SyncHold(RealtimeStreamHoldReason.Pointer);
    }

    private void PickAt(double x)
    {
        if (_plot is null)
        {
            return;
        }
        var index = NearestVisible(_plot.TicksAtX(x));
        var sequence = index < 0 ? -1 : _buffer.Dropped + index;
        if (sequence != _cursorSequence)
        {
            _cursorSequence = sequence;
            _plot.SyncReading();
        }
    }

    /// <summary>The reading in view closest to <paramref name="ticks"/>, or -1.</summary>
    internal int NearestVisible(long ticks)
    {
        var (first, last) = VisibleRange;
        if (first > last)
        {
            return -1;
        }
        var after = Math.Clamp(_buffer.LowerBound(ticks), first, last);
        if (after > first && ticks - _buffer.TicksAt(after - 1) <= _buffer.TicksAt(after) - ticks)
        {
            return after - 1;
        }
        return after;
    }

    private void ClearReading()
    {
        if (_cursorSequence < 0 && _spoken is null)
        {
            return;
        }
        _cursorSequence = -1;
        _spoken = null;
        _plot?.SyncReading();
    }

    private void SetCursor(int index)
    {
        _cursorSequence = _buffer.Dropped + index;
        _plot?.SyncReading();
        Say(Describe(index));
    }

    /// <summary>The words for a reading.</summary>
    internal string Describe(int index)
    {
        var text = $"{FormatTime(_buffer.TicksAt(index))}: {Format(_buffer.ValueAt(index))}";
        return _buffer.TagAt(index) is { } tag ? $"{text}, anomaly: {tag}" : text;
    }

    private void Say(string text)
    {
        _spoken = text;
        _plot?.Announce();
        SpokenSink?.Invoke(text);
    }

    internal void KeyboardEntered()
    {
        if (IsEmpty)
        {
            return;
        }
        _keyHold = true;
        SyncHold(RealtimeStreamHoldReason.Keyboard);
        ReadNewestVisible();
    }

    internal void KeyboardLeft()
    {
        if (!_keyHold)
        {
            return;
        }
        _keyHold = false;
        if (_pointerX is null)
        {
            ClearReading();
        }
        SyncHold(RealtimeStreamHoldReason.Keyboard);
    }

    private void ReadNewestVisible()
    {
        var (first, last) = VisibleRange;
        if (first <= last)
        {
            SetCursor(last);
        }
    }

    /// <summary>The keyboard model of the plot. Returns whether the key was used.</summary>
    internal bool HandleKey(Key key)
    {
        if (IsEmpty)
        {
            return false;
        }
        switch (key)
        {
            case Key.Left:
                MoveCursor(-1);
                return true;
            case Key.Right:
                MoveCursor(1);
                return true;
            case Key.PageUp:
                MoveCursor(-10);
                return true;
            case Key.PageDown:
                MoveCursor(10);
                return true;
            case Key.Home:
                JumpCursor(oldest: true);
                return true;
            case Key.End:
                JumpCursor(oldest: false);
                return true;
            case Key.Space:
            case Key.Enter:
                if (_isHeld)
                {
                    Resume();
                }
                else
                {
                    IsPaused = true;
                    ReadNewestVisible();
                }
                return true;
            case Key.Escape when _isHeld || CursorIndex >= 0:
                Resume();
                return true;
            default:
                return false;
        }
    }

    private void Resume()
    {
        ClearReading();
        _keyHold = false;
        // A pointer resting on the plot keeps holding; it is released when it leaves.
        SyncHold(RealtimeStreamHoldReason.Keyboard);
        IsPaused = false;
    }

    private void Hold()
    {
        if (!_keyHold)
        {
            _keyHold = true;
            SyncHold(RealtimeStreamHoldReason.Keyboard);
        }
    }

    private void MoveCursor(int delta)
    {
        Hold();
        var (first, last) = VisibleRange;
        if (first > last)
        {
            return;
        }
        var at = CursorIndex;
        SetCursor(at < 0 ? last : Math.Clamp(at + delta, first, last));
    }

    private void JumpCursor(bool oldest)
    {
        Hold();
        var (first, last) = VisibleRange;
        if (first <= last)
        {
            SetCursor(oldest ? first : last);
        }
    }

    // ---- text ----------------------------------------------------------------------------------------------------

    internal string Format(double value)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value);
        }
        return ChartsBMath.Grouped(value).Replace("-", "−") + Unit;
    }

    internal string FormatTime(long ticks)
    {
        var time = new DateTimeOffset(ticks, TimeSpan.Zero).ToOffset(_offset);
        return TimeFormatter is { } formatter ? formatter(time) : time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>The whole chart in words.</summary>
    internal string Summary
    {
        get
        {
            var name = string.IsNullOrEmpty(Label) ? "Realtime stream" : Label;
            if (IsEmpty)
            {
                return $"{name}. {EmptyLabel}.";
            }
            var (first, last) = VisibleRange;
            var count = Math.Max(0, last - first + 1);
            return $"{name}. Latest {Format(_buffer.ValueAt(_buffer.Count - 1))} at {FormatTime(_buffer.LastTicks)}. {count} readings in view.";
        }
    }

    private void UpdateHeader()
    {
        if (_valueRow is null)
        {
            return;
        }
        var empty = IsEmpty;
        var custom = ValueFormatter is not null;
        var value = empty ? 0 : _buffer.ValueAt(_buffer.Count - 1);
        if (_digits is not null)
        {
            _digits.IsVisible = !empty && !custom;
            if (!empty && !custom)
            {
                _digits.Decimals = _decimals;
                _digits.Value = value;
            }
        }
        if (_valueText is not null)
        {
            _valueText.IsVisible = empty || custom;
            _valueText.Text = empty ? "–" : custom ? Format(value) : null;
        }
        if (_unitText is not null)
        {
            _unitText.IsVisible = !empty && !custom && !string.IsNullOrEmpty(Unit);
            // The row already spaces the unit from the digits; a leading space in Unit is for the axis labels.
            _unitText.Text = Unit.Trim();
        }
        var label = string.IsNullOrEmpty(Label) ? "Current value" : Label;
        AutomationProperties.SetName(_valueRow, empty ? $"{label}: {EmptyLabel}" : $"{label}: {Format(value)}");
    }

    private void UpdateStatus()
    {
        var held = _isHeld;
        if (_statusText is not null)
        {
            _statusText.Text = held ? PausedLabel : LiveLabel;
        }
        if (_liveDot is not null)
        {
            _liveDot.IsVisible = !held;
        }
        if (_pausedGlyph is not null)
        {
            _pausedGlyph.IsVisible = held;
        }
        if (_status is not null)
        {
            AutomationProperties.SetName(_status, held ? $"{PausedLabel}. Resume updates" : $"{LiveLabel}. Pause updates");
        }
    }

    // ---- control -------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_status is not null)
        {
            _status.Click -= OnStatusClick;
        }
        if (_plot is not null)
        {
            _plot.Owner = null;
        }
        var scope = e.NameScope;
        _plot = scope.Find<RealtimeStreamPlot>(PlotPart);
        _status = scope.Find<Button>(StatusPart);
        _digits = scope.Find<RollingDigits>(ValuePart);
        _valueText = scope.Find<TextBlock>("PART_ValueText");
        _unitText = scope.Find<TextBlock>("PART_Unit");
        _statusText = scope.Find<TextBlock>("PART_StatusText");
        _valueRow = scope.Find<StackPanel>("PART_ValueRow");
        _liveDot = scope.Find<Control>("PART_LiveDot");
        _pausedGlyph = scope.Find<Control>("PART_PausedGlyph");
        _labelText = scope.Find<TextBlock>("PART_Label");
        if (_labelText is not null)
        {
            _labelText.IsVisible = !string.IsNullOrEmpty(Label);
        }
        if (_status is not null)
        {
            _status.Click += OnStatusClick;
        }
        if (_plot is not null)
        {
            _plot.Owner = this;
            _plot.Focusable = !IsEmpty;
        }
        UpdateHeader();
        UpdateStatus();
    }

    private void OnStatusClick(object? sender, RoutedEventArgs e) => IsPaused = !IsPaused;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
        if (_wasDetached)
        {
            _wasDetached = false;
            if (Points is not null)
            {
                // Changes made while detached were not observed.
                Reload();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
        _wasDetached = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PointsProperty)
        {
            Unsubscribe();
            Subscribe();
            Reload();
        }
        else if (property == IsPausedProperty)
        {
            SyncHold(RealtimeStreamHoldReason.Request);
        }
        else if (property == IsPauseOnHoverProperty)
        {
            if (!IsPauseOnHover && _hoverHold)
            {
                _hoverHold = false;
                SyncHold(RealtimeStreamHoldReason.Pointer);
            }
        }
        else if (property == WindowDurationProperty && Points is not null)
        {
            // A longer window may need readings already trimmed from the buffer.
            Reload();
            _plot?.OnScaleChanged();
        }
        else if (property == WindowDurationProperty || property == MinimumProperty || property == MaximumProperty || property == IntervalProperty)
        {
            if (!IsEmpty)
            {
                _hasAxis = _hasAxis && IsFullMotion;
                UpdateRange(force: true);
            }
            else
            {
                UpdateRange();
            }
            _plot?.OnScaleChanged();
            _plot?.Wake();
        }
        else if (property == ValueFormatterProperty || property == TimeFormatterProperty || property == UnitProperty)
        {
            UpdateHeader();
            _plot?.OnTextChanged();
        }
        else if (property == EmptyLabelProperty)
        {
            UpdateHeader();
            _plot?.OnTextChanged();
        }
        else if (property == LabelProperty)
        {
            UpdateHeader();
            if (_labelText is not null)
            {
                _labelText.IsVisible = !string.IsNullOrEmpty(Label);
            }
        }
        else if (property == LiveLabelProperty || property == PausedLabelProperty)
        {
            UpdateStatus();
        }
        else if (property == PlotHeightProperty || property == TimeAxisHeightProperty)
        {
            _plot?.InvalidateMeasure();
            _plot?.OnScaleChanged();
        }
        else if (property == GutterProperty || property == NarrowGutterProperty || property == LineThicknessProperty || property == MarkerRadiusProperty)
        {
            _plot?.OnTextChanged();
        }
    }

}

/// <summary>A reading flagged as an anomaly.</summary>
internal sealed class RealtimeStreamMarker(long sequence, double progress)
{
    /// <summary>The reading, counted from the first since the last clear (see <see cref="RealtimeStreamBuffer.Dropped"/>).</summary>
    public long Sequence { get; } = sequence;

    /// <summary>How far the pop has come, 0 to 1.</summary>
    public double Progress { get; set; } = progress;

    /// <summary>The marker text, laid out once.</summary>
    public Avalonia.Media.TextFormatting.TextLayout? Layout { get; set; }
}
