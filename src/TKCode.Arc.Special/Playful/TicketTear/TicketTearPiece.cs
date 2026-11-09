using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// One piece of paper: its child is clipped to the shape, and it fills that shape and strokes its edge. Holes and notches
/// are part of the shape, so what is behind the ticket shows through them. The stroke is twice the edge width because the
/// clip takes its outer half away.
/// </summary>
internal sealed class TicketTearPiece : Decorator
{
    private Geometry? _fill;
    private Geometry? _outline;

    internal TicketTear? Owner { get; set; }

    /// <summary>Whether this is the stub's paper rather than the body's.</summary>
    internal bool IsStub { get; set; }

    /// <summary>Sets the paper's shape. <paramref name="fill"/> includes the fibers; <paramref name="outline"/> does not.</summary>
    public void SetShape(Geometry fill, Geometry outline)
    {
        _fill = fill;
        _outline = outline;
        Clip = fill;
        // A changed clip geometry reaches the compositor only through an invalidation.
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is { } owner && _fill is not null && _outline is not null)
        {
            context.DrawGeometry(IsStub ? owner.StubBackground : owner.Background, null, _fill);
            if (owner.EdgePen is { } pen)
            {
                context.DrawGeometry(null, pen, _outline);
            }
        }
    }
}

/// <summary>
/// The torn part of the stub, lifting from a crease at the tear's front. Most of the turn happens at the crease and the
/// rest bends gently along the flap: each height has a point on the torn edge and a direction across the stub, the
/// direction turns a little more the further it is from the crease, and the edge follows it. The outline goes through
/// that bend point by point; the picture of the stub goes through a mesh of triangles, each an exact affine map of its
/// corners, so the print stays continuous.
/// Once free it is the whole stub, and it falls. It draws only: assistive technology reads the real stub.
/// </summary>
internal sealed class TicketTearFlap : Control
{
    /// <summary>Paper shadow under the flap.</summary>
    public static readonly StyledProperty<IBrush?> ShadowBrushProperty =
        AvaloniaProperty.Register<TicketTearFlap, IBrush?>(nameof(ShadowBrush));

    private const int Bands = 10;
    private const double MaxShade = 0.07;

    /// <summary>The share of the flap's turn taken at the crease.</summary>
    private const double CreaseShare = 0.72;

    private const int ShadeLevels = 16;
    private const double ShadowX = 1;
    private const double ShadowY = 2;

    private static readonly IBrush[] s_shades = CreateShades();

    // One band is the unit square split in two. The triangles reach slightly over their neighbours so no seam shows.
    private static readonly Geometry s_printA = Triangle(new Point(-0.02, -0.02), new Point(1.04, -0.02), new Point(-0.02, 1.04));
    private static readonly Geometry s_printB = Triangle(new Point(1.02, -0.04), new Point(1.02, 1.02), new Point(-0.04, 1.02));

    private readonly Point[] _edge = new Point[Bands + 1];
    private readonly Vector[] _across = new Vector[Bands + 1];
    private readonly List<Point> _points = [];
    private double _bandHeight;
    private double _hinge;
    private double _angle;
    private double _inset;

    static TicketTearFlap()
    {
        AffectsRender<TicketTearFlap>(ShadowBrushProperty);
    }

    public IBrush? ShadowBrush
    {
        get => GetValue(ShadowBrushProperty);
        set => SetValue(ShadowBrushProperty, value);
    }

    internal TicketTear? Owner { get; set; }

    private static IBrush[] CreateShades()
    {
        var shades = new IBrush[ShadeLevels + 1];
        for (var i = 0; i <= ShadeLevels; i++)
        {
            shades[i] = new ImmutableSolidColorBrush(Colors.Black, MaxShade * i / ShadeLevels);
        }
        return shades;
    }

    private static StreamGeometry Triangle(Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.BeginFigure(a, true);
        path.LineTo(b);
        path.LineTo(c);
        path.EndFigure(true);
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is not { Geometry: { } geometry } owner || !owner.ShowsFlap)
        {
            return;
        }
        var motion = owner.Motion;
        var hinge = motion.Front >= geometry.Height ? geometry.Height : geometry.SnapHinge(motion.Front);
        if (hinge <= 0)
        {
            return;
        }
        Bend(geometry, hinge, motion.Angle);

        // The fall: the stub spins about its centre and drops.
        var centre = new Vector(geometry.StubWidth / 2, geometry.Height / 2);
        var fall = Matrix.CreateTranslation(-centre)
                   * Matrix.CreateRotation(motion.FallRotation)
                   * Matrix.CreateTranslation(centre + motion.FallOffset);
        using var pose = context.PushTransform(fall);
        using var fade = context.PushOpacity(motion.FallOpacity);

