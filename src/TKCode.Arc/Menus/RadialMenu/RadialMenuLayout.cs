using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>One item's share of the ring: from <see cref="Start"/> clockwise by <see cref="Sweep"/> (degrees from 12 o'clock).</summary>
internal readonly record struct RadialMenuSlot(double Start, double Sweep)
{
    public double End => Start + Sweep;

    public double Mid => Start + Sweep / 2;
}

/// <summary>
/// The pure geometry of a <see cref="RadialMenu"/>: how the sweep is shared between items, which item an angle points at,
/// the shape of a sector (an annular wedge whose sides keep a constant gap and whose corners are rounded), and the extent
/// of the whole ring for edge avoidance. Angles are degrees measured clockwise from 12 o'clock; screen y grows downward.
/// </summary>
internal static class RadialMenuLayout
{
    private const double Epsilon = 1e-6;

    /// <summary>The sweep, limited to (0, 360].</summary>
    public static double ClampSweep(double sweep) => double.IsNaN(sweep) ? 360 : Math.Clamp(sweep, 1, 360);

    public static bool IsFullCircle(double sweep) => ClampSweep(sweep) >= 360 - Epsilon;

    /// <summary>
    /// Where the first slot begins. In a full circle the first item is centred on <paramref name="startAngle"/>; in a
    /// sector its leading edge sits on it.
    /// </summary>
    public static double FirstEdge(int count, double startAngle, double sweep)
    {
        sweep = ClampSweep(sweep);
        if (count <= 0)
        {
            return startAngle;
        }
        return IsFullCircle(sweep) ? startAngle - sweep / count / 2 : startAngle;
    }

    /// <summary>The slot of item <paramref name="index"/> among <paramref name="count"/> visible items.</summary>
    public static RadialMenuSlot Slot(int index, int count, double startAngle, double sweep)
    {
        sweep = ClampSweep(sweep);
        var step = sweep / Math.Max(count, 1);
        return new RadialMenuSlot(FirstEdge(count, startAngle, sweep) + index * step, step);
    }

    /// <summary>Normalizes an angle to [0, 360).</summary>
    public static double Normalize(double degrees)
    {
        var value = degrees % 360;
        return value < 0 ? value + 360 : value;
    }

