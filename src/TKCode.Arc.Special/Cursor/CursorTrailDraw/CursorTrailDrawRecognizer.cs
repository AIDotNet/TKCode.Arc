using Avalonia;

namespace TKCode.Arc;

/// <summary>A clean shape found in a rough stroke: its kind and the geometry it snaps to.</summary>
/// <param name="Shape">The kind.</param>
/// <param name="Corners">Line ends or polygon corners (in drawing order); empty for ovals.</param>
/// <param name="Center">Oval center.</param>
/// <param name="RadiusX">Oval radius along its own x axis.</param>
/// <param name="RadiusY">Oval radius along its own y axis.</param>
/// <param name="Angle">Oval or rectangle rotation in radians (0 when level).</param>
/// <param name="Clockwise">Whether the stroke went round clockwise on screen (closed shapes).</param>
internal readonly record struct CursorTrailDrawCleanShape(
    CursorTrailDrawShape Shape,
    IReadOnlyList<Point> Corners,
    Point Center,
    double RadiusX,
    double RadiusY,
    double Angle,
    bool Clockwise)
{
    public bool IsClosed => Shape is not CursorTrailDrawShape.Line;

    public bool IsOval => Shape is CursorTrailDrawShape.Circle or CursorTrailDrawShape.Ellipse;

    /// <summary>The centerline as a polyline: two ends, the corners, or <paramref name="ovalSteps"/> points round an oval.</summary>
    public Point[] Centerline(int ovalSteps = 64)
    {
        if (!IsOval)
        {
            return Corners.ToArray();
        }
        var points = new Point[ovalSteps];
        var cos = Math.Cos(Angle);
        var sin = Math.Sin(Angle);
        for (var i = 0; i < ovalSteps; i++)
        {
            var t = i * Math.PI * 2 / ovalSteps;
            var u = Math.Cos(t) * RadiusX;
            var v = Math.Sin(t) * RadiusY;
            points[i] = new Point(Center.X + u * cos - v * sin, Center.Y + u * sin + v * cos);
        }
        return points;
    }

    /// <summary>
    /// <paramref name="n"/> points evenly spaced along the clean centerline, matched to a stroke: closed shapes start at
    /// the point nearest <paramref name="start"/>, go round the way the stroke went and end where they started.
    /// </summary>
    public Point[] Sample(int n, Point start)
    {
        if (!IsClosed)
        {
            return CursorTrailDrawInkGeometry.Resample(Corners, n);
        }
        var ring = Centerline(256).ToList();
        var clockwise = CursorTrailDrawRecognizer.SignedArea(ring) > 0;
        if (clockwise != Clockwise)
        {
            ring.Reverse();
        }
        // Densify polygons so the start can sit anywhere along an edge.
        var dense = new List<Point>(512);
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            var steps = IsOval ? 1 : 64;
            for (var k = 0; k < steps; k++)
            {
                var t = (double)k / steps;
                dense.Add(new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            }
        }
        var nearest = 0;
        var best = double.MaxValue;
        for (var i = 0; i < dense.Count; i++)
        {
            var d = CursorTrailDrawInkGeometry.Distance(dense[i], start);
            if (d < best)
            {
                best = d;
                nearest = i;
            }
        }
        var loop = new List<Point>(dense.Count + 1);
        for (var i = 0; i <= dense.Count; i++)
        {
            loop.Add(dense[(nearest + i) % dense.Count]);
        }
        return CursorTrailDrawInkGeometry.Resample(loop, n);
    }
}

/// <summary>
/// Recognizes rough shapes in a stroke (Arc Cursor trail draw, design reference for the thresholds): straight lines
/// (snapped to 0, 45 and 90 degrees within <see cref="LineSnapDegrees"/>), circles and ellipses, rectangles and
/// squares (leveled within <see cref="LevelDegrees"/>), triangles and other four sided shapes. Anything else stays
/// freehand. Pure geometry, independent of the board's scale.
/// </summary>
internal static class CursorTrailDrawRecognizer
{
    /// <summary>Lines within this many degrees of 0, 45 or 90 snap to it.</summary>
    public const double LineSnapDegrees = 7;

    /// <summary>Rectangles and triangle edges within this many degrees of level are leveled.</summary>
    public const double LevelDegrees = 10.5;

    /// <summary>Ovals up to this aspect are circles.</summary>
    public const double CircleAspect = 1.22;

    /// <summary>Rectangles up to this aspect are squares.</summary>
    public const double SquareAspect = 1.125;

    /// <summary>A stroke is closed when its end comes back within this share of its length to its start.</summary>
    public const double CloseShare = 0.17;

    /// <summary>A line's ends are at least this share of its length apart.</summary>
    public const double LineStraightness = 0.92;

    /// <summary>A line strays from its chord by at most this share of its length.</summary>
    public const double LineDeviation = 0.06;

