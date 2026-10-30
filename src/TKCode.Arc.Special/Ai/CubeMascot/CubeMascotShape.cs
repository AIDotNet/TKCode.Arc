using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A closed polygon of a fixed number of slots for <see cref="CubeMascot"/>, built once and moved in place each frame.
/// With a corner rounding each slot is a line into the corner and a quadratic curve around it (the silhouette); without
/// it the curves collapse onto the corners (the faces). Unused slots repeat the last point, so a 4-sided hull fits in an
/// 8-slot path. Drawing never allocates.
/// </summary>
internal sealed class CubeMascotShape
{
    private readonly PathFigure _figure = new() { IsClosed = true, IsFilled = true };
    private readonly LineSegment[] _lines;
    private readonly QuadraticBezierSegment[] _curves;

    public CubeMascotShape(int slots)
    {
        _lines = new LineSegment[slots];
        _curves = new QuadraticBezierSegment[slots];
        for (var i = 0; i < slots; i++)
        {
            _lines[i] = new LineSegment();
            _curves[i] = new QuadraticBezierSegment();
            _figure.Segments!.Add(_lines[i]);
            _figure.Segments.Add(_curves[i]);
        }
        Geometry.Figures!.Add(_figure);
    }

    /// <summary>The path.</summary>
    public PathGeometry Geometry { get; } = new();

    /// <summary>Moves the path onto <paramref name="count"/> points of <paramref name="points"/>, each corner rounded by up to <paramref name="rounding"/> px.</summary>
    public void Set(ReadOnlySpan<Point> points, int count, double rounding)
    {
        var slots = _lines.Length;
        count = Math.Min(count, slots);
        if (count < 3)
        {
            return;
        }
        Point first = default;
        Point last = default;
        for (var i = 0; i < slots; i++)
        {
            if (i >= count)
            {
                // Unused slots stay where the path already is: zero-length segments.
                Move(_lines[i], last);
                Move(_curves[i], last, last);
                continue;
            }
            var corner = points[i];
            Point into;
            Point outOf;
            if (rounding <= 0)
            {
                into = corner;
                outOf = corner;
            }
            else
            {
                into = Toward(corner, points[(i + count - 1) % count], rounding);
                outOf = Toward(corner, points[(i + 1) % count], rounding);
            }
            if (i == 0)
            {
                first = into;
            }
            Move(_lines[i], into);
            Move(_curves[i], corner, outOf);
            last = outOf;
        }
        if (_figure.StartPoint != first)
        {
            _figure.StartPoint = first;
        }
    }

    /// <summary>Moves the path onto the four corners of a face, given as indices into <paramref name="corners"/>.</summary>
    public void SetQuad(Point[] corners, ReadOnlySpan<int> indices)
    {
        Span<Point> quad = stackalloc Point[4];
        for (var i = 0; i < 4; i++)
        {
            quad[i] = corners[indices[i]];
        }
        Set(quad, 4, 0);
    }

    // The point on the edge from corner toward other, at most rounding px away and never past the edge's middle.
    private static Point Toward(Point corner, Point other, double rounding)
    {
        var dx = other.X - corner.X;
        var dy = other.Y - corner.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6)
        {
            return corner;
        }
        var d = Math.Min(rounding, length * 0.5) / length;
        return new Point(corner.X + dx * d, corner.Y + dy * d);
    }

    private static void Move(LineSegment line, Point point)
    {
        if (line.Point != point)
        {
            line.Point = point;
        }
    }

    private static void Move(QuadraticBezierSegment curve, Point control, Point end)
    {
        if (curve.Point1 != control)
        {
            curve.Point1 = control;
        }
        if (curve.Point2 != end)
        {
            curve.Point2 = end;
        }
    }
}
