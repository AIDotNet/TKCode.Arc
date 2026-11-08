using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The paper shapes of a <see cref="TicketTear"/>, for one size. The perforation runs down between the body and the stub
/// from one notch to the other, with evenly spread holes. Its torn edge goes through the middle of every hole and wanders
/// between them, on a coarse and a fine scale that are fixed for the ticket, so both pieces share one line and a tear
/// looks the same however far it has run. Every edge goes strictly downward, which lets a shape take the torn edge above
/// the tear's front and the intact one below it.
/// </summary>
internal sealed class TicketTearGeometry
{
    /// <summary>How far the intact body runs under the stub, so the join between the pieces never shows a hairline.</summary>
    private const double Overlap = 0.75;

    private const double SampleStep = 2.5;
    private const int ArcSegments = 8;
    private const double CoarseWander = 1.5;
    private const double FineWander = 0.55;

    private readonly List<Point> _intactBody = [];
    private readonly List<Point> _intactStub = [];
    private readonly List<Point> _tornBody = [];
    private readonly List<Point> _tornStub = [];
    private readonly List<Point> _fiberBody = [];
    private readonly List<Point> _fiberStub = [];
    private readonly List<Point> _stubRight = [];

    public TicketTearGeometry(double bodyWidth, double stubWidth, double height, double corner, double notch, double holeRadius, double holePitch)
    {
        BodyWidth = Math.Max(1, bodyWidth);
        StubWidth = Math.Max(1, stubWidth);
        Height = Math.Max(1, height);
        Notch = Math.Min(Math.Max(0, notch), Math.Min(Height / 4, Math.Min(BodyWidth, StubWidth) / 2));
        Corner = Math.Clamp(corner, 0, Math.Min(Height / 2, Math.Max(0, Math.Min(BodyWidth, StubWidth) - Notch)));

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

    public double HoleRadius { get; }

    /// <summary>The height of each hole's centre, from the top.</summary>
    public IReadOnlyList<double> Holes { get; }

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
        _intactBody.Add(new Point(Overlap, from));
        _intactBody.Add(new Point(Overlap, to));
        _intactStub.Add(new Point(0, from));
        _intactStub.Add(new Point(0, to));

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
            var y = from + length * u;
            var point = new Point(x, y);
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
        if (chance < -0.1)
        {
            return;
        }
        var length = 1.4 + (chance + 0.1) * 2.4;
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
            _intactBody.Add(body);
            _tornBody.Add(body);
            _fiberBody.Add(body);
            _intactStub.Add(stub);
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

    /// <summary>The body: rounded on the left, with the perforation on the right.</summary>
    public void BuildBody(List<Point> into, double front, bool fibers)
    {
        into.Clear();
        var w = BodyWidth;
        AppendArc(into, Corner, Corner, Corner, Math.PI, Math.PI * 1.5);
        AppendArc(into, w, 0, Notch, Math.PI, Math.PI / 2);
        var offset = new Vector(w, 0);
        AppendBelow(into, fibers ? _fiberBody : _tornBody, double.NegativeInfinity, front, offset);
        AppendBelow(into, _intactBody, front, double.PositiveInfinity, offset);
        AppendArc(into, w, Height, Notch, Math.PI * 1.5, Math.PI);
        AppendArc(into, Corner, Height - Corner, Corner, Math.PI / 2, Math.PI);
    }

    /// <summary>The whole stub: torn above <paramref name="front"/> and still perforated below it.</summary>
    public void BuildStub(List<Point> into, double front, bool fibers)
    {
        into.Clear();
        AppendArc(into, 0, 0, Notch, 0, 0);
        into.AddRange(_stubRight);
        AppendArc(into, 0, Height, Notch, 0, -Math.PI / 2);
        AppendAboveReversed(into, _intactStub, front, double.PositiveInfinity);
        AppendAboveReversed(into, fibers ? _fiberStub : _tornStub, double.NegativeInfinity, front);
        AppendArc(into, 0, 0, Notch, Math.PI / 2, 0);
    }

    /// <summary>The part of the stub still fixed to the ticket: everything under a straight hinge at <paramref name="hinge"/>.</summary>
    public void BuildAttachedStub(List<Point> into, double hinge)
    {
        into.Clear();
        var firstEdge = _intactStub[^1];
        foreach (var point in _intactStub)
        {
            if (point.Y >= hinge)
            {
                firstEdge = point;
                break;
            }
        }
        into.Add(new Point(firstEdge.X, hinge));
        into.Add(new Point(StubRightAt(hinge), hinge));
        foreach (var point in _stubRight)
        {
            if (point.Y > hinge)
            {
                into.Add(point);
            }
        }
        AppendArc(into, 0, Height, Notch, 0, -Math.PI / 2);
        AppendAboveReversed(into, _intactStub, hinge, double.PositiveInfinity);
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
        AppendArc(into, 0, 0, Notch, 0, Math.PI / 2);
        AppendBelow(into, fibers ? _fiberStub : _tornStub, double.NegativeInfinity, hinge, default);
        into.Add(new Point(into[^1].X, hinge));
    }

    private double StubRightAt(double y)
    {
        var fromTop = y < Corner ? Corner - y : y > Height - Corner ? y - (Height - Corner) : 0;
        return StubWidth - Corner + Math.Sqrt(Math.Max(0, Corner * Corner - fromTop * fromTop));
    }

    private static void AppendBelow(List<Point> into, List<Point> edge, double from, double to, Vector offset)
    {
        foreach (var point in edge)
        {
            if (point.Y >= from && point.Y < to)
            {
                into.Add(point + offset);
            }
        }
    }

    private static void AppendAboveReversed(List<Point> into, List<Point> edge, double from, double to)
    {
        for (var i = edge.Count - 1; i >= 0; i--)
        {
            var point = edge[i];
            if (point.Y >= from && point.Y < to)
            {
                into.Add(point);
            }
        }
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
        if (points.Count >= 3)
        {
            path.BeginFigure(points[0], true);
            for (var i = 1; i < points.Count; i++)
            {
                path.LineTo(points[i]);
            }
            path.EndFigure(true);
        }
        return geometry;
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
