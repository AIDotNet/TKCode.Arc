using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="ArcRing"/>: shows its value at once or chases it at full motion only.</summary>
public class ArcRingTests
{
    [AvaloniaFact]
    public void Defaults_And_Clamping()
    {
        var ring = new ArcRing();
        ring.Width.ShouldBe(16);
        ring.Height.ShouldBe(16);
        ring.Thickness.ShouldBe(2);
        ring.StartAngle.ShouldBe(-90);
        ring.IsRoundCap.ShouldBeTrue();
        ring.ChaseRate.ShouldBe(0);
        ring.IsHitTestVisible.ShouldBeFalse();
        ring.Value = 1.4;
        ring.Value.ShouldBe(1);
        ring.Value = -0.2;
        ring.Value.ShouldBe(0);
        ring.Value = double.NaN;
        ring.Value.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Without_A_Chase_Rate_The_Value_Shows_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var ring = new ArcRing { Foreground = Brushes.Black, TrackBrush = Brushes.Gray };
        var window = TestHelpers.Show(new StackPanel { Children = { ring } }, ArcMotionLevel.Full);
        try
        {
            ring.Value = 0.4;
            ring.DisplayedValue.ShouldBe(0.4);
            ring.IsChasing.ShouldBeFalse();
            ring.Value = 1;
            ring.DisplayedValue.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chase_Trails_The_Value_And_Settles_On_It()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var ring = new ArcRing { Foreground = Brushes.Black, ChaseRate = 0.15 };
        var window = TestHelpers.Show(new StackPanel { Children = { ring } }, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            ring.Value = 0.8;
            ring.IsChasing.ShouldBeTrue();
            ring.DisplayedValue.ShouldBe(0);
            CoreFrames.Run(clock, 10);
            // Each 1/60s closes 15% of the gap.
            ring.DisplayedValue.ShouldBeGreaterThan(0.8 * (1 - Math.Pow(0.85, 9)) - 0.01);
            ring.DisplayedValue.ShouldBeLessThan(0.8 * (1 - Math.Pow(0.85, 11)) + 0.01);
            CoreFrames.Run(clock, 120);
            ring.DisplayedValue.ShouldBe(0.8);
            ring.IsChasing.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_The_Value_At_Once_Even_With_A_Chase_Rate()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var ring = new ArcRing { Foreground = Brushes.Black, ChaseRate = 0.15 };
        var window = TestHelpers.Show(new StackPanel { Children = { ring } }, ArcMotionLevel.Reduced);
        try
        {
            ring.Value = 0.6;
            ring.DisplayedValue.ShouldBe(0.6);

            // Switching to reduced motion mid-chase lands on the value.
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            Dispatcher.UIThread.RunJobs();
            ring.Value = 0.2;
            CoreFrames.Run(clock, 2);
            ring.DisplayedValue.ShouldBeGreaterThan(0.2);
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Dispatcher.UIThread.RunJobs();
            ring.DisplayedValue.ShouldBe(0.2);
        }
        finally
        {
            window.Close();
        }
    }
}
