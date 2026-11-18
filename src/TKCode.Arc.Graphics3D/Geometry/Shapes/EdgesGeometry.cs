using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>Reads the triangles of a geometry (indexed or not, honoring its draw range).</summary>
internal static class TriangleReader
{
    public static void ForEach(BufferGeometry geometry, Action<Vector3, Vector3, Vector3> triangle)
    {
        var position = geometry.GetAttribute(BufferGeometry.Position);
        if (position is null || geometry.Topology != PrimitiveTopology.Triangles)
        {
            return;
        }

        var elements = geometry.ElementCount;
        var start = Math.Clamp(geometry.DrawStart, 0, elements);
        var count = geometry.DrawCount < 0 ? elements - start : Math.Min(geometry.DrawCount, elements - start);
        for (var i = start; i + 2 < start + count; i += 3)
        {
            triangle(
                position.GetVector3(geometry.GetIndex(i)), position.GetVector3(geometry.GetIndex(i + 1)), position.GetVector3(geometry.GetIndex(i + 2)));
        }
    }

    /// <summary>A key that is equal for positions that are equal to four decimal places.</summary>
    public static (long X, long Y, long Z) Key(Vector3 p) =>
        ((long)MathF.Round(p.X * 1e4f), (long)MathF.Round(p.Y * 1e4f), (long)MathF.Round(p.Z * 1e4f));
}

/// <summary>The hard edges of a geometry as line segments: edges whose two faces meet at more than
/// <c>thresholdAngle</c> degrees, and edges with only one face. Draw it with <see cref="LineSegments"/>.</summary>
public class EdgesGeometry : BufferGeometry
{
    /// <summary>Creates the edges of a triangle geometry.</summary>
    public EdgesGeometry(BufferGeometry geometry, float thresholdAngle = 1f)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Name = "edges";
        Topology = PrimitiveTopology.Lines;
        var thresholdDot = MathF.Cos(MathUtil.ToRadians(thresholdAngle));
        var open = new Dictionary<((long, long, long), (long, long, long)), (Vector3 A, Vector3 B, Vector3 Normal, bool Used)>();
        var vertices = new List<float>();
        var cornerArray = new Vector3[3];
        var keys = new (long, long, long)[3];
        TriangleReader.ForEach(geometry, (a, b, c) =>
        {
            var normal = Vector3.Cross(b - a, c - a);
            var ka = TriangleReader.Key(a);
            var kb = TriangleReader.Key(b);
            var kc = TriangleReader.Key(c);
            if (normal.LengthSquared() < 1e-20f || ka == kb || kb == kc || kc == ka)
            {
                return;
            }

            normal = Vector3.Normalize(normal);
            cornerArray[0] = a;
            cornerArray[1] = b;
            cornerArray[2] = c;
            keys[0] = ka;
            keys[1] = kb;
            keys[2] = kc;
            for (var j = 0; j < 3; j++)
            {
                var k = (j + 1) % 3;
                var forward = (keys[j], keys[k]);
                var reverse = (keys[k], keys[j]);
                if (open.TryGetValue(reverse, out var other) && !other.Used)
                {
                    // The neighbouring face runs along this edge the other way.
                    if (Vector3.Dot(normal, other.Normal) <= thresholdDot)
                    {
                        Add(vertices, cornerArray[j], cornerArray[k]);
                    }

                    open[reverse] = (other.A, other.B, other.Normal, true);
                }
                else if (!open.ContainsKey(forward))
                {
                    open[forward] = (cornerArray[j], cornerArray[k], normal, false);
                }
            }
        });
        foreach (var edge in open.Values)
        {
            if (!edge.Used)
            {
                Add(vertices, edge.A, edge.B);
            }
        }

        SetAttribute(Position, VertexAttribute.Float([.. vertices], 3));
    }

    private static void Add(List<float> vertices, Vector3 a, Vector3 b)
    {
        vertices.Add(a.X);
        vertices.Add(a.Y);
        vertices.Add(a.Z);
        vertices.Add(b.X);
        vertices.Add(b.Y);
        vertices.Add(b.Z);
    }
}

