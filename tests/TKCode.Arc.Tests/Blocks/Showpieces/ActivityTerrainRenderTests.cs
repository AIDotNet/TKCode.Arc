using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// How the terrain draws its bars: every face is one quad (the unit square through a projective matrix) pushed out a
/// little past its edges so neighbors overlap without a seam, faces too thin to see are skipped, and moving frames build
/// no geometry.
/// </summary>
public class ActivityTerrainRenderTests
{
    private const double Outset = ActivityTerrainQuad.SeamOutset;

    // Quad math ------------------------------------------------------------------------------------------------------------

    public static TheoryData<double[]> Quads => new()
    {
        new double[] { 0, 0, 10, 0, 10, 5, 0, 5 },                // rectangle
        new double[] { 2, 1, 12, 3, 14, 9, 4, 7 },                // parallelogram (affine)
        new double[] { 100, 200, 140, 190, 152, 230, 98, 251 },   // a projected side: perspective
        new double[] { 5, 5, 3, 25, 40, 30, 38, 4 },              // clockwise on screen
        new double[] { 0, 0, 30, 2, 31, 3.5, 1, 1.4 }             // a long sliver
    };

    [Theory]
    [MemberData(nameof(Quads))]
    public void Unit_Square_Maps_Onto_The_Quad(double[] q)
    {
        var (a, b, c, d) = Corners(q);
        ActivityTerrainQuad.TryUnitSquareTo(a, b, c, d, out var m).ShouldBeTrue();
        Distance(Map(m, 0, 0), a).ShouldBeLessThan(1e-9);
        Distance(Map(m, 1, 0), b).ShouldBeLessThan(1e-9);
        Distance(Map(m, 1, 1), c).ShouldBeLessThan(1e-9);
        Distance(Map(m, 0, 1), d).ShouldBeLessThan(1e-9);
        // Inside stays inside: the square's center lands within the quad.
        Inside(Map(m, 0.5, 0.5), a, b, c, d, 0).ShouldBeTrue();
    }

    [Fact]
    public void Degenerate_Quads_Have_No_Matrix_And_No_Outset()
    {
        // A segment traced twice: a side of a bar of zero height.
        Point a = new(0, 0), b = new(10, 2), c = new(10, 2), d = new(0, 0);
        ActivityTerrainQuad.TryUnitSquareTo(a, b, c, d, out _).ShouldBeFalse();
        ActivityTerrainQuad.TryOutset(ref a, ref b, ref c, ref d, Outset).ShouldBeFalse();
        a.ShouldBe(new Point(0, 0));
        ActivityTerrainQuad.Thickness(a, b, c, d).ShouldBe(0);
        // Three corners on a line.
        ActivityTerrainQuad.TryUnitSquareTo(new Point(0, 0), new Point(5, 0), new Point(10, 0), new Point(3, 4), out _).ShouldBeFalse();
    }

