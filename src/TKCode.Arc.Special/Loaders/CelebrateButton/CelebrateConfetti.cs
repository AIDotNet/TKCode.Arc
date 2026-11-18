using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The confetti layer of <see cref="CelebrateButton"/>: one control laid over the button that draws every piece in its
/// own <see cref="Render"/>, past its bounds (up to the reach set by the button's token) and never hit tested. A burst
/// fills a fixed array of pieces and starts a <see cref="LiveLoop"/>; the loop advances them, drives the button's kick and
/// stops when the last piece has faded, so an idle button requests no frames.
/// </summary>
/// <remarks>
/// Per-frame particles with their own physics are not something a ControlTheme transition can express, hence the loop.
/// A frame allocates nothing: the pieces are structs in a preallocated array, transforms are value types, and the
/// brushes (one per palette color and opacity step) and the triangle are built once per burst.
/// </remarks>
internal sealed class CelebrateConfetti : Control
{
    private const int AlphaSteps = 32;
    private const double MaxStep = 1.0 / 20;
    private const double KickDelay = 0.012;

    private readonly ConfettiPiece[] _pieces = new ConfettiPiece[CelebrateConfettiMath.MaxLive];
    private readonly LiveLoop _loop;
    private int _live;
    private Rect _button;
    private double _kickClock = double.NaN;
    private Color[] _palette = [];
    private IImmutableSolidColorBrush?[] _brushes = [];
    private static readonly StreamGeometry s_triangle = BuildTriangle();

