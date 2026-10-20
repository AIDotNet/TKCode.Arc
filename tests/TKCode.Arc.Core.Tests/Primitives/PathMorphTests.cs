using TKCode.Arc.Primitives;
using Avalonia;
using Shouldly;

namespace TKCode.Arc.Core.Tests.Primitives;

public class PathMorphTests
{
    private const string Play = "M7 4.5 L12 7.42 L12 16.58 L7 19.5 Z M12 7.42 L19.5 11.8 L19.5 12.2 L12 16.58 Z";
    private const string Pause = "M6.5 5 L10 5 L10 19 L6.5 19 Z M14 5 L17.5 5 L17.5 19 L14 19 Z";

    [Fact]
    public void Parses_Shorthand_Line_Commands()
    {
        var shape = PathMorphShape.Parse("M4 6h16");
        shape.Contours.Count.ShouldBe(1);
        var contour = shape.Contours[0];
        contour.IsClosed.ShouldBeFalse();
        contour.SegmentCount.ShouldBe(1);
        contour.Length.ShouldBe(16, 1e-9);
        contour.Points[0].ShouldBe(new Point(4, 6));
        contour.Points[^1].ShouldBe(new Point(20, 6));
    }

    [Fact]
    public void Parses_Implicit_Relative_LineTos_And_Close()
    {
        var shape = PathMorphShape.Parse("m2 2 3 0 0 3z");
        var contour = shape.Contours.ShouldHaveSingleItem();
        contour.IsClosed.ShouldBeTrue();
        contour.SegmentCount.ShouldBe(3);
        contour.Length.ShouldBe(6 + Math.Sqrt(18), 1e-9);
        contour.Points[^1].ShouldBe(contour.Points[0]);
        shape.Signature.ShouldBe("c3");
    }

    [Fact]
    public void Parses_Compact_Numbers_And_Exponents()
    {
        var shape = PathMorphShape.Parse("M.5.5L1-1l1e1 0");
        var points = shape.Contours[0].Points;
        points[0].ShouldBe(new Point(0.5, 0.5));
        points[1].ShouldBe(new Point(1, -1));
        points[2].ShouldBe(new Point(11, -1));
    }

    [Fact]
    public void Flattens_Arcs_To_The_Right_Length()
    {
        var circle = PathMorphShape.Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z");
        var contour = circle.Contours.ShouldHaveSingleItem();
        contour.IsClosed.ShouldBeTrue();
        contour.Length.ShouldBe(2 * Math.PI * 10, 2 * Math.PI * 10 * 0.005);
        foreach (var p in contour.Points)
        {
            Math.Sqrt((p.X - 12) * (p.X - 12) + (p.Y - 12) * (p.Y - 12)).ShouldBe(10, 1e-6);
        }
    }

    [Fact]
    public void Flattens_Curves_With_Smooth_Reflection()
    {
        // Lucide copy back sheet: relative cubic, V, and S-free curves.
        var sheet = PathMorphShape.Parse("M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2");
        var contour = sheet.Contours.ShouldHaveSingleItem();
        contour.SegmentCount.ShouldBe(5);
        contour.Points[^1].X.ShouldBe(16, 1e-9);
        contour.Points[^1].Y.ShouldBe(4, 1e-9);

        // A smooth cubic mirrors the previous control point: this S closes a symmetric bump.
        var bump = PathMorphShape.Parse("M0 0 C0 10 10 10 10 0 S20 -10 20 0");
        var points = bump.Contours[0].Points;
        points[^1].ShouldBe(new Point(20, 0));
        bump.Contours[0].PointInSegment(1, 0.5).Y.ShouldBe(-7.5, 1e-6);
    }

    [Fact]
    public void Compatible_Shapes_Are_Sampled_Vertex_To_Vertex()
    {
        var play = PathMorphShape.Parse(Play);
        var pause = PathMorphShape.Parse(Pause);
        play.IsCompatibleWith(pause).ShouldBeTrue();
        var (from, to) = PathMorphSampler.Align(play, pause, 32);
        from.HasSameLayout(to).ShouldBeTrue();
        from.Plan.ShouldNotBeNull();
        var plan = from.Plan![0];
        // Every source vertex lands on a sample in both frames, at the same index.
        var index = 0;
        var playVertices = new[] { new Point(7, 4.5), new Point(12, 7.42), new Point(12, 16.58), new Point(7, 19.5), new Point(7, 4.5) };
        var pauseVertices = new[] { new Point(6.5, 5), new Point(10, 5), new Point(10, 19), new Point(6.5, 19), new Point(6.5, 5) };
        for (var j = 0; j <= plan.Length; j++)
        {
            Close(from.GetContour(0)[index], playVertices[j]);
            Close(to.GetContour(0)[index], pauseVertices[j]);
            if (j < plan.Length)
            {
                index += plan[j];
            }
        }
        // Halfway, the second vertex sits halfway between the two shapes' vertices.
        var mid = from.Clone();
        PathMorphFrame.Interpolate(from, to, 0.5, mid);
        Close(mid.GetContour(0)[plan[0]], new Point((12 + 10) / 2.0, (7.42 + 5) / 2.0));
    }

