using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A ring-shaped tube in the XY plane, centered on the origin.</summary>
public class TorusGeometry : BufferGeometry
{
    /// <summary>Creates a torus.</summary>
    /// <param name="radius">From the center of the torus to the center of the tube.</param>
    /// <param name="tube">Radius of the tube.</param>
    /// <param name="radialSegments">Segments around the tube.</param>
    /// <param name="tubularSegments">Segments around the ring.</param>
    /// <param name="arc">Central angle of the ring, in radians.</param>
    public TorusGeometry(float radius = 1f, float tube = 0.4f, int radialSegments = 12, int tubularSegments = 48, float arc = MathF.PI * 2f)
    {
        Name = "torus";
        radialSegments = Math.Max(3, radialSegments);
        tubularSegments = Math.Max(3, tubularSegments);
        var b = new GeometryBuilder();
        for (var j = 0; j <= radialSegments; j++)
        {
            for (var i = 0; i <= tubularSegments; i++)
            {
                var u = (float)i / tubularSegments * arc;
                var v = (float)j / radialSegments * MathF.PI * 2f;
                var (sinU, cosU) = MathF.SinCos(u);
                var (sinV, cosV) = MathF.SinCos(v);
                var position = new Vector3((radius + (tube * cosV)) * cosU, (radius + (tube * cosV)) * sinU, tube * sinV);
                var center = new Vector3(radius * cosU, radius * sinU, 0f);
                b.Add(position, Vector3.Normalize(position - center), new Vector2((float)i / tubularSegments, (float)j / radialSegments));
            }
        }

        var row = tubularSegments + 1;
        for (var j = 1; j <= radialSegments; j++)
        {
            for (var i = 1; i <= tubularSegments; i++)
            {
                b.Quad((row * j) + i - 1, (row * (j - 1)) + i - 1, (row * (j - 1)) + i, (row * j) + i);
            }
        }

        b.Apply(this);
    }
}

/// <summary>A tube tied into a (p, q) torus knot.</summary>
public class TorusKnotGeometry : BufferGeometry
{
    /// <summary>Creates a torus knot that winds <paramref name="p"/> times around its axis and <paramref name="q"/>
    /// times around the inside of the torus.</summary>
    public TorusKnotGeometry(float radius = 1f, float tube = 0.4f, int tubularSegments = 64, int radialSegments = 8, int p = 2, int q = 3)
    {
        Name = "torusKnot";
        tubularSegments = Math.Max(3, tubularSegments);
        radialSegments = Math.Max(3, radialSegments);
        var b = new GeometryBuilder();
        for (var i = 0; i <= tubularSegments; i++)
        {
            var u = (float)i / tubularSegments * p * MathF.PI * 2f;
            var p1 = KnotPoint(u, p, q, radius);
            var p2 = KnotPoint(u + 0.01f, p, q, radius);
            var tangent = p2 - p1;
            var normal = p2 + p1;
            var binormal = Vector3.Cross(tangent, normal);
            normal = Vector3.Normalize(Vector3.Cross(binormal, tangent));
            binormal = Vector3.Normalize(binormal);
            for (var j = 0; j <= radialSegments; j++)
            {
                var v = (float)j / radialSegments * MathF.PI * 2f;
                var cx = -tube * MathF.Cos(v);
                var cy = tube * MathF.Sin(v);
                var position = p1 + (normal * cx) + (binormal * cy);
                b.Add(position, Vector3.Normalize(position - p1), new Vector2((float)i / tubularSegments, (float)j / radialSegments));
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

    private static Vector3 KnotPoint(float u, int p, int q, float radius)
    {
        var (su, cu) = MathF.SinCos(u);
        var quOverP = (float)q / p * u;
        var cs = MathF.Cos(quOverP);
        return new Vector3(radius * (2f + cs) * 0.5f * cu, radius * (2f + cs) * su * 0.5f, radius * MathF.Sin(quOverP) * 0.5f);
    }
}
