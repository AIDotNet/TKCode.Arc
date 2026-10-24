using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The envelope on the Sent step (96 × 80): a letter drops into an open envelope and the flap folds shut. Drawn from
/// plain shapes in paint order: the open back flap (a triangle above the body), the letter (a card with two ink lines),
/// the body (it hides the letter once it is in), the two folds of the front and the closed front flap. One value turns
/// the flap: from −1 (open, pointing up) through 0 (flat on the hinge) to 1 (closed, pointing down).
/// </summary>
/// <remarks>
/// Timeline (from <see cref="Replay"/>, measured on the reference): the letter waits 170 ms, then falls 30 units on the
/// smooth spring while fading in over 165 ms; the flap turns from 575 ms on the morph spring without bounce (0.42 s).
/// Reduced motion and Off show the closed envelope with the letter inside. Code-driven: three values on springs with
/// delays drive one drawing; nothing here is a property a ControlTheme could animate.
/// </remarks>
internal sealed class LoginImmersiveEnvelope : Control
{
    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        TextElement.ForegroundProperty.AddOwner<LoginImmersiveEnvelope>();

    public static readonly StyledProperty<IBrush?> PaperBrushProperty =
        AvaloniaProperty.Register<LoginImmersiveEnvelope, IBrush?>(nameof(PaperBrush));

    public static readonly StyledProperty<double> InkOpacityProperty =
        AvaloniaProperty.Register<LoginImmersiveEnvelope, double>(nameof(InkOpacity), 0.42);

    /// <summary>The drawing's box.</summary>
    public static readonly Size DesignSize = new(96, 80);

    private const double StrokeWidth = 1.5;
    private const double Hinge = 32;
    private const double LetterDrop = 30;

    private static readonly SpringSpec s_smooth = SpringSpec.FromVisual(0.4, 0);
    private static readonly SpringSpec s_morph = SpringSpec.FromVisual(0.42, 0);
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly TimeSpan s_letterDelay = TimeSpan.FromMilliseconds(170);
    private static readonly TimeSpan s_letterFade = TimeSpan.FromMilliseconds(165);
    private static readonly TimeSpan s_flapDelay = TimeSpan.FromMilliseconds(575);

    private static readonly Geometry s_body = new RectangleGeometry(new Rect(8, 32, 80, 44), 4, 4);
    private static readonly Geometry s_letter = new RectangleGeometry(new Rect(20, 38, 56, 34), 3, 3);
    private static readonly Geometry s_flap = Triangle(new Point(9, Hinge), new Point(87, Hinge), new Point(48, 56));

    private MotionValue<double>? _letterY;
    private MotionValue<double>? _letterOpacity;
    private MotionValue<double>? _flap;
    private double _y;
    private double _opacity = 1;
    private double _v = 1;
    private Pen? _pen;
    private Pen? _inkPen;
    private IBrush? _penBrush;

