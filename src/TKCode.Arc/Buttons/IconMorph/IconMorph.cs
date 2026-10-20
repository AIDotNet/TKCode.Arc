using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The icon pair an <see cref="IconMorph"/> morphs between (inactive → active).</summary>
public enum IconMorphKind
{
    /// <summary>Menu to close: the outer lines slide to the middle as they turn into an X; the middle line folds away.</summary>
    Menu,

    /// <summary>Play to pause: the triangle splits into two halves that straighten into bars.</summary>
    Play,

    /// <summary>Plus to check: each bar swings its ends to become one leg of the check.</summary>
    Plus,

    /// <summary>Copy to check: the back sheet tucks under, the front settles away, and the check draws in.</summary>
    Copy,

    /// <summary>Sun to moon: the disc grows while a shadow carves the crescent; the rays turn and fold in.</summary>
    Theme,

    /// <summary>A chevron that turns (<see cref="IconMorph.ChevronTurn"/>), for disclosure and sort controls.</summary>
    Chevron,

    /// <summary>Notifications on and off: turning on rings the bell; turning off strikes it through.</summary>
    Bell,

    /// <summary>Like: the heart fills with a pop and a ring and sparks burst out once.</summary>
    Heart
}

/// <summary>Where a <see cref="IconMorphKind.Chevron"/> points at rest.</summary>
public enum IconMorphChevronPointing
{
    Down,
    Right
}

/// <summary>
/// Icons that morph between two states instead of swapping: lines rotate and slide into place, outlines interpolate
/// (P16 <see cref="PathMorph"/>), and a few carry a moment of their own (the bell rings, the heart bursts). Every morph
/// rides an interruptible spring, so toggling quickly reverses from wherever the icon is. Drawn in one <see cref="Render"/>
/// on the 24-unit grid. The icon is decorative: put it in a button that carries the name and the pressed or expanded state.
/// </summary>
public class IconMorph : Control
{
    #region 公共属性定义

