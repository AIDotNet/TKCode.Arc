using System.Globalization;
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
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A time window over a live area chart. Drag either edge of the frame to resize the window, drag inside it to pan and
/// flick it to let it coast, or press anywhere else on the chart to glide the window there. The chart outside the window
/// fades back, the part inside stays in full color, and the start, length and end readouts roll to their new values. New
/// <see cref="Data"/> morphs the chart in place, and with <see cref="IsLive"/> a dot pulses on the latest point.
/// </summary>
/// <remarks>
/// Values are in domain units from <see cref="Minimum"/> to <see cref="Maximum"/>, one data point per unit by default
/// (for example day 0 to day 181). Both ends snap to <see cref="Step"/>. The start edge, the window and the end edge are
/// three focusable sliders.
/// </remarks>
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(StartReadoutPart, typeof(Control))]
[TemplatePart(SpanReadoutPart, typeof(Control))]
[TemplatePart(EndReadoutPart, typeof(Control))]
[TemplatePart(AxisPart, typeof(Control))]
[PseudoClasses(DraggingPseudoClass)]
public class WindowRange : TemplatedControl
{
    public const string PlotPart = "PART_Plot";
    public const string StartReadoutPart = "PART_StartReadout";
    public const string SpanReadoutPart = "PART_SpanReadout";
    public const string EndReadoutPart = "PART_EndReadout";
    public const string AxisPart = "PART_Axis";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Positions on screen are kept in thousandths of the domain, so springs settle well below a pixel.</summary>
    internal const double Units = 1000;

    // Arc window range (design reference), fitted from per-frame samples in px.
    /// <summary>Keys, presets, new values and the coast after a pan.</summary>
    internal static readonly SpringSpec GlideSpring = SpringSpec.FromPhysical(273, 30.5);

    /// <summary>The glide to a press outside the window, the snap of a released edge, the return from a stretch, the nudge.</summary>
    internal static readonly SpringSpec SnapSpring = SpringSpec.FromPhysical(565, 43.5);

    /// <summary>The press feedback: the tint inside the window and the stretch of the grips.</summary>
    internal static readonly SpringSpec PressSpring = SpringSpec.FromPhysical(765, 43.5);

    /// <summary>New data reshapes the chart on this spring.</summary>
    internal static readonly SpringSpec MorphSpring = SpringSpec.FromPhysical(190, 25);

    /// <summary>A released pan lands where its speed would carry it in this long.</summary>
    internal const double FlickHorizon = 0.2;

    /// <summary>Pixels an edge stretches past its limit, at most.</summary>
    internal const double EdgeStretch = 10;

    /// <summary>Pixels the window stretches past either end of the domain, at most.</summary>
    internal const double PanStretch = 18;

    /// <summary>Speed (px/s) a key press at a limit nudges the frame with.</summary>
    internal const double NudgeSpeed = 237;

    /// <summary>Grip height while the window is held.</summary>
    internal const double PanGripScale = 1.12;

    /// <summary>Grip height while its edge is held.</summary>
    internal const double EdgeGripScale = 1.4;

    /// <summary>One pulse of the live dot, in seconds; the ring grows for the first 70% of it.</summary>
    internal const double PulsePeriod = 2.4;

    private const double PulseGrow = 0.7;
    private const double PulseScale = 3.2;
    private const double PulseStartOpacity = 0.4;

    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    #region Properties

    /// <summary>
    /// The series behind the window, one number per point, spread evenly from <see cref="Minimum"/> to
    /// <see cref="Maximum"/>. Assign a new list to morph the chart to it; edits inside the list are not observed.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> DataProperty =
        AvaloniaProperty.Register<WindowRange, IReadOnlyList<double>?>(nameof(Data));

