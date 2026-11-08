using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The paper shapes of a <see cref="TicketTear"/>, for one size. The stub lies over the end of the body, and the
/// perforation runs down a few pixels inside the stub's left edge, from one notch to the other, with evenly spread holes.
/// The body's paper reaches to that line, so it is what shows through the left half of a hole and what is left when the
/// stub has gone. The torn edge goes through the middle of every hole and wanders between them, on a coarse and a fine
/// scale that are fixed for the ticket. Every edge goes strictly downward, which lets a shape take part of it.
/// </summary>
internal sealed class TicketTearGeometry
{
    private const double SampleStep = 2;
    private const int ArcSegments = 8;
    private const int HoleSegments = 12;
    private const double CoarseWander = 2;
    private const double FineWander = 0.9;

    private readonly List<Point> _tornBody = [];
    private readonly List<Point> _tornStub = [];
    private readonly List<Point> _fiberBody = [];
    private readonly List<Point> _fiberStub = [];
    private readonly List<Point> _stubRight = [];
    private readonly double _notchReach;
    private readonly double _notchAngle;

    public TicketTearGeometry(double bodyWidth, double stubWidth, double height, double corner, double notch, double inset, double holeRadius, double holePitch)
    {
        BodyWidth = Math.Max(1, bodyWidth);
        StubWidth = Math.Max(1, stubWidth);
        Height = Math.Max(1, height);
        Notch = Math.Min(Math.Max(0.5, notch), Math.Min(Height / 4, StubWidth / 2));
        // The notch has to reach past the stub's left edge, so the two arcs always meet that edge.
        Inset = Math.Clamp(inset, 0, Math.Max(0, Notch - 0.25));
        Corner = Math.Clamp(corner, 0, Math.Min(Height / 2, Math.Max(0, StubWidth - Inset - Notch)));

        // Where a notch's arc meets the stub's left edge.
        _notchAngle = Math.Acos(Inset / Notch);
        _notchReach = Notch * Math.Sin(_notchAngle);

        var span = Height - 2 * Notch;
        var count = Math.Max(0, (int)Math.Floor(span / Math.Max(1, holePitch)) - 1);
        var spacing = span / (count + 1);
        HoleRadius = Math.Min(holeRadius, spacing * 0.35);
        var holes = new double[count];
        for (var i = 0; i < count; i++)
        {
            holes[i] = Notch + spacing * (i + 1);
        }
        Holes = holes;

        BuildEdges();
        BuildStubRight();
    }

    public double BodyWidth { get; }

    public double StubWidth { get; }

    public double Height { get; }

    public double Corner { get; }

    public double Notch { get; }

    /// <summary>How far inside the stub the perforation runs.</summary>
    public double Inset { get; }

    public double HoleRadius { get; }

    /// <summary>The height of each hole's centre, from the top.</summary>
    public IReadOnlyList<double> Holes { get; }

    /// <summary>The height the flap bends at for a tear that has reached <paramref name="front"/>: never through a hole, so what stays on the ticket has only whole holes.</summary>
    public double SnapHinge(double front)
    {
        foreach (var hole in Holes)
        {
            if (front > hole - HoleRadius && front < hole + HoleRadius)
            {
                return hole - HoleRadius;
            }
        }
        return front;
    }

    private void BuildEdges()
    {
        var gapStart = Notch;
        for (var i = 0; i <= Holes.Count; i++)
        {
            var gapEnd = i < Holes.Count ? Holes[i] - HoleRadius : Height - Notch;
            AppendGap(i, gapStart, gapEnd);
            if (i < Holes.Count)
            {
                AppendHole(Holes[i]);
                gapStart = Holes[i] + HoleRadius;
            }
        }
    }