    /// <summary>The direction of <paramref name="point"/> seen from <paramref name="center"/>, in degrees clockwise from 12 o'clock.</summary>
    public static double AngleOf(Point center, Point point)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return Normalize(Math.Atan2(dx, -dy) * 180 / Math.PI);
    }

    public static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The unit vector pointing at <paramref name="degrees"/>.</summary>
    public static Vector Direction(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Vector(Math.Sin(radians), -Math.Cos(radians));
    }

    /// <summary>
    /// The visible item an angle points at, or -1. Inside the sweep every angle belongs to a slot (the gaps are split
    /// between their neighbours). Outside a sector's sweep, <paramref name="tolerance"/> degrees past either end still
    /// count for the end item (the gesture is forgiving at the ends); anything further points at nothing.
    /// </summary>
    public static int IndexAt(double angle, int count, double startAngle, double sweep, double tolerance = 0)
    {
        if (count <= 0)
        {
            return -1;
        }
        sweep = ClampSweep(sweep);
        var first = FirstEdge(count, startAngle, sweep);
        var relative = Normalize(angle - first);
        var step = sweep / count;
        if (IsFullCircle(sweep))
        {
            return Math.Min(count - 1, (int)(relative / step));
        }
        if (relative < sweep)
        {
            return Math.Min(count - 1, (int)(relative / step));
        }
        // Past the end (clockwise) or before the start (counter-clockwise): the nearer end, within the tolerance.
        var pastEnd = relative - sweep;
        var beforeStart = 360 - relative;
        if (pastEnd <= beforeStart)
        {
            return pastEnd <= tolerance ? count - 1 : -1;
        }
        return beforeStart <= tolerance ? 0 : -1;
    }

    /// <summary>Whether <paramref name="angle"/> lies in the sweep (widened by <paramref name="margin"/> degrees on both ends).</summary>
    public static bool InSweep(double angle, double startAngle, double sweep, double margin)
    {
        sweep = ClampSweep(sweep);
        if (IsFullCircle(sweep))
        {
            return true;
        }
        var relative = Normalize(angle - startAngle + margin);
        return relative <= sweep + 2 * margin;
    }

    /// <summary>The point at the middle of a slot's band, where its content is centred.</summary>
    public static Point LabelPoint(Point center, RadialMenuSlot slot, double inner, double outer) =>
        center + Direction(slot.Mid) * ((inner + outer) / 2);

    /// <summary>
    /// The box the content of a slot can use. Across a sector at the top or bottom of the ring the content has the chord
    /// at mid radius; across a sector on the left or right it has the band's depth; sectors in between get a blend
    /// weighted by the angle. Both less the gap and padding. Height is the band's depth.
    /// </summary>
    public static Size ContentBox(RadialMenuSlot slot, double inner, double outer, double gap, double padding, bool wholeRing = false)
    {
        var mid = (inner + outer) / 2;
        var depth = Math.Max(0, outer - inner - 2 * padding);
        if (wholeRing || slot.Sweep >= 360 - Epsilon)
        {
            return new Size(Math.Max(0, 2 * inner - 2 * padding), depth);
        }
        var half = Math.Min(slot.Sweep, 180) / 2 * Math.PI / 180;
        var chord = slot.Sweep >= 180 ? 2 * mid : 2 * mid * Math.Sin(half);
        chord = Math.Max(0, chord - gap - 2 * padding);
        var across = Math.Sin(slot.Mid * Math.PI / 180);
        var weight = across * across;
        var width = chord + (Math.Max(depth, chord) - chord) * weight;
        return new Size(width, depth);
    }

    /// <summary>
    /// The shape of one sector around <paramref name="center"/>: the band between <paramref name="inner"/> and
    /// <paramref name="outer"/> across the slot, with sides moved <paramref name="sideOffset"/> into the slot (half the gap,
    /// so neighbouring sectors keep a constant gap; negative widens the shape, as the plate does) and corners rounded by
    /// up to <paramref name="corner"/>. A single item filling a full circle is a whole ring.
    /// </summary>
    public static StreamGeometry Sector(Point center, double inner, double outer, RadialMenuSlot slot, double sideOffset, double corner,
                                        bool wholeRing = false)
    {
        var geometry = new StreamGeometry();
        inner = Math.Max(0, inner);
        outer = Math.Max(inner + 1, outer);
        using var ctx = geometry.Open();
        if (wholeRing || slot.Sweep >= 360 - Epsilon)
        {
            ctx.SetFillRule(FillRule.EvenOdd);
            Circle(ctx, center, outer);
            if (inner > 0)
            {
                Circle(ctx, center, inner);
            }
            return geometry;
        }
        var d = sideOffset;
        var t1 = slot.Start * Math.PI / 180;
        var t2 = slot.End * Math.PI / 180;
        var u1 = new Vector(Math.Sin(t1), -Math.Cos(t1));
        var n1 = new Vector(Math.Cos(t1), Math.Sin(t1));
        var u2 = new Vector(Math.Sin(t2), -Math.Cos(t2));
        var n2 = new Vector(Math.Cos(t2), Math.Sin(t2));
        // The sides only reach the band when the radius clears the offset.
        if (outer <= Math.Abs(d) + 0.5)
        {
            return geometry;
        }
        var outerCorner = OuterCorner(outer, d, slot.Sweep, Math.Max(0, corner));
        var innerSpan = inner > Math.Abs(d) ? slot.Sweep * Math.PI / 180 - 2 * Math.Asin(d / inner) : -1;
        var innerCorner = innerSpan > 0 ? InnerCorner(inner, outer, d, slot.Sweep, Math.Max(0, corner), outerCorner) : 0;

        // Outer corner of side 1, along the outer arc, outer corner of side 2.
        var (o1Line, o1Circle) = CornerPoints(u1, n1, d, outer, outerCorner, isOuter: true, side: 1);
        var (o2Line, o2Circle) = CornerPoints(u2, n2, d, outer, outerCorner, isOuter: true, side: 2);
        ctx.BeginFigure(center + o1Line, true);
        if (outerCorner > 0)
        {
            ctx.ArcTo(center + o1Circle, new Size(outerCorner, outerCorner), 0, false, SweepDirection.Clockwise);
        }
        var outerSpan = SpanDegrees(o1Circle, o2Circle);
        ctx.ArcTo(center + o2Circle, new Size(outer, outer), 0, outerSpan > 180, SweepDirection.Clockwise);
        if (outerCorner > 0)
        {
            ctx.ArcTo(center + o2Line, new Size(outerCorner, outerCorner), 0, false, SweepDirection.Clockwise);
        }

        if (innerSpan > 0)
        {
            var (i2Line, i2Circle) = CornerPoints(u2, n2, d, inner, innerCorner, isOuter: false, side: 2);
            var (i1Line, i1Circle) = CornerPoints(u1, n1, d, inner, innerCorner, isOuter: false, side: 1);
            ctx.LineTo(center + i2Line);
            if (innerCorner > 0)
            {
                ctx.ArcTo(center + i2Circle, new Size(innerCorner, innerCorner), 0, false, SweepDirection.Clockwise);
            }
            var span = SpanDegrees(i1Circle, i2Circle);
            ctx.ArcTo(center + i1Circle, new Size(inner, inner), 0, span > 180, SweepDirection.CounterClockwise);
            if (innerCorner > 0)
            {
                ctx.ArcTo(center + i1Line, new Size(innerCorner, innerCorner), 0, false, SweepDirection.Clockwise);
            }
        }
        else
        {
            // The slot is too narrow for the gap at the inner radius: the sides meet in a point on the bisector.
            var half = slot.Sweep / 2 * Math.PI / 180;
            var apex = Direction(slot.Mid) * (d / Math.Max(Math.Sin(half), Epsilon));
            ctx.LineTo(center + apex);
        }
        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>
    /// The extent of the ring around its centre, for edge avoidance: the plate of a full circle, or the plate of a
    /// sector's band plus the centre disc.
    /// </summary>
    public static Rect Extent(double inner, double outer, double plate, double startAngle, double sweep)
    {
        var radius = outer + plate;
        if (IsFullCircle(sweep))
        {
            return new Rect(-radius, -radius, 2 * radius, 2 * radius);
        }
        sweep = ClampSweep(sweep);
        // The centre disc (no wider than the inner radius), the band's rim at every cardinal direction it spans, and the
        // four corners of the band, which the plate pushes out past the end angles by its padding.
        var disc = Math.Max(0, inner);
        var left = -disc;
        var top = -disc;
        var right = disc;
        var bottom = disc;
        void Include(Vector v)
        {
            left = Math.Min(left, v.X);
            top = Math.Min(top, v.Y);
            right = Math.Max(right, v.X);
            bottom = Math.Max(bottom, v.Y);
        }
        var bandInner = Math.Max(0, inner - plate);
        var end = startAngle + sweep;
        var startNormal = new Vector(Math.Cos(startAngle * Math.PI / 180), Math.Sin(startAngle * Math.PI / 180));
        var endNormal = new Vector(Math.Cos(end * Math.PI / 180), Math.Sin(end * Math.PI / 180));
        Include(Direction(startAngle) * radius - startNormal * plate);
        Include(Direction(startAngle) * bandInner - startNormal * plate);
        Include(Direction(end) * radius + endNormal * plate);
        Include(Direction(end) * bandInner + endNormal * plate);
        for (var cardinal = 0; cardinal < 360; cardinal += 90)
        {
            if (Normalize(cardinal - startAngle) <= sweep)
            {
                Include(Direction(cardinal) * radius);
            }
        }
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// The offset that moves <paramref name="extent"/> (centred on <paramref name="center"/>) inside
    /// <paramref name="bounds"/>; when it cannot fit, it is centred on that axis.
    /// </summary>
    public static Vector Shift(Point center, Rect extent, Rect bounds)
    {
        var box = extent.Translate((Vector)center);
        return new Vector(ShiftAxis(box.Left, box.Right, bounds.Left, bounds.Right), ShiftAxis(box.Top, box.Bottom, bounds.Top, bounds.Bottom));
    }

    private static double ShiftAxis(double start, double end, double min, double max)
    {
        if (end - start > max - min)
        {
            return (min + max) / 2 - (start + end) / 2;
        }
        if (start < min)
        {
            return min - start;
        }
        if (end > max)
        {
            return max - end;
        }
        return 0;
    }

    private static void Circle(StreamGeometryContext ctx, Point center, double radius)
    {
        ctx.BeginFigure(new Point(center.X, center.Y - radius), true);
        ctx.ArcTo(new Point(center.X, center.Y + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        ctx.ArcTo(new Point(center.X, center.Y - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(true);
    }

    /// <summary>
    /// The tangent points of a rounded corner between a side (offset by <paramref name="d"/> from the slot edge) and the
    /// circle of radius <paramref name="radius"/>: where the rounding leaves the side and where it meets the circle.
    /// </summary>
    private static (Vector Line, Vector Circle) CornerPoints(Vector u, Vector n, double d, double radius, double rc, bool isOuter, int side)
    {
        // Side 1 is offset clockwise (into the slot), side 2 counter-clockwise.
        var normal = side == 1 ? n : -n;
        if (rc <= 0)
        {
            var along = Math.Sqrt(Math.Max(0, radius * radius - d * d));
            var point = u * along + normal * d;
            return (point, point);
        }
        var centerRadius = isOuter ? radius - rc : radius + rc;
        var a = Math.Sqrt(Math.Max(0, centerRadius * centerRadius - (d + rc) * (d + rc)));
        var filletCenter = u * a + normal * (d + rc);
        var line = u * a + normal * d;
        var circle = filletCenter * (radius / centerRadius);
        return (line, circle);
    }

    private static double SpanDegrees(Vector from, Vector to)
    {
        var a = Math.Atan2(from.X, -from.Y) * 180 / Math.PI;
        var b = Math.Atan2(to.X, -to.Y) * 180 / Math.PI;
        return Normalize(b - a);
    }

    private static double OuterCorner(double outer, double d, double sweep, double corner)
    {
        for (var rc = corner; rc > 0.25; rc *= 0.7)
        {
            if (outer - rc <= d + rc)
            {
                continue;
            }
            var (_, c1) = CornerPointsFor(1, outer, d, rc, true);
            var (_, c2) = CornerPointsFor(2, outer, d, rc, true, sweep);
            if (FrameAngle(c1) < FrameAngle(c2) && FrameAngle(c2) <= sweep)
            {
                return rc;
            }
        }
        return 0;
    }

    private static double InnerCorner(double inner, double outer, double d, double sweep, double corner, double outerCorner)
    {
        // The two inner roundings must not cross, and the straight part of each side must stay non-negative.
        var maxByBand = (outer - inner) / 2 - 0.5;
        for (var rc = Math.Min(corner, maxByBand); rc > 0.25; rc *= 0.7)
        {
            if (inner + rc <= d + rc)
            {
                continue;
            }
            var (l1, c1) = CornerPointsFor(1, inner, d, rc, false);
            var (_, c2) = CornerPointsFor(2, inner, d, rc, false, sweep);
            if (FrameAngle(c1) >= FrameAngle(c2) || FrameAngle(c2) > sweep)
            {
                continue;
            }
            var (o1, _) = CornerPointsFor(1, outer, d, outerCorner, true);
            if (l1.Length > o1.Length)
            {
                continue;
            }
            return rc;
        }
        return 0;
    }

    /// <summary>The angle of a corner point in the frame of <see cref="CornerPointsFor"/>, in [0, 360).</summary>
    private static double FrameAngle(Vector v) => Normalize(Math.Atan2(v.X, -v.Y) * 180 / Math.PI);

    /// <summary>Corner points in a frame where side 1 points straight up (so angles compare without wrapping).</summary>
    private static (Vector Line, Vector Circle) CornerPointsFor(int side, double radius, double d, double rc, bool isOuter, double sweep = 0)
    {
        var t = side == 1 ? 0 : sweep * Math.PI / 180;
        var u = new Vector(Math.Sin(t), -Math.Cos(t));
        var n = new Vector(Math.Cos(t), Math.Sin(t));
        return CornerPoints(u, n, d, radius, rc, isOuter, side);
    }
}
