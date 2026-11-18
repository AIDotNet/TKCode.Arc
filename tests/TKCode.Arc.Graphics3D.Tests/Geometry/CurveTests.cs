using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class CurveTests
{
    [Fact]
    public void Line_Curve_Is_Linear()
    {
        var line = new LineCurve3(new Vector3(1f, 2f, 3f), new Vector3(4f, 6f, 3f));
        line.GetLength().ShouldBe(5f, 1e-4f);
        line.GetPoint(0.5f).ShouldBeNear(new Vector3(2.5f, 4f, 3f));
        line.GetPointAt(0.25f).ShouldBeNear(new Vector3(1.75f, 3f, 3f));
        line.GetTangent(0.3f).ShouldBeNear(new Vector3(0.6f, 0.8f, 0f));
        line.GetPoints(4).Length.ShouldBe(5);
        new LineCurve2(new Vector2(0f, 0f), new Vector2(3f, 4f)).GetLength().ShouldBe(5f, 1e-4f);
    }

    [Fact]
    public void Bezier_Curves_Start_And_End_On_Their_End_Points()
    {
        var quadratic = new QuadraticBezierCurve3(new Vector3(0f, 0f, 0f), new Vector3(1f, 2f, 0f), new Vector3(2f, 0f, 0f));
        quadratic.GetPoint(0f).ShouldBeNear(Vector3.Zero);
        quadratic.GetPoint(1f).ShouldBeNear(new Vector3(2f, 0f, 0f));
        quadratic.GetPoint(0.5f).ShouldBeNear(new Vector3(1f, 1f, 0f));
        quadratic.GetTangent(0f).ShouldBeNear(Vector3.Normalize(new Vector3(1f, 2f, 0f)), 1e-2f);
        var cubic = new CubicBezierCurve3(new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 0f));
        cubic.GetPoint(0f).ShouldBeNear(Vector3.Zero);
        cubic.GetPoint(1f).ShouldBeNear(new Vector3(1f, 0f, 0f));
        cubic.GetPoint(0.5f).ShouldBeNear(new Vector3(0.5f, 0.75f, 0f));

        // A straight "curve": its length is the distance.
        new CubicBezierCurve2(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f), new Vector2(3f, 0f)).GetLength().ShouldBe(3f, 1e-4f);
        new QuadraticBezierCurve2(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f)).GetPoint(0.5f).ShouldBeNear(new Vector2(1f, 0.5f));
    }

    [Fact]
    public void Ellipse_Curve_Measures_A_Circle()
    {
        var circle = new EllipseCurve(1f, 2f, 3f, 3f);
        circle.GetLength().ShouldBe(MathF.PI * 6f, 0.01f);
        circle.GetPoint(0f).ShouldBeNear(new Vector2(4f, 2f));
        circle.GetPoint(0.25f).ShouldBeNear(new Vector2(1f, 5f));

        // Clockwise runs the other way.
        new EllipseCurve(0f, 0f, 1f, 1f, 0f, MathF.PI / 2f, clockwise: true).GetPoint(0.5f).ShouldBeNear(new Vector2(MathF.Cos(-0.75f * MathF.PI), MathF.Sin(-0.75f * MathF.PI)), 1e-4f);
        new ArcCurve(0f, 0f, 2f, 0f, MathF.PI).GetLength().ShouldBe(MathF.PI * 2f, 0.01f);

        // Rotated by a quarter turn, the long axis points up.
        new EllipseCurve(0f, 0f, 2f, 1f, rotation: MathF.PI / 2f).GetPoint(0f).ShouldBeNear(new Vector2(0f, 2f));
    }

    [Fact]
    public void Spaced_Points_Are_Evenly_Spaced()
    {
        // A curve whose parameter speed varies a lot.
        var curve = new CubicBezierCurve3(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 5f, 0f));
        var points = curve.GetSpacedPoints(20);
        var step = curve.GetLength() / 20f;
        for (var i = 1; i < points.Length; i++)
        {
            Vector3.Distance(points[i], points[i - 1]).ShouldBe(step, step * 0.05f);
        }

        // By parameter they are not.
        var uneven = curve.GetPoints(20);
        var first = Vector3.Distance(uneven[1], uneven[0]);
        var middle = Vector3.Distance(uneven[11], uneven[10]);
        (middle / first).ShouldBeGreaterThan(3f);
        curve.GetUtoTMapping(0f).ShouldBe(0f);
        curve.GetUtoTMapping(1f).ShouldBe(1f);
    }

    [Fact]
    public void Catmull_Rom_Passes_Through_Its_Points()
    {
        Vector3[] points = [new(0f, 0f, 0f), new(1f, 2f, 0f), new(3f, 1f, 1f), new(4f, 0f, 0f)];
        foreach (var type in new[] { CatmullRomType.Centripetal, CatmullRomType.Chordal, CatmullRomType.CatmullRom })
        {
            var open = new CatmullRomCurve3(points, false, type);
            for (var i = 0; i < points.Length; i++)
            {
                open.GetPoint(i / 3f).ShouldBeNear(points[i], 1e-4f, type.ToString());
            }

            var closed = new CatmullRomCurve3(points, true, type);
            for (var i = 0; i < points.Length; i++)
            {
                closed.GetPoint(i / 4f).ShouldBeNear(points[i], 1e-4f, type.ToString());
            }

            closed.GetPoint(1f).ShouldBeNear(points[0], 1e-4f, type.ToString());
            closed.GetLength().ShouldBeGreaterThan(open.GetLength());
        }
    }

    [Fact]
    public void Curve_Path_Joins_Its_Pieces()
    {
        var path = new CurvePath3()
            .Add(new LineCurve3(new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f)))
            .Add(new LineCurve3(new Vector3(2f, 0f, 0f), new Vector3(2f, 3f, 0f)));
        path.GetLength().ShouldBe(5f, 1e-4f);
        path.GetPoint(0.4f).ShouldBeNear(new Vector3(2f, 0f, 0f));
        path.GetPoint(0.7f).ShouldBeNear(new Vector3(2f, 1.5f, 0f));
        path.ClosePath();
        path.Curves.Count.ShouldBe(3);
        path.GetLength().ShouldBe(5f + MathF.Sqrt(13f), 1e-3f);
        path.GetPoint(1f).ShouldBeNear(Vector3.Zero);
    }

    [Fact]
    public void Frenet_Frames_Are_Orthonormal_And_Follow_The_Curve()
    {
        var helix = new CatmullRomCurve3(Enumerable.Range(0, 40).Select(i => new Vector3(MathF.Cos(i * 0.4f), i * 0.1f, MathF.Sin(i * 0.4f))).ToArray());
        var frames = helix.ComputeFrenetFrames(64);
        frames.Tangents.Length.ShouldBe(65);
        for (var i = 0; i <= 64; i++)
        {
            frames.Tangents[i].Length().ShouldBe(1f, 1e-3f);
            frames.Normals[i].Length().ShouldBe(1f, 1e-3f);
            frames.Binormals[i].Length().ShouldBe(1f, 1e-3f);
            Vector3.Dot(frames.Tangents[i], frames.Normals[i]).ShouldBe(0f, 2e-2f);
            Vector3.Dot(frames.Tangents[i], frames.Binormals[i]).ShouldBe(0f, 2e-2f);
            Vector3.Dot(frames.Normals[i], frames.Binormals[i]).ShouldBe(0f, 2e-2f);
            Vector3.Cross(frames.Tangents[i], frames.Normals[i]).ShouldBeNear(frames.Binormals[i], 2e-2f);
            if (i > 0)
            {
                // No sudden twist between neighbours.
                Vector3.Dot(frames.Normals[i], frames.Normals[i - 1]).ShouldBeGreaterThan(0.8f);
            }
        }
    }

    [Fact]
    public void Closed_Frames_Meet_Where_The_Curve_Closes()
    {
        var loop = new CatmullRomCurve3([new(1f, 0f, 0f), new(0f, 0.6f, 1f), new(-1f, 0f, 0f), new(0f, -0.6f, -1f)], closed: true);
        var frames = loop.ComputeFrenetFrames(48, closed: true);
        frames.Normals[48].ShouldBeNear(frames.Normals[0], 2e-2f);
        frames.Binormals[48].ShouldBeNear(frames.Binormals[0], 2e-2f);
    }

    [Fact]
    public void Path_Commands_Build_An_Outline()
    {
        var path = new Path2().MoveTo(0f, 0f).LineTo(2f, 0f).LineTo(2f, 1f).LineTo(0f, 1f).ClosePath();
        path.Curves.Count.ShouldBe(4);
        path.GetLength().ShouldBe(6f, 1e-4f);
        path.CurrentPoint.ShouldBe(Vector2.Zero);
        var points = path.GetPoints();
        points.Length.ShouldBe(5);
        points[^1].ShouldBe(points[0]);

        // Curves are cut into pieces, lines are not; joints are not repeated.
        var rounded = new Path2().MoveTo(0f, 0f).LineTo(1f, 0f).QuadraticCurveTo(2f, 0f, 2f, 1f).BezierCurveTo(2f, 2f, 1f, 2f, 0f, 2f);
        rounded.GetPoints(6).Length.ShouldBe(1 + 1 + 6 + 6);
        rounded.CurrentPoint.ShouldBe(new Vector2(0f, 2f));

        // An arc is joined to the current point by a line.
        var arc = new Path2().MoveTo(0f, 0f).LineTo(1f, 0f).AbsArc(3f, 0f, 1f, MathF.PI, 0f, true);
        arc.Curves.Count.ShouldBe(3);
        arc.CurrentPoint.ShouldBeNear(new Vector2(4f, 0f));
        arc.GetPoint(1f).ShouldBeNear(new Vector2(4f, 0f));

        // Relative arc: the center is given from the current point.
        new Path2().MoveTo(5f, 5f).Arc(1f, 0f, 1f, MathF.PI, 0f).CurrentPoint.ShouldBeNear(new Vector2(7f, 5f));
        new Path2().MoveTo(0f, 0f).SplineThru([new(1f, 1f), new(2f, 0f)]).CurrentPoint.ShouldBe(new Vector2(2f, 0f));
    }

    [Fact]
    public void Shape_Extracts_Outline_And_Holes()
    {
        var shape = new Shape2();
        shape.AbsArc(0f, 0f, 2f, 0f, MathF.PI * 2f);
        var hole = new Path2();
        hole.AbsArc(0f, 0f, 1f, 0f, MathF.PI * 2f, true);
        shape.Holes.Add(hole);
        var (outline, holes) = shape.ExtractPoints(16);

        // The closing point is not repeated.
        outline.Length.ShouldBe(32);
        holes.Length.ShouldBe(1);
        holes[0].Length.ShouldBe(32);
        ShapeTriangulator.IsClockwise(outline).ShouldBeFalse();
        ShapeTriangulator.IsClockwise(holes[0]).ShouldBeTrue();
        GeometryAssert.Area(new ShapeGeometry(shape, 64)).ShouldBe(MathF.PI * 3f, 0.02f);
        var spline = new SplineCurve2([new(0f, 0f), new(1f, 1f), new(2f, 0f)]);
        spline.GetPoint(0.5f).ShouldBeNear(new Vector2(1f, 1f));
        spline.GetPoint(1f).ShouldBeNear(new Vector2(2f, 0f));
    }
}
