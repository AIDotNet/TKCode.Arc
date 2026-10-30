using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The plane geometry of a peeling sticker: the die-cut outline, where a peel starts, how far the fold may lean and the
/// clipping that makes the flap. Pure functions on points, no UI.
/// </summary>
internal static class StickerPeelGeometry
{
    public const int CornerSegments = 8;
    public const int MaxOutlinePoints = 4 * (CornerSegments + 1);

    /// <summary>The fold may lean at most this far (35 degrees) from the peel direction, so it is always a short, clean corner.</summary>
    public static readonly double MaxLean = 35 * Math.PI / 180;

    public static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y;

    public static double Cross(Vector a, Vector b) => a.X * b.Y - a.Y * b.X;

    public static Vector Rotate(Vector v, double angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return new Vector(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    /// <summary>Corner radius of the die cut: half the smaller side for circles and pills.</summary>
    public static double CornerRadius(StickerPeelShape shape, double width, double height, double roundedRadius)
    {
        var half = Math.Min(width, height) / 2;
        return shape == StickerPeelShape.Rounded ? Math.Min(roundedRadius, half) : half;
    }

    /// <summary>Writes the outline of a rounded rectangle, clockwise, and returns the number of points.</summary>
    public static int BuildOutline(Span<Point> buffer, double left, double top, double right, double bottom, double r)
    {
        var n = 0;
        Corner(buffer, ref n, left + r, top + r, 180, r);
        Corner(buffer, ref n, right - r, top + r, 270, r);
        Corner(buffer, ref n, right - r, bottom - r, 0, r);
        Corner(buffer, ref n, left + r, bottom - r, 90, r);
        return n;
    }

    private static void Corner(Span<Point> buffer, ref int n, double cx, double cy, double startDegrees, double r)
    {
        for (var i = 0; i <= CornerSegments; i++)
        {
            var angle = (startDegrees + 90.0 * i / CornerSegments) * Math.PI / 180;
            var (sin, cos) = Math.SinCos(angle);
            buffer[n++] = new Point(cx + r * cos, cy + r * sin);
        }
    }

    /// <summary>
    /// Where a peel starts for a pointer at <paramref name="local"/> (sticker coordinates) and which way it runs into
    /// the sticker. Circles peel from the rim point nearest the pointer (<paramref name="quantize"/> snaps it to eight
    /// directions for the hover lift); other shapes from the nearest corner, along its diagonal.
    /// </summary>
    public static void Anchor(StickerPeelShape shape, double width, double height, double roundedRadius, Point local,
        bool quantize, out Point anchor, out Vector inward)
    {
        var r = CornerRadius(shape, width, height, roundedRadius);
        if (shape == StickerPeelShape.Circle)
        {
            var dx = local.X - width / 2;
            var dy = local.Y - height / 2;
            var angle = dx == 0 && dy == 0 ? -3 * Math.PI / 4 : Math.Atan2(dy, dx);
            if (quantize)
            {
                angle = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
            }
            var (sin, cos) = Math.SinCos(angle);
            anchor = new Point(width / 2 + r * cos, height / 2 + r * sin);
            inward = new Vector(-cos, -sin);
            return;
        }
        var sx = local.X < width / 2 ? -1 : 1;
        var sy = local.Y < height / 2 ? -1 : 1;
        var cx = sx < 0 ? r : width - r;
        var cy = sy < 0 ? r : height - r;
        var d = Math.Sqrt(0.5);
        anchor = new Point(cx + sx * r * d, cy + sy * r * d);
        inward = new Vector(-sx * d, -sy * d);
    }

    /// <summary>How far the outline reaches from <paramref name="anchor"/> along <paramref name="inward"/>.</summary>
    public static double Extent(ReadOnlySpan<Point> outline, Point anchor, Vector inward)
    {
        var best = 0.0;
        foreach (var p in outline)
        {
            best = Math.Max(best, Dot(new Vector(p.X - anchor.X, p.Y - anchor.Y), inward));
        }
        return best;
    }

    /// <summary>
    /// The fold vector for a pull: the pull projected onto a direction that leans at most <see cref="MaxLean"/> from
    /// <paramref name="inward"/>. Pulling outward gives no fold.
    /// </summary>
    public static Vector Constrain(Vector inward, Vector pull)
    {
        if (Dot(pull, inward) <= 0)
        {
            return default;
        }
        var angle = Math.Atan2(Cross(inward, pull), Dot(inward, pull));
        var dir = Rotate(inward, Math.Clamp(angle, -MaxLean, MaxLean));
        return dir * Math.Max(0, Dot(pull, dir));
    }

    /// <summary>
    /// Clips a convex polygon to one side of the line through <paramref name="origin"/> with <paramref name="normal"/>
    /// (Sutherland-Hodgman) and returns the number of points written. The output holds at most two more points than the input.
    /// </summary>
    public static int Clip(ReadOnlySpan<Point> input, Span<Point> output, Point origin, Vector normal, bool keepPositive)
    {
        if (input.Length == 0)
        {
            return 0;
        }
        var sign = keepPositive ? 1.0 : -1.0;
        var count = 0;
        var prev = input[^1];
        var prevD = sign * Dot(new Vector(prev.X - origin.X, prev.Y - origin.Y), normal);
        foreach (var cur in input)
        {
            var curD = sign * Dot(new Vector(cur.X - origin.X, cur.Y - origin.Y), normal);
            if (curD >= 0)
            {
                if (prevD < 0)
                {
                    output[count++] = Cut(prev, cur, prevD, curD);
                }
                output[count++] = cur;
            }
            else if (prevD >= 0)
            {
                output[count++] = Cut(prev, cur, prevD, curD);
            }
            prev = cur;
            prevD = curD;
        }
        return count;
    }

    private static Point Cut(Point a, Point b, double da, double db)
    {
        var t = da / (da - db);
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>Area of a simple polygon (absolute).</summary>
    public static double Area(ReadOnlySpan<Point> polygon)
    {
        var sum = 0.0;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Length];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }
}
