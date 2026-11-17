using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceProjection"/>, the orbit and sphere views: measured projections and round trips.</summary>
public class ShowpieceProjectionTests
{
    private static double Rad(double degrees) => ShowpieceProjection.Radians(degrees);

    // Exploded view 4.1: fully exploded (rotateX 60°, rotateZ −40°, s 0.689, t (−87.28, −16.37)) on the 743×629 stage,
    // layer 2 (z = 1) anchor (140, 265.9) measured at (484.17, 338.39).
    private static readonly Point s_stageCenter = new(743 / 2.0, 629 / 2.0);
    private static readonly Vector s_t = new(-87.28, -16.37);
    private const double S = 0.689;

    private static ShowpieceTransform3D Layer2 =>
        ShowpieceTransform3D.Translation(0, 0, 1)
            .Then(ShowpieceTransform3D.RotationZ(Rad(-40)))
            .Then(ShowpieceTransform3D.RotationX(Rad(60)));

    [Fact]
    public void Exploded_View_Anchor_Projects_Where_It_Was_Measured()
    {
        var scene = Layer2.Transform(140, 265.9, 0);
        var p = ShowpieceProjection.Perspective(scene, 2400);
        var screen = new Point(s_stageCenter.X + s_t.X + S * p.X, s_stageCenter.Y + s_t.Y + S * p.Y);
        screen.X.ShouldBe(484.17, 0.3);
        screen.Y.ShouldBe(338.39, 0.3);
    }

    [Fact]
    public void Plane_Matrix_Is_The_Exact_Homography_Of_The_Projected_Layer()
    {
        var plane = ShowpieceProjection.PlaneMatrix(Layer2, 2400);
        var matrix = ShowpieceProjection.Multiply(plane, ShowpieceProjection.ScaleThenTranslate(S, (Vector)s_stageCenter + s_t));
        foreach (var local in new[] { new Point(140, 265.9), new Point(-140, -310), new Point(140, 310), new Point(0, 0) })
        {
            var scene = ShowpieceProjection.Perspective(Layer2.Transform(local.X, local.Y, 0), 2400);
            var expected = new Point(s_stageCenter.X + s_t.X + S * scene.X, s_stageCenter.Y + s_t.Y + S * scene.Y);
            var viaMatrix = ShowpieceProjection.Apply(matrix, local);
            viaMatrix.X.ShouldBe(expected.X, 1e-6);
            viaMatrix.Y.ShouldBe(expected.Y, 1e-6);
            // Avalonia's own transform agrees (row vectors, perspective in M13 / M23, w divide; it computes in float).
            var avalonia = matrix.Transform(local);
            avalonia.X.ShouldBe(expected.X, 1e-3);
            avalonia.Y.ShouldBe(expected.Y, 1e-3);
        }
        ShowpieceProjection.Apply(matrix, new Point(140, 265.9)).X.ShouldBe(484.17, 0.3);
    }

    [Fact]
    public void Perspective_Terms_Divide_By_W_As_Measured()
    {
        // showpieces.md 2.3: M13 = 0.0005, M23 = 0.0003 sends (200, 100) to (176.99, 88.50), w = 1.13.
        var m = new Matrix(1, 0, 0.0005, 0, 1, 0.0003, 0, 0, 1);
        var p = ShowpieceProjection.Apply(m, new Point(200, 100));
        p.X.ShouldBe(176.99, 0.01);
        p.Y.ShouldBe(88.50, 0.01);
        m.Transform(new Point(200, 100)).X.ShouldBe(176.99, 0.01);
    }

