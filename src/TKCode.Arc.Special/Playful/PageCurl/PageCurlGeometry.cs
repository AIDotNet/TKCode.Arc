using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The fold of a page being turned (<see cref="PageCurl"/>), in book coordinates: x runs from 0 to 2 page widths with
/// the spine at one page width, y from 0 to the page height. The fold is the perpendicular bisector between the
/// grabbed corner and the pointer, so the corner lands on the pointer when the flap is mirrored across it.
/// </summary>
internal readonly struct PageCurlFold
{
    public PageCurlFold(Point corner, Point pointer, Point mid, Vector normal)
    {
        Corner = corner;
        Pointer = pointer;
        Mid = mid;
        Normal = normal;
    }

    /// <summary>The grabbed corner of the sheet while it is flat.</summary>
    public Point Corner { get; }

    /// <summary>Where the corner is now (the pointer, held within a page width of the spine).</summary>
    public Point Pointer { get; }

    /// <summary>The middle of the corner and the pointer, a point on the fold line.</summary>
    public Point Mid { get; }

    /// <summary>Unit vector from the corner toward the pointer, perpendicular to the fold line.</summary>
    public Vector Normal { get; }

    /// <summary>False while the corner has not moved: there is no fold yet.</summary>
    public bool IsValid => Normal != default;

    /// <summary>Signed distance of <paramref name="p"/> from the fold line; negative on the corner's side, the flap.</summary>
    public double Side(Point p) => (p.X - Mid.X) * Normal.X + (p.Y - Mid.Y) * Normal.Y;
}

/// <summary>
/// The pure geometry of a page curl: fold line, reflection, polygon clipping and the spread layout. No UI types beyond
/// points, so it is testable on its own.
/// </summary>
internal static class PageCurlGeometry
{
    /// <summary>A rectangle clipped by one line has at most this many vertices.</summary>
    public const int MaxVertices = 8;

    /// <summary>Pointer movement below this many px makes no fold.</summary>
    public const double MinFold = 0.5;

    /// <summary>Number of spreads of a book of <paramref name="pageCount"/> pages: the closed cover, then one per two pages.</summary>
    public static int SpreadCount(int pageCount) => Math.Max(0, pageCount) / 2 + 1;

    /// <summary>Page number (one-based) on the left of <paramref name="spread"/>, or 0 when that side is empty.</summary>
    public static int LeftPage(int spread, int pageCount)
    {
        var page = 2 * spread;
        return page >= 1 && page <= pageCount ? page : 0;
    }

    /// <summary>Page number (one-based) on the right of <paramref name="spread"/>, or 0 when that side is empty.</summary>
    public static int RightPage(int spread, int pageCount)
    {
        var page = 2 * spread + 1;
        return page >= 1 && page <= pageCount ? page : 0;
    }

    /// <summary>
    /// Horizontal shift of the book for <paramref name="spread"/>: a lone cover is centred (half a page left), a lone
    /// back cover too (half a page right), two pages need none.
    /// </summary>
    public static double BookOffset(int spread, int pageCount, double pageWidth)
    {
        var left = LeftPage(spread, pageCount) != 0;
        var right = RightPage(spread, pageCount) != 0;
        if (left == right)
        {
            return 0;
        }
        return right ? -pageWidth / 2 : pageWidth / 2;
    }

    /// <summary>The corner a turn in <paramref name="direction"/> (+1 forward, -1 back) grabs.</summary>
    public static Point CornerOf(int direction, bool bottom, double pageWidth, double pageHeight)
        => new(direction > 0 ? 2 * pageWidth : 0, bottom ? pageHeight : 0);

    /// <summary>Where the corner rests once the page has turned: across the spine.</summary>
    public static Point FarCorner(Point corner, double pageWidth) => new(2 * pageWidth - corner.X, corner.Y);

    /// <summary>The corner is paper bound on one side: it stays within a page width of the spine.</summary>
    public static Point ClampPointer(Point corner, Point pointer, double pageWidth)
    {
        var dx = pointer.X - pageWidth;
        var dy = pointer.Y - corner.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        return length > pageWidth && length > 0
            ? new Point(pageWidth + dx * (pageWidth / length), corner.Y + dy * (pageWidth / length))
            : pointer;
    }

