using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class TriangulatorTests
{
    private static float Check(Vector2[] contour, Vector2[][]? holes = null)
    {
        var all = new List<Vector2>(contour);
        foreach (var hole in holes ?? [])
        {
            all.AddRange(hole);
        }

        var triangles = ShapeTriangulator.Triangulate(contour, holes);
        (triangles.Length % 3).ShouldBe(0);
        var area = 0f;
        for (var i = 0; i < triangles.Length; i += 3)
        {
            triangles[i].ShouldBeInRange(0, all.Count - 1);
            var (a, b, c) = (all[triangles[i]], all[triangles[i + 1]], all[triangles[i + 2]]);
            var signed = (((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y))) * 0.5f;
            signed.ShouldBeGreaterThanOrEqualTo(-1e-6f, "every triangle is counter-clockwise");
            area += signed;
        }

        return area;
    }

    private static float PolygonArea(Vector2[] contour, Vector2[][]? holes = null)
    {
        var area = MathF.Abs(ShapeTriangulator.SignedArea(contour));
        foreach (var hole in holes ?? [])
        {
            area -= MathF.Abs(ShapeTriangulator.SignedArea(hole));
        }

        return area;
    }

    [Fact]
    public void Signed_Area_Tells_The_Winding()
    {
        Vector2[] ccw = [new(0, 0), new(2, 0), new(2, 1), new(0, 1)];
        ShapeTriangulator.SignedArea(ccw).ShouldBe(2f);
        ShapeTriangulator.IsClockwise(ccw).ShouldBeFalse();
        Array.Reverse(ccw);
        ShapeTriangulator.SignedArea(ccw).ShouldBe(-2f);
        ShapeTriangulator.IsClockwise(ccw).ShouldBeTrue();
    }

    [Fact]
    public void Convex_Polygon_Gives_N_Minus_Two_Triangles()
    {
        var hexagon = Enumerable.Range(0, 6).Select(i => new Vector2(MathF.Cos(i * MathF.PI / 3f), MathF.Sin(i * MathF.PI / 3f))).ToArray();
        ShapeTriangulator.Triangulate(hexagon).Length.ShouldBe(4 * 3);
        Check(hexagon).ShouldBe(PolygonArea(hexagon), 1e-5f);
    }

    [Fact]
    public void Either_Winding_Gives_Counter_Clockwise_Triangles()
    {
        Vector2[] l = [new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)];
        Check(l).ShouldBe(5f, 1e-5f);
        Array.Reverse(l);
        Check(l).ShouldBe(5f, 1e-5f);
    }

    [Fact]
    public void Concave_Polygons_Are_Covered_Exactly()
    {
        // A star: alternating outer and inner points.
        var star = Enumerable.Range(0, 10).Select(i =>
        {
            var r = i % 2 == 0 ? 2f : 0.8f;
            return new Vector2(r * MathF.Cos(i * MathF.PI / 5f), r * MathF.Sin(i * MathF.PI / 5f));
        }).ToArray();
        ShapeTriangulator.Triangulate(star).Length.ShouldBe(8 * 3);
        Check(star).ShouldBe(PolygonArea(star), 1e-4f);

        // A comb with deep notches.
        Vector2[] comb = [new(0, 0), new(7, 0), new(7, 3), new(6, 3), new(6, 1), new(5, 1), new(5, 3), new(4, 3), new(4, 1), new(3, 1), new(3, 3), new(2, 3), new(2, 1), new(1, 1), new(1, 3), new(0, 3)];
        Check(comb).ShouldBe(PolygonArea(comb), 1e-4f);

        // A spiral.
        Vector2[] spiral = [new(0, 0), new(5, 0), new(5, 5), new(1, 5), new(1, 2), new(3, 2), new(3, 3), new(2, 3), new(2, 4), new(4, 4), new(4, 1), new(0, 1)];
        Check(spiral).ShouldBe(PolygonArea(spiral), 1e-4f);
    }

    [Fact]
    public void Holes_Are_Left_Open()
    {
        Vector2[] outer = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        Vector2[][] holes =
        [
            [new(1, 1), new(4, 1), new(4, 4), new(1, 4)],
            [new(6, 6), new(6, 9), new(9, 9), new(9, 6)],
            [new(6, 1), new(8, 3), new(6, 5), new(5, 3)],
        ];
        Check(outer, holes).ShouldBe(PolygonArea(outer, holes), 1e-4f);

        // 16 points and three holes: n + 2h - 2 triangles.
        ShapeTriangulator.Triangulate(outer, holes).Length.ShouldBe((16 + 6 - 2) * 3);

        // No triangle covers the middle of a hole.
        var all = outer.Concat(holes.SelectMany(h => h)).ToArray();
        var triangles = ShapeTriangulator.Triangulate(outer, holes);
        foreach (var probe in new Vector2[] { new(2.5f, 2.5f), new(7.5f, 7.5f), new(6.5f, 3f) })
        {
            for (var i = 0; i < triangles.Length; i += 3)
            {
                Inside(probe, all[triangles[i]], all[triangles[i + 1]], all[triangles[i + 2]]).ShouldBeFalse();
            }
        }

        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            static float Cross(Vector2 o, Vector2 u, Vector2 v) => ((u.X - o.X) * (v.Y - o.Y)) - ((v.X - o.X) * (u.Y - o.Y));
            return Cross(a, b, p) > 1e-6f && Cross(b, c, p) > 1e-6f && Cross(c, a, p) > 1e-6f;
        }
    }

    [Fact]
    public void A_Hole_In_A_Concave_Outline_Is_Bridged_Correctly()
    {
        // A "C" shape with a hole in its spine: the bridge must not cross the notch.
        Vector2[] outer = [new(0, 0), new(6, 0), new(6, 2), new(3, 2), new(3, 4), new(6, 4), new(6, 6), new(0, 6)];
        Vector2[][] holes = [[new(1, 2.5f), new(2, 2.5f), new(2, 3.5f), new(1, 3.5f)]];
        Check(outer, holes).ShouldBe(PolygonArea(outer, holes), 1e-4f);
    }

    [Fact]
    public void Collinear_And_Repeated_Points_Do_Not_Break_It()
    {
        Vector2[] square = [new(0, 0), new(1, 0), new(2, 0), new(2, 0), new(2, 1), new(2, 2), new(1, 2), new(0, 2), new(0, 2), new(0, 1)];
        Check(square).ShouldBe(4f, 1e-5f);

        // The same point at the start and the end.
        Vector2[] closed = [new(0, 0), new(2, 0), new(2, 2), new(0, 2), new(0, 0)];
        Check(closed).ShouldBe(4f, 1e-5f);
    }

    [Fact]
    public void Degenerate_Input_Gives_No_Triangles()
    {
        ShapeTriangulator.Triangulate([]).ShouldBeEmpty();
        ShapeTriangulator.Triangulate([new(0, 0), new(1, 1)]).ShouldBeEmpty();
        ShapeTriangulator.Triangulate([new(0, 0), new(1, 1), new(2, 2)]).ShouldBeEmpty();
    }

    [Fact]
    public void A_Circle_With_Many_Points_And_A_Round_Hole()
    {
        var outer = Enumerable.Range(0, 200).Select(i => new Vector2(5f * MathF.Cos(i * MathF.PI / 100f), 5f * MathF.Sin(i * MathF.PI / 100f))).ToArray();
        Vector2[][] holes = [Enumerable.Range(0, 100).Select(i => new Vector2(2f * MathF.Cos(-i * MathF.PI / 50f), 2f * MathF.Sin(-i * MathF.PI / 50f))).ToArray()];
        Check(outer, holes).ShouldBe(PolygonArea(outer, holes), 1e-2f);
    }
}