/// <summary>Every edge of every triangle of a geometry as line segments (each edge once). Draw it with
/// <see cref="LineSegments"/>.</summary>
public class WireframeGeometry : BufferGeometry
{
    /// <summary>Creates the wireframe of a triangle geometry.</summary>
    public WireframeGeometry(BufferGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        Name = "wireframe";
        Topology = PrimitiveTopology.Lines;
        var seen = new HashSet<((long, long, long), (long, long, long))>();
        var vertices = new List<float>();
        void Edge(Vector3 a, Vector3 b)
        {
            var ka = TriangleReader.Key(a);
            var kb = TriangleReader.Key(b);
            if (ka == kb || seen.Contains((ka, kb)) || seen.Contains((kb, ka)))
            {
                return;
            }

            seen.Add((ka, kb));
            vertices.Add(a.X);
            vertices.Add(a.Y);
            vertices.Add(a.Z);
            vertices.Add(b.X);
            vertices.Add(b.Y);
            vertices.Add(b.Z);
        }

        TriangleReader.ForEach(geometry, (a, b, c) =>
        {
            Edge(a, b);
            Edge(b, c);
            Edge(c, a);
        });
        SetAttribute(Position, VertexAttribute.Float([.. vertices], 3));
    }
}

/// <summary>A box with rounded edges and corners, centered on the origin. Each side is a material group, as in
/// <see cref="BoxGeometry"/>.</summary>
public class RoundedBoxGeometry : BufferGeometry
{
    /// <summary>Creates a rounded box. <paramref name="segments"/> is the number of steps across each rounded edge.</summary>
    public RoundedBoxGeometry(float width = 1f, float height = 1f, float depth = 1f, int segments = 2, float radius = 0.1f)
    {
        Name = "roundedBox";
        segments = Math.Max(1, segments);
        radius = MathF.Max(0f, MathF.Min(radius, MathF.Min(width, MathF.Min(height, depth)) / 2f));

        // A unit box with an odd number of segments: the middle one becomes the flat face, the others the rounding.
        var grid = (segments * 2) + 1;
        using var unit = new BoxGeometry(1f, 1f, 1f, grid, grid, grid);
        var source = unit.GetAttribute(Position)!.FloatData;
        var count = source.Length / 3;
        var positions = new float[source.Length];
        var normals = new float[source.Length];
        var uvs = new float[count * 2];
        var half = new Vector3(width, height, depth) / 2f;
        var inner = half - new Vector3(radius);
        var halfSegment = 0.5f / grid;
        var perSide = (grid + 1) * (grid + 1);

        // Axes and directions of the six sides, as BoxGeometry builds them.
        ReadOnlySpan<int> uAxis = [2, 2, 0, 0, 0, 0];
        ReadOnlySpan<int> vAxis = [1, 1, 2, 2, 1, 1];
        ReadOnlySpan<float> uDir = [-1f, 1f, 1f, 1f, 1f, -1f];
        ReadOnlySpan<float> vDir = [-1f, -1f, 1f, -1f, -1f, -1f];
        Span<float> p = stackalloc float[3];
        Span<float> size = [width, height, depth];
        for (var i = 0; i < count; i++)
        {
            var unitPosition = new Vector3(source[i * 3], source[(i * 3) + 1], source[(i * 3) + 2]);
            var sign = new Vector3(MathF.Sign(unitPosition.X), MathF.Sign(unitPosition.Y), MathF.Sign(unitPosition.Z));
            var normal = unitPosition - (sign * halfSegment);

            // Inside the middle segment the component is zero: that part stays flat.
            normal = new Vector3(Flat(unitPosition.X, normal.X, halfSegment), Flat(unitPosition.Y, normal.Y, halfSegment), Flat(unitPosition.Z, normal.Z, halfSegment));
            normal = Vector3.Normalize(normal);
            var position = (inner * sign) + (normal * radius);
            positions[i * 3] = position.X;
            positions[(i * 3) + 1] = position.Y;
            positions[(i * 3) + 2] = position.Z;
            normals[i * 3] = normal.X;
            normals[(i * 3) + 1] = normal.Y;
            normals[(i * 3) + 2] = normal.Z;
            var side = Math.Min(i / perSide, 5);
            p[0] = position.X;
            p[1] = position.Y;
            p[2] = position.Z;
            uvs[i * 2] = (p[uAxis[side]] / size[uAxis[side]] * uDir[side]) + 0.5f;
            uvs[(i * 2) + 1] = 0.5f - (p[vAxis[side]] / size[vAxis[side]] * vDir[side]);
        }

        SetAttribute(Position, VertexAttribute.Float(positions, 3));
        SetAttribute(Normal, VertexAttribute.Float(normals, 3));
        SetAttribute(Uv, VertexAttribute.Float(uvs, 2));
        var indexCount = unit.IndexCount;
        var indices = new int[indexCount];
        for (var i = 0; i < indexCount; i++)
        {
            indices[i] = unit.GetIndex(i);
        }

        SetIndex(indices, count);
        foreach (var group in unit.Groups)
        {
            AddGroup(group.Start, group.Count, group.MaterialIndex);
        }
    }

    private static float Flat(float unit, float shifted, float halfSegment) => MathF.Abs(unit) <= halfSegment + 1e-6f ? 0f : shifted;
}
