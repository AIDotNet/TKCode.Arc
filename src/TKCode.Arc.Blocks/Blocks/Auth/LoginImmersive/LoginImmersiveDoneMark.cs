using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The success mark on the Done step (56 × 56): a ring drawn clockwise from twelve o'clock, then a check drawn from its
/// short arm. Ring: radius 26, 1.5 wide, 575 ms on the in-out curve after 140 ms. Check: from 580 ms it fades in over
/// 100 ms while it draws on Arc's quick spring (0.26 s, no bounce). Reduced motion and Off draw both at once.
/// </summary>
/// <remarks>
/// Code-driven: two stroke lengths on a tween and a spring with delays. Drawn as short line segments so a frame
/// allocates nothing.
/// </remarks>
internal sealed class LoginImmersiveDoneMark : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LoginImmersiveDoneMark>();

    /// <summary>The drawing's box.</summary>
    public const double DesignSize = 56;

    private const double Radius = 26;
    private const int RingSegments = 96;

    private static readonly Point s_checkStart = new(19, 29);
    private static readonly Point s_checkCorner = new(25, 35);
    private static readonly Point s_checkEnd = new(38, 22);
    private static readonly SpringSpec s_quick = SpringSpec.FromVisual(0.26, 0);
    private static readonly TimeSpan s_ringDelay = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan s_ringDuration = TimeSpan.FromMilliseconds(575);
    private static readonly TimeSpan s_checkDelay = TimeSpan.FromMilliseconds(580);
    private static readonly TimeSpan s_checkFade = TimeSpan.FromMilliseconds(100);
    private static readonly Easing s_linear = new LinearEasing();

    private MotionValue<double>? _ringValue;
    private MotionValue<double>? _checkValue;
    private MotionValue<double>? _checkOpacityValue;
    private double _ring = 1;
    private double _check = 1;
    private double _checkOpacity = 1;
    private Pen? _ringPen;
    private Pen? _checkPen;
    private IBrush? _penBrush;

    static LoginImmersiveDoneMark()
    {
        AffectsRender<LoginImmersiveDoneMark>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginImmersiveDoneMark>(false);
        ClipToBoundsProperty.OverrideDefaultValue<LoginImmersiveDoneMark>(false);
    }

    /// <summary>The mark's colour (the success colour).</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>How much of the ring is drawn, 0..1 (tests read it).</summary>
    internal double Ring => _ring;

    /// <summary>How much of the check is drawn, 0..1 (tests read it).</summary>
    internal double Check => _check;

    /// <summary>The check's opacity (tests read it).</summary>
    internal double CheckOpacity => _checkOpacity;

    /// <summary>Draws the ring and then the check from nothing; reduced motion and Off show the finished mark.</summary>
    public void Replay()
    {
        EnsureValues();
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full || !IsLoaded)
        {
            ShowDrawn();
            return;
        }
        _ringValue!.Jump(0);
        _checkValue!.Jump(0);
        _checkOpacityValue!.Jump(0);
        _ = _ringValue.AnimateTo(1, MotionSpec.Tween(s_ringDuration, ArcEasings.InOut, s_ringDelay));
        _ = _checkOpacityValue.AnimateTo(1, MotionSpec.Tween(s_checkFade, s_linear, s_checkDelay));
        _ = _checkValue.AnimateTo(1, MotionSpec.Spring(s_quick, s_checkDelay));
    }

    /// <summary>Shows the finished mark without motion.</summary>
    public void ShowDrawn()
    {
        EnsureValues();
        _ringValue!.Jump(1);
        _checkValue!.Jump(1);
        _checkOpacityValue!.Jump(1);
    }

    private void EnsureValues()
    {
        if (_ringValue is not null)
        {
            return;
        }
        _ringValue = new MotionValue<double>(this, 1, MotionVectors.Double);
        _ringValue.Changed += v =>
        {
            _ring = v;
            InvalidateVisual();
        };
        _checkValue = new MotionValue<double>(this, 1, MotionVectors.Double);
        _checkValue.Changed += v =>
        {
            _check = v;
            InvalidateVisual();
        };
        _checkOpacityValue = new MotionValue<double>(this, 1, MotionVectors.Double);
        _checkOpacityValue.Changed += v =>
        {
            _checkOpacity = v;
            InvalidateVisual();
        };
    }

    protected override Size MeasureOverride(Size availableSize) => new(DesignSize, DesignSize);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (_ringPen is null || !ReferenceEquals(_penBrush, brush))
        {
            _ringPen = new Pen(brush, 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _checkPen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / DesignSize;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            DrawRing(context, Math.Clamp(_ring, 0, 1));
            var check = Math.Clamp(_check, 0, 1);
            if (check > 0 && _checkOpacity > 0.001)
            {
                using (context.PushOpacity(Math.Clamp(_checkOpacity, 0, 1)))
                {
                    DrawCheck(context, check);
                }
            }
        }
    }

    private void DrawRing(DrawingContext context, double amount)
    {
        if (amount <= 0)
        {
            return;
        }
        var center = new Point(DesignSize / 2, DesignSize / 2);
        var total = amount * RingSegments;
        var whole = (int)Math.Floor(total);
        var previous = PointOnRing(center, 0);
        for (var i = 1; i <= whole; i++)
        {
            var next = PointOnRing(center, (double)i / RingSegments);
            context.DrawLine(_ringPen!, previous, next);
            previous = next;
        }
        var rest = total - whole;
        if (rest > 0.001 && whole < RingSegments)
        {
            context.DrawLine(_ringPen!, previous, PointOnRing(center, (whole + rest) / RingSegments));
        }
    }

    /// <summary>A point on the ring at <paramref name="fraction"/> of a turn clockwise from twelve o'clock.</summary>
    private static Point PointOnRing(Point center, double fraction)
    {
        var angle = fraction * 2 * Math.PI - Math.PI / 2;
        return new Point(center.X + Radius * Math.Cos(angle), center.Y + Radius * Math.Sin(angle));
    }

    private void DrawCheck(DrawingContext context, double amount)
    {
        var first = Distance(s_checkStart, s_checkCorner);
        var second = Distance(s_checkCorner, s_checkEnd);
        var length = amount * (first + second);
        if (length <= first)
        {
            context.DrawLine(_checkPen!, s_checkStart, Lerp(s_checkStart, s_checkCorner, length / first));
            return;
        }
        context.DrawLine(_checkPen!, s_checkStart, s_checkCorner);
        context.DrawLine(_checkPen!, s_checkCorner, Lerp(s_checkCorner, s_checkEnd, (length - first) / second));
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
