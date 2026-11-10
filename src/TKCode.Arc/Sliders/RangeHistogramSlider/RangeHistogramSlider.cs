// Ported from Arc Range histogram slider (MIT, Copyright (c) 2026 Elia Kuratli): the value model, the drag, release and
// keyboard rules, and every motion constant.
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Arc range histogram slider: two thumbs over a histogram of the values they filter. Bars inside the range light up, cut
/// exactly at each thumb. The thumbs follow the pointer 1:1, never cross, rubber-band up to 10px at the ends and settle on
/// a nearby bar edge with the velocity of the release. A press anywhere on the chart or the track springs the nearest thumb
/// there. Arrows step, Shift+arrows and PageUp / PageDown move to the next bar edge, and Home / End go to a thumb's limit.
/// </summary>
[TemplatePart(RailPart, typeof(Control))]
[PseudoClasses(DraggingPseudoClass)]
public class RangeHistogramSlider : TemplatedControl, ISliderRailOwner, ISliderThumbHost
{
    public const string RailPart = "PART_Rail";
    internal const string ReadoutLowPart = "PART_ReadoutLow";
    internal const string ReadoutHighPart = "PART_ReadoutHigh";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Pixels of rubber-band travel past a limit.</summary>
    internal const double Stretch = 10;

    /// <summary>Pixel reach of a bar edge on release.</summary>
    internal const double BinSnapPixels = 9;

    /// <summary>Speed (px/s) a key press at a limit strains with.</summary>
    internal const double BumpSpeed = 150;

    /// <summary>Extra hit radius around a thumb (Arc's 11px <c>::after</c>).</summary>
    internal const double ThumbReach = 11;

    /// <summary>Thumb scale while it is held.</summary>
    internal const double LiftScale = 1.18;

    /// <summary>The guide starts at this height and rises to its full height while it fades in.</summary>
    internal const double GuideRestScale = 0.6;

    /// <summary>The shortest visible bar, as a share of the chart height.</summary>
    internal const double MinBarShare = 0.04;

    /// <summary>The move a stacked thumb waits for before it knows which way the pointer means.</summary>
    private const double StackedPickDistance = 2;

    private const double EdgeEpsilon = 1e-6;

    private static readonly SpringSpec s_thumbSpring = ArcMotionPresets.Snappy;
    private static readonly SpringSpec s_kickSpring = ArcMotionPresets.Morph;
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The visible label; the thumbs are named from it unless <see cref="LowerThumbName"/> or <see cref="UpperThumbName"/> say otherwise.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, string?>(nameof(Label));

    /// <summary>The raw values the histogram counts, such as every listing's price. Assign a new list to redraw the bars.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> DataProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, IReadOnlyList<double>?>(nameof(Data));

