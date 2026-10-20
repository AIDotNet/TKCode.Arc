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
}
