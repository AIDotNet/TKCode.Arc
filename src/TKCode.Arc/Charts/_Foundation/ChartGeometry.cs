using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// StreamGeometry builders shared by the charts. Point buffers are owned by the caller and reused; a geometry
/// is built only when its shape changes (data, size, or a morph frame) and is otherwise cached.
/// </summary>
internal static class ChartGeometry
{
    /// <summary>An open polyline through <paramref name="points"/>.</summary>
    public static StreamGeometry Line(ReadOnlySpan<Point> points)
    {
        var geometry = new StreamGeometry();
        if (points.Length == 0)
        {
            return geometry;
        }
        using var context = geometry.Open();
        context.BeginFigure(points[0], false);
        for (var i = 1; i < points.Length; i++)
        {
            context.LineTo(points[i]);
        }
        context.EndFigure(false);
        return geometry;
    }

    /// <summary>The polyline closed down to <paramref name="baseline"/> (area fills).</summary>
    public static StreamGeometry Area(ReadOnlySpan<Point> points, double baseline)
    {
        var geometry = new StreamGeometry();
        if (points.Length == 0)
        {
            return geometry;
        }
        using var context = geometry.Open();
        context.BeginFigure(points[0], true);
        for (var i = 1; i < points.Length; i++)
        {
            context.LineTo(points[i]);
        }
        context.LineTo(new Point(points[^1].X, baseline));
        context.LineTo(new Point(points[0].X, baseline));
        context.EndFigure(true);
        return geometry;
    }

    /// <summary>A closed band: <paramref name="upper"/> left to right, then <paramref name="lower"/> right to left.</summary>
    public static StreamGeometry Band(ReadOnlySpan<Point> upper, ReadOnlySpan<Point> lower)
    {
        var geometry = new StreamGeometry();
        if (upper.Length == 0)
        {
            return geometry;
        }
        using var context = geometry.Open();
        context.BeginFigure(upper[0], true);
        for (var i = 1; i < upper.Length; i++)
        {
            context.LineTo(upper[i]);
        }
        for (var i = lower.Length - 1; i >= 0; i--)
        {
            context.LineTo(lower[i]);
        }
        context.EndFigure(true);
        return geometry;
    }

    /// <summary>A column from a square baseline to a softly rounded data end (radius ≤ 4, half the width and the height).</summary>
    public static StreamGeometry Bar(double x, double width, double height, double baseline, double radius = 4)
    {
        var geometry = new StreamGeometry();
        if (width <= 0 || height <= 0.01)
        {
            return geometry;
        }
        var r = Math.Min(radius, Math.Min(width / 2, height));
        var top = baseline - height;
        using var context = geometry.Open();
        context.BeginFigure(new Point(x, baseline), true);
        context.LineTo(new Point(x, top + r));
        if (r > 0.01)
        {
            context.ArcTo(new Point(x + r, top), new Size(r, r), 0, false, SweepDirection.Clockwise);
        }
        context.LineTo(new Point(x + width - r, top));
        if (r > 0.01)
        {
            context.ArcTo(new Point(x + width, top + r), new Size(r, r), 0, false, SweepDirection.Clockwise);
        }
        context.LineTo(new Point(x + width, baseline));
        context.EndFigure(true);
        return geometry;
    }

    private const double Tau = Math.PI * 2;

