using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

/// <summary>The motion of <see cref="SignupForm"/>, frame by frame (values measured on Arc's live preview, 2026-10-24).</summary>
public class SignupFormMotionTests
{
    private static Window Show(Control control, ArcMotionLevel level, double width = 600)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        // Tall enough for the form and a wrapped footer in the wide headless font.
        window.Height = 1200;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static SignupForm Quick() => new()
    {
        SubmitMinimumDuration = TimeSpan.Zero,
        ProviderSimulationDuration = TimeSpan.Zero
    };

    private static void Fill(SignupForm form)
    {
        form.NameInput!.Text = "Jasmine Brooks";
        form.EmailInput!.Text = "jasmine@northwind.studio";
        form.PasswordInput!.Text = "Northwind26";
        Dispatcher.UIThread.RunJobs();
    }

    private static void Advance(ManualFrameClock clock, Window window, double ms)
    {
        clock.Advance(TimeSpan.FromMilliseconds(ms));
        // A finished animation's await resumes through the thread pool onto the dispatcher: give it a few turns.
        for (var i = 0; i < 6; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Thread.Sleep(2);
        }
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Steps_Cross_Over_Sideways_While_The_Height_Glides()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = Quick();
        var window = Show(form, ArcMotionLevel.Full);
        try
        {
            Fill(form);
            Advance(clock, window, 400);
            var formStep = TestHelpers.Descendant<Border>(form, "PART_FormStep");
            var doneStep = TestHelpers.Descendant<Border>(form, "PART_DoneStep");
            var stage = form.Stage!;
            var formHeight = stage.Bounds.Height;
            formHeight.ShouldBeGreaterThan(500);

            form.Submit();
            Dispatcher.UIThread.RunJobs();
            form.Step.ShouldBe(SignupFormStep.Done);
            stage.IsGliding.ShouldBeTrue();
            doneStep.IsVisible.ShouldBeTrue();
            formStep.IsVisible.ShouldBeTrue();
            formStep.IsHitTestVisible.ShouldBeFalse();
            ((TranslateTransform)doneStep.RenderTransform!).X.ShouldBe(24, 0.01);
            doneStep.Opacity.ShouldBe(0, 0.001);

            Advance(clock, window, 100);
            // Critically damped, visualDuration .4: Arc's new step was at x 15.6 after 100 ms.
            ((TranslateTransform)doneStep.RenderTransform!).X.ShouldBeInRange(13, 18.5);
            // Opacity waits 50 ms, then 240 ms on the enter curve.
            doneStep.Opacity.ShouldBeInRange(0.3, 0.95);
            // The old step fades over 180 ms (standard) and drifts toward −16.
            formStep.Opacity.ShouldBeLessThan(0.15);
            ((TranslateTransform)formStep.RenderTransform!).X.ShouldBeInRange(-9, -3);
            stage.Bounds.Height.ShouldBeLessThan(formHeight);

            Advance(clock, window, 900);
            formStep.IsVisible.ShouldBeFalse();
            ((TranslateTransform)doneStep.RenderTransform!).X.ShouldBe(0, 0.05);
            doneStep.Opacity.ShouldBe(1, 0.001);
            stage.IsGliding.ShouldBeFalse();
            stage.Bounds.Height.ShouldBe(doneStep.DesiredSize.Height, 0.6);
            stage.Bounds.Height.ShouldBeLessThan(formHeight - 150);

            // Start over goes back the other way: the form comes from the left.
            form.Reset();
            Dispatcher.UIThread.RunJobs();
            ((TranslateTransform)formStep.RenderTransform!).X.ShouldBe(-24, 0.01);
            Advance(clock, window, 100);
            ((TranslateTransform)doneStep.RenderTransform!).X.ShouldBeGreaterThan(2);
            Advance(clock, window, 900);
            doneStep.IsVisible.ShouldBeFalse();
            stage.IsGliding.ShouldBeFalse();
            stage.Bounds.Height.ShouldBe(formStep.DesiredSize.Height, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Completion_Mark_Draws_Ring_Then_Check()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = Quick();
        var window = Show(form, ArcMotionLevel.Full);
        try
        {
            Fill(form);
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            var mark = form.Mark!;
            mark.RingOpacity.ShouldBe(0, 1e-6);
            mark.RingProgress.ShouldBe(0, 1e-6);
            mark.CheckOpacity.ShouldBe(0, 1e-6);
            Advance(clock, window, 130);
            mark.RingOpacity.ShouldBe(0, 1e-6);
            // 400 ms: 240 of the ring's 480 on the in-out curve (half drawn); the check waits until 500.
            Advance(clock, window, 270);
            mark.RingOpacity.ShouldBe(1, 1e-6);
            mark.RingProgress.ShouldBeInRange(0.4, 0.6);
            mark.CheckProgress.ShouldBe(0, 1e-6);
            Advance(clock, window, 250);
            // 150 of 320 ms on the front-loaded enter curve.
            mark.CheckProgress.ShouldBeInRange(0.9, 0.999);
            mark.CheckOpacity.ShouldBeGreaterThan(0.5);
            Advance(clock, window, 400);
            mark.RingProgress.ShouldBe(1, 1e-6);
            mark.CheckProgress.ShouldBe(1, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Password_Line_Lifts_The_Old_One_Away()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = Quick();
        var window = Show(form, ArcMotionLevel.Full);
        try
        {
            Advance(clock, window, 300);
            var line = form.FeedbackLine!;
            form.Password = "a";
            Dispatcher.UIThread.RunJobs();
            line.ExitingCount.ShouldBe(1);
            line.CurrentBlock!.Opacity.ShouldBe(0, 1e-6);
            ((TranslateTransform)line.CurrentBlock.RenderTransform!).Y.ShouldBe(6, 1e-6);
            Advance(clock, window, 100);
            line.CurrentBlock.Opacity.ShouldBeInRange(0.6, 0.98);
            Advance(clock, window, 100);
            TestHelpers.ShouldDrain(() => line.ExitingCount);
            Advance(clock, window, 100);
            line.CurrentBlock.Opacity.ShouldBe(1, 1e-6);
            ((TranslateTransform)line.CurrentBlock.RenderTransform!).Y.ShouldBe(0, 1e-6);
            // Same key (still score 0): the words change in place.
            form.Password = "ab";
            Dispatcher.UIThread.RunJobs();
            line.ExitingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Submit_Error_Row_Opens_And_Closes_On_The_Smooth_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = Quick();
        var window = Show(form, ArcMotionLevel.Full);
        try
        {
            Advance(clock, window, 300);
            var row = form.ErrorRow!;
            form.ServerError = "That email is already registered.";
            Dispatcher.UIThread.RunJobs();
            row.IsPresent.ShouldBeTrue();
            Advance(clock, window, 50);
            row.Bounds.Height.ShouldBeInRange(0.5, 12);
            Advance(clock, window, 800);
            // At rest the row is as tall as its text (the headless font wraps it to more lines than Inter would).
            row.Bounds.Height.ShouldBeGreaterThanOrEqualTo(16.8);
            (row.Bounds.Height % 16.8).ShouldBeLessThan(0.6);
            form.ServerError = null;
            Dispatcher.UIThread.RunJobs();
            row.IsPresent.ShouldBeTrue();
            Advance(clock, window, 900);
            row.IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_The_New_Step_And_Draws_The_Mark_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var form = Quick();
        var window = Show(form, ArcMotionLevel.Reduced);
        try
        {
            Fill(form);
            form.Submit();
            Dispatcher.UIThread.RunJobs();
            var formStep = TestHelpers.Descendant<Border>(form, "PART_FormStep");
            var doneStep = TestHelpers.Descendant<Border>(form, "PART_DoneStep");
            formStep.IsVisible.ShouldBeFalse();
            form.Stage!.IsGliding.ShouldBeFalse();
            ((TranslateTransform)doneStep.RenderTransform!).X.ShouldBe(0, 1e-6);
            doneStep.Opacity.ShouldBe(0, 1e-6);
            doneStep.Effect.ShouldBeNull();
            form.Mark!.RingProgress.ShouldBe(1, 1e-6);
            form.Mark.CheckProgress.ShouldBe(1, 1e-6);
            Advance(clock, window, 60);
            doneStep.Opacity.ShouldBeInRange(0.2, 0.95);
            Advance(clock, window, 100);
            doneStep.Opacity.ShouldBe(1, 1e-6);

            // The status line fades without moving and the old line goes at once.
            form.OpenLink(SignupFormLink.Terms);
            form.StatusLine!.ExitingCount.ShouldBe(0);
            ((TranslateTransform)form.StatusLine.CurrentBlock!.RenderTransform!).Y.ShouldBe(0, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }
}
