using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Which control a <see cref="ColorPickerPad"/> is.</summary>
internal enum ColorPickerPadKind
{
    /// <summary>Saturation left to right, brightness bottom to top, over the pure hue (a 2D slider).</summary>
    Area,

    /// <summary>Hue, 0 to 360 degrees.</summary>
    Hue,

    /// <summary>Opacity, 0 to 100%.</summary>
    Alpha
}

/// <summary>
/// The saturation / brightness area and the hue and opacity sliders, each one drawn control. The thumb chases the
/// value on a quick spring with a little life (0.26s, bounce 0.22); under a drag it switches to a stiff spring with no
/// bounce (0.1s) so it stays glued to the pointer (P12). Dragging past an edge stretches the thumb a few pixels on
/// Arc's rubber band and a key press at a limit kicks it, then it springs back (P13). The thumb grows to 1.2 while
/// held. Reduced motion places it directly. Gradients are cached brushes; nothing is allocated per frame.
/// </summary>
internal sealed class ColorPickerPad : Control
{
    public const double TrackEnd = 11;
    private const double Stretch = 10;

    internal static readonly SpringSpec ThumbSpring = SpringSpec.FromVisual(0.26, 0.22);
    internal static readonly SpringSpec DragSpring = SpringSpec.FromVisual(0.1, 0);

