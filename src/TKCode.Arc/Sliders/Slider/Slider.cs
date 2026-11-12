using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using System.Collections.Specialized;
using System.Globalization;
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
[PseudoClasses(DraggingPseudoClass, RangePseudoClass, MarksPseudoClass, HeaderlessPseudoClass)]
public class Slider : RangeBase, ISliderRailOwner, ISliderThumbHost, ISliderMarkHost
{
    public const string RailPart = "PART_Rail";
    public const string MarksPart = "PART_Marks";
    public const string ReadoutLowPart = "PART_ReadoutLow";
    public const string ReadoutHighPart = "PART_ReadoutHigh";
    internal const string ReadoutPart = "PART_Readout";
    internal const string ReadoutDashPart = "PART_ReadoutDash";
    public const string DraggingPseudoClass = ":dragging";
    public const string RangePseudoClass = ":range";
    public const string MarksPseudoClass = ":marks";
    /// <summary>Set while there is neither a <see cref="Label"/> nor a visible readout; the theme then collapses the header row.</summary>
    public const string HeaderlessPseudoClass = ":headerless";

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

    /// <summary>
    /// Diameter of the thumbs in device-independent pixels. The theme sets it from the <c>SliderToken</c>; set it on the
    /// control, or from a style, to size the thumbs of one slider.
    /// </summary>
    public static readonly StyledProperty<double> ThumbDiameterProperty =
        AvaloniaProperty.Register<Slider, double>(nameof(ThumbDiameter), 22);

