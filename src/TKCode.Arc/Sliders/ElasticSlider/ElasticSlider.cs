using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Arc elastic slider: a single value slider made of something that gives. The thumb swells to 1.28× under the finger
/// (P24), the track stretches like rubber up to 26px past either end and springs back with the release velocity
/// (0.5s, bounce 0.42), the end icons are pushed outward with the stretch, a value tag hangs off the thumb and sways
/// with its speed (up to 11°), and marks act as detents the thumb clicks into within 7px. Arrows, Shift+arrows,
/// PageUp / PageDown and Home / End work as in a native range input; a key at a limit hits the wall and bounces.
/// <see cref="RangeBase.SmallChange"/> is the step the value snaps to.
/// </summary>
[TemplatePart(RailPart, typeof(Control))]
[TemplatePart(MarksPart, typeof(Control))]
[PseudoClasses(DraggingPseudoClass, TagPseudoClass, MarksPseudoClass)]
public class ElasticSlider : RangeBase, ISliderRailOwner, ISliderThumbHost, ISliderMarkHost
{
    public const string RailPart = "PART_Rail";
    public const string MarksPart = "PART_Marks";
    public const string StartIconPart = "PART_StartIcon";
    public const string EndIconPart = "PART_EndIcon";
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while the value tag rides the thumb; the readout steps back.</summary>
    public const string TagPseudoClass = ":tag";
    public const string MarksPseudoClass = ":marks";

    /// <summary>Pixels the track may stretch past a limit.</summary>
    internal const double Stretch = 26;

    /// <summary>Pixel reach of a detent.</summary>
    internal const double DetentPixels = 7;

    /// <summary>Speed (px/s) a key press at a limit hits the wall with.</summary>
    internal const double BumpSpeed = 260;

    /// <summary>Extra hit radius around the thumb (Arc's 12px <c>::after</c>).</summary>
    internal const double ThumbReach = 12;

    internal const double LiftedScale = 1.28;

    internal static readonly TimeSpan Linger = TimeSpan.FromMilliseconds(700);
    internal static readonly SpringSpec ThumbSpring = ArcMotionPresets.Snappy;

    /// <summary>The stretch lets go with more life than anything else: it is the one thing the user threw.</summary>
    internal static readonly SpringSpec SnapBack = SpringSpec.FromVisual(0.5, 0.42);

    private static readonly TimeSpan s_pulseDuration = TimeSpan.FromSeconds(0.34);
    private static readonly Easing s_linear = new LinearEasing();

    #region 公共属性定义

    /// <summary>Visible label; it also names the thumb.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ElasticSlider, string?>(nameof(Label));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<ElasticSlider, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Shows the value beside the label. The tag still appears while dragging or using keys.</summary>
    public static readonly StyledProperty<bool> IsValueVisibleProperty =
        AvaloniaProperty.Register<ElasticSlider, bool>(nameof(IsValueVisible), true);

    /// <summary>Icon before the track; pushed outward when the track is pulled past its start.</summary>
    public static readonly StyledProperty<object?> StartIconProperty =
        AvaloniaProperty.Register<ElasticSlider, object?>(nameof(StartIcon));

    /// <summary>Icon after the track; pushed outward when the track is pulled past its end.</summary>
    public static readonly StyledProperty<object?> EndIconProperty =
        AvaloniaProperty.Register<ElasticSlider, object?>(nameof(EndIcon));

    public static readonly DirectProperty<ElasticSlider, IList<SliderMark>> MarksProperty =
        AvaloniaProperty.RegisterDirect<ElasticSlider, IList<SliderMark>>(nameof(Marks), o => o.Marks, (o, v) => o.Marks = v);

    internal static readonly DirectProperty<ElasticSlider, string> ValueTextProperty =
        AvaloniaProperty.RegisterDirect<ElasticSlider, string>(nameof(ValueText), o => o.ValueText);

