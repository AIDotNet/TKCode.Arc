using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The terrain's faces as quads. A convex quadrilateral on the canvas is drawn as the unit square through the projective
/// matrix that maps the square onto it: Skia fills a rectangle under any matrix as an anti-aliased quad, with no path to
/// build, and the UI thread allocates no geometry per face (a path per face, rebuilt whenever the camera moved, cost a new
/// geometry each and several times the GPU time with its seam stroke). Faces are pushed out a little along their edges so
/// neighboring faces overlap and no anti-aliasing seam lets the floor through between them.
/// </summary>
internal static class ActivityTerrainQuad
{
    /// <summary>The rectangle every face draws, through its <see cref="TryUnitSquareTo"/> matrix.</summary>
    public static readonly Rect UnitSquare = new(0, 0, 1, 1);

    /// <summary>
    /// How far every face reaches past its edges (px). It is half the 0.6px stroke in the face's own color that closed the
    /// seams before, so faces and silhouettes keep the size they had.
    /// </summary>
    public const double SeamOutset = 0.3;

    /// <summary>
    /// A corner moves at most this many outsets: at an acute corner (a top face seen from low down) the mitered corner
    /// would otherwise poke far out of the silhouette.
    /// </summary>
    public const double MiterLimit = 2;

    /// <summary>
    /// Faces thinner than this (px: the area over the longest side) are not drawn. They are sides seen edge-on or pressed
    /// flat (a bar of zero height, every bar in the top view), and the faces drawn after them cover their outset anyway.
    /// </summary>
    public const double MinThickness = 0.05;

    /// <summary>The area of the quad a, b, c, d over its longest side: its width across, in px (0 when degenerate).</summary>
    public static double Thickness(Point a, Point b, Point c, Point d)
    {
        var area = Math.Abs(Cross(c - a, d - b)) / 2;
        var longest = Math.Max(Math.Max(Length(b - a), Length(c - b)), Math.Max(Length(d - c), Length(a - d)));
        return longest > 0 ? area / longest : 0;
    }

    /// <summary>
    /// Pushes every edge of the convex quad a, b, c, d out by <paramref name="distance"/>, each corner moving to where its
    /// two pushed edges meet (at most <see cref="MiterLimit"/> × the distance). An edge of zero length lends its corner
    /// the other edge's direction. False, and the corners untouched, when the quad is degenerate or would not stay convex.
    /// </summary>
    public static bool TryOutset(ref Point a, ref Point b, ref Point c, ref Point d, double distance)
    {
        var twiceArea = Cross(c - a, d - b);
        if (Math.Abs(twiceArea) < 1e-9)
        {
            return false;
        }
        // Outward normals: right of each edge for a positive (shoelace) orientation, left otherwise.
        var sign = Math.Sign(twiceArea);
        Span<Vector> normals = stackalloc Vector[4];
        Span<bool> valid = stackalloc bool[4];
        valid[0] = Normal(a, b, sign, out normals[0]);
        valid[1] = Normal(b, c, sign, out normals[1]);
        valid[2] = Normal(c, d, sign, out normals[2]);
        valid[3] = Normal(d, a, sign, out normals[3]);
        if (!Miter(normals, valid, 3, 0, out var ma) || !Miter(normals, valid, 0, 1, out var mb) ||
            !Miter(normals, valid, 1, 2, out var mc) || !Miter(normals, valid, 2, 3, out var md))
        {
            return false;
        }
        var oa = a + ma * distance;
        var ob = b + mb * distance;
        var oc = c + mc * distance;
        var od = d + md * distance;
        if (!IsConvex(oa, ob, oc, od, sign))
        {
            return false;
        }
        (a, b, c, d) = (oa, ob, oc, od);
        return true;
    }

    /// <summary>
    /// The projective matrix that maps the unit square onto the quad a, b, c, d: (0, 0) → a, (1, 0) → b, (1, 1) → c and
    /// (0, 1) → d. False when the quad is degenerate (three corners on a line).
    /// </summary>
    public static bool TryUnitSquareTo(Point a, Point b, Point c, Point d, out Matrix matrix)
    {
        double g = 0, h = 0;
        var sx = a.X - b.X + c.X - d.X;
        var sy = a.Y - b.Y + c.Y - d.Y;
        if (sx != 0 || sy != 0)
        {
            var dx1 = b.X - c.X;
            var dy1 = b.Y - c.Y;
            var dx2 = d.X - c.X;
            var dy2 = d.Y - c.Y;
            var den = dx1 * dy2 - dx2 * dy1;
            if (Math.Abs(den) < 1e-9)
            {
                matrix = default;
                return false;
            }
            g = (sx * dy2 - dx2 * sy) / den;
            h = (dx1 * sy - sx * dy1) / den;
        }
        // Row vectors: (u, v, 1) × M, divided by the third component.
        matrix = new Matrix(
            b.X - a.X + g * b.X, b.Y - a.Y + g * b.Y, g,
            d.X - a.X + h * d.X, d.Y - a.Y + h * d.Y, h,
            a.X, a.Y, 1);
        var area = Cross(b - a, d - a);
        return Math.Abs(area) > 1e-9 && matrix.HasInverse;
    }

    private static bool Normal(Point from, Point to, int sign, out Vector normal)
    {
        var edge = to - from;
        var length = Length(edge);
        if (length < 1e-9)
        {
            normal = default;
            return false;
        }
        normal = new Vector(edge.Y, -edge.X) * (sign / length);
        return true;
    }

    /// <summary>The corner's offset per unit of outset, from its incoming and outgoing edges' normals.</summary>
    private static bool Miter(ReadOnlySpan<Vector> normals, ReadOnlySpan<bool> valid, int incoming, int outgoing, out Vector miter)
    {
        if (valid[incoming] && valid[outgoing])
        {
            var n1 = normals[incoming];
            var n2 = normals[outgoing];
            var sum = n1 + n2;
            if (Length(sum) < 1e-9)
            {
                // The edges fold back on each other: there is no corner to push.
                miter = default;
                return false;
            }
            // A sliver's corner can be a degree or two: the miter is long and the limit below shortens it.
            miter = sum / (1 + n1.X * n2.X + n1.Y * n2.Y);
        }
        else if (valid[incoming] || valid[outgoing])
        {
            miter = valid[incoming] ? normals[incoming] : normals[outgoing];
        }
        else
        {
            miter = default;
            return false;
        }
        var length = Length(miter);
        if (length > MiterLimit)
        {
            miter *= MiterLimit / length;
        }
        return true;
    }

    private static bool IsConvex(Point a, Point b, Point c, Point d, int sign) =>
        Cross(b - a, c - b) * sign >= 0 && Cross(c - b, d - c) * sign >= 0 && Cross(d - c, a - d) * sign >= 0 && Cross(a - d, b - a) * sign >= 0;

    private static double Cross(Vector u, Vector v) => u.X * v.Y - u.Y * v.X;

    private static double Length(Vector v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
}