        geometry.BuildFlap(_points, hinge, true);
        var shape = MapToGeometry();

        if (ShadowBrush is { } shadow && motion.Angle > 0.001)
        {
            using (context.PushTransform(Matrix.CreateTranslation(ShadowX, ShadowY)))
            {
                context.DrawGeometry(shadow, null, shape);
            }
        }
        using (context.PushGeometryClip(shape))
        {
            context.DrawGeometry(owner.StubBackground, null, shape);
            if (owner.StubSnapshot is { } snapshot)
            {
                DrawPrint(context, snapshot, geometry);
            }
            // A flap held at an angle catches less light. One shade for all of it: per band it would show their seams.
            var level = (int)Math.Round(ShadeLevels * motion.Angle / TicketTearMotion.MaxAngle);
            if (level > 0)
            {
                context.DrawGeometry(s_shades[Math.Min(level, ShadeLevels)], null, shape);
            }
            if (owner.EdgePen is { } pen)
            {
                // Only an edge needs the outline without the fibers.
                geometry.BuildFlap(_points, hinge, false);
                context.DrawGeometry(null, pen, MapToGeometry());
            }
        }
    }

    /// <summary>Works out the bend: where the torn edge goes, and which way the paper runs from it, at each band's border.</summary>
    private void Bend(TicketTearGeometry geometry, double hinge, double angle)
    {
        _hinge = hinge;
        _angle = angle;
        _inset = geometry.Inset;
        _bandHeight = hinge / Bands;
        var edge = new Point(_inset, hinge);
        for (var i = 0; i <= Bands; i++)
        {
            var (sin, cos) = Math.SinCos(TurnAt(i));
            _edge[i] = edge;
            _across[i] = new Vector(cos, sin);
            // Up the paper is at right angles to across it, so a turning paper moves its edge away from the body.
            var (midSin, midCos) = Math.SinCos(TurnAt(i + 0.5));
            edge += new Vector(_bandHeight * midSin, -_bandHeight * midCos);
        }
    }

    /// <summary>How far the paper has turned at a height, counted in bands from the crease.</summary>
    private double TurnAt(double band) => _angle * (CreaseShare + (1 - CreaseShare) * band / Bands);

    /// <summary>Where a point of the flat stub goes.</summary>
    private Point Map(Point point)
    {
        var at = Math.Clamp((_hinge - point.Y) / _bandHeight, 0, Bands);
        var band = Math.Min((int)at, Bands - 1);
        var share = at - band;
        var edge = _edge[band] + (_edge[band + 1] - _edge[band]) * share;
        var (sin, cos) = Math.SinCos(TurnAt(at));
        return edge + new Vector(cos, sin) * (point.X - _inset);
    }

    private StreamGeometry MapToGeometry()
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        if (_points.Count >= 3)
        {
            path.BeginFigure(Map(_points[0]), true);
            for (var i = 1; i < _points.Count; i++)
            {
                path.LineTo(Map(_points[i]));
            }
            path.EndFigure(true);
        }
        return geometry;
    }

    private void DrawPrint(DrawingContext context, IImage image, TicketTearGeometry geometry)
    {
        var width = geometry.StubWidth;
        var source = new Rect(image.Size);
        if (image is Avalonia.Media.Imaging.Bitmap bitmap)
        {
            // A bitmap's source rectangle is in pixels.
            source = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        for (var i = 0; i < Bands; i++)
        {
            // Unit square: x runs across the stub, y from the band's top border down to its bottom one.
            var topLeft = _edge[i + 1] - _across[i + 1] * _inset;
            var topRight = topLeft + _across[i + 1] * width;
            var bottomLeft = _edge[i] - _across[i] * _inset;
            var bottomRight = bottomLeft + _across[i] * width;
            var upper = FromUnit(topRight - topLeft, bottomLeft - topLeft, topLeft);
            var lowerAcross = bottomRight - bottomLeft;
            var lower = FromUnit(lowerAcross, bottomRight - topRight, topRight - lowerAcross);

            // The whole picture, placed so that this band's slice of it lands on the unit square.
            var top = _hinge - _bandHeight * (i + 1);
            var target = new Rect(0, -top / _bandHeight, 1, geometry.Height / _bandHeight);
            DrawTriangle(context, image, source, target, upper, s_printA);
            DrawTriangle(context, image, source, target, lower, s_printB);
        }
    }

    private static Matrix FromUnit(Vector x, Vector y, Point origin) => new(x.X, x.Y, y.X, y.Y, origin.X, origin.Y);

    private static void DrawTriangle(DrawingContext context, IImage image, Rect source, Rect target, Matrix map, Geometry triangle)
    {
        using (context.PushTransform(map))
        using (context.PushGeometryClip(triangle))
        {
            context.DrawImage(image, source, target);
        }
    }
}
