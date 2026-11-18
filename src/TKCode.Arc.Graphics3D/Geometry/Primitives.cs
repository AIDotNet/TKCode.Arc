using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Collects vertices and indices of a generated geometry.</summary>
internal sealed class GeometryBuilder
{
    public readonly List<float> Positions = [];
    public readonly List<float> Normals = [];
    public readonly List<float> Uvs = [];
    public readonly List<int> Indices = [];

    public int VertexCount => Positions.Count / 3;

    public int Add(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Positions.Add(position.X);
        Positions.Add(position.Y);
        Positions.Add(position.Z);
        Normals.Add(normal.X);
        Normals.Add(normal.Y);
        Normals.Add(normal.Z);
        Uvs.Add(uv.X);
        Uvs.Add(uv.Y);
        return VertexCount - 1;
    }

    public void Triangle(int a, int b, int c)
    {
        Indices.Add(a);
        Indices.Add(b);
        Indices.Add(c);
    }

    public void Quad(int a, int b, int c, int d)
    {
        Triangle(a, b, d);
        Triangle(b, c, d);
    }

    public void Apply(BufferGeometry geometry)
    {
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([.. Positions], 3));
        geometry.SetAttribute(BufferGeometry.Normal, VertexAttribute.Float([.. Normals], 3));
        geometry.SetAttribute(BufferGeometry.Uv, VertexAttribute.Float([.. Uvs], 2));
        geometry.SetIndex(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Indices), VertexCount);
    }
}

/// <summary>A box centered on the origin. Each side is a material group (order: +X, -X, +Y, -Y, +Z, -Z).</summary>
public class BoxGeometry : BufferGeometry
{
    /// <summary>Creates a box.</summary>
    public BoxGeometry(float width = 1f, float height = 1f, float depth = 1f, int widthSegments = 1, int heightSegments = 1, int depthSegments = 1)
    {
        Name = "box";
        var b = new GeometryBuilder();
        widthSegments = Math.Max(1, widthSegments);
        heightSegments = Math.Max(1, heightSegments);
        depthSegments = Math.Max(1, depthSegments);
        var groupStart = 0;
        var p = new float[3];
        var n = new float[3];
        void Side(int u, int v, int w, float udir, float vdir, float sw, float sh, float sd, int gridX, int gridY, int materialIndex)
        {
            var first = b.VertexCount;
            for (var iy = 0; iy <= gridY; iy++)
            {
                var y = ((iy * sh / gridY) - (sh / 2f)) * vdir;
                for (var ix = 0; ix <= gridX; ix++)
                {
                    var x = ((ix * sw / gridX) - (sw / 2f)) * udir;
                    Array.Clear(n);
                    p[u] = x;
                    p[v] = y;
                    p[w] = sd / 2f;
                    n[w] = sd > 0f ? 1f : -1f;
                    b.Add(new Vector3(p[0], p[1], p[2]), new Vector3(n[0], n[1], n[2]), new Vector2((float)ix / gridX, 1f - ((float)iy / gridY)));
                }
            }

            for (var iy = 0; iy < gridY; iy++)
            {
                for (var ix = 0; ix < gridX; ix++)
                {
                    var a = first + ix + ((gridX + 1) * iy);
                    var c = first + ix + ((gridX + 1) * (iy + 1));
                    var d = first + ix + 1 + ((gridX + 1) * (iy + 1));
                    var e = first + ix + 1 + ((gridX + 1) * iy);
                    b.Triangle(a, c, e);
                    b.Triangle(c, d, e);
                }
            }

            var count = gridX * gridY * 6;
            AddGroup(groupStart, count, materialIndex);
            groupStart += count;
        }

        Side(2, 1, 0, -1f, -1f, depth, height, width, depthSegments, heightSegments, 0);
        Side(2, 1, 0, 1f, -1f, depth, height, -width, depthSegments, heightSegments, 1);
        Side(0, 2, 1, 1f, 1f, width, depth, height, widthSegments, depthSegments, 2);
        Side(0, 2, 1, 1f, -1f, width, depth, -height, widthSegments, depthSegments, 3);
        Side(0, 1, 2, 1f, -1f, width, height, depth, widthSegments, heightSegments, 4);
        Side(0, 1, 2, -1f, -1f, width, height, -depth, widthSegments, heightSegments, 5);
        b.Apply(this);
    }
}