    private void AppendGap(int gap, double from, double to)
    {
        var length = to - from;
        var samples = Math.Max(1, (int)Math.Ceiling(length / SampleStep));
        // Short gaps wander less, so the line can never reach into the next hole.
        var scale = Math.Min(1, length / 8);
        var coarse = Hash(gap * 7 + 1) * CoarseWander * scale;
        var bends = Hash(gap * 11 + 3) > 0 ? 1 : 2;
        for (var k = 0; k <= samples; k++)
        {
            var u = (double)k / samples;
            var window = Math.Sin(Math.PI * u);
            var x = window * (coarse * Math.Sin(Math.PI * u * bends) + Hash(gap * 97 + k) * FineWander * scale);
            var point = new Point(x, from + length * u);
            _tornBody.Add(point);
            _tornStub.Add(point);
            _fiberBody.Add(point);
            _fiberStub.Add(point);
            // Loose fibers stand just past the edge, on both sides. Each is a sliver of paper in the piece's own shape.
            if (k < samples && length / samples > 1)
            {
                AddFiber(_fiberBody, point, 1, gap * 131 + k);
                AddFiber(_fiberStub, point, -1, gap * 173 + k);
            }
        }
    }

    private static void AddFiber(List<Point> edge, Point at, double outward, int seed)
    {
        var chance = Hash(seed);
        if (chance < -0.35)
        {
            return;
        }
        var length = 1.2 + (chance + 0.35) * 2.6;
        var drift = Hash(seed + 5) * 0.25;
        edge.Add(new Point(at.X + outward * length, at.Y + 0.3 + drift));
        edge.Add(new Point(at.X, at.Y + 0.75));
    }

    private void AppendHole(double centre)
    {
        for (var i = 0; i <= ArcSegments; i++)
        {
            var angle = Math.PI * i / ArcSegments - Math.PI / 2;
            var dx = HoleRadius * Math.Cos(angle);
            var y = centre + HoleRadius * Math.Sin(angle);
            var body = new Point(-dx, y);
            var stub = new Point(dx, y);
            _tornBody.Add(body);
            _fiberBody.Add(body);
            _tornStub.Add(stub);
            _fiberStub.Add(stub);
        }
    }

    private void BuildStubRight()
    {
        var x = StubWidth - Corner;
        AppendArc(_stubRight, x, Corner, Corner, -Math.PI / 2, 0);
        // The straight part is sampled too: a flap bends it.
        var straight = Height - 2 * Corner;
        var samples = (int)Math.Ceiling(straight / (SampleStep * 3));
        for (var i = 1; i < samples; i++)
        {
            _stubRight.Add(new Point(StubWidth, Corner + straight * i / samples));
        }
        AppendArc(_stubRight, x, Height - Corner, Corner, 0, Math.PI / 2);
    }

    /// <summary>The body: its paper reaches the torn line, which the stub covers until it comes off.</summary>
    public void BuildBody(List<Point> into, bool fibers)
    {
        into.Clear();
        var line = BodyWidth + Inset;
        AppendArc(into, Corner, Corner, Corner, Math.PI, Math.PI * 1.5);
        AppendArc(into, line, 0, Notch, Math.PI, Math.PI / 2);
        var offset = new Vector(line, 0);
        foreach (var point in fibers ? _fiberBody : _tornBody)
        {
            into.Add(point + offset);
        }
        AppendArc(into, line, Height, Notch, Math.PI * 1.5, Math.PI);
        AppendArc(into, Corner, Height - Corner, Corner, Math.PI / 2, Math.PI);
    }

    /// <summary>The whole stub: torn along the line above <paramref name="front"/> and still whole below it.</summary>
    public void BuildStub(List<Point> into, double front, bool fibers)
    {
        into.Clear();
        into.AddRange(_stubRight);
        if (front >= Height)
        {
            AppendArc(into, Inset, Height, Notch, 0, -Math.PI / 2);
            AppendTornReversed(into, fibers, double.PositiveInfinity);
        }
        else
        {
            AppendArc(into, Inset, Height, Notch, 0, -Math.PI + _notchAngle);
            if (front > Notch)
            {
                into.Add(new Point(0, front));
                AppendTornReversed(into, fibers, front);
            }
            else
            {
                AppendArc(into, Inset, 0, Notch, Math.PI - _notchAngle, 0);
                return;
            }
        }
        AppendArc(into, Inset, 0, Notch, Math.PI / 2, 0);
    }

