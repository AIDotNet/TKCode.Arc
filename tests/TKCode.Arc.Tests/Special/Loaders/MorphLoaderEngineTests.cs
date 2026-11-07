using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>
/// The morph loader's motion stepped frame by frame against the measured preview: the three fitted springs, the loop
/// clock and weight, the staggered fold into a result, the forward turn to upright with the loop's speed, the gather back,
/// the length lag when leaving the dots, velocity kept across interruptions and the static shapes of reduced motion.
/// </summary>
public class MorphLoaderEngineTests
{
    private const double Frame = 1.0 / 60;
    private const int L = MorphLoaderStroke.LengthIndex;

    private static void Run(MorphLoaderEngine engine, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += Frame)
        {
            engine.Step(Frame);
        }
    }

    private static MorphLoaderStroke[] Rest(MorphLoaderShape shape)
    {
        var strokes = new MorphLoaderStroke[4];
        MorphLoaderShapes.WriteRest(shape, 2.5, 4, 3, strokes);
        return strokes;
    }

    [Fact]
    public void Springs_match_the_fitted_constants()
    {
        MorphLoaderEngine.ShapeSpring.Stiffness.ShouldBe(130.6, 1.5);
        MorphLoaderEngine.ShapeSpring.Damping.ShouldBe(22.9, 0.3);
        MorphLoaderEngine.ResultSpring.Stiffness.ShouldBe(101.3, 1.5);
        MorphLoaderEngine.ResultSpring.Damping.ShouldBe(20.13, 0.3);
        MorphLoaderEngine.TurnSpring.Stiffness.ShouldBe(76.3, 1.5);
        MorphLoaderEngine.TurnSpring.Damping.ShouldBe(17.5, 0.3);
        foreach (var spec in new[] { MorphLoaderEngine.ShapeSpring, MorphLoaderEngine.ResultSpring, MorphLoaderEngine.TurnSpring, MorphLoaderEngine.WeightSpring })
        {
            spec.DampingRatio.ShouldBe(1, 1e-9);
        }
    }

    [Fact]
    public void Loading_loops_at_full_weight_from_the_first_frame()
    {
        var engine = new MorphLoaderEngine();
        engine.LoopWeight.ShouldBe(1);
        engine.NeedsFrames.ShouldBeTrue();
        Run(engine, 0.225);
        // Dot 0 is at the top of its hop.
        engine.Strokes[0].Cy.ShouldBe(12 - 3.2, 0.05);
    }

    [Fact]
    public void A_stalled_frame_advances_the_loop_by_at_most_a_sixteenth_of_a_second()
    {
        var engine = new MorphLoaderEngine();
        engine.Step(0.5);
        engine.LoopTime.ShouldBe(1.0 / 16, 1e-12);
    }

    [Fact]
    public void Result_freezes_the_loop_at_once_and_stops_its_clock()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Bars, MorphLoaderStatus.Loading);
        Run(engine, 1.5);
        engine.MorphTo(MorphLoaderVariant.Bars, MorphLoaderStatus.Success);
        engine.LoopWeight.ShouldBe(0);
        // The bars jump back to their 12 long resting length in the same frame.
        engine.Strokes[0].Length.ShouldBe(12, 1e-9);
        var clock = engine.LoopTime;
        Run(engine, 1);
        engine.LoopTime.ShouldBe(clock);
    }

    [Fact]
    public void Result_draws_the_strokes_35ms_apart_on_the_result_spring()
    {
        var engine = new MorphLoaderEngine();
        Run(engine, 1);
        engine.MorphTo(MorphLoaderVariant.Dots, MorphLoaderStatus.Success);
        engine.Step(Frame);
        engine.IsWaiting(0).ShouldBeFalse();
        engine.IsWaiting(1).ShouldBeTrue();
        engine.BaseOf(0, MorphLoaderStroke.CxIndex).ShouldBeGreaterThan(5);
        engine.BaseOf(1, MorphLoaderStroke.CyIndex).ShouldBe(12);
        engine.Step(Frame);
        engine.Step(Frame);
        engine.IsWaiting(1).ShouldBeFalse();
        engine.IsWaiting(2).ShouldBeTrue();
        // Stroke 0 follows the closed-form result spring from the dot at (5, 12) to (6.3, 13.8): after one frame.
        var check = Rest(MorphLoaderShape.Check);
        var (d, _) = SpringSolver.Evaluate(MorphLoaderEngine.ResultSpring, 5 - check[0].Cx, 0, 3 * Frame);
        engine.BaseOf(0, MorphLoaderStroke.CxIndex).ShouldBe(check[0].Cx + d, 1e-9);
        Run(engine, 2);
        for (var s = 0; s < 4; s++)
        {
            engine.Strokes[s].Cx.ShouldBe(check[s].Cx, 1e-3);
            engine.Strokes[s].Cy.ShouldBe(check[s].Cy, 1e-3);
            engine.Strokes[s].Length.ShouldBe(check[s].Length, 1e-3);
            engine.Strokes[s].Width.ShouldBe(2.5, 1e-3);
            engine.Strokes[s].Opacity.ShouldBe(1, 1e-3);
        }
        // The dots (direction 0) aim at the long side the short way: -49.2, not 130.8.
        engine.Strokes[2].Angle.ShouldBe(-49.214, 1e-3);
        engine.IsSettling.ShouldBeFalse();
        engine.NeedsFrames.ShouldBeFalse();
    }

    [Fact]
    public void Result_pops_to_1_14_at_148ms_and_back_by_420ms()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Dots, MorphLoaderStatus.Error);
        var peak = 0.0;
        for (var i = 0; i < 30; i++)
        {
            engine.Step(Frame);
            peak = Math.Max(peak, engine.Scale);
        }
        peak.ShouldBe(1.14, 0.002);
        engine.Scale.ShouldBe(1);
    }

    [Fact]
    public void Ring_turns_330_degrees_a_second_and_a_result_turns_forward_to_upright_keeping_that_speed()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading);
        var before = engine.Rotation;
        Run(engine, 0.5);
        (engine.Rotation - before).ShouldBe(165, 0.5);
        engine.RotationVelocity.ShouldBe(330);
        var theta = engine.Rotation;
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Success);
        engine.RotationVelocity.ShouldBe(330, 1e-9);
        engine.Step(Frame);
        engine.Rotation.ShouldBeGreaterThan(theta);
        Run(engine, 2.5);
        (engine.Rotation % 360).ShouldBe(0, 1e-9);
        engine.Rotation.ShouldBe(MorphLoaderShapes.NextUpright(theta), 1e-3);
    }

    [Fact]
    public void Square_tumbles_a_quarter_turn_per_cycle_from_wherever_it_is()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading);
        Run(engine, 0.3);
        engine.MorphTo(MorphLoaderVariant.Square, MorphLoaderStatus.Loading);
        // Entering the square, the ring's turn stops at once (no velocity carried).
        var start = engine.Rotation;
        var startTurns = MorphLoaderShapes.SquareTurns(engine.LoopTime);
        Run(engine, 0.9);
        var turns = MorphLoaderShapes.SquareTurns(engine.LoopTime) - startTurns;
        (engine.Rotation - start).ShouldBe(90 * turns, 1e-6);
        Run(engine, 2.7);
        (engine.Rotation - start).ShouldBe(90 * (MorphLoaderShapes.SquareTurns(engine.LoopTime) - startTurns), 1e-6);
    }

    [Fact]
    public void Dots_and_bars_turn_forward_to_upright_from_a_tilted_square()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Square, MorphLoaderStatus.Loading);
        Run(engine, 0.55);
        var theta = engine.Rotation;
        (theta % 90).ShouldNotBe(0);
        engine.MorphTo(MorphLoaderVariant.Dots, MorphLoaderStatus.Loading);
        engine.RotationFollowsLoop.ShouldBeFalse();
        Run(engine, 2);
        engine.Rotation.ShouldBe(MorphLoaderShapes.NextUpright(theta), 1e-3);
    }

    [Fact]
    public void Leaving_the_dots_the_length_starts_a_dot_width_below_zero()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Bars, MorphLoaderStatus.Loading);
        engine.BaseOf(0, L).ShouldBe(-4);
        engine.BaseOf(3, L).ShouldBe(-4);
        // For the first ~80-110ms the drawn stroke stays a dot while it travels.
        Run(engine, 0.05);
        engine.BaseOf(0, L).ShouldBeLessThan(0.5);
        engine.BaseOf(0, MorphLoaderStroke.CxIndex).ShouldBeLessThan(5);
        // Entering the dots the length springs straight to 0.
        Run(engine, 1.5);
        engine.MorphTo(MorphLoaderVariant.Dots, MorphLoaderStatus.Loading);
        engine.BaseOf(0, L).ShouldBe(12, 1e-3);
        engine.TargetOf(0, L).ShouldBe(0);
    }

    [Fact]
    public void The_fourth_stroke_grows_from_the_center_and_fades_in()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Square, MorphLoaderStatus.Loading);
        engine.Strokes[3].Opacity.ShouldBe(0);
        Run(engine, 0.1);
        engine.Strokes[3].Opacity.ShouldBeInRange(0.01, 0.9);
        engine.Strokes[3].Width.ShouldBeInRange(0.01, 2.5);
        Run(engine, 1.5);
        engine.Strokes[3].Opacity.ShouldBe(1, 1e-3);
        engine.Strokes[3].Cx.ShouldBe(5, 1e-3);
    }

    [Fact]
    public void An_interrupted_morph_keeps_its_velocity()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Square, MorphLoaderStatus.Loading);
        Run(engine, 0.184);
        var position = engine.BaseOf(0, MorphLoaderStroke.CxIndex);
        var velocity = engine.VelocityOf(0, MorphLoaderStroke.CxIndex);
        velocity.ShouldNotBe(0);
        engine.MorphTo(MorphLoaderVariant.Dots, MorphLoaderStatus.Loading);
        engine.BaseOf(0, MorphLoaderStroke.CxIndex).ShouldBe(position);
        engine.VelocityOf(0, MorphLoaderStroke.CxIndex).ShouldBe(velocity);
        engine.Step(Frame);
        // Continuous: one frame later it moved by about velocity * frame.
        (engine.BaseOf(0, MorphLoaderStroke.CxIndex) - position).ShouldBe(velocity * Frame, Math.Abs(velocity * Frame) * 0.5 + 0.01);
    }

    [Fact]
    public void Returning_to_loading_gathers_together_and_fades_the_loop_in_within_100ms()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading);
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Success);
        Run(engine, 1.7);
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading);
        engine.LoopWeight.ShouldBe(0);
        // No stagger on the way back.
        for (var s = 0; s < 4; s++)
        {
            engine.IsWaiting(s).ShouldBeFalse();
        }
        engine.Step(Frame);
        engine.Step(Frame);
        engine.LoopWeight.ShouldBe(0.5, 0.06);
        Run(engine, 0.067);
        engine.LoopWeight.ShouldBeGreaterThan(0.95);
        // The ring speeds up with the weight.
        engine.RotationVelocity.ShouldBe(330 * engine.LoopWeight, 1e-9);
    }

    [Fact]
    public void A_variant_change_during_a_result_is_picked_up_when_loading_resumes()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Dots, MorphLoaderStatus.Success);
        Run(engine, 0.5);
        engine.MorphTo(MorphLoaderVariant.Square, MorphLoaderStatus.Success);
        engine.TargetOf(0, L).ShouldBe(Rest(MorphLoaderShape.Check)[0].Length, 1e-9);
        engine.MorphTo(MorphLoaderVariant.Square, MorphLoaderStatus.Loading);
        engine.TargetOf(0, L).ShouldBe(14);
        engine.RotationFollowsLoop.ShouldBeTrue();
    }

    [Fact]
    public void Success_to_error_folds_again_with_a_new_pop()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Bars, MorphLoaderStatus.Success);
        Run(engine, 1);
        engine.MorphTo(MorphLoaderVariant.Bars, MorphLoaderStatus.Error);
        engine.Step(Frame);
        engine.Scale.ShouldBeGreaterThan(1);
        Run(engine, 1.5);
        var cross = Rest(MorphLoaderShape.Cross);
        for (var s = 0; s < 4; s++)
        {
            engine.Strokes[s].Cx.ShouldBe(cross[s].Cx, 1e-3);
            engine.Strokes[s].Cy.ShouldBe(cross[s].Cy, 1e-3);
        }
    }

    [Fact]
    public void Without_motion_every_state_is_a_static_shape()
    {
        var engine = new MorphLoaderEngine();
        engine.Jump(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading, false);
        engine.NeedsFrames.ShouldBeFalse();
        engine.Step(Frame);
        engine.Rotation.ShouldBe(0);
        engine.Strokes[0].Length.ShouldBe(MorphLoaderShapes.RingChord, 1e-9);
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Error);
        engine.Strokes[0].Cx.ShouldBe(Rest(MorphLoaderShape.Cross)[0].Cx);
        engine.Scale.ShouldBe(1);
    }

    [Fact]
    public void Stroke_width_changes_reach_the_shape()
    {
        var engine = new MorphLoaderEngine();
        engine.Jump(MorphLoaderVariant.Square, MorphLoaderStatus.Loading, false);
        engine.SetWidths(3, 4, 3);
        engine.Strokes[0].Width.ShouldBe(3);
        engine.Jump(MorphLoaderVariant.Dots, MorphLoaderStatus.Loading, true);
        engine.SetWidths(3, 5, 3);
        Run(engine, 1.5);
        engine.BaseOf(0, MorphLoaderStroke.WidthIndex).ShouldBe(5, 1e-3);
    }

    [Fact]
    public void The_rotation_stays_small_over_a_long_run()
    {
        var engine = new MorphLoaderEngine();
        engine.MorphTo(MorphLoaderVariant.Ring, MorphLoaderStatus.Loading);
        for (var i = 0; i < 60 * 120; i++)
        {
            engine.Step(Frame);
        }
        Math.Abs(engine.Rotation).ShouldBeLessThanOrEqualTo(3600 + 360);
        engine.LoopTime.ShouldBeLessThan(MorphLoaderShapes.LoopWrap);
    }
}
