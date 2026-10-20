using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="ArcSpinner"/>: the loading ring turns (0.7s a turn) only at full motion.</summary>
public class ArcSpinnerTests
{
    [AvaloniaFact]
    public void Turns_At_Full_Motion_And_Rests_Under_Reduced_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var spinner = new ArcSpinner { Foreground = Brushes.Black };
        var window = TestHelpers.Show(new StackPanel { Children = { spinner } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeTrue();
            CoreFrames.Run(clock, 21); // 0.35s: half a turn
            spinner.Angle.ShouldBe(180, 8);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeFalse();
            var rest = spinner.Angle;
            CoreFrames.Run(clock, 30);
            spinner.Angle.ShouldBe(rest);

            // Back at full motion it carries on from where it stopped.
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeTrue();
            CoreFrames.Run(clock, 6);
            spinner.Angle.ShouldBeGreaterThan(rest);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Spinners_Request_No_Frames()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var spinner = new ArcSpinner { Foreground = Brushes.Black, IsVisible = false };
        var window = TestHelpers.Show(new StackPanel { Children = { spinner } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeFalse();
            spinner.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