    [Fact]
    public void Thickness_Is_The_Width_Across()
    {
        ActivityTerrainQuad.Thickness(new Point(0, 0), new Point(20, 0), new Point(20, 3), new Point(0, 3)).ShouldBe(3, 1e-12);
        ActivityTerrainQuad.Thickness(new Point(0, 0), new Point(0.04, 0), new Point(0.04, 30), new Point(0, 30)).ShouldBe(0.04, 1e-12);
        (ActivityTerrainQuad.Thickness(new Point(0, 0), new Point(30, 0), new Point(30, 0.01), new Point(0, 0.01)) <
         ActivityTerrainQuad.MinThickness).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Quads))]
    public void Outset_Pushes_Every_Edge_Out_By_The_Seam_Outset(double[] q)
    {
        var (a, b, c, d) = Corners(q);
        var (oa, ob, oc, od) = (a, b, c, d);
        ActivityTerrainQuad.TryOutset(ref oa, ref ob, ref oc, ref od, Outset).ShouldBeTrue();
        Point[] original = [a, b, c, d];
        Point[] pushed = [oa, ob, oc, od];
        for (var k = 0; k < 4; k++)
        {
            // Corners move out, at most the miter limit.
            Distance(original[k], pushed[k]).ShouldBeGreaterThan(Outset * 0.99);
            Distance(original[k], pushed[k]).ShouldBeLessThanOrEqualTo(ActivityTerrainQuad.MiterLimit * Outset + 1e-9);
            Inside(original[k], oa, ob, oc, od, 0).ShouldBeTrue();
            // Each edge's middle is covered a little past the edge and no further than the outset.
            var p = original[k];
            var r = original[(k + 1) % 4];
            var middle = p + (r - p) / 2;
            var normal = OutwardNormal(original, k);
            Inside(middle + normal * (Outset * 0.5), oa, ob, oc, od, 0).ShouldBeTrue();
            Inside(middle + normal * (Outset + 0.02), oa, ob, oc, od, 0).ShouldBeFalse();
        }
    }

    [Fact]
    public void Acute_Corners_Stop_At_The_Miter_Limit()
    {
        // A top face seen from low down: a flat rhombus with 12° corners left and right.
        var half = Math.Tan(ActivityTerrainMath.Radians(6)) * 40;
        Point a = new(0, 0), b = new(40, -half), c = new(80, 0), d = new(40, half);
        var (oa, oc) = (a, c);
        var (ob, od) = (b, d);
        ActivityTerrainQuad.TryOutset(ref oa, ref ob, ref oc, ref od, Outset).ShouldBeTrue();
        Distance(a, oa).ShouldBe(ActivityTerrainQuad.MiterLimit * Outset, 1e-9);
        Distance(c, oc).ShouldBe(ActivityTerrainQuad.MiterLimit * Outset, 1e-9);
        Inside(new Point(40, 0), oa, ob, oc, od, 0).ShouldBeTrue();
    }

    [Fact]
    public void A_Zero_Length_Edge_Still_Pushes_Out()
    {
        // A triangle written as a quad (two corners equal): the corners split apart and the quad gets a matrix.
        Point a = new(0, 0), b = new(10, 0), c = new(10, 0), d = new(0, 8);
        ActivityTerrainQuad.TryOutset(ref a, ref b, ref c, ref d, Outset).ShouldBeTrue();
        Distance(b, c).ShouldBeGreaterThan(0.1);
        ActivityTerrainQuad.TryUnitSquareTo(a, b, c, d, out _).ShouldBeTrue();
    }

    // The scene ------------------------------------------------------------------------------------------------------------

    public static TheoryData<string> Poses => ["rest", "behind", "steep", "low", "top", "highlight"];

    [AvaloniaTheory]
    [MemberData(nameof(Poses))]
    public void Faces_Overlap_Their_Neighbors_And_Keep_Their_Size(string pose)
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = terrain.Canvas.ShouldNotBeNull();
            Pose(terrain, canvas, pose);
            Render(canvas);
            var drawn = 0;
            var shared = 0;
            Span<bool> has = stackalloc bool[3];
            var quads = new (Point A, Point B, Point C, Point D, Avalonia.Matrix M)[3];
            for (var i = 0; i < ActivityTerrainMath.Cells; i++)
            {
                for (var f = 0; f < 3; f++)
                {
                    has[f] = canvas.TryGetDrawnFace(i, f, out var a, out var b, out var c, out var d, out var m);
                    if (!has[f])
                    {
                        continue;
                    }
                    drawn++;
                    quads[f] = (a, b, c, d, m);
                    Point[] geometric = [a, b, c, d];
                    var (da, db, dc, dd) = (Map(m, 0, 0), Map(m, 1, 0), Map(m, 1, 1), Map(m, 0, 1));
                    Point[] drawnCorners = [da, db, dc, dd];
                    for (var k = 0; k < 4; k++)
                    {
                        // The face as drawn covers the face, and grows by no more than the old 0.6px seam stroke did,
                        // except at corners (mitered there, within the miter limit, instead of rounded).
                        Inside(geometric[k], da, db, dc, dd, 1e-6).ShouldBeTrue($"{pose} bar {i} face {f}");
                        Distance(geometric[k], drawnCorners[k]).ShouldBeLessThanOrEqualTo(ActivityTerrainQuad.MiterLimit * Outset + 1e-6);
                        var p = geometric[k];
                        var r = geometric[(k + 1) % 4];
                        var middle = p + (r - p) / 2;
                        Inside(middle + OutwardNormal(geometric, k) * (Outset + 0.05), da, db, dc, dd, 0).ShouldBeFalse($"{pose} bar {i} face {f} edge {k}");
                    }
                }
                // Where two faces of a bar meet, each reaches past the shared edge into the other, together by 0.4px or more
                // (the old 0.6px seam stroke: 0.6): at 2x that is about a device pixel, so the later face's anti-aliased edge
                // lies over the earlier face, never over the floor. Sampled away from the corners: an acute corner stops at
                // the miter limit, so a sliver seen edge-on reaches less far near it.
                for (var first = 0; first < 3; first++)
                {
                    for (var second = first + 1; second < 3; second++)
                    {
                        if (!has[first] || !has[second] || !SharedEdge(quads[first], quads[second], out var p, out var r))
                        {
                            continue;
                        }
                        var under = quads[first];
                        var over = quads[second];
                        if (ActivityTerrainQuad.Thickness(under.A, under.B, under.C, under.D) < 1 ||
                            ActivityTerrainQuad.Thickness(over.A, over.B, over.C, over.D) < 1)
                        {
                            // A sliver seen edge-on is narrower than a pixel: no seam of it can show.
                            continue;
                        }
                        shared++;
                        var across = Perpendicular(r - p);
                        // Point the normal into the later face.
                        var center = new Point((over.A.X + over.B.X + over.C.X + over.D.X) / 4, (over.A.Y + over.B.Y + over.C.Y + over.D.Y) / 4);
                        if (Cross(r - p, center - p) * Cross(r - p, across) < 0)
                        {
                            across = -across;
                        }
                        foreach (var t in new[] { 0.3, 0.5, 0.7 })
                        {
                            var onEdge = p + (r - p) * t;
                            var underReach = Reach(under.M, onEdge, across);
                            var overReach = Reach(over.M, onEdge, -across);
                            var where = $"{pose} bar {i} faces {first}/{second} t {t}: {underReach:F3} + {overReach:F3}";
                            underReach.ShouldBeGreaterThan(0.05, where);
                            overReach.ShouldBeGreaterThan(0.05, where);
                            (underReach + overReach).ShouldBeGreaterThan(0.4, where);
                        }
                    }
                }
            }
            drawn.ShouldBeGreaterThan(pose == "top" ? 100 : 300);
            if (pose == "top")
            {
                // Pressed flat and seen from straight above, the sides are slivers under the tops: only tops are drawn.
                shared.ShouldBe(0);
                for (var i = 0; i < ActivityTerrainMath.Cells; i++)
                {
                    canvas.TryGetDrawnFace(i, 0, out _, out _, out _, out _, out _).ShouldBeFalse();
                    canvas.TryGetDrawnFace(i, 1, out _, out _, out _, out _, out _).ShouldBeFalse();
                    canvas.TryGetDrawnFace(i, 2, out _, out _, out _, out _, out _).ShouldBeTrue();
                }
            }
            else
            {
                shared.ShouldBeGreaterThan(200);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bars_Of_Zero_Height_Draw_Only_Their_Top()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = terrain.Canvas.ShouldNotBeNull();
            Render(canvas);
            // Sunday's night hours are zero in the test week.
            var zero = new ActivityTerrainCell(6, 1).Index;
            canvas.HeightAt(zero).ShouldBe(0);
            canvas.TryGetDrawnFace(zero, 0, out _, out _, out _, out _, out _).ShouldBeFalse();
            canvas.TryGetDrawnFace(zero, 1, out _, out _, out _, out _, out _).ShouldBeFalse();
            canvas.TryGetDrawnFace(zero, 2, out _, out _, out _, out _, out _).ShouldBeTrue();
            var tall = new ActivityTerrainCell(2, 14).Index;
            canvas.TryGetDrawnFace(tall, 0, out _, out _, out _, out _, out _).ShouldBeTrue();
            canvas.TryGetDrawnFace(tall, 1, out _, out _, out _, out _, out _).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Moving_Frames_Build_No_Geometry()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = terrain.Canvas.ShouldNotBeNull();
            terrain.HighlightedCell = new ActivityTerrainCell(2, 14);
            var camera = canvas.OrbitCamera;
            var yaw = camera.Yaw;
            using var bitmap = new RenderTargetBitmap(new PixelSize(800, 600));
            // Warm up: one full turn draws every shade of every face once (their brushes are cached).
            for (var i = 0; i < 120; i++)
            {
                camera.JumpTo(new ShowpieceOrbitPose { Yaw = yaw + i * Math.PI / 60 });
                Draw(canvas, bitmap);
            }
            // A still frame (labels, outline, cached faces) against a frame where every face moves. Best of three, so a
            // one-off runtime allocation (tiered JIT) does not count.
            long still = long.MaxValue, moving = long.MaxValue;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 30; i++)
                {
                    Draw(canvas, bitmap);
                }
                still = Math.Min(still, (GC.GetAllocatedBytesForCurrentThread() - before) / 30);
                before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 30; i++)
                {
                    camera.JumpTo(new ShowpieceOrbitPose { Yaw = yaw + i * Math.PI / 15 });
                    Draw(canvas, bitmap);
                }
                moving = Math.Min(moving, (GC.GetAllocatedBytesForCurrentThread() - before) / 30);
            }
            // 500 faces rebuilt per moving frame: a path each used to cost ~100 KB a frame; now only the highlighted
            // bar's outline is a path.
            (moving - still).ShouldBeLessThan(2048, $"still {still} B/frame, moving {moving} B/frame");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Highlight_Outline_Is_Rebuilt_Only_When_Its_Bar_Moves()
    {
        var (window, terrain) = Show();
        try
        {
            var canvas = terrain.Canvas.ShouldNotBeNull();
            terrain.HighlightedCell = new ActivityTerrainCell(3, 10);
            Render(canvas);
            var outline = canvas.OutlineGeometry.ShouldNotBeNull();
            Render(canvas);
            canvas.OutlineGeometry.ShouldBeSameAs(outline);
            var camera = canvas.OrbitCamera;
            camera.JumpTo(new ShowpieceOrbitPose { Yaw = camera.Yaw + 0.2 });
            Render(canvas);
            canvas.OutlineGeometry.ShouldNotBeSameAs(outline);
            terrain.HighlightedCell = new ActivityTerrainCell(4, 10);
            var moved = canvas.OutlineGeometry;
            Render(canvas);
            canvas.OutlineGeometry.ShouldNotBeSameAs(moved);
            Distance(canvas.OutlineGeometry!.Bounds.Center, canvas.BarTopCenter(new ActivityTerrainCell(4, 10))!.Value).ShouldBeLessThan(1);
        }
        finally
        {
            window.Close();
        }
    }

    // Helpers --------------------------------------------------------------------------------------------------------------

    private static (Window Window, ActivityTerrain Terrain) Show()
    {
        var values = new double[ActivityTerrainMath.Cells];
        for (var i = 0; i < values.Length; i++)
        {
            var cell = ActivityTerrainCell.FromIndex(i);
            // Busy afternoons, quiet nights; Sunday's small hours are empty (bars of zero height).
            values[i] = cell.Day == 6 && cell.Hour < 6 ? 0 : 20 + 80 * Math.Exp(-Math.Pow((cell.Hour - 14) / 4.0, 2)) + 7 * cell.Day;
        }
        var terrain = new ActivityTerrain
        {
            Width = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Title = "Weekly rhythm"
        };
        terrain.Metrics.Add(new ActivityTerrainMetric { Key = "visits", Label = "Visits", Unit = "visits", Values = values });
        var window = TestHelpers.Show(terrain, ArcMotionLevel.Off);
        window.Width = 800;
        window.Height = 1000;
        for (var i = 0; i < 3; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        return (window, terrain);
    }

    private static void Pose(ActivityTerrain terrain, ActivityTerrainCanvas canvas, string pose)
    {
        var camera = canvas.OrbitCamera;
        switch (pose)
        {
            case "behind":
                camera.JumpTo(new ShowpieceOrbitPose { Yaw = ActivityTerrainMath.Radians(200) });
                break;
            case "steep":
                camera.JumpTo(new ShowpieceOrbitPose { Yaw = ActivityTerrainMath.Radians(-60), Pitch = ActivityTerrainMath.Radians(80) });
                break;
            case "low":
                camera.JumpTo(new ShowpieceOrbitPose { Yaw = ActivityTerrainMath.Radians(45), Pitch = ActivityTerrainMath.Radians(10), Zoom = 2 });
                break;
            case "top":
                terrain.SetViewMode(ActivityTerrainViewMode.Top);
                break;
            case "highlight":
                terrain.HighlightedCell = new ActivityTerrainCell(3, 15);
                break;
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Render(ActivityTerrainCanvas canvas)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(800, 600));
        Draw(canvas, bitmap);
    }

    private static void Draw(ActivityTerrainCanvas canvas, RenderTargetBitmap bitmap)
    {
        using var context = bitmap.CreateDrawingContext();
        canvas.Render(context);
    }

    private static (Point, Point, Point, Point) Corners(double[] q) =>
        (new Point(q[0], q[1]), new Point(q[2], q[3]), new Point(q[4], q[5]), new Point(q[6], q[7]));

    /// <summary>(u, v) through the matrix in double precision (Avalonia's own transform rounds to float under perspective).</summary>
    private static Point Map(Avalonia.Matrix m, double u, double v)
    {
        var w = u * m.M13 + v * m.M23 + m.M33;
        return new Point((u * m.M11 + v * m.M21 + m.M31) / w, (u * m.M12 + v * m.M22 + m.M32) / w);
    }

    /// <summary>Inside the convex quad (either winding), or within <paramref name="tolerance"/> of it.</summary>
    private static bool Inside(Point p, Point a, Point b, Point c, Point d, double tolerance)
    {
        Point[] q = [a, b, c, d];
        var sign = Math.Sign(Cross(c - a, d - b));
        for (var k = 0; k < 4; k++)
        {
            var edge = q[(k + 1) % 4] - q[k];
            var length = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
            if (length < 1e-12)
            {
                continue;
            }
            if (Cross(edge, p - q[k]) * sign / length < -tolerance)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>How far from <paramref name="from"/> along <paramref name="direction"/> the drawn quad still covers (px).</summary>
    private static double Reach(Avalonia.Matrix m, Point from, Vector direction)
    {
        var (a, b, c, d) = (Map(m, 0, 0), Map(m, 1, 0), Map(m, 1, 1), Map(m, 0, 1));
        if (!Inside(from, a, b, c, d, 1e-9))
        {
            return 0;
        }
        double inside = 0, outside = 2;
        for (var k = 0; k < 40; k++)
        {
            var mid = (inside + outside) / 2;
            if (Inside(from + direction * mid, a, b, c, d, 0))
            {
                inside = mid;
            }
            else
            {
                outside = mid;
            }
        }
        return inside;
    }

    private static Vector OutwardNormal(Point[] quad, int edge)
    {
        var sign = Math.Sign(Cross(quad[2] - quad[0], quad[3] - quad[1]));
        var e = quad[(edge + 1) % 4] - quad[edge];
        var length = Math.Sqrt(e.X * e.X + e.Y * e.Y);
        return new Vector(e.Y, -e.X) * (sign / length);
    }

    private static Vector Perpendicular(Vector edge)
    {
        var length = Math.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        return new Vector(-edge.Y / length, edge.X / length);
    }

    private static bool SharedEdge((Point A, Point B, Point C, Point D, Avalonia.Matrix M) one,
                                   (Point A, Point B, Point C, Point D, Avalonia.Matrix M) other, out Point p, out Point r)
    {
        Point[] x = [one.A, one.B, one.C, one.D];
        Point[] y = [other.A, other.B, other.C, other.D];
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                var (x0, x1) = (x[i], x[(i + 1) % 4]);
                var (y0, y1) = (y[j], y[(j + 1) % 4]);
                if ((x0 == y0 && x1 == y1 || x0 == y1 && x1 == y0) && Distance(x0, x1) > 1)
                {
                    (p, r) = (x0, x1);
                    return true;
                }
            }
        }
        (p, r) = (default, default);
        return false;
    }

    private static double Cross(Vector u, Vector v) => u.X * v.Y - u.Y * v.X;

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
