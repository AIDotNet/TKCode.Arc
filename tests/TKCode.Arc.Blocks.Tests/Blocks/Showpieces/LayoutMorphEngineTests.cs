using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="LayoutMorphEngine"/> against the measurements in design/components/blocks/showpieces/layout-morph.md
/// (sections 4, 6 and 8): breakpoints, every layout's geometry across widths, the open detail, the stagger, the flight
/// lift and lean, the ring visibility, the release snap and the springs.
/// </summary>
public class LayoutMorphEngineTests
{
    private const int Count = 24;

    private static LayoutMorphPose[] Arrange(LayoutMorphMode mode, double width, double height, double front = 0, double[]? aspects = null)
    {
        var poses = new LayoutMorphPose[Count];
        LayoutMorphEngine.Arrange(mode, new LayoutMorphMetrics(width, height), aspects ?? Enumerable.Repeat(1.0, Count).ToArray(),
            front, poses, out _, out _);
        return poses;
    }

    private static double Left(in LayoutMorphPose p) => p.X - p.Width / 2;

    private static double Top(in LayoutMorphPose p) => p.Y - p.Height / 2;

    // Breakpoints ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1200, 600)]
    [InlineData(721, 600)]
    [InlineData(720, 540)]
    [InlineData(480, 540)]
    [InlineData(479, 500)]
    [InlineData(360, 500)]
    [InlineData(359, 460)]
    public void Stage_height_follows_the_container_width(double width, double height) =>
        LayoutMorphEngine.StageHeightFor(width).ShouldBe(height);

    [Theory]
    [InlineData(1080, 1242)]
    [InlineData(665, 900)]
    [InlineData(1312, 1500)]
    [InlineData(900, 1035)]
    public void Perspective_is_1_15_times_the_width_between_900_and_1500(double width, double perspective) =>
        LayoutMorphEngine.PerspectiveFor(width).ShouldBe(perspective, 0.01);

    [Fact]
    public void Narrow_below_720_and_dense_below_520()
    {
        new LayoutMorphMetrics(720, 540).IsNarrow.ShouldBeFalse();
        new LayoutMorphMetrics(719, 540).IsNarrow.ShouldBeTrue();
        new LayoutMorphMetrics(520, 540).IsDense.ShouldBeFalse();
        var dense = new LayoutMorphMetrics(519, 540);
        dense.IsDense.ShouldBeTrue();
        dense.Pad.ShouldBe(14);
        dense.Gap.ShouldBe(8);
    }

    // Grid -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Grid_at_1080_is_eight_captioned_columns_of_116_by_156()
    {
        var poses = Arrange(LayoutMorphMode.Grid, 1080, 600);
        poses[0].Width.ShouldBe(116);
        poses[0].Height.ShouldBe(156);
        poses[0].Radius.ShouldBe(16);
        Left(poses[0]).ShouldBe(-513);
        Top(poses[0]).ShouldBe(-248);
        Left(poses[23]).ShouldBe(397);
        Top(poses[23]).ShouldBe(92);
        Left(poses[1]).ShouldBe(-383);
        poses[0].Caption.ShouldBe(LayoutMorphCaption.Compact);
        // The picture: 5px inside the 1px border, 106 square, radius 11.
        poses[0].MediaX.ShouldBe(6);
        poses[0].MediaWidth.ShouldBe(106);
        poses[0].MediaRadius.ShouldBe(11);
    }

    [Theory]
    [InlineData(1372, 600, 8, 134, true)]
    [InlineData(1125, 600, 8, 122, true)]
    [InlineData(1044, 600, 8, 112, true)]
    [InlineData(1035, 600, 6, 127, false)]
    [InlineData(855, 600, 6, 122, false)]
    [InlineData(765, 600, 6, 107, false)]
    [InlineData(720, 540, 6, 100, false)]
    [InlineData(648, 540, 6, 88, false)]
    [InlineData(630, 540, 5, 87, false)]
    [InlineData(540, 540, 5, 87, false)]
    [InlineData(504, 540, 5, 88, false)]
    [InlineData(448, 500, 5, 77, false)]
    [InlineData(428, 500, 5, 73, false)]
    [InlineData(388, 500, 4, 72, false)]
    [InlineData(348, 460, 4, 65, false)]
    [InlineData(288, 460, 4, 59, false)]
    public void Grid_shape_across_widths(double width, double height, int columns, double cell, bool captioned)
    {
        var shape = LayoutMorphEngine.GridShape(new LayoutMorphMetrics(width, height), Count);
        shape.Columns.ShouldBe(columns);
        shape.CardWidth.ShouldBe(cell);
        shape.IsCaptioned.ShouldBe(captioned);
    }