    /// <summary>Thickness of the track in device-independent pixels. The theme sets it from the <c>SliderToken</c>.</summary>
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<Slider, double>(nameof(TrackThickness), 6);

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
    /// <inheritdoc cref="ThumbDiameterProperty"/>
    public double ThumbDiameter { get => GetValue(ThumbDiameterProperty); set => SetValue(ThumbDiameterProperty, value); }
    /// <inheritdoc cref="TrackThicknessProperty"/>
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }
    /// <inheritdoc cref="LowerThumbNameProperty"/>
    public string? LowerThumbName { get => GetValue(LowerThumbNameProperty); set => SetValue(LowerThumbNameProperty, value); }
    /// <inheritdoc cref="UpperThumbNameProperty"/>
    public string? UpperThumbName { get => GetValue(UpperThumbNameProperty); set => SetValue(UpperThumbNameProperty, value); }
    public object? StartContent { get => GetValue(StartContentProperty); set => SetValue(StartContentProperty, value); }
    /// <inheritdoc cref="EndContentProperty"/>
    public object? EndContent { get => GetValue(EndContentProperty); set => SetValue(EndContentProperty, value); }

    private IList<SliderMark>? _marks;
    // Marks may be a view model's list, and their properties may be bound: they are observed only while the slider is
    // shown, and re-read when it comes back.
    private ObservedItems? _observedMarks;
    private bool _watching;

    /// <summary>Ticks on the track; labeled marks also print under it and are clickable.</summary>
    public IList<SliderMark> Marks
    {
        get => _marks ??= new AvaloniaList<SliderMark>();
        set
        {
            var next = value ?? new AvaloniaList<SliderMark>();
            if (!EqualityComparer<IList<SliderMark>>.Default.Equals(_marks!, next))
            {
                var old = _marks;
                _marks = next;
                RaisePropertyChanged(MarksProperty, old!, next);
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
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Lag { get; set; }
        public MotionValue<double>? Lift { get; set; }
        public double Goal { get; set; } = double.NaN;
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

    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");
    private readonly ThumbMotion[] _thumbs = [new ThumbMotion(), new ThumbMotion()];
    private readonly double[] _values = new double[2];
    private readonly Action<double> _onMoved;
    private SliderVelocityTracker? _tracker;
    private DragState? _dragState;
    private bool _dragActive;
    private SliderRail? _rail;
    private SliderMarkLabels? _markLabels;
    private Panel? _readout;
    private SliderNumberText? _readoutLow;
    // The dash and the high readout are built the first time the slider is a range.
    private TextBlock? _readoutDash;
    private SliderNumberText? _readoutHigh;
    // Built with their bubbles, the first time a bubble shows.
    private readonly SliderNumberText?[] _bubbleTexts = new SliderNumberText?[2];
    // During a drag the readouts, bubbles and spoken values are refreshed once per frame, not once per pointer event.
    private bool _textsPosted;
    private Action? _flushTexts;
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
        // One cached delegate for every thumb motion value instead of one capturing lambda each.
        _onMoved = OnMoved;
        UpdatePseudoClasses();
    }

    /// <summary>Creates a thumb's motion values the first time they are needed: a never-shown slider allocates none.</summary>
    private void EnsureThumbMotion(int index)
    {
        var thumb = _thumbs[index];
        if (thumb.Position is not null)
        {
            return;
        }
        var position = new MotionValue<double>(this, 0, MotionVectors.Double);
        var lag = new MotionValue<double>(this, 0, MotionVectors.Double);
        var lift = new MotionValue<double>(this, 1, MotionVectors.Double);
        position.Changed += _onMoved;
        lag.Changed += _onMoved;
        lift.Changed += _onMoved;
        thumb.Position = position;
        thumb.Lag = lag;
        thumb.Lift = lift;
    }

    protected override Type StyleKeyOverride => typeof(Slider);

    #region Values

    private int Count => IsRange ? 2 : 1;

    private double Span => Maximum - Minimum is var span && span > 0 ? span : 1;

    private double Step => SmallChange > 0 ? SmallChange : 1;

    // Decimals and the "N" format string are derived from Minimum and SmallChange; both change rarely, so they are
    // cached and invalidated in OnPropertyChanged.
    private int _decimals = -1;
    private string? _format;

    private int Decimals => _decimals >= 0 ? _decimals : _decimals = Math.Max(SliderMath.DecimalsOf(Step), SliderMath.DecimalsOf(Minimum));

    private string FormatString => _format ??= "N" + Math.Clamp(Decimals, 0, 15);

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

    internal string FormatValue(double value) => ValueFormatter?.Invoke(value) ?? value.ToString(FormatString, s_culture);

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
            if (thumb.Position is not { } position || thumb.Lag is not { } lag)
            {
                // No template yet: the values are read and ApplyTemplate will place the thumbs.
                continue;
            }
            var target = ToPosition(_values[i]);
            if ((_dragActive && _dragState!.Index == i) || thumb.Goal == target)
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
            _ = position.AnimateTo(target, MotionSpec.Spring(ThumbSpring));
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
        _markLabels?.SyncMarkStates();
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

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rail = e.NameScope.Find<SliderRail>(RailPart);
        _markLabels = e.NameScope.Find<SliderMarkLabels>(MarksPart);
        _readout = e.NameScope.Find<Panel>(ReadoutPart);
        _readoutLow = e.NameScope.Find<SliderNumberText>(ReadoutLowPart);
        _readoutDash = null;
        _readoutHigh = null;
        if (_rail is not null)
        {
            EnsureThumbMotion(0);
            EnsureThumbMotion(1);
            _rail.Slider = this;
            _rail.Setup(this, this, 2, CreateBubbleText, bubble =>
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
        UpdateRangeReadout();
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
            _decimals = -1;
            _format = null;
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
            if (change.Property == LabelProperty)
            {
                UpdateHeaderPseudoClass();
            }
        }
        else if (change.Property == IsValueVisibleProperty)
        {
            UpdateHeaderPseudoClass();
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            UpdateThumbs();
        }
    }

    /// <summary>Listens to the current marks while shown (and to nothing otherwise).</summary>
    private void WatchMarks()
    {
        var observed = _observedMarks ??= new ObservedItems(_ => UpdateMarks());
        observed.Observe(_watching ? Marks : null);
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
        UpdateHeaderPseudoClass();
        UpdateRangeReadout();
    }

    /// <summary>The header row (label and readout) is empty without a label and with the readout hidden; the theme then takes it out of the layout.</summary>
    private void UpdateHeaderPseudoClass() =>
        PseudoClasses.Set(HeaderlessPseudoClass, string.IsNullOrEmpty(Label) && !IsValueVisible);

    /// <summary>
    /// The dash and the high readout exist only once the slider has been a range (most sliders never are); afterwards
    /// they hide and show with <see cref="IsRange"/>, as the template's hidden parts did.
    /// </summary>
    private void UpdateRangeReadout()
    {
        if (_readout is null)
        {
            return;
        }
        if (_readoutHigh is null)
        {
            if (!IsRange)
            {
                return;
            }
            _readoutDash = new TextBlock
            {
                Name = ReadoutDashPart,
                Text = "–",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            ArcTokens.Bind(_readoutDash, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            _readoutHigh = new SliderNumberText { Name = ReadoutHighPart, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            // Before it is mounted, so the first value shows at once instead of rolling in.
            _readoutHigh.SetText(FormatValue(_values[1]));
            _readout.Children.Add(_readoutDash);
            _readout.Children.Add(_readoutHigh);
        }
        _readoutDash!.IsVisible = IsRange;
        _readoutHigh.IsVisible = IsRange;
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

    private SliderNumberText CreateBubbleText(int index)
    {
        var text = _bubbleTexts[index] ??= new SliderNumberText();
        text.SetText(FormatValue(_values[index]));
        return text;
    }

    private void UpdateTexts()
    {
        var low = FormatValue(_values[0]);
        // Without a range the two values are the same (ReadValues), so reuse the low string.
        var high = IsRange ? FormatValue(_values[1]) : low;
        _readoutLow?.SetText(low);
        _readoutHigh?.SetText(high);
        _bubbleTexts[0]?.SetText(low);
        _bubbleTexts[1]?.SetText(high);
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

    private void OnMoved(double _) => OnThumbMoved();

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
            rail.Thumbs[i].Place(x, motion.Lift?.Value ?? 1);
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
            _rail.ShowBubble(i, shown);
        }
    }

    private void SetLifted(int index)
    {
        for (var i = 0; i < 2; i++)
        {
            var target = i == index ? 1.16 : 1.0;
            var lift = _thumbs[i].Lift;
            if (lift is not null && lift.Target != target)
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
        var drag = _dragState ??= new DragState();
        drag.Pointer = e.Pointer;
        drag.Index = -1;
        drag.Grab = 0;
        drag.StartX = x;
        drag.Raw = at;
        var tracker = _tracker ??= new SliderVelocityTracker();
        tracker.Reset();
        if (grabbed == -2)
        {
            // Stacked range thumbs wait for the first movement to decide which one the pointer means.
            FocusFromPointer(_lastActive);
            return;
        }
        var index = grabbed >= 0 ? grabbed : Nearest(at);
        Begin(rail, index, at, press: grabbed < 0, e.Timestamp);
        tracker.Add(e.Timestamp, _thumbs[index].Position!.Value);
    }

    /// <summary>Starts tracking one thumb. A pressed track springs the thumb over; a grabbed thumb keeps its grab offset.</summary>
    private void Begin(SliderRailBase rail, int index, double at, bool press, ulong time)
    {
        EnsureThumbMotion(index);
        var thumb = _thumbs[index];
        var drag = _dragState!;
        var from = thumb.Shown;
        drag.Index = index;
        drag.Grab = press ? 0 : at - from;
        thumb.Lag!.Jump(0);
        thumb.Position!.Jump(from);
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
        var drag = _dragState!;
        var index = drag.Index;
        if (index < 0)
        {
            return;
        }
        var thumb = _thumbs[index];
        var position = thumb.Position!;
        var lag = thumb.Lag!;
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
            lag.Jump(from - placed);
            _ = lag.AnimateTo(0, MotionSpec.Spring(ThumbSpring));
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
        var drag = _dragState;
        if (!_dragActive || drag is null || e.Pointer != drag.Pointer)
        {
            return;
        }
        var x = e.GetPosition(rail).X;
        var at = rail.ToPosition(x);
        if (drag.Index < 0)
        {
            var dx = x - drag.StartX;
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
        if (!_dragActive || e.Pointer != _dragState!.Pointer)
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
        var drag = _dragState!;
        var index = drag.Index;
        drag.Pointer = null;
        if (index < 0)
        {
            return;
        }
        var thumb = _thumbs[index];
        var position = thumb.Position!;
        var lag = thumb.Lag!;
        var width = rail.TrackWidth;
        // Velocity in track units per second; a release during the catch-up spring keeps that spring's speed too.
        var velocity = (release ? _tracker?.Velocity(time) ?? 0 : 0) + lag.Velocity;
        var stepPosition = Step / Span * SliderMath.Span;
        var carry = release && stepPosition / SliderMath.Span * width >= SnapPixels
            ? Math.Clamp(SliderMath.Project(velocity), -stepPosition, stepPosition)
            : 0;
        var next = Settle(index, ValueAt(drag.Raw + carry));
        var from = thumb.Shown;
        var target = ToPosition(next);
        lag.Jump(0);
        position.Jump(from);
        thumb.Goal = target;
        if (IsLoaded && ArcMotion.IsFull(this))
        {
            _ = position.AnimateTo(target, MotionSpec.Spring(ThumbSpring), velocity);
        }
        else
        {
            position.Jump(target);
        }
        Commit(index, next);
        drag.Index = -1;
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
        EnsureThumbMotion(index);
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
            if (toward != 0 && _rail is not null && IsLoaded && ArcMotion.IsFull(this) && !double.IsNaN(motion.Goal) && motion.Position is { } position)
            {
                var velocity = toward * BumpSpeed / _rail.TrackWidth * SliderMath.Span;
                _ = position.AnimateTo(motion.Goal, MotionSpec.Spring(KickSpring), velocity);
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

    internal bool IsBubbleShown(int index) => _rail?.IsBubbleShown(index) ?? false;

    internal SliderRail? Rail => _rail;
}
