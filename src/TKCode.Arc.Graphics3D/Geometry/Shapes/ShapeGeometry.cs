using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Flat polygons (with holes) in the XY plane facing +Z. Each shape is a material group; UVs are the XY
/// coordinates.</summary>
public class ShapeGeometry : BufferGeometry
{
    /// <summary>Creates the geometry of one shape.</summary>
    public ShapeGeometry(Shape2 shape, int curveSegments = 12)
        : this([shape ?? throw new ArgumentNullException(nameof(shape))], curveSegments)
    {
    }

    /// <summary>Creates the geometry of several shapes.</summary>
    public ShapeGeometry(IReadOnlyList<Shape2> shapes, int curveSegments = 12)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        Name = "shape";
        var b = new GeometryBuilder();
        var groupStart = 0;
        for (var s = 0; s < shapes.Count; s++)
        {
            var (contour, holes) = shapes[s].ExtractPoints(curveSegments);
            var offset = b.VertexCount;
            var triangles = ShapeTriangulator.Triangulate(contour, holes);
            foreach (var p in contour)
            {
                b.Add(new Vector3(p, 0f), Vector3.UnitZ, p);
            }

            foreach (var hole in holes)
            {
                foreach (var p in hole)
                {
                    b.Add(new Vector3(p, 0f), Vector3.UnitZ, p);
                }
            }

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                b.Triangle(offset + triangles[i], offset + triangles[i + 1], offset + triangles[i + 2]);
            }

            AddGroup(groupStart, triangles.Length, s);
            groupStart += triangles.Length;
        }

        b.Apply(this);
    }
}

/// <summary>Settings of <see cref="ExtrudeGeometry"/>.</summary>
public sealed class ExtrudeOptions
{
    /// <summary>Points per curve of the outline.</summary>
    public int CurveSegments { get; set; } = 12;

    /// <summary>Slices along the depth.</summary>
    public int Steps { get; set; } = 1;

    /// <summary>How far the shape is pushed along +Z.</summary>
    public float Depth { get; set; } = 1f;

    /// <summary>True rounds the front and back edges.</summary>
    public bool BevelEnabled { get; set; } = true;

    /// <summary>How deep the bevel goes into the depth direction, on each side.</summary>
    public float BevelThickness { get; set; } = 0.2f;

    /// <summary>How far the bevel extends outward from the outline.</summary>
    public float BevelSize { get; set; } = 0.1f;

    /// <summary>Distance from the outline at which the bevel starts.</summary>
    public float BevelOffset { get; set; }

    /// <summary>Bevel layers; more are rounder.</summary>
    public int BevelSegments { get; set; } = 3;

    /// <summary>When set, the shape is swept along this curve instead of straight along Z (no bevel).</summary>
    public Curve3? ExtrudePath { get; set; }
}

/// <summary>A shape (with holes) pushed into the third dimension, with optional beveled edges, or swept along a
/// curve. Material groups: 0 the front and back faces, 1 the sides.</summary>
public class ExtrudeGeometry : BufferGeometry
{
    /// <summary>Extrudes one shape.</summary>
    public ExtrudeGeometry(Shape2 shape, ExtrudeOptions? options = null)
        : this([shape ?? throw new ArgumentNullException(nameof(shape))], options)
    {
    }

    /// <summary>Extrudes several shapes.</summary>
    public ExtrudeGeometry(IReadOnlyList<Shape2> shapes, ExtrudeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        Name = "extrude";
        options ??= new ExtrudeOptions();
        var lids = new GeometryBuilder();
        var sides = new GeometryBuilder();
        foreach (var shape in shapes)
        {
            Build(shape, options, lids, sides);
        }

        // One vertex list, lids first.
        var lidVertices = lids.VertexCount;
        lids.Positions.AddRange(sides.Positions);
        lids.Normals.AddRange(sides.Normals);
        lids.Uvs.AddRange(sides.Uvs);
        var lidIndices = lids.Indices.Count;
        foreach (var i in sides.Indices)
        {
            lids.Indices.Add(i + lidVertices);
        }

        AddGroup(0, lidIndices, 0);
        AddGroup(lidIndices, sides.Indices.Count, 1);
        lids.Apply(this);
    }