    [Fact]
    public void Square_grids_are_centered_with_full_bleed_pictures()
    {
        var six = Arrange(LayoutMorphMode.Grid, 1035, 600);
        Left(six[0]).ShouldBe(-416);
        Top(six[0]).ShouldBe(-275);
        six[0].Radius.ShouldBe(12);
        six[0].Caption.ShouldBe(LayoutMorphCaption.None);
        six[0].MediaWidth.ShouldBe(six[0].Width - 2);
        var five = Arrange(LayoutMorphMode.Grid, 630, 540);
        Left(five[0]).ShouldBe(-245.5);
        Top(five[0]).ShouldBe(-245.5);
        Arrange(LayoutMorphMode.Grid, 504, 540)[0].Radius.ShouldBe(10);
    }

    // Masonry ----------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1080, 8)]
    [InlineData(1035, 8)]
    [InlineData(990, 6)]
    [InlineData(765, 6)]
    [InlineData(720, 5)]
    [InlineData(540, 5)]
    [InlineData(504, 4)]
    [InlineData(288, 4)]
    public void Masonry_columns_across_widths(double width, int columns) =>
        LayoutMorphEngine.MasonryColumns(new LayoutMorphMetrics(width, 600)).ShouldBe(columns);

    [Fact]
    public void Masonry_fills_the_shortest_column_and_centers_a_block_that_fits()
    {
        var aspects = Enumerable.Range(0, Count).Select(i => i == 0 ? 1.5 : 1.0).ToArray();
        var poses = Arrange(LayoutMorphMode.Masonry, 1080, 600, aspects: aspects);
        var cw = (1080 - 48 - 7 * 14) / 8.0;
        poses[0].Width.ShouldBe(cw, 1e-9);
        Left(poses[0]).ShouldBe(-516);
        poses[0].Height.ShouldBe(cw * 1.5, 1e-9);
        // Cards 1..7 fill the other columns; card 8 goes to the shortest (column 1), not under the tall card 0.
        Left(poses[8]).ShouldBe(Left(poses[1]), 1e-9);
        // Tallest column: 1.5 + 1 + 1 widths and two gaps; centered vertically.
        var tallest = cw * 3.5 + 2 * 14;
        Top(poses[0]).ShouldBe(-tallest / 2, 1e-9);
        poses[0].Radius.ShouldBe(12);
    }

    [Fact]
    public void Masonry_scales_cards_and_gaps_together_when_the_tallest_column_is_too_tall()
    {
        var aspects = Enumerable.Repeat(1.5, Count).ToArray();
        var poses = Arrange(LayoutMorphMode.Masonry, 720, 540, aspects: aspects);
        var cw = (720 - 48 - 4 * 14) / 5.0;
        var tallest = 5 * cw * 1.5 + 4 * 14;
        var s = (540 - 48) / tallest;
        poses[0].Height.ShouldBe(cw * 1.5 * s, 1e-9);
        Top(poses[0]).ShouldBe(-246, 1e-9);
        // The second card of column 0 starts one scaled card and one scaled gap lower.
        Top(poses[5]).ShouldBe(-246 + (cw * 1.5 + 14) * s, 1e-9);
    }

    // List -------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1372, 3)]
    [InlineData(990, 3)]
    [InlineData(945, 2)]
    [InlineData(648, 2)]
    [InlineData(630, 1)]
    [InlineData(288, 1)]
    public void List_columns_follow_304px_per_column(double width, int columns) =>
        LayoutMorphEngine.ListColumns(new LayoutMorphMetrics(width, 600)).ShouldBe(columns);