    private static readonly IBrush s_white = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Colors.White), new ImmutableGradientStop(1, Color.FromArgb(0, 255, 255, 255))],
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

    private static readonly IBrush s_black = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))],
        startPoint: new RelativePoint(0, 1, RelativeUnit.Relative), endPoint: new RelativePoint(0, 0, RelativeUnit.Relative));

    public static readonly StyledProperty<ColorPickerPadKind> KindProperty =
        AvaloniaProperty.Register<ColorPickerPad, ColorPickerPadKind>(nameof(Kind));

    public static readonly StyledProperty<double> TrackRadiusProperty =
        AvaloniaProperty.Register<ColorPickerPad, double>(nameof(TrackRadius), 16);

    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<ColorPickerPad, double>(nameof(TrackThickness), 14);

    public static readonly StyledProperty<IBrush?> CheckerColorProperty =
        ColorPickerChip.CheckerColorProperty.AddOwner<ColorPickerPad>();

    public static readonly StyledProperty<IBrush?> CheckerBaseProperty =
        ColorPickerChip.CheckerBaseProperty.AddOwner<ColorPickerPad>();

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<ColorPickerPad, IBrush?>(nameof(FocusRingBrush));

    public ColorPickerPadKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double TrackRadius { get => GetValue(TrackRadiusProperty); set => SetValue(TrackRadiusProperty, value); }
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }
    public IBrush? CheckerColor { get => GetValue(CheckerColorProperty); set => SetValue(CheckerColorProperty, value); }
    public IBrush? CheckerBase { get => GetValue(CheckerBaseProperty); set => SetValue(CheckerBaseProperty, value); }
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    private readonly SolidColorBrush _hueFill = new(Colors.Red);
    private readonly SolidColorBrush _thumbFill = new(Colors.Red);
    private readonly GradientStop _alphaFrom = new(Color.FromArgb(0, 0, 0, 0), 0);
    private readonly GradientStop _alphaTo = new(Colors.Red, 1);
    private readonly LinearGradientBrush _alphaTrack;
    private LinearGradientBrush? _hueTrack;
    private double _trackWidth = -1;
    private Point _value;
    private Vector _overflow;
    private MotionValue<Point>? _thumb;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _kick;
    private bool _active;
    private DragTracker? _drag;
    private Point _origin;
    private IPen? _ring;
    private IBrush? _ringBrush;
    private bool _kickVertical;

    public ColorPickerPad()
    {
        Focusable = true;
        ClipToBounds = false;
        _alphaTrack = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { _alphaFrom, _alphaTo }
        };
    }

    static ColorPickerPad()
    {
        AffectsRender<ColorPickerPad>(KindProperty, TrackRadiusProperty, TrackThicknessProperty, CheckerColorProperty, CheckerBaseProperty);
        FocusAdornerProperty.OverrideDefaultValue<ColorPickerPad>(null);
    }

    /// <summary>The value as fractions (x: saturation / hue / opacity; y: 1 − brightness for the area).</summary>
    public Point Value => _value;

    public bool IsActive => _active;

    /// <summary>Where the thumb is drawn right now (fractions; includes stretch and spring lag).</summary>
    public Point ThumbPosition => _thumb?.Value ?? _value;

    /// <summary>The user moved the thumb to these fractions (clamped); the owner commits and calls <see cref="SetValue(Point, bool)"/>.</summary>
    public event EventHandler<Point>? ValueRequested;

    /// <summary>A drag started (true) or ended (false).</summary>
    public event EventHandler<bool>? ActiveChanged;

    /// <summary>Sets the hue (area base), the opaque color (area thumb, alpha track end) and the full color (alpha thumb).</summary>
    public void SetColors(double hue, Color opaque, Color color)
    {
        _hueFill.Color = ColorPickerMath.PureHue(hue);
        _alphaTo.Color = opaque;
        _thumbFill.Color = Kind switch
        {
            ColorPickerPadKind.Area => opaque,
            ColorPickerPadKind.Hue => ColorPickerMath.PureHue(hue),
            _ => color
        };
        InvalidateVisual();
    }

    /// <summary>Moves the thumb to <paramref name="value"/> (fractions), on the follow spring unless told otherwise.</summary>
    public void SetValue(Point value, bool animate)
    {
        _value = value;
        MoveThumb(animate);
        UpdateAutomation();
    }

    private void MoveThumb(bool animate)
    {
        _thumb ??= Create(_value, MotionVectors.Point);
        var size = TravelSize;
        var target = new Point(_value.X + (size.Width > 0 ? _overflow.X / size.Width : 0),
            _value.Y + (size.Height > 0 ? _overflow.Y / size.Height : 0));
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _thumb.Jump(target);
            return;
        }
        _ = _thumb.AnimateTo(target, MotionSpec.Spring(_active ? DragSpring : ThumbSpring));
    }

    private MotionValue<T> Create<T>(T initial, IMotionVector<T> vector) where T : struct
    {
        var value = new MotionValue<T>(this, initial, vector);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Thumb travel: the whole area, or the slider between its 11px ends.</summary>
    private Size TravelSize => Kind == ColorPickerPadKind.Area
        ? Bounds.Size
        : new Size(Math.Max(0, Bounds.Width - TrackEnd * 2), 0);

    private Point ThumbCenter(Point fraction)
    {
        var size = Bounds.Size;
        if (Kind == ColorPickerPadKind.Area)
        {
            return new Point(fraction.X * size.Width, fraction.Y * size.Height);
        }
        return new Point(TrackEnd + fraction.X * Math.Max(0, size.Width - TrackEnd * 2), size.Height / 2);
    }

    // ---------------------------------------------------------------- pointer

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_drag is null)
        {
            _drag = new DragTracker(this, new DragOptions { Threshold = 0, HandledEventsToo = true });
            _drag.Pressed += OnDragPressed;
            _drag.Moved += OnDragMoved;
            _drag.Completed += (_, _) => EndDrag();
            _drag.Released += (_, _) => EndDrag();
            _drag.Cancelled += (_, _) => EndDrag();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Focus(NavigationMethod.Pointer);
            e.Handled = true;
        }
    }

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        _origin = top is not null && this.TranslatePoint(default, top) is { } p ? p : default;
        _active = true;
        AnimateScale(1.2);
        ActiveChanged?.Invoke(this, true);
        Read(e.Position);
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e) => Read(e.Position);

    private void Read(Point topLevelPoint)
    {
        var local = topLevelPoint - _origin;
        var size = TravelSize;
        double x, y = 0;
        if (Kind == ColorPickerPadKind.Area)
        {
            x = size.Width > 0 ? local.X / size.Width : 0;
            y = size.Height > 0 ? local.Y / size.Height : 0;
        }
        else
        {
            x = size.Width > 0 ? (local.X - TrackEnd) / size.Width : 0;
        }
        var clamped = new Point(Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
        // Past an edge the thumb stretches on the rubber band instead of stopping dead (P13).
        _overflow = new Vector(
            RubberBand.Apply((x - clamped.X) * size.Width, Stretch),
            Kind == ColorPickerPadKind.Area ? RubberBand.Apply((y - clamped.Y) * size.Height, Stretch) : 0);
        if (clamped == _value)
        {
            MoveThumb(animate: true);
        }
        ValueRequested?.Invoke(this, clamped);
    }

    private void EndDrag()
    {
        if (!_active)
        {
            return;
        }
        _active = false;
        _overflow = default;
        AnimateScale(1);
        MoveThumb(animate: true);
        ActiveChanged?.Invoke(this, false);
    }

    private void AnimateScale(double target)
    {
        _scale ??= Create(1.0, MotionVectors.Double);
        _ = _scale.AnimateTo(target, ArcMotion.IsFull(this)
            ? MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring)
            : MotionSpec.Instant);
    }

    // ---------------------------------------------------------------- keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var big = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Key is Key.PageUp or Key.PageDown;
        var size = big ? 10.0 : 1.0;
        var unit = Kind == ColorPickerPadKind.Hue ? 1 / 360.0 : 1 / 100.0;
        Vector? step = null;
        Point? absolute = null;
        if (Kind == ColorPickerPadKind.Area)
        {
            // Arrows move saturation (x) and brightness (y, up is brighter); Page keys step saturation by ten.
            step = e.Key switch
            {
                Key.Right => new Vector(size, 0),
                Key.Left => new Vector(-size, 0),
                Key.Up => new Vector(0, -size),
                Key.Down => new Vector(0, size),
                Key.PageUp => new Vector(10, 0),
                Key.PageDown => new Vector(-10, 0),
                _ => null
            };
        }
        else
        {
            if (e.Key is Key.Home or Key.End)
            {
                absolute = new Point(e.Key == Key.Home ? 0 : 1, 0);
            }
            step = e.Key switch
            {
                Key.Right or Key.Up => new Vector(size, 0),
                Key.Left or Key.Down => new Vector(-size, 0),
                Key.PageUp => new Vector(10, 0),
                Key.PageDown => new Vector(-10, 0),
                _ => null
            };
        }
        if (step is null && absolute is null)
        {
            return;
        }
        e.Handled = true;
        var unitY = 1 / 100.0;
        var wanted = absolute ?? new Point(_value.X + step!.Value.X * unit, _value.Y + step.Value.Y * unitY);
        var next = new Point(Math.Clamp(wanted.X, 0, 1), Math.Clamp(wanted.Y, 0, 1));
        if (next == _value && absolute is null)
        {
            var vertical = step!.Value.X == 0;
            Kick(Math.Sign(vertical ? step.Value.Y : step.Value.X), vertical);
            return;
        }
        ValueRequested?.Invoke(this, next);
    }

    /// <summary>A key press at a limit nudges the thumb past it and lets it spring back (P13).</summary>
    private void Kick(int direction, bool vertical)
    {
        _kickVertical = vertical;
        if (!IsLoaded || !ArcMotion.IsFull(this) || direction == 0)
        {
            return;
        }
        _kick ??= Create(0.0, MotionVectors.Double);
        _kick.Jump(0);
        _ = _kick.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Morph), direction * 140);
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

    // ---------------------------------------------------------------- render

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        return new Size(width, Kind == ColorPickerPadKind.Area ? 164 : ColorPickerDrawing.ThumbRadius * 2);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var checker = ColorPickerDrawing.Checker(CheckerColor, CheckerBase);
        if (Kind == ColorPickerPadKind.Area)
        {
            var area = new RoundedRect(new Rect(size), TrackRadius);
            context.DrawRectangle(_hueFill, null, area);
            context.DrawRectangle(s_white, null, area);
            context.DrawRectangle(s_black, null, area);
            context.DrawRectangle(null, ColorPickerDrawing.TrackLine, area.Deflate(0.5, 0.5));
        }
        else
        {
            var track = new Rect(0, (size.Height - TrackThickness) / 2, size.Width, TrackThickness);
            var round = new RoundedRect(track, TrackThickness / 2);
            if (Kind == ColorPickerPadKind.Hue)
            {
                context.DrawRectangle(HueTrack(size.Width), null, round);
            }
            else
            {
                UpdateAlphaStops(size.Width);
                context.DrawRectangle(checker, null, round);
                context.DrawRectangle(_alphaTrack, null, round);
            }
            context.DrawRectangle(null, ColorPickerDrawing.TrackLine, round.Deflate(0.5, 0.5));
        }
        var center = ThumbCenter(ThumbPosition);
        var kick = _kick?.Value ?? 0;
        center = _kickVertical ? center.WithY(center.Y + kick) : center.WithX(center.X + kick);
        IPen? ring = null;
        if (IsFocused && Classes.Contains(":focus-visible") && FocusRingBrush is { } ringBrush)
        {
            if (_ring is null || !ReferenceEquals(_ringBrush, ringBrush))
            {
                _ringBrush = ringBrush;
                _ring = new Pen(ringBrush, 2);
            }
            ring = _ring;
        }
        ColorPickerDrawing.DrawThumb(context, center, _scale?.Value ?? 1, _thumbFill,
            Kind == ColorPickerPadKind.Area ? null : checker, ring);
    }

    /// <summary>The hue stops sit at the thumb's travel ends, so the color under the thumb is the value it reports.</summary>
    private IBrush HueTrack(double width)
    {
        if (_hueTrack is not null && Math.Abs(width - _trackWidth) < 0.5)
        {
            return _hueTrack;
        }
        _trackWidth = width;
        var end = width > 0 ? TrackEnd / width : 0;
        var span = 1 - end * 2;
        Color[] colors = [Colors.Red, Color.Parse("#FFFF00"), Color.Parse("#00FF00"), Color.Parse("#00FFFF"), Colors.Blue, Color.Parse("#FF00FF"), Colors.Red];
        var stops = new GradientStops();
        for (var i = 0; i < colors.Length; i++)
        {
            stops.Add(new GradientStop(colors[i], end + span * i / 6.0));
        }
        _hueTrack = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = stops
        };
        return _hueTrack;
    }

    private void UpdateAlphaStops(double width)
    {
        var end = width > 0 ? TrackEnd / width : 0;
        if (Math.Abs(_alphaFrom.Offset - end) > 0.0001)
        {
            _alphaFrom.Offset = end;
            _alphaTo.Offset = 1 - end;
        }
    }

    // ---------------------------------------------------------------- automation

    private void UpdateAutomation()
    {
        var text = Kind switch
        {
            ColorPickerPadKind.Area => $"Saturation {Math.Round(_value.X * 100)}%, brightness {Math.Round((1 - _value.Y) * 100)}%",
            ColorPickerPadKind.Hue => $"{Math.Round(_value.X * 360)} degrees",
            _ => $"{Math.Round(_value.X * 100)}%"
        };
        AutomationProperties.SetHelpText(this, text);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ColorPickerPadPeer(this);

    private sealed class ColorPickerPadPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public ColorPickerPadPeer(ColorPickerPad owner) : base(owner)
        {
        }

        private ColorPickerPad Pad => (ColorPickerPad)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetLocalizedControlTypeCore() => Pad.Kind == ColorPickerPadKind.Area ? "2D slider" : base.GetLocalizedControlTypeCore();

        private double Scale => Pad.Kind == ColorPickerPadKind.Hue ? 360 : 100;

        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => Scale;

        public double Value => Math.Round(Pad._value.X * Scale);

        public double SmallChange => 1;

        public double LargeChange => 10;

        public void SetValue(double value) =>
            Pad.ValueRequested?.Invoke(Pad, new Point(Math.Clamp(value / Scale, 0, 1), Pad._value.Y));
    }
}
