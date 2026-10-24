// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): PasskeyPulse and the success mark of login-centered.tsx.
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The fingerprint in the passkey button while the device is asked: two rings leave it, one after the other, and stop on
/// their own. Each ring is a 1.5px circle 4px wider than the glyph, growing from .7 to 2.1 times while it fades from .6
/// to nothing over 1.2s on the standard curve; the second starts .6s later. No rings under reduced motion. Drawn in the
/// button's text color.
/// </summary>
/// <remarks>Self-drawn so the rings can overflow the 16px glyph without taking layout; the timeline is one tween.</remarks>
internal sealed class LoginCenteredPulse : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LoginCenteredPulse>();

    internal const double GlyphSize = 16;
    private const double RingDiameter = GlyphSize + 4;
    private const double RingDuration = 1.2;
    private const double RingDelay = 0.6;
    private const double Total = RingDuration + RingDelay;

    private static readonly Easing s_linear = new LinearEasing();
    private MotionValue<double>? _time;
    private double _seconds = Total;
    private Pen? _glyphPen;
    private Pen? _ringPen;
    private IBrush? _penBrush;

    static LoginCenteredPulse()
    {
        AffectsRender<LoginCenteredPulse>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginCenteredPulse>(false);
        ClipToBoundsProperty.OverrideDefaultValue<LoginCenteredPulse>(false);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Seconds since the rings started (1.8 once they are done; tests read it).</summary>
    internal double Seconds => _seconds;

    /// <summary>A ring's scale and opacity at <paramref name="seconds"/> after the start; opacity 0 before and after it runs.</summary>
    internal static (double Scale, double Opacity) RingAt(int index, double seconds)
    {
        var local = (seconds - index * RingDelay) / RingDuration;
        if (local < 0 || local >= 1)
        {
            return (0.7, 0);
        }
        var eased = ArcEasings.Standard.Ease(local);
        return (0.7 + 1.4 * eased, 0.6 * (1 - eased));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!ArcMotion.IsFull(this))
        {
            _seconds = Total;
            return;
        }
        _time ??= CreateTime();
        _time.Jump(0);
        _ = _time.AnimateTo(Total, MotionSpec.Tween(TimeSpan.FromSeconds(Total), s_linear));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _time?.Stop();
    }

    private MotionValue<double> CreateTime()
    {
        var time = new MotionValue<double>(this, 0, MotionVectors.Double);
        time.Changed += t =>
        {
            _seconds = t;
            InvalidateVisual();
        };
        return time;
    }

    protected override Size MeasureOverride(Size availableSize) => new(GlyphSize, GlyphSize);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (!ReferenceEquals(brush, _penBrush))
        {
            _penBrush = brush;
            _glyphPen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _ringPen = new Pen(brush, 1.5);
        }
        var center = new Point(GlyphSize / 2, GlyphSize / 2);
        for (var i = 0; i < 2; i++)
        {
            var (scale, opacity) = RingAt(i, _seconds);
            if (opacity <= 0)
            {
                continue;
            }
            // The ring's border box is 20px with its 1.5px border inside (centerline radius 9.25); the whole box scales,
            // the border with it.
            using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                         Matrix.CreateTranslation(center.X, center.Y)))
            using (context.PushOpacity(opacity))
            {
                context.DrawEllipse(null, _ringPen!, center, RingDiameter / 2 - 0.75, RingDiameter / 2 - 0.75);
            }
        }
        using (context.PushTransform(Matrix.CreateScale(GlyphSize / 24, GlyphSize / 24)))
        {
            context.DrawGeometry(null, _glyphPen!, ArcIcons.Fingerprint);
        }
    }
}

