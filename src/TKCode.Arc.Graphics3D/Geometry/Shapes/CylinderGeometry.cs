using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A cylinder (or truncated cone) along the Y axis, centered on the origin. Material groups: 0 side, 1 top
/// cap, 2 bottom cap.</summary>
public class CylinderGeometry : BufferGeometry
{
    /// <summary>Creates a cylinder.</summary>
    public CylinderGeometry(
        float radiusTop = 1f, float radiusBottom = 1f, float height = 1f, int radialSegments = 32, int heightSegments = 1,
        bool openEnded = false, float thetaStart = 0f, float thetaLength = MathF.PI * 2f)
    {
        Name = "cylinder";
        radialSegments = Math.Max(3, radialSegments);
        heightSegments = Math.Max(1, heightSegments);
        var b = new GeometryBuilder();
        var half = height / 2f;
        var slope = (radiusBottom - radiusTop) / height;
        for (var y = 0; y <= heightSegments; y++)
        {
            var v = (float)y / heightSegments;
            var radius = (v * (radiusBottom - radiusTop)) + radiusTop;
            for (var x = 0; x <= radialSegments; x++)
            {
                var u = (float)x / radialSegments;
                var (sin, cos) = MathF.SinCos((u * thetaLength) + thetaStart);
                b.Add(new Vector3(radius * sin, (-v * height) + half, radius * cos), Vector3.Normalize(new Vector3(sin, slope, cos)), new Vector2(u, 1f - v));
            }
        }

        var groupStart = 0;
        var groupCount = 0;
        var row = radialSegments + 1;
        for (var x = 0; x < radialSegments; x++)
        {
            for (var y = 0; y < heightSegments; y++)
            {
                var a = (y * row) + x;
                var bb = ((y + 1) * row) + x;
                var c = ((y + 1) * row) + x + 1;
                var d = (y * row) + x + 1;
                if (radiusTop > 0f || y != 0)
                {
                    b.Triangle(a, bb, d);
                    groupCount += 3;
                }

                if (radiusBottom > 0f || y != heightSegments - 1)
                {
                    b.Triangle(bb, c, d);
                    groupCount += 3;
                }
            }
        }

        AddGroup(groupStart, groupCount, 0);
        groupStart += groupCount;
        if (!openEnded)
        {
            if (radiusTop > 0f)
            {
                groupStart += Cap(b, true, radiusTop, half, radialSegments, thetaStart, thetaLength, groupStart);
            }

            if (radiusBottom > 0f)
            {
                Cap(b, false, radiusBottom, half, radialSegments, thetaStart, thetaLength, groupStart);
            }
        }

        b.Apply(this);
    }

    private int Cap(GeometryBuilder b, bool top, float radius, float half, int radialSegments, float thetaStart, float thetaLength, int groupStart)
    {
        var sign = top ? 1f : -1f;
        var normal = new Vector3(0f, sign, 0f);
        var centerStart = b.VertexCount;
        for (var x = 1; x <= radialSegments; x++)
        {
            b.Add(new Vector3(0f, half * sign, 0f), normal, new Vector2(0.5f, 0.5f));
        }

        var rimStart = b.VertexCount;
        for (var x = 0; x <= radialSegments; x++)
        {
            var (sin, cos) = MathF.SinCos(((float)x / radialSegments * thetaLength) + thetaStart);
            b.Add(new Vector3(radius * sin, half * sign, radius * cos), normal, new Vector2((cos * 0.5f) + 0.5f, (sin * 0.5f * sign) + 0.5f));
        }

        for (var x = 0; x < radialSegments; x++)
        {
            var c = centerStart + x;
            var i = rimStart + x;
            if (top)
            {
                b.Triangle(i, i + 1, c);
            }
            else
            {
                b.Triangle(i + 1, i, c);
            }
        }

        var count = radialSegments * 3;
        AddGroup(groupStart, count, top ? 1 : 2);
        return count;
    }
}

/// <summary>A cone along the Y axis with its tip up, centered on the origin.</summary>
public class ConeGeometry : CylinderGeometry
{
    /// <summary>Creates a cone.</summary>
    public ConeGeometry(
        float radius = 1f, float height = 1f, int radialSegments = 32, int heightSegments = 1, bool openEnded = false,
        float thetaStart = 0f, float thetaLength = MathF.PI * 2f)
        : base(0f, radius, height, radialSegments, heightSegments, openEnded, thetaStart, thetaLength)
    {
        Name = "cone";
    }
}

/// <summary>A cylinder with hemispherical ends along the Y axis, centered on the origin.</summary>
public class CapsuleGeometry : BufferGeometry
{
    /// <summary>Creates a capsule. <paramref name="height"/> is the length of the straight middle part; the total
    /// height is <c>height + 2 * radius</c>.</summary>
    public CapsuleGeometry(float radius = 1f, float height = 1f, int capSegments = 4, int radialSegments = 8, int heightSegments = 1)
    {
        Name = "capsule";
        capSegments = Math.Max(1, capSegments);
        radialSegments = Math.Max(3, radialSegments);
        heightSegments = Math.Max(1, heightSegments);
        var half = height / 2f;

        // The profile from the top pole to the bottom pole: (ring radius, y, normal radial part, normal y part).
        var profile = new List<(float R, float Y, float Nr, float Ny)>();
        for (var i = 0; i <= capSegments; i++)
        {
            var (sin, cos) = MathF.SinCos((float)i / capSegments * (MathF.PI / 2f));
            profile.Add((radius * sin, half + (radius * cos), sin, cos));
        }

        for (var i = 1; i <= heightSegments; i++)
        {
            profile.Add((radius, half - ((float)i / heightSegments * height), 1f, 0f));
        }

        for (var i = 1; i <= capSegments; i++)
        {
            var (sin, cos) = MathF.SinCos((MathF.PI / 2f) + ((float)i / capSegments * (MathF.PI / 2f)));
            profile.Add((radius * sin, -half + (radius * cos), sin, cos));
        }

        // V follows the length of the profile.
        var total = (MathF.PI * radius) + height;
        var b = new GeometryBuilder();
        var rings = profile.Count;
        var travelled = 0f;
        for (var j = 0; j < rings; j++)
        {
            var p = profile[j];
            if (j > 0)
            {
                var q = profile[j - 1];
                travelled += MathF.Sqrt(((p.R - q.R) * (p.R - q.R)) + ((p.Y - q.Y) * (p.Y - q.Y)));
            }

            var v = total > 0f ? 1f - (travelled / total) : 0f;
            for (var x = 0; x <= radialSegments; x++)
            {
                var u = (float)x / radialSegments;
                var (sin, cos) = MathF.SinCos(u * MathF.PI * 2f);
                b.Add(new Vector3(p.R * sin, p.Y, p.R * cos), new Vector3(p.Nr * sin, p.Ny, p.Nr * cos), new Vector2(u, v));
            }
        }

        var row = radialSegments + 1;
        for (var j = 0; j < rings - 1; j++)
        {
            for (var x = 0; x < radialSegments; x++)
            {
                var a = (j * row) + x;
                var bb = ((j + 1) * row) + x;
                var c = ((j + 1) * row) + x + 1;
                var d = (j * row) + x + 1;
                if (j != 0)
                {
                    b.Triangle(a, bb, d);
                }

                if (j != rings - 2)
                {
                    b.Triangle(bb, c, d);
                }
            }
        }

        b.Apply(this);
    }
}
