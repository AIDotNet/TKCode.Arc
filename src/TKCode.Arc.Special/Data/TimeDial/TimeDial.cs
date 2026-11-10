using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="TimeDial.ValueCommitted"/>.</summary>
public sealed class TimeDialValueCommittedEventArgs : RoutedEventArgs
{
    internal TimeDialValueCommittedEventArgs(RoutedEvent routedEvent, TimeSpan value) : base(routedEvent)
    {
        Value = value;
    }

    /// <summary>The value the person settled on.</summary>
    public TimeSpan Value { get; }
}

/// <summary>
/// A round dial for picking a time of day or a length of time: drag the handle around the face, press anywhere on the
/// track to jump there, or use the arrow keys. The value snaps to <see cref="Step"/>, the handle follows on a spring, and
/// the readout in the middle shows the value.
/// </summary>
/// <remarks>
/// In <see cref="TimeDialMode.Time"/> one turn is twelve hours: carrying the handle past 12 o'clock switches between AM
/// and PM, and a click on the readout (or the A and P keys) switches too. In <see cref="TimeDialMode.Duration"/> one turn
/// is <see cref="TurnLength"/>, the track fills from 12 o'clock, and the handle can go round more than once up to
/// <see cref="Maximum"/>.
/// </remarks>
[PseudoClasses(PressedPseudoClass)]
public class TimeDial : TemplatedControl
{
    internal const string PressedPseudoClass = ":pressed";

    /// <summary>The picked value: a time of day (0:00 to 23:59) or a length of time, depending on <see cref="Mode"/>.</summary>
    public static readonly StyledProperty<TimeSpan> ValueProperty =
        AvaloniaProperty.Register<TimeDial, TimeSpan>(nameof(Value), defaultBindingMode: BindingMode.TwoWay,
            coerce: static (o, v) => ((TimeDial)o).CoerceTime(v));

    /// <summary>Whether the dial picks a time of day or a length of time.</summary>
    public static readonly StyledProperty<TimeDialMode> ModeProperty =
        AvaloniaProperty.Register<TimeDial, TimeDialMode>(nameof(Mode));

    /// <summary>The value snaps to multiples of this while dragging, and the arrow keys move by it. Default 5 minutes.</summary>
    public static readonly StyledProperty<TimeSpan> StepProperty =
        AvaloniaProperty.Register<TimeDial, TimeSpan>(nameof(Step), TimeSpan.FromMinutes(5));

    /// <summary>What Page Up and Page Down move by. Default 1 hour.</summary>
    public static readonly StyledProperty<TimeSpan> LargeStepProperty =
        AvaloniaProperty.Register<TimeDial, TimeSpan>(nameof(LargeStep), TimeSpan.FromHours(1));

    /// <summary>The shortest duration (duration mode only). Default zero.</summary>
    public static readonly StyledProperty<TimeSpan> MinimumProperty =
        AvaloniaProperty.Register<TimeDial, TimeSpan>(nameof(Minimum));

    /// <summary>The longest duration (duration mode only). Default 2 hours.</summary>
    public static readonly StyledProperty<TimeSpan> MaximumProperty =
        AvaloniaProperty.Register<TimeDial, TimeSpan>(nameof(Maximum), TimeSpan.FromHours(2));

    /// <summary>How much one full turn of the handle adds (duration mode only). Default 1 hour.</summary>
    public static readonly StyledProperty<TimeSpan> TurnLengthProperty =
        AvaloniaProperty.Register<TimeDial, TimeSpan>(nameof(TurnLength), TimeSpan.FromHours(1));

    /// <summary>Shows a time of day as 19:05 instead of 7:05 PM (time mode only).</summary>
    public static readonly StyledProperty<bool> Is24HourProperty =
        AvaloniaProperty.Register<TimeDial, bool>(nameof(Is24Hour));

    /// <summary>Accessible name of the dial, such as "Alarm time" or "Focus length".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<TimeDial, string>(nameof(Label), "Time");

    /// <summary>Text for times before noon.</summary>
    public static readonly StyledProperty<string> AmTextProperty =
        AvaloniaProperty.Register<TimeDial, string>(nameof(AmText), "AM");