    public static readonly RoutedEvent<SliderValueCommittedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<ElasticSlider, SliderValueCommittedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }
    public bool IsValueVisible { get => GetValue(IsValueVisibleProperty); set => SetValue(IsValueVisibleProperty, value); }
    public object? StartIcon { get => GetValue(StartIconProperty); set => SetValue(StartIconProperty, value); }
    public object? EndIcon { get => GetValue(EndIconProperty); set => SetValue(EndIconProperty, value); }

    private IList<SliderMark> _marks = new AvaloniaList<SliderMark>();

    /// <summary>Detents: a dragged thumb clicks into each one and holds for a few pixels. Labels print under the track.</summary>
    public IList<SliderMark> Marks
    {
        get => _marks;
        set
        {
            var old = _marks;
            if (SetAndRaise(MarksProperty, ref _marks, value ?? new AvaloniaList<SliderMark>()))
            {
                if (old is INotifyCollectionChanged oldObservable)
                {
                    oldObservable.CollectionChanged -= OnMarksChanged;
                }
                if (_marks is INotifyCollectionChanged observable)
                {
                    observable.CollectionChanged += OnMarksChanged;
                }
                UpdateMarks();
            }
        }
    }

    private string _valueText = string.Empty;

    /// <summary>The formatted value (readout, tag and spoken value).</summary>
    internal string ValueText
    {
        get => _valueText;
        private set => SetAndRaise(ValueTextProperty, ref _valueText, value);
    }

    /// <summary>Runs once a drag is released, a key changes the value or a mark label is clicked.</summary>
    public event EventHandler<SliderValueCommittedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    #endregion

    private readonly MotionValue<double> _position;
    private readonly MotionValue<double> _lag;
    private readonly MotionValue<double> _over;
    private readonly MotionValue<double> _press;
    private readonly MotionValue<double> _lift;
    private readonly ElasticSliderSway _sway;
    private readonly SliderVelocityTracker _tracker = new();
    private readonly SliderVelocityTracker _overTracker = new();
    private readonly TranslateTransform _startShift = new();
    private readonly TranslateTransform _endShift = new();
    private readonly TextBlock _tagText = new();
    private MotionValue<double>?[] _pulses = [];
    private ElasticSliderRail? _rail;
    private SliderMarkLabels? _markLabels;
    private DispatcherTimer? _lingerTimer;
    private IPointer? _pointer;
    private double _grab;
    private double _edge;
    private int _detent = -1;
    private double _goal = double.NaN;
    private bool _dragging;
    private bool _keyed;
    private bool _linger;

    static ElasticSlider()
    {
        FocusableProperty.OverrideDefaultValue<ElasticSlider>(false);
        SmallChangeProperty.OverrideDefaultValue<ElasticSlider>(1);
    }

    public ElasticSlider()
    {
        _position = Create(0, OnMoved);
        _lag = Create(0, OnMoved);
        _over = Create(0, OnMoved);
        _press = Create(0, () => _rail?.InvalidateVisual());
        _lift = Create(1, Place);
        _sway = new ElasticSliderSway(this);
        _sway.Changed += Place;
        if (_marks is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnMarksChanged;
        }
        ValueText = FormatValue(Value);
    }

    private MotionValue<double> Create(double initial, Action changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => changed();
        return value;
    }

    protected override Type StyleKeyOverride => typeof(ElasticSlider);

    #region Values

    private double Span => Maximum - Minimum is var span && span > 0 ? span : 1;

    private double Step => SmallChange > 0 ? SmallChange : 1;

    private int Decimals => Math.Max(SliderMath.DecimalsOf(Step), SliderMath.DecimalsOf(Minimum));

    internal double LargeStep => IsSet(LargeChangeProperty) && LargeChange > 0
        ? LargeChange
        : SliderMath.DefaultLargeStep(Minimum, Maximum, Step, Decimals);

    internal double ToPosition(double value) => (value - Minimum) / Span * SliderMath.Span;

    private double ValueAt(double position) => Minimum + position / SliderMath.Span * Span;

    private double Snap(double value) => Math.Clamp(SliderMath.Snap(value, Minimum, Step, Decimals), Minimum, Maximum);

    internal string FormatValue(double value) => ValueFormatter?.Invoke(value) ?? SliderMath.Format(value, Decimals);

    /// <summary>Thumb position in track per-mille (committed position plus catch-up offset).</summary>
    internal double ShownPosition => _position.Value + _lag.Value;

    /// <summary>Rubber-band stretch in pixels, signed by side.</summary>
    internal double Over => _over.Value;

    /// <summary>Press thickening (0..1).</summary>
    internal double Press => _press.Value;

    /// <summary>The detent pop of tick <paramref name="index"/>: 1 → 2.1 → 1 over 0.34s, each half on the enter ease.</summary>
    internal double TickScale(int index)
    {
        if (index >= _pulses.Length || _pulses[index] is not { } pulse || !pulse.IsAnimating)
        {
            return 1;
        }
        var p = pulse.Value;
        return p < 0.5
            ? 1 + 1.1 * ArcEasings.Enter.Ease(p / 0.5)
            : 2.1 - 1.1 * ArcEasings.Enter.Ease((p - 0.5) / 0.5);
    }

    private void Emit(double next)
    {
        if (Value != next)
        {
            SetCurrentValue(ValueProperty, next);
        }
    }

    private void RaiseCommitted() => RaiseEvent(new SliderValueCommittedEventArgs(ValueCommittedEvent, Value, Value, Value, false));

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rail = e.NameScope.Find<ElasticSliderRail>(RailPart);
        _markLabels = e.NameScope.Find<SliderMarkLabels>(MarksPart);
        if (e.NameScope.Find<Control>(StartIconPart) is { } start)
        {
            start.RenderTransform = _startShift;
        }
        if (e.NameScope.Find<Control>(EndIconPart) is { } end)
        {
            end.RenderTransform = _endShift;
        }
        if (_rail is not null)
        {
            _rail.Slider = this;
            _rail.Setup(this, this, 1, _ => _tagText, bubble =>
            {
                bubble.EnterScale = 0.7;
                bubble.ExitScale = 0.85;
                bubble.ReducedEnter = TimeSpan.FromSeconds(0.15);
                // The tag hangs from below its bottom edge, so a swing pivots near the thumb.
                bubble.PivotY = 1.3;
            });
            _rail.Thumbs[0].LostFocus += (_, _) =>
            {
                _keyed = false;
                UpdateBubble();
            };
        }
        if (_markLabels is not null)
        {
            _markLabels.Host = this;
        }
        _goal = double.NaN;
        SyncThumb(animate: false);
        UpdateThumb();
        UpdateMarks();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            SyncThumb(animate: true);
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == SmallChangeProperty)
        {
            _goal = double.NaN;
            SyncThumb(animate: false);
            UpdateMarks();
        }
        else if (change.Property == ValueFormatterProperty)
        {
            UpdateText();
        }
        else if (change.Property == LabelProperty || change.Property == IsEffectivelyEnabledProperty)
        {
            UpdateThumb();
        }
    }

    /// <summary>Values from outside a drag (keys, bindings, a clicked mark) spring the thumb from wherever it is on screen.</summary>
    private void SyncThumb(bool animate)
    {
        UpdateText();
        var target = ToPosition(Math.Clamp(Value, Minimum, Maximum));
        if (_dragging || _goal == target)
        {
            _rail?.InvalidateVisual();
            _markLabels?.InvalidateVisual();
            return;
        }
        var placed = !double.IsNaN(_goal);
        _goal = target;
        var from = ShownPosition;
        _lag.Jump(0);
        if (!animate || !placed || !IsLoaded)
        {
            _position.Jump(target);
            _sway.Jump(target);
        }
        else
        {
            _position.Jump(from);
            _ = _position.AnimateTo(target, MotionSpec.Spring(ThumbSpring));
        }
        _rail?.InvalidateVisual();
        _markLabels?.InvalidateVisual();
    }

    private void UpdateText()
    {
        ValueText = FormatValue(Math.Clamp(Value, Minimum, Maximum));
        _tagText.Text = ValueText;
        if (_rail is not null)
        {
            AutomationProperties.SetItemStatus(_rail.Thumbs[0], ValueText);
        }
    }

    private void OnMarksChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateMarks();

    private void UpdateMarks()
    {
        _pulses = new MotionValue<double>?[_marks.Count];
        _markLabels?.Refresh();
        if (_markLabels is not null)
        {
            _markLabels.IsVisible = _markLabels.HasLabels;
        }
        PseudoClasses.Set(MarksPseudoClass, _markLabels?.HasLabels ?? false);
        _rail?.InvalidateVisual();
    }

    private void UpdateThumb()
    {
        if (_rail is null)
        {
            return;
        }
        var thumb = _rail.Thumbs[0];
        thumb.IsTabStop = IsEffectivelyEnabled;
        AutomationProperties.SetName(thumb, Label);
        UpdateText();
        Place();
    }

    #endregion

    #region Placement

    void ISliderRailOwner.OnRailArranged(SliderRailBase rail) => Place();

    private void OnMoved()
    {
        _sway.Follow(ShownPosition, IsLoaded && ArcMotion.IsFull(this) && _rail is not null);
        Place();
        _rail?.InvalidateVisual();
    }

    private void Place()
    {
        if (_rail is not { } rail || rail.Thumbs.Count == 0)
        {
            return;
        }
        var width = rail.TrackWidth;
        var over = _over.Value;
        rail.Thumbs[0].Place(ShownPosition / SliderMath.Span * width + over, _lift.Value);
        var full = ArcMotion.IsFull(this);
        var tagAt = full ? _sway.Position : ShownPosition;
        var angle = full ? Math.Clamp(-_sway.Velocity / SliderMath.Span * width * 0.009, -11, 11) : 0;
        rail.PlaceBubble(0, rail.TrackLeft + tagAt / SliderMath.Span * width, over, angle);
        var startShift = Math.Min(0, over);
        var endShift = Math.Max(0, over);
        if (_startShift.X != startShift)
        {
            _startShift.X = startShift;
        }
        if (_endShift.X != endShift)
        {
            _endShift.X = endShift;
        }
    }

    private void UpdateBubble()
    {
        var shown = _dragging || _keyed || _linger;
        _rail?.Bubbles[0].Show(shown);
        PseudoClasses.Set(TagPseudoClass, shown);
        PseudoClasses.Set(DraggingPseudoClass, _dragging);
    }

    private void HoldBubble()
    {
        _lingerTimer ??= CreateLingerTimer();
        _lingerTimer.Stop();
        _linger = true;
        _lingerTimer.Start();
        UpdateBubble();
    }

    private DispatcherTimer CreateLingerTimer()
    {
        var timer = new DispatcherTimer { Interval = Linger };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _linger = false;
            UpdateBubble();
        };
        return timer;
    }

    /// <summary>A detent answers with a small pop of its tick (Arc also taps the vibration motor on touch; not mapped).</summary>
    private void Pulse(int markIndex)
    {
        if (markIndex < 0 || markIndex >= _pulses.Length || !IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        var pulse = _pulses[markIndex] ??= Create(0, () => _rail?.InvalidateVisual());
        pulse.Jump(0);
        _ = pulse.AnimateTo(1, MotionSpec.Tween(s_pulseDuration, s_linear));
    }

    #endregion

    #region Pointer

    private int MarkIndexOf(double value)
    {
        for (var i = 0; i < _marks.Count; i++)
        {
            if (_marks[i].Value == value)
            {
                return i;
            }
        }
        return -1;
    }

    private int DetentAt(double position, double width)
    {
        var best = -1;
        var gap = DetentPixels + 1;
        for (var i = 0; i < _marks.Count; i++)
        {
            var value = _marks[i].Value;
            if (value < Minimum || value > Maximum)
            {
                continue;
            }
            var distance = Math.Abs((position - ToPosition(value)) / SliderMath.Span * width);
            if (distance <= DetentPixels && distance < gap)
            {
                best = i;
                gap = distance;
            }
        }
        return best;
    }

    /// <summary>Moves the thumb to a new resting spot through a spring on the catch-up offset, so the jump reads as travel.</summary>
    private void Glide(double placed, bool full)
    {
        var from = ShownPosition;
        _position.Jump(placed);
        if (!full)
        {
            _lag.Jump(0);
            return;
        }
        _lag.Jump(from - placed);
        _ = _lag.AnimateTo(0, MotionSpec.Spring(ThumbSpring));
    }

    void ISliderRailOwner.OnRailPressed(SliderRailBase rail, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.Pointer.IsPrimary || !e.GetCurrentPoint(rail).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var x = e.GetPosition(rail).X;
        var at = rail.ToPosition(x);
        var from = ShownPosition;
        var grabbed = Math.Abs(x - rail.ToPixel(from)) <= rail.ThumbDiameter / 2 + ThumbReach;
        e.Pointer.Capture(rail);
        e.Handled = true;
        _lag.Jump(0);
        _position.Jump(from);
        _pointer = e.Pointer;
        _grab = grabbed ? at - from : 0;
        _detent = -1;
        _edge = from;
        _tracker.Reset();
        _overTracker.Reset();
        _dragging = true;
        _lingerTimer?.Stop();
        _linger = false;
        var thumb = rail.Thumbs[0];
        if (!thumb.IsFocused)
        {
            thumb.Focus(NavigationMethod.Pointer);
        }
        // Springs jump under reduced motion (Arc: duration 0), so the thumb still swells, just at once.
        _ = _press.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        _ = _lift.AnimateTo(LiftedScale, MotionSpec.Spring(ArcSpring.Snappy));
        UpdateBubble();
        // A grabbed thumb stays put under the finger; a pressed track pulls the thumb over on a spring.
        Follow(rail, grabbed ? from + _grab : at, e.Timestamp, first: true);
    }

    private void Follow(SliderRailBase rail, double at, ulong time, bool first = false)
    {
        var width = rail.TrackWidth;
        var full = IsLoaded && ArcMotion.IsFull(this);
        var raw = at - _grab;
        var edge = Math.Clamp(raw, 0, SliderMath.Span);
        var over = full ? SliderMath.Rubber((raw - edge) / SliderMath.Span * width, Stretch) : 0;
        _over.Jump(over);
        _overTracker.Add(time, over);
        var detent = DetentAt(edge, width);
        var placed = detent >= 0 ? ToPosition(_marks[detent].Value) : edge;
        if (first || detent != _detent)
        {
            if (detent >= 0 && !first)
            {
                Pulse(detent);
            }
            Glide(placed, full);
        }
        else
        {
            _position.Jump(placed);
        }
        _detent = detent;
        _edge = edge;
        _tracker.Add(time, placed);
        Emit(detent >= 0 ? _marks[detent].Value : Snap(ValueAt(edge)));
    }

    void ISliderRailOwner.OnRailMoved(SliderRailBase rail, PointerEventArgs e)
    {
        if (_dragging && e.Pointer == _pointer)
        {
            Follow(rail, rail.ToPosition(e.GetPosition(rail).X), e.Timestamp);
        }
    }

    void ISliderRailOwner.OnRailReleased(SliderRailBase rail, PointerReleasedEventArgs e)
    {
        if (_dragging && e.Pointer == _pointer)
        {
            End(e.Timestamp, release: true);
            e.Pointer.Capture(null);
        }
    }

    void ISliderRailOwner.OnRailCaptureLost(SliderRailBase rail)
    {
        if (_dragging)
        {
            End(0, release: false);
        }
    }

    private void End(ulong time, bool release)
    {
        _dragging = false;
        _pointer = null;
        var velocity = (release ? _tracker.Velocity(time) : 0) + _lag.Velocity;
        var overVelocity = release ? _overTracker.Velocity(time) : 0;
        var next = _detent >= 0 ? _marks[_detent].Value : Snap(ValueAt(_edge));
        var from = ShownPosition;
        var target = ToPosition(next);
        _lag.Jump(0);
        _position.Jump(from);
        _goal = target;
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _position.AnimateTo(target, MotionSpec.Spring(ThumbSpring), velocity);
            _ = _over.AnimateTo(0, MotionSpec.Spring(SnapBack), overVelocity);
            _ = _press.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _lift.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _position.Jump(target);
            _over.Jump(0);
            _press.Jump(0);
            _lift.Jump(1);
        }
        Emit(next);
        HoldBubble();
        RaiseCommitted();
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not SliderThumb thumb || !IsEffectivelyEnabled)
        {
            base.OnKeyDown(e);
            return;
        }
        var now = Value;
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
                wanted = Minimum;
                break;
            case Key.End:
                wanted = Maximum;
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
        _keyed = true;
        thumb.ShowRing = true;
        UpdateBubble();
        var next = Snap(wanted);
        if (next == now)
        {
            // At a limit the track still stretches toward the press and snaps back, so the key visibly lands on something.
            var toward = Math.Sign(wanted - now);
            if (toward != 0 && IsLoaded && ArcMotion.IsFull(this))
            {
                _ = _over.AnimateTo(0, MotionSpec.Spring(SnapBack), toward * BumpSpeed);
            }
            return;
        }
        Emit(next);
        Pulse(MarkIndexOf(next));
        RaiseCommitted();
    }

    #endregion

    #region Marks and automation

    double ISliderMarkHost.MarkInset => _rail?.ThumbDiameter / 2 ?? 12;

    bool ISliderMarkHost.IsMarkOn(double value) => value == Value;

    void ISliderMarkHost.ActivateMark(double value)
    {
        if (!IsEffectivelyEnabled || value == Value)
        {
            return;
        }
        Emit(value);
        Pulse(MarkIndexOf(value));
        RaiseCommitted();
    }

    double ISliderThumbHost.GetThumbMinimum(int index) => Minimum;

    double ISliderThumbHost.GetThumbMaximum(int index) => Maximum;

    double ISliderThumbHost.GetThumbValue(int index) => Value;

    double ISliderThumbHost.SmallStep => Step;

    double ISliderThumbHost.LargeStep => LargeStep;

    bool ISliderThumbHost.IsThumbReadOnly => !IsEffectivelyEnabled;

    void ISliderThumbHost.SetThumbValue(int index, double value)
    {
        Emit(Snap(value));
        RaiseCommitted();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ElasticSliderGroupAutomationPeer(this);

    private sealed class ElasticSliderGroupAutomationPeer : ControlAutomationPeer
    {
        public ElasticSliderGroupAutomationPeer(Control owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }

    #endregion

    // Read by tests.
    internal bool IsTagShown => _rail?.Bubbles[0].IsShown ?? false;

    internal double ThumbScale => _lift.Value;

    internal ElasticSliderRail? Rail => _rail;
}
