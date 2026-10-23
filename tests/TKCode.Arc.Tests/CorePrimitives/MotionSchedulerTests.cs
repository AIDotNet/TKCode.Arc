using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>The motion scheduler: one step and one report per frame, and retargeting that changes nothing is free.</summary>
public class MotionSchedulerTests
{
    [AvaloniaFact]
    public void A_Long_Frame_Is_One_Step_And_One_Changed()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner, ArcMotionLevel.Full);
        try
        {
            var value = new MotionValue<double>(owner, 0, MotionVectors.Double);
            var changes = 0;
            value.Changed += _ => changes++;
            _ = value.AnimateTo(100, MotionSpec.Spring(ArcSpring.Smooth));
            // Frames 100ms apart (a slow device): each used to be split into six 1/60s steps that each raised Changed.
            var frames = 0;
            for (var i = 0; i < 4; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
                frames++;
            }
            changes.ShouldBeLessThanOrEqualTo(frames);
            value.Value.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Retargeting_To_The_Same_Target_And_Spec_Keeps_The_Run_Going()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner, ArcMotionLevel.Full);
        try
        {
            var value = new MotionValue<double>(owner, 0, MotionVectors.Double);
            var first = value.AnimateTo(100, MotionSpec.Spring(ArcSpring.Snappy));
            CoreFrames.Run(clock, 5);
            var before = value.Value;
            var second = value.AnimateTo(100, MotionSpec.Spring(ArcSpring.Snappy));
            ReferenceEquals(first, second).ShouldBeTrue();
            value.Value.ShouldBe(before);
            first.IsCompleted.ShouldBeFalse();

            // A different target still retargets and ends the first run.
            _ = value.AnimateTo(50, MotionSpec.Spring(ArcSpring.Snappy));
            first.Result.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Hidden_Spinner_Asks_For_No_Frames()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var spinner = new ArcSpinner { IsVisible = false };
        var window = TestHelpers.Show(new StackPanel { Children = { spinner } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeFalse();
            clock.RequestCount.ShouldBe(0);
            spinner.IsVisible = true;
            Dispatcher.UIThread.RunJobs();
            spinner.IsSpinning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Theme_Motion_Level_Is_Cached_And_Follows_Resource_Changes()
    {
        var child = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(child, ArcMotionLevel.Full);
        try
        {
            ArcMotion.GetEffectiveLevel(child).ShouldBe(ArcMotionLevel.Full);
            window.Resources[TKCode.Arc.DesignTokens.ArcDesignTokenKind.MotionMode] = "reduced";
            ArcMotion.GetEffectiveLevel(child).ShouldBe(ArcMotionLevel.Reduced);
            window.Resources[TKCode.Arc.DesignTokens.ArcDesignTokenKind.MotionMode] = "off";
            ArcMotion.GetEffectiveLevel(child).ShouldBe(ArcMotionLevel.Off);
            window.Resources.Remove(TKCode.Arc.DesignTokens.ArcDesignTokenKind.MotionMode);
            ArcMotion.GetEffectiveLevel(child).ShouldBe(ArcMotionLevel.Full);
        }
        finally
        {
            window.Close();
        }
    }
}