/// <summary>
/// The check at the top of the signed-in view: a 1.5px success ring that draws itself clockwise from twelve o'clock over
/// 480ms on the in-out curve after 160ms, then a 2px tick over 320ms on the enter curve after 520ms. Each stroke appears
/// over 120ms as it starts. Under reduced motion (and off) both are simply there.
/// </summary>
internal sealed class LoginCenteredSuccessMark : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LoginCenteredSuccessMark>();

    internal const double MarkSize = 48;

    private static readonly Geometry s_ring = StreamGeometry.Parse("M24 1.75A22.25 22.25 0 0 1 24 46.25A22.25 22.25 0 0 1 24 1.75");
    private static readonly Geometry s_tick = StreamGeometry.Parse("M15.5 24.5l5.5 5.5 11.5-12");

    private MotionValue<double>? _ring;
    private MotionValue<double>? _ringOpacity;
    private MotionValue<double>? _tick;
    private MotionValue<double>? _tickOpacity;
    private double _ringProgress = 1;
    private double _ringAlpha = 1;
    private double _tickProgress = 1;
    private double _tickAlpha = 1;
    private Pen? _ringPen;
    private Pen? _tickPen;
    private IBrush? _penBrush;

    static LoginCenteredSuccessMark()
    {
        AffectsRender<LoginCenteredSuccessMark>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginCenteredSuccessMark>(false);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>How much of the ring and of the tick is drawn, 0 to 1 (tests read them).</summary>
    internal double RingProgress => _ringProgress;

    internal double TickProgress => _tickProgress;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!ArcMotion.IsFull(this))
        {
            _ringProgress = _ringAlpha = _tickProgress = _tickAlpha = 1;
            return;
        }
        _ring ??= Value(v => _ringProgress = v);
        _ringOpacity ??= Value(v => _ringAlpha = v);
        _tick ??= Value(v => _tickProgress = v);
        _tickOpacity ??= Value(v => _tickAlpha = v);
        _ring.Jump(0);
        _ringOpacity.Jump(0);
        _tick.Jump(0);
        _tickOpacity.Jump(0);
        var ringDelay = TimeSpan.FromMilliseconds(160);
        var tickDelay = TimeSpan.FromMilliseconds(520);
        _ = _ring.AnimateTo(1, MotionSpec.Tween(LoginCenteredMotion.Considered, ArcEasings.InOut, ringDelay));
        _ = _ringOpacity.AnimateTo(1, MotionSpec.Tween(LoginCenteredMotion.Instant, LoginCenteredMotion.EaseOut, ringDelay));
        _ = _tick.AnimateTo(1, MotionSpec.Tween(LoginCenteredMotion.Standard + TimeSpan.FromMilliseconds(80), ArcEasings.Enter, tickDelay));
        _ = _tickOpacity.AnimateTo(1, MotionSpec.Tween(LoginCenteredMotion.Instant, LoginCenteredMotion.EaseOut, tickDelay));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ring?.Stop();
        _ringOpacity?.Stop();
        _tick?.Stop();
        _tickOpacity?.Stop();
    }

    private MotionValue<double> Value(Action<double> apply)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            apply(v);
            InvalidateVisual();
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize) => new(MarkSize, MarkSize);

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush)
        {
            return;
        }
        if (!ReferenceEquals(brush, _penBrush))
        {
            _penBrush = brush;
            _ringPen = new Pen(brush, 1.5);
            _tickPen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        var scale = Bounds.Width / MarkSize;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            Stroke(context, s_ring, _ringPen!, _ringProgress, _ringAlpha);
            Stroke(context, s_tick, _tickPen!, _tickProgress, _tickAlpha);
        }
    }

    private static void Stroke(DrawingContext context, Geometry geometry, Pen pen, double progress, double alpha)
    {
        if (progress <= 0 || alpha <= 0)
        {
            return;
        }
        using (context.PushOpacity(alpha))
        {
            if (progress >= 1)
            {
                context.DrawGeometry(null, pen, geometry);
                return;
            }
            var length = geometry.ContourLength;
            if (length > 0 && geometry.TryGetSegment(0, length * progress, true, out var segment) && segment is not null)
            {
                context.DrawGeometry(null, pen, segment);
            }
        }
    }
}
