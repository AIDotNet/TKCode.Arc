using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.Special.Ai;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>The StretchRefresh control: pull, wheel, button, the refresh cycle, the rows, keyboard and announcements.</summary>
public class StretchRefreshTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private sealed class Fixture
    {
        public StretchRefresh Control = null!;
        public StretchRefreshSampleFeed Feed = null!;
        public AgentStatusTestClock Clock = null!;
        public Window Window = null!;
        public readonly List<StretchRefreshStartedEventArgs> Started = new();
        public readonly List<StretchRefreshCompletedEventArgs> Completed = new();
        public int Calls;

        public void Pump(int rounds = 3)
        {
            for (var i = 0; i < rounds; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public void Advance(double ms)
        {
            Clock.Advance(ms);
            Pump();
        }

        /// <summary>A point on the feed, <paramref name="y"/> below the sheet's top.</summary>
        public Point FeedPoint(double y = 40)
        {
            var sheet = Control.Sheet!;
            return sheet.TranslatePoint(new Point(sheet.Bounds.Width / 2, y), Window) ?? default;
        }

        public void Press(Point p)
        {
            Pump();
            Window.MouseMove(p, RawInputModifiers.None);
            Window.MouseDown(p, MouseButton.Left);
        }

        public void MoveTo(Point p) => Window.MouseMove(p, RawInputModifiers.LeftMouseButton);

        public void Release(Point p)
        {
            Window.MouseUp(p, MouseButton.Left);
            Pump();
        }

        public void Pull(double dy, int steps = 10)
        {
            var from = FeedPoint();
            Press(from);
            for (var i = 1; i <= steps; i++)
            {
                MoveTo(from + new Vector(0, dy * i / steps));
            }
        }

        public string? Status => Control.StatusSwap?.Text;
    }

    private static Fixture Open(ArcMotionLevel level = ArcMotionLevel.Off, Func<CancellationToken, Task<string?>>? handler = null)
    {
        var f = new Fixture { Clock = new AgentStatusTestClock() };
        f.Feed = new StretchRefreshSampleFeed(f.Clock);
        f.Control = new StretchRefresh
        {
            Title = StretchRefreshSamples.Title,
            Subtitle = f.Feed.Subtitle,
            ItemsSource = f.Feed.Items,
            Width = 376,
            Height = 368,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Time = f.Clock
        };
        f.Control.RefreshHandler = handler ?? (async ct =>
        {
            f.Calls++;
            var result = await f.Feed.RefreshAsync(ct);
            f.Control.Subtitle = f.Feed.Subtitle;
            return result;
        });
        f.Control.RefreshStarted += (_, e) => f.Started.Add(e);
        f.Control.RefreshCompleted += (_, e) => f.Completed.Add(e);
        f.Window = TestHelpers.Show(f.Control, level);
        f.Window.Width = 600;
        f.Window.Height = 600;
        f.Pump();
        return f;
    }

    [AvaloniaFact]
    public void Defaults_match_the_design()
    {
        var f = Open();
        var c = f.Control;
        Assert.Equal(72, c.Threshold);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.False(c.IsRefreshing);
        Assert.Equal("Pull to refresh", f.Status);
        Assert.Equal("Refresh", AutomationProperties.GetName(c.RefreshButton!));
        Assert.Equal(StretchRefreshSamples.Title, AutomationProperties.GetName(c.Scroller!));
        Assert.True(c.Scroller!.Focusable);
        Assert.Equal(7, c.List!.Rows.Count());
        Assert.Equal(0, c.PullOffset);
        Assert.Equal(0, c.Anchor!.Opacity);
        Assert.Equal(36, c.RefreshButton!.Bounds.Width);
        Assert.Equal(72, c.Meter!.Bounds.Width);
        Assert.Equal(3, c.Meter.Bounds.Height);
        // Rows after the first carry the separator; the first row is padded 12 20.
        var first = c.List.Rows.First();
        Assert.Equal(new Thickness(20, 12), first.Padding);
    }

    [AvaloniaFact]
    public void A_short_pull_follows_the_resistance_curve_and_springs_back()
    {
        var f = Open();
        f.Pull(100);
        var c = f.Control;
        Assert.Equal(StretchRefreshState.Pulling, c.State);
        Assert.Equal(56, c.PullOffset, 3);
        Assert.Equal(56, c.Tension, 3);
        var rows = c.List!.Rows.ToList();
        Assert.Equal(0, rows[0].SpreadOffset);
        Assert.Equal(2.8, rows[1].SpreadOffset, 3);
        Assert.Equal(5.6, rows[2].SpreadOffset, 3);
        Assert.Equal(1, c.Anchor!.Opacity);
        Assert.Equal("Pull to refresh", f.Status);
        f.Release(f.FeedPoint() + new Vector(0, 100));
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.Equal(0, c.PullOffset);
        Assert.Equal(0, f.Calls);
    }

    [AvaloniaFact]
    public void A_pull_past_the_threshold_arms_and_refreshes_on_release()
    {
        var f = Open();
        var c = f.Control;
        var from = f.FeedPoint();
        f.Pull(400);
        Assert.Equal(140, c.PullOffset, 3);
        Assert.Equal(StretchRefreshState.Armed, c.State);
        Assert.Equal("Release to refresh", f.Status);
        // Back under the threshold and over it again.
        f.MoveTo(from + new Vector(0, 120));
        Assert.Equal(StretchRefreshState.Pulling, c.State);
        Assert.Equal("Pull to refresh", f.Status);
        f.MoveTo(from + new Vector(0, 300));
        Assert.Equal(StretchRefreshState.Armed, c.State);
        f.Release(from + new Vector(0, 300));

        Assert.Equal(StretchRefreshState.Refreshing, c.State);
        Assert.True(c.IsRefreshing);
        Assert.Equal(60, c.PullOffset);
        Assert.Equal(0, c.Tension);
        Assert.Equal("Refreshing", f.Status);
        Assert.Equal(StretchRefreshTrigger.Pull, Assert.Single(f.Started).Trigger);
        Assert.Equal("Refreshing", AutomationProperties.GetItemStatus(c.Scroller!));
        Assert.Equal(1, f.Calls);

        f.Advance(1149);
        Assert.Equal(StretchRefreshState.Refreshing, c.State);
        f.Advance(1);
        Assert.Equal(StretchRefreshState.Done, c.State);
        Assert.False(c.IsRefreshing);
        Assert.Equal("3 new updates", f.Status);
        Assert.Equal(new[] { "Refreshing", "3 new updates" }, c.Announcements);
        Assert.Equal("3 new updates", Assert.Single(f.Completed).ResultText);
        Assert.Equal(10, c.List!.Rows.Count());
        Assert.Equal("n1", c.List.Rows.First().Key);
        Assert.Null(AutomationProperties.GetItemStatus(c.Scroller!));
        Assert.True(c.MarkHost!.IsVisible);
        Assert.Equal("Updated just now", c.SubtitleSwap!.Text);

        // Reduced and off: the sheet returns and the cycle ends together 1200 ms after the result.
        f.Advance(1199);
        Assert.Equal(StretchRefreshState.Done, c.State);
        Assert.Equal(60, c.PullOffset);
        f.Advance(1);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.Equal(0, c.PullOffset);
        Assert.Equal("Pull to refresh", f.Status);
    }

    [AvaloniaFact]
    public void Pulls_and_the_button_are_ignored_while_busy()
    {
        var f = Open();
        var c = f.Control;
        Assert.True(c.RefreshAsync().IsCompleted == false);
        Assert.Equal(StretchRefreshState.Refreshing, c.State);
        Assert.Contains("busy", c.RefreshButton!.Classes);
        var second = c.RefreshAsync();
        Assert.True(second.IsCompleted);
        Assert.False(second.Result);
        f.Pull(400);
        Assert.Equal(60, c.PullOffset);
        f.Release(f.FeedPoint());
        Assert.Equal(1, f.Calls);
        f.Advance(1150);
        Assert.Equal(StretchRefreshState.Done, c.State);
        Assert.False(c.RefreshAsync().Result);
        Assert.Contains("busy", c.RefreshButton.Classes);
        f.Advance(1200);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.DoesNotContain("busy", c.RefreshButton.Classes);
    }

    [AvaloniaFact]
    public void Enter_and_Space_on_the_button_refresh()
    {
        var f = Open();
        var c = f.Control;
        c.RefreshButton!.Focus(NavigationMethod.Tab);
        f.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        f.Pump();
        Assert.Equal(StretchRefreshState.Refreshing, c.State);
        Assert.Equal(StretchRefreshTrigger.Button, f.Started[0].Trigger);
        // Busy: the button keeps focus but does nothing.
        f.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Pump();
        Assert.Equal(1, f.Calls);
        Assert.True(c.RefreshButton.IsFocused);
        f.Advance(1150);
        f.Advance(1200);
        f.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Pump();
        Assert.Equal(2, f.Calls);
        f.Advance(1150);
        Assert.Equal("1 new update", f.Status);
    }

    [AvaloniaFact]
    public void The_preview_script_adds_three_one_two_then_nothing()
    {
        var f = Open();
        var c = f.Control;
        var results = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            c.RefreshAsync();
            f.Advance(1150);
            results.Add(f.Status!);
            f.Advance(1200);
        }
        Assert.Equal(new[] { "3 new updates", "1 new update", "2 new updates", "You’re all caught up" }, results);
        Assert.Equal(13, c.List!.Rows.Count());
        // The empty fourth refresh still marks everything read.
        Assert.DoesNotContain(f.Feed.Items, a => a.IsUnread);
        f.Feed.Reset();
        f.Pump();
        Assert.Equal(7, c.List.Rows.Count());
    }

    [AvaloniaFact]
    public void The_wheel_pulls_at_the_top_and_releases_after_150ms_idle()
    {
        var f = Open();
        var c = f.Control;
        var p = f.FeedPoint();
        f.Window.MouseMove(p, RawInputModifiers.None);
        f.Pump();
        for (var i = 0; i < 3; i++)
        {
            f.Window.MouseWheel(p, new Vector(0, 1));
            f.Pump();
        }
        Assert.Equal(StretchRefreshMath.Resist(150), c.PullOffset, 3);
        Assert.Equal(StretchRefreshState.Armed, c.State);
        Assert.Equal(0, c.Scroller!.Offset.Y);
        f.Advance(149);
        Assert.Equal(StretchRefreshState.Armed, c.State);
        f.Advance(1);
        Assert.Equal(StretchRefreshState.Refreshing, c.State);
        Assert.Equal(StretchRefreshTrigger.Wheel, f.Started[0].Trigger);
    }

    [AvaloniaFact]
    public void A_short_wheel_pull_springs_back_and_the_wheel_scrolls_when_not_at_top()
    {
        var f = Open();
        var c = f.Control;
        var p = f.FeedPoint();
        f.Window.MouseMove(p, RawInputModifiers.None);
        f.Window.MouseWheel(p, new Vector(0, 1));
        f.Pump();
        Assert.Equal(StretchRefreshState.Pulling, c.State);
        f.Advance(150);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.Equal(0, f.Calls);
        // Scroll down, then a wheel-up only scrolls (no pull) until the feed is back at the top.
        f.Window.MouseWheel(p, new Vector(0, -2));
        f.Pump();
        Assert.True(c.Scroller!.Offset.Y > 0);
        f.Window.MouseWheel(p, new Vector(0, 1));
        f.Pump();
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.Equal(0, c.PullOffset);
    }

    [AvaloniaFact]
    public void A_drag_does_not_pull_when_the_feed_is_scrolled()
    {
        var f = Open();
        var c = f.Control;
        c.Scroller!.Offset = new Vector(0, 80);
        f.Pump();
        f.Pull(200);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        Assert.Equal(0, c.PullOffset);
        f.Release(f.FeedPoint());
    }

    [AvaloniaFact]
    public void A_pull_whose_first_move_leaves_the_feed_still_pulls()
    {
        var f = Open();
        var c = f.Control;
        var from = f.FeedPoint(4);
        f.Press(from);
        // Straight out of the control to the right and down: Arc's pull sticks here; ours follows.
        f.MoveTo(new Point(560, from.Y + 200));
        Assert.Equal(StretchRefreshState.Armed, c.State);
        Assert.Equal(StretchRefreshMath.Resist(200), c.PullOffset, 3);
        f.Release(new Point(560, from.Y + 200));
        Assert.Equal(StretchRefreshState.Refreshing, c.State);
    }

    [AvaloniaFact]
    public void A_failing_handler_shows_the_error_label()
    {
        var f = Open(handler: async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("offline");
        });
        var c = f.Control;
        c.RefreshAsync();
        f.Pump();
        Assert.Equal(StretchRefreshState.Failed, c.State);
        Assert.Equal("Couldn’t refresh", f.Status);
        var done = Assert.Single(f.Completed);
        Assert.True(done.IsFailed);
        Assert.IsType<InvalidOperationException>(done.Error);
        f.Advance(1200);
        Assert.Equal(StretchRefreshState.Idle, c.State);
    }

    [AvaloniaFact]
    public void An_empty_result_shows_the_done_label_and_labels_can_be_replaced()
    {
        var f = Open(handler: _ => Task.FromResult<string?>(null));
        var c = f.Control;
        c.Labels = new StretchRefreshLabels { PullHint = "下拉刷新", Done = "已是最新", RefreshButton = "刷新动态" };
        f.Pump();
        Assert.Equal("下拉刷新", f.Status);
        Assert.Equal("刷新动态", AutomationProperties.GetName(c.RefreshButton!));
        c.RefreshAsync();
        f.Pump();
        Assert.Equal("已是最新", f.Status);
    }

    [AvaloniaFact]
    public void Threshold_changes_where_the_pull_arms()
    {
        var f = Open();
        var c = f.Control;
        c.Threshold = 40;
        f.Pull(100);
        Assert.Equal(StretchRefreshState.Armed, c.State);
        Assert.Equal(1, c.Meter!.FillScale);
    }

    [AvaloniaFact]
    public void New_rows_are_inserted_without_detaching_the_rows_that_stay()
    {
        var f = Open(ArcMotionLevel.Full);
        var c = f.Control;
        var before = c.List!.Rows.ToList();
        var detached = 0;
        foreach (var row in before)
        {
            row.DetachedFromVisualTree += (_, _) => detached++;
        }
        c.RefreshAsync();
        f.Advance(1150);
        f.Pump();
        var after = c.List.Rows.ToList();
        Assert.Equal(0, detached);
        Assert.Equal(10, after.Count);
        Assert.Equal(before, after.Skip(3));
        Assert.Equal(new[] { 0, 1, 2, 3 }, after.Take(4).Select(r => r.Index));
        // The spread reaches the visible rows only; rows below the feed's viewport stay unshifted.
        f.Advance(1210);
        f.Advance(650);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        f.Pull(400);
        var rows = c.List.Rows.ToList();
        Assert.True(c.PullOffset > 100);
        Assert.Equal(c.PullOffset * 0.05, rows[1].SpreadOffset, 3);
        Assert.Equal(0, rows[^1].SpreadOffset);
        f.Release(f.FeedPoint());
    }

    [AvaloniaFact]
    public void Rows_show_unread_dots_and_names()
    {
        var f = Open();
        var c = f.Control;
        c.RefreshAsync();
        f.Advance(1150);
        var rows = c.List!.Rows.Select(r => (StretchRefreshActivityRow)r.Child!).ToList();
        Assert.Contains(":unread", rows[0].Classes);
        Assert.StartsWith("New. Kenji Watanabe replied on Fix duplicate invoice emails", AutomationProperties.GetName(rows[0]));
        Assert.DoesNotContain(":unread", rows[3].Classes);
        Assert.Contains(":glyph", rows[1].Classes);
        Assert.Contains(":person", rows[0].Classes);
        f.Feed.Items[0].IsUnread = false;
        Assert.DoesNotContain(":unread", rows[0].Classes);
        Assert.DoesNotContain("New.", AutomationProperties.GetName(rows[0]));
    }

    [AvaloniaFact]
    public void Keyboard_focus_on_the_feed_shows_an_inset_ring()
    {
        var f = Open();
        var c = f.Control;
        var ring = TestHelpers.Descendant<Border>(c, StretchRefresh.FeedRingPart);
        c.Scroller!.Focus(NavigationMethod.Tab);
        f.Pump();
        Assert.True(ring.IsVisible);
        c.RefreshButton!.Focus(NavigationMethod.Tab);
        f.Pump();
        Assert.False(ring.IsVisible);
        c.Scroller.Focus(NavigationMethod.Pointer);
        f.Pump();
        Assert.False(ring.IsVisible);
    }

    [AvaloniaFact]
    public void Full_motion_springs_the_sheet_runs_the_line_and_settles()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Full);
        var c = f.Control;
        f.Pull(400);
        // Armed: the track over-stretches to 1.12 on a quick spring.
        clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
        Assert.Equal(1.12, c.Meter!.TrackScale, 2);
        f.Release(f.FeedPoint());
        Assert.Equal(StretchRefreshState.Refreshing, c.State);
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        Assert.InRange(c.PullOffset, 60.5, 139);
        clock.Advance(TimeSpan.FromMilliseconds(800), Frame);
        Assert.Equal(60, c.PullOffset, 0);
        Assert.Equal(0, c.Tension, 0);
        Assert.InRange(c.Meter.RunPosition, 0.01, 1.5);
        Assert.Equal(1, c.Meter.RunOpacity, 2);
        Assert.Equal(0, c.Meter.FillOpacity, 2);
        Assert.NotEqual(0, c.SpinAngle);

        f.Advance(1150);
        Assert.Equal(StretchRefreshState.Done, c.State);
        // New rows wait 100 ms, then fade in; the old first row slides down from where it was.
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        var rows = c.List!.Rows.ToList();
        Assert.True(rows[0].Opacity < 0.05);
        Assert.True(rows[3].SlideOffset < 0);
        clock.Advance(TimeSpan.FromMilliseconds(1000), Frame);
        Assert.Equal(1, rows[0].Opacity, 2);
        Assert.Equal(0, rows[3].SlideOffset, 1);
        Assert.Equal(0.4, c.Meter.TrackScale, 2);

        // The sheet returns at +1210 ms and the cycle ends at +1860 ms.
        f.Advance(1210);
        clock.Advance(TimeSpan.FromMilliseconds(600), Frame);
        Assert.True(c.PullOffset < 2);
        Assert.Equal(StretchRefreshState.Done, c.State);
        f.Advance(650);
        Assert.Equal(StretchRefreshState.Idle, c.State);
        clock.Advance(TimeSpan.FromMilliseconds(600), Frame);
        Assert.Equal(1, c.Meter.TrackScale, 2);
        Assert.Equal(1, c.Meter.TrackOpacity, 2);
    }

    [AvaloniaFact]
    public void Reduced_motion_keeps_the_pull_but_jumps_and_does_not_overstretch()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Reduced);
        var c = f.Control;
        f.Pull(400);
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        Assert.Equal(140, c.PullOffset, 3);
        Assert.Equal(1, c.Meter!.TrackScale, 3);
        f.Release(f.FeedPoint());
        Assert.Equal(60, c.PullOffset);
        clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
        // The segment does not run and the glyph does not turn: the segment rests mid-track and breathes instead.
        Assert.Equal(StretchRefreshMath.RunTravel / 2, c.Meter.RunPosition);
        Assert.Equal(0, c.SpinAngle);
        Assert.True(c.RunPulse < 0.95);
        clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
        Assert.True(c.RunPulse < 0.6);
        Assert.Equal(StretchRefreshMath.RunTravel / 2, c.Meter.RunPosition);
        Assert.Equal(0, c.SpinAngle);
    }

    [AvaloniaFact]
    public void Motion_off_keeps_the_running_segment_still_and_opaque()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var f = Open(ArcMotionLevel.Off);
        var c = f.Control;
        f.Pull(400);
        f.Release(f.FeedPoint());
        clock.Advance(TimeSpan.FromMilliseconds(900), Frame);
        Assert.Equal(0, c.Meter!.RunPosition);
        Assert.Equal(1, c.RunPulse);
        Assert.Equal(0, c.SpinAngle);
    }
}
