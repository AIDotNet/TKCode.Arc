using TKCode.Arc.Primitives;
using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaSlider = Avalonia.Controls.Slider;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="Slider.ValueCommitted"/>.</summary>
public sealed class SliderValueCommittedEventArgs : RoutedEventArgs
{
    public SliderValueCommittedEventArgs(RoutedEvent routedEvent, double value, double lowerValue, double upperValue, bool isRange)
        : base(routedEvent)
    {
        Value = value;
        LowerValue = lowerValue;
        UpperValue = upperValue;
        IsRange = isRange;
    }

    /// <summary>The value (single thumb), or the lower end of a range.</summary>
    public double Value { get; }

    public double LowerValue { get; }

    public double UpperValue { get; }

    public bool IsRange { get; }
}

/// <summary>Arguments of <see cref="Slider.RangeChanged"/>.</summary>
public sealed class SliderRangeChangedEventArgs : RoutedEventArgs
{
    public SliderRangeChangedEventArgs(RoutedEvent routedEvent, double lowerValue, double upperValue) : base(routedEvent)
    {
        LowerValue = lowerValue;
        UpperValue = upperValue;
    }

    public double LowerValue { get; }

    public double UpperValue { get; }
}

/// <summary>
/// Arc slider: a value or a range on a track. The thumb follows the pointer 1:1, rubber-bands up to 9px past its limits
/// and settles on the step grid with the velocity of the release (on a coarse grid the release may carry it one step
/// further); pressing the track springs the nearest thumb there. A bubble with rolling digits rides the thumb while it
/// is dragged or keyboard focused and lingers 700ms after a release. Arrows, Shift+arrows, PageUp / PageDown and
/// Home / End work as in a native range input; a key at a limit makes the thumb strain toward the press and spring home.
/// Unlike <see cref="AvaloniaSlider"/>, <see cref="RangeBase.SmallChange"/> is the step the value snaps to.
/// Set <see cref="IsRange"/> for two thumbs (<see cref="LowerValue"/> / <see cref="UpperValue"/>).
/// </summary>
[TemplatePart(RailPart, typeof(Control))]
[TemplatePart(MarksPart, typeof(Control))]
[PseudoClasses(DraggingPseudoClass, RangePseudoClass, MarksPseudoClass)]
public class Slider : RangeBase, ISliderRailOwner, ISliderThumbHost, ISliderMarkHost
{
    public const string RailPart = "PART_Rail";
    public const string MarksPart = "PART_Marks";
    public const string ReadoutLowPart = "PART_ReadoutLow";
    public const string ReadoutHighPart = "PART_ReadoutHigh";
    public const string DraggingPseudoClass = ":dragging";
    public const string RangePseudoClass = ":range";
    public const string MarksPseudoClass = ":marks";

    /// <summary>Pixels of rubber-band travel past a limit.</summary>
    internal const double Stretch = 9;

    /// <summary>Speed (px/s) a key press at a limit hits the wall with.</summary>
    internal const double BumpSpeed = 150;

    /// <summary>A step must be at least this many pixels wide before release momentum may choose it.</summary>
    internal const double SnapPixels = 12;

    /// <summary>Extra hit radius around a thumb (Arc's 11px <c>::after</c>).</summary>
    internal const double ThumbReach = 11;

    /// <summary>How long the bubble stays after a release.</summary>
    internal static readonly TimeSpan Linger = TimeSpan.FromMilliseconds(700);

    // Motion drops velocity on time-defined springs, so a gesture hands off to the same springs as stiffness/damping.
    internal static readonly SpringSpec ThumbSpring = ArcMotionPresets.Snappy;
    internal static readonly SpringSpec KickSpring = ArcMotionPresets.Morph;

    #region 公共属性定义

    /// <summary>Visible label; also names the thumb (or, with " minimum" / " maximum", the range thumbs).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Slider, string?>(nameof(Label));

