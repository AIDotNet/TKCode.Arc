using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A disc (or a sector of one) in the XY plane facing +Z.</summary>
public class CircleGeometry : BufferGeometry
{
    /// <summary>Creates a disc.</summary>
    public CircleGeometry(float radius = 1f, int segments = 32, float thetaStart = 0f, float thetaLength = MathF.PI * 2f)
    {
        Name = "circle";
        segments = Math.Max(3, segments);
        var b = new GeometryBuilder();
        b.Add(Vector3.Zero, Vector3.UnitZ, new Vector2(0.5f, 0.5f));
        for (var s = 0; s <= segments; s++)
        {
            var (sin, cos) = MathF.SinCos(thetaStart + ((float)s / segments * thetaLength));
            b.Add(new Vector3(radius * cos, radius * sin, 0f), Vector3.UnitZ, new Vector2((cos + 1f) / 2f, (sin + 1f) / 2f));
        }

        for (var i = 1; i <= segments; i++)
        {
            b.Triangle(i, i + 1, 0);
        }

        b.Apply(this);
    }
}

/// <summary>A flat ring (or a sector of one) in the XY plane facing +Z.</summary>
public class RingGeometry : BufferGeometry
{
    /// <summary>Creates a ring.</summary>
    public RingGeometry(
        float innerRadius = 0.5f, float outerRadius = 1f, int thetaSegments = 32, int phiSegments = 1, float thetaStart = 0f,
        float thetaLength = MathF.PI * 2f)
    {
        Name = "ring";
        thetaSegments = Math.Max(3, thetaSegments);
        phiSegments = Math.Max(1, phiSegments);
        var b = new GeometryBuilder();
        var step = (outerRadius - innerRadius) / phiSegments;
        for (var j = 0; j <= phiSegments; j++)
        {
            var radius = innerRadius + (step * j);
            for (var i = 0; i <= thetaSegments; i++)
            {
                var (sin, cos) = MathF.SinCos(thetaStart + ((float)i / thetaSegments * thetaLength));
                var x = radius * cos;
                var y = radius * sin;
                b.Add(new Vector3(x, y, 0f), Vector3.UnitZ, new Vector2(((x / outerRadius) + 1f) / 2f, ((y / outerRadius) + 1f) / 2f));
            }
        }

        var row = thetaSegments + 1;
        for (var j = 0; j < phiSegments; j++)
        {
            for (var i = 0; i < thetaSegments; i++)
            {
                var segment = i + (j * row);
                b.Quad(segment, segment + row, segment + row + 1, segment + 1);
            }
        }

        b.Apply(this);
    }
}
