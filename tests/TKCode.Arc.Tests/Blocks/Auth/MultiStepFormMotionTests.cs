using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Auth;

/// <summary>Motion of the multi-step form against the timings measured on Arc's preview (frame clock driven).</summary>
public class MultiStepFormMotionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Advance(ManualFrameClock clock, Window window, double milliseconds)
    {
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds), Frame);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    /// <summary>Runs exactly <paramref name="count"/> frames (count / 60 s of motion time).</summary>
    private static void Frames(ManualFrameClock clock, Window window, int count)
    {
        clock.Advance(TimeSpan.FromTicks(Frame.Ticks * count), Frame);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    /// <summary>Critically damped smooth spring (ω = 2π / (1.2 · 0.4)) from <paramref name="from"/> to 0 after <paramref name="t"/> s.</summary>
    private static double Smooth(double from, double t)
    {
        var w = 2 * Math.PI / (1.2 * 0.4);
        return from * (1 + w * t) * Math.Exp(-w * t);
    }

    [AvaloniaFact]
    public void Continue_slides_the_new_step_in_from_the_right_and_the_old_one_out_to_the_left()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var oldView = form.ViewOf(form.Steps[0])!;
            MultiStepFormTests.Press(window, MultiStepFormTests.NextButton(form));
            var newView = form.ViewOf(form.Steps[1])!;
            newView.LayerMotion.X.ShouldBe(20, 0.01);
            newView.LayerMotion.Opacity.ShouldBe(0, 0.01);
            oldView.LayerMotion.IsLeaving.ShouldBeTrue();
            oldView.IsHitTestVisible.ShouldBeFalse();
            KeyboardNavigation.GetTabNavigation(oldView).ShouldBe(KeyboardNavigationMode.None);

            Frames(clock, window, 6);
            // Smooth spring (ω 13.1, ζ 1): 20 → 0 is about 12.5 after 100 ms; the old one has gone about 7.5 to the left.
            newView.LayerMotion.X.ShouldBe(Smooth(20, 0.1), 0.05);
            oldView.LayerMotion.X.ShouldBe(-20 + Smooth(20, 0.1), 0.05);
            oldView.LayerMotion.Opacity.ShouldBeLessThan(0.15);
            newView.LayerMotion.Opacity.ShouldBeInRange(0.5, 0.99);

            Advance(clock, window, 40);
            // The old step is transparent after 120 ms; the new one is fully in at 240 ms.
            oldView.LayerMotion.Opacity.ShouldBe(0, 0.001);
            Advance(clock, window, 120);
            newView.LayerMotion.Opacity.ShouldBe(1, 0.001);

            Advance(clock, window, 600);
            newView.LayerMotion.X.ShouldBe(0, 0.01);
            oldView.IsVisible.ShouldBeFalse();
            oldView.LayerMotion.IsLeaving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_runs_the_slide_the_other_way()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        form.CurrentIndex = 1;
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var oldView = form.ViewOf(form.Steps[1])!;
            MultiStepFormTests.Press(window, MultiStepFormTests.BackButton(form));
            var newView = form.ViewOf(form.Steps[0])!;
            newView.LayerMotion.X.ShouldBe(-20, 0.01);
            Frames(clock, window, 6);
            newView.LayerMotion.X.ShouldBe(-Smooth(20, 0.1), 0.05);
            oldView.LayerMotion.X.ShouldBeGreaterThan(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Going_back_mid_swap_turns_the_old_step_around_from_where_it_is()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var first = form.ViewOf(form.Steps[0])!;
            form.CurrentIndex = 1;
            Advance(clock, window, 110);
            var second = form.ViewOf(form.Steps[1])!;
            var caught = first.LayerMotion.X;
            caught.ShouldBeInRange(-12, -4);
            form.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            // The same presenter comes back from where it was, not from −20.
            form.ViewOf(form.Steps[0]).ShouldBeSameAs(first);
            first.IsVisible.ShouldBeTrue();
            first.LayerMotion.IsLeaving.ShouldBeFalse();
            first.LayerMotion.X.ShouldBe(caught, 0.01);
            second.LayerMotion.IsLeaving.ShouldBeTrue();
            // The leftward velocity carries on for a moment before the spring turns it around (no jump, no restart).
            Frames(clock, window, 1);
            first.LayerMotion.X.ShouldBeInRange(caught - 1.5, caught + 1.5);
            Frames(clock, window, 8);
            first.LayerMotion.X.ShouldBeGreaterThan(caught);
            second.LayerMotion.X.ShouldBeGreaterThan(0);
            Advance(clock, window, 800);
            first.LayerMotion.X.ShouldBe(0, 0.01);
            first.LayerMotion.Opacity.ShouldBe(1, 0.001);
            second.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_step_area_springs_to_a_taller_step_and_back_down()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        form.Steps.Insert(1, new MultiStepFormStep { Title = "Tall", Content = new Border { Height = 400 } });
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var size = TestHelpers.Descendant<SpringSizeDecorator>(form, "PART_ViewportSize");
            size.Bounds.Height.ShouldBe(224, 0.6);
            form.CurrentIndex = 1;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var tall = form.ViewOf(form.Steps[1])!.DesiredSize.Height;
            tall.ShouldBeGreaterThan(400);
            Advance(clock, window, 100);
            size.Bounds.Height.ShouldBeInRange(260, tall - 20);
            size.IsMoving.ShouldBeTrue();
            Advance(clock, window, 900);
            size.Bounds.Height.ShouldBe(tall, 0.6);

            // Arc's area never shrinks again; this one springs back down to the shorter step.
            form.CurrentIndex = 2;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Advance(clock, window, 100);
            size.Bounds.Height.ShouldBeInRange(240, tall - 20);
            Advance(clock, window, 900);
            size.Bounds.Height.ShouldBe(224, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_swaps_steps_at_once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Reduced);
        try
        {
            var first = form.ViewOf(form.Steps[0])!;
            MultiStepFormTests.Press(window, MultiStepFormTests.NextButton(form));
            var second = form.ViewOf(form.Steps[1])!;
            first.IsVisible.ShouldBeFalse();
            second.LayerMotion.X.ShouldBe(0);
            second.LayerMotion.Opacity.ShouldBe(1);
            var marker = form.ProgressItems[0].Marker!;
            marker.Check.Opacity.ShouldBe(1);
            marker.Check.Progress.ShouldBe(1);
            marker.Digit.Opacity.ShouldBe(0);
            var digit = TestHelpers.Descendant<MultiStepSwap>(form, MultiStepForm.CountDigitPart);
            digit.ExitingCount.ShouldBe(0);
            var slot = TestHelpers.Descendant<SpringSizeDecorator>(MultiStepFormTests.NextButton(form), "PART_LabelSlot");
            slot.IsMoving.ShouldBeFalse();
            MultiStepFormTests.Press(window, MultiStepFormTests.NextButton(form));
            var success = form.SuccessView!;
            success.LayerMotion.Opacity.ShouldBe(1);
            success.Glyph!.Progress.ShouldBe(1);
            TestHelpers.Descendant<MultiStepCollapse>(form, MultiStepForm.ActionsPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_done_marker_pops_its_check_and_draws_it()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var marker = form.ProgressItems[0].Marker!;
            marker.Check.Opacity.ShouldBe(0);
            form.CurrentIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Frames(clock, window, 2);
            // The pop has started; the stroke waits about 38 ms.
            marker.Check.Opacity.ShouldBeInRange(0.05, 0.9);
            marker.Check.Progress.ShouldBe(0, 0.001);
            Advance(clock, window, 90);
            marker.Check.Progress.ShouldBeInRange(0.2, 0.99);
            marker.Digit.Opacity.ShouldBe(0, 0.001);
            Advance(clock, window, 400);
            marker.Check.Progress.ShouldBe(1, 0.001);
            marker.Check.Opacity.ShouldBe(1, 0.01);

            // Undone: the check leaves in 120 ms and the number pops back out of a blur.
            form.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Advance(clock, window, 34);
            marker.Digit.Effect.ShouldNotBeNull();
            Advance(clock, window, 100);
            marker.Check.Opacity.ShouldBe(0, 0.001);
            Advance(clock, window, 600);
            marker.Digit.Opacity.ShouldBe(1, 0.01);
            marker.Digit.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_count_rolls_up_going_forward_and_down_going_back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var digit = TestHelpers.Descendant<MultiStepSwap>(form, MultiStepForm.CountDigitPart);
            form.CurrentIndex = 1;
            Dispatcher.UIThread.RunJobs();
            digit.ExitingCount.ShouldBe(1);
            var translate = (TranslateTransform)digit.CurrentBlock!.RenderTransform!;
            // 0.3em of a 12 px count.
            translate.Y.ShouldBe(3.6, 0.01);
            digit.CurrentBlock.Opacity.ShouldBe(0);
            Advance(clock, window, 300);
            translate.Y.ShouldBe(0, 0.01);
            digit.ExitingCount.ShouldBe(0);

            form.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            ((TranslateTransform)digit.CurrentBlock!.RenderTransform!).Y.ShouldBe(-3.6, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_submit_label_swaps_upward_while_its_width_springs()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        form.CompleteLabel = "Create workspace";
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var next = MultiStepFormTests.NextButton(form);
            var slot = TestHelpers.Descendant<SpringSizeDecorator>(next, "PART_LabelSlot");
            var label = TestHelpers.Descendant<MultiStepSwap>(next, "PART_Label");
            var before = slot.Bounds.Width;
            form.CurrentIndex = 1;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            ((TranslateTransform)label.CurrentBlock!.RenderTransform!).Y.ShouldBe(4.2, 0.01);
            Advance(clock, window, 100);
            slot.IsMoving.ShouldBeTrue();
            Advance(clock, window, 900);
            slot.IsMoving.ShouldBeFalse();
            slot.Bounds.Width.ShouldBeGreaterThan(before);

            // Back: the label still rises from below (the label's swap does not follow the direction).
            form.CurrentIndex = 0;
            Dispatcher.UIThread.RunJobs();
            ((TranslateTransform)label.CurrentBlock!.RenderTransform!).Y.ShouldBe(4.2, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Finishing_reveals_the_success_block_in_order_and_folds_the_buttons()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        form.CurrentIndex = 1;
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var actions = TestHelpers.Descendant<MultiStepCollapse>(form, MultiStepForm.ActionsPart);
            form.IsComplete = true;
            Dispatcher.UIThread.RunJobs();
            var success = form.SuccessView!;
            success.LayerMotion.Y.ShouldBe(8, 0.01);
            success.LayerMotion.Opacity.ShouldBe(0, 0.01);
            var title = TestHelpers.Descendant<TextBlock>(success, MultiStepSuccessView.TitlePart);
            var line = TestHelpers.Descendant<TextBlock>(success, MultiStepSuccessView.DescriptionPart);
            Frames(clock, window, 4);
            success.Glyph!.Progress.ShouldBe(0, 0.001);
            title.Opacity.ShouldBe(0, 0.001);
            actions.HeightFactor.ShouldBeLessThan(1);
            Advance(clock, window, 50);
            success.Glyph.Progress.ShouldBeGreaterThan(0);
            title.Opacity.ShouldBeGreaterThan(0);
            line.Opacity.ShouldBe(0, 0.001);
            title.Effect.ShouldNotBeNull();
            Advance(clock, window, 50);
            line.Opacity.ShouldBeGreaterThan(0);
            line.Effect.ShouldBeNull();
            Advance(clock, window, 900);
            success.LayerMotion.Y.ShouldBe(0, 0.01);
            success.Glyph.Progress.ShouldBe(1, 0.001);
            title.Opacity.ShouldBe(1, 0.001);
            line.Opacity.ShouldBe(1, 0.001);
            actions.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_scales_to_098_and_springs_back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            var next = MultiStepFormTests.NextButton(form);
            var center = next.TranslatePoint(new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Advance(clock, window, 120);
            next.Scale.ShouldBe(0.98, 0.001);
            window.MouseUp(new Point(2, 2), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Advance(clock, window, 50);
            next.Scale.ShouldBeInRange(0.981, 0.999);
            Advance(clock, window, 800);
            next.Scale.ShouldBe(1, 0.0001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_does_not_scale_presses()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Reduced);
        try
        {
            var next = MultiStepFormTests.NextButton(form);
            var center = next.TranslatePoint(new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Advance(clock, window, 120);
            next.Scale.ShouldBe(1);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Restart_fades_the_success_block_and_brings_the_buttons_back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = MultiStepFormTests.TwoSteps(out _, out _);
        form.CurrentIndex = 1;
        var window = MultiStepFormTests.Show(form, level: ArcMotionLevel.Full);
        try
        {
            form.IsComplete = true;
            Dispatcher.UIThread.RunJobs();
            Advance(clock, window, 1000);
            var actions = TestHelpers.Descendant<MultiStepCollapse>(form, MultiStepForm.ActionsPart);
            actions.IsVisible.ShouldBeFalse();
            form.Restart();
            Dispatcher.UIThread.RunJobs();
            actions.IsVisible.ShouldBeTrue();
            var first = form.ViewOf(form.Steps[0])!;
            first.LayerMotion.X.ShouldBe(-20, 0.01);
            form.SuccessView!.LayerMotion.IsLeaving.ShouldBeTrue();
            Advance(clock, window, 100);
            actions.HeightFactor.ShouldBeInRange(0.2, 0.95);
            Advance(clock, window, 900);
            actions.HeightFactor.ShouldBe(1, 0.001);
            form.SuccessView.IsVisible.ShouldBeFalse();
            first.LayerMotion.X.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
