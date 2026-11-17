using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>The sheet stack's pure layout, drag model and motion constants against the measured reference numbers.</summary>
public class SheetStackLogicTests
{
    private static readonly SheetStackMetrics M = SheetStackMetrics.Default;

    [Fact]
    public void Available_height_drops_one_peek_per_level_below()
    {
        SheetStackLayout.AvailableHeight(M, 578, false, 0).ShouldBe(558);
        SheetStackLayout.AvailableHeight(M, 578, false, 1).ShouldBe(548);
        SheetStackLayout.AvailableHeight(M, 578, false, 2).ShouldBe(538);
        SheetStackLayout.AvailableHeight(M, 578, true, 0).ShouldBe(498);
        SheetStackLayout.AvailableHeight(M, 578, true, 1).ShouldBe(484);
        SheetStackLayout.AvailableHeight(M, 578, true, 2).ShouldBe(470);
    }

    [Fact]
    public void Panel_width_and_anchor()
    {
        SheetStackLayout.PanelWidth(M, 742, false).ShouldBe(560);
        SheetStackLayout.PanelWidth(M, 342, false).ShouldBe(342);
        SheetStackLayout.PanelWidth(M, 742, true).ShouldBe(440);
        SheetStackLayout.LayoutTop(578, 504, false).ShouldBe(74);
        SheetStackLayout.LayoutTop(578, 411, true).ShouldBe(83.5);
    }

    [Fact]
    public void Auto_mode_switches_at_the_breakpoint()
    {
        SheetStackLayout.IsDialog(SheetStackMode.Auto, 652, 640).ShouldBeTrue();
        SheetStackLayout.IsDialog(SheetStackMode.Auto, 640, 640).ShouldBeTrue();
        SheetStackLayout.IsDialog(SheetStackMode.Auto, 592, 640).ShouldBeFalse();
        SheetStackLayout.IsDialog(SheetStackMode.Sheet, 1200, 640).ShouldBeFalse();
        SheetStackLayout.IsDialog(SheetStackMode.Dialog, 300, 640).ShouldBeTrue();
    }

    [Fact]
    public void Sheets_below_peek_ten_above_the_top_sheet()
    {
        // Three sheets 316, 444 and 504 high in a 578 layer: tops 262, 134, 74. Arc: L1 ty -208 (top 54), L2 -69.5 (64.5).
        var reference = SheetStackLayout.LayoutTop(578, 504, false);
        var l1 = SheetStackLayout.Pose(M, false, 2, reference, 262, true);
        var l2 = SheetStackLayout.Pose(M, false, 1, reference, 134, true);
        l1.TranslateY.ShouldBe(-208, 0.01);
        l1.Scale.ShouldBe(0.90, 1e-9);
        l1.Dim.ShouldBe(1);
        l2.TranslateY.ShouldBe(-70, 0.01);
        l2.Scale.ShouldBe(0.95, 1e-9);
        l2.Dim.ShouldBe(0.5);
        (134 + l2.TranslateY).ShouldBe(64, 0.6);
    }

    [Fact]
    public void Dialogs_below_peek_fourteen_above_the_top_dialog()
    {
        // Dialogs 303, 431 and 411 high: Arc L1 ty -82 (top 55.5), L2 -4.1 (top 69.4), L3 top 83.5.
        var reference = SheetStackLayout.LayoutTop(578, 411, true);
        var l1 = SheetStackLayout.Pose(M, true, 2, reference, SheetStackLayout.LayoutTop(578, 303, true), true);
        var l2 = SheetStackLayout.Pose(M, true, 1, reference, SheetStackLayout.LayoutTop(578, 431, true), true);
        l1.TranslateY.ShouldBe(-82, 0.01);
        l2.TranslateY.ShouldBe(-4, 0.2);
    }

    [Fact]
    public void A_shorter_top_sheet_pushes_the_level_below_down()
    {
        // 390 wide: the third level is only 360 high, so the 444 high second level moves down to peek above it.
        var reference = SheetStackLayout.LayoutTop(578, 360, false);
        var pose = SheetStackLayout.Pose(M, false, 1, reference, SheetStackLayout.LayoutTop(578, 444, false), true);
        pose.TranslateY.ShouldBeGreaterThan(70);
    }

    [Fact]
    public void Dragging_the_top_interpolates_the_level_below_linearly()
    {
        // Offset 54 on a 444 high top: Arc measured the parent at dim 0.439, scale 0.9561, ty -121.2.
        var p = SheetStackLayout.DragProgress(54, 444);
        var pose = SheetStackLayout.Pose(M, false, 1 - p, 134, 262, true);
        pose.Dim.ShouldBe(0.439, 0.001);
        pose.Scale.ShouldBe(0.9561, 0.0001);
        pose.TranslateY.ShouldBe(-121.2, 0.1);
        SheetStackLayout.DragProgress(-20, 444).ShouldBe(0);
    }

    [Fact]
    public void Reduced_motion_keeps_only_the_dim()
    {
        var pose = SheetStackLayout.Pose(M, false, 1, 134, 262, false);
        pose.TranslateY.ShouldBe(0);
        pose.Scale.ShouldBe(1);
        pose.Dim.ShouldBe(0.5);
    }