    /// <summary>The lower end of the range; null means the smallest value on the scale.</summary>
    public static readonly StyledProperty<double?> LowerValueProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, double?>(nameof(LowerValue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The upper end of the range; null means the largest value on the scale.</summary>
    public static readonly StyledProperty<double?> UpperValueProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, double?>(nameof(UpperValue), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The start of the scale; null takes it from the data, rounded down and never above 0.</summary>
    public static readonly StyledProperty<double?> MinimumProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, double?>(nameof(Minimum));

    /// <summary>The end of the scale; null takes it from the data, rounded up.</summary>
    public static readonly StyledProperty<double?> MaximumProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, double?>(nameof(Maximum));

    /// <summary>The grid the values rest on and the distance of one arrow key press.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, double>(nameof(Step), 1);

    /// <summary>How many bars the histogram draws.</summary>
    public static readonly StyledProperty<int> BinsProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, int>(nameof(Bins), 32);

    /// <summary>Lets a released thumb that lands within 9px of a bar edge settle on that edge, so a range does not cut a bar in half.</summary>
    public static readonly StyledProperty<bool> SnapToBinsProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, bool>(nameof(SnapToBins), true);

    /// <summary>The closest the two thumbs may sit, in steps.</summary>
    public static readonly StyledProperty<int> MinStepsBetweenThumbsProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, int>(nameof(MinStepsBetweenThumbs), 1);

    /// <summary>Formats the readout and the spoken values (default: en-US with the decimals of the step or the minimum).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The accessible name of the lower thumb; when null it is the label with ", minimum" appended.</summary>
    public static readonly StyledProperty<string?> LowerThumbNameProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, string?>(nameof(LowerThumbName));

    /// <summary>The accessible name of the upper thumb; when null it is the label with ", maximum" appended.</summary>
    public static readonly StyledProperty<string?> UpperThumbNameProperty =
        AvaloniaProperty.Register<RangeHistogramSlider, string?>(nameof(UpperThumbName));

    /// <summary>Raised on every change of the range, from a drag, a key or code.</summary>
    public static readonly RoutedEvent<SliderRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<RangeHistogramSlider, SliderRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once a drag is released or a key changes the range.</summary>
    public static readonly RoutedEvent<SliderValueCommittedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<RangeHistogramSlider, SliderValueCommittedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="RangeChangedEvent"/>
    public event EventHandler<SliderRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    /// <inheritdoc cref="ValueCommittedEvent"/>
    public event EventHandler<SliderValueCommittedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DataProperty"/>
    public IReadOnlyList<double>? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <inheritdoc cref="LowerValueProperty"/>
    public double? LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }

    /// <inheritdoc cref="UpperValueProperty"/>
    public double? UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }

    /// <inheritdoc cref="MinimumProperty"/>
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <inheritdoc cref="BinsProperty"/>
    public int Bins { get => GetValue(BinsProperty); set => SetValue(BinsProperty, value); }

    /// <inheritdoc cref="SnapToBinsProperty"/>
    public bool SnapToBins { get => GetValue(SnapToBinsProperty); set => SetValue(SnapToBinsProperty, value); }

    /// <inheritdoc cref="MinStepsBetweenThumbsProperty"/>
    public int MinStepsBetweenThumbs { get => GetValue(MinStepsBetweenThumbsProperty); set => SetValue(MinStepsBetweenThumbsProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="LowerThumbNameProperty"/>
    public string? LowerThumbName { get => GetValue(LowerThumbNameProperty); set => SetValue(LowerThumbNameProperty, value); }

    /// <inheritdoc cref="UpperThumbNameProperty"/>
    public string? UpperThumbName { get => GetValue(UpperThumbNameProperty); set => SetValue(UpperThumbNameProperty, value); }

    /// <summary>
    /// One thumb's motion. It runs in code because the bars, the fill and the thumbs must cut at the same position on the
    /// same frame, and a press and a release hand their speed to a spring: a ControlTheme transition can do neither.
    /// </summary>
    private sealed class ThumbMotion
    {
        public MotionValue<double>? Position;
        public MotionValue<double>? Lag;
        public MotionValue<double>? Lift;
        // Built the first time the thumb is held.
        public MotionValue<double>? GuideOpacity;
        public MotionValue<double>? GuideScale;
        public double Goal = double.NaN;
        public double Shown => (Position?.Value ?? 0) + (Lag?.Value ?? 0);
    }

    private sealed class DragState
    {
        public IPointer? Pointer;
        public int Index = -1;
        public double Grab;
        public double StartX;
        public double Raw;
    }

    private readonly ThumbMotion[] _thumbs = [new ThumbMotion(), new ThumbMotion()];
    private readonly double[] _values = new double[2];
    private readonly Action<double> _onMoved;
    private SliderVelocityTracker? _tracker;
    private DragState? _drag;
    private RangeHistogramRail? _rail;
    private TextBlock? _readoutLow;
    private TextBlock? _readoutHigh;
    private int[] _counts = [];
    private int _tallest = 1;
    private double _min;
    private double _max = 1;
    private int _decimals;
    private string _format = "N0";
    private int _lastActive = 1;
    private bool _dragActive;
    private bool _textsPosted;
    private Action? _flushTexts;

    static RangeHistogramSlider()
    {
        FocusableProperty.OverrideDefaultValue<RangeHistogramSlider>(false);
    }

    public RangeHistogramSlider()
    {
        _onMoved = OnMoved;
        Recalculate();
    }

    #region Values

    private double Grid => Step > 0 ? Step : 1;

    private double Span => _max - _min is var span && span > 0 ? span : 1;

    private int BinCount => Math.Max(1, Bins);

    private double BinSpan => Span / BinCount;

    private double Gap => Math.Max(0, MinStepsBetweenThumbs) * Grid;

    internal IReadOnlyList<int> Counts => _counts;

    internal int Tallest => _tallest;

    internal double ScaleMinimum => _min;

    internal double ScaleMaximum => _max;

    internal double ToPosition(double value) => (value - _min) / Span * SliderMath.Span;

    private double ValueAt(double position) => _min + position / SliderMath.Span * Span;

    private double LowOf(int index) => index == 1 ? _values[0] + Gap : _min;

    private double HighOf(int index) => index == 0 ? _values[1] - Gap : _max;

    private double Settle(int index, double input)
    {
        var snapped = SliderMath.Snap(Math.Clamp(input, _min, _max), _min, Grid, _decimals);
        var low = LowOf(index);
        var high = HighOf(index);
        return high < low ? low : Math.Clamp(snapped, low, high);
    }

    private string FormatValue(double value) => ValueFormatter?.Invoke(value) ?? value.ToString(_format, s_culture);

    /// <summary>Counts <paramref name="data"/> into equal bars between the limits. The last bar keeps the maximum itself.</summary>
    internal static int[] Histogram(IReadOnlyList<double>? data, double min, double max, int bins)
    {
        var counts = new int[bins];
        if (data is null)
        {
            return counts;
        }
        var width = (max - min) / bins is var share && share != 0 ? share : 1;
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            if (item >= min && item <= max)
            {
                counts[Math.Min(bins - 1, (int)Math.Floor((item - min) / width))]++;
            }
        }
        return counts;
    }

    /// <summary>Resolves the scale from the properties and the data, and counts the bars.</summary>
    private void Recalculate()
    {
        var data = Data;
        var low = 0.0;
        var high = double.NegativeInfinity;
        if (data is not null)
        {
            for (var i = 0; i < data.Count; i++)
            {
                low = Math.Min(low, data[i]);
                high = Math.Max(high, data[i]);
            }
        }
        _min = Minimum ?? Math.Floor(low);
        _max = Maximum ?? Math.Ceiling(Math.Max(high, _min + Grid));
        _decimals = Math.Clamp(Math.Max(SliderMath.DecimalsOf(Grid), SliderMath.DecimalsOf(_min)), 0, 15);
        _format = "N" + _decimals.ToString(CultureInfo.InvariantCulture);
        _counts = Histogram(data, _min, _max, BinCount);
        _tallest = 1;
        foreach (var count in _counts)
        {
            _tallest = Math.Max(_tallest, count);
        }
        ReadValues();
    }

    private void ReadValues()
    {
        var hi = Math.Max(_min, _max);
        var low = Math.Clamp(LowerValue ?? _min, _min, hi);
        var high = Math.Clamp(UpperValue ?? hi, _min, hi);
        _values[0] = Math.Min(low, high);
        _values[1] = Math.Max(low, high);
    }

    /// <summary>The lit part of the scale between the thumbs (track positions, clamped).</summary>
    internal (double From, double To) LitSpan()
    {
        var from = Math.Clamp(_thumbs[0].Shown, 0, SliderMath.Span);
        var to = Math.Clamp(_thumbs[1].Shown, 0, SliderMath.Span);
        return (from, Math.Max(from, to));
    }

    /// <summary>The guide of thumb <paramref name="index"/>: where it stands, how visible it is and how far it has risen.</summary>
    internal (double Position, double Opacity, double Scale) GuideOf(int index)
    {
        var thumb = _thumbs[index];
        return (thumb.Shown, thumb.GuideOpacity?.Value ?? 0, thumb.GuideScale?.Value ?? GuideRestScale);
    }

    private void Commit(int index, double next)
    {
        if (_values[index] == next)
        {
            return;
        }
        _values[index] = next;
        SetCurrentValue(index == 0 ? LowerValueProperty : UpperValueProperty, next);
    }

    private void RaiseCommitted()
        => RaiseEvent(new SliderValueCommittedEventArgs(ValueCommittedEvent, _values[0], _values[0], _values[1], true));

    private bool CanAnimate => IsLoaded && ArcMotion.IsFull(this);

    /// <summary>Values that arrive from outside a drag spring the thumb to their place from where it is on screen.</summary>
    private void SyncThumbs(bool animate)
    {
        ReadValues();
        for (var i = 0; i < 2; i++)
        {
            var thumb = _thumbs[i];
            if (thumb.Position is not { } position || thumb.Lag is not { } lag)
            {
                continue;
            }
            var target = ToPosition(_values[i]);
            if ((_dragActive && _drag!.Index == i) || thumb.Goal == target)
            {
                continue;
            }
            var wasPlaced = !double.IsNaN(thumb.Goal);
            thumb.Goal = target;
            var from = thumb.Shown;
            lag.Jump(0);
            if (!animate || !wasPlaced || !IsLoaded)
            {
                position.Jump(target);
                continue;
            }
            position.Jump(from);
            _ = position.AnimateTo(target, MotionSpec.Spring(s_thumbSpring));
        }
        if (_dragActive)
        {
            // Pointer events arrive far above the frame rate; the value itself is already committed.
            RequestTextUpdate();
        }
        else
        {
            UpdateTexts();
        }
        _rail?.InvalidateVisual();
    }

    private void RequestTextUpdate()
    {
        if (_textsPosted)
        {
            return;
        }
        _textsPosted = true;
        _flushTexts ??= () =>
        {
            _textsPosted = false;
            UpdateTexts();
        };
        Dispatcher.UIThread.Post(_flushTexts, DispatcherPriority.Render);
    }

    private void UpdateTexts()
    {
        var low = FormatValue(_values[0]);
        var high = FormatValue(_values[1]);
        if (_readoutLow is not null)
        {
            _readoutLow.Text = low;
        }
        if (_readoutHigh is not null)
        {
            _readoutHigh.Text = high;
        }
        if (_rail is { } rail)
        {
            // Spoken value text (aria-valuetext).
            AutomationProperties.SetItemStatus(rail.Thumbs[0], low);
            AutomationProperties.SetItemStatus(rail.Thumbs[1], high);
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rail = e.NameScope.Find<RangeHistogramRail>(RailPart);
        _readoutLow = e.NameScope.Find<TextBlock>(ReadoutLowPart);
        _readoutHigh = e.NameScope.Find<TextBlock>(ReadoutHighPart);
        if (_rail is { } rail)
        {
            EnsureMotion(_thumbs[0]);
            EnsureMotion(_thumbs[1]);
            rail.Slider = this;
            // This slider has no value bubble: the readout above the chart carries the numbers.
            rail.Setup(this, this, 2, _ => new Control(), _ => { });
            rail.InvalidateBars();
        }
        foreach (var thumb in _thumbs)
        {
            thumb.Goal = double.NaN;
        }
        SyncThumbs(animate: false);
        UpdateThumbs();
    }

    private void EnsureMotion(ThumbMotion thumb)
    {
        if (thumb.Position is not null)
        {
            return;
        }
        thumb.Position = CreateValue(0);
        thumb.Lag = CreateValue(0);
        thumb.Lift = CreateValue(1);
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _onMoved;
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LowerValueProperty || change.Property == UpperValueProperty)
        {
            SyncThumbs(animate: true);
            RaiseEvent(new SliderRangeChangedEventArgs(RangeChangedEvent, _values[0], _values[1]));
        }
        else if (change.Property == DataProperty || change.Property == MinimumProperty || change.Property == MaximumProperty ||
                 change.Property == StepProperty || change.Property == BinsProperty || change.Property == MinStepsBetweenThumbsProperty)
        {
            Recalculate();
            foreach (var thumb in _thumbs)
            {
                thumb.Goal = double.NaN;
            }
            SyncThumbs(animate: false);
            _rail?.InvalidateBars();
        }
        else if (change.Property == ValueFormatterProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == LabelProperty || change.Property == LowerThumbNameProperty ||
                 change.Property == UpperThumbNameProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            UpdateThumbs();
        }
    }

    private void UpdateThumbs()
    {
        if (_rail is not { } rail)
        {
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            var thumb = rail.Thumbs[i];
            thumb.IsTabStop = IsEffectivelyEnabled;
            thumb.ZIndex = i == _lastActive ? 2 : 1;
            AutomationProperties.SetName(thumb, i == 0 ? LowerThumbName ?? $"{Label}, minimum" : UpperThumbName ?? $"{Label}, maximum");
        }
        PlaceVisuals();
    }

    #endregion

    #region Placement

    void ISliderRailOwner.OnRailArranged(SliderRailBase rail) => PlaceVisuals();

    private void OnMoved(double _)
    {
        PlaceVisuals();
        _rail?.InvalidateVisual();
    }

    private void PlaceVisuals()
    {
        if (_rail is not { } rail || rail.Thumbs.Count < 2)
        {
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            var motion = _thumbs[i];
            rail.Thumbs[i].Place(motion.Shown / SliderMath.Span * rail.TrackWidth, motion.Lift?.Value ?? 1);
        }
    }

    private void SetLifted(int index)
    {
        for (var i = 0; i < 2; i++)
        {
            var on = i == index;
            var thumb = _thumbs[i];
            var target = on ? LiftScale : 1.0;
            if (thumb.Lift is { } lift && lift.Target != target)
            {
                _ = lift.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
            }
            SetGuide(thumb, on);
        }
    }

    /// <summary>While a thumb is held, a hairline rises from it through the chart to show exactly where the range cuts.</summary>
    private void SetGuide(ThumbMotion thumb, bool on)
    {
        if (thumb.GuideOpacity is null)
        {
            if (!on)
            {
                return;
            }
            thumb.GuideOpacity = CreateValue(0);
            thumb.GuideScale = CreateValue(GuideRestScale);
        }
        var opacity = on ? 1.0 : 0.0;
        if (!CanAnimate)
        {
            thumb.GuideOpacity.Jump(opacity);
            thumb.GuideScale!.Jump(1);
            return;
        }
        if (thumb.GuideOpacity.Target != opacity)
        {
            _ = thumb.GuideOpacity.AnimateTo(opacity, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            _ = thumb.GuideScale!.AnimateTo(on ? 1 : GuideRestScale, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
    }

    #endregion

    #region Pointer

    private int Nearest(double position)
    {
        var low = Math.Abs(position - ToPosition(_values[0]));
        var high = Math.Abs(position - ToPosition(_values[1]));
        if (low == high)
        {
            return position < ToPosition(_values[0]) ? 0 : 1;
        }
        return low < high ? 0 : 1;
    }

    private bool IsOverThumb(RangeHistogramRail rail, int index, Point point)
    {
        var reach = rail.ThumbDiameter / 2 + ThumbReach;
        var dx = point.X - rail.ToPixel(_thumbs[index].Shown);
        var dy = point.Y - rail.TrackCenterY;
        return dx * dx + dy * dy <= reach * reach;
    }

    /// <summary>The thumb under the point (the one on top first), or -1; a press on the chart or the track grabs neither.</summary>
    private int GrabbedThumb(RangeHistogramRail rail, Point point)
    {
        var other = 1 - _lastActive;
        return IsOverThumb(rail, _lastActive, point) ? _lastActive : IsOverThumb(rail, other, point) ? other : -1;
    }

    private void FocusFromPointer(int index)
    {
        if (_rail?.Thumbs[index] is { IsFocused: false } thumb)
        {
            thumb.Focus(NavigationMethod.Pointer);
        }
    }

    void ISliderRailOwner.OnRailPressed(SliderRailBase rail, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.Pointer.IsPrimary || !e.GetCurrentPoint(rail).Properties.IsLeftButtonPressed ||
            rail is not RangeHistogramRail histogram)
        {
            return;
        }
        var point = e.GetPosition(rail);
        var at = rail.ToPosition(point.X);
        var grabbed = GrabbedThumb(histogram, point);
        e.Pointer.Capture(rail);
        e.Handled = true;
        _dragActive = true;
        var drag = _drag ??= new DragState();
        drag.Pointer = e.Pointer;
        drag.Index = -1;
        drag.Grab = 0;
        drag.StartX = point.X;
        drag.Raw = at;
        var tracker = _tracker ??= new SliderVelocityTracker();
        tracker.Reset();
        if (grabbed >= 0 && _values[1] - _values[0] <= Gap)
        {
            // Stacked thumbs wait for the first movement to decide which one the pointer means.
            FocusFromPointer(grabbed);
            return;
        }
        var index = grabbed >= 0 ? grabbed : Nearest(at);
        Begin(rail, index, at, press: grabbed < 0, e.Timestamp);
        tracker.Add(e.Timestamp, _thumbs[index].Position!.Value);
    }

    /// <summary>Starts tracking one thumb. A press elsewhere springs the thumb over; a grabbed thumb keeps its grab offset.</summary>
    private void Begin(SliderRailBase rail, int index, double at, bool press, ulong time)
    {
        var thumb = _thumbs[index];
        var drag = _drag!;
        var from = thumb.Shown;
        drag.Index = index;
        drag.Grab = press ? 0 : at - from;
        thumb.Lag!.Jump(0);
        thumb.Position!.Jump(from);
        _lastActive = index;
        FocusFromPointer(index);
        SetLifted(index);
        PseudoClasses.Set(DraggingPseudoClass, true);
        UpdateThumbs();
        if (press)
        {
            Follow(rail, at, time, press: true);
        }
    }

    /// <summary>Places the dragged thumb under the pointer, with resistance past its limits, and commits the stepped value.</summary>
    private void Follow(SliderRailBase rail, double at, ulong time, bool press = false)
    {
        var drag = _drag!;
        var index = drag.Index;
        if (index < 0)
        {
            return;
        }
        var thumb = _thumbs[index];
        var position = thumb.Position!;
        var width = rail.TrackWidth;
        var full = ArcMotion.IsFull(this);
        var raw = at - drag.Grab;
        var low = ToPosition(LowOf(index));
        var high = ToPosition(HighOf(index));
        var edge = Math.Clamp(raw, Math.Min(low, high), Math.Max(low, high));
        var placed = full ? edge + SliderMath.Rubber((raw - edge) / SliderMath.Span * width, Stretch) / width * SliderMath.Span : edge;
        if (press && full)
        {
            var from = position.Value;
            position.Jump(placed);
            thumb.Lag!.Jump(from - placed);
            _ = thumb.Lag.AnimateTo(0, MotionSpec.Spring(s_thumbSpring));
        }
        else
        {
            position.Jump(placed);
        }
        drag.Raw = raw;
        _tracker!.Add(time, placed);
        Commit(index, Settle(index, ValueAt(raw)));
    }

    void ISliderRailOwner.OnRailMoved(SliderRailBase rail, PointerEventArgs e)
    {
        var drag = _drag;
        if (!_dragActive || drag is null || e.Pointer != drag.Pointer)
        {
            return;
        }
        var x = e.GetPosition(rail).X;
        var at = rail.ToPosition(x);
        if (drag.Index < 0)
        {
            var dx = x - drag.StartX;
            if (Math.Abs(dx) < StackedPickDistance)
            {
                return;
            }
            Begin(rail, dx < 0 ? 0 : 1, at - dx / rail.TrackWidth * SliderMath.Span, press: false, e.Timestamp);
        }
        Follow(rail, at, e.Timestamp);
    }

    void ISliderRailOwner.OnRailReleased(SliderRailBase rail, PointerReleasedEventArgs e)
    {
        if (!_dragActive || e.Pointer != _drag!.Pointer)
        {
            return;
        }
        End(rail, e.Timestamp, release: true);
        e.Pointer.Capture(null);
    }

    void ISliderRailOwner.OnRailCaptureLost(SliderRailBase rail)
    {
        if (_dragActive)
        {
            End(rail, 0, release: false);
        }
    }

    /// <summary>Momentum may carry the thumb at most one bar further; then a nearby bar edge claims it.</summary>
    private void End(SliderRailBase rail, ulong time, bool release)
    {
        _dragActive = false;
        var drag = _drag!;
        var index = drag.Index;
        drag.Pointer = null;
        if (index < 0)
        {
            return;
        }
        var thumb = _thumbs[index];
        var position = thumb.Position!;
        var lag = thumb.Lag!;
        var velocity = (release ? _tracker?.Velocity(time) ?? 0 : 0) + lag.Velocity;
        var binPosition = SliderMath.Span / BinCount;
        var carry = release ? Math.Clamp(SliderMath.Project(velocity), -binPosition, binPosition) : 0;
        var landing = Math.Clamp(drag.Raw + carry, 0, SliderMath.Span);
        var wanted = ValueAt(landing);
        if (SnapToBins)
        {
            var edge = _min + Math.Floor((wanted - _min) / BinSpan + 0.5) * BinSpan;
            if (Math.Abs(ToPosition(edge) - landing) / SliderMath.Span * rail.TrackWidth <= BinSnapPixels)
            {
                wanted = edge;
            }
        }
        var next = Settle(index, wanted);
        var from = thumb.Shown;
        var target = ToPosition(next);
        lag.Jump(0);
        position.Jump(from);
        thumb.Goal = target;
        if (CanAnimate)
        {
            _ = position.AnimateTo(target, MotionSpec.Spring(s_thumbSpring), velocity);
        }
        else
        {
            position.Jump(target);
        }
        Commit(index, next);
        drag.Index = -1;
        SetLifted(-1);
        PseudoClasses.Set(DraggingPseudoClass, false);
        RaiseCommitted();
    }

    #endregion

    #region Keyboard

    /// <summary>The next bar edge in a direction, so large moves walk the histogram bar by bar.</summary>
    private double EdgeFrom(double now, int direction)
    {
        var at = (now - _min) / BinSpan;
        var next = direction > 0 ? Math.Floor(at + EdgeEpsilon) + 1 : Math.Ceiling(at - EdgeEpsilon) - 1;
        return _min + next * BinSpan;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not SliderThumb thumb || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        var index = thumb.Index;
        var now = _values[index];
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        double wanted;
        switch (e.Key)
        {
            case Key.Right or Key.Up:
                wanted = large ? EdgeFrom(now, 1) : now + Grid;
                break;
            case Key.Left or Key.Down:
                wanted = large ? EdgeFrom(now, -1) : now - Grid;
                break;
            case Key.PageUp:
                wanted = EdgeFrom(now, 1);
                break;
            case Key.PageDown:
                wanted = EdgeFrom(now, -1);
                break;
            case Key.Home:
                wanted = LowOf(index);
                break;
            case Key.End:
                wanted = HighOf(index);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
        _lastActive = index;
        thumb.ShowRing = true;
        UpdateThumbs();
        var next = Settle(index, wanted);
        if (next == now)
        {
            // At a limit the thumb strains toward the press and springs home, so the key still answers.
            var toward = Math.Sign(wanted - now);
            var motion = _thumbs[index];
            if (toward != 0 && _rail is { } rail && CanAnimate && !double.IsNaN(motion.Goal) && motion.Position is { } position)
            {
                _ = position.AnimateTo(motion.Goal, MotionSpec.Spring(s_kickSpring), toward * BumpSpeed / rail.TrackWidth * SliderMath.Span);
            }
            return;
        }
        Commit(index, next);
        RaiseCommitted();
    }

    #endregion

    #region Automation

    double ISliderThumbHost.GetThumbMinimum(int index) => LowOf(index);

    double ISliderThumbHost.GetThumbMaximum(int index) => HighOf(index);

    double ISliderThumbHost.GetThumbValue(int index) => _values[index];

    double ISliderThumbHost.SmallStep => Grid;

    double ISliderThumbHost.LargeStep => BinSpan;

    bool ISliderThumbHost.IsThumbReadOnly => !IsEffectivelyEnabled;

    void ISliderThumbHost.SetThumbValue(int index, double value)
    {
        Commit(index, Settle(index, value));
        RaiseCommitted();
    }

    /// <summary>The slider itself is a group; each thumb is a Slider control.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer : ControlAutomationPeer
    {
        public GroupPeer(RangeHistogramSlider owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() ?? ((RangeHistogramSlider)Owner).Label;
    }

    #endregion

    // Read by tests.
    internal RangeHistogramRail? Rail => _rail;

    internal double ShownPosition(int index) => _thumbs[index].Shown;

    internal double LiftOf(int index) => _thumbs[index].Lift?.Value ?? 1;

    internal double ValueOf(int index) => _values[index];
}