    [Fact]
    public void List_fills_columns_first_and_scrolls_when_taller_than_the_stage()
    {
        var poses = new LayoutMorphPose[Count];
        LayoutMorphEngine.Arrange(LayoutMorphMode.List, new LayoutMorphMetrics(1080, 600), new double[Count], 0, poses, out _, out var range);
        range.ShouldBe(0);
        poses[0].Width.ShouldBe(334.67, 0.01);
        poses[0].Height.ShouldBe(56);
        Left(poses[0]).ShouldBe(-516, 1e-9);
        Top(poses[0]).ShouldBe(-252, 1e-9);
        Top(poses[7]).ShouldBe(196, 1e-9);
        Left(poses[8]).ShouldBe(Left(poses[0]) + poses[0].Width + 14, 1e-9);
        poses[0].MediaWidth.ShouldBe(44);
        poses[0].MediaX.ShouldBe(7);
        poses[0].Radius.ShouldBe(14);
        poses[0].Caption.ShouldBe(LayoutMorphCaption.Row);

        LayoutMorphEngine.Arrange(LayoutMorphMode.List, new LayoutMorphMetrics(945, 600), new double[Count], 0, poses, out _, out range);
        poses[0].Width.ShouldBe(441.5, 1e-9);
        Top(poses[0]).ShouldBe(-276, 1e-9);
        Left(poses[23]).ShouldBe(7, 1e-9);
        Top(poses[23]).ShouldBe(428, 1e-9);
        range.ShouldBe(760 - 552, 1e-9);

        LayoutMorphEngine.Arrange(LayoutMorphMode.List, new LayoutMorphMetrics(504, 540), new double[Count], 0, poses, out _, out _);
        poses[0].Width.ShouldBe(476, 1e-9);
        Top(poses[0]).ShouldBe(-256, 1e-9);
        Top(poses[23]).ShouldBe(1216, 1e-9);
    }

    // Stack ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Stack_fans_24_cards_over_1_3_rad_on_a_circle_of_2_3_card_heights()
    {
        var poses = Arrange(LayoutMorphMode.Stack, 1312, 600);
        poses[0].Width.ShouldBe(200);
        poses[0].Height.ShouldBe(272);
        poses[0].RotZ.ShouldBe(-0.65, 1e-9);
        poses[23].RotZ.ShouldBe(0.65, 1e-9);
        (poses[1].RotZ - poses[0].RotZ).ShouldBe(1.3 / 23, 1e-9);
        // Arc at 1312: card 0 top-left (−478.6, −88.61), card 1 (−449.87, −109.2).
        Left(poses[0]).ShouldBe(-478.6, 0.1);
        Top(poses[0]).ShouldBe(-88.61, 1.5);
        Left(poses[1]).ShouldBe(-449.87, 0.1);
        poses[1].Z.ShouldBe(0.8, 1e-9);
        poses[23].Z.ShouldBe(18.4, 1e-9);
        poses[12].PullY.ShouldBe(60);
        poses[12].PullZ.ShouldBe(36);
        // The middle cards sit about 80 above the center.
        poses[11].Y.ShouldBe(-80, 2);
    }

    [Theory]
    [InlineData(900, 171, 233, 0.65)]
    [InlineData(647, 123, 167, 0.65)]
    [InlineData(540, 103, 140, 0.6486)]
    [InlineData(447, 96, 131, 0.4561)]
    [InlineData(358, 96, 131, 0.2971)]
    [InlineData(327, 96, 131, 0.2437)]
    public void Stack_narrows_its_fan_to_fit(double width, double w, double h, double halfAngle)
    {
        var metrics = new LayoutMorphMetrics(width, LayoutMorphEngine.StageHeightFor(width));
        var size = LayoutMorphEngine.StackCardSize(metrics);
        size.Width.ShouldBe(w);
        size.Height.ShouldBe(h);
        LayoutMorphEngine.StackHalfAngle(metrics).ShouldBe(halfAngle, 0.002);
    }

