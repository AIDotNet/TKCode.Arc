using TKCode.Arc.Gestures;
using Avalonia;

namespace TKCode.Arc.Core.Tests.Gestures;

public class VelocityTrackerTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Constant_Motion_Reports_Its_Speed()
    {
        var tracker = new VelocityTracker();
        for (var i = 0; i <= 10; i++)
        {
            // 600 px/s right, 300 px/s up, sampled every 8ms.
            tracker.AddSample(new Point(i * 4.8, -i * 2.4), Ms(i * 8));
        }
        var v = tracker.GetVelocity(Ms(80));
        v.X.ShouldBe(600, 0.5);
        v.Y.ShouldBe(-300, 0.5);
    }

    [Fact]
    public void Only_Samples_Inside_The_Window_Count()
    {
        var tracker = new VelocityTracker(Ms(50));
        // Slow drift for a long time, then a fast flick in the last 48ms.
        for (var i = 0; i < 20; i++)
        {
            tracker.AddSample(i * 0.1, Ms(i * 10));
        }
        var start = 1.9;
        for (var i = 1; i <= 6; i++)
        {
            tracker.AddSample(start + i * 8, Ms(190 + i * 8));
        }
        tracker.GetVelocity(Ms(238)).X.ShouldBe(1000, 60);
    }

    [Fact]
    public void A_Pause_Before_Release_Drops_The_Momentum()
    {
        var tracker = new VelocityTracker(staleAfter: Ms(60));
        tracker.AddSample(0, Ms(0));
        tracker.AddSample(10, Ms(10));
        tracker.AddSample(20, Ms(20));
        tracker.GetVelocity(Ms(70)).X.ShouldBe(1000, 1);
        tracker.GetVelocity(Ms(120)).ShouldBe(default);
    }

    [Fact]
    public void Fewer_Than_Two_Samples_Is_Zero_And_Same_Time_Replaces()
    {
        var tracker = new VelocityTracker();
        tracker.Velocity.ShouldBe(default);
        tracker.AddSample(5, Ms(10));
        tracker.Velocity.ShouldBe(default);
        tracker.AddSample(9, Ms(10));
        tracker.Count.ShouldBe(1);
        tracker.Velocity.ShouldBe(default);
    }

    [Fact]
    public void Ring_Buffer_Keeps_Working_Past_Its_Capacity()
    {
        var tracker = new VelocityTracker();
        for (var i = 0; i < 500; i++)
        {
            tracker.AddSample(i * 2.0, Ms(i * 4));
        }
        tracker.Velocity.X.ShouldBe(500, 0.5);
    }

    [Fact]
    public void Time_Going_Backwards_Restarts()
    {
        var tracker = new VelocityTracker();
        tracker.AddSample(0, Ms(100));
        tracker.AddSample(50, Ms(110));
        tracker.AddSample(0, Ms(5));
        tracker.Count.ShouldBe(1);
    }
}
