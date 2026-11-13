using Avalonia;

namespace TKCode.Arc.Tests.Special;

/// <summary>
/// Cursor trail draw's shape recognizer: every threshold measured on Arc's board (lines snap within 7 degrees, circles up
/// to aspect 1.2, squares up to 1.1, rectangles leveled up to 10 degrees, 0.9 of a turn still closes), plus the shapes
/// that stay freehand.
/// </summary>
public class CursorTrailDrawRecognizerTests
{
    private static Point[] Line(double degrees, double length = 300)
    {
        var a = degrees * Math.PI / 180;
        var points = new Point[40];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (double)(points.Length - 1);
            points[i] = new Point(200 + Math.Cos(a) * length * t, 400 - Math.Sin(a) * length * t);
        }
        return points;
    }

    private static Point[] Oval(double rx, double ry, double turns = 1.03, double wobble = 0, double cx = 400, double cy = 300)
    {
        var points = new Point[121];
        for (var i = 0; i < points.Length; i++)
        {
            var a = -Math.PI / 2 + Math.PI * 2 * turns * i / (points.Length - 1);
            var w = 1 + wobble * Math.Sin(3 * a);
            points[i] = new Point(cx + rx * w * Math.Cos(a), cy + ry * w * Math.Sin(a));
        }
        return points;
    }

    private static Point[] Polygon(params Point[] corners)
    {
        var points = new List<Point>();
        var loop = corners.Concat([corners[0] + (corners[1] - corners[0]) * 0.04]).ToArray();
        for (var i = 1; i < loop.Length; i++)
        {
            for (var k = 0; k < 20; k++)
            {
                points.Add(loop[i - 1] + (loop[i] - loop[i - 1]) * (k / 20.0));
            }
        }
        points.Add(loop[^1]);
        return points.ToArray();
    }

    private static Point[] Rotated(Point[] corners, double degrees)
    {
        var a = degrees * Math.PI / 180;
        var c = new Point(corners.Average(p => p.X), corners.Average(p => p.Y));
        return corners.Select(p => new Point(
            c.X + (p.X - c.X) * Math.Cos(a) - (p.Y - c.Y) * Math.Sin(a),
            c.Y + (p.X - c.X) * Math.Sin(a) + (p.Y - c.Y) * Math.Cos(a))).ToArray();
    }

    private static Point[] Box(double width, double height, double degrees = 0) => Polygon(Rotated(
        [new Point(300, 300), new Point(300 + width, 300), new Point(300 + width, 300 + height), new Point(300, 300 + height)], degrees));

    private static double AngleOf(CursorTrailDrawCleanShape shape)
    {
        var a = shape.Corners[0];
        var b = shape.Corners[1];
        return -Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(6.5, 0)]
    [InlineData(39, 45)]
    [InlineData(84.5, 90)]
    [InlineData(-6, 0)]
    public void Lines_close_to_0_45_or_90_degrees_snap_to_them(double drawn, double snapped)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Line(drawn))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Line);
        AngleOf(shape).ShouldBe(snapped, 0.01);
    }

    [Theory]
    [InlineData(7.5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(36)]
    [InlineData(82)]
    public void Lines_further_off_keep_their_angle(double drawn)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Line(drawn))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Line);
        AngleOf(shape).ShouldBe(drawn, 0.01);
    }

    [Fact]
    public void A_snapped_line_keeps_its_start_and_its_length()
    {
        var drawn = Line(4, 500);
        var shape = CursorTrailDrawRecognizer.Recognize(drawn)!.Value;
        shape.Corners[0].ShouldBe(drawn[0]);
        var length = CursorTrailDrawInkGeometry.Distance(shape.Corners[0], shape.Corners[1]);
        length.ShouldBe(CursorTrailDrawInkGeometry.Distance(drawn[0], drawn[^1]), 0.01);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.1)]
    [InlineData(1.15)]
    [InlineData(1.2)]
    public void Round_loops_up_to_aspect_1_2_are_circles(double aspect)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Oval(100 * aspect, 100))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Circle);
        // The diameter is the mean of the two axes.
        shape.RadiusX.ShouldBe((100 * aspect + 100) / 2, 2.5);
    }

    [Theory]
    [InlineData(1.25)]
    [InlineData(1.3)]
    [InlineData(1.8)]
    public void Longer_loops_are_level_ellipses(double aspect)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Oval(100 * aspect, 100))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Ellipse);
        shape.Angle.ShouldBe(0);
        shape.RadiusX.ShouldBe(100 * aspect, 3);
        shape.RadiusY.ShouldBe(100, 3);
    }

    [Fact]
    public void A_wobbly_circle_still_snaps()
    {
        CursorTrailDrawRecognizer.Recognize(Oval(130, 120, 1.05, 0.08))!.Value.Shape.ShouldBe(CursorTrailDrawShape.Circle);
    }

    [Fact]
    public void Nine_tenths_of_a_turn_closes_but_eight_tenths_stays_freehand()
    {
        CursorTrailDrawRecognizer.Recognize(Oval(100, 100, 0.9))!.Value.Shape.ShouldBe(CursorTrailDrawShape.Circle);
        CursorTrailDrawRecognizer.Recognize(Oval(100, 100, 0.8)).ShouldBeNull();
        CursorTrailDrawRecognizer.Recognize(Oval(90, 70, 0.55)).ShouldBeNull();
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.05)]
    [InlineData(1.1)]
    public void Boxes_up_to_aspect_1_1_are_squares(double aspect)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Box(130 * aspect, 130))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Square);
        shape.RadiusX.ShouldBe(shape.RadiusY);
    }

    [Theory]
    [InlineData(1.15)]
    [InlineData(1.2)]
    [InlineData(1.7)]
    public void Longer_boxes_are_rectangles(double aspect)
    {
        CursorTrailDrawRecognizer.Recognize(Box(130 * aspect, 130))!.Value.Shape.ShouldBe(CursorTrailDrawShape.Rectangle);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(-9)]
    public void Rectangles_tilted_up_to_10_degrees_are_leveled(double degrees)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Box(220, 130, degrees))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Rectangle);
        shape.Angle.ShouldBe(0);
        shape.Corners.Select(p => Math.Round(p.Y, 6)).Distinct().Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(25)]
    public void Rectangles_tilted_further_keep_their_tilt(double degrees)
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Box(220, 130, degrees))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Rectangle);
        (shape.Angle * 180 / Math.PI).ShouldBe(degrees, 1.5);
    }

    [Fact]
    public void A_triangle_snaps_with_a_level_base()
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Polygon(new Point(430, 330), new Point(530, 480), new Point(330, 476)))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Triangle);
        shape.Corners.Count.ShouldBe(3);
        shape.Corners.Select(p => Math.Round(p.Y, 6)).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void Other_four_sided_shapes_snap_through_their_corners()
    {
        var shape = CursorTrailDrawRecognizer.Recognize(Polygon(new Point(640, 340), new Point(800, 345), new Point(860, 470), new Point(600, 465)))!.Value;
        shape.Shape.ShouldBe(CursorTrailDrawShape.Quad);
        shape.Corners.Count.ShouldBe(4);
    }

    [Fact]
    public void Zigzags_and_tiny_strokes_stay_freehand()
    {
        Point[] zigzag = [new(700, 500), new(760, 640), new(800, 520), new(840, 650), new(880, 510), new(920, 600)];
        CursorTrailDrawRecognizer.Recognize(zigzag).ShouldBeNull();
        CursorTrailDrawRecognizer.Recognize(Line(0, 10), minimumLength: 24).ShouldBeNull();
        CursorTrailDrawRecognizer.Recognize([new Point(1, 1)]).ShouldBeNull();
    }

    [Fact]
    public void A_clean_shape_samples_from_the_stroke_start_the_way_it_went()
    {
        var drawn = Oval(100, 100);
        var shape = CursorTrailDrawRecognizer.Recognize(drawn)!.Value;
        var samples = shape.Sample(64, drawn[0]);
        samples.Length.ShouldBe(64);
        CursorTrailDrawInkGeometry.Distance(samples[0], drawn[0]).ShouldBeLessThan(6);
        CursorTrailDrawInkGeometry.Distance(samples[^1], samples[0]).ShouldBeLessThan(1);
        // Clockwise on screen, like the stroke: the second sample is to the right of the top.
        samples[3].X.ShouldBeGreaterThan(samples[0].X);
    }

    [Fact]
    public void Saved_ovals_are_fitted_back_from_their_points()
    {
        var circle = new CursorTrailDrawCleanShape(CursorTrailDrawShape.Ellipse, [], new Point(300, 200), 120, 70, 0, false);
        var rebuilt = CursorTrailDrawRecognizer.FromPoints(CursorTrailDrawShape.Ellipse, circle.Centerline(48));
        rebuilt.Center.X.ShouldBe(300, 0.5);
        rebuilt.RadiusX.ShouldBe(120, 0.5);
        rebuilt.RadiusY.ShouldBe(70, 0.5);
    }
}
