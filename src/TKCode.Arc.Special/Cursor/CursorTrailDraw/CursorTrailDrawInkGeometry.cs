using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Variable width ink for <see cref="CursorTrailDraw"/>: the outline polygon of a stroke through centerline points
/// with a width per point, a short linear taper at the start, an optional taper at the end, round caps and round
/// sweeps at sharp turns, filled as one closed figure. Also builds the clean shapes' centerlines.
/// </summary>
internal static class CursorTrailDrawInkGeometry
{
    /// <summary>Where the start taper begins, as a share of the full width.</summary>
    public const double StartTaperFloor = 0.35;

    /// <summary>Where the end taper ends, as a share of the full width.</summary>
    public const double EndTaperFloor = 0.45;

    private const int CapSteps = 10;
    private const int TurnSteps = 10;

    /// <summary>
    /// Builds the outline of a stroke into <paramref name="outline"/> (cleared first).
    /// </summary>
    /// <param name="points">Centerline, in drawing units.</param>
    /// <param name="widths">Full width at each point, in drawing units.</param>
    /// <param name="count">How many of the points to use.</param>
    /// <param name="startTaper">Length over which the start widens from <see cref="StartTaperFloor"/>; 0 for none.</param>
    /// <param name="endTaper">Length over which the end narrows to <see cref="EndTaperFloor"/>; 0 for none.</param>
    /// <param name="outline">Receives the closed polygon.</param>
    public static void Build(IReadOnlyList<Point> points, IReadOnlyList<double> widths, int count, double startTaper,
        double endTaper, List<Point> outline)
    {
        outline.Clear();
        if (count <= 0)
        {
            return;
        }
        if (count == 1 || TotalLength(points, count) < 0.01)
        {
            AddDot(points[0], Math.Max(0.2, widths[0] * 0.5 * 0.5), outline);
            return;
        }
        var total = TotalLength(points, count);
        var left = s_left;
        var right = s_right;
        left.Clear();
        right.Clear();
        double along = 0;
        var previousDirection = Direction(points[0], points[1]);
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                along += Distance(points[i - 1], points[i]);
            }
            var into = i == 0 ? Direction(points[0], points[1]) : Direction(points[i - 1], points[i]);
            var outOf = i == count - 1 ? into : Direction(points[i], points[i + 1]);
            if (into == default)
            {
                into = previousDirection;
            }
            if (outOf == default)
            {
                outOf = into;
            }
            var factor = 1.0;
            if (startTaper > 0)
            {
                factor = Math.Min(factor, StartTaperFloor + (1 - StartTaperFloor) * Math.Min(1, along / startTaper));
            }
            if (endTaper > 0)
            {
                factor = Math.Min(factor, EndTaperFloor + (1 - EndTaperFloor) * Math.Min(1, (total - along) / endTaper));
            }
            var radius = Math.Max(0.05, widths[i] * 0.5 * factor);
            var p = points[i];
            if (i > 0 && i < count - 1 && Dot(into, outOf) < -0.1)
            {
                // A sharp turn: sweep round the point on both sides so the ink does not pinch or fold.
                var startAngle = Math.Atan2(into.Y, into.X);
                var endAngle = Math.Atan2(outOf.Y, outOf.X);
                var sweep = NormalizeAngle(endAngle - startAngle);
                for (var k = 0; k <= TurnSteps; k++)
                {
                    var a = startAngle + sweep * k / TurnSteps;
                    var n = new Vector(-Math.Sin(a), Math.Cos(a));
                    left.Add(p + n * radius);
                    right.Add(p - n * radius);
                }
            }
            else
            {
                var tangent = Normalize(into + outOf);
                if (tangent == default)
                {
                    tangent = outOf;
                }
                var normal = new Vector(-tangent.Y, tangent.X);
                left.Add(p + normal * radius);
                right.Add(p - normal * radius);
            }
            previousDirection = outOf;
        }
        outline.AddRange(left);
        // End cap: half a circle from the left edge round the tip to the right edge.
        AddCap(points[count - 1], left[^1], Direction(points[count - 2], points[count - 1]), outline);
        for (var i = right.Count - 1; i >= 0; i--)
        {
            outline.Add(right[i]);
        }
        AddCap(points[0], right[0], Direction(points[1], points[0]), outline);
    }

    private static readonly List<Point> s_left = new(512);
    private static readonly List<Point> s_right = new(512);

    /// <summary>Fills a polygon into a new geometry (null when it has fewer than three points).</summary>
    public static StreamGeometry? ToGeometry(List<Point> outline)
    {
        if (outline.Count < 3)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(FillRule.NonZero);
            context.BeginFigure(outline[0], true);
            for (var i = 1; i < outline.Count; i++)
            {
                context.LineTo(outline[i]);
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>A stroked centerline (lines, polygons, ovals) as a geometry.</summary>
    public static StreamGeometry Centerline(IReadOnlyList<Point> points, bool closed)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (points.Count > 0)
            {
                context.BeginFigure(points[0], false);
                for (var i = 1; i < points.Count; i++)
                {
                    context.LineTo(points[i]);
                }
                context.EndFigure(closed);
            }
        }
        return geometry;
    }

    public static double TotalLength(IReadOnlyList<Point> points, int count)
    {
        double length = 0;
        for (var i = 1; i < count; i++)
        {
            length += Distance(points[i - 1], points[i]);
        }
        return length;
    }

    /// <summary>Resamples a polyline to <paramref name="n"/> points evenly spaced along its length.</summary>
    public static Point[] Resample(IReadOnlyList<Point> points, int n)
    {
        var result = new Point[n];
        if (points.Count == 0)
        {
            return result;
        }
        var total = TotalLength(points, points.Count);
        if (points.Count == 1 || total <= 0)
        {
            Array.Fill(result, points[0]);
            return result;
        }
        var step = total / (n - 1);
        var segment = 1;
        double walked = 0;
        result[0] = points[0];
        for (var k = 1; k < n - 1; k++)
        {
            var target = step * k;
            while (segment < points.Count - 1 && walked + Distance(points[segment - 1], points[segment]) < target)
            {
                walked += Distance(points[segment - 1], points[segment]);
                segment++;
            }
            var a = points[segment - 1];
            var b = points[segment];
            var length = Distance(a, b);
            var t = length <= 0 ? 0 : Math.Clamp((target - walked) / length, 0, 1);
            result[k] = new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }
        result[n - 1] = points[^1];
        return result;
    }

    /// <summary>Resamples a polyline's widths the same way as <see cref="Resample"/> resamples its points.</summary>
    public static double[] ResampleWidths(IReadOnlyList<Point> points, IReadOnlyList<double> widths, int n)
    {
        var result = new double[n];
        if (points.Count == 0)
        {
            return result;
        }
        var total = TotalLength(points, points.Count);
        if (points.Count == 1 || total <= 0)
        {
            Array.Fill(result, widths[0]);
            return result;
        }
        var step = total / (n - 1);
        var segment = 1;
        double walked = 0;
        result[0] = widths[0];
        for (var k = 1; k < n - 1; k++)
        {
            var target = step * k;
            while (segment < points.Count - 1 && walked + Distance(points[segment - 1], points[segment]) < target)
            {
                walked += Distance(points[segment - 1], points[segment]);
                segment++;
            }
            var length = Distance(points[segment - 1], points[segment]);
            var t = length <= 0 ? 0 : Math.Clamp((target - walked) / length, 0, 1);
            result[k] = widths[segment - 1] + (widths[segment] - widths[segment - 1]) * t;
        }
        result[n - 1] = widths[^1];
        return result;
    }

    private static void AddCap(Point center, Point from, Vector away, List<Point> outline)
    {
        var radius = Distance(center, from);
        var begin = Math.Atan2(from.Y - center.Y, from.X - center.X);
        var mid = begin + Math.PI / 2;
        var sign = Dot(new Vector(Math.Cos(mid), Math.Sin(mid)), away) >= 0 ? 1 : -1;
        for (var k = 1; k < CapSteps; k++)
        {
            var a = begin + sign * Math.PI * k / CapSteps;
            outline.Add(new Point(center.X + Math.Cos(a) * radius, center.Y + Math.Sin(a) * radius));
        }
    }

    private static void AddDot(Point center, double radius, List<Point> outline)
    {
        for (var k = 0; k < 16; k++)
        {
            var a = k / 16.0 * Math.PI * 2;
            outline.Add(new Point(center.X + Math.Cos(a) * radius, center.Y + Math.Sin(a) * radius));
        }
    }

    private static double NormalizeAngle(double angle)
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

    public static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Vector Direction(Point from, Point to) => Normalize(to - from);

    private static Vector Normalize(Vector v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return length < 1e-9 ? default : v / length;
    }

    private static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y;
}