    public CelebrateConfetti()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        // Decoration only: hidden from assistive technology (Arc's canvas is aria-hidden).
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        _loop = new LiveLoop(this, Step) { ViewportMargin = 200 };
        _loop.RunningChanged += running =>
        {
            if (!running && !double.IsNaN(_kickClock))
            {
                // Paused or stopped mid-kick: rest the button at its normal size.
                _kickClock = double.NaN;
                KickChanged?.Invoke(1);
            }
        };
    }

    /// <summary>Called with the button's kick scale on every frame of a burst (1 when it ends).</summary>
    public Action<double>? KickChanged { get; set; }

    /// <summary>The reach of the drawing past the button: left and right, above, below.</summary>
    public double ReachSide { get; set; } = 150;

    public double ReachTop { get; set; } = 190;

    public double ReachBottom { get; set; } = 110;

    /// <summary>The seed source of the pieces (tests set a seeded one).</summary>
    internal Random Random { get; set; } = new();

    /// <summary>Pieces alive now.</summary>
    internal int LiveCount => _live;

    /// <summary>The live pieces (tests read them).</summary>
    internal ReadOnlySpan<ConfettiPiece> Pieces => _pieces.AsSpan(0, _live);

    /// <summary>True while the frame loop is wanted (a burst is still in the air or the kick is still playing).</summary>
    internal bool IsActive => _loop.IsActive;

    /// <summary>
    /// Launches <paramref name="count"/> pieces from the outline of the button at <paramref name="button"/> (in this
    /// layer's coordinates) in the colors of <paramref name="palette"/> (weighted when <paramref name="weighted"/> is the
    /// default five-tone palette).
    /// </summary>
    public void Burst(Rect button, IReadOnlyList<Color> palette, bool weighted, int count)
    {
        if (palette.Count == 0)
        {
            return;
        }
        SetPalette(palette);
        _button = button;
        var first = _live;
        _live = CelebrateConfettiMath.Emit(_pieces, _live, count, button.Width, button.Height, palette.Count, weighted, Random);
        for (var i = first; i < _live; i++)
        {
            _pieces[i].X += button.X;
            _pieces[i].Y += button.Y;
        }
        // The kick leaves a frame after the status flips, with the first pieces (Arc: ~12 ms later).
        _kickClock = -KickDelay;
        KickChanged?.Invoke(1);
        _loop.Start();
        InvalidateVisual();
    }

    /// <summary>Drops every piece and stops the loop.</summary>
    public void Clear()
    {
        _live = 0;
        _loop.Stop();
        if (!double.IsNaN(_kickClock))
        {
            _kickClock = double.NaN;
            KickChanged?.Invoke(1);
        }
        InvalidateVisual();
    }

    /// <summary>Advances the burst by <paramref name="ms"/> milliseconds in 60 Hz steps (tests and the snapshot harness).</summary>
    internal void AdvanceFrames(double ms)
    {
        var dt = 1.0 / 60;
        for (var t = 0.0; t < ms - 0.001; t += 1000.0 / 60)
        {
            if (!Step(dt))
            {
                _loop.Stop();
                break;
            }
        }
    }

    private bool Step(double dt)
    {
        dt = Math.Min(dt, MaxStep);
        _live = CelebrateConfettiMath.Step(_pieces, _live, dt);
        if (!double.IsNaN(_kickClock))
        {
            _kickClock += dt;
            var kick = CelebrateConfettiMath.Kick(_kickClock);
            if (_kickClock >= CelebrateConfettiMath.KickUpSeconds + CelebrateConfettiMath.KickDownSeconds)
            {
                _kickClock = double.NaN;
                kick = 1;
            }
            KickChanged?.Invoke(kick);
        }
        InvalidateVisual();
        return _live > 0 || !double.IsNaN(_kickClock);
    }

    private void SetPalette(IReadOnlyList<Color> palette)
    {
        var same = _palette.Length == palette.Count;
        for (var i = 0; same && i < palette.Count; i++)
        {
            same = _palette[i] == palette[i];
        }
        if (same)
        {
            return;
        }
        if (_live > 0)
        {
            // Pieces in the air keep their color index; a palette of another size would misread them.
            _live = 0;
        }
        _palette = new Color[palette.Count];
        for (var i = 0; i < palette.Count; i++)
        {
            _palette[i] = palette[i];
        }
        _brushes = new IImmutableSolidColorBrush?[palette.Count * (AlphaSteps + 1)];
    }

    private IImmutableSolidColorBrush Brush(int color, double alpha)
    {
        var step = (int)Math.Round(Math.Clamp(alpha, 0, 1) * AlphaSteps);
        var index = (color * (AlphaSteps + 1)) + step;
        return _brushes[index] ??= new ImmutableSolidColorBrush(_palette[color], (double)step / AlphaSteps);
    }

    public override void Render(DrawingContext context)
    {
        if (_live == 0 || _palette.Length == 0)
        {
            return;
        }
        var area = new Rect(_button.X - ReachSide, _button.Y - ReachTop, _button.Width + (2 * ReachSide),
            _button.Height + ReachTop + ReachBottom);
        using (context.PushClip(area))
        {
            for (var i = 0; i < _live; i++)
            {
                ref readonly var p = ref _pieces[i];
                var alpha = CelebrateConfettiMath.Alpha(p);
                var grow = CelebrateConfettiMath.Grow(p);
                if (alpha <= 0 || grow <= 0)
                {
                    continue;
                }
                var brush = Brush(Math.Min(p.Color, _palette.Length - 1), alpha);
                if (p.Shape == ConfettiShape.Circle)
                {
                    var radius = p.Width / 2 * grow;
                    context.DrawEllipse(brush, null, new Point(p.X, p.Y), radius, radius);
                    continue;
                }
                var squash = Math.Cos(p.Flip);
                squash = squash < 0 ? Math.Min(squash, -0.06) : Math.Max(squash, 0.06);
                var m = Matrix.CreateScale(grow, grow * squash) * Matrix.CreateRotation(p.Rotation)
                        * Matrix.CreateTranslation(p.X, p.Y);
                using (context.PushTransform(m))
                {
                    switch (p.Shape)
                    {
                        case ConfettiShape.Ribbon:
                            context.DrawRectangle(brush, null,
                                new RoundedRect(new Rect(-p.Width / 2, -p.Height / 2, p.Width, p.Height), p.Height / 2));
                            break;
                        case ConfettiShape.Square:
                            context.DrawRectangle(brush, null,
                                new RoundedRect(new Rect(-p.Width / 2, -p.Height / 2, p.Width, p.Height), 1.5));
                            break;
                        default:
                            using (context.PushTransform(Matrix.CreateScale(p.Width, p.Width)))
                            {
                                context.DrawGeometry(brush, null, s_triangle);
                            }
                            break;
                    }
                }
            }
        }
    }

    /// <summary>An equilateral triangle of side 1 centred on its centroid.</summary>
    private static StreamGeometry BuildTriangle()
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            var h = Math.Sqrt(3) / 2;
            c.BeginFigure(new Point(0, -h * 2 / 3), true);
            c.LineTo(new Point(0.5, h / 3));
            c.LineTo(new Point(-0.5, h / 3));
            c.EndFigure(true);
        }
        return g;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Clear();
    }
}
