using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A surface of revolution: a 2D profile (x = distance from the axis, y = height) spun around the Y axis.
/// List the profile from bottom to top for outward-facing surfaces.</summary>
public class LatheGeometry : BufferGeometry
{
    /// <summary>Creates a lathe surface.</summary>
    public LatheGeometry(IReadOnlyList<Vector2>? points = null, int segments = 12, float phiStart = 0f, float phiLength = MathF.PI * 2f)
    {
        Name = "lathe";
        points ??= [new Vector2(0f, -0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f)];
        if (points.Count < 2)
        {
            throw new ArgumentException("A lathe profile needs at least two points.", nameof(points));
        }

        segments = Math.Max(3, segments);
        phiLength = Math.Clamp(phiLength, 0f, MathF.PI * 2f);
        var n = points.Count;

        // Normals of the profile: the average of the two neighbouring edges' normals.
        var profileNormals = new Vector2[n];
        var previous = Vector2.Zero;
        for (var j = 0; j < n; j++)
        {
            if (j == n - 1)
            {
                profileNormals[j] = previous;
                continue;
            }

            var d = points[j + 1] - points[j];
            var edge = new Vector2(d.Y, -d.X);
            edge = edge.LengthSquared() > 0f ? Vector2.Normalize(edge) : previous;
            profileNormals[j] = j == 0 ? edge : edge + previous;
            previous = edge;
        }

        for (var j = 0; j < n; j++)
        {
            profileNormals[j] = profileNormals[j].LengthSquared() > 0f ? Vector2.Normalize(profileNormals[j]) : Vector2.UnitX;
        }

        var b = new GeometryBuilder();
        for (var i = 0; i <= segments; i++)
        {
            var (sin, cos) = MathF.SinCos(phiStart + ((float)i / segments * phiLength));
            for (var j = 0; j < n; j++)
            {
                var p = points[j];
                var pn = profileNormals[j];
                b.Add(new Vector3(p.X * sin, p.Y, p.X * cos), new Vector3(pn.X * sin, pn.Y, pn.X * cos), new Vector2((float)i / segments, (float)j / (n - 1)));
            }
        }

        for (var i = 0; i < segments; i++)
        {
            for (var j = 0; j < n - 1; j++)
            {
                var a = j + (i * n);
                var bb = a + n;
                var c = a + n + 1;
                var d = a + 1;
                b.Triangle(a, bb, d);
                b.Triangle(c, d, bb);
            }
        }

        b.Apply(this);
    }
}

/// <summary>A tube that follows a curve.</summary>
public class TubeGeometry : BufferGeometry
{
    /// <summary>Creates a tube.</summary>
    public TubeGeometry(Curve3 path, int tubularSegments = 64, float radius = 1f, int radialSegments = 8, bool closed = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        Name = "tube";
        tubularSegments = Math.Max(1, tubularSegments);
        radialSegments = Math.Max(3, radialSegments);
        var frames = path.ComputeFrenetFrames(tubularSegments, closed);
        Frames = frames;
        var b = new GeometryBuilder();
        for (var i = 0; i <= tubularSegments; i++)
        {
            // A closed tube ends on the first ring again, so the seam has no gap.
            var k = closed && i == tubularSegments ? 0 : i;
            var center = path.GetPointAt((float)k / tubularSegments);
            var normal = frames.Normals[k];
            var binormal = frames.Binormals[k];
            for (var j = 0; j <= radialSegments; j++)
            {
                var (sin, cos) = MathF.SinCos((float)j / radialSegments * MathF.PI * 2f);
                var direction = Vector3.Normalize((normal * -cos) + (binormal * sin));
                b.Add(center + (direction * radius), direction, new Vector2((float)i / tubularSegments, (float)j / radialSegments));
            }
        }

        var row = radialSegments + 1;
        for (var j = 1; j <= tubularSegments; j++)
        {
            for (var i = 1; i <= radialSegments; i++)
            {
                b.Quad((row * (j - 1)) + i - 1, (row * j) + i - 1, (row * j) + i, (row * (j - 1)) + i);
            }
        }

        b.Apply(this);
    }

    /// <summary>The frames the tube was built on.</summary>
    public FrenetFrames Frames { get; }
}
