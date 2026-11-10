using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A closed polygon of a fixed number of slots for <see cref="CubeMascot"/>. With a corner rounding each slot is a line
/// into the corner and a quadratic curve around it (the silhouette); without it the curves collapse onto the corners (the
/// faces). Unused slots repeat the last point, so a 4-sided hull fits in an 8-slot path.
/// <para>
/// A frame moves the points in an array; the path is built from them when it is next drawn, and only if a point moved,
/// so a face that is turned away or a cube that holds still builds nothing. A path object edited in place would raise a
/// property change for every point and be rebuilt for the renderer all the same.
/// </para>
/// </summary>
internal sealed class CubeMascotShape
{
    // Three points per slot: where the line ends, the curve's control point and where the curve ends.
    private readonly Point[] _points;
    private readonly int _slots;
    private Point _start;
    private StreamGeometry? _geometry;
    private bool _set;

    public CubeMascotShape(int slots)
    {
        _slots = slots;
        _points = new Point[slots * 3];
    }

    /// <summary>The path for the points now.</summary>
    public Geometry Geometry
    {
        get
        {
            if (_geometry is { } geometry)
            {
                return geometry;
            }
            geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.SetFillRule(FillRule.EvenOdd);
                context.BeginFigure(_start, true);
                for (var i = 0; i < _points.Length; i += 3)
                {
                    context.LineTo(_points[i]);
                    context.QuadraticBezierTo(_points[i + 1], _points[i + 2]);
                }
                context.EndFigure(true);
            }
            _geometry = geometry;
            return geometry;
        }
    }

    /// <summary>Moves the path onto <paramref name="count"/> points of <paramref name="points"/>, each corner rounded by up to <paramref name="rounding"/> px.</summary>
    public void Set(ReadOnlySpan<Point> points, int count, double rounding)
    {
        var slots = _slots;
        count = Math.Min(count, slots);
        if (count < 3)
        {
            return;
        }
        var moved = !_set;
        Point first = default;
        Point last = default;
        for (var i = 0; i < slots; i++)
        {
            if (i >= count)
            {
                // Unused slots stay where the path already is: zero-length segments.
                moved |= Move(i * 3, last) | Move(i * 3 + 1, last) | Move(i * 3 + 2, last);
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
            moved |= Move(i * 3, into) | Move(i * 3 + 1, corner) | Move(i * 3 + 2, outOf);
            last = outOf;
        }
        if (_start != first)
        {
            _start = first;
            moved = true;
        }
        _set = true;
        if (moved)
        {
            _geometry = null;
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

    private bool Move(int index, Point point)
    {
        if (_points[index] == point)
        {
            return false;
        }
        _points[index] = point;
        return true;
    }
}