    /// <summary>The start of the window in domain units; null means the start of the last quarter of the domain.</summary>
    public static readonly StyledProperty<double?> StartProperty =
        AvaloniaProperty.Register<WindowRange, double?>(nameof(Start), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The end of the window in domain units; null means the end of the domain.</summary>
    public static readonly StyledProperty<double?> EndProperty =
        AvaloniaProperty.Register<WindowRange, double?>(nameof(End), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The start of the domain, for example day 0.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<WindowRange, double>(nameof(Minimum));

    /// <summary>The end of the domain; null means the index of the last data point.</summary>
    public static readonly StyledProperty<double?> MaximumProperty =
        AvaloniaProperty.Register<WindowRange, double?>(nameof(Maximum));

    /// <summary>Both ends snap to multiples of this, counted from <see cref="Minimum"/>, and an arrow key moves by it.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<WindowRange, double>(nameof(Step), 1);

    /// <summary>How far Page Up, Page Down and Shift with an arrow move; null means a tenth of the domain.</summary>
    public static readonly StyledProperty<double?> LargeStepProperty =
        AvaloniaProperty.Register<WindowRange, double?>(nameof(LargeStep));

    /// <summary>The narrowest window as end minus start; null means one <see cref="Step"/>.</summary>
    public static readonly StyledProperty<double?> MinSpanProperty =
        AvaloniaProperty.Register<WindowRange, double?>(nameof(MinSpan));

    /// <summary>The widest window as end minus start; null means the whole domain.</summary>
    public static readonly StyledProperty<double?> MaxSpanProperty =
        AvaloniaProperty.Register<WindowRange, double?>(nameof(MaxSpan));

    /// <summary>
    /// Formats the start and end readouts and the spoken values, for example a day index as "Jun 14". Without it the value
    /// is written as a number with the decimals of <see cref="Step"/>.
    /// </summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<WindowRange, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>
    /// Formats the middle readout from end minus start, for example <c>span =&gt; $"{span + 1} days"</c>. Without it the
    /// span is written as a number.
    /// </summary>
    public static readonly StyledProperty<Func<double, string>?> SpanFormatterProperty =
        AvaloniaProperty.Register<WindowRange, Func<double, string>?>(nameof(SpanFormatter));

    /// <summary>Shows the length of the window between the start and end readouts. Turn it off in very tight spaces.</summary>
    public static readonly StyledProperty<bool> ShowSpanProperty =
        AvaloniaProperty.Register<WindowRange, bool>(nameof(ShowSpan), true);

    /// <summary>Labels under the chart, such as the first day of each month. Assign a new list to change them.</summary>
    public static readonly StyledProperty<IEnumerable<WindowRangeTick>?> TicksProperty =
        AvaloniaProperty.Register<WindowRange, IEnumerable<WindowRangeTick>?>(nameof(Ticks));

    /// <summary>Names the group and prefixes the names of the start, window and end sliders.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<WindowRange, string>(nameof(Label), "Date range");

    /// <summary>Pulses a dot on the latest point to show that the series is still arriving.</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<WindowRange, bool>(nameof(IsLive));

    /// <summary>
    /// One color for the frame and the line. Null uses the theme: the selected-control color for the frame and the first
    /// chart series for the line.
    /// </summary>
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<WindowRange, IBrush?>(nameof(Accent));

    /// <summary>Height of the chart and its frame in pixels, from 64 to 240.</summary>
    public static readonly StyledProperty<double> ChartHeightProperty =
        AvaloniaProperty.Register<WindowRange, double>(nameof(ChartHeight), 112,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 64, 240) : 112);

    /// <summary>Raised on every change of the window: while dragging, while it coasts after a flick, and for keys and code.</summary>
    public static readonly RoutedEvent<WindowRangeValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<WindowRange, WindowRangeValueChangedEventArgs>(nameof(ValueChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once the window has settled after a drag, a flick or a key. Fetch the report for the window here.</summary>
    public static readonly RoutedEvent<WindowRangeValueCommittedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<WindowRange, WindowRangeValueCommittedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="DataProperty"/>
    public IReadOnlyList<double>? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <inheritdoc cref="StartProperty"/>
    public double? Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="EndProperty"/>
    public double? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <inheritdoc cref="LargeStepProperty"/>
    public double? LargeStep { get => GetValue(LargeStepProperty); set => SetValue(LargeStepProperty, value); }

    /// <inheritdoc cref="MinSpanProperty"/>
    public double? MinSpan { get => GetValue(MinSpanProperty); set => SetValue(MinSpanProperty, value); }

    /// <inheritdoc cref="MaxSpanProperty"/>
    public double? MaxSpan { get => GetValue(MaxSpanProperty); set => SetValue(MaxSpanProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="SpanFormatterProperty"/>
    public Func<double, string>? SpanFormatter { get => GetValue(SpanFormatterProperty); set => SetValue(SpanFormatterProperty, value); }

    /// <inheritdoc cref="ShowSpanProperty"/>
    public bool ShowSpan { get => GetValue(ShowSpanProperty); set => SetValue(ShowSpanProperty, value); }

    /// <inheritdoc cref="TicksProperty"/>
    public IEnumerable<WindowRangeTick>? Ticks { get => GetValue(TicksProperty); set => SetValue(TicksProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IsLiveProperty"/>
    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    /// <inheritdoc cref="AccentProperty"/>
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <inheritdoc cref="ChartHeightProperty"/>
    public double ChartHeight { get => GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }

    /// <inheritdoc cref="ValueChangedEvent"/>
    public event EventHandler<WindowRangeValueChangedEventArgs>? ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <inheritdoc cref="ValueCommittedEvent"/>
    public event EventHandler<WindowRangeValueCommittedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    /// <summary>The start of the window as it is now (after defaults and limits are applied).</summary>
    public double WindowStart => _start;

    /// <summary>The end of the window as it is now (after defaults and limits are applied).</summary>
    public double WindowEnd => _end;

    #endregion

    private sealed class DragState
    {
        public IPointer? Pointer;
        public WindowRangePart Part;
        public double Grab;
        public double Raw;
        public double SpanUnits;
        public double SpanValue;
    }

    private readonly MotionValue<double> _shownStart;
    private readonly MotionValue<double> _shownEnd;
    private readonly MotionValue<double> _panPress;
    private readonly MotionValue<double> _startPress;
    private readonly MotionValue<double> _endPress;
    private readonly MotionValue<double> _morph;
    private readonly VelocityTracker _tracker = new();
    private readonly LiveLoop _pulseLoop;
    private WindowRangePlot? _plot;
    private WindowRangeReadout? _startReadout;
    private WindowRangeReadout? _spanReadout;
    private WindowRangeReadout? _endReadout;
    private WindowRangeAxis? _axis;
    private DragState? _drag;
    private WindowRangeMode _mode;
    private WindowRangePart _hover;
    private double[] _shownData = [];
    private double[] _fromData = [];
    private double[] _toData = [];
    private double _fromTop = 1;
    private double _toTop = 1;
    private double _shownTop = 1;
    private int _shapeVersion;
    private double _min;
    private double _max = 1;
    private double _grid = 1;
    private int _decimals;
    private double _minSpan = 1;
    private double _maxSpan = 1;
    private double _start;
    private double _end;
    private bool _writing;
    private int _settleVersion;
    private double _pulseTime;

    static WindowRange()
    {
        FocusableProperty.OverrideDefaultValue<WindowRange>(false);
    }

    public WindowRange()
    {
        _shownStart = CreateValue(0);
        _shownEnd = CreateValue(Units);
        _panPress = CreateValue(0);
        _startPress = CreateValue(0);
        _endPress = CreateValue(0);
        _morph = new MotionValue<double>(this, 1, MotionVectors.Double);
        _morph.Changed += OnMorphStep;
        _shownStart.Changed += OnShownChanged;
        _pulseLoop = new LiveLoop(this, StepPulse);
        _pulseLoop.RunningChanged += _ => _plot?.InvalidateVisual();
        Recalculate();
        ResolveValues(anchor: WindowRangePart.None, roll: false, raise: false);
        JumpShown();
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => _plot?.InvalidateVisual();
        return value;
    }

    #region Read by the parts

    internal double ShownStart => _shownStart.Value;

    internal double ShownEnd => _shownEnd.Value;

    internal double PanPress => Math.Max(0, _panPress.Value);

    internal double[] ShownData => _shownData;

    internal double ShownScaleTop => _shownTop;

    internal int ShapeVersion => _shapeVersion;

    internal WindowRangePart HoverPart => _hover;

    internal WindowRangePart ActivePart => _drag?.Pointer is not null ? _drag.Part : WindowRangePart.None;

    internal WindowRangeMode Mode => _mode;

    /// <summary>The speed (px/s) of the last released pan (tests).</summary>
    internal double LastReleaseSpeed { get; private set; }

    internal double EffectiveStep => _grid;

    internal double EffectiveLargeStep => LargeStep is { } large && large > 0 ? large : DefaultLargeStep();

    internal double EffectiveMinSpan => _minSpan;

    internal double EffectiveMaxSpan => _maxSpan;

    internal double DomainMinimum => _min;

    internal double DomainMaximum => _max;

    internal WindowRangePlot? Plot => _plot;

    internal WindowRangeReadout? StartReadout => _startReadout;

    internal WindowRangeReadout? SpanReadout => _spanReadout;

    internal WindowRangeReadout? EndReadout => _endReadout;

    internal WindowRangeAxis? Axis => _axis;

    /// <summary>The grip height factor of an edge (1 at rest).</summary>
    internal double GripScaleOf(WindowRangePart part)
    {
        var pan = Math.Max(0, _panPress.Value) * (PanGripScale - 1);
        var edge = (part == WindowRangePart.Start ? _startPress.Value : _endPress.Value) * (EdgeGripScale - 1);
        return 1 + pan + edge;
    }

    /// <summary>The ring around the live dot: its scale and opacity now.</summary>
    internal (double Scale, double Opacity) Pulse
    {
        get
        {
            if (!_pulseLoop.IsRunning)
            {
                return (1, 0);
            }
            var phase = _pulseTime % PulsePeriod / PulsePeriod;
            if (phase >= PulseGrow)
            {
                return (PulseScale, 0);
            }
            var eased = ArcEasings.Standard.Ease(phase / PulseGrow);
            return (1 + (PulseScale - 1) * eased, PulseStartOpacity * (1 - eased));
        }
    }

    /// <summary>The x of <paramref name="value"/> in a plot or axis <paramref name="width"/> pixels wide.</summary>
    internal double PixelOfValue(double value, double width)
    {
        var edge = _plot?.EdgeWidth ?? 12;
        return edge + UnitsOf(value) / Units * Math.Max(1, width - 2 * edge);
    }

    #endregion

    #region Domain and values

    private double DomainLength => _max - _min is var length && length > 0 ? length : 1;

    private double UnitsOf(double value) => (value - _min) / DomainLength * Units;

    private double ValueOfUnits(double units) => _min + units / Units * DomainLength;

    private double Snap(double value)
    {
        var snapped = _min + Math.Floor((value - _min) / _grid + 0.5) * _grid;
        return Math.Round(Math.Clamp(snapped, _min, _max), Math.Clamp(_decimals, 0, 15));
    }

    private double DefaultLargeStep()
    {
        var tenth = Math.Floor(DomainLength / 10 / _grid + 0.5) * _grid;
        return Math.Max(_grid, tenth);
    }

    /// <summary>Resolves the domain, the step and the span limits from the properties and the data.</summary>
    private void Recalculate()
    {
        var count = Data?.Count ?? 0;
        _grid = Step > 0 && double.IsFinite(Step) ? Step : 1;
        _min = double.IsFinite(Minimum) ? Minimum : 0;
        _max = Maximum is { } max && double.IsFinite(max) ? max : _min + Math.Max(count - 1, 0);
        if (_max <= _min)
        {
            _max = _min + _grid;
        }
        _decimals = Math.Max(DecimalsOf(_grid), DecimalsOf(_min));
        var length = _max - _min;
        _minSpan = Math.Clamp(MinSpan is { } minSpan && double.IsFinite(minSpan) ? minSpan : _grid, 0, length);
        _maxSpan = Math.Clamp(MaxSpan is { } maxSpan && double.IsFinite(maxSpan) ? maxSpan : length, _minSpan, length);
    }

    private static int DecimalsOf(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        return text.Contains('E', StringComparison.OrdinalIgnoreCase) || dot < 0 ? 0 : Math.Min(15, text.Length - dot - 1);
    }

    /// <summary>Applies the span limits to a window, keeping the end that was set (<paramref name="anchor"/>) where it is.</summary>
    private (double Start, double End) Normalize(double start, double end, WindowRangePart anchor)
    {
        start = Snap(start);
        end = Snap(end);
        if (end < start)
        {
            if (anchor == WindowRangePart.End)
            {
                start = end;
            }
            else
            {
                end = start;
            }
        }
        var span = Math.Clamp(end - start, _minSpan, _maxSpan);
        if (anchor == WindowRangePart.End)
        {
            start = end - span;
            if (start < _min)
            {
                start = _min;
                end = Math.Min(_max, start + span);
            }
        }
        else
        {
            end = start + span;
            if (end > _max)
            {
                end = _max;
                start = Math.Max(_min, end - span);
            }
        }
        return (Snap(start), Snap(end));
    }

    /// <summary>The lowest value <paramref name="part"/> can take (the window reports its start).</summary>
    internal double MinimumOf(WindowRangePart part) => part switch
    {
        WindowRangePart.Start => Math.Max(_min, _end - _maxSpan),
        WindowRangePart.End => _start + _minSpan,
        _ => _min
    };

    /// <summary>The highest value <paramref name="part"/> can take (the window reports its start).</summary>
    internal double MaximumOf(WindowRangePart part) => part switch
    {
        WindowRangePart.Start => _end - _minSpan,
        WindowRangePart.End => Math.Min(_max, _start + _maxSpan),
        _ => _max - (_end - _start)
    };

    internal double ValueOf(WindowRangePart part) => part == WindowRangePart.End ? _end : _start;

    internal string FormatValue(double value) =>
        ValueFormatter?.Invoke(value) ?? value.ToString("N" + Math.Clamp(_decimals, 0, 15), s_culture);

    internal string FormatSpan(double span) =>
        SpanFormatter?.Invoke(span) ?? span.ToString("N" + Math.Clamp(_decimals, 0, 15), s_culture);

    /// <summary>The window from the properties, with defaults and limits applied.</summary>
    private void ResolveValues(WindowRangePart anchor, bool roll, bool raise)
    {
        var quarter = Snap(_max - DomainLength / 4);
        var (start, end) = Normalize(Start ?? quarter, End ?? _max, anchor);
        SetValues(start, end, roll, raise, fromProperties: true);
    }

    /// <summary>
    /// Commits a window: writes <see cref="Start"/> and <see cref="End"/>, updates the readouts (rolling them when
    /// <paramref name="roll"/>) and the spoken values, and raises <see cref="ValueChanged"/>.
    /// </summary>
    private bool SetValues(double start, double end, bool roll, bool raise = true, bool fromProperties = false)
    {
        var oldStart = _start;
        var oldEnd = _end;
        var changed = oldStart != start || oldEnd != end;
        _start = start;
        _end = end;
        _writing = true;
        try
        {
            // Values read from the properties are not written back: the domain may not be known yet (the data comes later),
            // and a property left null keeps meaning "the default window". A drag, a key or SetRange writes both ends.
            if (!fromProperties && Start != start)
            {
                SetCurrentValue(StartProperty, start);
            }
            if (!fromProperties && End != end)
            {
                SetCurrentValue(EndProperty, end);
            }
        }
        finally
        {
            _writing = false;
        }
        UpdateTexts(oldStart, oldEnd, roll && changed);
        if (changed && raise)
        {
            RaiseEvent(new WindowRangeValueChangedEventArgs(ValueChangedEvent, oldStart, oldEnd, start, end));
        }
        return changed;
    }

    private void UpdateTexts(double oldStart, double oldEnd, bool roll)
    {
        var start = FormatValue(_start);
        var end = FormatValue(_end);
        _startReadout?.SetText(start, Math.Sign(_start - oldStart), roll);
        _endReadout?.SetText(end, Math.Sign(_end - oldEnd), roll);
        _spanReadout?.SetText(FormatSpan(_end - _start), Math.Sign(_end - _start - (oldEnd - oldStart)), roll);
        if (_plot is { } plot && plot.IsAttached)
        {
            // Spoken values (aria-valuetext).
            AutomationProperties.SetItemStatus(plot.StartHandle, start);
            AutomationProperties.SetItemStatus(plot.EndHandle, end);
            AutomationProperties.SetItemStatus(plot.WindowHandle, $"{start} to {end}");
        }
    }

    /// <summary>
    /// Sets both ends at once and glides the frame there, raising one <see cref="ValueChanged"/>. Use it for quick range
    /// buttons; setting <see cref="Start"/> and <see cref="End"/> one after the other also works.
    /// </summary>
    public void SetRange(double start, double end)
    {
        var (s, e) = Normalize(start, end, WindowRangePart.None);
        if (SetValues(s, e, roll: true))
        {
            Glide(GlideSpring);
        }
    }

    #endregion

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _plot = e.NameScope.Find<WindowRangePlot>(PlotPart);
        _startReadout = e.NameScope.Find<WindowRangeReadout>(StartReadoutPart);
        _spanReadout = e.NameScope.Find<WindowRangeReadout>(SpanReadoutPart);
        _endReadout = e.NameScope.Find<WindowRangeReadout>(EndReadoutPart);
        _axis = e.NameScope.Find<WindowRangeAxis>(AxisPart);
        _plot?.Attach(this);
        if (_axis is { } axis)
        {
            axis.Owner = this;
        }
        UpdateTicks();
        UpdateHandles();
        UpdateTexts(_start, _end, roll: false);
        UpdateSpanVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartProperty || change.Property == EndProperty)
        {
            if (_writing)
            {
                return;
            }
            var anchor = change.Property == StartProperty ? WindowRangePart.Start : WindowRangePart.End;
            var before = (_start, _end);
            ResolveValues(anchor, roll: true, raise: true);
            if (before != (_start, _end))
            {
                Glide(GlideSpring);
            }
        }
        else if (change.Property == DataProperty)
        {
            var oldCount = _toData.Length;
            Recalculate();
            ResolveValues(WindowRangePart.None, roll: false, raise: true);
            SetShape(change.GetNewValue<IReadOnlyList<double>?>(), animate: oldCount == (Data?.Count ?? 0));
            if (oldCount != (Data?.Count ?? 0))
            {
                JumpShown();
                _axis?.InvalidateVisual();
            }
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == StepProperty ||
                 change.Property == MinSpanProperty || change.Property == MaxSpanProperty)
        {
            Recalculate();
            ResolveValues(WindowRangePart.None, roll: false, raise: true);
            JumpShown();
            _axis?.InvalidateVisual();
        }
        else if (change.Property == ValueFormatterProperty || change.Property == SpanFormatterProperty)
        {
            UpdateTexts(_start, _end, roll: false);
        }
        else if (change.Property == ShowSpanProperty)
        {
            UpdateSpanVisibility();
        }
        else if (change.Property == TicksProperty)
        {
            UpdateTicks();
        }
        else if (change.Property == LabelProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            UpdateHandles();
        }
        else if (change.Property == IsLiveProperty)
        {
            UpdatePulse();
            _plot?.InvalidateVisual();
        }
        else if (change.Property == AccentProperty)
        {
            _plot?.InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePulse();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pulseLoop.Stop();
    }

    private void UpdatePulse()
    {
        if (IsLive)
        {
            _pulseLoop.Start();
        }
        else
        {
            _pulseLoop.Stop();
        }
    }

    private bool StepPulse(double seconds)
    {
        _pulseTime = (_pulseTime + seconds) % (PulsePeriod * 1000);
        _plot?.InvalidateVisual();
        return true;
    }

    private void UpdateSpanVisibility()
    {
        if (_spanReadout is { } span)
        {
            span.IsVisible = ShowSpan;
        }
    }

    private void UpdateTicks()
    {
        if (_axis is not { } axis)
        {
            return;
        }
        var ticks = Ticks?.ToArray() ?? [];
        axis.SetTicks(ticks);
        axis.IsVisible = ticks.Length > 0;
    }

    private void UpdateHandles()
    {
        if (_plot is not { IsAttached: true } plot)
        {
            return;
        }
        var label = Label;
        AutomationProperties.SetName(plot.StartHandle, $"{label}, start");
        AutomationProperties.SetName(plot.WindowHandle, $"{label}, window");
        AutomationProperties.SetName(plot.EndHandle, $"{label}, end");
        foreach (var handle in plot.Handles)
        {
            handle.IsTabStop = IsEffectivelyEnabled;
            handle.Focusable = IsEffectivelyEnabled;
        }
        UpdateTexts(_start, _end, roll: false);
    }

    /// <summary>Takes a new series: morphs the drawn one to it when it has as many points, otherwise replaces it.</summary>
    private void SetShape(IReadOnlyList<double>? data, bool animate)
    {
        var next = data?.ToArray() ?? [];
        var top = WindowRangeChart.TopOf(next);
        if (animate && next.Length == _shownData.Length && next.Length > 0 && IsLoaded && ArcMotion.IsFull(this))
        {
            _fromData = (double[])_shownData.Clone();
            _toData = next;
            _fromTop = _shownTop;
            _toTop = top;
            _morph.Jump(0);
            _ = _morph.AnimateTo(1, MotionSpec.Spring(MorphSpring));
            return;
        }
        _morph.Jump(1);
        _toData = next;
        _fromData = next;
        _shownData = (double[])next.Clone();
        _toTop = _fromTop = _shownTop = top;
        _shapeVersion++;
        _plot?.InvalidateVisual();
    }

    private void OnMorphStep(double progress)
    {
        if (_toData.Length != _shownData.Length || _fromData.Length != _toData.Length)
        {
            return;
        }
        for (var i = 0; i < _shownData.Length; i++)
        {
            _shownData[i] = _fromData[i] + (_toData[i] - _fromData[i]) * progress;
        }
        _shownTop = _fromTop + (_toTop - _fromTop) * progress;
        _shapeVersion++;
        _plot?.InvalidateVisual();
    }

    #endregion

    #region Frame motion

    private bool CanAnimate => IsLoaded && ArcMotion.IsFull(this);

    private void JumpShown()
    {
        _shownStart.Jump(UnitsOf(_start));
        _shownEnd.Jump(UnitsOf(_end));
    }

    /// <summary>Moves the frame to the committed window on <paramref name="spring"/>, from where it is on screen.</summary>
    private Task<bool> Glide(SpringSpec spring)
    {
        _mode = WindowRangeMode.Idle;
        _settleVersion++;
        if (!IsLoaded)
        {
            JumpShown();
            return s_settled;
        }
        var spec = MotionSpec.Spring(spring);
        return Both(_shownStart.AnimateTo(UnitsOf(_start), spec), _shownEnd.AnimateTo(UnitsOf(_end), spec));
    }

    private static readonly Task<bool> s_settled = Task.FromResult(true);

    private static async Task<bool> Both(Task<bool> a, Task<bool> b)
    {
        var first = await a;
        var second = await b;
        return first && second;
    }

    /// <summary>Raises <see cref="ValueCommitted"/> once <paramref name="settled"/> finishes, unless a newer motion took over.</summary>
    private async void CommitWhenSettled(Task<bool> settled)
    {
        var version = _settleVersion;
        var done = await settled;
        if (done && version == _settleVersion)
        {
            if (_mode == WindowRangeMode.Coast)
            {
                _mode = WindowRangeMode.Idle;
            }
            RaiseEvent(new WindowRangeValueCommittedEventArgs(ValueCommittedEvent, _start, _end));
        }
    }

    /// <summary>While the window coasts, the values follow the frame so the readouts never trail it.</summary>
    private void OnShownChanged(double units)
    {
        if (_mode != WindowRangeMode.Coast || _drag is not { } drag)
        {
            return;
        }
        var span = drag.SpanValue;
        var start = Math.Clamp(Snap(ValueOfUnits(units)), _min, _max - span);
        SetValues(start, start + span, roll: false);
    }

    private void Nudge(WindowRangePart part, int direction)
    {
        if (direction == 0 || !CanAnimate || _plot is not { } plot)
        {
            return;
        }
        var speed = direction * NudgeSpeed / Math.Max(1e-6, plot.PixelsPerUnit);
        var spec = MotionSpec.Spring(SnapSpring);
        if (part != WindowRangePart.End)
        {
            _ = _shownStart.AnimateTo(UnitsOf(_start), spec, speed);
        }
        if (part != WindowRangePart.Start)
        {
            _ = _shownEnd.AnimateTo(UnitsOf(_end), spec, speed);
        }
    }

    private void SetPress(WindowRangePart part, bool on)
    {
        var value = part switch
        {
            WindowRangePart.Start => _startPress,
            WindowRangePart.End => _endPress,
            _ => _panPress
        };
        var target = on ? 1.0 : 0.0;
        if (!CanAnimate)
        {
            value.Jump(target);
            return;
        }
        if (value.Target != target)
        {
            _ = value.AnimateTo(target, MotionSpec.Spring(PressSpring));
        }
    }

    internal void SetHover(WindowRangePart part)
    {
        if (_hover != part)
        {
            _hover = part;
            _plot?.InvalidateVisual();
        }
    }

    #endregion

    #region Pointer

    internal void OnPlotPressed(WindowRangePlot plot, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.Pointer.IsPrimary || !e.GetCurrentPoint(plot).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var x = e.GetPosition(plot).X;
        var part = plot.PartAt(x);
        e.Pointer.Capture(plot);
        e.Handled = true;
        _settleVersion++;
        var drag = _drag ??= new DragState();
        drag.Pointer = e.Pointer;
        drag.Part = part;
        drag.SpanValue = _end - _start;
        drag.SpanUnits = UnitsOf(_end) - UnitsOf(_start);
        _tracker.Reset();
        _tracker.AddSample(x, Time(e.Timestamp));
        // Grabbing a moving frame stops it where it is.
        var shownStart = _shownStart.Value;
        var shownEnd = _shownEnd.Value;
        _shownStart.Jump(shownStart);
        _shownEnd.Jump(part is WindowRangePart.Window or WindowRangePart.None ? shownStart + drag.SpanUnits : shownEnd);
        switch (part)
        {
            case WindowRangePart.Start:
                drag.Grab = x - plot.XOf(shownStart);
                drag.Raw = shownStart;
                _mode = WindowRangeMode.Start;
                break;
            case WindowRangePart.End:
                drag.Grab = x - plot.XOf(shownEnd);
                drag.Raw = shownEnd;
                _mode = WindowRangeMode.End;
                break;
            case WindowRangePart.Window:
                drag.Grab = x - plot.XOf(shownStart);
                drag.Raw = shownStart;
                _mode = WindowRangeMode.Pan;
                break;
            default:
                _mode = WindowRangeMode.Glide;
                GlideToward(plot, x);
                break;
        }
        SetPress(part == WindowRangePart.None ? WindowRangePart.Window : part, true);
        var handle = plot.HandleOf(part);
        if (!handle.IsFocused)
        {
            handle.Focus(NavigationMethod.Pointer);
        }
        SetHover(part is WindowRangePart.Start or WindowRangePart.End ? part : WindowRangePart.None);
        PseudoClasses.Set(DraggingPseudoClass, true);
    }

    internal void OnPlotMoved(WindowRangePlot plot, PointerEventArgs e)
    {
        var x = e.GetPosition(plot).X;
        var drag = _drag;
        if (drag?.Pointer is null || e.Pointer != drag.Pointer)
        {
            if (IsEffectivelyEnabled && e.Pointer.Type != PointerType.Touch)
            {
                var part = plot.PartAt(x);
                SetHover(part is WindowRangePart.Start or WindowRangePart.End ? part : WindowRangePart.None);
            }
            return;
        }
        _tracker.AddSample(x, Time(e.Timestamp));
        switch (_mode)
        {
            case WindowRangeMode.Pan:
                FollowPan(plot, x);
                break;
            case WindowRangeMode.Glide:
                GlideToward(plot, x);
                break;
            case WindowRangeMode.Start:
            case WindowRangeMode.End:
                FollowEdge(plot, x);
                break;
        }
    }

    internal void OnPlotReleased(WindowRangePlot plot, PointerReleasedEventArgs e)
    {
        if (_drag?.Pointer is null || e.Pointer != _drag.Pointer)
        {
            return;
        }
        Release(plot, Time(e.Timestamp), release: true);
        e.Pointer.Capture(null);
    }

    internal void OnPlotCaptureLost()
    {
        if (_drag?.Pointer is not null && _plot is { } plot)
        {
            Release(plot, TimeSpan.Zero, release: false);
        }
    }

    private static TimeSpan Time(ulong timestamp) => TimeSpan.FromMilliseconds(timestamp);

    /// <summary>A press outside the window: the window glides to center on the pointer and keeps following it.</summary>
    private void GlideToward(WindowRangePlot plot, double x)
    {
        var drag = _drag!;
        var span = drag.SpanUnits;
        var target = Math.Clamp(plot.UnitsOf(x) - span / 2, 0, Units - span);
        drag.Raw = target;
        if (CanAnimate)
        {
            var spec = MotionSpec.Spring(SnapSpring);
            _ = _shownStart.AnimateTo(target, spec);
            _ = _shownEnd.AnimateTo(target + span, spec);
        }
        else
        {
            _shownStart.Jump(target);
            _shownEnd.Jump(target + span);
        }
        var start = Math.Clamp(Snap(ValueOfUnits(target)), _min, _max - drag.SpanValue);
        SetValues(start, start + drag.SpanValue, roll: false);
    }

    /// <summary>The window follows the pointer 1:1 and stretches past either end of the domain.</summary>
    private void FollowPan(WindowRangePlot plot, double x)
    {
        var drag = _drag!;
        var span = drag.SpanUnits;
        var raw = plot.UnitsOf(x - drag.Grab);
        var clamped = Math.Clamp(raw, 0, Math.Max(0, Units - span));
        var shown = clamped + Stretch(plot, raw - clamped, PanStretch);
        drag.Raw = raw;
        _shownStart.Jump(shown);
        _shownEnd.Jump(shown + span);
        var start = Math.Clamp(Snap(ValueOfUnits(clamped)), _min, _max - drag.SpanValue);
        SetValues(start, start + drag.SpanValue, roll: false);
    }

    /// <summary>An edge follows the pointer 1:1 within its limits and stretches past them.</summary>
    private void FollowEdge(WindowRangePlot plot, double x)
    {
        var drag = _drag!;
        var part = drag.Part;
        var raw = plot.UnitsOf(x - drag.Grab);
        var low = UnitsOf(MinimumOf(part));
        var high = UnitsOf(MaximumOf(part));
        var clamped = Math.Clamp(raw, Math.Min(low, high), Math.Max(low, high));
        var shown = clamped + Stretch(plot, raw - clamped, EdgeStretch);
        drag.Raw = raw;
        var value = Math.Clamp(Snap(ValueOfUnits(clamped)), MinimumOf(part), MaximumOf(part));
        if (part == WindowRangePart.Start)
        {
            _shownStart.Jump(shown);
            SetValues(value, _end, roll: false);
        }
        else
        {
            _shownEnd.Jump(shown);
            SetValues(_start, value, roll: false);
        }
    }

    /// <summary>Resistance past a limit in units (iOS curve, at most <paramref name="limit"/> pixels); none without full motion.</summary>
    private double Stretch(WindowRangePlot plot, double overUnits, double limit)
    {
        if (overUnits == 0 || !CanAnimate)
        {
            return 0;
        }
        var ppu = Math.Max(1e-6, plot.PixelsPerUnit);
        return RubberBand.Apply(overUnits * ppu, limit) / ppu;
    }

    private void Release(WindowRangePlot plot, TimeSpan time, bool release)
    {
        var drag = _drag!;
        var part = drag.Part;
        var mode = _mode;
        drag.Pointer = null;
        SetPress(WindowRangePart.Window, false);
        SetPress(WindowRangePart.Start, false);
        SetPress(WindowRangePart.End, false);
        PseudoClasses.Set(DraggingPseudoClass, false);
        var ppu = Math.Max(1e-6, plot.PixelsPerUnit);
        switch (mode)
        {
            case WindowRangeMode.Pan:
            {
                // The flick's speed decides where the window lands; the coast starts at that speed.
                var velocity = release && CanAnimate ? _tracker.GetVelocity(time).X / ppu : 0;
                LastReleaseSpeed = velocity * ppu;
                var landing = drag.Raw + velocity * FlickHorizon;
                var start = Math.Clamp(Snap(ValueOfUnits(landing)), _min, _max - drag.SpanValue);
                Coast(start, drag.SpanValue, velocity);
                break;
            }
            case WindowRangeMode.Glide:
            {
                // The glide hands its own speed to the coast and lands where it was heading.
                var start = Math.Clamp(Snap(ValueOfUnits(_shownStart.Target)), _min, _max - drag.SpanValue);
                Coast(start, drag.SpanValue, null);
                break;
            }
            case WindowRangeMode.Start:
            case WindowRangeMode.End:
            {
                _mode = WindowRangeMode.Idle;
                var spec = MotionSpec.Spring(SnapSpring);
                _settleVersion++;
                Task<bool> settled;
                if (CanAnimate)
                {
                    settled = part == WindowRangePart.Start
                        ? _shownStart.AnimateTo(UnitsOf(_start), spec, 0)
                        : _shownEnd.AnimateTo(UnitsOf(_end), spec, 0);
                }
                else
                {
                    JumpShown();
                    settled = s_settled;
                }
                CommitWhenSettled(settled);
                break;
            }
        }
        drag.Part = WindowRangePart.None;
    }

    private void Coast(double start, double span, double? velocity)
    {
        _settleVersion++;
        if (!CanAnimate)
        {
            _mode = WindowRangeMode.Idle;
            SetValues(start, start + span, roll: false);
            JumpShown();
            CommitWhenSettled(s_settled);
            return;
        }
        _mode = WindowRangeMode.Coast;
        var spec = MotionSpec.Spring(GlideSpring);
        var target = UnitsOf(start);
        var moving = Both(_shownStart.AnimateTo(target, spec, velocity),
            _shownEnd.AnimateTo(target + UnitsOf(_min + span), spec, velocity));
        CommitWhenSettled(Land(moving, start, span));
    }

    /// <summary>The coast's values follow the frame; at rest they are the landing window exactly.</summary>
    private async Task<bool> Land(Task<bool> moving, double start, double span)
    {
        var done = await moving;
        if (done)
        {
            SetValues(start, start + span, roll: false);
        }
        return done;
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not WindowRangeHandle handle || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        var large = EffectiveLargeStep;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var part = handle.Part;
        var now = ValueOf(part);
        double wanted;
        switch (e.Key)
        {
            case Key.Right or Key.Up:
                wanted = now + (shift ? large : _grid);
                break;
            case Key.Left or Key.Down:
                wanted = now - (shift ? large : _grid);
                break;
            case Key.PageUp:
                wanted = now + large;
                break;
            case Key.PageDown:
                wanted = now - large;
                break;
            case Key.Home:
                wanted = MinimumOf(part);
                break;
            case Key.End:
                wanted = MaximumOf(part);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
        handle.ShowRing = true;
        SetPartValue(part, wanted, fromKeyboard: true);
    }

    /// <summary>Moves one part to <paramref name="wanted"/> within its limits; at a limit a key nudges the frame instead.</summary>
    internal void SetPartValue(WindowRangePart part, double wanted, bool fromKeyboard)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        var now = ValueOf(part);
        var next = Math.Clamp(Snap(wanted), MinimumOf(part), MaximumOf(part));
        if (next == now)
        {
            Nudge(part, Math.Sign(wanted - now));
            return;
        }
        var span = _end - _start;
        var (start, end) = part switch
        {
            WindowRangePart.Start => (next, _end),
            WindowRangePart.End => (_start, next),
            _ => (next, next + span)
        };
        SetValues(start, end, roll: true);
        var settled = Glide(GlideSpring);
        if (fromKeyboard)
        {
            CommitWhenSettled(settled);
        }
    }

    #endregion

    #region Automation

    /// <summary>The control is a group named by <see cref="Label"/>; the start edge, the window and the end edge are sliders.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer : ControlAutomationPeer
    {
        public GroupPeer(WindowRange owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() ?? ((WindowRange)Owner).Label;
    }

    #endregion
}