    /// <summary>
    /// An annular sector between two turns of the ring (clockwise from the top) with rounded corners. Its sides
    /// run parallel to the radius at a fixed distance, so the gap between neighbours is the same number of
    /// pixels from the inner edge to the outer one; a thin sector narrows to a wedge and then to nothing.
    /// Returns null when nothing should be drawn.
    /// </summary>
    public static StreamGeometry? Sector(Point c, double outer, double inner, double t0, double t1, double gap, double corner)
    {
        var turns = t1 - t0;
        if (turns <= 1e-6)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            Point At(double radius, double angle) => new(c.X + radius * Math.Cos(angle), c.Y + radius * Math.Sin(angle));
            if (turns >= 1 - 1e-6)
            {
                context.BeginFigure(At(outer, -Math.PI / 2), true);
                context.ArcTo(At(outer, Math.PI / 2), new Size(outer, outer), 0, true, SweepDirection.Clockwise);
                context.ArcTo(At(outer, -Math.PI / 2), new Size(outer, outer), 0, true, SweepDirection.Clockwise);
                context.EndFigure(true);
                context.BeginFigure(At(inner, -Math.PI / 2), true);
                context.ArcTo(At(inner, Math.PI / 2), new Size(inner, inner), 0, true, SweepDirection.CounterClockwise);
                context.ArcTo(At(inner, -Math.PI / 2), new Size(inner, inner), 0, true, SweepDirection.CounterClockwise);
                context.EndFigure(true);
                return geometry;
            }
            // Half the gap on each side; a lone segment closes its gap as the rest of the ring empties.
            var p = Math.Min(gap / 2, (1 - turns) * Tau * outer / 2);
            var a0 = t0 * Tau - Math.PI / 2;
            var a1 = t1 * Tau - Math.PI / 2;
            var half = (a1 - a0) / 2;
            var s = Math.Sin(Math.Min(half, Math.PI / 2));
            if (s * outer <= p + 1e-6)
            {
                return null;
            }
            Point Side(double t, double angle, int sign)
                => new(c.X + t * Math.Cos(angle) - sign * p * Math.Sin(angle), c.Y + t * Math.Sin(angle) + sign * p * Math.Cos(angle));
            var band = (outer - inner) / 2;
            var ro = Math.Max(0, Math.Min(Math.Min(corner, band), s >= 0.9999 ? double.PositiveInfinity : (s * outer - p) / (1 + s)));
            var dO = Math.Asin(Math.Min(1, (p + ro) / (outer - ro)));
            var tO = Math.Sqrt(Math.Max(0, (outer - ro) * (outer - ro) - (p + ro) * (p + ro)));
            var bigO = a1 - a0 - 2 * dO > Math.PI;
            context.BeginFigure(Side(tO, a0, 1), true);
            if (ro > 0.01)
            {
                context.ArcTo(At(outer, a0 + dO), new Size(ro, ro), 0, false, SweepDirection.Clockwise);
            }
            context.ArcTo(At(outer, a1 - dO), new Size(outer, outer), 0, bigO, SweepDirection.Clockwise);
            if (ro > 0.01)
            {
                context.ArcTo(Side(tO, a1, -1), new Size(ro, ro), 0, false, SweepDirection.Clockwise);
            }
            if (s * inner > p + 1e-6 || s >= 0.9999)
            {
                var ri = Math.Max(0, Math.Min(Math.Min(corner, band), s >= 0.9999 ? double.PositiveInfinity : (s * inner - p) / (1 - s)));
                var dI = Math.Asin(Math.Min(1, (p + ri) / (inner + ri)));
                var tI = Math.Sqrt(Math.Max(0, (inner + ri) * (inner + ri) - (p + ri) * (p + ri)));
                var bigI = a1 - a0 - 2 * dI > Math.PI;
                context.LineTo(Side(tI, a1, -1));
                if (ri > 0.01)
                {
                    context.ArcTo(At(inner, a1 - dI), new Size(ri, ri), 0, false, SweepDirection.Clockwise);
                }
                context.ArcTo(At(inner, a0 + dI), new Size(inner, inner), 0, bigI, SweepDirection.CounterClockwise);
                if (ri > 0.01)
                {
                    context.ArcTo(Side(tI, a0, 1), new Size(ri, ri), 0, false, SweepDirection.Clockwise);
                }
            }
            else
            {
                context.LineTo(At(p / s, (a0 + a1) / 2));
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>Half the width, in pixels, of a sector's outer edge once the gap is cut (thin sectors fade instead of drawing hairlines).</summary>
    public static double SectorBreadth(double outer, double t0, double t1, double gap)
    {
        var turns = t1 - t0;
        if (turns >= 1 - 1e-6)
        {
            return double.PositiveInfinity;
        }
        return Math.Sin(Math.Min(turns * Math.PI, Math.PI / 2)) * outer - Math.Min(gap / 2, (1 - turns) * Tau * outer / 2);
    }
}