    /// <summary>Two thumbs: <see cref="LowerValue"/> and <see cref="UpperValue"/> instead of <see cref="RangeBase.Value"/>.</summary>
    public static readonly StyledProperty<bool> IsRangeProperty =
        AvaloniaProperty.Register<Slider, bool>(nameof(IsRange));

    /// <summary>Lower end of the range when IsRange is true (two-way, default 0).</summary>
    public static readonly StyledProperty<double> LowerValueProperty =
        AvaloniaProperty.Register<Slider, double>(nameof(LowerValue), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Upper end of the range when IsRange is true (two-way, default 100).</summary>
    public static readonly StyledProperty<double> UpperValueProperty =
        AvaloniaProperty.Register<Slider, double>(nameof(UpperValue), 100, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The closest the two range thumbs may sit, in steps.</summary>
    public static readonly StyledProperty<int> MinStepsBetweenThumbsProperty =
        AvaloniaProperty.Register<Slider, int>(nameof(MinStepsBetweenThumbs));

    /// <summary>Formats the readout, the bubble and the spoken value (default: en-US with the step's decimals).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<Slider, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Shows the value beside the label. The bubble still appears while dragging or keyboard focused.</summary>
    public static readonly StyledProperty<bool> IsValueVisibleProperty =
        AvaloniaProperty.Register<Slider, bool>(nameof(IsValueVisible), true);

    /// <summary>Accessible name of the lower range thumb; when null it is the Label with ", minimum" appended.</summary>
    public static readonly StyledProperty<string?> LowerThumbNameProperty =
        AvaloniaProperty.Register<Slider, string?>(nameof(LowerThumbName));

    /// <summary>Accessible name of the upper range thumb; when null it is the Label with ", maximum" appended.</summary>
    public static readonly StyledProperty<string?> UpperThumbNameProperty =
        AvaloniaProperty.Register<Slider, string?>(nameof(UpperThumbName));

    /// <summary>Content before the track, such as an icon or a mute button.</summary>
    public static readonly StyledProperty<object?> StartContentProperty =
        AvaloniaProperty.Register<Slider, object?>(nameof(StartContent));

    /// <summary>Content after the track, such as an icon or a button.</summary>
    public static readonly StyledProperty<object?> EndContentProperty =
        AvaloniaProperty.Register<Slider, object?>(nameof(EndContent));

    public static readonly DirectProperty<Slider, IList<SliderMark>> MarksProperty =
        AvaloniaProperty.RegisterDirect<Slider, IList<SliderMark>>(nameof(Marks), o => o.Marks, (o, v) => o.Marks = v);

    public static readonly RoutedEvent<SliderValueCommittedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<Slider, SliderValueCommittedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<SliderRangeChangedEventArgs> RangeChangedEvent =
        RoutedEvent.Register<Slider, SliderRangeChangedEventArgs>(nameof(RangeChanged), RoutingStrategies.Bubble);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsRange { get => GetValue(IsRangeProperty); set => SetValue(IsRangeProperty, value); }
    /// <inheritdoc cref="LowerValueProperty"/>
    public double LowerValue { get => GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    /// <inheritdoc cref="UpperValueProperty"/>
    public double UpperValue { get => GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public int MinStepsBetweenThumbs { get => GetValue(MinStepsBetweenThumbsProperty); set => SetValue(MinStepsBetweenThumbsProperty, value); }
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }
    public bool IsValueVisible { get => GetValue(IsValueVisibleProperty); set => SetValue(IsValueVisibleProperty, value); }
    /// <inheritdoc cref="LowerThumbNameProperty"/>
    public string? LowerThumbName { get => GetValue(LowerThumbNameProperty); set => SetValue(LowerThumbNameProperty, value); }
    /// <inheritdoc cref="UpperThumbNameProperty"/>
    public string? UpperThumbName { get => GetValue(UpperThumbNameProperty); set => SetValue(UpperThumbNameProperty, value); }
    public object? StartContent { get => GetValue(StartContentProperty); set => SetValue(StartContentProperty, value); }
    /// <inheritdoc cref="EndContentProperty"/>
    public object? EndContent { get => GetValue(EndContentProperty); set => SetValue(EndContentProperty, value); }

    private IList<SliderMark> _marks = new AvaloniaList<SliderMark>();
    // Marks may be a view model's list, and their properties may be bound: they are observed only while the slider is
    // shown, and re-read when it comes back.
    private readonly ObservedItems _observedMarks;
    private bool _watching;

    /// <summary>Ticks on the track; labeled marks also print under it and are clickable.</summary>
    public IList<SliderMark> Marks
    {
        get => _marks;
        set
        {
            if (SetAndRaise(MarksProperty, ref _marks, value ?? new AvaloniaList<SliderMark>()))
            {
                WatchMarks();
                UpdateMarks();
            }
        }
    }

    /// <summary>Runs once a drag is released, a key changes the value or a mark label is clicked.</summary>
    public event EventHandler<SliderValueCommittedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    /// <summary>Raised on every step of a range (drag or keys), like <see cref="RangeBase.ValueChanged"/> for one thumb.</summary>
    public event EventHandler<SliderRangeChangedEventArgs>? RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    #endregion

    private sealed class ThumbMotion
    {
        public required MotionValue<double> Position { get; init; }
        public required MotionValue<double> Lag { get; init; }
        public required MotionValue<double> Lift { get; init; }
        public double Goal { get; set; } = double.NaN;
        public double Shown => Position.Value + Lag.Value;
    }

    private sealed class DragState
    {
        public IPointer? Pointer;
        public int Index = -1;
        public double Grab;
        public double StartX;
        public double Raw;
    }

    private readonly ThumbMotion[] _thumbs = new ThumbMotion[2];
    private readonly double[] _values = new double[2];
    private readonly SliderVelocityTracker _tracker = new();
    private readonly DragState _dragState = new();
    private bool _dragActive;
    private SliderRail? _rail;
    private SliderMarkLabels? _markLabels;
    private SliderNumberText? _readoutLow;
    private SliderNumberText? _readoutHigh;
    private readonly SliderNumberText[] _bubbleTexts = new SliderNumberText[2];
    private DispatcherTimer? _lingerTimer;
    private int _dragging = -1;
    private int _keyFocus = -1;
    private int _linger = -1;
    private int _lastActive;

    static Slider()
    {
        FocusableProperty.OverrideDefaultValue<Slider>(false);
        SmallChangeProperty.OverrideDefaultValue<Slider>(1);
    }

    public Slider()
    {
        _observedMarks = new ObservedItems(_ => UpdateMarks());
        for (var i = 0; i < 2; i++)
        {
            var index = i;
            var position = new MotionValue<double>(this, 0, MotionVectors.Double);
            var lag = new MotionValue<double>(this, 0, MotionVectors.Double);
            var lift = new MotionValue<double>(this, 1, MotionVectors.Double);
            position.Changed += _ => OnThumbMoved();
            lag.Changed += _ => OnThumbMoved();
            lift.Changed += _ => OnThumbMoved();
            _thumbs[index] = new ThumbMotion { Position = position, Lag = lag, Lift = lift };
            _bubbleTexts[index] = new SliderNumberText();
        }
        UpdatePseudoClasses();
    }

    protected override Type StyleKeyOverride => typeof(Slider);

    #region Values

    private int Count => IsRange ? 2 : 1;

    private double Span => Maximum - Minimum is var span && span > 0 ? span : 1;

    private double Step => SmallChange > 0 ? SmallChange : 1;

    private int Decimals => Math.Max(SliderMath.DecimalsOf(Step), SliderMath.DecimalsOf(Minimum));

    /// <summary>PageUp / PageDown / Shift+arrow step: <see cref="RangeBase.LargeChange"/> when set, else a tenth of the range.</summary>
    internal double LargeStep => IsSet(LargeChangeProperty) && LargeChange > 0
        ? LargeChange
        : SliderMath.DefaultLargeStep(Minimum, Maximum, Step, Decimals);

    private double Gap => IsRange ? Math.Max(0, MinStepsBetweenThumbs) * Step : 0;

    internal double ToPosition(double value) => (value - Minimum) / Span * SliderMath.Span;

    private double ValueAt(double position) => Minimum + position / SliderMath.Span * Span;

    private double Snap(double value) => SliderMath.Snap(value, Minimum, Step, Decimals);

    private double LowOf(int index) => index == 1 ? _values[0] + Gap : Minimum;

    private double HighOf(int index) => index == 0 && IsRange ? _values[1] - Gap : Maximum;

    private double Settle(int index, double input)
    {
        var snapped = Snap(ClampToRange(input));
        var low = LowOf(index);
        var high = HighOf(index);
        return high < low ? low : Math.Clamp(snapped, low, high);
    }

    internal string FormatValue(double value) => ValueFormatter?.Invoke(value) ?? SliderMath.Format(value, Decimals);

    // Minimum can briefly exceed Maximum while XAML sets them one by one (Minimum="360" before Maximum="900"): RangeBase
    // coerces Maximum only after Minimum's change is reported, and Math.Clamp throws when min > max.
    private double ClampToRange(double value) => Math.Clamp(value, Minimum, Math.Max(Minimum, Maximum));

    private void ReadValues()
    {
        if (IsRange)
        {
            var low = ClampToRange(LowerValue);
            var high = ClampToRange(UpperValue);
            _values[0] = Math.Min(low, high);
            _values[1] = Math.Max(low, high);
        }
        else
        {
            _values[0] = ClampToRange(Value);
            _values[1] = _values[0];
        }
    }

    /// <summary>The fill between the range ends, or from the start to the thumb (track positions, clamped).</summary>
    internal (double From, double To) FillSpan()
    {
        var from = IsRange ? Math.Clamp(_thumbs[0].Shown, 0, SliderMath.Span) : 0;
        var to = Math.Clamp(_thumbs[Count - 1].Shown, 0, SliderMath.Span);
        return (from, Math.Max(from, to));
    }

    private void Commit(int index, double next)
    {
        if (_values[index] == next)
        {
            return;
        }
        _values[index] = next;
        if (!IsRange)
        {
            SetCurrentValue(ValueProperty, next);
        }
        else if (index == 0)
        {
            SetCurrentValue(LowerValueProperty, next);
        }
        else
        {
            SetCurrentValue(UpperValueProperty, next);
        }
    }

    private void RaiseCommitted()
    {
        RaiseEvent(new SliderValueCommittedEventArgs(ValueCommittedEvent, _values[0], _values[0], _values[Count - 1], IsRange));
    }

    /// <summary>Values that arrive from outside a drag (keys, bindings, a clicked mark) spring the thumb to their place.</summary>
    private void SyncThumbs(bool animate)
    {
        ReadValues();
        for (var i = 0; i < Count; i++)
        {
            var thumb = _thumbs[i];
            var target = ToPosition(_values[i]);
            if ((_dragActive && _dragState.Index == i) || thumb.Goal == target)
            {
                continue;
            }
            var wasPlaced = !double.IsNaN(thumb.Goal);
            thumb.Goal = target;
            var from = thumb.Shown;
            thumb.Lag.Jump(0);
            if (!animate || !wasPlaced || !IsLoaded)
            {
                thumb.Position.Jump(target);
                continue;
            }
            thumb.Position.Jump(from);
            _ = thumb.Position.AnimateTo(target, MotionSpec.Spring(ThumbSpring));
        }
        UpdateTexts();
        _rail?.InvalidateVisual();
        _markLabels?.InvalidateVisual();
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rail = e.NameScope.Find<SliderRail>(RailPart);
        _markLabels = e.NameScope.Find<SliderMarkLabels>(MarksPart);
        _readoutLow = e.NameScope.Find<SliderNumberText>(ReadoutLowPart);
        _readoutHigh = e.NameScope.Find<SliderNumberText>(ReadoutHighPart);
        if (_rail is not null)
        {
            _rail.Slider = this;
            _rail.Setup(this, this, 2, index => _bubbleTexts[index], bubble =>
            {
                bubble.EnterScale = 0.6;
                bubble.ExitScale = 0.8;
            });
            for (var i = 0; i < 2; i++)
            {
                _rail.Thumbs[i].GotFocus += OnThumbGotFocus;
                _rail.Thumbs[i].LostFocus += OnThumbLostFocus;
            }
        }
        if (_markLabels is not null)
        {
            _markLabels.Host = this;
        }
        foreach (var thumb in _thumbs)
        {
            thumb.Goal = double.NaN;
        }
        SyncThumbs(animate: false);
        UpdateThumbs();
        UpdateMarks();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == LowerValueProperty || change.Property == UpperValueProperty)
        {
            SyncThumbs(animate: true);
            if (IsRange && change.Property != ValueProperty)
            {
                RaiseEvent(new SliderRangeChangedEventArgs(RangeChangedEvent, _values[0], _values[1]));
            }
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == SmallChangeProperty ||
                 change.Property == MinStepsBetweenThumbsProperty)
        {
            foreach (var thumb in _thumbs)
            {
                thumb.Goal = double.NaN;
            }
            SyncThumbs(animate: false);
            UpdateMarks();
        }
        else if (change.Property == IsRangeProperty)
        {
            foreach (var thumb in _thumbs)
            {
                thumb.Goal = double.NaN;
            }
            _lastActive = IsRange ? 1 : 0;
            SyncThumbs(animate: false);
            UpdateThumbs();
            UpdatePseudoClasses();
        }
        else if (change.Property == ValueFormatterProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == LabelProperty || change.Property == LowerThumbNameProperty || change.Property == UpperThumbNameProperty)
        {
            UpdateThumbs();
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            UpdateThumbs();
        }
    }

    /// <summary>Listens to the current marks while shown (and to nothing otherwise).</summary>
    private void WatchMarks()
    {
        _observedMarks.Observe(_watching ? _marks : null);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchMarks();
        // The marks may have changed while the slider was away.
        UpdateMarks();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchMarks();
    }

    private void UpdateMarks()
    {
        _markLabels?.Refresh();
        if (_markLabels is not null)
        {
            _markLabels.IsVisible = _markLabels.HasLabels;
        }
        PseudoClasses.Set(MarksPseudoClass, _markLabels?.HasLabels ?? false);
        _rail?.InvalidateVisual();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(RangePseudoClass, IsRange);
        PseudoClasses.Set(DraggingPseudoClass, _dragging >= 0);
    }

    private void UpdateThumbs()
    {
        if (_rail is null)
        {
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            var thumb = _rail.Thumbs[i];
            thumb.IsVisible = i < Count;
            thumb.IsTabStop = IsEffectivelyEnabled;
            thumb.ZIndex = i == _lastActive ? 2 : 1;
            var name = !IsRange
                ? Label
                : i == 0 ? LowerThumbName ?? $"{Label}, minimum" : UpperThumbName ?? $"{Label}, maximum";
            AutomationProperties.SetName(thumb, name);
        }
        UpdateTexts();
        UpdateBubbles();
        OnThumbMoved();
    }

    private void UpdateTexts()
    {
        var low = FormatValue(_values[0]);
        var high = FormatValue(_values[1]);
        _readoutLow?.SetText(low);
        _readoutHigh?.SetText(high);
        _bubbleTexts[0].SetText(low);
        _bubbleTexts[1].SetText(high);
        if (_rail is not null)
        {
            // Spoken value text (aria-valuetext).
            AutomationProperties.SetItemStatus(_rail.Thumbs[0], low);
            AutomationProperties.SetItemStatus(_rail.Thumbs[1], high);
        }
    }

    #endregion

    #region Placement

    void ISliderRailOwner.OnRailArranged(SliderRailBase rail) => PlaceVisuals();

    private void OnThumbMoved()
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
        for (var i = 0; i < Count; i++)
        {
            var motion = _thumbs[i];
            var x = motion.Shown / SliderMath.Span * rail.TrackWidth;
            rail.Thumbs[i].Place(x, motion.Lift.Value);
            rail.PlaceBubble(i, rail.TrackLeft + x);
        }
    }

    private void UpdateBubbles()
    {
        if (_rail is null)
        {
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            var shown = i < Count && (_dragging == i || _keyFocus == i || _linger == i);
            _rail.Bubbles[i].Show(shown);
        }
    }

    private void SetLifted(int index)
    {
        for (var i = 0; i < 2; i++)
        {
            var target = i == index ? 1.16 : 1.0;
            var lift = _thumbs[i].Lift;
            if (lift.Target != target)
            {
                _ = lift.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
            }
        }
    }

    private void HoldBubble(int index)
    {
        _lingerTimer ??= CreateLingerTimer();
        _lingerTimer.Stop();
        _linger = index;
        _lingerTimer.Start();
        UpdateBubbles();
    }

    private DispatcherTimer CreateLingerTimer()
    {
        var timer = new DispatcherTimer { Interval = Linger };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _linger = -1;
            UpdateBubbles();
        };
        return timer;
    }

