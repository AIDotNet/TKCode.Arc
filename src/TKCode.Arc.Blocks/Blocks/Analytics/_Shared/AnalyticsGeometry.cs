using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// <see cref="StreamGeometry"/> builders for the Analytics plots, all in pixel coordinates (never stretch a geometry with
/// a transform to fit: strokes would scale; Arc's <c>non-scaling-stroke</c> paths are rebuilt at the plot size instead).
/// Build a geometry when its inputs change and keep it; a per-frame morph builds one per frame (cheap for ≤ 400 points).
/// </summary>
internal static class AnalyticsGeometry
{
    /// <summary>An open line through the points.</summary>
    public static StreamGeometry Line(ReadOnlySpan<Point> points, AnalyticsCurve curve = AnalyticsCurve.Linear)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            AnalyticsCurves.Write(context, points, curve);
            if (points.Length > 0)
            {
                context.EndFigure(false);
            }
        }
        return geometry;
    }

    /// <summary>
    /// The area between the curve and a horizontal <paramref name="baselineY"/> (Arc: line, then <c>L</c> down to the
    /// baseline under the last point, <c>L</c> back under the first, <c>Z</c>).
    /// </summary>
    public static StreamGeometry Area(ReadOnlySpan<Point> points, double baselineY, AnalyticsCurve curve = AnalyticsCurve.Linear)
    {
        var geometry = new StreamGeometry();
        if (points.Length == 0)
        {
            return geometry;
        }
        using (var context = geometry.Open())
        {
            AnalyticsCurves.Write(context, points, curve);
            context.LineTo(new Point(points[^1].X, baselineY));
            context.LineTo(new Point(points[0].X, baselineY));
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>
    /// A closed band: <paramref name="upper"/> forwards, then <paramref name="lower"/> backwards (forecast cone, stacked
    /// area layer whose bottom is the layer below, latency p95/p50 band). Both edges should share their x values.
    /// </summary>
    public static StreamGeometry Band(ReadOnlySpan<Point> upper, ReadOnlySpan<Point> lower, AnalyticsCurve curve = AnalyticsCurve.Linear)
    {
        var geometry = new StreamGeometry();
        if (upper.Length == 0 || lower.Length == 0)
        {
            return geometry;
        }
        using (var context = geometry.Open())
        {
            AnalyticsCurves.Write(context, upper, curve);
            AnalyticsCurves.WriteReversed(context, lower, curve);
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>A closed straight-edged polygon.</summary>
    public static StreamGeometry Polygon(ReadOnlySpan<Point> points)
    {
        var geometry = new StreamGeometry();
        if (points.Length == 0)
        {
            return geometry;
        }
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
            {
                context.LineTo(points[i]);
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>
    /// A rectangle with its own radius per corner, each clamped to half the shorter side (MRR waterfall: totals 3px on the
    /// top corners and square at the baseline, movements 2px on all four; cohort cells).
    /// </summary>
    public static StreamGeometry RoundedRect(Rect rect, double topLeft, double topRight, double bottomRight, double bottomLeft)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            WriteRoundedRect(context, rect, topLeft, topRight, bottomRight, bottomLeft);
        }
        return geometry;
    }

    /// <summary>Writes a closed rounded rectangle figure (see <see cref="RoundedRect"/>) into an open context.</summary>
    public static void WriteRoundedRect(StreamGeometryContext context, Rect rect, double topLeft, double topRight, double bottomRight, double bottomLeft)
    {
        var max = Math.Max(0, Math.Min(rect.Width, rect.Height) / 2);
        var tl = Math.Clamp(topLeft, 0, max);
        var tr = Math.Clamp(topRight, 0, max);
        var br = Math.Clamp(bottomRight, 0, max);
        var bl = Math.Clamp(bottomLeft, 0, max);
        context.BeginFigure(new Point(rect.Left + tl, rect.Top), true);
        context.LineTo(new Point(rect.Right - tr, rect.Top));
        Corner(context, tr, new Point(rect.Right, rect.Top + tr));
        context.LineTo(new Point(rect.Right, rect.Bottom - br));
        Corner(context, br, new Point(rect.Right - br, rect.Bottom));
        context.LineTo(new Point(rect.Left + bl, rect.Bottom));
        Corner(context, bl, new Point(rect.Left, rect.Bottom - bl));
        context.LineTo(new Point(rect.Left, rect.Top + tl));
        Corner(context, tl, new Point(rect.Left + tl, rect.Top));
        context.EndFigure(true);
    }

    /// <summary>
    /// A Sankey ribbon (Journey flow): the top edge is a cubic from (<paramref name="x0"/>, <paramref name="y0Top"/>) to
    /// (<paramref name="x1"/>, <paramref name="y1Top"/>) with both control points at the horizontal midpoint (horizontal
    /// start and end tangents), down the target side, the same shape back along the bottom edge, closed up the source side.
    /// </summary>
    public static StreamGeometry Ribbon(double x0, double y0Top, double y0Bottom, double x1, double y1Top, double y1Bottom)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            WriteRibbon(context, x0, y0Top, y0Bottom, x1, y1Top, y1Bottom);
        }
        return geometry;
    }

    /// <summary>Writes a closed ribbon figure (see <see cref="Ribbon"/>) into an open context.</summary>
    public static void WriteRibbon(StreamGeometryContext context, double x0, double y0Top, double y0Bottom, double x1, double y1Top, double y1Bottom)
    {
        var mid = (x0 + x1) / 2;
        context.BeginFigure(new Point(x0, y0Top), true);
        context.CubicBezierTo(new Point(mid, y0Top), new Point(mid, y1Top), new Point(x1, y1Top));
        context.LineTo(new Point(x1, y1Bottom));
        context.CubicBezierTo(new Point(mid, y1Bottom), new Point(mid, y0Bottom), new Point(x0, y0Bottom));
        context.EndFigure(true);
    }

    private static void Corner(StreamGeometryContext context, double radius, Point end)
    {
        if (radius <= 0)
        {
            context.LineTo(end);
            return;
        }
        context.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
    }
}