    // Carousel -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Carousel_ring_at_1080()
    {
        var metrics = new LayoutMorphMetrics(1080, 600);
        LayoutMorphEngine.CarouselCardSize(metrics).ShouldBe(new Size(320, 396));
        LayoutMorphEngine.CarouselRadius(metrics).ShouldBe(1188, 1e-9);
        LayoutMorphEngine.CarouselStep(metrics).ShouldBe(0.2882, 0.0001);
        var poses = Arrange(LayoutMorphMode.Carousel, 1080, 600, front: 2);
        poses[2].X.ShouldBe(0, 1e-9);
        poses[2].Z.ShouldBe(0, 1e-9);
        poses[2].RotY.ShouldBe(0, 1e-9);
        poses[0].RotY.ShouldBe(-2 * 0.28822, 1e-4);
        poses[0].X.ShouldBe(1188 * Math.Sin(poses[0].RotY), 1e-9);
        poses[0].Z.ShouldBe(1188 * (Math.Cos(poses[0].RotY) - 1), 1e-9);
        poses[0].Radius.ShouldBe(22);
        poses[0].MediaWidth.ShouldBe(304);
        poses[0].MediaRadius.ShouldBe(15);
        poses[0].Caption.ShouldBe(LayoutMorphCaption.Detail);
        // Opaque within two cards of the front, ≈ 0.91 at three, gone at four.
        poses[0].Opacity.ShouldBe(1, 1e-9);
        poses[5].Opacity.ShouldBe(0.91, 0.005);
        poses[6].Opacity.ShouldBe(0, 1e-9);
        // The strip does not wrap: the last card is 21 places on, round the back of the ring, hidden even where the
        // ring brings it beside the front (Arc: card 23 at −0.23 rad of card 2, opacity 0).
        poses[23].RotY.ShouldBe(21 * 0.28822, 1e-3);
        ShowpieceProjection.WrapAngle(poses[23].RotY).ShouldBe(-0.232, 0.002);
        poses[23].Opacity.ShouldBe(0);
    }

    [Theory]
    [InlineData(1312, 320, 1443.2, 342.4)]
    [InlineData(900, 270, 990, 292.4)]
    [InlineData(738, 221, 811.8, 243.4)]
    [InlineData(647, 210, 720, 232.4)]
    [InlineData(447, 210, 720, 222.8)]
    public void Carousel_card_ring_and_step_across_widths(double width, double cardWidth, double radius, double arc)
    {
        var metrics = new LayoutMorphMetrics(width, LayoutMorphEngine.StageHeightFor(width));
        LayoutMorphEngine.CarouselCardSize(metrics).Width.ShouldBe(cardWidth);
        LayoutMorphEngine.CarouselCardSize(metrics).Height.ShouldBe(cardWidth + 76);
        LayoutMorphEngine.CarouselRadius(metrics).ShouldBe(radius, 1e-9);
        (LayoutMorphEngine.CarouselStep(metrics) * radius).ShouldBe(arc, 1e-6);
    }

    [Fact]
    public void Carousel_is_a_strip_with_two_ends()
    {
        // On the first card nothing shows before it; on the last nothing after it (Arc measured both).
        var first = Arrange(LayoutMorphMode.Carousel, 1080, 600, front: 0);
        Enumerable.Range(0, Count).Where(i => first[i].Opacity > 0).ShouldBe([0, 1, 2, 3]);
        first[3].Opacity.ShouldBe(0.91, 0.005);
        var last = Arrange(LayoutMorphMode.Carousel, 1080, 600, front: Count - 1);
        Enumerable.Range(0, Count).Where(i => last[i].Opacity > 0).ShouldBe([20, 21, 22, 23]);
        LayoutMorphEngine.CarouselOpacity(0.80).ShouldBe(1, 1e-9);
        LayoutMorphEngine.CarouselOpacity(1.15).ShouldBe(0, 1e-9);
        LayoutMorphEngine.CarouselOpacity(-0.975).ShouldBe(0.5, 1e-9);
        // An angle a whole turn on is still far from the front.
        LayoutMorphEngine.CarouselOpacity(2 * Math.PI).ShouldBe(0);
    }