    /// <summary>Text for times from noon on.</summary>
    public static readonly StyledProperty<string> PmTextProperty =
        AvaloniaProperty.Register<TimeDial, string>(nameof(PmText), "PM");

    /// <summary>Unit shown under a duration of an hour or more.</summary>
    public static readonly StyledProperty<string> HoursTextProperty =
        AvaloniaProperty.Register<TimeDial, string>(nameof(HoursText), "hr");

    /// <summary>Unit shown under a duration of less than an hour.</summary>
    public static readonly StyledProperty<string> MinutesTextProperty =
        AvaloniaProperty.Register<TimeDial, string>(nameof(MinutesText), "min");

    /// <summary>Raised when a drag ends, a key moves the value, or AM and PM are switched.</summary>
    public static readonly RoutedEvent<TimeDialValueCommittedEventArgs> ValueCommittedEvent =
        RoutedEvent.Register<TimeDial, TimeDialValueCommittedEventArgs>(nameof(ValueCommitted), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> DialSizeProperty =
        AvaloniaProperty.Register<TimeDial, double>("DialSize", 240);
    internal static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<TimeDial, double>("TrackThickness", 10);
    internal static readonly StyledProperty<double> TrackInsetProperty =
        AvaloniaProperty.Register<TimeDial, double>("TrackInset", 16);
    internal static readonly StyledProperty<double> HandleRadiusProperty =
        AvaloniaProperty.Register<TimeDial, double>("HandleRadius", 13);
    internal static readonly StyledProperty<double> HandlePressScaleProperty =
        AvaloniaProperty.Register<TimeDial, double>("HandlePressScale", 1.12);
    internal static readonly StyledProperty<double> MinorTickLengthProperty =
        AvaloniaProperty.Register<TimeDial, double>("MinorTickLength", 4);
    internal static readonly StyledProperty<double> MajorTickLengthProperty =
        AvaloniaProperty.Register<TimeDial, double>("MajorTickLength", 9);
    internal static readonly StyledProperty<double> TickGapProperty =
        AvaloniaProperty.Register<TimeDial, double>("TickGap", 12);
    internal static readonly StyledProperty<double> FocusRingGapProperty =
        AvaloniaProperty.Register<TimeDial, double>("FocusRingGap", 3);
    internal static readonly StyledProperty<double> GrabSlackProperty =
        AvaloniaProperty.Register<TimeDial, double>("GrabSlack", 18);
    internal static readonly StyledProperty<double> UnitFontSizeProperty =
        AvaloniaProperty.Register<TimeDial, double>("UnitFontSize", 14);
    internal static readonly StyledProperty<double> NumeralFontSizeProperty =
        AvaloniaProperty.Register<TimeDial, double>("NumeralFontSize", 12);
    internal static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("TrackBrush");
    internal static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("FillBrush");
    internal static readonly StyledProperty<IBrush?> HandleBrushProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("HandleBrush");
    internal static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("TickBrush");
    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("MutedBrush");
    internal static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("FocusBrush");
    internal static readonly StyledProperty<IBrush?> HandleShadowProperty =
        AvaloniaProperty.Register<TimeDial, IBrush?>("HandleShadow");

    private enum Grab
    {
        None,
        Track,
        Readout
    }

    private readonly DragTracker _drag;
    private readonly EllipseGeometry _trackCircle = new();
    private readonly PathGeometry _fillArc = new();
    private readonly PathFigure _fillFigure = new() { IsClosed = false, IsFilled = false };
    private readonly ArcSegment _fillSegment = new() { SweepDirection = SweepDirection.Clockwise };
    private readonly TextLayout?[] _numerals = new TextLayout?[4];
    private MotionValue<double>? _shownSpring;
    private MotionValue<double>? _pressSpring;
    private double _shown;
    private double _press;
    private Pen? _trackPen;
    private Pen? _fillPen;
    private Pen? _minorPen;
    private Pen? _majorPen;
    private Pen? _edgePen;
    private Pen? _focusPen;
    private TextLayout? _main;
    private TextLayout? _unit;
    private string? _mainText;
    private string? _unitText;
    private Point _origin;
    private Grab _grab;
    private double _lastAngle;
    private double _dragMinutes;
    private double _wheel;
    private Size _built;
    private double _builtFill = double.NaN;

    // Theme-set values, read through these so the XAML setters can name them.
    internal double DialSize => GetValue(DialSizeProperty);
    internal double TrackThickness => GetValue(TrackThicknessProperty);
    internal double TrackInset => GetValue(TrackInsetProperty);
    internal double HandleRadius => GetValue(HandleRadiusProperty);
    internal double HandlePressScale => GetValue(HandlePressScaleProperty);
    internal double MinorTickLength => GetValue(MinorTickLengthProperty);
    internal double MajorTickLength => GetValue(MajorTickLengthProperty);
    internal double TickGap => GetValue(TickGapProperty);
    internal double FocusRingGap => GetValue(FocusRingGapProperty);
    internal double GrabSlack => GetValue(GrabSlackProperty);
    internal double UnitFontSize => GetValue(UnitFontSizeProperty);
    internal double NumeralFontSize => GetValue(NumeralFontSizeProperty);
    internal IBrush? TrackBrush => GetValue(TrackBrushProperty);
    internal IBrush? FillBrush => GetValue(FillBrushProperty);
    internal IBrush? HandleBrush => GetValue(HandleBrushProperty);
    internal IBrush? TickBrush => GetValue(TickBrushProperty);
    internal IBrush? MutedBrush => GetValue(MutedBrushProperty);
    internal IBrush? FocusBrush => GetValue(FocusBrushProperty);
    internal IBrush? HandleShadow => GetValue(HandleShadowProperty);

    static TimeDial()
    {
        FocusableProperty.OverrideDefaultValue<TimeDial>(true);
        ClipToBoundsProperty.OverrideDefaultValue<TimeDial>(false);
        AffectsMeasure<TimeDial>(DialSizeProperty);
        AffectsRender<TimeDial>(BackgroundProperty, BorderBrushProperty, ForegroundProperty, TrackBrushProperty,
            FillBrushProperty, HandleBrushProperty, TickBrushProperty, MutedBrushProperty, FocusBrushProperty,
            HandleShadowProperty);
    }

    public TimeDial()
    {
        _fillFigure.Segments!.Add(_fillSegment);
        _fillArc.Figures!.Add(_fillFigure);
        _drag = new DragTracker(this, new DragOptions { Threshold = 1 });
        _drag.Pressed += OnDragPressed;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragEnded;
        _drag.Released += OnDragReleased;
        _drag.Cancelled += OnDragEnded;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public TimeSpan Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="ModeProperty"/>
    public TimeDialMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <inheritdoc cref="StepProperty"/>
    public TimeSpan Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <inheritdoc cref="LargeStepProperty"/>
    public TimeSpan LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    /// <inheritdoc cref="MinimumProperty"/>
    public TimeSpan Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <inheritdoc cref="MaximumProperty"/>
    public TimeSpan Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <inheritdoc cref="TurnLengthProperty"/>
    public TimeSpan TurnLength
    {
        get => GetValue(TurnLengthProperty);
        set => SetValue(TurnLengthProperty, value);
    }

    /// <inheritdoc cref="Is24HourProperty"/>
    public bool Is24Hour
    {
        get => GetValue(Is24HourProperty);
        set => SetValue(Is24HourProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="AmTextProperty"/>
    public string AmText
    {
        get => GetValue(AmTextProperty);
        set => SetValue(AmTextProperty, value);
    }

    /// <inheritdoc cref="PmTextProperty"/>
    public string PmText
    {
        get => GetValue(PmTextProperty);
        set => SetValue(PmTextProperty, value);
    }

    /// <inheritdoc cref="HoursTextProperty"/>
    public string HoursText
    {
        get => GetValue(HoursTextProperty);
        set => SetValue(HoursTextProperty, value);
    }

    /// <inheritdoc cref="MinutesTextProperty"/>
    public string MinutesText
    {
        get => GetValue(MinutesTextProperty);
        set => SetValue(MinutesTextProperty, value);
    }

    /// <summary>Raised when a drag ends, a key moves the value, or AM and PM are switched.</summary>
    public event EventHandler<TimeDialValueCommittedEventArgs>? ValueCommitted
    {
        add => AddHandler(ValueCommittedEvent, value);
        remove => RemoveHandler(ValueCommittedEvent, value);
    }

    /// <summary>The value as it is read out: "7:05 PM", "1 hr 25 min".</summary>
    public string ValueText => TimeDialMath.Spoken(Value.TotalMinutes, Mode, Is24Hour, AmText, PmText, HoursText, MinutesText);

    /// <summary>The readout as drawn (tests).</summary>
    internal string? ReadoutText => _mainText is null ? null : _unitText is { Length: > 0 } unit ? _mainText + " " + unit : _mainText;

    /// <summary>The value the handle is drawn at, in minutes (trails <see cref="Value"/> while its spring runs).</summary>
    internal double ShownMinutes => _shown;

    /// <summary>Minutes in one turn of the handle.</summary>
    internal double TurnMinutes => Mode == TimeDialMode.Time
        ? TimeDialMath.MinutesPerHalfDay
        : Math.Max(1, TurnLength.TotalMinutes);

    internal double StepMinutes => Math.Max(0, Step.TotalMinutes);

    internal double MinMinutes => Mode == TimeDialMode.Time ? 0 : Math.Max(0, Minimum.TotalMinutes);

    internal double MaxMinutes => Mode == TimeDialMode.Time
        ? TimeDialMath.MinutesPerDay - Math.Max(1, StepMinutes)
        : Math.Max(MinMinutes, Maximum.TotalMinutes);

    /// <summary>Center of the dial and radius of the middle of the track, for the current size.</summary>
    internal (Point Center, double Radius) Track
    {
        get
        {
            var size = Bounds.Size;
            var side = Math.Min(size.Width, size.Height);
            return (new Point(size.Width / 2, size.Height / 2), Math.Max(0, side / 2 - GetValue(TrackInsetProperty)));
        }
    }

    /// <summary>Moves the value by whole steps, as the arrow keys do, and commits it.</summary>
    public void StepBy(int steps)
    {
        var step = StepMinutes > 0 ? StepMinutes : 1;
        MoveBy(steps * step);
    }

    private TimeSpan CoerceTime(TimeSpan value)
    {
        var minutes = value.TotalMinutes;
        if (double.IsNaN(minutes))
        {
            return TimeSpan.Zero;
        }
        minutes = Mode == TimeDialMode.Time
            ? TimeDialMath.WrapDay(minutes)
            : Math.Clamp(minutes, MinMinutes, MaxMinutes);
        return TimeSpan.FromMinutes(minutes);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            Follow();
            UpdateReadout();
        }
        else if (change.Property == ModeProperty || change.Property == MinimumProperty || change.Property == MaximumProperty ||
                 change.Property == TurnLengthProperty)
        {
            CoerceValue(ValueProperty);
            JumpShown();
            UpdateReadout();
            UpdateNumerals();
        }
        else if (change.Property == Is24HourProperty || change.Property == AmTextProperty || change.Property == PmTextProperty ||
                 change.Property == HoursTextProperty || change.Property == MinutesTextProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty ||
                 change.Property == MutedBrushProperty || change.Property == UnitFontSizeProperty)
        {
            UpdateReadout();
            UpdateNumerals();
        }
        else if (change.Property == NumeralFontSizeProperty || change.Property == TickBrushProperty)
        {
            _minorPen = null;
            _majorPen = null;
            UpdateNumerals();
        }
        else if (change.Property == TrackThicknessProperty || change.Property == TrackBrushProperty ||
                 change.Property == FillBrushProperty)
        {
            _trackPen = null;
            _fillPen = null;
            InvalidateVisual();
        }
        else if (change.Property == BorderBrushProperty || change.Property == FocusBrushProperty)
        {
            _edgePen = null;
            _focusPen = null;
        }
        else if (change.Property == TrackInsetProperty)
        {
            BuildGeometry();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        JumpShown();
        UpdateReadout();
        UpdateNumerals();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = GetValue(DialSizeProperty);
        var side = Math.Min(size, Math.Min(availableSize.Width, availableSize.Height));
        return double.IsFinite(side) && side > 0 ? new Size(side, side) : new Size(size, size);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        BuildGeometry();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimeDialAutomationPeer(this);

    // --- value and springs ----------------------------------------------------------------------------------------

    /// <summary>Sets a new value from input, snapped to the step and kept in range.</summary>
    internal void SetFromInput(double minutes) => SetCurrentValue(ValueProperty, TimeSpan.FromMinutes(minutes));

    private void MoveBy(double minutes)
    {
        var current = Value.TotalMinutes;
        var next = Mode == TimeDialMode.Time
            ? TimeDialMath.WrapDay(TimeDialMath.Snap(current, StepMinutes) + minutes)
            : Math.Clamp(TimeDialMath.Snap(current, StepMinutes) + minutes, MinMinutes, MaxMinutes);
        SetFromInput(next);
        Commit();
    }

    private void Commit() => RaiseEvent(new TimeDialValueCommittedEventArgs(ValueCommittedEvent, Value));

    /// <summary>
    /// The handle follows the value on a spring, the short way round the face: a clock's handle never runs a whole turn
    /// back when the time passes midnight, and switching AM and PM does not move it.
    /// </summary>
    private void Follow()
    {
        var value = Value.TotalMinutes;
        double target;
        if (Mode == TimeDialMode.Time)
        {
            var turn = TimeDialMath.MinutesPerHalfDay;
            var delta = (value - _shown) % turn;
            if (delta > turn / 2)
            {
                delta -= turn;
            }
            else if (delta < -turn / 2)
            {
                delta += turn;
            }
            target = _shown + delta;
        }
        else
        {
            target = value;
        }
        if (!IsAttachedToVisualTree() || !ArcMotion.IsFull(this))
        {
            _shownSpring?.Jump(target);
            Show(target);
            return;
        }
        _shownSpring ??= CreateShownSpring();
        _ = _shownSpring.AnimateTo(target, MotionSpec.Spring(_grab == Grab.Track ? ArcSpring.Snappy : ArcSpring.Smooth));
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private MotionValue<double> CreateShownSpring()
    {
        var spring = new MotionValue<double>(this, _shown, MotionVectors.Double);
        spring.Changed += Show;
        return spring;
    }

    private void JumpShown()
    {
        _shownSpring?.Jump(Value.TotalMinutes);
        Show(Value.TotalMinutes);
    }

    private void Show(double minutes)
    {
        _shown = minutes;
        BuildFill();
        InvalidateVisual();
    }

    private void Press(bool pressed)
    {
        PseudoClasses.Set(PressedPseudoClass, pressed);
        var target = pressed ? 1.0 : 0.0;
        if (!IsAttachedToVisualTree() || !ArcMotion.IsFull(this))
        {
            _pressSpring?.Jump(target);
            _press = target;
            InvalidateVisual();
            return;
        }
        if (_pressSpring is null)
        {
            _pressSpring = new MotionValue<double>(this, _press, MotionVectors.Double);
            _pressSpring.Changed += value =>
            {
                _press = value;
                InvalidateVisual();
            };
        }
        _ = _pressSpring.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy));
    }

    // --- pointer --------------------------------------------------------------------------------------------------

    private Point Local(DragGestureEventArgs e) => e.Position - _origin;

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        if (!IsEffectivelyEnabled || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }
        _origin = this.TranslatePoint(default, top) ?? default;
        var local = Local(e);
        var (center, radius) = Track;
        var distance = Math.Sqrt((local.X - center.X) * (local.X - center.X) + (local.Y - center.Y) * (local.Y - center.Y));
        var reach = GetValue(TrackThicknessProperty) / 2 + Math.Max(GetValue(GrabSlackProperty), GetValue(HandleRadiusProperty));
        Focus(NavigationMethod.Pointer);
        if (Math.Abs(distance - radius) <= reach)
        {
            _grab = Grab.Track;
            var angle = TimeDialMath.AngleOf(local, center);
            _lastAngle = angle;
            _dragMinutes = TimeDialMath.Nearest(angle, Value.TotalMinutes, TurnMinutes, MinMinutes, MaxMinutes,
                Mode == TimeDialMode.Time);
            SetFromInput(TimeDialMath.Snap(_dragMinutes, StepMinutes));
            Press(true);
            return;
        }
        _grab = Mode == TimeDialMode.Time && !Is24Hour && distance < radius * 0.5 ? Grab.Readout : Grab.None;
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (_grab != Grab.Track)
        {
            return;
        }
        var (center, _) = Track;
        var angle = TimeDialMath.AngleOf(Local(e), center);
        var turn = TimeDialMath.Turn(_lastAngle, angle);
        _lastAngle = angle;
        _dragMinutes += turn / 360 * TurnMinutes;
        _dragMinutes = Mode == TimeDialMode.Time
            ? TimeDialMath.WrapDay(_dragMinutes)
            : Math.Clamp(_dragMinutes, MinMinutes, MaxMinutes);
        var snapped = TimeDialMath.Snap(_dragMinutes, StepMinutes);
        if (Mode == TimeDialMode.Duration)
        {
            snapped = Math.Clamp(snapped, MinMinutes, MaxMinutes);
        }
        SetFromInput(snapped);
    }

    private void OnDragEnded(object? sender, DragGestureEventArgs e)
    {
        if (_grab == Grab.Track)
        {
            Press(false);
            Commit();
        }
        _grab = Grab.None;
    }

    private void OnDragReleased(object? sender, DragGestureEventArgs e)
    {
        if (_grab == Grab.Readout)
        {
            SwitchHalfOfDay();
        }
        OnDragEnded(sender, e);
    }

    /// <summary>Switches between AM and PM without moving the handle.</summary>
    internal void SwitchHalfOfDay()
    {
        if (Mode != TimeDialMode.Time)
        {
            return;
        }
        SetFromInput(TimeDialMath.WrapDay(Value.TotalMinutes + TimeDialMath.MinutesPerHalfDay));
        Commit();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        _wheel += e.Delta.Y;
        var steps = (int)Math.Truncate(_wheel);
        if (steps != 0)
        {
            _wheel -= steps;
            StepBy(steps);
        }
        e.Handled = true;
    }

    // --- keyboard -------------------------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        var large = Math.Max(StepMinutes, LargeStep.TotalMinutes);
        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                StepBy(1);
                break;
            case Key.Down:
            case Key.Left:
                StepBy(-1);
                break;
            case Key.PageUp:
                MoveBy(large);
                break;
            case Key.PageDown:
                MoveBy(-large);
                break;
            case Key.Home:
                SetFromInput(MinMinutes);
                Commit();
                break;
            case Key.End:
                SetFromInput(MaxMinutes);
                Commit();
                break;
            case Key.A when Mode == TimeDialMode.Time && Value.TotalMinutes >= TimeDialMath.MinutesPerHalfDay:
            case Key.P when Mode == TimeDialMode.Time && Value.TotalMinutes < TimeDialMath.MinutesPerHalfDay:
                SwitchHalfOfDay();
                break;
            case Key.A:
            case Key.P:
                if (Mode != TimeDialMode.Time)
                {
                    return;
                }
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // --- drawing --------------------------------------------------------------------------------------------------

    private Typeface Face(FontWeight weight) => new(FontFamily, FontStyle.Normal, weight);

    private void UpdateReadout()
    {
        var (main, unit) = TimeDialMath.Readout(Value.TotalMinutes, Mode, Is24Hour, AmText, PmText, HoursText, MinutesText);
        if (_mainText != main || _main is null)
        {
            _mainText = main;
            _main?.Dispose();
            _main = new TextLayout(main, Face(FontWeight.SemiBold), FontSize, Foreground);
        }
        var muted = GetValue(MutedBrushProperty) ?? Foreground;
        if (_unitText != unit || _unit is null)
        {
            _unitText = unit;
            _unit?.Dispose();
            _unit = new TextLayout(unit, Face(FontWeight.Medium), GetValue(UnitFontSizeProperty), muted);
        }
        InvalidateVisual();
    }

    private void UpdateNumerals()
    {
        var brush = GetValue(MutedBrushProperty) ?? Foreground;
        for (var i = 0; i < _numerals.Length; i++)
        {
            _numerals[i]?.Dispose();
            _numerals[i] = new TextLayout(TimeDialMath.QuarterLabel(i, Mode, TurnMinutes), Face(FontWeight.Medium),
                GetValue(NumeralFontSizeProperty), brush);
        }
        // A forced rebuild: the readout's brush or font may have changed too.
        _main?.Dispose();
        _main = null;
        _unit?.Dispose();
        _unit = null;
        var (main, unit) = TimeDialMath.Readout(Value.TotalMinutes, Mode, Is24Hour, AmText, PmText, HoursText, MinutesText);
        _mainText = main;
        _unitText = unit;
        _main = new TextLayout(main, Face(FontWeight.SemiBold), FontSize, Foreground);
        _unit = new TextLayout(unit, Face(FontWeight.Medium), GetValue(UnitFontSizeProperty), brush);
        InvalidateVisual();
    }

    private void BuildGeometry()
    {
        var (center, radius) = Track;
        _trackCircle.Center = center;
        _trackCircle.RadiusX = radius;
        _trackCircle.RadiusY = radius;
        _built = Bounds.Size;
        _builtFill = double.NaN;
        BuildFill();
        InvalidateVisual();
    }

    /// <summary>The duration's fill: the part of the current turn, updated in place (never in the render pass).</summary>
    private void BuildFill()
    {
        if (Mode != TimeDialMode.Duration)
        {
            return;
        }
        var turn = TurnMinutes;
        var progress = Math.Max(0, _shown) / turn;
        var fraction = progress - Math.Floor(progress);
        if (fraction.Equals(_builtFill))
        {
            return;
        }
        _builtFill = fraction;
        if (fraction < 0.0005 || fraction > 0.9995)
        {
            return;
        }
        var (center, radius) = Track;
        var to = (fraction * 360 - 90) * Math.PI / 180;
        _fillFigure.StartPoint = new Point(center.X, center.Y - radius);
        _fillSegment.Point = new Point(center.X + radius * Math.Cos(to), center.Y + radius * Math.Sin(to));
        _fillSegment.Size = new Size(radius, radius);
        _fillSegment.IsLargeArc = fraction > 0.5;
    }

    public override void Render(DrawingContext context)
    {
        if (_built != Bounds.Size)
        {
            return;
        }
        var (center, radius) = Track;
        if (radius <= 0)
        {
            return;
        }
        var side = Math.Min(Bounds.Width, Bounds.Height);
        var thickness = GetValue(TrackThicknessProperty);

        // Face.
        if (Background is { } face)
        {
            context.DrawEllipse(face, null, center, side / 2, side / 2);
        }
        if (BorderBrush is { } edge)
        {
            _edgePen ??= new Pen(edge, 1);
            context.DrawEllipse(null, _edgePen, center, side / 2 - 0.5, side / 2 - 0.5);
        }

        // Track and the duration's fill.
        if (GetValue(TrackBrushProperty) is { } trackBrush)
        {
            _trackPen ??= new Pen(trackBrush, thickness);
            context.DrawGeometry(null, _trackPen, _trackCircle);
        }
        var fillBrush = GetValue(FillBrushProperty);
        if (Mode == TimeDialMode.Duration && fillBrush is not null && _shown > 0)
        {
            _fillPen ??= new Pen(fillBrush, thickness, lineCap: PenLineCap.Round);
            var progress = _shown / TurnMinutes;
            if (progress >= 0.9995)
            {
                context.DrawGeometry(null, _fillPen, _trackCircle);
            }
            var fraction = progress - Math.Floor(progress);
            if (fraction >= 0.0005 && fraction <= 0.9995)
            {
                context.DrawGeometry(null, _fillPen, _fillArc);
            }
        }

        DrawTicks(context, center, radius - thickness / 2 - GetValue(TickGapProperty));
        DrawReadout(context, center);
        DrawHandle(context, center, radius);
    }

    private void DrawTicks(DrawingContext context, Point center, double outer)
    {
        if (GetValue(TickBrushProperty) is not { } tickBrush)
        {
            return;
        }
        _minorPen ??= new Pen(tickBrush, 1, lineCap: PenLineCap.Round);
        _majorPen ??= new Pen(tickBrush, 2, lineCap: PenLineCap.Round);
        var minor = GetValue(MinorTickLengthProperty);
        var major = GetValue(MajorTickLengthProperty);
        int count;
        if (Mode == TimeDialMode.Time)
        {
            count = 60;
        }
        else
        {
            var steps = StepMinutes > 0 ? TurnMinutes / StepMinutes : 60;
            count = (int)Math.Clamp(Math.Round(steps / 12) * 12, 12, 120);
        }
        for (var i = 0; i < count; i++)
        {
            var isMajor = i * 12 % count == 0;
            var angle = (i * 360.0 / count - 90) * Math.PI / 180;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var length = isMajor ? major : minor;
            context.DrawLine(isMajor ? _majorPen : _minorPen,
                new Point(center.X + outer * cos, center.Y + outer * sin),
                new Point(center.X + (outer - length) * cos, center.Y + (outer - length) * sin));
        }
        var labelRadius = outer - major - 10;
        for (var q = 0; q < _numerals.Length; q++)
        {
            if (_numerals[q] is not { } numeral)
            {
                continue;
            }
            var angle = (q * 90 - 90) * Math.PI / 180;
            var x = center.X + labelRadius * Math.Cos(angle) - numeral.WidthIncludingTrailingWhitespace / 2;
            var y = center.Y + labelRadius * Math.Sin(angle) - numeral.Height / 2;
            numeral.Draw(context, new Point(x, y));
        }
    }

    private void DrawReadout(DrawingContext context, Point center)
    {
        if (_main is not { } main)
        {
            return;
        }
        var unitHeight = _unit is { } u && _unitText is { Length: > 0 } ? u.Height : 0;
        var top = center.Y - (main.Height + unitHeight) / 2;
        main.Draw(context, new Point(center.X - main.WidthIncludingTrailingWhitespace / 2, top));
        if (unitHeight > 0)
        {
            _unit!.Draw(context, new Point(center.X - _unit.WidthIncludingTrailingWhitespace / 2, top + main.Height - 2));
        }
    }

    private void DrawHandle(DrawingContext context, Point center, double radius)
    {
        var angle = (TimeDialMath.AngleFor(_shown, TurnMinutes) - 90) * Math.PI / 180;
        var position = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
        var scale = 1 + (GetValue(HandlePressScaleProperty) - 1) * _press;
        var r = GetValue(HandleRadiusProperty) * scale;
        if (GetValue(HandleShadowProperty) is { } shadow)
        {
            context.DrawEllipse(shadow, null, new Point(position.X, position.Y + 1.5), r + 1, r + 1);
        }
        if (GetValue(HandleBrushProperty) is { } handle)
        {
            context.DrawEllipse(handle, null, position, r, r);
        }
        if (GetValue(FillBrushProperty) is { } dot)
        {
            var inner = Math.Max(2, r * 0.32);
            context.DrawEllipse(dot, null, position, inner, inner);
        }
        if (IsFocused && Classes.Contains(":focus-visible") && GetValue(FocusBrushProperty) is { } focus)
        {
            _focusPen ??= new Pen(focus, 2);
            var ring = r + GetValue(FocusRingGapProperty) + 1;
            context.DrawEllipse(null, _focusPen, position, ring, ring);
        }
    }
}

/// <summary>The dial as a slider: its value in minutes, the step sizes, and the value read out as help text.</summary>
internal sealed class TimeDialAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    public TimeDialAutomationPeer(TimeDial owner) : base(owner)
    {
    }

    private new TimeDial Owner => (TimeDial)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Owner.Label;

    protected override string? GetHelpTextCore() => Owner.ValueText;

    public bool IsReadOnly => !Owner.IsEffectivelyEnabled;

    public double Minimum => Owner.MinMinutes;

    public double Maximum => Owner.MaxMinutes;

    public double Value => Owner.Value.TotalMinutes;

    public double LargeChange => Math.Max(Owner.StepMinutes, Owner.LargeStep.TotalMinutes);

    public double SmallChange => Owner.StepMinutes > 0 ? Owner.StepMinutes : 1;

    public void SetValue(double value) => Owner.SetFromInput(value);
}
