using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary>
/// <see cref="ShowpieceRenderer"/> and <see cref="ShowpieceCacheHold"/>: the GPU gate (the headless renderer is software,
/// so the caches stay off), switching a part's cache, the cache scale and the hold between rest and vectors.
/// </summary>
public class ShowpieceRendererTests
{
    private static void RunFrames(ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Theory]
    [InlineData(0.1, 0.25)]
    [InlineData(0.25, 0.25)]
    [InlineData(1, 1)]
    [InlineData(1.01, 1.25)]
    [InlineData(2.3, 2.5)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Cache_scale_rounds_up_to_a_quarter(double largest, double expected)
    {
        ShowpieceRenderer.CacheScale(largest).ShouldBe(expected);
    }

    [AvaloniaFact]
    public void SetCache_switches_the_mode_once_and_back()
    {
        var part = new Border { Width = 40, Height = 40 };
        var window = TestHelpers.Show(part);
        try
        {
            var cache = new BitmapCache();
            ShowpieceRenderer.SetCache(part, cache).ShouldBeTrue();
            part.CacheMode.ShouldBeSameAs(cache);
            ShowpieceRenderer.SetCache(part, cache).ShouldBeFalse();
            ShowpieceRenderer.SetCache(part, null).ShouldBeTrue();
            part.CacheMode.ShouldBeNull();
            ShowpieceRenderer.SetCache(null, cache).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_headless_compositor_is_not_a_GPU()
    {
        var part = new Border { Width = 40, Height = 40 };
        var window = TestHelpers.Show(part);
        try
        {
            bool? answer = null;
            ShowpieceRenderer.Detect(part, gpu => answer = gpu);
            for (var i = 0; i < 20 && answer is null; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            answer.ShouldBe(false);
            // Known now: the next detection answers at once.
            bool? again = null;
            ShowpieceRenderer.Detect(part, gpu => again = gpu);
            again.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_hold_runs_out_after_half_a_second_and_can_be_cancelled_or_restarted()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var part = new Border { Width = 40, Height = 40 };
        var window = TestHelpers.Show(part, ArcMotionLevel.Full);
        try
        {
            var elapsed = 0;
            var hold = new ShowpieceCacheHold(part, () => elapsed++);
            hold.Start();
            hold.IsRunning.ShouldBeTrue();
            RunFrames(clock, (int)Math.Floor(ShowpieceRenderer.CacheHold.TotalMilliseconds / 16) - 2);
            elapsed.ShouldBe(0);
            RunFrames(clock, 4);
            elapsed.ShouldBe(1);
            hold.IsRunning.ShouldBeFalse();

            // Cancelled: no call back. Started again: the full half second again.
            hold.Start();
            RunFrames(clock, 10);
            hold.Cancel();
            RunFrames(clock, 60);
            elapsed.ShouldBe(1);
            hold.Start();
            RunFrames(clock, 20);
            hold.Start();
            RunFrames(clock, 25);
            elapsed.ShouldBe(1);
            RunFrames(clock, 10);
            elapsed.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_hold_without_motion_or_a_window_ends_at_once()
    {
        var detached = new Border();
        var calls = 0;
        new ShowpieceCacheHold(detached, () => calls++).Start();
        calls.ShouldBe(1);

        var part = new Border { Width = 40, Height = 40 };
        var window = TestHelpers.Show(part, ArcMotionLevel.Off);
        try
        {
            new ShowpieceCacheHold(part, () => calls++).Start();
            calls.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}
