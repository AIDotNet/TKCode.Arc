using System.Buffers;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a series' points are joined. Each kind is the one an Analytics block was measured to use.</summary>
internal enum AnalyticsCurve
{
    /// <summary>Straight segments (KPI drilldown, Metric explorer polylines, Horizon chart, Usage forecast).</summary>
    Linear,

    /// <summary>
    /// Monotone cubic exactly as d3 <c>curveMonotoneX</c> (Fritsch–Carlson limiter, end tangents (3·secant − t) / 2):
    /// Revenue explorer, Uptime latency. One cubic per segment, control points at 1/3 and 2/3 of the segment.
    /// </summary>
    Monotone,

    /// <summary>
    /// Monotone cubic with harmonic-mean interior tangents <c>2 / (1/δ₀ + 1/δ₁)</c> (0 when the secants differ in sign or
    /// one is 0) and end tangents equal to the adjacent secant: measured in Metrics dashboard (differs from d3 at peaks).
    /// </summary>
    MonotoneHarmonic,

    /// <summary>Uniform Catmull-Rom, tension 0, ends duplicated (Connected scatter; see <see cref="AnalyticsCatmullRom"/>).</summary>
    CatmullRom
}

/// <summary>
/// Curve math shared by the Analytics plots: monotone tangents (d3 and harmonic), Hermite evaluation, Catmull-Rom
/// control points, linear resampling, and writing a curve into a <see cref="StreamGeometryContext"/>. Points must have
/// increasing x for the monotone kinds. Nothing here allocates per call except buffers above 256 points (pooled).
/// </summary>
internal static class AnalyticsCurves
{
    /// <summary>
    /// Writes d3 <c>curveMonotoneX</c> tangents (dy/dx at each point) into <paramref name="tangents"/>. Two points get the
    /// secant at both ends (d3 draws a straight line); one point gets 0.
    /// </summary>
    public static void MonotoneTangents(ReadOnlySpan<Point> points, Span<double> tangents)
    {
        var n = points.Length;
        if (n == 0)
        {
            return;
        }
        if (n == 1)
        {
            tangents[0] = 0;
            return;
        }
        if (n == 2)
        {
            var s = Secant(points[0], points[1]);
            tangents[0] = s;
            tangents[1] = s;
            return;
        }
        for (var i = 1; i < n - 1; i++)
        {
            tangents[i] = Slope3(points[i - 1], points[i], points[i + 1]);
        }
        tangents[0] = Slope2(points[0], points[1], tangents[1]);
        tangents[n - 1] = Slope2(points[n - 2], points[n - 1], tangents[n - 2]);
    }

    /// <summary>Metrics dashboard's tangents: harmonic mean of the neighbouring secants inside, the secant at the ends.</summary>
    public static void HarmonicTangents(ReadOnlySpan<Point> points, Span<double> tangents)
    {
        var n = points.Length;
        if (n == 0)
        {
            return;
        }
        if (n == 1)
        {
            tangents[0] = 0;
            return;
        }
        for (var i = 1; i < n - 1; i++)
        {
            var d0 = Secant(points[i - 1], points[i]);
            var d1 = Secant(points[i], points[i + 1]);
            tangents[i] = d0 * d1 > 0 ? 2 / (1 / d0 + 1 / d1) : 0;
        }
        tangents[0] = Secant(points[0], points[1]);
        tangents[n - 1] = Secant(points[n - 2], points[n - 1]);
    }