    [Fact]
    public void Homography_Maps_Four_Points_And_Unprojects_Back()
    {
        Span<Point> from = [new(0, 0), new(280, 0), new(280, 620), new(0, 620)];
        Span<Point> to = [new(120, 40), new(390, 80), new(350, 500), new(90, 420)];
        ShowpieceProjection.TryHomography(from, to, out var h).ShouldBeTrue();
        for (var i = 0; i < 4; i++)
        {
            var mapped = ShowpieceProjection.Apply(h, from[i]);
            mapped.X.ShouldBe(to[i].X, 1e-6);
            mapped.Y.ShouldBe(to[i].Y, 1e-6);
            ShowpieceProjection.TryUnproject(h, to[i], out var back).ShouldBeTrue();
            back.X.ShouldBe(from[i].X, 1e-6);
            back.Y.ShouldBe(from[i].Y, 1e-6);
        }
        // An interior point survives the round trip too.
        var inside = ShowpieceProjection.Apply(h, new Point(100, 300));
        ShowpieceProjection.TryUnproject(h, inside, out var p).ShouldBeTrue();
        p.X.ShouldBe(100, 1e-6);
        p.Y.ShouldBe(300, 1e-6);
        // Three points on a line have no homography.
        Span<Point> line = [new(0, 0), new(1, 1), new(2, 2), new(0, 5)];
        ShowpieceProjection.TryHomography(line, to, out _).ShouldBeFalse();
    }

    [Fact]
    public void Css_Rotations_Turn_The_Css_Way()
    {
        // rotateZ(90°) is clockwise on screen (y down): x → y.
        var z = ShowpieceProjection.RotateZ(new ShowpieceVec3(1, 0, 0), Rad(90));
        z.X.ShouldBe(0, 1e-12);
        z.Y.ShouldBe(1, 1e-12);
        // rotateX(90°): y → z (toward the viewer); rotateY(90°): z → x.
        ShowpieceProjection.RotateX(new ShowpieceVec3(0, 1, 0), Rad(90)).Z.ShouldBe(1, 1e-12);
        ShowpieceProjection.RotateY(new ShowpieceVec3(0, 0, 1), Rad(90)).X.ShouldBe(1, 1e-12);
        // Then() applies left to right and matches the point functions.
        var t = ShowpieceTransform3D.RotationY(0.3).Then(ShowpieceTransform3D.Translation(5, 6, 7));
        var p = t.Transform(1, 2, 3);
        var q = ShowpieceProjection.RotateY(new ShowpieceVec3(1, 2, 3), 0.3) + new ShowpieceVec3(5, 6, 7);
        (p - q).Length.ShouldBeLessThan(1e-12);
    }

    [Fact]
    public void Fit_Scales_Uniformly_And_Centers()
    {
        var fit = ShowpieceProjection.Fit(new Rect(0, 0, 200, 100), new Rect(0, 0, 400, 400));
        fit.Scale.ShouldBe(2);
        fit.Apply(new Point(0, 0)).ShouldBe(new Point(0, 100));
        fit.Apply(new Point(200, 100)).ShouldBe(new Point(400, 300));
        ShowpieceProjection.Fit(new Rect(0, 0, 200, 100), new Rect(0, 0, 400, 400), 0.5).Scale.ShouldBe(1);
        Span<Point> points = [new(3, -1), new(-2, 4), new(1, 1)];
        ShowpieceProjection.Bounds(points).ShouldBe(new Rect(-2, -1, 5, 5));
    }

    [Fact]
    public void Wrap_Angle_Lands_In_The_Half_Open_Range()
    {
        ShowpieceProjection.WrapAngle(Rad(370)).ShouldBe(Rad(10), 1e-12);
        ShowpieceProjection.WrapAngle(Rad(-190)).ShouldBe(Rad(170), 1e-12);
        ShowpieceProjection.WrapAngle(Math.PI).ShouldBe(Math.PI, 1e-12);
        ShowpieceProjection.WrapAngle(-Math.PI).ShouldBe(Math.PI, 1e-12);
    }

