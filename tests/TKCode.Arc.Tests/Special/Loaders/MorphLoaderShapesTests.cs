using Avalonia;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>
/// The morph loader's pure geometry against the measured preview: the resting shapes on the 24 unit grid, the four loading
/// loops, the direction rule for morph targets, the forward turn to upright, the pop keyframes and the arc sampling.
/// </summary>
public class MorphLoaderShapesTests
{
    private static MorphLoaderStroke[] Rest(MorphLoaderShape shape, double strokeWidth = 2.5)
    {
        var strokes = new MorphLoaderStroke[4];
        MorphLoaderShapes.WriteRest(shape, strokeWidth, 4, 3, strokes);
        return strokes;
    }

    [Fact]
    public void Dots_are_three_dots_seven_apart_and_hide_the_fourth_stroke_at_the_center()
    {
        var dots = Rest(MorphLoaderShape.Dots);
        dots[0].Cx.ShouldBe(5);
        dots[1].Cx.ShouldBe(12);
        dots[2].Cx.ShouldBe(19);
        dots.Take(3).ShouldAllBe(d => d.Cy == 12 && d.Length == 0 && d.Width == 4 && d.Opacity == 1);
        dots[3].ShouldBe(new MorphLoaderStroke(12, 12, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Bars_are_four_vertical_lines_five_apart_twelve_long_three_wide()
    {
        var bars = Rest(MorphLoaderShape.Bars);
        bars.Select(b => b.Cx).ShouldBe([4.5, 9.5, 14.5, 19.5]);
        bars.ShouldAllBe(b => b.Cy == 12 && b.Length == 12 && b.Angle == 90 && b.Width == 3 && b.Bend == 0);
    }

    [Fact]
    public void Ring_is_four_64_degree_arcs_of_a_radius_8_5_circle()
    {
        var ring = Rest(MorphLoaderShape.Ring);
        MorphLoaderShapes.RingChord.ShouldBe(9.008, 0.001);
        ring[0].Cy.ShouldBe(4.792, 0.001);
        ring[1].Cx.ShouldBe(19.208, 0.001);
        ring[2].Cy.ShouldBe(19.208, 0.001);
        ring[3].Cx.ShouldBe(4.792, 0.001);
        ring.Select(r => r.Angle).ShouldBe([0, 90, 180, -90]);
        ring.ShouldAllBe(r => Math.Abs(r.Bend - 1 / 8.5) < 1e-12 && r.Width == 2.5);
    }

    [Fact]
    public void Square_is_a_closed_fourteen_box()
    {
        var square = Rest(MorphLoaderShape.Square, 3);
        square[0].ShouldBe(new MorphLoaderStroke(12, 5, 14, 0, 0, 3, 1));
        square[1].ShouldBe(new MorphLoaderStroke(19, 12, 14, 90, 0, 3, 1));
        square[2].ShouldBe(new MorphLoaderStroke(12, 19, 14, 180, 0, 3, 1));
        square[3].ShouldBe(new MorphLoaderStroke(5, 12, 14, -90, 0, 3, 1));
    }

    [Fact]
    public void Check_is_drawn_along_the_pen_path_and_cross_as_four_halves()
    {
        var check = Rest(MorphLoaderShape.Check);
        check[0].Length.ShouldBe(3.677, 0.001);
        check[1].Length.ShouldBe(3.677, 0.001);
        check[2].Length.ShouldBe(6.736, 0.001);
        check[3].Length.ShouldBe(6.736, 0.001);
        check[0].Angle.ShouldBe(45, 1e-9);
        check[2].Angle.ShouldBe(-49.214, 0.001);
        check[3].Angle.ShouldBe(-49.214, 0.001);
        check[1].Cx.ShouldBe(8.9, 1e-9);
        var cross = Rest(MorphLoaderShape.Cross);
        cross.ShouldAllBe(c => Math.Abs(c.Length - 7.778) < 0.001);
        cross.Select(c => c.Angle).ShouldBe([45, 45, 135, 135], 1e-9);
        cross[0].Cx.ShouldBe(9.25);
        cross[3].Cy.ShouldBe(14.75);
    }

    [Fact]
    public void Status_picks_check_or_cross_and_loading_picks_the_variant()
    {
        MorphLoaderShapes.ShapeFor(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading).ShouldBe(MorphLoaderShape.Ring);
        MorphLoaderShapes.ShapeFor(MorphLoaderVariant.Ring, MorphLoaderStatus.Success).ShouldBe(MorphLoaderShape.Check);
        MorphLoaderShapes.ShapeFor(MorphLoaderVariant.Bars, MorphLoaderStatus.Error).ShouldBe(MorphLoaderShape.Cross);
    }

    [Fact]
    public void Dots_hop_3_2_in_the_first_half_of_a_900ms_cycle_116_7ms_apart()
    {
        MorphLoaderShapes.DotLiftAt(0.225, 0).ShouldBe(3.2, 1e-9);
        MorphLoaderShapes.DotLiftAt(0.5, 0).ShouldBe(0);
        MorphLoaderShapes.DotLiftAt(0.89, 0).ShouldBe(0);
        MorphLoaderShapes.DotLiftAt(0.225 + 0.1167, 1).ShouldBe(3.2, 1e-9);
        MorphLoaderShapes.DotLiftAt(0.225 + 2 * 0.1167, 2).ShouldBe(3.2, 1e-9);
        // The next cycle repeats.
        MorphLoaderShapes.DotLiftAt(0.225 + 0.9, 0).ShouldBe(3.2, 1e-9);
        // The dot grows with its lift: 4 -> 4.6 at the peak.
        var dots = new MorphLoaderStroke[4];
        MorphLoaderShapes.WriteRest(MorphLoaderShape.Dots, 2.5, 4, 3, dots);
        MorphLoaderShapes.ApplyLoop(MorphLoaderVariant.Dots, 0.225, 1, dots);
        dots[0].Cy.ShouldBe(8.8, 1e-9);
        dots[0].Width.ShouldBe(4.6, 1e-9);
        dots[3].Width.ShouldBe(0);
    }

    [Fact]
    public void Bars_breathe_between_4_5_and_12_on_a_950ms_sine_each_0_7rad_behind()
    {
        var samples = Enumerable.Range(0, 950).Select(ms => 12 - MorphLoaderShapes.BarShortening(ms / 1000.0, 0)).ToArray();
        samples.Min().ShouldBe(4.5, 0.001);
        samples.Max().ShouldBe(12, 0.001);
        // Bar 1 is bar 0 shifted by 0.7rad of the 950ms period (≈ 105.8ms).
        var lag = 0.7 / (2 * Math.PI) * 0.95;
        MorphLoaderShapes.BarShortening(0.3 + lag, 1).ShouldBe(MorphLoaderShapes.BarShortening(0.3, 0), 1e-9);
    }

    [Fact]
    public void Ring_arcs_breathe_between_5_808_and_9_008_every_1400ms_and_turn_330_degrees_a_second()
    {
        var chords = Enumerable.Range(0, 1400).Select(ms => MorphLoaderShapes.RingChord - MorphLoaderShapes.RingShortening(ms / 1000.0)).ToArray();
        chords.Min().ShouldBe(5.808, 0.001);
        chords.Max().ShouldBe(9.008, 0.001);
        MorphLoaderShapes.LoopTurnRate(MorphLoaderVariant.Ring, 0.4).ShouldBe(330);
        MorphLoaderShapes.LoopTurnRate(MorphLoaderVariant.Dots, 0.4).ShouldBe(0);
    }

    [Fact]
    public void Square_holds_180ms_then_tumbles_a_smoothstep_quarter_turn_in_720ms_opening_its_corners()
    {
        MorphLoaderShapes.SquareTurns(0.1).ShouldBe(0);
        MorphLoaderShapes.SquareTurns(0.18).ShouldBe(0);
        MorphLoaderShapes.SquareTurns(0.18 + 0.36).ShouldBe(0.5, 1e-9);
        MorphLoaderShapes.SquareTurns(0.9).ShouldBe(1, 1e-9);
        MorphLoaderShapes.SquareTurns(1.0).ShouldBe(1, 1e-9);
        MorphLoaderShapes.SquareTurns(1.8 + 0.18 + 0.36).ShouldBe(2.5, 1e-9);
        // Mid tumble the sides shrink to 6 and grow back to 14.
        MorphLoaderShapes.SquareShortening(0.54).ShouldBe(8, 1e-9);
        MorphLoaderShapes.SquareShortening(0.1).ShouldBe(0);
        MorphLoaderShapes.SquareShortening(0.9).ShouldBe(0, 1e-9);
        // The tumble's speed: 0 in the hold, 90 * 1.5 / 0.72 = 187.5 degrees/s at the middle.
        MorphLoaderShapes.LoopTurnRate(MorphLoaderVariant.Square, 0.1).ShouldBe(0);
        MorphLoaderShapes.LoopTurnRate(MorphLoaderVariant.Square, 0.54).ShouldBe(187.5, 1e-9);
    }

    [Fact]
    public void Straight_strokes_aim_modulo_180_and_bent_strokes_modulo_360()
    {
        // Bars (90) to the check's long side (-49.2): the same line drawn the other way, 130.8.
        MorphLoaderShapes.AimAngle(90, 0, -49.214, 0).ShouldBe(130.786, 1e-9);
        // A square side (180) to the check's short side (45): 225.
        MorphLoaderShapes.AimAngle(180, 0, 45, 0).ShouldBe(225, 1e-9);
        // Ring arc (180, bent) to the check's long side: +130.8 the short way round, never flipped.
        MorphLoaderShapes.AimAngle(180, 1 / 8.5, -49.214, 0).ShouldBe(310.786, 1e-9);
        // Ring arc (-90) to the cross (135): -225.
        MorphLoaderShapes.AimAngle(-90, 1 / 8.5, 135, 0).ShouldBe(-225, 1e-9);
        // Into a bent shape the full circle applies too.
        MorphLoaderShapes.AimAngle(-90, 0, 90, 1 / 8.5).ShouldBe(90, 1e-9);
        MorphLoaderShapes.AimAngle(400, 0, 0, 0).ShouldBe(360, 1e-9);
    }

    [Fact]
    public void Upright_is_the_next_whole_turn_ahead()
    {
        MorphLoaderShapes.NextUpright(0).ShouldBe(0);
        MorphLoaderShapes.NextUpright(39).ShouldBe(360);
        MorphLoaderShapes.NextUpright(360).ShouldBe(360);
        MorphLoaderShapes.NextUpright(1856.65).ShouldBe(2160);
        MorphLoaderShapes.NextUpright(-30).ShouldBe(0);
    }

    [Fact]
    public void Pop_is_1_to_1_14_and_back_in_420ms_peaking_at_35_2_percent()
    {
        MorphLoaderShapes.PopAt(0).ShouldBe(1);
        MorphLoaderShapes.PopAt(0.42 * 0.352).ShouldBe(1.14, 1e-6);
        MorphLoaderShapes.PopAt(0.42).ShouldBe(1);
        MorphLoaderShapes.PopAt(0.05).ShouldBeGreaterThan(1.05);
        MorphLoaderShapes.PopAt(0.3).ShouldBeInRange(1.0, 1.1);
        var samples = Enumerable.Range(0, 43).Select(i => MorphLoaderShapes.PopAt(i / 100.0)).ToArray();
        samples.Max().ShouldBeLessThanOrEqualTo(1.14 + 1e-9);
    }

    [Fact]
    public void A_ring_arc_samples_onto_the_circle_and_bulges_outward()
    {
        var ring = Rest(MorphLoaderShape.Ring);
        var points = new Point[24];
        var count = MorphLoaderShapes.Sample(ring[0], ring[0].Length, points);
        count.ShouldBeGreaterThan(5);
        for (var i = 0; i < count; i++)
        {
            var d = points[i] - new Point(12, 12);
            Math.Sqrt(d.X * d.X + d.Y * d.Y).ShouldBe(8.5, 1e-9);
        }
        points[0].X.ShouldBe(12 - 4.504, 0.001);
        points[0].Y.ShouldBe(4.792, 0.001);
        points[count - 1].X.ShouldBe(12 + 4.504, 0.001);
        // The middle of the arc is the top of the circle, above the chord.
        points.Take(count).Min(p => p.Y).ShouldBe(3.5, 0.02);
        // The right arc bulges right.
        count = MorphLoaderShapes.Sample(ring[1], ring[1].Length, points);
        points.Take(count).Max(p => p.X).ShouldBe(20.5, 0.02);
    }

    [Fact]
    public void A_straight_stroke_is_one_segment_and_an_overlong_chord_is_a_half_circle()
    {
        var points = new Point[24];
        var line = new MorphLoaderStroke(12, 12, 10, 90, 0, 3, 1);
        MorphLoaderShapes.Sample(line, 10, points).ShouldBe(2);
        points[0].ShouldBe(new Point(12, 7), new PointComparer());
        points[1].ShouldBe(new Point(12, 17), new PointComparer());
        var bent = new MorphLoaderStroke(12, 12, 20, 0, 1 / 8.5, 3, 1);
        var count = MorphLoaderShapes.Sample(bent, 20, points);
        points.Take(count).Min(p => p.Y).ShouldBe(2, 0.05);
    }

    private sealed class PointComparer : IEqualityComparer<Point>
    {
        public bool Equals(Point a, Point b) => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
        public int GetHashCode(Point p) => 0;
    }
}
