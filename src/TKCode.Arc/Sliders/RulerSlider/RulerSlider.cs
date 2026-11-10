// Ported from Arc Ruler slider (MIT, Copyright (c) 2026 Elia Kuratli): the value model, the drag, flick, tap, wheel and
// keyboard rules, and every motion constant.
using System.Globalization;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Arc ruler slider: a precise value input shaped like a tape measure. A ruler of ticks slides under a fixed needle: drag it
/// 1:1, flick it and it coasts onto a tick, pull it past either end and it rubber-bands back, or tap a spot to glide it there.
/// The readout rolls each digit as the tape passes. Arrows step one tick, Shift+arrows and PageUp / PageDown move
/// <see cref="RangeBase.LargeChange"/>, Home and End go to the ends, and horizontal trackpad scrolling moves the tape.
/// <see cref="RangeBase.SmallChange"/> is the value of one small tick, and the tape always rests on a tick.
/// </summary>
[TemplatePart(TapePart, typeof(Control))]
[TemplatePart(ReadoutPart, typeof(Control))]
[TemplatePart(ViewportPart, typeof(Panel))]
[PseudoClasses(DraggingPseudoClass)]
public class RulerSlider : RangeBase
{
    public const string TapePart = "PART_Tape";
    public const string ReadoutPart = "PART_ReadoutValue";
    public const string ViewportPart = "PART_Viewport";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Pixels the tape may be pulled past either end.</summary>
    internal const double Stretch = 44;

    /// <summary>How far a tap may wander before it counts as a drag.</summary>
    internal const double TapSlop = 4;

    /// <summary>A flick lands where this many seconds of its speed would carry it.</summary>
    internal const double Power = 0.42;

    /// <summary>How fast a flick decays toward its landing tick, in seconds.</summary>
    internal const double TimeConstant = 0.26;

    /// <summary>Wheel input is quiet for this long before the tape settles on a tick.</summary>
    internal static readonly TimeSpan WheelSettle = TimeSpan.FromMilliseconds(120);

    /// <summary>Pixels of tape per unit of a wheel delta, as Avalonia's scroll viewer moves content.</summary>
    internal const double WheelPixels = 50;

    /// <summary>
    /// Position units per small tick. A value's rest threshold (half a unit) is then 1/200 of a tick, as in Arc, whatever
    /// the step and the spacing are.
    /// </summary>
    internal const double UnitsPerTick = 100;

    private const double RestDelta = 0.5;