    /// <summary>The fold for a corner pulled to <paramref name="pointer"/> (clamped here).</summary>
    public static PageCurlFold Fold(Point corner, Point pointer, double pageWidth)
    {
        var p = ClampPointer(corner, pointer, pageWidth);
        var dx = p.X - corner.X;
        var dy = p.Y - corner.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < MinFold)
        {
            return new PageCurlFold(corner, corner, corner, default);
        }
        return new PageCurlFold(corner, p, new Point((corner.X + p.X) / 2, (corner.Y + p.Y) / 2), new Vector(dx / length, dy / length));
    }

    /// <summary>Mirrors <paramref name="p"/> across the fold line.</summary>
    public static Point Reflect(in PageCurlFold fold, Point p)
    {
        var s = fold.Side(p) * 2;
        return new Point(p.X - s * fold.Normal.X, p.Y - s * fold.Normal.Y);
    }

    /// <summary>Mirrors <paramref name="p"/> across the spine.</summary>
    public static Point MirrorSpine(Point p, double pageWidth) => new(2 * pageWidth - p.X, p.Y);

    /// <summary>How far the page has turned, 0 flat to 1 across the spine, for a turn in <paramref name="direction"/>.</summary>
    public static double Progress(int direction, Point corner, Point pointer, double pageWidth)
    {
        var travelled = direction > 0 ? corner.X - pointer.X : pointer.X - corner.X;
        return Math.Clamp(travelled / (2 * pageWidth), 0, 1);
    }

    /// <summary>
    /// Pulls the corner a small way toward the page: <paramref name="amount"/> is a fraction of the page width along the
    /// diagonal that leans toward the page's middle.
    /// </summary>
    public static Point PeekPoint(Point corner, int direction, bool bottom, double amount, double pageWidth)
    {
        var distance = amount * pageWidth;
        var x = -direction * 0.82 * distance;
        var y = (bottom ? -1 : 1) * 0.57 * distance;
        return new Point(corner.X + x, corner.Y + y);
    }

    /// <summary>
    /// The corner's path for a tap, key or button: straight across the book while lifted on a sine arc, so the page
    /// rises, passes over the spine and lays down.
    /// </summary>
    public static Point ArcPoint(Point corner, bool bottom, double t, double pageWidth, double pageHeight)
    {
        var end = FarCorner(corner, pageWidth);
        var lift = Math.Sin(Math.PI * Math.Clamp(t, 0, 1)) * 0.2 * pageHeight * (bottom ? -1 : 1);
        return new Point(corner.X + (end.X - corner.X) * t, corner.Y + (end.Y - corner.Y) * t + lift);
    }

    /// <summary>
    /// Clips <paramref name="rect"/> to the flap side (<paramref name="flap"/>, the corner's side of the fold) or the
    /// staying side. Writes the polygon to <paramref name="output"/> (at least <see cref="MaxVertices"/> long) and
    /// returns its vertex count (0 when empty).
    /// </summary>
    public static int ClipToSide(Rect rect, in PageCurlFold fold, bool flap, Span<Point> output)
    {
        Span<Point> corners =
        [
            rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft
        ];
        var sign = flap ? -1.0 : 1.0;
        var count = 0;
        for (var i = 0; i < corners.Length; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % corners.Length];
            var sa = sign * fold.Side(a);
            var sb = sign * fold.Side(b);
            if (sa >= 0)
            {
                output[count++] = a;
            }
            if ((sa > 0 && sb < 0) || (sa < 0 && sb > 0))
            {
                var t = sa / (sa - sb);
                output[count++] = new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            }
        }
        return count < 3 ? 0 : count;
    }

    /// <summary>
    /// The map from the back page's own coordinates (0 to page size) to book coordinates while it is the flap: the page
    /// is first laid where its back faces the sheet (across the spine from its resting place), then mirrored over the fold.
    /// </summary>
    public static Matrix FlapMatrix(in PageCurlFold fold, double pageWidth, Point restOrigin)
    {
        var o = Flap(fold, pageWidth, restOrigin, new Point(0, 0));
        var x = Flap(fold, pageWidth, restOrigin, new Point(1, 0));
        var y = Flap(fold, pageWidth, restOrigin, new Point(0, 1));
        return new Matrix(x.X - o.X, x.Y - o.Y, y.X - o.X, y.Y - o.Y, o.X, o.Y);
    }

    /// <summary>A point of the back page, in its own coordinates, as it is drawn on the flap.</summary>
    public static Point Flap(in PageCurlFold fold, double pageWidth, Point restOrigin, Point local)
        => Reflect(fold, MirrorSpine(new Point(local.X + restOrigin.X, local.Y + restOrigin.Y), pageWidth));

    /// <summary>The inverse of the spine mirror and rest origin: a sheet point's place in the back page's own coordinates.</summary>
    public static Point BackLocal(Point sheetPoint, double pageWidth, Point restOrigin)
    {
        var m = MirrorSpine(sheetPoint, pageWidth);
        return new Point(m.X - restOrigin.X, m.Y - restOrigin.Y);
    }
}
