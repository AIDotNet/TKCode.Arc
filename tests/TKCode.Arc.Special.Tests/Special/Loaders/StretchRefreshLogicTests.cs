using Avalonia;
using TKCode.Arc;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>The pure pull math, gesture and refresh cycle of StretchRefresh, against the measured numbers.</summary>
public class StretchRefreshLogicTests
{
    [Theory]
    [InlineData(5, 3.457)]
    [InlineData(20, 13.333)]
    [InlineData(60, 36.522)]
    [InlineData(100, 56.0)]
    [InlineData(150, 76.364)]
    [InlineData(200, 93.333)]
    [InlineData(220, 99.355)]
    [InlineData(300, 120.0)]
    [InlineData(400, 140.0)]
    public void Resistance_matches_the_measured_curve(double distance, double expected)
    {
        Assert.InRange(Math.Abs(expected - StretchRefreshMath.Resist(distance)), 0, 0.006);
        Assert.Equal(280 * distance / (distance + 400), StretchRefreshMath.Resist(distance), 9);
    }

    [Fact]
    public void Resistance_is_zero_for_no_or_negative_pull_and_inverts()
    {
        Assert.Equal(0, StretchRefreshMath.Resist(0));
        Assert.Equal(0, StretchRefreshMath.Resist(-30));
        Assert.Equal(138.46, StretchRefreshMath.Unresist(72), 1);
        Assert.Equal(72, StretchRefreshMath.Resist(StretchRefreshMath.Unresist(72)), 6);
    }

