using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class ShapeGeometryTests
{
    private static Dictionary<string, BufferGeometry> Solids() => new()
    {
        ["box"] = new BoxGeometry(1f, 2f, 3f, 2, 3, 4),
        ["sphere"] = new SphereGeometry(1f, 16, 8),
        ["cylinder"] = new CylinderGeometry(),
        ["cylinder tapered"] = new CylinderGeometry(0.5f, 1f, 2f, 12, 3),
        ["cone"] = new ConeGeometry(),
        ["capsule"] = new CapsuleGeometry(),
        ["capsule fine"] = new CapsuleGeometry(0.5f, 2f, 8, 16, 3),
        ["torus"] = new TorusGeometry(),
        ["torus knot"] = new TorusKnotGeometry(),
        ["torus knot 3,4"] = new TorusKnotGeometry(1f, 0.2f, 128, 8, 3, 4),
        ["tetrahedron"] = new TetrahedronGeometry(),
        ["octahedron"] = new OctahedronGeometry(),
        ["icosahedron"] = new IcosahedronGeometry(),
        ["dodecahedron"] = new DodecahedronGeometry(),
        ["icosahedron detail 2"] = new IcosahedronGeometry(1f, 2),
        ["lathe"] = new LatheGeometry(),
        ["tube closed"] = new TubeGeometry(new CatmullRomCurve3([new(1, 0, 0), new(0, 0.3f, 1), new(-1, 0, 0), new(0, -0.3f, -1)], closed: true), 48, 0.15f, 8, closed: true),
        ["rounded box"] = new RoundedBoxGeometry(2f, 1f, 1.5f, 3, 0.2f),
        ["extrude"] = new ExtrudeGeometry(Square(), new ExtrudeOptions { BevelEnabled = false }),
        ["extrude bevel"] = new ExtrudeGeometry(Square(), new ExtrudeOptions { Depth = 0.5f, Steps = 2 }),
        ["extrude hole"] = new ExtrudeGeometry(SquareWithHole(), new ExtrudeOptions { BevelEnabled = false, Depth = 0.4f }),
        ["extrude hole bevel"] = new ExtrudeGeometry(SquareWithHole(), new ExtrudeOptions { Depth = 0.4f, BevelThickness = 0.05f, BevelSize = 0.04f }),
    };

    private static Dictionary<string, BufferGeometry> Surfaces() => new()
    {
        ["plane"] = new PlaneGeometry(2f, 1f, 3, 2),
        ["circle"] = new CircleGeometry(),
        ["circle sector"] = new CircleGeometry(1f, 8, 0.3f, 2f),
        ["ring"] = new RingGeometry(),
        ["ring sector"] = new RingGeometry(0.2f, 1f, 6, 3, 1f, 3f),
        ["cylinder open"] = new CylinderGeometry(1f, 1f, 1f, 16, 2, openEnded: true),
        ["cylinder sector"] = new CylinderGeometry(1f, 0.5f, 1f, 8, 1, false, 0.5f, 3f),
        ["torus arc"] = new TorusGeometry(1f, 0.3f, 8, 16, MathF.PI),
        ["lathe open"] = new LatheGeometry([new(0.5f, -1f), new(1f, 0f), new(0.7f, 1f)], 16, 0f, MathF.PI),
        ["tube"] = new TubeGeometry(new CubicBezierCurve3(new(-1, 0, 0), new(-0.5f, 1, 0), new(0.5f, -1, 0.5f), new(1, 0, 0)), 20, 0.1f, 6),
        ["shape"] = new ShapeGeometry(SquareWithHole()),
        ["extrude along path"] = new ExtrudeGeometry(
            new Shape2([new(-0.1f, -0.1f), new(0.1f, -0.1f), new(0.1f, 0.1f), new(-0.1f, 0.1f)]),
            new ExtrudeOptions { Steps = 20, ExtrudePath = new CatmullRomCurve3([new(0, 0, 0), new(1, 0.5f, 0), new(2, 0, 1)]) }),
    };

    private static Shape2 Square() => new([new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f)]);

    private static Shape2 SquareWithHole()
    {
        var shape = new Shape2([new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f)]);
        shape.Holes.Add(new Path2([new(-0.4f, -0.4f), new(0.4f, -0.4f), new(0.4f, 0.4f), new(-0.4f, 0.4f)]));
        return shape;
    }

    [Fact]
    public void Every_Geometry_Is_Well_Formed_And_Wound_Counter_Clockwise()
    {
        foreach (var (name, g) in Solids().Concat(Surfaces()))
        {
            GeometryAssert.WellFormed(g, name);
            GeometryAssert.WindingMatchesNormals(g, name);
            g.BoundingBox.IsEmpty.ShouldBeFalse(name);
            g.BoundingSphere.Radius.ShouldBeGreaterThan(0f, name);
        }
    }

    [Fact]
    public void Closed_Geometries_Are_Watertight_And_Face_Outward()
    {
        foreach (var (name, g) in Solids())
        {
            GeometryAssert.Watertight(g, name);
        }
    }

    [Fact]
    public void Vertex_And_Index_Counts_Match_The_Formulas()
    {
        Count(new CylinderGeometry(), 196, 384);
        Count(new CylinderGeometry(1f, 1f, 1f, 8, 3, openEnded: true), 9 * 4, 8 * 3 * 6);
        Count(new ConeGeometry(), 131, 192);
        Count(new CapsuleGeometry(), 90, 384);
        Count(new TorusGeometry(), 13 * 49, 12 * 48 * 6);
        Count(new TorusKnotGeometry(), 65 * 9, 64 * 8 * 6);
        Count(new CircleGeometry(), 34, 96);
        Count(new RingGeometry(), 66, 192);
        Count(new RingGeometry(0.5f, 1f, 8, 3), 9 * 4, 8 * 3 * 6);
        Count(new TetrahedronGeometry(), 12, 12);
        Count(new OctahedronGeometry(), 24, 24);
        Count(new IcosahedronGeometry(), 60, 60);
        Count(new DodecahedronGeometry(), 108, 108);
        Count(new IcosahedronGeometry(1f, 2), 20 * 9 * 3, 20 * 9 * 3);
        Count(new LatheGeometry(), 13 * 3, 12 * 2 * 6);
        Count(new TubeGeometry(new LineCurve3(Vector3.Zero, Vector3.UnitX)), 65 * 9, 64 * 8 * 6);
        Count(new RoundedBoxGeometry(), 6 * 36, 6 * 25 * 6);
        Count(new ShapeGeometry(Square()), 4, 6);
        Count(new ShapeGeometry(SquareWithHole()), 8, 24);
        Count(new ExtrudeGeometry(Square(), new ExtrudeOptions { BevelEnabled = false }), 8 + 16, 12 + 24);

        static void Count(BufferGeometry g, int vertices, int indices)
        {
            g.VertexCount.ShouldBe(vertices, g.Name);
            g.IndexCount.ShouldBe(indices, g.Name);
        }
    }

    [Fact]
    public void Dimensions_Match_The_Parameters()
    {
        var cylinder = new CylinderGeometry(2f, 3f, 5f);
        cylinder.BoundingBox.Min.ShouldBeNear(new Vector3(-3f, -2.5f, -3f), 1e-3f);
        cylinder.BoundingBox.Max.ShouldBeNear(new Vector3(3f, 2.5f, 3f), 1e-3f);
        var capsule = new CapsuleGeometry(0.5f, 2f, 8, 16);
        capsule.BoundingBox.Max.Y.ShouldBe(1.5f, 1e-4f);
        capsule.BoundingBox.Min.Y.ShouldBe(-1.5f, 1e-4f);
        capsule.BoundingBox.Max.X.ShouldBe(0.5f, 1e-4f);
        var torus = new TorusGeometry(2f, 0.5f, 16, 64);
        torus.BoundingBox.Max.ShouldBeNear(new Vector3(2.5f, 2.5f, 0.5f), 1e-3f);
        var rounded = new RoundedBoxGeometry(2f, 1f, 4f, 4, 0.25f);
        rounded.BoundingBox.Min.ShouldBeNear(new Vector3(-1f, -0.5f, -2f), 1e-4f);
        rounded.BoundingBox.Max.ShouldBeNear(new Vector3(1f, 0.5f, 2f), 1e-4f);
        foreach (var solid in new BufferGeometry[] { new TetrahedronGeometry(2f), new OctahedronGeometry(2f), new IcosahedronGeometry(2f, 1), new DodecahedronGeometry(2f) })
        {
            var position = solid.GetAttribute(BufferGeometry.Position)!;
            for (var i = 0; i < position.Count; i++)
            {
                position.GetVector3(i).Length().ShouldBe(2f, 1e-4f, solid.Name);
            }
        }

        // Volumes of the exact solids.
        GeometryAssert.Volume(new BoxGeometry(1f, 2f, 3f)).ShouldBe(6.0, 1e-4);
        GeometryAssert.Volume(new CylinderGeometry(1f, 1f, 2f, 256)).ShouldBe(Math.PI * 2.0, 0.01);
        GeometryAssert.Volume(new ConeGeometry(1f, 3f, 256)).ShouldBe(Math.PI, 0.01);
        GeometryAssert.Volume(new ExtrudeGeometry(SquareWithHole(), new ExtrudeOptions { BevelEnabled = false, Depth = 0.5f })).ShouldBe((4.0 - 0.64) * 0.5, 1e-4);
        GeometryAssert.Area(new CircleGeometry(2f, 256)).ShouldBe(MathF.PI * 4f, 0.01f);
        GeometryAssert.Area(new RingGeometry(1f, 2f, 256)).ShouldBe(MathF.PI * 3f, 0.01f);
        GeometryAssert.Area(new ShapeGeometry(SquareWithHole())).ShouldBe(4f - 0.64f, 1e-4f);
    }

    [Fact]
    public void Texture_Coordinates_Stay_In_Range()
    {
        var unit = new BufferGeometry[]
        {
            new CylinderGeometry(), new ConeGeometry(), new CapsuleGeometry(), new TorusGeometry(), new TorusKnotGeometry(), new CircleGeometry(),
            new RingGeometry(), new LatheGeometry(), new RoundedBoxGeometry(), new TubeGeometry(new LineCurve3(Vector3.Zero, Vector3.UnitX)),
        };
        foreach (var g in unit)
        {
            var uv = g.GetAttribute(BufferGeometry.Uv)!.FloatData;
            foreach (var f in uv)
            {
                f.ShouldBeInRange(-1e-5f, 1f + 1e-5f, g.Name);
            }
        }

        // The seam of a polyhedron wraps past 1 so that no triangle spans the whole texture.
        var icosahedron = new IcosahedronGeometry(1f, 1).GetAttribute(BufferGeometry.Uv)!;
        for (var i = 0; i + 2 < icosahedron.Count; i += 3)
        {
            var (u0, u1, u2) = (icosahedron.Get(i, 0), icosahedron.Get(i + 1, 0), icosahedron.Get(i + 2, 0));
            (MathF.Max(u0, MathF.Max(u1, u2)) - MathF.Min(u0, MathF.Min(u1, u2))).ShouldBeLessThan(0.5f);
        }
    }

    [Fact]
    public void Material_Groups_Cover_The_Parts()
    {
        var cylinder = new CylinderGeometry(1f, 1f, 1f, 8, 2);
        cylinder.Groups.Select(g => g.MaterialIndex).ShouldBe([0, 1, 2]);
        cylinder.Groups.Sum(g => g.Count).ShouldBe(cylinder.IndexCount);
        cylinder.Groups[1].Start.ShouldBe(cylinder.Groups[0].Count);
        new ConeGeometry().Groups.Select(g => g.MaterialIndex).ShouldBe([0, 2]);
        new CylinderGeometry(openEnded: true).Groups.Count.ShouldBe(1);
        var extrude = new ExtrudeGeometry(Square());
        extrude.Groups.Select(g => g.MaterialIndex).ShouldBe([0, 1]);
        extrude.Groups.Sum(g => g.Count).ShouldBe(extrude.IndexCount);
        var shapes = new ShapeGeometry([Square(), SquareWithHole()]);
        shapes.Groups.Select(g => (g.Start, g.Count, g.MaterialIndex)).ShouldBe([(0, 6, 0), (6, 24, 1)]);
        new RoundedBoxGeometry().Groups.Count.ShouldBe(6);
    }

    [Fact]
    public void Lathe_Normals_Point_Away_From_The_Axis()
    {
        var lathe = new LatheGeometry([new(1f, -1f), new(1f, 1f)], 24);
        var position = lathe.GetAttribute(BufferGeometry.Position)!;
        var normal = lathe.GetAttribute(BufferGeometry.Normal)!;
        for (var i = 0; i < position.Count; i++)
        {
            var p = position.GetVector3(i);
            var radial = Vector3.Normalize(new Vector3(p.X, 0f, p.Z));
            Vector3.Dot(normal.GetVector3(i), radial).ShouldBe(1f, 1e-4f);
        }
    }

    [Fact]
    public void Bevel_Extends_The_Solid_By_Its_Size_And_Thickness()
    {
        var g = new ExtrudeGeometry(Square(), new ExtrudeOptions { Depth = 1f, BevelThickness = 0.2f, BevelSize = 0.1f, BevelSegments = 4 });
        g.BoundingBox.Min.ShouldBeNear(new Vector3(-0.6f, -0.6f, -0.2f), 1e-4f);
        g.BoundingBox.Max.ShouldBeNear(new Vector3(0.6f, 0.6f, 1.2f), 1e-4f);
        var flat = new ExtrudeGeometry(Square(), new ExtrudeOptions { Depth = 2f, BevelEnabled = false, Steps = 4 });
        flat.BoundingBox.Min.ShouldBeNear(new Vector3(-0.5f, -0.5f, 0f), 1e-5f);
        flat.BoundingBox.Max.ShouldBeNear(new Vector3(0.5f, 0.5f, 2f), 1e-5f);

        // A clockwise outline gives the same solid.
        var clockwise = new ExtrudeGeometry(new Shape2([new(-0.5f, 0.5f), new(0.5f, 0.5f), new(0.5f, -0.5f), new(-0.5f, -0.5f)]), new ExtrudeOptions { BevelEnabled = false });
        GeometryAssert.Watertight(clockwise, "clockwise extrude");
        GeometryAssert.Volume(clockwise).ShouldBe(1.0, 1e-5);
    }

    [Fact]
    public void Edges_Geometry_Keeps_Only_Hard_Edges()
    {
        var box = new EdgesGeometry(new BoxGeometry(1f, 1f, 1f, 3, 3, 3));
        box.Topology.ShouldBe(Rhi.PrimitiveTopology.Lines);

        // 12 box edges, each cut into 3 pieces by the segments.
        box.VertexCount.ShouldBe(12 * 3 * 2);
        new EdgesGeometry(new SphereGeometry(1f, 32, 16), 30f).VertexCount.ShouldBe(0);

        // A lone plane has only boundary edges.
        new EdgesGeometry(new PlaneGeometry(1f, 1f, 2, 2)).VertexCount.ShouldBe(8 * 2);
        new EdgesGeometry(new IcosahedronGeometry()).VertexCount.ShouldBe(30 * 2);
    }

    [Fact]
    public void Wireframe_Geometry_Lists_Every_Edge_Once()
    {
        var plane = new WireframeGeometry(new PlaneGeometry(1f, 1f, 1, 1));
        plane.Topology.ShouldBe(Rhi.PrimitiveTopology.Lines);
        plane.VertexCount.ShouldBe(5 * 2);
        new WireframeGeometry(new IcosahedronGeometry()).VertexCount.ShouldBe(30 * 2);
        new WireframeGeometry(new TetrahedronGeometry()).VertexCount.ShouldBe(6 * 2);
    }

    [Fact]
    public void Tube_Follows_Its_Path()
    {
        var path = new LineCurve3(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 4f));
        var tube = new TubeGeometry(path, 8, 0.5f, 12);
        tube.BoundingBox.Min.Z.ShouldBe(0f, 1e-4f);
        tube.BoundingBox.Max.Z.ShouldBe(4f, 1e-4f);
        var position = tube.GetAttribute(BufferGeometry.Position)!;
        for (var i = 0; i < position.Count; i++)
        {
            var p = position.GetVector3(i);
            MathF.Sqrt((p.X * p.X) + (p.Y * p.Y)).ShouldBe(0.5f, 1e-4f);
        }
    }
}
