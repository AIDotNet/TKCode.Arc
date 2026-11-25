using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Progress;

public class ProgressRingTests
{
    [Theory]
    [InlineData(0, 0, 100, 0)]
    [InlineData(50, 0, 100, 50)]
    [InlineData(75, 50, 100, 50)]
    [InlineData(150, 0, 100, 100)]
    [InlineData(-10, 0, 100, 0)]
    [InlineData(25, 0, 0, 25)]
    [InlineData(double.NaN, 0, 100, 0)]
    public void Percentage_Computes_And_Clamps_Within_Range(double value, double min, double max, int expected) =>
        ProgressRing.ComputePercentage(value, min, max).ShouldBe(expected);

    [AvaloniaFact]
    public void Determinate_Mode_Updates_Arc_Readout_And_Completion_State()
    {
        var ring = new ProgressRing
        {
            Label = "Uploading",
            IsValueVisible = true,
            Value = 35
        };
        var window = TestHelpers.Show(ring);
        try
        {
            var track = TestHelpers.Descendant<ProgressRingTrack>(ring);
            var readout = TestHelpers.Descendant<ProgressRingReadout>(ring);
            ring.Focusable.ShouldBeFalse();
            ring.Percentage.ShouldBe(35);
            ring.IsComplete.ShouldBeFalse();
            ring.ShowsReadout.ShouldBeTrue();
            track.Fraction.ShouldBe(0.35, 1e-9);
            track.Doneness.ShouldBe(0);
            readout.DisplayedCount.ShouldBe(35);
            readout.CheckOpacity.ShouldBe(0);
            AutomationProperties.GetName(ring).ShouldBe("Uploading");
            AutomationProperties.GetItemStatus(ring).ShouldBe("35%");

            ring.Value = 100;
            Dispatcher.UIThread.RunJobs();
            ring.IsComplete.ShouldBeTrue();
            ring.Classes.ShouldContain(ProgressRing.CompletePseudoClass);
            track.Fraction.ShouldBe(1, 1e-9);
            track.Doneness.ShouldBe(1);
            readout.DisplayedCount.ShouldBe(100);
            readout.CheckOpacity.ShouldBe(1);
            AutomationProperties.GetItemStatus(ring).ShouldBe("100%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Indeterminate_Mode_Spins_Under_Full_Pulses_Under_Reduced_And_Rests_Under_Off()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var ring = new ProgressRing
        {
            IsIndeterminate = true,
            Value = 100,
            IsValueVisible = true
        };
        var window = TestHelpers.Show(ring, ArcMotionLevel.Full);
        try
        {
            var track = TestHelpers.Descendant<ProgressRingTrack>(ring);
            ring.Classes.ShouldContain(ProgressRing.IndeterminatePseudoClass);
            ring.IsComplete.ShouldBeFalse();
            ring.ShowsReadout.ShouldBeFalse();
            AutomationProperties.GetName(ring).ShouldBe("Loading");
            AutomationProperties.GetItemStatus(ring).ShouldBe("Loading");

            track.IsSpinning.ShouldBeTrue();
            var startAngle = track.IndeterminateAngle;
            clock.Advance(TimeSpan.FromMilliseconds(200));
            track.IndeterminateAngle.ShouldNotBe(startAngle);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            track.IsPulsing.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(300));
            track.PulseOpacity.ShouldBeLessThan(1.0);

            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            Dispatcher.UIThread.RunJobs();
            track.IsSpinning.ShouldBeFalse();
            track.IsPulsing.ShouldBeFalse();
            track.PulseOpacity.ShouldBe(1.0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Apply_Expected_Diameters_And_Stroke_Thickness()
    {
        var small = new ProgressRing { Size = ControlSize.Small, Value = 40 };
        var medium = new ProgressRing { Size = ControlSize.Medium, Value = 40 };
        var large = new ProgressRing { Size = ControlSize.Large, Value = 40 };
        var custom = new ProgressRing { Width = 96, Height = 96, Thickness = 6, Value = 40 };
        var window = TestHelpers.Show(new StackPanel { Children = { small, medium, large, custom } });
        try
        {
            small.Classes.ShouldContain(ProgressRing.SmallPseudoClass);
            small.Bounds.Width.ShouldBe(20);
            small.Bounds.Height.ShouldBe(20);
            small.Thickness.ShouldBe(2);

            medium.Bounds.Width.ShouldBe(40);
            medium.Bounds.Height.ShouldBe(40);
            medium.Thickness.ShouldBe(3);

            large.Classes.ShouldContain(ProgressRing.LargePseudoClass);
            large.Bounds.Width.ShouldBe(64);
            large.Bounds.Height.ShouldBe(64);
            large.Thickness.ShouldBe(4);

            custom.Bounds.Width.ShouldBe(96);
            custom.Bounds.Height.ShouldBe(96);
            custom.Thickness.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Content_Takes_Precedence_Over_BuiltIn_Readout()
    {
        var ring = new ProgressRing
        {
            Value = 60,
            IsValueVisible = true,
            Content = new TextBlock { Text = "3/5" }
        };
        var window = TestHelpers.Show(ring);
        try
        {
            ring.HasCustomContent.ShouldBeTrue();
            ring.ShowsReadout.ShouldBeFalse();
            AutomationProperties.GetName(ring).ShouldBe("Progress");

            ring.Content = null;
            Dispatcher.UIThread.RunJobs();
            ring.HasCustomContent.ShouldBeFalse();
            ring.ShowsReadout.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Determinate_Fill_Springs_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var ring = new ProgressRing { Value = 0 };
        var window = TestHelpers.Show(ring, ArcMotionLevel.Full);
        try
        {
            ring.Value = 80;
            clock.Advance(TimeSpan.FromMilliseconds(100));
            ring.ShownPercentage.ShouldBeGreaterThan(0);
            ring.ShownPercentage.ShouldBeLessThan(80);
            clock.Advance(TimeSpan.FromSeconds(2));
            ring.ShownPercentage.ShouldBe(80, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
