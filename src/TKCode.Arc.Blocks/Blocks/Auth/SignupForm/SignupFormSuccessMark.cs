// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): the completion mark of signup-form.tsx.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The completion mark (Arc's 48px <c>.success</c> SVG): a 1.5px ring of radius 22.25 drawn clockwise from 12 o'clock
/// (pathLength over 480 ms on the in-out curve after 160 ms, its opacity 0 → 1 over 120 ms) and a 2px round check
/// <c>M15.5 24.5l5.5 5.5 11.5-12</c> drawn after 500 ms (320 ms on the enter curve, opacity over 120 ms). Reduced motion and
/// Off show both drawn at once (Arc: <c>initial={reduce ? false : …}</c>). Colored by <see cref="Foreground"/> (success).
/// </summary>
/// <remarks>Code-driven: a stroke that draws itself along its length has no ControlTheme transition.</remarks>
internal sealed class SignupFormSuccessMark : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SignupFormSuccessMark>();

    private const double ViewBox = 48;
    private static readonly Point P0 = new(15.5, 24.5);
    private static readonly Point P1 = new(21, 30);
    private static readonly Point P2 = new(32.5, 18);
    private static readonly double L1 = Distance(P0, P1);
    private static readonly double L2 = Distance(P1, P2);

    // No size of its own (ArcRing defaults to 16): it fills the 46px box ArrangeOverride gives it.
    private readonly ArcRing _ring = new() { Width = double.NaN, Height = double.NaN, Thickness = 1.5, IsRoundCap = false, Value = 1, IsHitTestVisible = false };
    private MotionValue<double>? _ringOpacity;
    private MotionValue<double>? _ringDraw;
    private MotionValue<double>? _checkOpacity;
    private MotionValue<double>? _checkDraw;
    private double _check = 1;
    private double _checkAlpha = 1;
    private Pen? _pen;
    private IBrush? _penBrush;

    static SignupFormSuccessMark()
    {
        AffectsRender<SignupFormSuccessMark>(ForegroundProperty);
    }

    public SignupFormSuccessMark()
    {
        VisualChildren.Add(_ring);
        LogicalChildren.Add(_ring);
        IsHitTestVisible = false;
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>How much of the ring is drawn, 0 to 1 (tests read it).</summary>
    internal double RingProgress => _ring.Value;

    /// <summary>The ring's opacity (tests read it).</summary>
    internal double RingOpacity => _ring.Opacity;

    /// <summary>How much of the check is drawn, 0 to 1 (tests read it).</summary>
    internal double CheckProgress => _check;

    /// <summary>The check's opacity (tests read it).</summary>
    internal double CheckOpacity => _checkAlpha;

    /// <summary>Draws the mark in: animated at full motion, at once otherwise.</summary>
    public void Play()
    {
        Ensure();
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            Show();
            return;
        }
        _ringOpacity!.Jump(0);
        _ringDraw!.Jump(0);
        _checkOpacity!.Jump(0);
        _checkDraw!.Jump(0);
        var ringDelay = TimeSpan.FromMilliseconds(160);
        var checkDelay = TimeSpan.FromMilliseconds(500);
        _ = _ringOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, SignupFormSwap.EaseOut, ringDelay));
        _ = _ringDraw.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.InOut, ringDelay));
        _ = _checkOpacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, SignupFormSwap.EaseOut, checkDelay));
        _ = _checkDraw.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard + TimeSpan.FromMilliseconds(80), ArcEasings.Enter, checkDelay));
    }

    /// <summary>Shows the mark fully drawn.</summary>
    public void Show()
    {
        Ensure();
        _ringOpacity!.Jump(1);
        _ringDraw!.Jump(1);
        _checkOpacity!.Jump(1);
        _checkDraw!.Jump(1);
    }

    private void Ensure()
    {
        if (_ringOpacity is not null)
        {
            return;
        }
        _ringOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _ringOpacity.Changed += v => _ring.Opacity = v;
        _ringDraw = new MotionValue<double>(this, 1, MotionVectors.Double);
        _ringDraw.Changed += v => _ring.Value = v;
        _checkOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _checkOpacity.Changed += v =>
        {
            _checkAlpha = v;
            InvalidateVisual();
        };
        _checkDraw = new MotionValue<double>(this, 1, MotionVectors.Double);
        _checkDraw.Changed += v =>
        {
            _check = v;
            InvalidateVisual();
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _ring.Measure(availableSize);
        return new Size(ViewBox, ViewBox);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var scale = Math.Min(finalSize.Width, finalSize.Height) / ViewBox;
        // r = 22.25 with a 1.5 stroke: the ring's box is 46 inside the 48 view box.
        var side = 46 * scale;
        _ring.Arrange(new Rect((finalSize.Width - side) / 2, (finalSize.Height - side) / 2, side, side));
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ringOpacity?.Stop();
        _ringDraw?.Stop();
        _checkOpacity?.Stop();
        _checkDraw?.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (_checkAlpha <= 0 || Foreground is not { } brush)
        {
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / ViewBox;
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            // In view-box units: the transform below scales it with the mark.
            _penBrush = brush;
            _pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        var length = Math.Clamp(_check, 0, 1) * (L1 + L2);
        using (context.PushOpacity(_checkAlpha))
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            var first = Math.Min(length, L1);
            context.DrawLine(_pen, P0, Lerp(P0, P1, first / L1));
            if (length > L1)
            {
                context.DrawLine(_pen, P1, Lerp(P1, P2, (length - L1) / L2));
            }
        }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