    private const int Samples = 64;
    private const double CornerDegrees = 50;

    /// <summary>The clean shape a stroke snaps to, or null when it stays freehand.</summary>
    /// <param name="points">The stroke's centerline.</param>
    /// <param name="minimumLength">Strokes shorter than this (in the points' units) never snap.</param>
    public static CursorTrailDrawCleanShape? Recognize(IReadOnlyList<Point> points, double minimumLength = 0)
    {
        if (points.Count < 2)
        {
            return null;
        }
        var length = CursorTrailDrawInkGeometry.TotalLength(points, points.Count);
        if (length <= 0 || length < minimumLength)
        {
            return null;
        }
        var path = CursorTrailDrawInkGeometry.Resample(points, Samples);
        var closing = ClosingIndex(path, length);
        if (closing < 0)
        {
            return RecognizeLine(path, length);
        }
        var loop = CursorTrailDrawInkGeometry.Resample(path.Take(closing + 1).ToList(), Samples);
        var clockwise = SignedArea(loop) > 0;
        var corners = Corners(loop);
        if (corners.Count is 3 or 4)
        {
            var polygon = corners.Select(i => loop[i]).ToList();
            if (FitsPolygon(loop, polygon))
            {
                return corners.Count == 3 ? Triangle(polygon, clockwise) : Quadrilateral(polygon, loop, clockwise);
            }
        }
        if (corners.Count <= 2)
        {
            return Oval(loop, clockwise);
        }
        return null;
    }

    /// <summary>A clean shape rebuilt from saved points: ovals are fitted (center, radii, rotation), others use the points.</summary>
    public static CursorTrailDrawCleanShape FromPoints(CursorTrailDrawShape shape, IReadOnlyList<Point> points)
    {
        var clockwise = SignedArea(points) > 0;
        if (shape is not (CursorTrailDrawShape.Circle or CursorTrailDrawShape.Ellipse) || points.Count < 3)
        {
            return new CursorTrailDrawCleanShape(shape, points.ToArray(), default, 0, 0, 0, clockwise);
        }
        var loop = points.Concat([points[0]]).ToArray();
        var fit = FitOval(loop, level: false);
        return new CursorTrailDrawCleanShape(shape, [], fit.Center, fit.RadiusX, fit.RadiusY, fit.Angle, clockwise);
    }

    /// <summary>Snaps an angle (radians) to the nearest multiple of 45 degrees when it is close enough.</summary>
    public static double SnapLineAngle(double angle)
    {
        var step = Math.PI / 4;
        var nearest = Math.Round(angle / step) * step;
        return Math.Abs(angle - nearest) < LineSnapDegrees * Math.PI / 180 ? nearest : angle;
    }

    /// <summary>Twice the signed area of a polygon; positive when it goes round clockwise on screen (y down).</summary>
    public static double SignedArea(IReadOnlyList<Point> polygon)
    {
        double sum = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum;
    }

