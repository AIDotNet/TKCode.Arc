using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A solid with flat faces, projected onto a sphere. Each detail level splits every
/// triangle further, approaching a sphere with evenly sized triangles. Vertices are not shared between triangles.</summary>
public class PolyhedronGeometry : BufferGeometry
{
    /// <summary>Creates a polyhedron from corner positions (xyz triples) and triangle indices.</summary>
    public PolyhedronGeometry(ReadOnlySpan<float> vertices, ReadOnlySpan<int> indices, float radius = 1f, int detail = 0)
    {
        Name = "polyhedron";
        detail = Math.Max(0, detail);
        var points = new List<Vector3>();
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            Subdivide(Corner(vertices, indices[i]), Corner(vertices, indices[i + 1]), Corner(vertices, indices[i + 2]), detail, points);
        }

        var count = points.Count;
        var positions = new float[count * 3];
        var normals = new float[count * 3];
        var uvs = new float[count * 2];
        for (var i = 0; i < count; i++)
        {
            var unit = Vector3.Normalize(points[i]);
            points[i] = unit * radius;
            positions[i * 3] = points[i].X;
            positions[(i * 3) + 1] = points[i].Y;
            positions[(i * 3) + 2] = points[i].Z;
            uvs[i * 2] = (Azimuth(unit) / (2f * MathF.PI)) + 0.5f;
            uvs[(i * 2) + 1] = 1f - ((Inclination(unit) / MathF.PI) + 0.5f);
        }

        // Poles have no azimuth of their own and the seam wraps: take both from the triangle.
        for (var i = 0; i + 2 < count; i += 3)
        {
            var azimuth = Azimuth((points[i] + points[i + 1] + points[i + 2]) / 3f);
            for (var k = 0; k < 3; k++)
            {
                var o = (i + k) * 2;
                if (azimuth < 0f && uvs[o] == 1f)
                {
                    uvs[o] -= 1f;
                }

                if (points[i + k].X == 0f && points[i + k].Z == 0f)
                {
                    uvs[o] = (azimuth / (2f * MathF.PI)) + 0.5f;
                }
            }

            float u0 = uvs[i * 2], u1 = uvs[(i + 1) * 2], u2 = uvs[(i + 2) * 2];
            if (MathF.Max(u0, MathF.Max(u1, u2)) > 0.9f && MathF.Min(u0, MathF.Min(u1, u2)) < 0.1f)
            {
                for (var k = 0; k < 3; k++)
                {
                    if (uvs[(i + k) * 2] < 0.2f)
                    {
                        uvs[(i + k) * 2] += 1f;
                    }
                }
            }
        }

        for (var i = 0; i + 2 < count; i += 3)
        {
            // Flat faces without subdivision, smooth (spherical) normals with it.
            var face = Vector3.Cross(points[i + 1] - points[i], points[i + 2] - points[i]);
            face = face.LengthSquared() > 0f ? Vector3.Normalize(face) : Vector3.UnitY;
            for (var k = 0; k < 3; k++)
            {
                var n = detail == 0 ? face : Vector3.Normalize(points[i + k]);
                normals[(i + k) * 3] = n.X;
                normals[((i + k) * 3) + 1] = n.Y;
                normals[((i + k) * 3) + 2] = n.Z;
            }
        }

        var index = new int[count];
        for (var i = 0; i < count; i++)
        {
            index[i] = i;
        }

        SetAttribute(Position, VertexAttribute.Float(positions, 3));
        SetAttribute(Normal, VertexAttribute.Float(normals, 3));
        SetAttribute(Uv, VertexAttribute.Float(uvs, 2));
        SetIndex(index, count);
    }

    private static Vector3 Corner(ReadOnlySpan<float> vertices, int index) =>
        new(vertices[index * 3], vertices[(index * 3) + 1], vertices[(index * 3) + 2]);

    private static float Azimuth(Vector3 v) => MathF.Atan2(v.Z, -v.X);

    private static float Inclination(Vector3 v) => MathF.Atan2(-v.Y, MathF.Sqrt((v.X * v.X) + (v.Z * v.Z)));

    private static void Subdivide(Vector3 a, Vector3 b, Vector3 c, int detail, List<Vector3> output)
    {
        var cols = detail + 1;
        var grid = new Vector3[cols + 1][];
        for (var i = 0; i <= cols; i++)
        {
            var aj = Vector3.Lerp(a, c, (float)i / cols);
            var bj = Vector3.Lerp(b, c, (float)i / cols);
            var rows = cols - i;
            grid[i] = new Vector3[rows + 1];
            for (var j = 0; j <= rows; j++)
            {
                grid[i][j] = j == 0 && i == cols ? aj : Vector3.Lerp(aj, bj, (float)j / rows);
            }
        }

        for (var i = 0; i < cols; i++)
        {
            for (var j = 0; j < (2 * (cols - i)) - 1; j++)
            {
                var k = j / 2;
                if (j % 2 == 0)
                {
                    output.Add(grid[i][k + 1]);
                    output.Add(grid[i + 1][k]);
                    output.Add(grid[i][k]);
                }
                else
                {
                    output.Add(grid[i][k + 1]);
                    output.Add(grid[i + 1][k + 1]);
                    output.Add(grid[i + 1][k]);
                }
            }
        }
    }
}