    [Fact]
    public void A_face_turned_from_the_eye_is_a_back_face_in_perspective()
    {
        // Flat at the center: facing. Turned a little under a quarter turn but 1083px to the side and 711px back (the
        // carousel's fourth card at 1080): already turned away from an eye 1242px in front.
        var flat = LayoutMorphEngine.CardTransform(Card(0, 0, 0, 0), 0, 0, 0, LayoutMorphCamera.Identity, 0, 0);
        LayoutMorphEngine.FacesAway(flat, 320, 396, 1242).ShouldBeFalse();
        var side = LayoutMorphEngine.CardTransform(Card(1083, 0, -711, 1.153), 0, 0, 0, LayoutMorphCamera.Identity, 0, 0);
        LayoutMorphEngine.FacesAway(side, 320, 396, 1242).ShouldBeTrue();
        var near = LayoutMorphEngine.CardTransform(Card(0, 0, 0, 1.4), 0, 0, 0, LayoutMorphCamera.Identity, 0, 0);
        LayoutMorphEngine.FacesAway(near, 320, 396, 1242).ShouldBeFalse();
        var turned = LayoutMorphEngine.CardTransform(Card(0, 0, 0, 1.7), 0, 0, 0, LayoutMorphCamera.Identity, 0, 0);
        LayoutMorphEngine.FacesAway(turned, 320, 396, 1242).ShouldBeTrue();

        static LayoutMorphPose Card(double x, double y, double z, double rotY) =>
            new() { X = x, Y = y, Z = z, RotY = rotY, Width = 320, Height = 396 };
    }

    [Fact]
    public void Dragging_past_an_end_stretches_with_resistance()
    {
        var metrics = new LayoutMorphMetrics(1080, 600);
        var arc = LayoutMorphEngine.ArcPerCard(LayoutMorphMode.Carousel, metrics);
        LayoutMorphEngine.StretchFront(5.5, Count, arc, metrics.InnerHeight).ShouldBe(5.5);
        // Arc: 600px past the first card shows about 200px (carousel 203, helix 198).
        var stretched = LayoutMorphEngine.StretchFront(-600 / arc, Count, arc, metrics.InnerHeight) * arc;
        stretched.ShouldBe(-206.5, 1);
        var helixArc = LayoutMorphEngine.ArcPerCard(LayoutMorphMode.Helix, metrics);
        (LayoutMorphEngine.StretchFront(Count - 1 + 600 / helixArc, Count, helixArc, metrics.InnerHeight) - (Count - 1)).ShouldBe(206.5 / helixArc, 0.01);
    }

    // Helix --------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1312, 160, 208, 281.54, 33.3, 12)]
    [InlineData(900, 135, 176, 241.09, 28.5, 12)]
    [InlineData(738, 111, 144, 202.25, 23.7, 12)]
    [InlineData(647, 97, 126, 179.60, 21.0, 12)]
    [InlineData(540, 81, 105, 153.71, 17.85, 12)]
    [InlineData(447, 67, 87, 121.35, 14.25, 9)]
    [InlineData(358, 66, 86, 119.73, 14.1, 9)]
    public void Helix_geometry_across_widths(double width, double w, double h, double radius, double step, double cardRadius)
    {
        var metrics = new LayoutMorphMetrics(width, LayoutMorphEngine.StageHeightFor(width));
        LayoutMorphEngine.HelixCardSize(metrics).ShouldBe(new Size(w, h));
        LayoutMorphEngine.HelixRadius(metrics).ShouldBe(radius, 0.01);
        LayoutMorphEngine.HelixStep(metrics).ShouldBe(step, 0.01);
        Arrange(LayoutMorphMode.Helix, width, LayoutMorphEngine.StageHeightFor(width), 11)[0].Radius.ShouldBe(cardRadius);
    }