    [Theory]
    [InlineData(6.83, 0.118)]
    [InlineData(13.33, 0.389)]
    [InlineData(25.45, 0.894)]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(60, 1)]
    public void Indicator_fades_in_over_the_first_pull(double pull, double opacity)
    {
        Assert.Equal(opacity, StretchRefreshMath.AnchorOpacity(pull), 3);
    }

    [Fact]
    public void Indicator_is_centred_in_the_gap_and_the_sheet_peels()
    {
        Assert.Equal(-17, StretchRefreshMath.AnchorOffset(0));
        Assert.Equal(13, StretchRefreshMath.AnchorOffset(60));
        Assert.Equal(0.5, StretchRefreshMath.Peel(20));
        Assert.Equal(1, StretchRefreshMath.Peel(140));
        Assert.Equal(0.5, StretchRefreshMath.FillScale(36, 72));
        Assert.Equal(1, StretchRefreshMath.FillScale(140, 72));
        Assert.Equal(60 / 72.0, StretchRefreshMath.FillScale(60, 72), 6);
    }

    [Fact]
    public void Rows_spread_by_index_times_tension_times_five_percent()
    {
        Assert.Equal(0, StretchRefreshMath.RowShift(0, 140));
        Assert.Equal(7, StretchRefreshMath.RowShift(1, 140), 6);
        Assert.Equal(42, StretchRefreshMath.RowShift(6, 140), 6);
        Assert.Equal(5.6, StretchRefreshMath.RowShift(2, 56), 6);
    }

    [Fact]
    public void Running_segment_goes_there_and_back_in_700ms_legs()
    {
        Assert.Equal(0, StretchRefreshMath.RunPosition(0));
        Assert.Equal(0.75, StretchRefreshMath.RunPosition(0.35), 3);
        Assert.Equal(1.5, StretchRefreshMath.RunPosition(0.6999), 2);
        Assert.Equal(0.75, StretchRefreshMath.RunPosition(1.05), 3);
        Assert.Equal(0, StretchRefreshMath.RunPosition(1.3999), 2);
        // In-out: slow at the ends.
        Assert.True(StretchRefreshMath.RunPosition(0.07) < 0.05);
    }

    [Fact]
    public void Result_text_falls_back_to_the_done_label()
    {
        var labels = new StretchRefreshLabels();
        Assert.Equal("Up to date", StretchRefreshMath.ResultText(null, labels));
        Assert.Equal("Up to date", StretchRefreshMath.ResultText("  ", labels));
        Assert.Equal("3 new updates", StretchRefreshMath.ResultText("3 new updates", labels));
    }

    [Fact]
    public void Mouse_pull_starts_past_four_px_down_without_an_axis_lock()
    {
        var g = new StretchRefreshPullGesture();
        g.Press(new Point(100, 100), touch: false, atTop: true);
        Assert.Equal(StretchRefreshPullStep.None, g.Move(new Point(100, 104), () => true));
        Assert.Equal(StretchRefreshPullStep.None, g.Move(new Point(160, 103), () => true));
        // Mostly sideways, but more than 4 down: pulls, and the distance is not reduced by the slop.
        Assert.Equal(StretchRefreshPullStep.Started, g.Move(new Point(190, 108), () => true));
        Assert.Equal(8, g.Distance);
        Assert.Equal(StretchRefreshPullStep.Moved, g.Move(new Point(190, 250), () => true));
        Assert.Equal(150, g.Distance);
        // Back above the press point: clamped at 0, still pulling.
        Assert.Equal(StretchRefreshPullStep.Moved, g.Move(new Point(190, 60), () => true));
        Assert.Equal(0, g.Distance);
        Assert.True(g.IsPointerPulling);
        Assert.True(g.Release());
        Assert.False(g.IsPointerPulling);
    }

    [Fact]
    public void Mouse_pull_needs_the_feed_at_its_top_when_it_starts()
    {
        var g = new StretchRefreshPullGesture();
        g.Press(new Point(0, 0), touch: false, atTop: true);
        Assert.Equal(StretchRefreshPullStep.Rejected, g.Move(new Point(0, 10), () => false));
        Assert.Equal(StretchRefreshPullStep.None, g.Move(new Point(0, 80), () => true));
        Assert.False(g.Release());
    }

    [Fact]
    public void Touch_pull_needs_the_feed_at_its_top_when_the_finger_goes_down()
    {
        var g = new StretchRefreshPullGesture();
        g.Press(new Point(0, 0), touch: true, atTop: false);
        Assert.Equal(StretchRefreshPullStep.None, g.Move(new Point(0, 40), () => true));
        Assert.False(g.Release());

        g.Press(new Point(0, 0), touch: true, atTop: true);
        Assert.Equal(StretchRefreshPullStep.Rejected, g.Move(new Point(0, -6), () => true));
        Assert.Equal(StretchRefreshPullStep.None, g.Move(new Point(0, 40), () => true));
        g.Release();

        // The browser's touch slop delays the first move, but the distance still counts from the press.
        g.Press(new Point(0, 0), touch: true, atTop: true);
        Assert.Equal(StretchRefreshPullStep.Started, g.Move(new Point(0, 16), () => true));
        Assert.Equal(16, g.Distance);
    }

    [Fact]
    public void A_pull_can_catch_a_returning_sheet_where_it_is()
    {
        var g = new StretchRefreshPullGesture();
        g.Press(new Point(0, 0), touch: false, atTop: true, from: 50);
        g.Move(new Point(0, 10), () => true);
        Assert.Equal(60, g.Distance);
    }

    [Fact]
    public void Wheel_pulls_only_upwards_at_the_top_and_ends_after_150ms_idle()
    {
        var g = new StretchRefreshPullGesture();
        var t = TimeSpan.Zero;
        Assert.False(g.Wheel(10, atTop: false, t));
        Assert.False(g.Wheel(-10, atTop: true, t));
        Assert.True(g.Wheel(10, atTop: true, t));
        Assert.Equal(10, g.Distance);
        t += TimeSpan.FromMilliseconds(16);
        // Once pulling, a downward event takes some pull back instead of scrolling.
        Assert.True(g.Wheel(190, atTop: true, t));
        Assert.True(g.Wheel(-20, atTop: true, t));
        Assert.Equal(180, g.Distance);
        Assert.False(g.IsWheelIdle(t + TimeSpan.FromMilliseconds(149)));
        Assert.True(g.IsWheelIdle(t + TimeSpan.FromMilliseconds(150)));
        Assert.True(g.EndWheel());
        Assert.Equal(0, g.Distance);
        Assert.False(g.EndWheel());
    }

    [Fact]
    public void Machine_arms_at_the_threshold_both_ways_and_refreshes_on_release()
    {
        var m = new StretchRefreshMachine();
        Assert.True(m.Pull(30));
        Assert.Equal(StretchRefreshState.Pulling, m.State);
        Assert.False(m.Pull(71.9));
        Assert.True(m.Pull(72));
        Assert.Equal(StretchRefreshState.Armed, m.State);
        Assert.True(m.Pull(70));
        Assert.Equal(StretchRefreshState.Pulling, m.State);
        Assert.False(m.Release());
        Assert.Equal(StretchRefreshState.Idle, m.State);

        m.Pull(140);
        Assert.True(m.Release());
        Assert.Equal(StretchRefreshState.Refreshing, m.State);
        Assert.True(m.IsBusy);
        Assert.False(m.Pull(140));
        Assert.False(m.Trigger());
        Assert.True(m.Complete(failed: false));
        Assert.Equal(StretchRefreshState.Done, m.State);
        Assert.False(m.Trigger());
        Assert.True(m.Settle());
        Assert.Equal(StretchRefreshState.Idle, m.State);
    }

    [Fact]
    public void Machine_button_refresh_and_failure()
    {
        var m = new StretchRefreshMachine { Threshold = 100 };
        Assert.False(m.Pull(90) && m.State == StretchRefreshState.Armed);
        m.Cancel();
        Assert.Equal(StretchRefreshState.Idle, m.State);
        Assert.True(m.Trigger());
        Assert.True(m.Complete(failed: true));
        Assert.Equal(StretchRefreshState.Failed, m.State);
        Assert.False(m.Complete(failed: false));
        Assert.True(m.Settle());
        Assert.False(m.Settle());
    }
}
