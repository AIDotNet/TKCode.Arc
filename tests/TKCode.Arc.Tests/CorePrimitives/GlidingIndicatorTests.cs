using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="GlidingIndicator"/>: moves on a preset or on a component's own spring.</summary>
public class GlidingIndicatorTests
{
    [AvaloniaFact]
    public void MoveTo_With_A_SpringSpec_Glides_On_That_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var custom = new GlidingIndicator { Width = 300, Height = 40 };
        var preset = new GlidingIndicator { Width = 300, Height = 40, Spring = ArcSpring.Morph };
        var window = TestHelpers.Show(new StackPanel { Children = { custom, preset } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var from = new Rect(0, 0, 60, 20);
            var to = new Rect(120, 0, 80, 20);
            custom.MoveTo(from, animate: false);
            preset.MoveTo(from, animate: false);
            var spring = SpringSpec.FromVisual(0.6, 0);
            custom.MoveTo(to, spring);
            preset.MoveTo(to);
            var t = CoreFrames.Run(clock, 6);

            var (dx, _) = SpringSolver.Evaluate(spring, from.X - to.X, 0, t);
            custom.CurrentRect.X.ShouldBe(to.X + dx, 0.01);
            var (morph, _) = SpringSolver.Evaluate(ArcMotionPresets.Morph, from.X - to.X, 0, t);
            preset.CurrentRect.X.ShouldBe(to.X + morph, 0.01);
            Math.Abs(custom.CurrentRect.X - preset.CurrentRect.X).ShouldBeGreaterThan(1);

            // Without animation it snaps, like the preset overload.
            custom.MoveTo(from, spring, animate: false);
            custom.CurrentRect.ShouldBe(from);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FadeDuration_Sets_How_Fast_It_Appears_In_Place_And_Fades_Out()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var indicator = new GlidingIndicator { Width = 300, Height = 40, FadeDuration = TimeSpan.FromMilliseconds(80) };
        var window = TestHelpers.Show(new StackPanel { Children = { indicator } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var row = new Rect(0, 0, 120, 36);
            indicator.MoveTo(row);
            indicator.CurrentRect.ShouldBe(row); // lands in place, no glide from elsewhere
            indicator.CurrentOpacity.ShouldBe(0, 0.001);
            CoreFrames.Run(clock, 3); // 50ms
            indicator.CurrentOpacity.ShouldBeGreaterThan(0.5);
            indicator.CurrentOpacity.ShouldBeLessThan(1);
            CoreFrames.Run(clock, 3); // 100ms > 80ms
            indicator.CurrentOpacity.ShouldBe(1, 0.001);

            indicator.Hide();
            CoreFrames.Run(clock, 6);
            indicator.CurrentOpacity.ShouldBe(0, 0.001);

            // After a hide the next move lands where it is asked to, fading in again.
            var next = new Rect(0, 40, 120, 36);
            indicator.MoveTo(next);
            indicator.CurrentRect.ShouldBe(next);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MoveTo_With_A_Duration_Tweens_Instead_Of_Springing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var indicator = new GlidingIndicator { Width = 300, Height = 40 };
        var window = TestHelpers.Show(new StackPanel { Children = { indicator } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var from = new Rect(0, 0, 100, 32);
            var to = new Rect(110, 0, 100, 32);
            indicator.MoveTo(from, animate: false);
            indicator.MoveTo(to, TimeSpan.FromMilliseconds(220), new Avalonia.Animation.Easings.LinearEasing());
            var t = CoreFrames.Run(clock, 6);
            indicator.CurrentRect.X.ShouldBe(110 * t / 0.22, 0.5);
            CoreFrames.Run(clock, 12);
            indicator.CurrentRect.ShouldBe(to);
        }
        finally
        {
            window.Close();
        }
    }
}