    private static void Build(Shape2 shape, ExtrudeOptions o, GeometryBuilder lids, GeometryBuilder sides)
    {
        var (contour, holes) = shape.ExtractPoints(o.CurveSegments);
        if (contour.Length < 3)
        {
            return;
        }

        // The solid is on the left of every ring: outline counter-clockwise, holes clockwise.
        if (ShapeTriangulator.IsClockwise(contour))
        {
            Array.Reverse(contour);
        }

        foreach (var hole in holes)
        {
            if (!ShapeTriangulator.IsClockwise(hole))
            {
                Array.Reverse(hole);
            }
        }

        var triangles = ShapeTriangulator.Triangulate(contour, holes);
        var rings = new List<Vector2[]>(holes.Length + 1) { contour };
        rings.AddRange(holes);
        var path = o.ExtrudePath;
        var steps = Math.Max(1, o.Steps);
        var bevel = o.BevelEnabled && path is null;
        var bevelSegments = bevel ? Math.Max(0, o.BevelSegments) : 0;
        var bevelThickness = bevel ? o.BevelThickness : 0f;
        var bevelSize = bevel ? o.BevelSize : 0f;
        var bevelOffset = bevel ? o.BevelOffset : 0f;

        // The layers from back to front: (outward offset of the outline, z).
        var layers = new List<(float Offset, float Z)>();
        for (var i = 0; i < bevelSegments; i++)
        {
            var t = (float)i / bevelSegments;
            layers.Add(((bevelSize * MathF.Sin(t * MathF.PI / 2f)) + bevelOffset, -bevelThickness * MathF.Cos(t * MathF.PI / 2f)));
        }

        for (var s = 0; s <= steps; s++)
        {
            layers.Add((bevelSize + bevelOffset, o.Depth / steps * s));
        }

        for (var i = bevelSegments - 1; i >= 0; i--)
        {
            var t = (float)i / bevelSegments;
            layers.Add(((bevelSize * MathF.Sin(t * MathF.PI / 2f)) + bevelOffset, o.Depth + (bevelThickness * MathF.Cos(t * MathF.PI / 2f))));
        }

        Vector3[]? pathPoints = null;
        FrenetFrames? frames = null;
        if (path is not null)
        {
            pathPoints = path.GetSpacedPoints(steps);
            frames = path.ComputeFrenetFrames(steps, false);
        }

        // Per-vertex direction that moves both adjacent edges outward by one unit.
        var miters = new Vector2[rings.Count][];
        for (var r = 0; r < rings.Count; r++)
        {
            miters[r] = Miters(rings[r]);
        }

        Vector3 Place(Vector2 p, int layer)
        {
            if (pathPoints is not null)
            {
                return pathPoints[layer] + (frames!.Normals[layer] * p.X) + (frames.Binormals[layer] * p.Y);
            }

            return new Vector3(p, layers[layer].Z);
        }

        Vector2 Offset(int ring, int i, int layer) => rings[ring][i] + (miters[ring][i] * layers[layer].Offset);

        // Lids: the back one faces backward, so its triangles are reversed.
        var last = layers.Count - 1;
        for (var side = 0; side < 2; side++)
        {
            var layer = side == 0 ? 0 : last;
            var first = lids.VertexCount;
            Vector3 normal;
            if (frames is not null)
            {
                normal = frames.Tangents[layer] * (side == 0 ? -1f : 1f);
            }
            else
            {
                normal = side == 0 ? -Vector3.UnitZ : Vector3.UnitZ;
            }

            for (var r = 0; r < rings.Count; r++)
            {
                for (var i = 0; i < rings[r].Length; i++)
                {
                    var p = Offset(r, i, layer);
                    lids.Add(Place(p, layer), normal, p);
                }
            }

            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                if (side == 0)
                {
                    lids.Triangle(first + triangles[i + 2], first + triangles[i + 1], first + triangles[i]);
                }
                else
                {
                    lids.Triangle(first + triangles[i], first + triangles[i + 1], first + triangles[i + 2]);
                }
            }
        }

        // Sides: one flat quad per outline edge and layer.
        for (var r = 0; r < rings.Count; r++)
        {
            var ring = rings[r];
            for (var i = 0; i < ring.Length; i++)
            {
                var j = (i + 1) % ring.Length;
                for (var layer = 0; layer < last; layer++)
                {
                    var a = Place(Offset(r, i, layer), layer);
                    var b = Place(Offset(r, j, layer), layer);
                    var c = Place(Offset(r, j, layer + 1), layer + 1);
                    var d = Place(Offset(r, i, layer + 1), layer + 1);
                    var normal = Vector3.Cross(b - a, d - a);
                    if (normal.LengthSquared() < 1e-20f)
                    {
                        normal = Vector3.Cross(c - b, d - b);
                    }

                    if (normal.LengthSquared() < 1e-20f)
                    {
                        continue;
                    }

                    normal = Vector3.Normalize(normal);
                    var alongX = MathF.Abs(a.Y - b.Y) < MathF.Abs(a.X - b.X);
                    Vector2 Uv(Vector3 p) => new(alongX ? p.X : p.Y, 1f - p.Z);
                    var v = sides.VertexCount;
                    sides.Add(a, normal, Uv(a));
                    sides.Add(b, normal, Uv(b));
                    sides.Add(c, normal, Uv(c));
                    sides.Add(d, normal, Uv(d));
                    sides.Triangle(v, v + 1, v + 2);
                    sides.Triangle(v, v + 2, v + 3);
                }
            }
        }
    }

    private static Vector2[] Miters(Vector2[] ring)
    {
        var n = ring.Length;
        var result = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            var prev = ring[(i + n - 1) % n];
            var cur = ring[i];
            var next = ring[(i + 1) % n];
            var n1 = Outward(cur - prev);
            var n2 = Outward(next - cur);
            if (n1 == Vector2.Zero)
            {
                n1 = n2;
            }
            else if (n2 == Vector2.Zero)
            {
                n2 = n1;
            }

            var sum = n1 + n2;
            if (sum.LengthSquared() < 1e-12f)
            {
                // A spike: the edges fold back on themselves.
                result[i] = n1;
                continue;
            }

            var bisector = Vector2.Normalize(sum);
            // Keep both edges at distance one, but do not let sharp corners shoot out.
            result[i] = bisector / MathF.Max(Vector2.Dot(bisector, n1), 0.25f);
        }

        return result;
    }

    /// <summary>The unit normal on the right of a direction: outward for a ring with the solid on its left.</summary>
    private static Vector2 Outward(Vector2 direction)
    {
        var n = new Vector2(direction.Y, -direction.X);
        return n.LengthSquared() > 0f ? Vector2.Normalize(n) : Vector2.Zero;
    }
}
