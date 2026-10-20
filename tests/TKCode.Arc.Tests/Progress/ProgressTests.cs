using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Progress;

public class ProgressTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(1380, 2400, 57)] // 57.4999… in floating point, as in Arc
    [InlineData(150, 100, 100)]
    [InlineData(-5, 100, 0)]
    [InlineData(5, 0, 5)]
    public void Percentage_Follows_Arc(double value, double max, int expected) =>
        TKCode.Arc.Progress.ComputePercentage(value, max).ShouldBe(expected);

    [AvaloniaFact]
    public void Fill_And_Count_Share_One_Value()
    {
        var progress = new TKCode.Arc.Progress { Label = "Uploading", IsValueVisible = true, Value = 30, Width = 300 };
        var window = TestHelpers.Show(progress);
        try
        {
            var track = TestHelpers.Descendant<ProgressTrack>(progress);
            var readout = TestHelpers.Descendant<ProgressReadout>(progress);
            track.Bounds.Height.ShouldBe(7);
            track.Fraction.ShouldBe(0.3, 1e-9);
            readout.DisplayedCount.ShouldBe(30);
            readout.CheckOpacity.ShouldBe(0);
            AutomationProperties.GetName(progress).ShouldBe("Uploading");
            AutomationProperties.GetItemStatus(progress).ShouldBe("30%");

            progress.Value = 100;
            Dispatcher.UIThread.RunJobs();
            progress.IsComplete.ShouldBeTrue();
            progress.Classes.ShouldContain(TKCode.Arc.Progress.CompletePseudoClass);
            track.Fraction.ShouldBe(1, 1e-9);
            track.Doneness.ShouldBe(1);
            readout.DisplayedCount.ShouldBe(100);
            readout.CheckOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unlabelled_Bar_Is_Named_Progress_And_Hides_The_Meta_Row()
    {
        var progress = new TKCode.Arc.Progress { Value = 10, Width = 200, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(progress);
        try
        {
            AutomationProperties.GetName(progress).ShouldBe("Progress");
            progress.HasMeta.ShouldBeFalse();
            progress.Bounds.Height.ShouldBe(7);
            progress.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fill_Springs_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var progress = new TKCode.Arc.Progress { Value = 0, Width = 200 };
        var window = TestHelpers.Show(progress, ArcMotionLevel.Full);
        try
        {
            progress.Value = 80;
            clock.Advance(TimeSpan.FromMilliseconds(100));
            progress.ShownPercentage.ShouldBeGreaterThan(0);
            progress.ShownPercentage.ShouldBeLessThan(80);
            clock.Advance(TimeSpan.FromSeconds(2));
            progress.ShownPercentage.ShouldBe(80, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