    /// <summary>The part of the stub still fixed to the ticket: everything under a straight hinge at <paramref name="hinge"/>.</summary>
    public void BuildAttachedStub(List<Point> into, double hinge)
    {
        into.Clear();
        into.Add(new Point(0, hinge));
        into.Add(new Point(StubRightAt(hinge), hinge));
        foreach (var point in _stubRight)
        {
            if (point.Y > hinge)
            {
                into.Add(point);
            }
        }
        AppendArc(into, Inset, Height, Notch, 0, -Math.PI + _notchAngle);
    }

    /// <summary>The torn part of the stub: everything above a straight hinge at <paramref name="hinge"/>, or the whole stub once it is free.</summary>
    public void BuildFlap(List<Point> into, double hinge, bool fibers)
    {
        if (hinge >= Height)
        {
            BuildStub(into, Height, fibers);
            return;
        }
        into.Clear();
        into.Add(new Point(StubRightAt(hinge), hinge));
        for (var i = _stubRight.Count - 1; i >= 0; i--)
        {
            if (_stubRight[i].Y < hinge)
            {
                into.Add(_stubRight[i]);
            }
        }
        AppendArc(into, Inset, 0, Notch, 0, Math.PI / 2);
        var offset = new Vector(Inset, 0);
        foreach (var point in fibers ? _fiberStub : _tornStub)
        {
            if (point.Y < hinge)
            {
                into.Add(point + offset);
            }
        }
        into.Add(new Point(into[^1].X, hinge));
    }

    private void AppendTornReversed(List<Point> into, bool fibers, double below)
    {
        var edge = fibers ? _fiberStub : _tornStub;
        var offset = new Vector(Inset, 0);
        for (var i = edge.Count - 1; i >= 0; i--)
        {
            if (edge[i].Y < below)
            {
                into.Add(edge[i] + offset);
            }
        }
    }

    private double StubRightAt(double y)
    {
        var fromTop = y < Corner ? Corner - y : y > Height - Corner ? y - (Height - Corner) : 0;
        return StubWidth - Corner + Math.Sqrt(Math.Max(0, Corner * Corner - fromTop * fromTop));
    }

    private static void AppendArc(List<Point> into, double cx, double cy, double radius, double from, double to)
    {
        for (var i = 0; i <= ArcSegments; i++)
        {
            var angle = from + (to - from) * i / ArcSegments;
            into.Add(new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
        }
    }

    /// <summary>A fixed pseudo-random value from -1 to 1, so a ticket tears along the same line every time.</summary>
    private static double Hash(int seed)
    {
        var value = Math.Sin(seed * 12.9898 + 4.1414) * 43758.5453;
        return 2 * (value - Math.Floor(value)) - 1;
    }

    /// <summary>A closed figure through the points.</summary>
    public static StreamGeometry ToGeometry(List<Point> points)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        AppendFigure(path, points);
        return geometry;
    }

    /// <summary>A closed figure through the points, with the whole holes below <paramref name="below"/> cut out of it.</summary>
    public StreamGeometry ToGeometryWithHoles(List<Point> points, double below)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        path.SetFillRule(FillRule.EvenOdd);
        AppendFigure(path, points);
        foreach (var hole in Holes)
        {
            if (hole - HoleRadius < below)
            {
                continue;
            }
            path.BeginFigure(new Point(Inset + HoleRadius, hole), true);
            for (var i = 1; i < HoleSegments; i++)
            {
                var (sin, cos) = Math.SinCos(2 * Math.PI * i / HoleSegments);
                path.LineTo(new Point(Inset + HoleRadius * cos, hole + HoleRadius * sin));
            }
            path.EndFigure(true);
        }
        return geometry;
    }

    private static void AppendFigure(StreamGeometryContext path, List<Point> points)
    {
        if (points.Count >= 3)
        {
            path.BeginFigure(points[0], true);
            for (var i = 1; i < points.Count; i++)
            {
                path.LineTo(points[i]);
            }
            path.EndFigure(true);
        }
    }

    /// <summary>The area inside a closed polygon.</summary>
    public static double Area(IReadOnlyList<Point> points)
    {
        var sum = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }
}