    private void ClearLinger()
    {
        _lingerTimer?.Stop();
        _linger = -1;
    }

    #endregion

    #region Pointer

    private int Nearest(double position)
    {
        if (!IsRange)
        {
            return 0;
        }
        var low = Math.Abs(position - ToPosition(_values[0]));
        var high = Math.Abs(position - ToPosition(_values[1]));
        if (low == high)
        {
            return position < ToPosition(_values[0]) ? 0 : 1;
        }
        return low < high ? 0 : 1;
    }

    /// <summary>The thumb under <paramref name="x"/> (within its hit radius), -1 for none, -2 for two stacked thumbs.</summary>
    private int GrabbedThumb(SliderRailBase rail, double x)
    {
        var reach = rail.ThumbDiameter / 2 + ThumbReach;
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < Count; i++)
        {
            var distance = Math.Abs(x - rail.ToPixel(_thumbs[i].Shown));
            if (distance <= reach && distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        if (best >= 0 && IsRange && _values[0] == _values[1])
        {
            return -2;
        }
        return best;
    }

    private void FocusFromPointer(int index)
    {
        if (_rail is null)
        {
            return;
        }
        var thumb = _rail.Thumbs[index];
        if (!thumb.IsFocused)
        {
            thumb.Focus(NavigationMethod.Pointer);
        }
    }

    void ISliderRailOwner.OnRailPressed(SliderRailBase rail, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.Pointer.IsPrimary || !e.GetCurrentPoint(rail).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var x = e.GetPosition(rail).X;
        var at = rail.ToPosition(x);
        var grabbed = GrabbedThumb(rail, x);
        e.Pointer.Capture(rail);
        e.Handled = true;
        _dragActive = true;
        _dragState.Pointer = e.Pointer;
        _dragState.Index = -1;
        _dragState.Grab = 0;
        _dragState.StartX = x;
        _dragState.Raw = at;
        _tracker.Reset();
        if (grabbed == -2)
        {
            // Stacked range thumbs wait for the first movement to decide which one the pointer means.
            FocusFromPointer(_lastActive);
            return;
        }
        var index = grabbed >= 0 ? grabbed : Nearest(at);
        Begin(rail, index, at, press: grabbed < 0, e.Timestamp);
        _tracker.Add(e.Timestamp, _thumbs[index].Position.Value);
    }

    /// <summary>Starts tracking one thumb. A pressed track springs the thumb over; a grabbed thumb keeps its grab offset.</summary>
    private void Begin(SliderRailBase rail, int index, double at, bool press, ulong time)
    {
        var thumb = _thumbs[index];
        var from = thumb.Shown;
        _dragState.Index = index;
        _dragState.Grab = press ? 0 : at - from;
        thumb.Lag.Jump(0);
        thumb.Position.Jump(from);
        _dragging = index;
        _lastActive = index;
        ClearLinger();
        FocusFromPointer(index);
        SetLifted(index);
        UpdatePseudoClasses();
        UpdateThumbs();
        if (press)
        {
            Follow(rail, at, time, press: true);
        }
    }

    /// <summary>Places the dragged thumb under the pointer, with resistance past its limits, and commits the stepped value.</summary>
    private void Follow(SliderRailBase rail, double at, ulong time, bool press = false)
    {
        var index = _dragState.Index;
        if (index < 0)
        {
            return;
        }
        var thumb = _thumbs[index];
        var width = rail.TrackWidth;
        var full = ArcMotion.IsFull(this);
        var raw = at - _dragState.Grab;
        var low = ToPosition(LowOf(index));
        var high = ToPosition(HighOf(index));
        var edge = Math.Clamp(raw, Math.Min(low, high), Math.Max(low, high));
        var placed = full ? edge + SliderMath.Rubber((raw - edge) / SliderMath.Span * width, Stretch) / width * SliderMath.Span : edge;
        if (press && full)
        {
            var from = thumb.Position.Value;
            thumb.Position.Jump(placed);
            thumb.Lag.Jump(from - placed);
            _ = thumb.Lag.AnimateTo(0, MotionSpec.Spring(ThumbSpring));
        }
        else
        {
            thumb.Position.Jump(placed);
        }
        _dragState.Raw = raw;
        _tracker.Add(time, placed);
        Commit(index, Settle(index, ValueAt(raw)));
    }

    void ISliderRailOwner.OnRailMoved(SliderRailBase rail, PointerEventArgs e)
    {
        if (!_dragActive || e.Pointer != _dragState.Pointer)
        {
            return;
        }
        var x = e.GetPosition(rail).X;
        var at = rail.ToPosition(x);
        if (_dragState.Index < 0)
        {
            var dx = x - _dragState.StartX;
            if (Math.Abs(dx) < 2)
            {
                return;
            }
            Begin(rail, dx < 0 ? 0 : 1, at - dx / rail.TrackWidth * SliderMath.Span, press: false, e.Timestamp);
        }
        Follow(rail, at, e.Timestamp);
    }

    void ISliderRailOwner.OnRailReleased(SliderRailBase rail, PointerReleasedEventArgs e)
    {
        if (!_dragActive || e.Pointer != _dragState.Pointer)
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

    private void End(SliderRailBase rail, ulong time, bool release)
    {
        _dragActive = false;
        var index = _dragState.Index;
        _dragState.Pointer = null;
        if (index < 0)
        {
            return;
        }
        var thumb = _thumbs[index];
        var width = rail.TrackWidth;
        // Velocity in track units per second; a release during the catch-up spring keeps that spring's speed too.
        var velocity = (release ? _tracker.Velocity(time) : 0) + thumb.Lag.Velocity;
        var stepPosition = Step / Span * SliderMath.Span;
        var carry = release && stepPosition / SliderMath.Span * width >= SnapPixels
            ? Math.Clamp(SliderMath.Project(velocity), -stepPosition, stepPosition)
            : 0;
        var next = Settle(index, ValueAt(_dragState.Raw + carry));
        var from = thumb.Shown;
        var target = ToPosition(next);
        thumb.Lag.Jump(0);
        thumb.Position.Jump(from);
        thumb.Goal = target;
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _ = thumb.Position.AnimateTo(target, MotionSpec.Spring(ThumbSpring), velocity);
        }
        else
        {
            thumb.Position.Jump(target);
        }
        Commit(index, next);
        _dragState.Index = -1;
        _dragging = -1;
        SetLifted(-1);
        UpdatePseudoClasses();
        HoldBubble(index);
        RaiseCommitted();
    }

    #endregion

    #region Keyboard

    private void OnThumbGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is SliderThumb thumb && e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
        {
            _keyFocus = thumb.Index;
            _lastActive = thumb.Index;
            UpdateThumbs();
        }
    }

