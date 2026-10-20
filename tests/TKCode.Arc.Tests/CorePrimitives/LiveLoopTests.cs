using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>P21 <see cref="LiveLoop"/>: gates, phase keeping, finishing and per-frame cost.</summary>
public class LiveLoopTests
{
    private static void Settle(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Runs_Only_While_Attached_And_Visible_And_Keeps_Its_Phase()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var probe = new Border { Width = 40, Height = 20 };
        var elapsed = 0.0;
        var loop = new LiveLoop(probe, dt =>
        {
            elapsed += dt;
            return true;
        });
        loop.Start();
        loop.IsActive.ShouldBeTrue();
        loop.IsRunning.ShouldBeFalse();
        var host = new Border { Child = probe };
        var window = TestHelpers.Show(new StackPanel { Children = { host } }, ArcMotionLevel.Full);
        try
        {
            loop.IsRunning.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            elapsed.ShouldBe(0.1, 0.02);

            // Hidden by an ancestor: the next frame pauses it, and a hidden loop costs no frames.
            host.IsVisible = false;
            Settle(window);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            loop.IsRunning.ShouldBeFalse();
            loop.IsActive.ShouldBeTrue();
            var paused = elapsed;
            clock.Advance(TimeSpan.FromMilliseconds(300));
            elapsed.ShouldBe(paused);

            // Shown again: it resumes on its own from where it was (no catch-up jump).
            host.IsVisible = true;
            Settle(window);
            loop.IsRunning.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            elapsed.ShouldBe(paused + 0.05, 0.02);

            // Its own visibility and detaching pause it at once.
            probe.IsVisible = false;
            loop.IsRunning.ShouldBeFalse();
            probe.IsVisible = true;
            loop.IsRunning.ShouldBeTrue();
            host.Child = null;
            loop.IsRunning.ShouldBeFalse();
            host.Child = probe;
            loop.IsRunning.ShouldBeTrue();

            loop.Stop();
            loop.IsActive.ShouldBeFalse();
            loop.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Follows_The_Motion_Level_With_An_Option_For_Reduced()
    {
        var probe = new Border { Width = 40, Height = 20 };
        var loop = new LiveLoop(probe, _ => true);
        var changes = new List<bool>();
        loop.RunningChanged += changes.Add;
        loop.Start();
        var window = TestHelpers.Show(probe, ArcMotionLevel.Full);
        try
        {
            loop.IsRunning.ShouldBeTrue();
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            loop.IsRunning.ShouldBeFalse();
            loop.MinimumLevel = ArcMotionLevel.Reduced;
            loop.IsRunning.ShouldBeTrue();
            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            loop.IsRunning.ShouldBeFalse();
            loop.MinimumLevel = ArcMotionLevel.Off;
            loop.IsRunning.ShouldBeTrue();
            loop.MinimumLevel = ArcMotionLevel.Full;
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            loop.IsRunning.ShouldBeTrue();
            changes.ShouldBe([true, false, true, false, true, false, true]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_That_Returns_False_Finishes_The_Loop()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var probe = new Border { Width = 40, Height = 20 };
        var frames = 0;
        var loop = new LiveLoop(probe, _ => ++frames < 3);
        var window = TestHelpers.Show(probe, ArcMotionLevel.Full);
        try
        {
            loop.Start();
            clock.Advance(TimeSpan.FromMilliseconds(200));
            frames.ShouldBe(3);
            loop.IsActive.ShouldBeFalse();
            loop.IsRunning.ShouldBeFalse();
            // A finished loop stays finished through visibility changes until it is started again.
            probe.IsVisible = false;
            probe.IsVisible = true;
            loop.IsRunning.ShouldBeFalse();
            loop.Start();
            loop.IsRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pauses_While_Scrolled_Out_Of_View()
    {
        var probe = new Border { Height = 20 };
        var loop = new LiveLoop(probe, _ => true);
        loop.Start();
        var scroller = new ScrollViewer
        {
            Height = 100,
            Content = new StackPanel { Children = { new Border { Height = 600 }, probe } }
        };
        var window = TestHelpers.Show(scroller, ArcMotionLevel.Full);
        try
        {
            Settle(window);
            loop.IsRunning.ShouldBeFalse();
            scroller.Offset = new Vector(0, 560);
            Settle(window);
            loop.IsRunning.ShouldBeTrue();
            loop.TracksViewport = false;
            scroller.Offset = new Vector(0, 0);
            Settle(window);
            loop.IsRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Running_Frame_Allocates_Nothing()
    {
        var probe = new Border { Width = 40, Height = 20 };
        var phase = 0.0;
        var loop = new LiveLoop(probe, dt =>
        {
            phase = (phase + dt) % 1;
            return true;
        });
        var window = TestHelpers.Show(probe, ArcMotionLevel.Full);
        try
        {
            loop.Start();
            IMotionDriver driver = loop;
            driver.Advance(1.0 / 60).ShouldBeTrue();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                driver.Advance(1.0 / 60);
            }
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