    [Fact]
    public void Uniform_Samples_Are_Evenly_Spaced()
    {
        var frame = PathMorphSampler.Sample(PathMorphShape.Parse("M0 0 L10 0 L10 10"), 5);
        var points = frame.GetContour(0);
        points.Length.ShouldBe(5);
        Close(points[0], new Point(0, 0));
        Close(points[1], new Point(5, 0));
        Close(points[2], new Point(10, 0));
        Close(points[3], new Point(10, 5));
        Close(points[4], new Point(10, 10));
    }

    [Fact]
    public void Missing_Contours_Grow_From_Their_Centre()
    {
        var one = PathMorphShape.Parse("M0 0 L4 0 L4 4 L0 4 Z");
        var two = PathMorphShape.Parse("M0 0 L4 0 L4 4 Z M10 10 L14 10 L14 14 L10 14 Z");
        var (from, to) = PathMorphSampler.Align(one, two, 16);
        from.ContourCount.ShouldBe(2);
        to.ContourCount.ShouldBe(2);
        foreach (var p in from.GetContour(1))
        {
            Close(p, new Point(12, 12));
        }
        to.GetContour(1)[0].ShouldNotBe(to.GetContour(1)[4]);
    }

    [Fact]
    public void Closed_Rings_Rotate_To_The_Nearest_Start()
    {
        var a = PathMorphShape.Parse("M0 0 L10 0 L10 10 L0 10 Z");
        // Same square, starting at the opposite corner and running the other way.
        var b = PathMorphShape.Parse("M10 10 L10 0 L0 0 L0 10 Z");
        a.IsCompatibleWith(b).ShouldBeTrue();
        var (from, _) = PathMorphSampler.Align(a, a, 17);
        var reference = from.GetContour(0).ToArray();
        var ring = PathMorphSampler.Sample(b, 17).GetContour(0).ToArray();
        PathMorphSampler.AlignRing(reference, ring);
        for (var i = 0; i < ring.Length; i++)
        {
            Close(ring[i], reference[i]);
        }
    }

    [Fact]
    public void Interpolation_Extrapolates_On_Overshoot()
    {
        var (from, to) = PathMorphSampler.Align(PathMorphShape.Parse("M0 0 L10 0"), PathMorphShape.Parse("M0 10 L10 10"), 2);
        var frame = from.Clone();
        PathMorphFrame.Interpolate(from, to, 1.1, frame);
        frame.GetContour(0)[0].Y.ShouldBe(11, 1e-9);
    }

    [Fact]
    public void Reversing_Mid_Flight_Keeps_The_Speed()
    {
        var (from, to) = PathMorphSampler.Align(PathMorphShape.Parse("M0 0 L10 0"), PathMorphShape.Parse("M0 10 L10 10"), 4);
        var halfway = from.Clone();
        PathMorphFrame.Interpolate(from, to, 0.5, halfway);
        // Back to the start: half the distance, so the progress rate doubles and flips sign.
        var rate = PathMorphFrame.CarryRate(from, to, 3, halfway, from);
        rate.ShouldBe(-6, 1e-9);
        // Onward to the same target keeps the direction and doubles too.
        PathMorphFrame.CarryRate(from, to, 3, halfway, to).ShouldBe(6, 1e-9);
    }

    [Fact]
    public void Retarget_Keeps_The_Vertex_Plan_For_The_Same_Structure()
    {
        var play = PathMorphShape.Parse(Play);
        var pause = PathMorphShape.Parse(Pause);
        var (from, to) = PathMorphSampler.Align(play, pause, 24);
        var current = from.Clone();
        PathMorphFrame.Interpolate(from, to, 0.3, current);
        var back = PathMorphSampler.SampleInto(play, current);
        back.ShouldNotBeNull();
        for (var c = 0; c < from.ContourCount; c++)
        {
            var expected = from.GetContour(c).ToArray();
            var actual = back!.GetContour(c).ToArray();
            for (var i = 0; i < expected.Length; i++)
            {
                Close(actual[i], expected[i]);
            }
        }
    }

    [Fact]
    public void Polylines_Make_One_Segment_Per_Edge()
    {
        var shape = PathMorphShape.FromPolylines([(new[] { new Point(0, 0), new Point(1, 0), new Point(1, 1) }, true)]);
        var contour = shape.Contours.ShouldHaveSingleItem();
        contour.SegmentCount.ShouldBe(3);
        contour.IsClosed.ShouldBeTrue();
        contour.Length.ShouldBe(2 + Math.Sqrt(2), 1e-9);
    }

    [Fact]
    public void Plan_Gives_Every_Segment_At_Least_One_Interval()
    {
        var a = PathMorphShape.Parse("M0 0 L100 0 L100 1").Contours[0];
        var plan = PathMorphSampler.PlanSegments(a, a, 8);
        plan.Sum().ShouldBe(7);
        plan.ShouldAllBe(k => k >= 1);
        plan[0].ShouldBeGreaterThan(plan[1]);
    }

    private static void Close(Point actual, Point expected)
    {
        actual.X.ShouldBe(expected.X, 1e-6);
        actual.Y.ShouldBe(expected.Y, 1e-6);
    }
}