/// <summary>A solid with four triangular faces.</summary>
public class TetrahedronGeometry : PolyhedronGeometry
{
    private static readonly float[] s_vertices = [1, 1, 1, -1, -1, 1, -1, 1, -1, 1, -1, -1];
    private static readonly int[] s_indices = [2, 1, 0, 0, 3, 2, 1, 3, 0, 2, 3, 1];

    /// <summary>Creates a tetrahedron.</summary>
    public TetrahedronGeometry(float radius = 1f, int detail = 0)
        : base(s_vertices, s_indices, radius, detail)
    {
        Name = "tetrahedron";
    }
}

/// <summary>A solid with eight triangular faces.</summary>
public class OctahedronGeometry : PolyhedronGeometry
{
    private static readonly float[] s_vertices = [1, 0, 0, -1, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 1, 0, 0, -1];
    private static readonly int[] s_indices = [0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 3, 1, 3, 4, 1, 4, 2];

    /// <summary>Creates an octahedron.</summary>
    public OctahedronGeometry(float radius = 1f, int detail = 0)
        : base(s_vertices, s_indices, radius, detail)
    {
        Name = "octahedron";
    }
}

/// <summary>A solid with twenty triangular faces; with detail, the most even sphere.</summary>
public class IcosahedronGeometry : PolyhedronGeometry
{
    private const float T = 1.618033988749895f;

    private static readonly float[] s_vertices =
    [
        -1, T, 0, 1, T, 0, -1, -T, 0, 1, -T, 0,
        0, -1, T, 0, 1, T, 0, -1, -T, 0, 1, -T,
        T, 0, -1, T, 0, 1, -T, 0, -1, -T, 0, 1,
    ];

    private static readonly int[] s_indices =
    [
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
        1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
        4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
    ];

    /// <summary>Creates an icosahedron.</summary>
    public IcosahedronGeometry(float radius = 1f, int detail = 0)
        : base(s_vertices, s_indices, radius, detail)
    {
        Name = "icosahedron";
    }
}

/// <summary>A solid with twelve pentagonal faces.</summary>
public class DodecahedronGeometry : PolyhedronGeometry
{
    private const float T = 1.618033988749895f;
    private const float R = 1f / T;

    private static readonly float[] s_vertices =
    [
        -1, -1, -1, -1, -1, 1, -1, 1, -1, -1, 1, 1,
        1, -1, -1, 1, -1, 1, 1, 1, -1, 1, 1, 1,
        0, -R, -T, 0, -R, T, 0, R, -T, 0, R, T,
        -R, -T, 0, -R, T, 0, R, -T, 0, R, T, 0,
        -T, 0, -R, T, 0, -R, -T, 0, R, T, 0, R,
    ];

    private static readonly int[] s_indices =
    [
        3, 11, 7, 3, 7, 15, 3, 15, 13,
        7, 19, 17, 7, 17, 6, 7, 6, 15,
        17, 4, 8, 17, 8, 10, 17, 10, 6,
        8, 0, 16, 8, 16, 2, 8, 2, 10,
        0, 12, 1, 0, 1, 18, 0, 18, 16,
        6, 10, 2, 6, 2, 13, 6, 13, 15,
        2, 16, 18, 2, 18, 3, 2, 3, 13,
        18, 1, 9, 18, 9, 11, 18, 11, 3,
        4, 14, 12, 4, 12, 0, 4, 0, 8,
        11, 9, 5, 11, 5, 19, 11, 19, 7,
        19, 5, 14, 19, 14, 4, 19, 4, 17,
        1, 12, 14, 1, 14, 5, 1, 5, 9,
    ];

    /// <summary>Creates a dodecahedron.</summary>
    public DodecahedronGeometry(float radius = 1f, int detail = 0)
        : base(s_vertices, s_indices, radius, detail)
    {
        Name = "dodecahedron";
    }
}
