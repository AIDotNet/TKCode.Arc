using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Triangulates polygons with holes by ear clipping (the algorithm of Mapbox's earcut: holes are bridged
/// into the outline, ears are clipped, and what resists is cured by removing local intersections and finally by
/// splitting the polygon). Accepts either winding, collinear and repeated points, and touching outlines.</summary>
public static class ShapeTriangulator
{
    /// <summary>Twice the signed area of a polygon: positive when counter-clockwise (Y up).</summary>
    public static float SignedArea(ReadOnlySpan<Vector2> contour)
    {
        var sum = 0.0;
        for (int i = 0, j = contour.Length - 1; i < contour.Length; j = i++)
        {
            sum += ((double)contour[j].X * contour[i].Y) - ((double)contour[i].X * contour[j].Y);
        }

        return (float)(sum * 0.5);
    }

    /// <summary>True when the polygon runs clockwise (Y up).</summary>
    public static bool IsClockwise(ReadOnlySpan<Vector2> contour) => SignedArea(contour) < 0f;

    /// <summary>Triangulates an outline with optional holes. The result indexes into the outline's points followed by
    /// each hole's points, three indices per triangle, counter-clockwise (Y up).</summary>
    public static int[] Triangulate(ReadOnlySpan<Vector2> contour, IReadOnlyList<Vector2[]>? holes = null)
    {
        var triangles = new List<int>(Math.Max(0, (contour.Length - 2) * 3));
        var outer = LinkedList(contour, 0, true);
        if (outer is null || ReferenceEquals(outer.Next, outer.Prev))
        {
            return [];
        }

        if (holes is { Count: > 0 })
        {
            var queue = new List<Node>(holes.Count);
            var offset = contour.Length;
            foreach (var hole in holes)
            {
                var list = LinkedList(hole, offset, false);
                offset += hole.Length;
                if (list is null)
                {
                    continue;
                }

                if (ReferenceEquals(list, list.Next))
                {
                    list.Steiner = true;
                }

                queue.Add(Leftmost(list));
            }

            queue.Sort(static (a, b) => a.X.CompareTo(b.X));
            foreach (var hole in queue)
            {
                outer = EliminateHole(hole, outer);
            }
        }

        Clip(outer, triangles, 0);
        return [.. triangles];
    }

    private sealed class Node
    {
        public Node(int index, double x, double y)
        {
            Index = index;
            X = x;
            Y = y;
            Prev = this;
            Next = this;
        }

        public readonly int Index;
        public readonly double X;
        public readonly double Y;
        public Node Prev;
        public Node Next;
        public bool Steiner;
    }

    /// <summary>A circular list of a ring's points, counter-clockwise for outlines and clockwise for holes.</summary>
    private static Node? LinkedList(ReadOnlySpan<Vector2> points, int indexOffset, bool counterClockwise)
    {
        Node? last = null;
        var area = 0.0;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            area += ((double)points[j].X * points[i].Y) - ((double)points[i].X * points[j].Y);
        }

        if (counterClockwise == area > 0.0)
        {
            for (var i = 0; i < points.Length; i++)
            {
                last = Insert(indexOffset + i, points[i].X, points[i].Y, last);
            }
        }
        else
        {
            for (var i = points.Length - 1; i >= 0; i--)
            {
                last = Insert(indexOffset + i, points[i].X, points[i].Y, last);
            }
        }

        if (last is not null && Same(last, last.Next))
        {
            Remove(last);
            last = last.Next;
        }

        return last;
    }

    private static Node Insert(int index, double x, double y, Node? last)
    {
        var p = new Node(index, x, y);
        if (last is not null)
        {
            p.Next = last.Next;
            p.Prev = last;
            last.Next.Prev = p;
            last.Next = p;
        }

        return p;
    }

    private static void Remove(Node p)
    {
        p.Next.Prev = p.Prev;
        p.Prev.Next = p.Next;
    }

    /// <summary>Removes repeated and collinear points.</summary>
    private static Node FilterPoints(Node start, Node? end = null)
    {
        end ??= start;
        var p = start;
        bool again;
        do
        {
            again = false;
            if (!p.Steiner && (Same(p, p.Next) || Area(p.Prev, p, p.Next) == 0.0))
            {
                Remove(p);
                p = end = p.Prev;
                if (ReferenceEquals(p, p.Next))
                {
                    break;
                }

                again = true;
            }
            else
            {
                p = p.Next;
            }
        }
        while (again || !ReferenceEquals(p, end));
        return end;
    }

    private static void Clip(Node? ear, List<int> triangles, int pass)
    {
        if (ear is null)
        {
            return;
        }

        var stop = ear;
        while (!ReferenceEquals(ear.Prev, ear.Next))
        {
            var prev = ear.Prev;
            var next = ear.Next;
            if (IsEar(ear))
            {
                triangles.Add(prev.Index);
                triangles.Add(ear.Index);
                triangles.Add(next.Index);
                Remove(ear);
                ear = next.Next;
                stop = next.Next;
                continue;
            }

            ear = next;
            if (ReferenceEquals(ear, stop))
            {
                // No ear left: clean up and try again, then cut crossings, then split in two.
                if (pass == 0)
                {
                    Clip(FilterPoints(ear), triangles, 1);
                }
                else if (pass == 1)
                {
                    ear = CureLocalIntersections(FilterPoints(ear), triangles);
                    Clip(ear, triangles, 2);
                }
                else if (pass == 2)
                {
                    Split(ear, triangles);
                }

                break;
            }
        }
    }

    private static bool IsEar(Node ear)
    {
        var a = ear.Prev;
        var b = ear;
        var c = ear.Next;
        if (Area(a, b, c) >= 0.0)
        {
            return false;
        }

        var p = c.Next;
        while (!ReferenceEquals(p, a))
        {
            if (PointInTriangle(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0.0)
            {
                return false;
            }

            p = p.Next;
        }

        return true;
    }

    private static Node CureLocalIntersections(Node start, List<int> triangles)
    {
        var p = start;
        do
        {
            var a = p.Prev;
            var b = p.Next.Next;
            if (!Same(a, b) && Intersects(a, p, p.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
            {
                triangles.Add(a.Index);
                triangles.Add(p.Index);
                triangles.Add(b.Index);
                Remove(p);
                Remove(p.Next);
                p = start = b;
            }

            p = p.Next;
        }
        while (!ReferenceEquals(p, start));
        return FilterPoints(p);
    }

    private static void Split(Node start, List<int> triangles)
    {
        var a = start;
        do
        {
            var b = a.Next.Next;
            while (!ReferenceEquals(b, a.Prev))
            {
                if (a.Index != b.Index && IsValidDiagonal(a, b))
                {
                    var c = SplitPolygon(a, b);
                    a = FilterPoints(a, a.Next);
                    c = FilterPoints(c, c.Next);
                    Clip(a, triangles, 0);
                    Clip(c, triangles, 0);
                    return;
                }

                b = b.Next;
            }

            a = a.Next;
        }
        while (!ReferenceEquals(a, start));
    }

    private static Node EliminateHole(Node hole, Node outer)
    {
        var bridge = FindHoleBridge(hole, outer);
        if (bridge is null)
        {
            return outer;
        }

        var bridgeReverse = SplitPolygon(bridge, hole);
        FilterPoints(bridgeReverse, bridgeReverse.Next);
        return FilterPoints(bridge, bridge.Next);
    }

    /// <summary>Finds the outline point to connect a hole's leftmost point to: the nearest edge hit by a ray to the
    /// left, then the visible point with the smallest angle to that ray.</summary>
    private static Node? FindHoleBridge(Node hole, Node outer)
    {
        var p = outer;
        var hx = hole.X;
        var hy = hole.Y;
        var qx = double.NegativeInfinity;
        Node? m = null;
        do
        {
            if (hy <= p.Y && hy >= p.Next.Y && p.Next.Y != p.Y)
            {
                var x = p.X + ((hy - p.Y) * (p.Next.X - p.X) / (p.Next.Y - p.Y));
                if (x <= hx && x > qx)
                {
                    qx = x;
                    m = p.X < p.Next.X ? p : p.Next;
                    if (x == hx)
                    {
                        return m;
                    }
                }
            }

            p = p.Next;
        }
        while (!ReferenceEquals(p, outer));
        if (m is null)
        {
            return null;
        }

        var stop = m;
        var mx = m.X;
        var my = m.Y;
        var tanMin = double.PositiveInfinity;
        p = m;
        do
        {
            if (hx >= p.X && p.X >= mx && hx != p.X
                && PointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.X, p.Y))
            {
                var tan = Math.Abs(hy - p.Y) / (hx - p.X);
                if (LocallyInside(p, hole)
                    && (tan < tanMin || (tan == tanMin && (p.X > m.X || (p.X == m.X && SectorContainsSector(m, p))))))
                {
                    m = p;
                    tanMin = tan;
                }
            }

            p = p.Next;
        }
        while (!ReferenceEquals(p, stop));
        return m;
    }

    private static bool SectorContainsSector(Node m, Node p) => Area(m.Prev, m, p.Prev) < 0.0 && Area(p.Next, m, m.Next) < 0.0;

    private static Node Leftmost(Node start)
    {
        var p = start;
        var leftmost = start;
        do
        {
            if (p.X < leftmost.X || (p.X == leftmost.X && p.Y < leftmost.Y))
            {
                leftmost = p;
            }

            p = p.Next;
        }
        while (!ReferenceEquals(p, start));
        return leftmost;
    }

    private static bool PointInTriangle(double ax, double ay, double bx, double by, double cx, double cy, double px, double py) =>
        ((cx - px) * (ay - py)) >= ((ax - px) * (cy - py))
        && ((ax - px) * (by - py)) >= ((bx - px) * (ay - py))
        && ((bx - px) * (cy - py)) >= ((cx - px) * (by - py));

    private static bool IsValidDiagonal(Node a, Node b) =>
        a.Next.Index != b.Index && a.Prev.Index != b.Index && !IntersectsPolygon(a, b)
        && ((LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b) && (Area(a.Prev, a, b.Prev) != 0.0 || Area(a, b.Prev, b) != 0.0))
            || (Same(a, b) && Area(a.Prev, a, a.Next) > 0.0 && Area(b.Prev, b, b.Next) > 0.0));

    /// <summary>Negative when p, q, r turn counter-clockwise (Y up).</summary>
    private static double Area(Node p, Node q, Node r) => ((q.Y - p.Y) * (r.X - q.X)) - ((q.X - p.X) * (r.Y - q.Y));

    private static bool Same(Node a, Node b) => a.X == b.X && a.Y == b.Y;

    private static bool Intersects(Node p1, Node q1, Node p2, Node q2)
    {
        var o1 = Math.Sign(Area(p1, q1, p2));
        var o2 = Math.Sign(Area(p1, q1, q2));
        var o3 = Math.Sign(Area(p2, q2, p1));
        var o4 = Math.Sign(Area(p2, q2, q1));
        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        return (o1 == 0 && OnSegment(p1, p2, q1)) || (o2 == 0 && OnSegment(p1, q2, q1))
            || (o3 == 0 && OnSegment(p2, p1, q2)) || (o4 == 0 && OnSegment(p2, q1, q2));
    }

    private static bool OnSegment(Node p, Node q, Node r) =>
        q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) && q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);

    private static bool IntersectsPolygon(Node a, Node b)
    {
        var p = a;
        do
        {
            if (p.Index != a.Index && p.Next.Index != a.Index && p.Index != b.Index && p.Next.Index != b.Index && Intersects(p, p.Next, a, b))
            {
                return true;
            }

            p = p.Next;
        }
        while (!ReferenceEquals(p, a));
        return false;
    }

    private static bool LocallyInside(Node a, Node b) => Area(a.Prev, a, a.Next) < 0.0
        ? Area(a, b, a.Next) >= 0.0 && Area(a, a.Prev, b) >= 0.0
        : Area(a, b, a.Prev) < 0.0 || Area(a, a.Next, b) < 0.0;

    private static bool MiddleInside(Node a, Node b)
    {
        var p = a;
        var inside = false;
        var px = (a.X + b.X) / 2.0;
        var py = (a.Y + b.Y) / 2.0;
        do
        {
            if ((p.Y > py) != (p.Next.Y > py) && p.Next.Y != p.Y && px < ((p.Next.X - p.X) * (py - p.Y) / (p.Next.Y - p.Y)) + p.X)
            {
                inside = !inside;
            }

            p = p.Next;
        }
        while (!ReferenceEquals(p, a));
        return inside;
    }

    /// <summary>Links two points of one ring (splitting it in two) or of two rings (joining them through a bridge).</summary>
    private static Node SplitPolygon(Node a, Node b)
    {
        var a2 = new Node(a.Index, a.X, a.Y);
        var b2 = new Node(b.Index, b.X, b.Y);
        var an = a.Next;
        var bp = b.Prev;
        a.Next = b;
        b.Prev = a;
        a2.Next = an;
        an.Prev = a2;
        b2.Next = a2;
        a2.Prev = b2;
        bp.Next = b2;
        b2.Prev = bp;
        return b2;
    }
}