    private static readonly SpringSpec s_glideSpring = ArcMotionPresets.Snappy;
    private static readonly SpringSpec s_bandSpring = SpringSpec.FromVisual(0.5, 0.1);
    private static readonly TimeSpan s_powerHorizon = TimeSpan.FromSeconds(Power);
    private static readonly MotionSpec s_digitMotion = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);
    private static readonly DragOptions s_dragOptions = new() { Axis = DragAxis.Horizontal, Threshold = TapSlop };
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The visible label; it also names the control.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RulerSlider, string?>(nameof(Label));

    /// <summary>The unit shown after the readout and spoken with the value, such as "cm".</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<RulerSlider, string?>(nameof(Unit));

    /// <summary>Small ticks between labelled ticks.</summary>
    public static readonly StyledProperty<int> MajorEveryProperty =
        AvaloniaProperty.Register<RulerSlider, int>(nameof(MajorEvery), 10);

    /// <summary>Pixels between small ticks. Wider spacing gives finer control.</summary>
    public static readonly StyledProperty<double> TickSpacingProperty =
        AvaloniaProperty.Register<RulerSlider, double>(nameof(TickSpacing), 10);

    /// <summary>Formats the readout, the tick labels and the spoken value (default: en-US with the decimals of the step or the minimum).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<RulerSlider, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Raised once the tape comes to rest after a drag or a flick, and at once for a tap or a key.</summary>
    public static readonly RoutedEvent<SliderValueCommittedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<RulerSlider, SliderValueCommittedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ValueCommittedEvent"/>
    public event EventHandler<SliderValueCommittedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="UnitProperty"/>
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <inheritdoc cref="MajorEveryProperty"/>
    public int MajorEvery { get => GetValue(MajorEveryProperty); set => SetValue(MajorEveryProperty, value); }

    /// <inheritdoc cref="TickSpacingProperty"/>
    public double TickSpacing { get => GetValue(TickSpacingProperty); set => SetValue(TickSpacingProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <summary>A flick's decay as a progress curve: the remaining distance halves every 0.69·τ and ends at the rest threshold.</summary>
    private sealed class CoastEasing : Easing
    {
        private readonly double _rate;

        public CoastEasing(double rate)
        {
            _rate = rate;
        }

        public override double Ease(double progress) => 1 - Math.Exp(-progress * _rate);
    }

    // The value under the needle as a float, in UnitsPerTick. It runs in code because a release, a key and a tap hand
    // their speed to the next motion, and ticks are committed as they pass the needle: a ControlTheme transition does neither.
    private readonly MotionValue<double> _position;
    private readonly Action<double> _onMoved;
    private RulerTape? _tape;
    private SliderNumberText? _readout;
    private DragTracker? _drag;
    private LazyFocusRing? _focusRing;
    private DispatcherTimer? _wheelTimer;
    private double _latest;
    private double _dragFrom;
    private bool _coasting;
    private int _decimals = -1;
    private string? _format;

    static RulerSlider()
    {
        FocusableProperty.OverrideDefaultValue<RulerSlider>(true);
        SmallChangeProperty.OverrideDefaultValue<RulerSlider>(1);
    }

    public RulerSlider()
    {
        _onMoved = OnPositionChanged;
        _position = new MotionValue<double>(this, 0, MotionVectors.Double);
        _position.Changed += _onMoved;
    }

    protected override Type StyleKeyOverride => typeof(RulerSlider);

    #region Values

    private double Step => SmallChange > 0 ? SmallChange : 1;

    internal int Ticks => Math.Max(1, MajorEvery);

    internal double Spacing => Math.Max(1, TickSpacing);

    private int Decimals => _decimals >= 0 ? _decimals : _decimals = Math.Clamp(Math.Max(SliderMath.DecimalsOf(Step), SliderMath.DecimalsOf(Minimum)), 0, 15);

    private double High => Math.Max(Minimum, Maximum);

    private double Large => IsSet(LargeChangeProperty) && LargeChange > 0 ? LargeChange : Step * Ticks;

    private double PerPixel => UnitsPerTick / Spacing;

    private double ToPosition(double value) => (value - Minimum) / Step * UnitsPerTick;

    private double ToValue(double position) => Minimum + position / UnitsPerTick * Step;

    private double Snap(double input) => Math.Clamp(SliderMath.Snap(input, Minimum, Step, Decimals), Minimum, High);

    private double Current => Snap(Value);

    private double MaxPosition => ToPosition(High);

    private bool CanAnimate => IsLoaded && ArcMotion.IsFull(this);

    internal string FormatValue(double value) => ValueFormatter?.Invoke(value) ?? value.ToString(_format ??= "N" + Decimals.ToString(CultureInfo.InvariantCulture), s_culture);

    /// <summary>Tick labels only carry the decimals their own spacing needs, so a half-kilo scale reads 30, 35, 40.</summary>
    internal string FormatLabel(double value)
    {
        if (ValueFormatter is { } formatter)
        {
            return formatter(value);
        }
        var decimals = Math.Min(Decimals, SliderMath.DecimalsOf(Math.Round(Step * Ticks, Decimals)));
        return value.ToString(decimals == 0 ? "#,##0" : "#,##0." + new string('#', decimals), s_culture);
    }

    /// <summary>What the tape draws: pixels of tape travelled from the minimum, the tape's length, and the value of each labelled tick.</summary>
    internal double TapeOffset => _position.Value / PerPixel;

    internal double TapeLength => MaxPosition / PerPixel;

    internal double MajorValue(int index) => Math.Round(Minimum + index * Step * Ticks, Decimals);

    internal int MajorCount => (int)Math.Floor((High - Minimum) / (Step * Ticks) + 1e-9) + 1;

    private void Emit(double next)
    {
        if (_latest == next)
        {
            return;
        }
        _latest = next;
        SetCurrentValue(ValueProperty, next);
    }

    private void RaiseCommitted()
        => RaiseEvent(new SliderValueCommittedEventArgs(ValueCommittedEvent, _latest, _latest, _latest, false));

    private void StopFlight()
    {
        _position.Stop();
        _coasting = false;
    }

    /// <summary>Lands the tape on a value. Keys and taps glide on a spring that keeps any speed already in the tape.</summary>
    private void GlideTo(double next)
    {
        var velocity = _position.Velocity;
        StopFlight();
        Emit(next);
        if (CanAnimate)
        {
            _ = _position.AnimateTo(ToPosition(next), MotionSpec.Spring(s_glideSpring), velocity);
        }
        else
        {
            _position.Jump(ToPosition(next));
        }
        RaiseCommitted();
    }

    private void OnPositionChanged(double at)
    {
        // While the tape coasts after a flick, every tick it lands under the needle is the new value, so the readout rolls with it.
        if (_coasting)
        {
            Emit(Snap(ToValue(at)));
        }
        _tape?.InvalidateVisual();
    }

    /// <summary>Jumps to the current value: the scale changed, so what was on screen means something else now.</summary>
    private void Reset()
    {
        _decimals = -1;
        _format = null;
        StopFlight();
        _latest = Current;
        _position.Jump(ToPosition(_latest));
        _tape?.InvalidateScale();
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        var text = FormatValue(_latest);
        _readout?.SetText(text);
        AutomationProperties.SetItemStatus(this, string.IsNullOrEmpty(Unit) ? text : $"{text} {Unit}");
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _drag?.Dispose();
        _drag = null;
        if (_tape is not null)
        {
            _tape.Owner = null;
        }
        _tape = e.NameScope.Find<RulerTape>(TapePart);
        _readout = e.NameScope.Find<SliderNumberText>(ReadoutPart);
        if (_readout is { } readout)
        {
            // Arc's digit strip: a timed slide, solid digits cut at the line box, 1.1em apart.
            readout.Digits.WheelMotion = s_digitMotion;
            readout.Digits.GlyphPitch = 1.1;
            readout.Digits.Falloff = RollingDigitsFalloff.None;
        }
        if (_tape is { } tape)
        {
            tape.Owner = this;
            _drag = new DragTracker(tape, s_dragOptions) { CanBegin = press => press.Pointer.IsPrimary };
            _drag.Pressed += OnDragPressed;
            _drag.Started += OnDragStarted;
            _drag.Moved += OnDragMoved;
            _drag.Completed += OnDragCompleted;
            _drag.Cancelled += OnDragCancelled;
            _drag.Released += OnTapReleased;
        }
        _focusRing ??= new LazyFocusRing(this, RulerSliderTokens.Key(RulerSliderTokenKind.FocusRingMargin), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep22));
        _focusRing.Attach(e.NameScope.Find<Panel>(ViewportPart), below: true);
        Reset();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var current = Current;
            if (current != _latest)
            {
                // A value from outside (a reset, a linked field) moves the tape there; values emitted here are already under the needle.
                _latest = current;
                StopFlight();
                if (CanAnimate)
                {
                    _ = _position.AnimateTo(ToPosition(current), MotionSpec.Spring(s_glideSpring));
                }
                else
                {
                    _position.Jump(ToPosition(current));
                }
            }
            UpdateTexts();
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == SmallChangeProperty ||
                 change.Property == MajorEveryProperty || change.Property == TickSpacingProperty || change.Property == ValueFormatterProperty)
        {
            Reset();
        }
        else if (change.Property == UnitProperty)
        {
            UpdateTexts();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _wheelTimer?.Stop();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    #endregion

    #region Pointer

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        Focus(NavigationMethod.Pointer);
        // Grabbing a coasting tape catches it where it is, like a finger on a spinning wheel.
        _wheelTimer?.Stop();
        StopFlight();
        _dragFrom = _position.Value;
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e) => PseudoClasses.Set(DraggingPseudoClass, true);

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        var perPixel = PerPixel;
        var rawPixels = _dragFrom / perPixel - e.Offset.X;
        var lengthPixels = TapeLength;
        var edge = Math.Clamp(rawPixels, 0, lengthPixels);
        var placed = ArcMotion.IsFull(this) ? RubberBand.Clamp(rawPixels, 0, lengthPixels, Stretch) : edge;
        _position.Jump(placed * perPixel);
        Emit(Snap(ToValue(edge * perPixel)));
    }

    private void OnDragCompleted(object? sender, DragGestureEventArgs e)
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        Release(-e.Velocity.X * PerPixel);
    }

    private void OnDragCancelled(object? sender, DragGestureEventArgs e)
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        Release(0);
    }

    /// <summary>A press that never became a drag: a tap glides the tape so the tapped tick comes to the needle.</summary>
    private void OnTapReleased(object? sender, DragGestureEventArgs e)
    {
        var moved = e.Position - e.Start;
        if (_tape is not { } tape || moved.X * moved.X + moved.Y * moved.Y >= TapSlop * TapSlop ||
            TopLevel.GetTopLevel(tape) is not { } root || root.TranslatePoint(e.Position, tape) is not { } at)
        {
            return;
        }
        GlideTo(Snap(ToValue(_position.Value + (at.X - tape.Bounds.Width / 2) * PerPixel)));
    }

    /// <summary>The share of the finger's speed a stretched tape keeps: the slope of the rubber band at this stretch.</summary>
    private double StretchSlope(double release)
    {
        var over = release < 0 ? -release : release - MaxPosition;
        if (over <= 0)
        {
            return 1;
        }
        // The band's curve is s = (1 − 1/(x·f/L + 1))·L, so its slope in terms of the stretch itself is f·(1 − s/L)².
        var remaining = Math.Max(0, 1 - over / PerPixel / Stretch);
        return RubberBand.DefaultFactor * remaining * remaining;
    }

    /// <summary>Lets the tape go with <paramref name="velocity"/> in position units per second.</summary>
    internal void Release(double velocity)
    {
        var release = _position.Value;
        if (!CanAnimate)
        {
            var next = Snap(ToValue(release));
            _position.Jump(ToPosition(next));
            Emit(next);
            RaiseCommitted();
            return;
        }
        _coasting = true;
        velocity *= StretchSlope(release);
        var max = MaxPosition;
        if (release < 0 || release > max)
        {
            // Past an end the band pulls the tape home with the speed it was thrown at.
            _ = LandAfter(MomentumProjector.SettleTo(_position, Math.Clamp(release, 0, max), velocity, s_bandSpring));
            return;
        }
        // A scroll-like throw that eases onto the tick it would land on.
        var target = ToPosition(Snap(ToValue(release + MomentumProjector.Project(velocity, s_powerHorizon))));
        var distance = Math.Abs(target - release);
        if (distance <= RestDelta)
        {
            _position.Jump(target);
            Land();
            return;
        }
        var rate = Math.Log(distance / RestDelta);
        _ = LandAfter(_position.AnimateTo(target, MotionSpec.Tween(TimeSpan.FromSeconds(TimeConstant * rate), new CoastEasing(rate))));
    }

    private async Task LandAfter(Task<bool> flight)
    {
        if (await flight)
        {
            Land();
        }
    }

    private void Land()
    {
        _coasting = false;
        Emit(Snap(ToValue(_position.Value)));
        RaiseCommitted();
    }

    /// <summary>Horizontal trackpad scrolling moves the tape; vertical scrolling is left to the page.</summary>
    internal void OnTapeWheel(PointerWheelEventArgs e)
    {
        if (Math.Abs(e.Delta.X) <= Math.Abs(e.Delta.Y))
        {
            return;
        }
        e.Handled = true;
        StopFlight();
        // Avalonia's delta points the way the content moves; Arc's the way the view moves.
        var next = Math.Clamp(_position.Value - e.Delta.X * WheelPixels * PerPixel, 0, MaxPosition);
        _position.Jump(next);
        Emit(Snap(ToValue(next)));
        _wheelTimer ??= CreateWheelTimer();
        _wheelTimer.Stop();
        _wheelTimer.Start();
    }

    private DispatcherTimer CreateWheelTimer()
    {
        var timer = new DispatcherTimer { Interval = WheelSettle };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            GlideTo(Snap(ToValue(_position.Value)));
        };
        return timer;
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        var now = _latest;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        double wanted;
        switch (e.Key)
        {
            case Key.Right or Key.Up:
                wanted = now + (shift ? Large : Step);
                break;
            case Key.Left or Key.Down:
                wanted = now - (shift ? Large : Step);
                break;
            case Key.PageUp:
                wanted = now + Large;
                break;
            case Key.PageDown:
                wanted = now - Large;
                break;
            case Key.Home:
                wanted = Minimum;
                break;
            case Key.End:
                wanted = High;
                break;
            default:
                return;
        }
        e.Handled = true;
        var next = Snap(wanted);
        if (next != now)
        {
            GlideTo(next);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new RulerSliderAutomationPeer(this);

    private sealed class RulerSliderAutomationPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public RulerSliderAutomationPeer(RulerSlider owner) : base(owner)
        {
        }

        private RulerSlider Ruler => (RulerSlider)Owner;

        public bool IsReadOnly => !Ruler.IsEffectivelyEnabled;
        public double Minimum => Ruler.Minimum;
        public double Maximum => Ruler.Maximum;
        public double Value => Ruler._latest;
        public double SmallChange => Ruler.Step;
        public double LargeChange => Ruler.Large;

        public void SetValue(double value) => Ruler.GlideTo(Ruler.Snap(value));

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => base.GetNameCore() ?? Ruler.Label;
    }

    // Read by tests.
    internal RulerTape? Tape => _tape;

    internal double ShownValue => ToValue(_position.Value);

    internal bool IsCoasting => _coasting;

    internal SliderNumberText? Readout => _readout;
}