/// <summary>A rectangle in the XY plane facing +Z.</summary>
public class PlaneGeometry : BufferGeometry
{
    /// <summary>Creates a plane.</summary>
    public PlaneGeometry(float width = 1f, float height = 1f, int widthSegments = 1, int heightSegments = 1)
    {
        Name = "plane";
        var b = new GeometryBuilder();
        var gx = Math.Max(1, widthSegments);
        var gy = Math.Max(1, heightSegments);
        for (var iy = 0; iy <= gy; iy++)
        {
            var y = (height / 2f) - (iy * height / gy);
            for (var ix = 0; ix <= gx; ix++)
            {
                var x = (ix * width / gx) - (width / 2f);
                b.Add(new Vector3(x, y, 0f), Vector3.UnitZ, new Vector2((float)ix / gx, 1f - ((float)iy / gy)));
            }
        }

        for (var iy = 0; iy < gy; iy++)
        {
            for (var ix = 0; ix < gx; ix++)
            {
                var a = ix + ((gx + 1) * iy);
                var c = ix + ((gx + 1) * (iy + 1));
                var d = ix + 1 + ((gx + 1) * (iy + 1));
                var e = ix + 1 + ((gx + 1) * iy);
                b.Triangle(a, c, e);
                b.Triangle(c, d, e);
            }
        }

        b.Apply(this);
    }
}

/// <summary>A UV sphere centered on the origin, poles on the Y axis.</summary>
public class SphereGeometry : BufferGeometry
{
    /// <summary>Creates a sphere, or the part of one between the given angles.</summary>
    public SphereGeometry(
        float radius = 1f, int widthSegments = 32, int heightSegments = 16,
        float phiStart = 0f, float phiLength = MathF.PI * 2f, float thetaStart = 0f, float thetaLength = MathF.PI)
    {
        Name = "sphere";
        var b = new GeometryBuilder();
        widthSegments = Math.Max(3, widthSegments);
        heightSegments = Math.Max(2, heightSegments);
        var thetaEnd = MathF.Min(thetaStart + thetaLength, MathF.PI);
        for (var iy = 0; iy <= heightSegments; iy++)
        {
            var v = (float)iy / heightSegments;
            // Shift the pole vertices' U to the middle of their triangle, so textures do not pinch.
            var uOffset = iy == 0 && thetaStart == 0f ? 0.5f / widthSegments : iy == heightSegments && thetaEnd == MathF.PI ? -0.5f / widthSegments : 0f;
            for (var ix = 0; ix <= widthSegments; ix++)
            {
                var u = (float)ix / widthSegments;
                var (sinPhi, cosPhi) = MathF.SinCos(phiStart + (u * phiLength));
                var (sinTheta, cosTheta) = MathF.SinCos(thetaStart + (v * thetaLength));
                var n = new Vector3(-cosPhi * sinTheta, cosTheta, sinPhi * sinTheta);
                b.Add(n * radius, n, new Vector2(u + uOffset, 1f - v));
            }
        }

        for (var iy = 0; iy < heightSegments; iy++)
        {
            for (var ix = 0; ix < widthSegments; ix++)
            {
                var a = ix + 1 + ((widthSegments + 1) * iy);
                var c = ix + ((widthSegments + 1) * iy);
                var d = ix + ((widthSegments + 1) * (iy + 1));
                var e = ix + 1 + ((widthSegments + 1) * (iy + 1));
                if (iy != 0 || thetaStart > 0f)
                {
                    b.Triangle(a, c, e);
                }

                if (iy != heightSegments - 1 || thetaEnd < MathF.PI)
                {
                    b.Triangle(c, d, e);
                }
            }
        }

        b.Apply(this);
    }
}
