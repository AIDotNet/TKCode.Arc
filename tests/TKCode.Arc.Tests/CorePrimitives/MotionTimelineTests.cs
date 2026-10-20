using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>P22 <see cref="MotionTimeline"/>: timed runs, pausing, completion, springs back and frame reports.</summary>
public class MotionTimelineTests
{
    /// <summary>Runs the dispatcher (jobs and timers) until <paramref name="condition"/> holds.</summary>
    private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return true;
            }
            Pump(10);
        }
        Dispatcher.UIThread.RunJobs();
        return condition();
    }

    /// <summary>Dispatcher timers only fire inside a running frame, so this pushes one.</summary>
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }

    [AvaloniaFact]
    public void A_Run_Reaches_One_On_Time_And_Completes_Once()
    {
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner);
        try
        {
            var timeline = new MotionTimeline(owner);
            var completed = 0;
            var values = new List<double>();
            timeline.Completed += () => completed++;
            timeline.Changed += values.Add;
            timeline.Start(TimeSpan.FromMilliseconds(120), from: 0.5);
            timeline.IsActive.ShouldBeTrue();
            timeline.IsRunning.ShouldBeTrue();
            timeline.Value.ShouldBe(0.5, 0.05);
            values[0].ShouldBe(0.5, 0.05);
            timeline.Remaining.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(60));
            WaitFor(() => completed > 0).ShouldBeTrue();
            Pump(50);
            completed.ShouldBe(1);
            timeline.IsActive.ShouldBeFalse();
            timeline.Value.ShouldBe(1);
            values[^1].ShouldBe(1);
            values.ShouldBeInOrder();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_Holds_The_Value_And_Resume_Finishes_The_Rest()
    {
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner);
        try
        {
            var timeline = new MotionTimeline(owner);
            var completed = 0;
            timeline.Completed += () => completed++;
            timeline.Start(TimeSpan.FromMilliseconds(1500));
            Pump(100);
            timeline.Pause();
            timeline.IsActive.ShouldBeTrue();
            timeline.IsRunning.ShouldBeFalse();
            var held = timeline.Value;
            held.ShouldBeGreaterThan(0.02);
            held.ShouldBeLessThan(0.9);
            var remaining = timeline.Remaining;
            Pump(150);
            timeline.Value.ShouldBe(held);
            timeline.Remaining.ShouldBe(remaining);
            completed.ShouldBe(0);
            timeline.Resume();
            timeline.IsRunning.ShouldBeTrue();
            WaitFor(() => completed > 0).ShouldBeTrue();
            timeline.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Keeps_The_Value_Reached_And_Never_Completes()
    {
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner);
        try
        {
            var timeline = new MotionTimeline(owner);
            var completed = 0;
            timeline.Completed += () => completed++;
            timeline.Start(TimeSpan.FromMilliseconds(1500));
            Pump(60);
            timeline.Stop();
            var reached = timeline.Value;
            reached.ShouldBeGreaterThan(0);
            reached.ShouldBeLessThan(1);
            timeline.IsActive.ShouldBeFalse();
            Pump(300);
            completed.ShouldBe(0);
            timeline.Value.ShouldBe(reached);
            // A new run can carry on from there (a quick re-press of a hold).
            timeline.Start(TimeSpan.FromMilliseconds(200), timeline.Value);
            timeline.Value.ShouldBe(reached, 0.05);
            timeline.Jump(0.25);
            timeline.IsActive.ShouldBeFalse();
            timeline.Value.ShouldBe(0.25);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Run_With_Nothing_Left_Completes_At_Once()
    {
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner);
        try
        {
            var timeline = new MotionTimeline(owner);
            var completed = 0;
            timeline.Completed += () => completed++;
            timeline.Start(TimeSpan.FromSeconds(1), from: 1);
            completed.ShouldBe(1);
            timeline.IsActive.ShouldBeFalse();
            timeline.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Frames_Report_The_Run_At_Every_Motion_Level()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner, ArcMotionLevel.Off);
        try
        {
            var timeline = new MotionTimeline(owner);
            var reports = 0;
            timeline.Changed += _ => reports++;
            timeline.Start(TimeSpan.FromSeconds(2));
            reports = 0;
            Thread.Sleep(40);
            clock.Advance(TimeSpan.FromMilliseconds(20));
            reports.ShouldBeGreaterThan(0);
            timeline.Value.ShouldBeGreaterThan(0);
            // Paused runs request no frames.
            timeline.Pause();
            reports = 0;
            Thread.Sleep(20);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            reports.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rewind_Springs_Back_From_Rest_Under_Full_Motion_And_Lands_At_Once_Otherwise()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var owner = new Border { Width = 40, Height = 20 };
        var window = TestHelpers.Show(owner, ArcMotionLevel.Full);
        try
        {
            var timeline = new MotionTimeline(owner);
            timeline.Jump(0.6);
            var spring = SpringSpec.FromVisual(0.3, 0);
            timeline.Start(TimeSpan.FromSeconds(10), timeline.Value);
            timeline.Rewind(spring);
            timeline.IsActive.ShouldBeFalse();
            var start = timeline.Value;
            var t = CoreFrames.Run(clock, 6);
            var (x, _) = SpringSolver.Evaluate(spring, start, 0, t);
            timeline.Value.ShouldBe(x, 0.01);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            timeline.Value.ShouldBe(0);

            timeline.SettleTo(1, spring);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            timeline.Value.ShouldBe(1);

            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            timeline.Rewind();
            timeline.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
