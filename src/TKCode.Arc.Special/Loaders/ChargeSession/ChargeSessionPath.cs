using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// An arc-length table for one cubic Bézier that continues as a straight run along its end tangent: <see cref="PointAt"/>
/// maps a distance along the path to a point, so things moved by distance travel at an even pace however the control
/// points bunch up. <see cref="ChargeSession"/> moves its energy packets along the cable (and on through the pack) with it.
/// Pure, immutable after construction and allocation-free when sampling; any loader that needs even travel along a curve
/// (a stretched line, a stroke drawn by length) can use it.
/// </summary>
internal sealed class ChargeSessionPath
{
    private readonly Point _p0;
    private readonly Point _c1;
    private readonly Point _c2;
    private readonly Point _p3;
    private readonly double[] _lengths;
    private readonly Vector _endDirection;

    /// <summary>Builds the table with <paramref name="segments"/> chords (256 keeps the error far below a pixel).</summary>
    public ChargeSessionPath(Point p0, Point c1, Point c2, Point p3, int segments = 256)
    {
        _p0 = p0;
        _c1 = c1;
        _c2 = c2;
        _p3 = p3;
        _lengths = new double[Math.Max(8, segments) + 1];
        var previous = p0;
        var total = 0.0;
        for (var i = 1; i < _lengths.Length; i++)
        {
            var point = Bezier((double)i / (_lengths.Length - 1));
            total += Distance(previous, point);
            _lengths[i] = total;
            previous = point;
        }
        Length = total;
        var tangent = new Vector(p3.X - c2.X, p3.Y - c2.Y);
        if (tangent.Length < 1e-9)
        {
            tangent = new Vector(p3.X - c1.X, p3.Y - c1.Y);
        }
        if (tangent.Length < 1e-9)
        {
            tangent = new Vector(p3.X - p0.X, p3.Y - p0.Y);
        }
        _endDirection = tangent.Length < 1e-9 ? new Vector(1, 0) : tangent / tangent.Length;
    }

    /// <summary>The length of the curved part.</summary>
    public double Length { get; }

    /// <summary>The curve's start point.</summary>
    public Point Start => _p0;

    /// <summary>The curve's end point, where the straight run begins.</summary>
    public Point End => _p3;

    /// <summary>
    /// The point <paramref name="distance"/> along the path: on the curve up to <see cref="Length"/>, then straight on along
    /// the end tangent; negative distances clamp to the start.
    /// </summary>
    public Point PointAt(double distance)
    {
        if (distance <= 0)
        {
            return _p0;
        }
        if (distance >= Length)
        {
            return _p3 + _endDirection * (distance - Length);
        }
        // Binary search the chord that holds the distance, then interpolate the parameter inside it.
        int lo = 0, hi = _lengths.Length - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (_lengths[mid] < distance)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        var span = _lengths[hi] - _lengths[lo];
        var f = span > 0 ? (distance - _lengths[lo]) / span : 0;
        var t = (lo + f) / (_lengths.Length - 1);
        return Bezier(t);
    }

    /// <summary>The cubic at parameter <paramref name="t"/> (0..1).</summary>
    public Point Bezier(double t)
    {
        var u = 1 - t;
        var a = u * u * u;
        var b = 3 * u * u * t;
        var c = 3 * u * t * t;
        var d = t * t * t;
        return new Point(
            a * _p0.X + b * _c1.X + c * _c2.X + d * _p3.X,
            a * _p0.Y + b * _c1.Y + c * _c2.Y + d * _p3.Y);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