    [Fact]
    public void Orbit_View_Puts_The_Target_On_Its_Center_At_19_Px_Per_Unit()
    {
        // Activity terrain 4.1b: yaw 32°, pitch 30°, distance 42, f 801, target (0, 0.864, 0), center (W/2 + 16.31, H/2 − 24.83).
        var center = new Point(623 / 2.0 + 16.31, 292 / 2.0 - 24.83);
        var view = new ShowpieceOrbitView(Rad(32), Rad(30), 42, new ShowpieceVec3(0, 0.864, 0), 801, center);
        view.Project(new ShowpieceVec3(0, 0.864, 0), out var target, out var depth).ShouldBeTrue();
        target.X.ShouldBe(center.X, 1e-9);
        target.Y.ShouldBe(center.Y, 1e-9);
        depth.ShouldBe(42, 1e-9);
        // A small step along the camera's right axis at the target depth moves 801 / 42 = 19.07 px per unit.
        var right = new ShowpieceVec3(Math.Cos(Rad(32)), 0, Math.Sin(Rad(32))) * 0.01;
        view.Project(new ShowpieceVec3(0, 0.864, 0) + right, out var moved, out _);
        ((moved.X - center.X) / 0.01).ShouldBe(801 / 42.0, 0.01);
        // In the default view Mon (z = −3) is behind Sun (z = +3) and 00:00 (x = −11.5) is left of 23:00.
        view.DepthOf(new ShowpieceVec3(0, 0, -3)).ShouldBeGreaterThan(view.DepthOf(new ShowpieceVec3(0, 0, 3)));
        view.Project(new ShowpieceVec3(-11.5, 0, 0), out var left, out _);
        view.Project(new ShowpieceVec3(11.5, 0, 0), out var rightEnd, out _);
        left.X.ShouldBeLessThan(rightEnd.X);
    }

    [Fact]
    public void Orbit_View_Unprojects_Screen_Points_Onto_The_Floor()
    {
        var view = new ShowpieceOrbitView(Rad(32), Rad(30), 42, new ShowpieceVec3(0, 0.864, 0), 801, new Point(327.8, 121.2));
        var floor = new ShowpieceVec3(3.5, 0, -2);
        view.Project(floor, out var screen, out _).ShouldBeTrue();
        view.TryUnprojectToPlane(screen, 0, out var back).ShouldBeTrue();
        (back - floor).Length.ShouldBeLessThan(1e-6);
    }

    [Fact]
    public void Sphere_View_Projects_Orthographically_And_Unprojects()
    {
        // Revenue globe 4.1: center longitude λc, latitude φc (22° by default), R = 0.41·min(w, h).
        (0.41 * Math.Min(990, 580)).ShouldBe(237.8, 0.01);
        var radius = 0.41 * Math.Min(663, 437.46);
        radius.ShouldBe(179.36, 0.01);
        var view = new ShowpieceSphereView(Rad(-65), Rad(22), radius, new Point(331.5, 218.5));
        view.Project(22, -65, out var center).ShouldBe(1, 1e-12);
        center.X.ShouldBe(331.5, 1e-9);
        center.Y.ShouldBe(218.5, 1e-9);
        // x = cx + R·cos φ·sin(λ − λc), y = cy − R·(sin φ·cos φc − cos φ·sin φc·cos(λ − λc)).
        var depth = view.Project(39.74, -104.99, out var denver);
        var phi = Rad(39.74);
        var dl = Rad(-104.99 + 65);
        denver.X.ShouldBe(331.5 + radius * Math.Cos(phi) * Math.Sin(dl), 1e-9);
        denver.Y.ShouldBe(218.5 - radius * (Math.Sin(phi) * Math.Cos(Rad(22)) - Math.Cos(phi) * Math.Sin(Rad(22)) * Math.Cos(dl)), 1e-9);
        depth.ShouldBe(Math.Sin(phi) * Math.Sin(Rad(22)) + Math.Cos(phi) * Math.Cos(Rad(22)) * Math.Cos(dl), 1e-12);
        view.TryUnproject(denver, out var unit).ShouldBeTrue();
        (unit - ShowpieceProjection.LatLonToUnit(39.74, -104.99)).Length.ShouldBeLessThan(1e-9);
        // The far side has negative depth; outside the disc there is nothing to unproject.
        view.Project(-22, 115, out _).ShouldBeLessThan(0);
        view.TryUnproject(new Point(331.5 + radius + 1, 218.5), out _).ShouldBeFalse();
    }
}