    private void OnThumbLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is SliderThumb thumb && _keyFocus == thumb.Index)
        {
            _keyFocus = -1;
            UpdateBubbles();
        }
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
        var step = Step;
        var large = LargeStep;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        double wanted;
        switch (e.Key)
        {
            case Key.Right or Key.Up:
                wanted = now + (shift ? large : step);
                break;
            case Key.Left or Key.Down:
                wanted = now - (shift ? large : step);
                break;
            case Key.PageUp:
                wanted = now + large;
                break;
            case Key.PageDown:
                wanted = now - large;
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
        _keyFocus = index;
        _lastActive = index;
        thumb.ShowRing = true;
        UpdateThumbs();
        var next = Settle(index, wanted);
        if (next == now)
        {
            // At a limit the thumb strains toward the press and springs home, so the key still answers.
            var toward = Math.Sign(wanted - now);
            var motion = _thumbs[index];
            if (toward != 0 && _rail is not null && IsLoaded && ArcMotion.IsFull(this) && !double.IsNaN(motion.Goal))
            {
                var velocity = toward * BumpSpeed / _rail.TrackWidth * SliderMath.Span;
                _ = motion.Position.AnimateTo(motion.Goal, MotionSpec.Spring(KickSpring), velocity);
            }
            return;
        }
        Commit(index, next);
        RaiseCommitted();
    }

    #endregion

    #region Marks and automation

    double ISliderMarkHost.MarkInset => _rail?.ThumbDiameter / 2 ?? 11;

    bool ISliderMarkHost.IsMarkOn(double value) => IsRange ? value >= _values[0] && value <= _values[1] : value <= _values[0];

    /// <summary>A clicked mark label moves the nearest thumb there.</summary>
    void ISliderMarkHost.ActivateMark(double value)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        var index = Nearest(ToPosition(value));
        var next = Settle(index, value);
        _lastActive = index;
        FocusFromPointer(index);
        UpdateThumbs();
        if (next == _values[index])
        {
            return;
        }
        Commit(index, next);
        RaiseCommitted();
    }

    double ISliderThumbHost.GetThumbMinimum(int index) => LowOf(index);

    double ISliderThumbHost.GetThumbMaximum(int index) => HighOf(index);

    double ISliderThumbHost.GetThumbValue(int index) => _values[index];

    double ISliderThumbHost.SmallStep => Step;

    double ISliderThumbHost.LargeStep => LargeStep;

    bool ISliderThumbHost.IsThumbReadOnly => !IsEffectivelyEnabled;

    void ISliderThumbHost.SetThumbValue(int index, double value)
    {
        Commit(index, Settle(index, value));
        RaiseCommitted();
    }

    /// <summary>The slider itself is a group; each thumb is a Slider control.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new SliderGroupAutomationPeer(this);

    private sealed class SliderGroupAutomationPeer : ControlAutomationPeer
    {
        public SliderGroupAutomationPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }

    #endregion

    // Read by tests.
    internal double ShownPosition(int index) => _thumbs[index].Shown;

    internal bool IsBubbleShown(int index) => _rail?.Bubbles[index].IsShown ?? false;

    internal SliderRail? Rail => _rail;
}