    [Fact]
    public void Helix_spirals_ten_cards_a_turn_and_keeps_the_front_card_centered()
    {
        var metrics = new LayoutMorphMetrics(1312, 600);
        var poses = new LayoutMorphPose[Count];
        LayoutMorphEngine.Arrange(LayoutMorphMode.Helix, metrics, new double[Count], 12, poses, out var camera, out _);
        // Arc at 1312 with card 12 in front: card 0 at (−347.76, −486.95, −194.54), turned −1.2566.
        Left(poses[0]).ShouldBe(-347.76, 0.01);
        Top(poses[0]).ShouldBe(-486.95, 0.01);
        poses[0].Z.ShouldBe(-194.54, 0.01);
        ShowpieceProjection.WrapAngle(poses[0].RotY).ShouldBe(-1.2566, 1e-4);
        camera.Tilt.ShouldBe(-0.24);
        camera.TranslateY.ShouldBe(-66.92, 0.01);
        camera.FocusY.ShouldBe(-16.65, 0.01);
        camera.Radius.ShouldBe(281.54, 0.01);
        // The front card's center lands on the stage center.
        var center = camera.Transform.Transform(poses[12].X, poses[12].Y, poses[12].Z);
        center.X.ShouldBe(0, 1e-9);
        center.Y.ShouldBe(0, 1e-9);
        // Faces turned away: a step to 0.72.
        poses[12].Opacity.ShouldBe(1);
        poses[15].Opacity.ShouldBe(LayoutMorphEngine.HelixBackOpacity);
        poses[22].Opacity.ShouldBe(1);
        poses[0].PullZ.ShouldBe(28);
    }

    [Fact]
    public void Default_fronts_match_arc()
    {
        LayoutMorphEngine.DefaultFront(LayoutMorphMode.Carousel, 24).ShouldBe(2);
        LayoutMorphEngine.DefaultFront(LayoutMorphMode.Helix, 24).ShouldBe(11);
        LayoutMorphEngine.DefaultTabStop(24).ShouldBe(2);
        LayoutMorphEngine.DefaultTabStop(1).ShouldBe(0);
    }

    // Open detail --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Open_detail_sizes()
    {
        var wide = new LayoutMorphMetrics(1080, 600);
        var hero = LayoutMorphEngine.OpenPose(wide, LayoutMorphOpenVariant.Hero, out var heroSheet);
        hero.Width.ShouldBe(940);
        hero.Height.ShouldBe(560);
        hero.Radius.ShouldBe(28);
        hero.MediaWidth.ShouldBe(584);
        hero.MediaHeight.ShouldBe(544);
        hero.MediaRadius.ShouldBe(20);
        heroSheet.Left.ShouldBe(614);
        heroSheet.In(940, 560).Width.ShouldBe(296);
        hero.UsesCamera.ShouldBeFalse();
        hero.Caption.ShouldBe(LayoutMorphCaption.Sheet);

        var profile = LayoutMorphEngine.OpenPose(wide, LayoutMorphOpenVariant.Profile, out var profileSheet);
        profile.Width.ShouldBe(640);
        profile.Height.ShouldBe(392);
        profile.MediaWidth.ShouldBe(196);
        profile.MediaHeight.ShouldBe(236);
        profile.MediaRadius.ShouldBe(16);
        profileSheet.Left.ShouldBe(240);
        profileSheet.In(640, 392).Width.ShouldBe(370);

        var narrow = LayoutMorphEngine.OpenPose(new LayoutMorphMetrics(358, 460), LayoutMorphOpenVariant.Hero, out var stacked);
        narrow.Width.ShouldBe(334);
        narrow.Height.ShouldBe(436);
        narrow.Radius.ShouldBe(24);
        narrow.MediaWidth.ShouldBe(318);
        narrow.MediaHeight.ShouldBe(201);
        narrow.MediaRadius.ShouldBe(18);
        stacked.IsStacked.ShouldBeTrue();
        stacked.Top.ShouldBe(221.6, 0.1);
    }

    // Covers ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(106, 11.66, 13.78, 12.72)]
    [InlineData(44, 4.84, 10, 10)]
    [InlineData(196, 21.56, 20, 18)]
    public void Cover_type_follows_the_short_side(double side, double padding, double title, double value)
    {
        LayoutMorphEngine.CoverPadding(side).ShouldBe(padding, 0.01);
        LayoutMorphEngine.CoverTitleSize(side).ShouldBe(title, 0.01);
        LayoutMorphEngine.CoverValueSize(side).ShouldBe(value, 0.01);
    }

    // Stagger and flight -------------------------------------------------------------------------------------------------

