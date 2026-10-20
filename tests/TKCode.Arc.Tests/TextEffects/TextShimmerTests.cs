using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.TextEffects;

public class TextShimmerTests
{
    [Fact]
    public void Shimmer_Band_Starts_Left_And_Ends_Right_Of_The_Text()
    {
        TextShimmer.BandCenter(0, 100, 10).ShouldBe(-22);
        TextShimmer.BandCenter(0.5, 100, 10).ShouldBe(50);
        TextShimmer.BandCenter(1, 100, 10).ShouldBe(122);
    }

    [AvaloniaFact]
    public void Shimmer_Sweeps_Only_While_Active_And_Visible()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var shimmer = new TextShimmer { Text = "Generating summary" };
        var window = TestHelpers.Show(shimmer, ArcMotionLevel.Full);
        try
        {
            AutomationProperties.GetItemStatus(shimmer).ShouldBe("Busy");
            shimmer.IsSweeping.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(0.12 + 0.9));
            shimmer.Sweep.ShouldBe(0.5, 0.05);

            shimmer.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            shimmer.IsSweeping.ShouldBeFalse();
            var paused = shimmer.Sweep;
            shimmer.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            shimmer.IsSweeping.ShouldBeTrue();
            shimmer.Sweep.ShouldBe(paused);

            shimmer.IsActive = false;
            Dispatcher.UIThread.RunJobs();
            shimmer.IsSweeping.ShouldBeFalse();
            clock.Advance(TimeSpan.FromSeconds(1));
            shimmer.Settle.ShouldBe(1, 0.001);
            AutomationProperties.GetItemStatus(shimmer).ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shimmer_Is_Still_Under_Reduced_Motion()
    {
        var shimmer = new TextShimmer { Text = "Thinking" };
        var window = TestHelpers.Show(shimmer, ArcMotionLevel.Reduced);
        try
        {
            shimmer.IsSweeping.ShouldBeFalse();
            shimmer.Sweep.ShouldBe(0);
            AutomationProperties.GetName(shimmer).ShouldBe("Thinking");
            shimmer.Bounds.Width.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }
}