    [Fact]
    public void Drag_starts_after_six_pixels_and_then_follows_one_to_one()
    {
        var model = new SheetStackDragModel();
        model.Press(100, 0);
        model.Move(106, 0.01).ShouldBeFalse();
        model.IsDragging.ShouldBeFalse();
        model.Move(107, 0.02).ShouldBeTrue();
        model.RawOffset.ShouldBe(1);
        model.Move(108, 0.03);
        model.RawOffset.ShouldBe(2);
        model.Move(110, 0.04);
        model.RawOffset.ShouldBe(4);
        model.Move(115, 0.05);
        model.RawOffset.ShouldBe(9);
    }

    [Fact]
    public void Upward_drag_resists_like_the_reference()
    {
        // Arc, H = 444: u 2 -> 0.6, 94 -> 26.52, 294 -> 73.58; H = 316: u 94 -> 25.88.
        SheetStackDragModel.Display(-2, 444, true).ShouldBe(-0.6, 0.05);
        SheetStackDragModel.Display(-94, 444, true).ShouldBe(-26.52, 0.05);
        SheetStackDragModel.Display(-294, 444, true).ShouldBe(-73.58, 0.05);
        SheetStackDragModel.Display(-94, 316, true).ShouldBe(-25.88, 0.05);
        SheetStackDragModel.Display(120, 444, true).ShouldBe(120);
        SheetStackDragModel.Display(120, 444, false).ShouldBeLessThan(40);
    }

    [Fact]
    public void Release_rule_matches_the_reference_thresholds()
    {
        // 35% of the exit travel (height + 24).
        SheetStackDragModel.Decide(163, 0, 444, true).ShouldBe(SheetStackRelease.Return);
        SheetStackDragModel.Decide(165, 0, 444, true).ShouldBe(SheetStackRelease.Dismiss);
        SheetStackDragModel.Decide(118.5, 0, 316, true).ShouldBe(SheetStackRelease.Return);
        SheetStackDragModel.Decide(121, 0, 316, true).ShouldBe(SheetStackRelease.Dismiss);
        SheetStackDragModel.Decide(158, 0, 431, true).ShouldBe(SheetStackRelease.Return);
        SheetStackDragModel.Decide(162, 0, 431, true).ShouldBe(SheetStackRelease.Dismiss);
        // 450 px/s, any distance; an upward flick cancels.
        SheetStackDragModel.Decide(60, 422, 444, true).ShouldBe(SheetStackRelease.Return);
        SheetStackDragModel.Decide(60, 475, 444, true).ShouldBe(SheetStackRelease.Dismiss);
        SheetStackDragModel.Decide(24, 640, 444, true).ShouldBe(SheetStackRelease.Dismiss);
        SheetStackDragModel.Decide(200, -222, 444, true).ShouldBe(SheetStackRelease.Dismiss);
        SheetStackDragModel.Decide(200, -556, 444, true).ShouldBe(SheetStackRelease.Return);
        SheetStackDragModel.Decide(300, 900, 444, false).ShouldBe(SheetStackRelease.Return);
    }

    [Fact]
    public void Release_velocity_averages_the_last_hundred_milliseconds()
    {
        var model = new SheetStackDragModel();
        model.Press(0, 0);
        var t = 0.0;
        var y = 0.0;
        for (var i = 0; i < 20; i++)
        {
            t += 1.0 / 60;
            y += 10;
            model.Move(y, t);
        }
        model.Release(t + 0.005).ShouldBe(600, 1);
    }

    [Fact]
    public void A_flick_with_a_single_move_after_the_start_has_no_velocity()
    {
        var model = new SheetStackDragModel();
        model.Press(0, 0);
        model.Move(15, 0.016);
        model.Move(30, 0.032);
        model.Release(0.033).ShouldBe(0);
    }

    [Fact]
    public void A_pause_before_release_drops_the_velocity()
    {
        var model = new SheetStackDragModel();
        model.Press(0, 0);
        for (var i = 1; i <= 10; i++)
        {
            model.Move(i * 10, i / 60.0);
        }
        model.Release(10 / 60.0 + 0.2).ShouldBe(0);
    }

    [Fact]
    public void Springs_match_the_fitted_constants()
    {
        SheetStackMotion.Rise.NaturalFrequency.ShouldBe(12, 1e-9);
        SheetStackMotion.Rise.DampingRatio.ShouldBe(0.945, 1e-9);
        SheetStackMotion.Depth.NaturalFrequency.ShouldBe(12.3, 1e-9);
        SheetStackMotion.Depth.DampingRatio.ShouldBe(0.99, 1e-9);
        SheetStackMotion.Exit.NaturalFrequency.ShouldBe(15.3, 1e-9);
        SheetStackMotion.Exit.DampingRatio.ShouldBe(1, 1e-9);
        SheetStackMotion.Height.ShouldBe(ArcMotionPresets.Smooth);
    }

    [Fact]
    public void Spring_keeps_its_velocity_when_retargeted()
    {
        var spring = new SheetStackSpring(468, 0.1, 5);
        spring.To(0, SheetStackMotion.Rise);
        for (var i = 0; i < 11; i++)
        {
            spring.Step(1.0 / 60);
        }
        spring.Velocity.ShouldBeLessThan(-500);
        var before = spring.Value;
        spring.To(468, SheetStackMotion.Exit);
        spring.Step(1.0 / 60);
        // Still moving up for a moment: the turn is smooth, not a restart.
        spring.Value.ShouldBeLessThan(before);
    }
}