    /// <summary>Index of the sample where a closed stroke comes back to its start, or -1 for an open stroke.</summary>
    private static int ClosingIndex(Point[] path, double length)
    {
        var start = path[0];
        var best = -1;
        var bestDistance = double.MaxValue;
        // The end may stop short of the start or overshoot past it: look in the second half for the nearest pass.
        for (var i = Samples / 2; i < Samples; i++)
        {
            var d = CursorTrailDrawInkGeometry.Distance(path[i], start);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        if (bestDistance >= CloseShare * length)
        {
            return -1;
        }
        // Must have gone round: something in between is far from the start.
        var spread = 0.0;
        for (var i = 1; i < best; i++)
        {
            spread = Math.Max(spread, CursorTrailDrawInkGeometry.Distance(path[i], start));
        }
        return spread > bestDistance * 3 ? best : -1;
    }

    private static CursorTrailDrawCleanShape? RecognizeLine(Point[] path, double length)
    {
        var start = path[0];
        var end = path[^1];
        var chord = CursorTrailDrawInkGeometry.Distance(start, end);
        if (chord < LineStraightness * length)
        {
            return null;
        }
        var dx = (end.X - start.X) / chord;
        var dy = (end.Y - start.Y) / chord;
        var deviation = 0.0;
        foreach (var p in path)
        {
            deviation = Math.Max(deviation, Math.Abs((p.X - start.X) * dy - (p.Y - start.Y) * dx));
        }
        if (deviation > LineDeviation * length)
        {
            return null;
        }
        // The clean line keeps the drawn length and turns about its first point.
        var angle = SnapLineAngle(Math.Atan2(end.Y - start.Y, end.X - start.X));
        var cleanEnd = new Point(start.X + Math.Cos(angle) * chord, start.Y + Math.Sin(angle) * chord);
        return new CursorTrailDrawCleanShape(CursorTrailDrawShape.Line, [start, cleanEnd], default, 0, 0, angle, false);
    }

    /// <summary>Samples where the loop turns sharply: windowed turning above the corner angle, one per corner.</summary>
    private static List<int> Corners(Point[] loop)
    {
        var n = loop.Length - 1; // the last sample closes the loop
        var turn = new double[n];
        for (var i = 0; i < n; i++)
        {
            var prev = loop[(i - 1 + n) % n];
            var here = loop[i];
            var next = loop[(i + 1) % n];
            var a = Math.Atan2(here.Y - prev.Y, here.X - prev.X);
            var b = Math.Atan2(next.Y - here.Y, next.X - here.X);
            turn[i] = Wrap(b - a);
        }
        var window = new double[n];
        for (var i = 0; i < n; i++)
        {
            window[i] = Math.Abs(turn[(i - 1 + n) % n] + turn[i] + turn[(i + 1) % n]) * 180 / Math.PI;
        }
        var corners = new List<int>();
        for (var i = 0; i < n; i++)
        {
            if (window[i] < CornerDegrees)
            {
                continue;
            }
            var isPeak = true;
            for (var k = -3; k <= 3 && isPeak; k++)
            {
                if (k == 0)
                {
                    continue;
                }
                var j = (i + k + n) % n;
                if (window[j] > window[i] || (window[j] == window[i] && k < 0))
                {
                    isPeak = false;
                }
            }
            if (isPeak)
            {
                corners.Add(i);
            }
        }
        return corners;
    }

    private static bool FitsPolygon(Point[] loop, List<Point> polygon)
    {
        double perimeter = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            perimeter += CursorTrailDrawInkGeometry.Distance(polygon[i], polygon[(i + 1) % polygon.Count]);
        }
        if (perimeter <= 0)
        {
            return false;
        }
        foreach (var p in loop)
        {
            var nearest = double.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                nearest = Math.Min(nearest, SegmentDistance(p, polygon[i], polygon[(i + 1) % polygon.Count]));
            }
            if (nearest > perimeter * 0.06)
            {
                return false;
            }
        }
        return true;
    }

    private static CursorTrailDrawCleanShape Triangle(List<Point> corners, bool clockwise)
    {
        var level = LevelDegrees * Math.PI / 180;
        for (var i = 0; i < 3; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 3];
            var angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            var fromLevel = Math.Abs(Wrap(angle * 2) / 2);
            if (fromLevel < level)
            {
                var y = (a.Y + b.Y) / 2;
                corners[i] = new Point(a.X, y);
                corners[(i + 1) % 3] = new Point(b.X, y);
            }
            else if (Math.Abs(fromLevel - Math.PI / 2) < level)
            {
                var x = (a.X + b.X) / 2;
                corners[i] = new Point(x, a.Y);
                corners[(i + 1) % 3] = new Point(x, b.Y);
            }
        }
        return new CursorTrailDrawCleanShape(CursorTrailDrawShape.Triangle, corners, default, 0, 0, 0, clockwise);
    }

    private static CursorTrailDrawCleanShape Quadrilateral(List<Point> corners, Point[] loop, bool clockwise)
    {
        var square = true;
        for (var i = 0; i < 4 && square; i++)
        {
            var prev = corners[(i + 3) % 4];
            var here = corners[i];
            var next = corners[(i + 1) % 4];
            var a = Math.Atan2(prev.Y - here.Y, prev.X - here.X);
            var b = Math.Atan2(next.Y - here.Y, next.X - here.X);
            var interior = Math.Abs(Wrap(b - a)) * 180 / Math.PI;
            square = Math.Abs(interior - 90) < 20;
        }
        if (!square)
        {
            return new CursorTrailDrawCleanShape(CursorTrailDrawShape.Quad, corners, default, 0, 0, 0, clockwise);
        }
        // Orientation and size: the smallest box round the stroke, searched every quarter degree.
        var theta = MinimumBoxAngle(loop);
        if (Math.Abs(theta) < LevelDegrees * Math.PI / 180)
        {
            theta = 0;
        }
        var cos = Math.Cos(theta);
        var sin = Math.Sin(theta);
        double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
        for (var i = 0; i < loop.Length - 1; i++)
        {
            var p = loop[i];
            var u = p.X * cos + p.Y * sin;
            var v = -p.X * sin + p.Y * cos;
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }
        var mu = (minU + maxU) / 2;
        var mv = (minV + maxV) / 2;
        var cx = mu * cos - mv * sin;
        var cy = mu * sin + mv * cos;
        var sumU = (maxU - minU) * 2;
        var sumV = (maxV - minV) * 2;
        var halfW = sumU / 4;
        var halfH = sumV / 4;
        var shape = CursorTrailDrawShape.Rectangle;
        if (Math.Max(halfW, halfH) / Math.Max(1e-9, Math.Min(halfW, halfH)) <= SquareAspect)
        {
            halfW = halfH = (halfW + halfH) / 2;
            shape = CursorTrailDrawShape.Square;
        }
        // Corners in the order the stroke visited them, each moved to its clean place.
        var clean = new List<Point>(4);
        foreach (var p in corners)
        {
            var u = (p.X - cx) * cos + (p.Y - cy) * sin >= 0 ? halfW : -halfW;
            var v = -(p.X - cx) * sin + (p.Y - cy) * cos >= 0 ? halfH : -halfH;
            clean.Add(new Point(cx + u * cos - v * sin, cy + u * sin + v * cos));
        }
        return new CursorTrailDrawCleanShape(shape, clean, new Point(cx, cy), halfW, halfH, theta, clockwise);
    }

    /// <summary>The rotation (−45° to 45°) of the smallest box that holds the loop.</summary>
    private static double MinimumBoxAngle(Point[] loop)
    {
        var best = 0.0;
        var bestArea = double.MaxValue;
        for (var step = -180; step < 180; step++)
        {
            var a = step * Math.PI / 720;
            var cos = Math.Cos(a);
            var sin = Math.Sin(a);
            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            for (var i = 0; i < loop.Length - 1; i++)
            {
                var u = loop[i].X * cos + loop[i].Y * sin;
                var v = -loop[i].X * sin + loop[i].Y * cos;
                minU = Math.Min(minU, u);
                maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v);
                maxV = Math.Max(maxV, v);
            }
            var area = (maxU - minU) * (maxV - minV);
            if (area < bestArea - 1e-9)
            {
                bestArea = area;
                best = a;
            }
        }
        return best;
    }

    private static CursorTrailDrawCleanShape? Oval(Point[] loop, bool clockwise)
    {
        var fit = FitOval(loop, level: true);
        var (center, rx, ry, angle) = fit;
        if (rx <= 0 || ry <= 0)
        {
            return null;
        }
        var n = loop.Length - 1;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        // A loop that is not round (a kidney, a figure of eight) stays freehand.
        double error = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = loop[i].X - center.X;
            var dy = loop[i].Y - center.Y;
            var u = (dx * cos + dy * sin) / rx;
            var v = (-dx * sin + dy * cos) / ry;
            var r = Math.Sqrt(u * u + v * v) - 1;
            error += r * r;
        }
        if (Math.Sqrt(error / n) > 0.14)
        {
            return null;
        }
        if (Math.Max(rx, ry) / Math.Min(rx, ry) <= CircleAspect)
        {
            var r = (rx + ry) / 2;
            return new CursorTrailDrawCleanShape(CursorTrailDrawShape.Circle, [], center, r, r, 0, clockwise);
        }
        return new CursorTrailDrawCleanShape(CursorTrailDrawShape.Ellipse, [], center, rx, ry, angle, clockwise);
    }

    /// <summary>Principal axes and extents of a closed loop (last point repeats the first).</summary>
    private static (Point Center, double RadiusX, double RadiusY, double Angle) FitOval(Point[] loop, bool level)
    {
        var n = loop.Length - 1;
        double cx = 0, cy = 0;
        for (var i = 0; i < n; i++)
        {
            cx += loop[i].X;
            cy += loop[i].Y;
        }
        cx /= n;
        cy /= n;
        double xx = 0, yy = 0, xy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = loop[i].X - cx;
            var dy = loop[i].Y - cy;
            xx += dx * dx;
            yy += dy * dy;
            xy += dx * dy;
        }
        var angle = 0.5 * Math.Atan2(2 * xy, xx - yy);
        var levelAngle = LevelDegrees * Math.PI / 180;
        if (level && (Math.Abs(Wrap(angle * 2) / 2) < levelAngle || Math.Abs(Math.Abs(Wrap(angle * 2) / 2) - Math.PI / 2) < levelAngle))
        {
            angle = 0;
        }
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
        for (var i = 0; i < n; i++)
        {
            var dx = loop[i].X - cx;
            var dy = loop[i].Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }
        var rx = (maxU - minU) / 2;
        var ry = (maxV - minV) / 2;
        var center = new Point(cx + (minU + maxU) / 2 * cos - (minV + maxV) / 2 * sin,
            cy + (minU + maxU) / 2 * sin + (minV + maxV) / 2 * cos);
        return (center, rx, ry, angle);
    }

    private static double SegmentDistance(Point p, Point a, Point b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = abx * abx + aby * aby;
        var t = lengthSquared <= 0 ? 0 : Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lengthSquared, 0, 1);
        var x = a.X + abx * t - p.X;
        var y = a.Y + aby * t - p.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static double Wrap(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2;
        }
        while (angle < -Math.PI)
        {
            angle += Math.PI * 2;
        }
        return angle;
    }
}