    [Fact]
    public void Stagger_waits_330ms_times_distance_over_the_farthest()
    {
        Point[] starts = [new(10, 0), new(50, 0), new(100, 0), new(0, 200)];
        var delays = new double[4];
        LayoutMorphEngine.StaggerDelays(starts, new Point(0, 0), 0.33, delays);
        delays[3].ShouldBe(0.33, 1e-12);
        delays[0].ShouldBe(0.33 * 10 / 200, 1e-12);
        delays[2].ShouldBe(0.33 * 100 / 200, 1e-12);
        LayoutMorphEngine.StaggerDelays(starts, new Point(0, 0), 0, delays);
        delays.ShouldAllBe(d => d == 0);
    }

    [Fact]
    public void Progress_spring_matches_the_measured_curve()
    {
        // 50ms .10, 100 .27, 150 .44, 200 .59, 300 .79, 400 .90, 500 .95, 800 .995 (± frame quantization).
        var expected = new (double Ms, double P)[] { (50, 0.10), (100, 0.27), (150, 0.44), (200, 0.59), (300, 0.79), (400, 0.90), (500, 0.95), (800, 0.995) };
        foreach (var (ms, p) in expected)
        {
            Spring(ShowpieceSprings.MorphProgress, ms).ShouldBe(p, 0.02, $"{ms}ms");
        }
        RestTime(ShowpieceSprings.MorphProgress, 2e-4, 2e-3).ShouldBe(1.15, 0.12);
    }

    [Fact]
    public void Caption_and_lift_springs_match_the_measured_curves()
    {
        // Caption out (ω 25): 16ms .91, 50 .62, 100 .30, 150 .13, 200 .06.
        foreach (var (ms, v) in new (double, double)[] { (16, 0.91), (50, 0.62), (100, 0.30), (150, 0.13), (200, 0.06) })
        {
            (1 - Spring(ShowpieceSprings.MorphCaptionOut, ms)).ShouldBe(v, 0.035, $"out {ms}ms");
        }
        // Caption in (ω 10): 50ms .10, 130 .40, 200 .61, 300 .80, 480 .95.
        foreach (var (ms, v) in new (double, double)[] { (50, 0.10), (130, 0.40), (200, 0.61), (300, 0.80), (480, 0.95) })
        {
            Spring(ShowpieceSprings.MorphCaptionIn, ms).ShouldBe(v, 0.03, $"in {ms}ms");
        }
        // Lift (k 500, c 49): 16ms .07, 50 .31, 100 .63, 150 .81, 250 .955.
        foreach (var (ms, v) in new (double, double)[] { (16, 0.07), (50, 0.31), (100, 0.63), (150, 0.81), (250, 0.955) })
        {
            Spring(ShowpieceSprings.MorphLift, ms).ShouldBe(v, 0.03, $"lift {ms}ms");
        }
    }

    [Fact]
    public void Flight_lift_and_lean_peak_at_0_1_D_and_6_2e5_per_px()
    {
        var channel = new ShowpieceSpringChannel(0, 1e-6, 1e-6);
        channel.SpringTo(1, ShowpieceSprings.MorphProgress);
        var peak = 0.0;
        var peakAt = 0.0;
        for (var t = 0; t < 600; t++)
        {
            channel.Step(0.001);
            if (channel.Velocity > peak)
            {
                peak = channel.Velocity;
                peakAt = (t + 1) / 1000.0;
            }
        }
        peakAt.ShouldBe(0.1, 0.002);
        // D = 895 → 88 measured; 804px across → ±0.05 rad.
        LayoutMorphEngine.FlightLift(895, peak).ShouldBe(89.5, 2);
        LayoutMorphEngine.FlightLift(119, peak).ShouldBe(12, 0.5);
        LayoutMorphEngine.FlightTilt(804, peak).ShouldBe(0.05, 0.002);
        LayoutMorphEngine.FlightTilt(-804, peak).ShouldBe(-0.05, 0.002);
        LayoutMorphEngine.FlightLift(895, 0).ShouldBe(0);
    }

