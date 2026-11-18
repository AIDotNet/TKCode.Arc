using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>Reads the triangles of a geometry without allocating: float positions and 16/32-bit indices take a direct
/// path, anything else goes through <see cref="VertexAttribute.GetVector3"/>. Positions can be replaced by an array of
/// deformed vertices (skinning, morph targets).</summary>
internal readonly struct TriangleSource
{
    private readonly float[]? _floats;
    private readonly int _itemSize;
    private readonly VertexAttribute? _position;
    private readonly Vector3[]? _override;
    private readonly ushort[]? _index16;
    private readonly uint[]? _index32;

    public TriangleSource(BufferGeometry geometry, Vector3[]? positions = null)
    {
        _position = geometry.GetAttribute(BufferGeometry.Position);
        _override = positions;
        if (_position is { ItemSize: >= 3, Data: float[] f })
        {
            _floats = f;
            _itemSize = _position.ItemSize;
        }
        else
        {
            _floats = null;
            _itemSize = 0;
        }

        _index16 = geometry.Index as ushort[];
        _index32 = geometry.Index as uint[];
        ElementCount = _position is null ? 0 : geometry.ElementCount;
        VertexCount = _position?.Count ?? 0;
    }

    /// <summary>Indices, or vertices when the geometry has no index.</summary>
    public int ElementCount { get; }

    public int VertexCount { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Index(int element)
    {
        if (_index16 is not null)
        {
            return _index16[element];
        }

        return _index32 is not null ? (int)_index32[element] : element;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Vertex(int vertex)
    {
        if (_override is not null)
        {
            return _override[vertex];
        }

        if (_floats is not null)
        {
            var o = vertex * _itemSize;
            return new Vector3(_floats[o], _floats[o + 1], _floats[o + 2]);
        }

        return _position!.GetVector3(vertex);
    }

    /// <summary>The corners of the triangle that starts at an element.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Triangle(int element, out Vector3 a, out Vector3 b, out Vector3 c)
    {
        a = Vertex(Index(element));
        b = Vertex(Index(element + 1));
        c = Vertex(Index(element + 2));
    }
}

/// <summary>A bounding volume hierarchy over the triangles of a geometry, for fast ray queries in the geometry's local
/// space. <see cref="Raycaster"/> builds and caches one per large geometry; it is rebuilt when positions, the index or
/// the attribute set change.</summary>
public sealed class MeshBvh
{
    /// <summary>Geometries with fewer triangles are tested directly: a hierarchy would not pay off.</summary>
    public const int MinimumTriangles = 96;

    private const int MaxLeafSize = 4;
    private const int Bins = 16;
    private const int MedianDepth = 44;
    private const int StackSize = 128;

    private readonly BufferGeometry _geometry;
    private readonly Node[] _nodes;
    private readonly int[] _triangles;
    private readonly long _stamp;

    [StructLayout(LayoutKind.Sequential)]
    private struct Node
    {
        public Vector3 Min;

        /// <summary>Leaf: first entry in the triangle list. Interior: index of the left child (the right one follows).</summary>
        public int LeftFirst;
        public Vector3 Max;

        /// <summary>Number of triangles of a leaf; 0 for interior nodes.</summary>
        public int Count;
    }

    private MeshBvh(BufferGeometry geometry, Node[] nodes, int nodeCount, int[] triangles, long stamp)
    {
        _geometry = geometry;
        _nodes = nodes;
        NodeCount = nodeCount;
        _triangles = triangles;
        _stamp = stamp;
    }

    /// <summary>Number of triangles in the hierarchy.</summary>
    public int TriangleCount => _triangles.Length;

    /// <summary>Number of nodes.</summary>
    public int NodeCount { get; }

    /// <summary>The box around all triangles, in local space.</summary>
    public BoundingBox Bounds => NodeCount == 0 ? BoundingBox.Empty : new BoundingBox(_nodes[0].Min, _nodes[0].Max);

    private static long StampOf(BufferGeometry geometry)
    {
        var position = geometry.GetAttribute(BufferGeometry.Position);
        return ((long)geometry.StructureVersion << 42) ^ ((long)geometry.IndexVersion << 21) ^ (uint)(position?.Version ?? 0);
    }

    /// <summary>The hierarchy cached on a geometry, built or rebuilt when needed. Returns null for geometries that are
    /// not triangle lists or have no triangles.</summary>
    public static MeshBvh? Get(BufferGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var stamp = StampOf(geometry);
        if (geometry.RaycastCache is MeshBvh cached && cached._stamp == stamp)
        {
            return cached;
        }

        var built = BuildCore(geometry, stamp);
        geometry.RaycastCache = built;
        geometry.RaycastCacheStamp = stamp;
        return built;
    }

    /// <summary>Builds a hierarchy without caching it. Returns null for geometries that are not triangle lists or have
    /// no triangles.</summary>
    public static MeshBvh? Build(BufferGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return BuildCore(geometry, StampOf(geometry));
    }

    private static MeshBvh? BuildCore(BufferGeometry geometry, long stamp)
    {
        if (geometry.Topology != PrimitiveTopology.Triangles)
        {
            return null;
        }

        var source = new TriangleSource(geometry);
        var n = source.ElementCount / 3;
        if (n == 0)
        {
            return null;
        }

        var triangles = new int[n];
        var boxMin = new Vector3[n];
        var boxMax = new Vector3[n];
        var centroid = new Vector3[n];
        for (var i = 0; i < n; i++)
        {
            triangles[i] = i;
            source.Triangle(i * 3, out var a, out var b, out var c);
            var min = Vector3.Min(a, Vector3.Min(b, c));
            var max = Vector3.Max(a, Vector3.Max(b, c));
            boxMin[i] = min;
            boxMax[i] = max;
            centroid[i] = (min + max) * 0.5f;
        }

        var nodes = new Node[Math.Max(16, (n / 2) + 16)];
        var nodeCount = 1;
        var stack = new List<(int Node, int First, int Count, int Depth)>(64) { (0, 0, n, 0) };
        Span<int> binCount = stackalloc int[Bins];
        Span<Vector3> binMin = stackalloc Vector3[Bins];
        Span<Vector3> binMax = stackalloc Vector3[Bins];
        Span<float> leftCost = stackalloc float[Bins];
        var infinity = new Vector3(float.PositiveInfinity);
        while (stack.Count > 0)
        {
            var (nodeIndex, first, count, depth) = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            Vector3 min = infinity, max = -infinity, cMin = infinity, cMax = -infinity;
            for (var i = first; i < first + count; i++)
            {
                var t = triangles[i];
                min = Vector3.Min(min, boxMin[t]);
                max = Vector3.Max(max, boxMax[t]);
                cMin = Vector3.Min(cMin, centroid[t]);
                cMax = Vector3.Max(cMax, centroid[t]);
            }

            ref var node = ref nodes[nodeIndex];
            node.Min = min;
            node.Max = max;
            if (count <= MaxLeafSize)
            {
                node.LeftFirst = first;
                node.Count = count;
                continue;
            }

            var extent = cMax - cMin;
            var axis = extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;
            var axisExtent = axis == 0 ? extent.X : axis == 1 ? extent.Y : extent.Z;
            var axisMin = axis == 0 ? cMin.X : axis == 1 ? cMin.Y : cMin.Z;
            var leftCount = 0;
            if (axisExtent > 0f && depth < MedianDepth && !float.IsInfinity(axisExtent))
            {
                // Binned surface area heuristic along the widest axis of the centroids.
                binCount.Clear();
                for (var b = 0; b < Bins; b++)
                {
                    binMin[b] = infinity;
                    binMax[b] = -infinity;
                }

                var scale = Bins / axisExtent;
                for (var i = first; i < first + count; i++)
                {
                    var t = triangles[i];
                    var c = centroid[t];
                    var b = Math.Min(Bins - 1, (int)(((axis == 0 ? c.X : axis == 1 ? c.Y : c.Z) - axisMin) * scale));
                    binCount[b]++;
                    binMin[b] = Vector3.Min(binMin[b], boxMin[t]);
                    binMax[b] = Vector3.Max(binMax[b], boxMax[t]);
                }

                Vector3 accMin = infinity, accMax = -infinity;
                var accCount = 0;
                for (var b = 0; b < Bins - 1; b++)
                {
                    accCount += binCount[b];
                    accMin = Vector3.Min(accMin, binMin[b]);
                    accMax = Vector3.Max(accMax, binMax[b]);
                    leftCost[b] = accCount == 0 ? 0f : accCount * HalfArea(accMin, accMax);
                }

                accMin = infinity;
                accMax = -infinity;
                accCount = 0;
                var bestCost = float.PositiveInfinity;
                var bestSplit = -1;
                for (var b = Bins - 1; b > 0; b--)
                {
                    accCount += binCount[b];
                    accMin = Vector3.Min(accMin, binMin[b]);
                    accMax = Vector3.Max(accMax, binMax[b]);
                    var cost = leftCost[b - 1] + (accCount == 0 ? 0f : accCount * HalfArea(accMin, accMax));
                    if (accCount > 0 && accCount < count && cost < bestCost)
                    {
                        bestCost = cost;
                        bestSplit = b;
                    }
                }

                if (bestSplit > 0)
                {
                    int lo = first, hi = first + count - 1;
                    while (lo <= hi)
                    {
                        var c = centroid[triangles[lo]];
                        var b = Math.Min(Bins - 1, (int)(((axis == 0 ? c.X : axis == 1 ? c.Y : c.Z) - axisMin) * scale));
                        if (b < bestSplit)
                        {
                            lo++;
                        }
                        else
                        {
                            (triangles[lo], triangles[hi]) = (triangles[hi], triangles[lo]);
                            hi--;
                        }
                    }

                    leftCount = lo - first;
                }
            }

            if (leftCount <= 0 || leftCount >= count)
            {
                // Coincident centroids or a very deep branch: halve the list so the depth stays bounded.
                leftCount = count / 2;
            }

            if (nodeCount + 2 > nodes.Length)
            {
                Array.Resize(ref nodes, nodes.Length * 2);
            }

            var left = nodeCount;
            nodeCount += 2;
            nodes[nodeIndex].LeftFirst = left;
            nodes[nodeIndex].Count = 0;
            stack.Add((left + 1, first + leftCount, count - leftCount, depth + 1));
            stack.Add((left, first, leftCount, depth + 1));
        }

        return new MeshBvh(geometry, nodes, nodeCount, triangles, stamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float HalfArea(Vector3 min, Vector3 max)
    {
        var s = max - min;
        return (s.X * s.Y) + (s.Y * s.Z) + (s.Z * s.X);
    }

    /// <summary>The closest intersection of a local-space ray with the triangles.</summary>
    /// <param name="ray">The ray in the geometry's local space.</param>
    /// <param name="near">Smallest accepted ray parameter.</param>
    /// <param name="far">Largest accepted ray parameter.</param>
    /// <param name="side">Which triangle faces count.</param>
    /// <param name="hit">The hit, when the method returns true.</param>
    public bool RaycastClosest(Ray ray, float near, float far, Side side, out MeshBvhHit hit) =>
        RaycastClosest(new TriangleSource(_geometry), ray.Origin, ray.Direction, near, far, 0, int.MaxValue, side, out hit);

    /// <summary>Appends every intersection of a local-space ray with the triangles (unsorted).</summary>
    public void RaycastAll(Ray ray, float near, float far, Side side, List<MeshBvhHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        RaycastAll(new TriangleSource(_geometry), ray.Origin, ray.Direction, near, far, 0, int.MaxValue, side, hits);
    }

    /// <summary>Möller–Trumbore. The direction does not have to be normalized; t is in units of its length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IntersectTriangle(
        Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, Side side, out float t, out float u, out float v)
    {
        t = 0f;
        u = 0f;
        v = 0f;
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(direction, e2);
        var det = Vector3.Dot(e1, p);
        if (side == Side.Front)
        {
            if (det <= 1e-30f)
            {
                return false;
            }
        }
        else if (side == Side.Back)
        {
            if (det >= -1e-30f)
            {
                return false;
            }
        }
        else if (MathF.Abs(det) <= 1e-30f)
        {
            return false;
        }

        var inv = 1f / det;
        var s = origin - a;
        u = Vector3.Dot(s, p) * inv;
        if (!(u >= 0f && u <= 1f))
        {
            return false;
        }

        var q = Vector3.Cross(s, e1);
        v = Vector3.Dot(direction, q) * inv;
        if (!(v >= 0f && u + v <= 1f))
        {
            return false;
        }

        t = Vector3.Dot(e2, q) * inv;
        return t >= 0f;
    }

    /// <summary>Slab test; returns the entry parameter. NaNs (origin on a slab plane, parallel ray) count as inside.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HitBox(in Node node, Vector3 origin, Vector3 inverse, float tMin, float tMax, out float entry)
    {
        var t1 = (node.Min.X - origin.X) * inverse.X;
        var t2 = (node.Max.X - origin.X) * inverse.X;
        if (inverse.X < 0f)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = t1 > tMin ? t1 : tMin;
        tMax = t2 < tMax ? t2 : tMax;
        t1 = (node.Min.Y - origin.Y) * inverse.Y;
        t2 = (node.Max.Y - origin.Y) * inverse.Y;
        if (inverse.Y < 0f)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = t1 > tMin ? t1 : tMin;
        tMax = t2 < tMax ? t2 : tMax;
        t1 = (node.Min.Z - origin.Z) * inverse.Z;
        t2 = (node.Max.Z - origin.Z) * inverse.Z;
        if (inverse.Z < 0f)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = t1 > tMin ? t1 : tMin;
        tMax = t2 < tMax ? t2 : tMax;
        entry = tMin;
        return tMin <= tMax;
    }

    internal bool RaycastClosest(
        in TriangleSource source, Vector3 origin, Vector3 direction, float near, float far, int elementStart, int elementEnd, Side side, out MeshBvhHit hit)
    {
        hit = default;
        var found = false;
        var best = far;
        var inverse = Vector3.One / direction;
        var nodes = _nodes;
        var triangles = _triangles;
        if (!HitBox(nodes[0], origin, inverse, near, best, out _))
        {
            return false;
        }

        Span<int> stack = stackalloc int[StackSize];
        var top = 0;
        stack[top++] = 0;
        while (top > 0)
        {
            ref readonly var node = ref nodes[stack[--top]];
            if (node.Count > 0)
            {
                var end = node.LeftFirst + node.Count;
                for (var i = node.LeftFirst; i < end; i++)
                {
                    var triangle = triangles[i];
                    var element = triangle * 3;
                    if (element < elementStart || element + 3 > elementEnd)
                    {
                        continue;
                    }

                    source.Triangle(element, out var a, out var b, out var c);
                    if (IntersectTriangle(origin, direction, a, b, c, side, out var t, out var u, out var v) && t >= near && t <= best)
                    {
                        best = t;
                        found = true;
                        hit.Triangle = triangle;
                        hit.T = t;
                        hit.U = u;
                        hit.V = v;
                    }
                }

                continue;
            }

            var left = node.LeftFirst;
            var hitLeft = HitBox(nodes[left], origin, inverse, near, best, out var entryLeft);
            var hitRight = HitBox(nodes[left + 1], origin, inverse, near, best, out var entryRight);
            if (hitLeft && hitRight)
            {
                // Nearer child last, so it is popped first and shrinks the search interval sooner.
                if (entryLeft <= entryRight)
                {
                    stack[top++] = left + 1;
                    stack[top++] = left;
                }
                else
                {
                    stack[top++] = left;
                    stack[top++] = left + 1;
                }
            }
            else if (hitLeft)
            {
                stack[top++] = left;
            }
            else if (hitRight)
            {
                stack[top++] = left + 1;
            }
        }

        return found;
    }

    internal void RaycastAll(
        in TriangleSource source, Vector3 origin, Vector3 direction, float near, float far, int elementStart, int elementEnd, Side side, List<MeshBvhHit> hits)
    {
        var inverse = Vector3.One / direction;
        var nodes = _nodes;
        var triangles = _triangles;
        if (!HitBox(nodes[0], origin, inverse, near, far, out _))
        {
            return;
        }

        Span<int> stack = stackalloc int[StackSize];
        var top = 0;
        stack[top++] = 0;
        while (top > 0)
        {
            ref readonly var node = ref nodes[stack[--top]];
            if (node.Count > 0)
            {
                var end = node.LeftFirst + node.Count;
                for (var i = node.LeftFirst; i < end; i++)
                {
                    var triangle = triangles[i];
                    var element = triangle * 3;
                    if (element < elementStart || element + 3 > elementEnd)
                    {
                        continue;
                    }

                    source.Triangle(element, out var a, out var b, out var c);
                    if (IntersectTriangle(origin, direction, a, b, c, side, out var t, out var u, out var v) && t >= near && t <= far)
                    {
                        hits.Add(new MeshBvhHit { Triangle = triangle, T = t, U = u, V = v });
                    }
                }

                continue;
            }

            var left = node.LeftFirst;
            if (HitBox(nodes[left], origin, inverse, near, far, out _))
            {
                stack[top++] = left;
            }

            if (HitBox(nodes[left + 1], origin, inverse, near, far, out _))
            {
                stack[top++] = left + 1;
            }
        }
    }
}