    static LoginImmersiveEnvelope()
    {
        AffectsRender<LoginImmersiveEnvelope>(InkBrushProperty, PaperBrushProperty, InkOpacityProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoginImmersiveEnvelope>(false);
        ClipToBoundsProperty.OverrideDefaultValue<LoginImmersiveEnvelope>(false);
    }

    /// <summary>The outlines (the foreground colour).</summary>
    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    /// <summary>The paper of the envelope and the letter (the surface colour).</summary>
    public IBrush? PaperBrush { get => GetValue(PaperBrushProperty); set => SetValue(PaperBrushProperty, value); }

    /// <summary>Opacity of the folds and the letter's lines (.42).</summary>
    public double InkOpacity { get => GetValue(InkOpacityProperty); set => SetValue(InkOpacityProperty, value); }

    /// <summary>The flap's turn: −1 open, 0 flat, 1 closed (tests read it).</summary>
    internal double Flap => _v;

    /// <summary>The letter's offset above its place inside the envelope (tests read it).</summary>
    internal double LetterOffset => _y;

    /// <summary>The letter's opacity (tests read it).</summary>
    internal double LetterOpacity => _opacity;

    private static Geometry Triangle(Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(a, true);
            g.LineTo(b);
            g.LineTo(c);
            g.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>Plays the drop and the fold from the start (every send and resend); reduced motion and Off show it closed.</summary>
    public void Replay()
    {
        EnsureValues();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level != ArcMotionLevel.Full || !IsLoaded)
        {
            ShowClosed();
            return;
        }
        _letterY!.Jump(-LetterDrop);
        _letterOpacity!.Jump(0);
        _flap!.Jump(-1);
        _ = _letterY.AnimateTo(0, MotionSpec.Spring(s_smooth, s_letterDelay));
        _ = _letterOpacity.AnimateTo(1, MotionSpec.Tween(s_letterFade, s_easeOut, s_letterDelay));
        _ = _flap.AnimateTo(1, MotionSpec.Spring(s_morph, s_flapDelay));
    }

    /// <summary>Shows the envelope closed with the letter inside, without motion.</summary>
    public void ShowClosed()
    {
        EnsureValues();
        _letterY!.Jump(0);
        _letterOpacity!.Jump(1);
        _flap!.Jump(1);
    }

    private void EnsureValues()
    {
        if (_letterY is not null)
        {
            return;
        }
        _letterY = new MotionValue<double>(this, 0, MotionVectors.Double);
        _letterY.Changed += y =>
        {
            _y = y;
            InvalidateVisual();
        };
        _letterOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _letterOpacity.Changed += o =>
        {
            _opacity = o;
            InvalidateVisual();
        };
        _flap = new MotionValue<double>(this, 1, MotionVectors.Double);
        _flap.Changed += v =>
        {
            _v = v;
            InvalidateVisual();
        };
    }

    protected override Size MeasureOverride(Size availableSize) => DesignSize;

    public override void Render(DrawingContext context)
    {
        var ink = InkBrush;
        var paper = PaperBrush;
        if (ink is null)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, ink))
        {
            _pen = new Pen(ink, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _inkPen = new Pen(ink, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = ink;
        }
        var scale = Math.Min(Bounds.Width / DesignSize.Width, Bounds.Height / DesignSize.Height);
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            // The back flap: the flap mirrored up over the hinge while v < 0, flattening as v reaches 0.
            if (_v < 0)
            {
                DrawFlap(context, paper, _v);
            }
            if (_opacity > 0.001)
            {
                using (context.PushTransform(Matrix.CreateTranslation(0, _y)))
                using (context.PushOpacity(_opacity))
                {
                    context.DrawGeometry(paper, _pen, s_letter);
                    using (context.PushOpacity(InkOpacity))
                    {
                        context.DrawLine(_inkPen!, new Point(28, 46), new Point(68, 46));
                        context.DrawLine(_inkPen!, new Point(28, 53), new Point(56, 53));
                    }
                }
            }
            context.DrawGeometry(paper, _pen, s_body);
            using (context.PushOpacity(InkOpacity))
            {
                context.DrawLine(_inkPen!, new Point(10, 74), new Point(34, 56));
                context.DrawLine(_inkPen!, new Point(86, 74), new Point(62, 56));
            }
            if (_v > 0)
            {
                DrawFlap(context, paper, _v);
            }
        }
    }

    private void DrawFlap(DrawingContext context, IBrush? paper, double v)
    {
        if (Math.Abs(v) < 0.002)
        {
            return;
        }
        // The flap is the closed triangle scaled by v about the hinge (like the web original's transform).
        var turn = Matrix.CreateTranslation(0, -Hinge) * Matrix.CreateScale(1, v) * Matrix.CreateTranslation(0, Hinge);
        using (context.PushTransform(turn))
        {
            context.DrawGeometry(paper, _pen, s_flap);
        }
    }
}