    [Fact]
    public void Release_snap_projects_the_flick_over_a_quarter_second()
    {
        const double arc = 342.4;
        // A slow 80px drag springs back; 50px at 530px/s goes one card; 300px at 1500 two, at 3400 three.
        // Dragging right by d px moves the front position by −d / arc (earlier cards come in).
        LayoutMorphEngine.SnapFront(-80 / arc, 40, arc).ShouldBe(0);
        LayoutMorphEngine.SnapFront(-50 / arc, 530, arc).ShouldBe(-1);
        LayoutMorphEngine.SnapFront(-300 / arc, 1500, arc).ShouldBe(-2);
        LayoutMorphEngine.SnapFront(-300 / arc, 3400, arc).ShouldBe(-3);
        // Helix (arc 176.9): 300px at 3200px/s turns six cards.
        var helixArc = 281.54 * 2 * Math.PI / 10;
        LayoutMorphEngine.SnapFront(11 - 300 / helixArc, 3200, helixArc).ShouldBe(5);
        LayoutMorphEngine.ArcPerCard(LayoutMorphMode.Carousel, new LayoutMorphMetrics(1080, 600)).ShouldBe(342.4, 0.01);
    }

    // Projection -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_baked_pose_draws_exactly_like_the_pose_under_its_camera()
    {
        var metrics = new LayoutMorphMetrics(1080, 600);
        var poses = new LayoutMorphPose[Count];
        LayoutMorphEngine.Arrange(LayoutMorphMode.Helix, metrics, new double[Count], 11.3, poses, out var camera, out _);
        foreach (var index in new[] { 3, 9, 11, 14, 20 })
        {
            var underCamera = LayoutMorphEngine.ScreenMatrix(
                LayoutMorphEngine.CardTransform(poses[index], 0, 0, 0, camera, -40, 120), metrics.Perspective, new Size(1080, 600));
            var baked = LayoutMorphEngine.Bake(poses[index], camera, -40, 120);
            baked.UsesCamera.ShouldBeFalse();
            var flat = LayoutMorphEngine.ScreenMatrix(
                LayoutMorphEngine.CardTransform(baked, 0, 0, 0, LayoutMorphCamera.Identity, 0, 0), metrics.Perspective, new Size(1080, 600));
            foreach (var corner in new[] { new Point(0, 0), new Point(160, 0), new Point(0, 208), new Point(160, 208) })
            {
                var a = ShowpieceProjection.Apply(underCamera, corner);
                var b = ShowpieceProjection.Apply(flat, corner);
                a.X.ShouldBe(b.X, 1e-6);
                a.Y.ShouldBe(b.Y, 1e-6);
            }
        }
    }

    [Fact]
    public void Flat_poses_project_without_perspective()
    {
        var poses = Arrange(LayoutMorphMode.Grid, 1080, 600);
        var m = LayoutMorphEngine.ScreenMatrix(LayoutMorphEngine.CardTransform(poses[0], 0, 0, 0, LayoutMorphCamera.Identity, 0, 0),
            1242, new Size(1080, 600));
        m.M13.ShouldBe(0, 1e-12);
        m.M23.ShouldBe(0, 1e-12);
        ShowpieceProjection.Apply(m, new Point(0, 0)).ShouldBe(new Point(540 - 513, 300 - 248));
    }

    [Fact]
    public void Shortest_turn_between_layouts()
    {
        var a = new LayoutMorphPose { RotY = 3.0 };
        var b = new LayoutMorphPose { RotY = -3.0 };
        // 3.0 → −3.0 is 0.28 rad the short way round, not 6 rad back.
        LayoutMorphPose.Lerp(a, b, 0.5).RotY.ShouldBe(3.0 + (2 * Math.PI - 6.0) / 2, 1e-9);
    }

    private static double Spring(in Motion.SpringSpec spec, double ms)
    {
        var channel = new ShowpieceSpringChannel(0, 1e-9, 1e-9);
        channel.SpringTo(1, spec);
        channel.Step(ms / 1000.0);
        return channel.Value;
    }

    private static double RestTime(in Motion.SpringSpec spec, double restDelta, double restSpeed)
    {
        var channel = new ShowpieceSpringChannel(0, restDelta, restSpeed);
        channel.SpringTo(1, spec);
        var t = 0.0;
        while (channel.Step(1 / 60.0) && t < 5)
        {
            t += 1 / 60.0;
        }
        return t;
    }
}