    public static readonly StyledProperty<IconMorphKind> KindProperty =
        AvaloniaProperty.Register<IconMorph, IconMorphKind>(nameof(Kind));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<IconMorph, bool>(nameof(IsActive), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<IconMorph, double>(nameof(IconSize), 24);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<IconMorph, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<double> ChevronTurnProperty =
        AvaloniaProperty.Register<IconMorph, double>(nameof(ChevronTurn), 180);

    public static readonly StyledProperty<IconMorphChevronPointing> ChevronPointingProperty =
        AvaloniaProperty.Register<IconMorph, IconMorphChevronPointing>(nameof(ChevronPointing));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<IconMorph>();

    /// <summary>Color of a liked heart (Arc <c>--icon-morph-like</c>; the theme uses Danger).</summary>
    public static readonly StyledProperty<IBrush?> LikeBrushProperty =
        AvaloniaProperty.Register<IconMorph, IBrush?>(nameof(LikeBrush));

    /// <summary>Which pair to draw.</summary>
    public IconMorphKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>The second state: close, pause, check, moon, turned, notifications on, liked.</summary>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <summary>Rendered size in px (default 24).</summary>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    /// <summary>Stroke width on the 24-unit grid (default 1.75); it scales with <see cref="IconSize"/>.</summary>
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    /// <summary>Degrees the chevron turns when active: 180 flips down to up, 90 turns right to down.</summary>
    public double ChevronTurn { get => GetValue(ChevronTurnProperty); set => SetValue(ChevronTurnProperty, value); }

    public IconMorphChevronPointing ChevronPointing { get => GetValue(ChevronPointingProperty); set => SetValue(ChevronPointingProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? LikeBrush { get => GetValue(LikeBrushProperty); set => SetValue(LikeBrushProperty, value); }

    #endregion

    // Play is split down the middle into two quads, so each half can become one pause bar.
    private const string PlayPath = "M7 4.5 L12 7.42 L12 16.58 L7 19.5 Z M12 7.42 L19.5 11.8 L19.5 12.2 L12 16.58 Z";
    private const string PausePath = "M6.5 5 L10 5 L10 19 L6.5 19 Z M14 5 L17.5 5 L17.5 19 L14 19 Z";
    private const string PlusPath = "M12 5 L12 19 M5 12 L19 12";
    private const string PlusCheckPath = "M20 6.5 L9.5 17 M4.5 12 L9.5 17";
    private const string CopySheetPath = "M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2";
    private const string BellPath = "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9";
    private const string ClapperPath = "M10.3 21a1.94 1.94 0 0 0 3.4 0";
    private const string HeartPath = "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7Z";
    private static readonly Point[] CopyCheck = [new(5, 12.5), new(9.5, 17), new(19, 7.5)];
    private static readonly double[] RingAngles = [0, 16, -13, 9, -6, 3, 0];
    private static readonly double[] ClapperOffsets = [0, -2.2, 2, -1.4, .9, -.4, 0];
    private static readonly double[] HeartScales = [.72, 1.14, 1];
    private static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly TimeSpan BurstLength = TimeSpan.FromSeconds(0.6);
    private static readonly TimeSpan RingLength = TimeSpan.FromSeconds(0.94);

    private static readonly (Point From, Point To)[] Rays = CreateRays();
    private static readonly (double X, double Y, double R)[] Sparks = CreateSparks();

    private static PathMorphShape? s_play, s_pause, s_plus, s_plusCheck;

    private MotionValue<double>? _primary;
    private MotionValue<double>? _secondary;
    private MotionValue<double>? _fade;
    private MotionValue<double>? _draw;
    private MotionValue<double>? _drawFade;
    private MotionValue<double>? _moment;
    private PathMorph? _path;
    private Geometry? _pathGeometry;
    private bool _momentActive;
    private bool _initialized;

    // Persistent clip geometries, updated in place while the sun / moon and bell morph.
    private readonly RectangleGeometry _maskBounds = new(new Rect(-8, -8, 40, 40));
    private readonly EllipseGeometry _shadow = new() { RadiusX = 6.5, RadiusY = 6.5 };
    private readonly EllipseGeometry _disc = new() { Center = new Point(12, 12) };
    private CombinedGeometry? _bite;
    private readonly PolylineGeometry _slashCut = new() { IsFilled = true };
    private CombinedGeometry? _cut;

    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;
    private readonly SolidColorBrush _heartBrush = new();
    private Pen? _heartPen;
    private Pen? _ringPen;
    private Pen? _solidPen;
    private readonly SolidColorBrush _heartFill = new();

    static IconMorph()
    {
        AffectsMeasure<IconMorph>(IconSizeProperty);
        AffectsRender<IconMorph>(StrokeThicknessProperty, ForegroundProperty, LikeBrushProperty, ChevronPointingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<IconMorph>(false);
        FocusableProperty.OverrideDefaultValue<IconMorph>(false);
    }

    private static (Point, Point)[] CreateRays()
    {
        var rays = new (Point, Point)[8];
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4;
            var c = Math.Cos(angle);
            var s = Math.Sin(angle);
            static double R(double v) => Math.Round(v * 100) / 100;
            rays[i] = (new Point(R(12 + c * 7.5), R(12 + s * 7.5)), new Point(R(12 + c * 10), R(12 + s * 10)));
        }
        return rays;
    }

    private static (double, double, double)[] CreateSparks()
    {
        var sparks = new (double, double, double)[7];
        for (var i = 0; i < 7; i++)
        {
            var angle = -Math.PI / 2 + i * 2 * Math.PI / 7;
            sparks[i] = (Math.Cos(angle), Math.Sin(angle), i % 2 == 1 ? 1 : 1.4);
        }
        return sparks;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_initialized)
        {
            return;
        }
        if (change.Property == KindProperty || change.Property == ChevronPointingProperty)
        {
            Reset();
        }
        else if (change.Property == IsActiveProperty)
        {
            Run(change.GetNewValue<bool>(), animate: IsLoaded);
        }
        else if (change.Property == ChevronTurnProperty && Kind == IconMorphKind.Chevron && IsActive)
        {
            Go(_primary!, ChevronTurn, MotionSpec.Spring(ArcSpring.Morph), IsLoaded);
        }
        else if (change.Property == ForegroundProperty || change.Property == LikeBrushProperty)
        {
            UpdateHeartColor();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized)
        {
            _initialized = true;
            Reset();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // One-shot moments end with the visual; the next attach shows the resting state.
        _momentActive = false;
        _moment?.Jump(0);
    }

    private MotionValue<double> Value(ref MotionValue<double>? field, double initial)
    {
        if (field is null)
        {
            field = new MotionValue<double>(this, initial, MotionVectors.Double);
            field.Changed += _ => OnValueChanged();
        }
        else
        {
            field.Jump(initial);
        }
        return field;
    }

    /// <summary>Places every value at the resting state of the current kind (no animation).</summary>
    private void Reset()
    {
        var on = IsActive ? 1.0 : 0.0;
        _momentActive = false;
        Value(ref _moment, 0);
        switch (Kind)
        {
            case IconMorphKind.Chevron:
                Value(ref _primary, IsActive ? ChevronTurn : 0);
                break;
            case IconMorphKind.Menu:
                Value(ref _primary, on);
                Value(ref _secondary, IsActive ? 0.2 : 1);
                Value(ref _fade, on);
                break;
            case IconMorphKind.Bell:
                // Slash drawn (1) while notifications are off.
                Value(ref _primary, 1 - on);
                Value(ref _fade, 1 - on);
                break;
            default:
                Value(ref _primary, on);
                Value(ref _fade, on);
                Value(ref _draw, on);
                Value(ref _drawFade, on);
                break;
        }
        if (Kind is IconMorphKind.Play or IconMorphKind.Plus)
        {
            if (_path is null)
            {
                _path = new PathMorph(this, 24);
                _path.Changed += OnPathChanged;
            }
            _path.Jump(ShapeFor(Kind, IsActive));
        }
        UpdateGeometry();
        UpdateHeartColor();
        InvalidateVisual();
    }

    private static PathMorphShape ShapeFor(IconMorphKind kind, bool active) => (kind, active) switch
    {
        (IconMorphKind.Play, false) => s_play ??= PathMorphShape.Parse(PlayPath),
        (IconMorphKind.Play, true) => s_pause ??= PathMorphShape.Parse(PausePath),
        (_, false) => s_plus ??= PathMorphShape.Parse(PlusPath),
        _ => s_plusCheck ??= PathMorphShape.Parse(PlusCheckPath)
    };

    private void OnPathChanged()
    {
        _pathGeometry = _path?.BuildGeometry();
        InvalidateVisual();
    }

    private bool CanAnimate => IsLoaded && ArcMotion.IsFull(this);

    /// <summary>The outline engine of Play and Plus (tests).</summary>
    internal PathMorph? Outline => _path;

    /// <summary>Main morph value (0 inactive, 1 active; degrees for Chevron) (tests).</summary>
    internal double Progress => _primary?.Value ?? 0;

    /// <summary>True while a ring or burst plays (tests).</summary>
    internal bool IsMomentPlaying => _momentActive;

    private void Go(MotionValue<double> value, double target, MotionSpec spec, bool animate)
    {
        if (animate && CanAnimate)
        {
            _ = value.AnimateTo(target, spec);
        }
        else
        {
            value.Jump(target);
        }
    }

    private static MotionSpec Fast => MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    private void Run(bool active, bool animate)
    {
        var on = active ? 1.0 : 0.0;
        switch (Kind)
        {
            case IconMorphKind.Menu:
                Go(_primary!, on, MotionSpec.Spring(ArcSpring.Morph), animate);
                Go(_secondary!, active ? 0.2 : 1, MotionSpec.Spring(ArcSpring.Snappy), animate);
                Go(_fade!, on, Fast, animate);
                break;
            case IconMorphKind.Play:
            case IconMorphKind.Plus:
                if (animate && CanAnimate)
                {
                    _ = _path!.MorphTo(ShapeFor(Kind, active), ArcSpring.Morph);
                }
                else
                {
                    _path!.Jump(ShapeFor(Kind, active));
                }
                break;
            case IconMorphKind.Copy:
            {
                Go(_primary!, on, MotionSpec.Spring(ArcSpring.Smooth), animate);
                Go(_fade!, on, Fast, animate);
                var delay = active ? TimeSpan.FromSeconds(0.06) : TimeSpan.Zero;
                Go(_draw!, on, MotionSpec.Spring(ArcSpring.Smooth, delay), animate);
                Go(_drawFade!, on, MotionSpec.Tween(TimeSpan.FromSeconds(0.08), ArcEasings.Standard, delay), animate);
                break;
            }
            case IconMorphKind.Theme:
                Go(_primary!, on, MotionSpec.Spring(ArcSpring.Morph), animate);
                Go(_fade!, on, Fast, animate);
                break;
            case IconMorphKind.Chevron:
                Go(_primary!, active ? ChevronTurn : 0, MotionSpec.Spring(ArcSpring.Morph), animate);
                break;
            case IconMorphKind.Bell:
                Go(_primary!, 1 - on, MotionSpec.Spring(ArcSpring.Smooth), animate);
                Go(_fade!, 1 - on, MotionSpec.Tween(TimeSpan.FromSeconds(0.08), ArcEasings.Standard), animate);
                PlayMoment(active, RingLength, animate);
                break;
            case IconMorphKind.Heart:
                Go(_fade!, on, Fast, animate);
                PlayMoment(active, BurstLength, animate);
                break;
        }
    }

    /// <summary>The bell's ring and the heart's burst replay on every turn on (full motion only).</summary>
    private void PlayMoment(bool active, TimeSpan length, bool animate)
    {
        if (!active || !animate || !CanAnimate)
        {
            _momentActive = false;
            _moment!.Jump(0);
            InvalidateVisual();
            return;
        }
        _momentActive = true;
        _momentLength = length.TotalSeconds;
        _moment!.Jump(0);
        _ = _moment.AnimateTo(length.TotalSeconds, MotionSpec.Tween(length, new LinearEasing()));
    }

    private double _momentLength;

    private void OnValueChanged()
    {
        if (_momentActive && _moment is not null && _moment.Value >= _momentLength - 1e-6)
        {
            // The one-shot moment ended: back to the resting drawing.
            _momentActive = false;
        }
        UpdateGeometry();
        UpdateHeartColor();
        InvalidateVisual();
    }

    /// <summary>Moves the persistent clip geometries to the current values (never in Render).</summary>
    private void UpdateGeometry()
    {
        if (_primary is null)
        {
            return;
        }
        var p = _primary.Value;
        if (Kind == IconMorphKind.Theme)
        {
            var r = 4.5 + 4 * p;
            _disc.RadiusX = r;
            _disc.RadiusY = r;
            _shadow.Center = new Point(30 - 12 * p, -6 + 12 * p);
            _bite ??= new CombinedGeometry(GeometryCombineMode.Exclude, _maskBounds, _shadow);
            // A combined geometry does not follow its children: re-attach the moved shadow.
            _bite.Geometry2 = null;
            _bite.Geometry2 = _shadow;
        }
        else if (Kind == IconMorphKind.Bell)
        {
            UpdateSlashCut(Math.Clamp(p, 0, 1));
            _cut ??= new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(-4, -4, 32, 32)), _slashCut);
            _cut.Geometry2 = null;
            _cut.Geometry2 = _slashCut;
        }
        else if (Kind == IconMorphKind.Heart && _momentActive && _moment is not null)
        {
            var u = ArcEasings.Enter.Ease(Math.Clamp(_moment.Value / 0.5, 0, 1));
            _ringPen ??= new Pen(_heartBrush, 3);
            _ringPen.Thickness = Math.Max(0, 3 * (1 - u));
        }
    }

    /// <summary>The slash's 5-unit stroke with round caps as a polygon (the mask that cuts the bell).</summary>
    private void UpdateSlashCut(double length)
    {
        const int capSteps = 8;
        var points = _slashCut.Points;
        var start = new Point(3, 3);
        var end = new Point(3 + 18 * length, 3 + 18 * length);
        const double half = 2.5;
        // Direction of the slash is (1, 1)/√2; caps sweep half circles around each end.
        var baseAngle = Math.PI / 4;
        var needed = 2 * (capSteps + 1);
        if (points.Count != needed)
        {
            points.Clear();
            for (var i = 0; i < needed; i++)
            {
                points.Add(default);
            }
        }
        for (var i = 0; i <= capSteps; i++)
        {
            var a = baseAngle - Math.PI / 2 + Math.PI * i / capSteps;
            points[i] = new Point(end.X + half * Math.Cos(a), end.Y + half * Math.Sin(a));
        }
        for (var i = 0; i <= capSteps; i++)
        {
            var a = baseAngle + Math.PI / 2 + Math.PI * i / capSteps;
            points[capSteps + 1 + i] = new Point(start.X + half * Math.Cos(a), start.Y + half * Math.Sin(a));
        }
    }

    private void UpdateHeartColor()
    {
        if (Kind != IconMorphKind.Heart)
        {
            return;
        }
        var baseColor = (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;
        var like = (LikeBrush as ISolidColorBrush)?.Color ?? baseColor;
        var t = Math.Clamp(_fade?.Value ?? 0, 0, 1);
        var color = Oklch.MixOklab(baseColor, like, t);
        _heartBrush.Color = color;
        _heartFill.Color = Color.FromArgb((byte)Math.Round(color.A * t), color.R, color.G, color.B);
    }

    protected override Size MeasureOverride(Size availableSize) => new(IconSize, IconSize);

    private Pen GetPen(IBrush brush, double thickness)
    {
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - thickness) > double.Epsilon)
        {
            _pen = new Pen(brush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = thickness;
        }
        return _pen;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground;
        if (brush is null || _primary is null)
        {
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            var pen = GetPen(brush, StrokeThickness);
            switch (Kind)
            {
                case IconMorphKind.Menu:
                    RenderMenu(context, pen);
                    break;
                case IconMorphKind.Play:
                    if (_pathGeometry is not null)
                    {
                        // The solid halves keep Arc's fixed 1.5-unit outline.
                        if (_solidPen is null || !ReferenceEquals(_solidPen.Brush, brush))
                        {
                            _solidPen = new Pen(brush, 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                        }
                        context.DrawGeometry(brush, _solidPen, _pathGeometry);
                    }
                    break;
                case IconMorphKind.Plus:
                    if (_pathGeometry is not null)
                    {
                        context.DrawGeometry(null, pen, _pathGeometry);
                    }
                    break;
                case IconMorphKind.Copy:
                    RenderCopy(context, pen);
                    break;
                case IconMorphKind.Theme:
                    RenderTheme(context, pen);
                    break;
                case IconMorphKind.Chevron:
                    RenderChevron(context, pen);
                    break;
                case IconMorphKind.Bell:
                    RenderBell(context, pen);
                    break;
                case IconMorphKind.Heart:
                    RenderHeart(context);
                    break;
            }
        }
    }

    private static Point Transform(Point p, Point origin, double degrees, double dy)
    {
        var rad = degrees * Math.PI / 180;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var x = p.X - origin.X;
        var y = p.Y - origin.Y;
        return new Point(origin.X + x * cos - y * sin, origin.Y + x * sin + y * cos + dy);
    }

    private void RenderMenu(DrawingContext context, Pen pen)
    {
        var p = _primary!.Value;
        // Outer lines: translate toward the middle while rotating about their own centres.
        context.DrawLine(pen, Transform(new Point(4, 6), new Point(12, 6), 45 * p, 6 * p), Transform(new Point(20, 6), new Point(12, 6), 45 * p, 6 * p));
        context.DrawLine(pen, Transform(new Point(4, 18), new Point(12, 18), -45 * p, -6 * p), Transform(new Point(20, 18), new Point(12, 18), -45 * p, -6 * p));
        var opacity = 1 - Math.Clamp(_fade!.Value, 0, 1);
        if (opacity > 0.001)
        {
            var half = 8 * _secondary!.Value;
            using (context.PushOpacity(opacity))
            {
                context.DrawLine(pen, new Point(12 - half, 12), new Point(12 + half, 12));
            }
        }
    }

    private void RenderCopy(DrawingContext context, Pen pen)
    {
        var p = _primary!.Value;
        var sheetOpacity = 1 - Math.Clamp(_fade!.Value, 0, 1);
        if (sheetOpacity > 0.001)
        {
            using (context.PushOpacity(sheetOpacity))
            {
                using (context.PushTransform(Matrix.CreateTranslation(4 * p, 4 * p)))
                {
                    context.DrawGeometry(null, pen, ArcIcons.Parse(CopySheetPath));
                }
                var s = 1 - 0.3 * p;
                using (context.PushTransform(Matrix.CreateTranslation(-15, -15) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(15, 15)))
                {
                    context.DrawRectangle(null, pen, new RoundedRect(new Rect(8, 8, 14, 14), 2));
                }
            }
        }
        var drawn = Math.Clamp(_draw!.Value, 0, 1);
        var checkOpacity = Math.Clamp(_drawFade!.Value, 0, 1);
        if (drawn > 0.001 && checkOpacity > 0.001)
        {
            using (context.PushOpacity(checkOpacity))
            {
                DrawPartialPolyline(context, pen, CopyCheck, drawn);
            }
        }
    }

    /// <summary>Draws the first <paramref name="fraction"/> of a polyline by length (SVG pathLength).</summary>
    private static void DrawPartialPolyline(DrawingContext context, Pen pen, Point[] points, double fraction)
    {
        double total = 0;
        for (var i = 1; i < points.Length; i++)
        {
            total += PathMorphContour.Distance(points[i - 1], points[i]);
        }
        var remaining = total * fraction;
        for (var i = 1; i < points.Length && remaining > 0; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var length = PathMorphContour.Distance(a, b);
            if (length <= remaining)
            {
                context.DrawLine(pen, a, b);
                remaining -= length;
                continue;
            }
            var f = remaining / length;
            context.DrawLine(pen, a, new Point(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f));
            break;
        }
    }

    private void RenderTheme(DrawingContext context, Pen pen)
    {
        var p = _primary!.Value;
        var r = Math.Max(0, 4.5 + 4 * p);
        // The crescent as an outline: the disc's edge outside the shadow, and the shadow's edge inside the disc.
        if (_bite is not null)
        {
            using (context.PushGeometryClip(_bite))
            {
                context.DrawEllipse(null, pen, new Point(12, 12), r, r);
            }
        }
        using (context.PushGeometryClip(_disc))
        {
            context.DrawEllipse(null, pen, _shadow.Center, 6.5, 6.5);
        }
        var rayOpacity = 1 - Math.Clamp(_fade!.Value, 0, 1);
        if (rayOpacity > 0.001)
        {
            var s = 1 - 0.6 * p;
            var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(s, s) * Matrix.CreateRotation(90 * p * Math.PI / 180) *
                         Matrix.CreateTranslation(12, 12);
            using (context.PushOpacity(rayOpacity))
            using (context.PushTransform(matrix))
            {
                foreach (var (from, to) in Rays)
                {
                    context.DrawLine(pen, from, to);
                }
            }
        }
    }

    private void RenderChevron(DrawingContext context, Pen pen)
    {
        var angle = _primary!.Value;
        var down = ChevronPointing == IconMorphChevronPointing.Down;
        var a = down ? new Point(6, 9) : new Point(9, 6);
        var b = down ? new Point(12, 15) : new Point(15, 12);
        var c = down ? new Point(18, 9) : new Point(9, 18);
        var center = new Point(12, 12);
        var pa = Transform(a, center, angle, 0);
        var pb = Transform(b, center, angle, 0);
        var pc = Transform(c, center, angle, 0);
        context.DrawLine(pen, pa, pb);
        context.DrawLine(pen, pb, pc);
    }

    /// <summary>Keyframes evenly spaced over [0, 1] with <paramref name="easing"/> on every segment (Motion keyframes).</summary>
    private static double Keyframes(double[] values, double t, Easing easing)
    {
        if (t <= 0)
        {
            return values[0];
        }
        if (t >= 1)
        {
            return values[^1];
        }
        var segments = values.Length - 1;
        var scaled = t * segments;
        var index = Math.Min((int)scaled, segments - 1);
        var local = easing.Ease(scaled - index);
        return values[index] + (values[index + 1] - values[index]) * local;
    }

    private void RenderBell(DrawingContext context, Pen pen)
    {
        var slash = Math.Clamp(_primary!.Value, 0, 1);
        var slashOpacity = Math.Clamp(_fade!.Value, 0, 1);
        var time = _momentActive ? _moment!.Value : 1;
        var ringT = Math.Clamp(time / 0.9, 0, 1);
        var clapperT = Math.Clamp((time - 0.04) / 0.9, 0, 1);
        var angle = _momentActive ? Keyframes(RingAngles, ringT, EaseOut) : 0;
        var clapperX = _momentActive ? Keyframes(ClapperOffsets, clapperT, EaseOut) : 0;
        var cut = slashOpacity > 0.01 && slash > 0.001 && _cut is not null;
        using (cut ? context.PushGeometryClip(_cut!) : default)
        {
            var matrix = Matrix.CreateTranslation(-12, -2) * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(12, 2);
            using (context.PushTransform(matrix))
            {
                context.DrawGeometry(null, pen, ArcIcons.Parse(BellPath));
            }
            using (context.PushTransform(Matrix.CreateTranslation(clapperX, 0)))
            {
                context.DrawGeometry(null, pen, ArcIcons.Parse(ClapperPath));
            }
        }
        if (slash > 0.001 && slashOpacity > 0.001)
        {
            using (context.PushOpacity(slashOpacity))
            {
                context.DrawLine(pen, new Point(3, 3), new Point(3 + 18 * slash, 3 + 18 * slash));
            }
        }
    }

    private void RenderHeart(DrawingContext context)
    {
        var thickness = StrokeThickness;
        if (_heartPen is null || Math.Abs(_heartPen.Thickness - thickness) > double.Epsilon)
        {
            _heartPen = new Pen(_heartBrush, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        var time = _momentActive ? _moment!.Value : 1;
        if (_momentActive && IsActive)
        {
            // Ring: r 4 → 13, stroke 3 → 0, opacity .5 → 0 over .5s (enter).
            var u = ArcEasings.Enter.Ease(Math.Clamp(time / 0.5, 0, 1));
            if (_ringPen is { Thickness: > 0.01 })
            {
                using (context.PushOpacity(0.5 * (1 - u)))
                {
                    context.DrawEllipse(null, _ringPen, new Point(12, 12), 4 + 9 * u, 4 + 9 * u);
                }
            }
            // Sparks: out from 7 to 14 units, fading and shrinking, .55s after .05s (enter).
            var v = ArcEasings.Enter.Ease(Math.Clamp((time - 0.05) / 0.55, 0, 1));
            if (v < 1)
            {
                using (context.PushOpacity(1 - v))
                {
                    foreach (var (x, y, r) in Sparks)
                    {
                        var distance = 7 + 7 * v;
                        var radius = r * (1 - 0.7 * v);
                        context.DrawEllipse(_heartBrush, null, new Point(12 + x * distance, 12 + y * distance), radius, radius);
                    }
                }
            }
        }
        var scale = _momentActive && IsActive ? Keyframes(HeartScales, Math.Clamp(time / 0.45, 0, 1), ArcEasings.Enter) : 1;
        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(12, 12)))
        {
            context.DrawGeometry(_heartFill, _heartPen, ArcIcons.Parse(HeartPath));
        }
    }
}