    /// <summary>
    /// Writes the curve through <paramref name="points"/>: starts a figure at the first point when
    /// <paramref name="beginFigure"/> (otherwise continues with a line to it), then one segment per pair of points.
    /// The figure is left open (callers close areas themselves).
    /// </summary>
    public static void Write(StreamGeometryContext context, ReadOnlySpan<Point> points, AnalyticsCurve curve, bool beginFigure = true)
    {
        var n = points.Length;
        if (n == 0)
        {
            return;
        }
        if (beginFigure)
        {
            context.BeginFigure(points[0], true);
        }
        else
        {
            context.LineTo(points[0]);
        }
        if (n == 1)
        {
            return;
        }
        switch (curve)
        {
            case AnalyticsCurve.Linear:
                for (var i = 1; i < n; i++)
                {
                    context.LineTo(points[i]);
                }
                return;
            case AnalyticsCurve.CatmullRom:
                for (var i = 0; i < n - 1; i++)
                {
                    var (c1, c2) = CatmullRomControls(points, i);
                    context.CubicBezierTo(c1, c2, points[i + 1]);
                }
                return;
        }
        if (n == 2 && curve == AnalyticsCurve.Monotone)
        {
            context.LineTo(points[1]);
            return;
        }
        double[]? rented = null;
        var tangents = n <= 256 ? stackalloc double[n] : (rented = ArrayPool<double>.Shared.Rent(n)).AsSpan(0, n);
        try
        {
            if (curve == AnalyticsCurve.MonotoneHarmonic)
            {
                HarmonicTangents(points, tangents);
            }
            else
            {
                MonotoneTangents(points, tangents);
            }
            for (var i = 0; i < n - 1; i++)
            {
                var (c1, c2) = HermiteControls(points[i], points[i + 1], tangents[i], tangents[i + 1]);
                context.CubicBezierTo(c1, c2, points[i + 1]);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Writes the curve backwards (last point first): the lower edge of a band or a stacked area.</summary>
    public static void WriteReversed(StreamGeometryContext context, ReadOnlySpan<Point> points, AnalyticsCurve curve, bool beginFigure = false)
    {
        if (curve is AnalyticsCurve.Monotone or AnalyticsCurve.MonotoneHarmonic)
        {
            // Monotone tangents need increasing x: compute them forwards, then walk the segments backwards.
            WriteMonotoneBackwards(context, points, curve, beginFigure);
            return;
        }
        var n = points.Length;
        Point[]? rented = null;
        var reversed = n <= 256 ? stackalloc Point[n] : (rented = ArrayPool<Point>.Shared.Rent(n)).AsSpan(0, n);
        try
        {
            for (var i = 0; i < n; i++)
            {
                reversed[i] = new Point(points[n - 1 - i].X, points[n - 1 - i].Y);
            }
            Write(context, reversed, curve, beginFigure);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<Point>.Shared.Return(rented);
            }
        }
    }

    private static void WriteMonotoneBackwards(StreamGeometryContext context, ReadOnlySpan<Point> points, AnalyticsCurve curve, bool beginFigure)
    {
        var n = points.Length;
        if (n == 0)
        {
            return;
        }
        if (beginFigure)
        {
            context.BeginFigure(points[n - 1], true);
        }
        else
        {
            context.LineTo(points[n - 1]);
        }
        if (n == 2 && curve == AnalyticsCurve.Monotone)
        {
            context.LineTo(points[0]);
            return;
        }
        double[]? rented = null;
        var tangents = n <= 256 ? stackalloc double[n] : (rented = ArrayPool<double>.Shared.Rent(n)).AsSpan(0, n);
        try
        {
            if (curve == AnalyticsCurve.MonotoneHarmonic)
            {
                HarmonicTangents(points, tangents);
            }
            else
            {
                MonotoneTangents(points, tangents);
            }
            for (var i = n - 1; i > 0; i--)
            {
                var (c1, c2) = HermiteControls(points[i - 1], points[i], tangents[i - 1], tangents[i]);
                context.CubicBezierTo(c2, c1, points[i - 1]);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Bezier control points of a Hermite segment: <c>(x0 + dx/3, y0 + t0·dx/3)</c> and <c>(x1 − dx/3, y1 − t1·dx/3)</c>.</summary>
    public static (Point C1, Point C2) HermiteControls(Point p0, Point p1, double t0, double t1)
    {
        var dx = (p1.X - p0.X) / 3;
        return (new Point(p0.X + dx, p0.Y + dx * t0), new Point(p1.X - dx, p1.Y - dx * t1));
    }

    /// <summary>
    /// Uniform Catmull-Rom (tension 0) controls of segment <paramref name="i"/> (from point i to i + 1):
    /// <c>c1 = p1 + (p2 − p0) / 6</c>, <c>c2 = p2 − (p3 − p1) / 6</c>, ends duplicated (p0 = p1 on the first segment,
    /// p3 = p2 on the last). Measured in Connected scatter to 0.005px.
    /// </summary>
    public static (Point C1, Point C2) CatmullRomControls(ReadOnlySpan<Point> points, int i)
    {
        var p1 = points[i];
        var p2 = points[i + 1];
        var p0 = i > 0 ? points[i - 1] : p1;
        var p3 = i + 2 < points.Length ? points[i + 2] : p2;
        return (new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6),
                new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6));
    }

    /// <summary>
    /// The monotone curve's y at <paramref name="x"/> (clamped to the points' x range); <paramref name="tangents"/> from
    /// <see cref="MonotoneTangents"/> or <see cref="HarmonicTangents"/>.
    /// </summary>
    public static double ValueAt(ReadOnlySpan<Point> points, ReadOnlySpan<double> tangents, double x)
    {
        var n = points.Length;
        if (n == 0)
        {
            return double.NaN;
        }
        if (n == 1 || x <= points[0].X)
        {
            return points[0].Y;
        }
        if (x >= points[n - 1].X)
        {
            return points[n - 1].Y;
        }
        var i = 0;
        var hi = n - 1;
        while (hi - i > 1)
        {
            var mid = (i + hi) / 2;
            if (points[mid].X <= x)
            {
                i = mid;
            }
            else
            {
                hi = mid;
            }
        }
        var h = points[i + 1].X - points[i].X;
        if (h <= 0)
        {
            return points[i].Y;
        }
        var t = (x - points[i].X) / h;
        var t2 = t * t;
        var t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * points[i].Y + (t3 - 2 * t2 + t) * h * tangents[i]
             + (-2 * t3 + 3 * t2) * points[i + 1].Y + (t3 - t2) * h * tangents[i + 1];
    }

    /// <summary>
    /// Linear resampling of <paramref name="source"/> onto <c>destination.Length</c> evenly spaced samples, first and last
    /// kept (KPI drilldown: every range becomes 40 sparkline / 72 detail points so ranges morph point by point).
    /// </summary>
    public static void ResampleLinear(ReadOnlySpan<double> source, Span<double> destination)
    {
        var m = destination.Length;
        var n = source.Length;
        if (m == 0)
        {
            return;
        }
        if (n == 0)
        {
            destination.Clear();
            return;
        }
        if (n == 1)
        {
            destination.Fill(source[0]);
            return;
        }
        if (m == 1)
        {
            destination[0] = source[n - 1];
            return;
        }
        for (var i = 0; i < m; i++)
        {
            var at = (double)i * (n - 1) / (m - 1);
            var k = Math.Min((int)Math.Floor(at), n - 2);
            var f = at - k;
            destination[i] = source[k] + (source[k + 1] - source[k]) * f;
        }
    }

    /// <summary>Length of a polyline.</summary>
    public static double PolylineLength(ReadOnlySpan<Point> points)
    {
        double length = 0;
        for (var i = 1; i < points.Length; i++)
        {
            var d = points[i] - points[i - 1];
            length += Math.Sqrt(d.X * d.X + d.Y * d.Y);
        }
        return length;
    }

    private static double Secant(Point a, Point b)
    {
        var h = b.X - a.X;
        return h == 0 ? 0 : (b.Y - a.Y) / h;
    }

    // d3: (sign(s0) + sign(s1)) * min(|s0|, |s1|, 0.5 |p|) || 0, with sign(0) = 1.
    private static double Slope3(Point p0, Point p1, Point p2)
    {
        var h0 = p1.X - p0.X;
        var h1 = p2.X - p1.X;
        var s0 = h0 != 0 ? (p1.Y - p0.Y) / h0 : 0;
        var s1 = h1 != 0 ? (p2.Y - p1.Y) / h1 : 0;
        var p = h0 + h1 != 0 ? (s0 * h1 + s1 * h0) / (h0 + h1) : 0;
        var result = (Sign(s0) + Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), 0.5 * Math.Abs(p));
        return double.IsFinite(result) ? result : 0;
    }

    // d3: h ? (3 (y1 − y0) / h − t) / 2 : t.
    private static double Slope2(Point p0, Point p1, double t)
    {
        var h = p1.X - p0.X;
        return h != 0 ? (3 * (p1.Y - p0.Y) / h - t) / 2 : t;
    }

    private static double Sign(double x) => x < 0 ? -1 : 1;
}

/// <summary>
/// A uniform Catmull-Rom path (Connected scatter) with an arc-length table: points and tangents at a continuous
/// parameter <c>u</c> ∈ [0, n − 1] (integer u = a data point), the length up to u, the u at a length (for a trail drawn
/// to a fraction of its length), and the path cut at u. Rebuild with <see cref="SetPoints"/> when the points move
/// (arrays are reused, no allocation once sized).
/// </summary>
internal sealed class AnalyticsCatmullRom
{
    /// <summary>Samples per segment in the arc-length table (Connected scatter's 24).</summary>
    public const int SamplesPerSegment = 24;

    private Point[] _points = [];
    private Point[] _c1 = [];
    private Point[] _c2 = [];
    private double[] _lengths = [];
    private int _count;

    /// <summary>Data points.</summary>
    public int Count => _count;

    /// <summary>Total length.</summary>
    public double Length => _count < 2 ? 0 : _lengths[(_count - 1) * SamplesPerSegment];

    /// <summary>Replaces the points and rebuilds controls and the arc-length table.</summary>
    public void SetPoints(ReadOnlySpan<Point> points)
    {
        _count = points.Length;
        if (_points.Length < _count)
        {
            _points = new Point[_count];
            _c1 = new Point[Math.Max(1, _count - 1)];
            _c2 = new Point[Math.Max(1, _count - 1)];
            _lengths = new double[Math.Max(1, (_count - 1) * SamplesPerSegment + 1)];
        }
        points.CopyTo(_points);
        if (_count < 2)
        {
            return;
        }
        var span = _points.AsSpan(0, _count);
        for (var i = 0; i < _count - 1; i++)
        {
            (_c1[i], _c2[i]) = AnalyticsCurves.CatmullRomControls(span, i);
        }
        _lengths[0] = 0;
        var previous = _points[0];
        var k = 1;
        for (var i = 0; i < _count - 1; i++)
        {
            for (var s = 1; s <= SamplesPerSegment; s++)
            {
                var p = Bezier(i, (double)s / SamplesPerSegment);
                var d = p - previous;
                _lengths[k] = _lengths[k - 1] + Math.Sqrt(d.X * d.X + d.Y * d.Y);
                previous = p;
                k++;
            }
        }
    }

    /// <summary>Point at <paramref name="u"/> (clamped to [0, n − 1]).</summary>
    public Point PointAt(double u)
    {
        if (_count == 0)
        {
            return default;
        }
        if (_count == 1)
        {
            return _points[0];
        }
        var (i, t) = Locate(u);
        return Bezier(i, t);
    }

    /// <summary>Unnormalized tangent (derivative) at <paramref name="u"/>; arrows take its angle.</summary>
    public Vector TangentAt(double u)
    {
        if (_count < 2)
        {
            return new Vector(1, 0);
        }
        var (i, t) = Locate(u);
        var p0 = _points[i];
        var p3 = _points[i + 1];
        var c1 = _c1[i];
        var c2 = _c2[i];
        var mt = 1 - t;
        var x = 3 * mt * mt * (c1.X - p0.X) + 6 * mt * t * (c2.X - c1.X) + 3 * t * t * (p3.X - c2.X);
        var y = 3 * mt * mt * (c1.Y - p0.Y) + 6 * mt * t * (c2.Y - c1.Y) + 3 * t * t * (p3.Y - c2.Y);
        return new Vector(x, y);
    }

    /// <summary>Arc length from the start to <paramref name="u"/> (table lookup with linear interpolation).</summary>
    public double LengthAt(double u)
    {
        if (_count < 2)
        {
            return 0;
        }
        var position = Math.Clamp(u, 0, _count - 1) * SamplesPerSegment;
        var k = Math.Min((int)Math.Floor(position), (_count - 1) * SamplesPerSegment - 1);
        var f = position - k;
        return _lengths[k] + (_lengths[k + 1] - _lengths[k]) * f;
    }

    /// <summary>The <c>u</c> whose arc length is <paramref name="length"/> (clamped).</summary>
    public double UAtLength(double length)
    {
        if (_count < 2)
        {
            return 0;
        }
        var last = (_count - 1) * SamplesPerSegment;
        if (length <= 0)
        {
            return 0;
        }
        if (length >= _lengths[last])
        {
            return _count - 1;
        }
        var lo = 0;
        var hi = last;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (_lengths[mid] <= length)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        var segment = _lengths[hi] - _lengths[lo];
        var f = segment > 0 ? (length - _lengths[lo]) / segment : 0;
        return (lo + f) / SamplesPerSegment;
    }

    /// <summary>
    /// Writes the path from the first point to <paramref name="uEnd"/> (the last segment is cut with de Casteljau so the
    /// trail ends exactly at the head point).
    /// </summary>
    public void Write(StreamGeometryContext context, double uEnd = double.PositiveInfinity)
    {
        if (_count == 0)
        {
            return;
        }
        context.BeginFigure(_points[0], false);
        if (_count == 1)
        {
            return;
        }
        var end = Math.Clamp(uEnd, 0, _count - 1);
        var whole = (int)Math.Floor(end);
        for (var i = 0; i < Math.Min(whole, _count - 1); i++)
        {
            context.CubicBezierTo(_c1[i], _c2[i], _points[i + 1]);
        }
        var t = end - whole;
        if (whole < _count - 1 && t > 1e-9)
        {
            // First part of segment `whole` up to t.
            var p0 = _points[whole];
            var c1 = _c1[whole];
            var c2 = _c2[whole];
            var p3 = _points[whole + 1];
            var a = Lerp(p0, c1, t);
            var b = Lerp(c1, c2, t);
            var c = Lerp(c2, p3, t);
            var ab = Lerp(a, b, t);
            var bc = Lerp(b, c, t);
            context.CubicBezierTo(a, ab, Lerp(ab, bc, t));
        }
    }

    private (int Segment, double T) Locate(double u)
    {
        var clamped = Math.Clamp(double.IsNaN(u) ? 0 : u, 0, _count - 1);
        var i = Math.Min((int)Math.Floor(clamped), _count - 2);
        return (i, clamped - i);
    }

    private Point Bezier(int i, double t)
    {
        var p0 = _points[i];
        var c1 = _c1[i];
        var c2 = _c2[i];
        var p3 = _points[i + 1];
        var mt = 1 - t;
        var a = mt * mt * mt;
        var b = 3 * mt * mt * t;
        var c = 3 * mt * t * t;
        var d = t * t * t;
        return new Point(a * p0.X + b * c1.X + c * c2.X + d * p3.X, a * p0.Y + b * c1.Y + c * c2.Y + d * p3.Y);
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
