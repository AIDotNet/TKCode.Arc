using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Data;

/// <summary>The time dial: angles and snapping, keys, dragging round the face, AM and PM, and automation.</summary>
public class TimeDialTests
{
    [Fact]
    public void Angles_run_clockwise_from_twelve()
    {
        var center = new Point(100, 100);
        TimeDialMath.AngleOf(new Point(100, 0), center).ShouldBe(0, 1e-9);
        TimeDialMath.AngleOf(new Point(200, 100), center).ShouldBe(90, 1e-9);
        TimeDialMath.AngleOf(new Point(100, 200), center).ShouldBe(180, 1e-9);
        TimeDialMath.AngleOf(new Point(0, 100), center).ShouldBe(270, 1e-9);
        TimeDialMath.Turn(350, 10).ShouldBe(20, 1e-9);
        TimeDialMath.Turn(10, 350).ShouldBe(-20, 1e-9);
    }

    [Fact]
    public void A_place_on_the_face_means_the_value_nearest_the_current_one()
    {
        // A clock: 3 o'clock is 3:00 in the morning or 15:00, whichever is nearer.
        TimeDialMath.Nearest(90, 60, 720, 0, 1435, true).ShouldBe(180, 1e-9);
        TimeDialMath.Nearest(90, 800, 720, 0, 1435, true).ShouldBe(900, 1e-9);
        // Just past midnight going back wraps to the evening.
        TimeDialMath.Nearest(359, 0, 720, 0, 1435, true).ShouldBe(1438, 0.1);
        // A duration of up to two turns: the second turn when the value is already on it, never past the maximum.
        TimeDialMath.Nearest(90, 70, 60, 0, 120, false).ShouldBe(75, 1e-9);
        TimeDialMath.Nearest(90, 10, 60, 0, 120, false).ShouldBe(15, 1e-9);
        TimeDialMath.Nearest(350, 118, 60, 0, 120, false).ShouldBe(118.333, 0.01);
        TimeDialMath.Snap(7.4, 5).ShouldBe(5);
        TimeDialMath.Snap(7.6, 5).ShouldBe(10);
    }

    [Fact]
    public void The_readout_reads_a_clock_or_a_duration()
    {
        TimeDialMath.Readout(19 * 60 + 5, TimeDialMode.Time, false, "AM", "PM", "hr", "min").ShouldBe(("7:05", "PM"));
        TimeDialMath.Readout(0, TimeDialMode.Time, false, "AM", "PM", "hr", "min").ShouldBe(("12:00", "AM"));
        TimeDialMath.Readout(19 * 60 + 5, TimeDialMode.Time, true, "AM", "PM", "hr", "min").ShouldBe(("19:05", ""));
        TimeDialMath.Readout(25, TimeDialMode.Duration, false, "AM", "PM", "hr", "min").ShouldBe(("25", "min"));
        TimeDialMath.Readout(85, TimeDialMode.Duration, false, "AM", "PM", "hr", "min").ShouldBe(("1:25", "hr"));
        TimeDialMath.Spoken(85, TimeDialMode.Duration, false, "AM", "PM", "hr", "min").ShouldBe("1 hr 25 min");
        TimeDialMath.Spoken(120, TimeDialMode.Duration, false, "AM", "PM", "hr", "min").ShouldBe("2 hr");
        TimeDialMath.QuarterLabel(1, TimeDialMode.Time, 720).ShouldBe("3");
        TimeDialMath.QuarterLabel(2, TimeDialMode.Duration, 60).ShouldBe("30");
        TimeDialMath.QuarterLabel(1, TimeDialMode.Duration, 480).ShouldBe("2h");
    }

    [AvaloniaFact]
    public void A_time_wraps_over_midnight_and_a_duration_stays_in_range()
    {
        var dial = new TimeDial { Value = TimeSpan.FromHours(25) };
        dial.Value.ShouldBe(TimeSpan.FromHours(1));
        var timer = new TimeDial { Mode = TimeDialMode.Duration, Maximum = TimeSpan.FromMinutes(90), Value = TimeSpan.FromHours(3) };
        timer.Value.ShouldBe(TimeSpan.FromMinutes(90));
        timer.Maximum = TimeSpan.FromMinutes(60);
        timer.Value.ShouldBe(TimeSpan.FromMinutes(60));
    }

    [AvaloniaFact]
    public void Keys_step_the_value_and_commit_it()
    {
        var dial = new TimeDial { Value = new TimeSpan(23, 55, 0) };
        var commits = new List<TimeSpan>();
        dial.ValueCommitted += (_, e) => commits.Add(e.Value);
        var window = TestHelpers.Show(dial);
        try
        {
            dial.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            dial.Value.ShouldBe(TimeSpan.Zero);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            dial.Value.ShouldBe(new TimeSpan(23, 55, 0));
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            dial.Value.ShouldBe(new TimeSpan(22, 55, 0));
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
            dial.Value.ShouldBe(new TimeSpan(10, 55, 0));
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
            dial.Value.ShouldBe(new TimeSpan(10, 55, 0));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            dial.Value.ShouldBe(TimeSpan.Zero);
            // The second A changes nothing and commits nothing.
            commits.Count.ShouldBe(5);
            dial.ValueText.ShouldBe("12:00 AM");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_duration_stops_at_its_ends()
    {
        var dial = new TimeDial { Mode = TimeDialMode.Duration, Value = TimeSpan.FromMinutes(115), Maximum = TimeSpan.FromMinutes(120) };
        var window = TestHelpers.Show(dial);
        try
        {
            dial.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            dial.Value.ShouldBe(TimeSpan.FromMinutes(120));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            dial.Value.ShouldBe(TimeSpan.Zero);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            dial.Value.ShouldBe(TimeSpan.Zero);
            dial.ReadoutText.ShouldBe("0 min");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_round_the_face_follows_the_handle_and_commits_on_release()
    {
        var dial = new TimeDial { Width = 240, Height = 240 };
        var commits = 0;
        dial.ValueCommitted += (_, _) => commits++;
        var window = TestHelpers.Show(dial);
        try
        {
            window.Width = 400;
            window.Height = 400;
            Dispatcher.UIThread.RunJobs();
            var (center, radius) = dial.Track;
            Point At(double degrees)
            {
                var r = degrees * Math.PI / 180;
                return dial.TranslatePoint(new Point(center.X + radius * Math.Sin(r), center.Y - radius * Math.Cos(r)), window)!.Value;
            }

            window.MouseDown(At(90), MouseButton.Left);
            dial.Value.ShouldBe(TimeSpan.FromHours(3));
            window.MouseMove(At(135), RawInputModifiers.LeftMouseButton);
            window.MouseMove(At(181), RawInputModifiers.LeftMouseButton);
            dial.Value.ShouldBe(TimeSpan.FromHours(6));
            commits.ShouldBe(0);
            // On round past 12: the afternoon.
            window.MouseMove(At(270), RawInputModifiers.LeftMouseButton);
            window.MouseMove(At(359), RawInputModifiers.LeftMouseButton);
            window.MouseMove(At(30), RawInputModifiers.LeftMouseButton);
            window.MouseUp(At(30), MouseButton.Left);
            dial.Value.ShouldBe(TimeSpan.FromHours(13));
            commits.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_on_the_readout_switches_am_and_pm()
    {
        var dial = new TimeDial { Width = 240, Height = 240, Value = new TimeSpan(7, 30, 0) };
        var window = TestHelpers.Show(dial);
        try
        {
            window.Width = 400;
            window.Height = 400;
            Dispatcher.UIThread.RunJobs();
            var middle = dial.TranslatePoint(dial.Track.Center, window)!.Value;
            window.MouseDown(middle, MouseButton.Left);
            window.MouseUp(middle, MouseButton.Left);
            dial.Value.ShouldBe(new TimeSpan(19, 30, 0));
            dial.ReadoutText.ShouldBe("7:30 PM");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Assistive_technology_sees_a_slider_in_minutes()
    {
        var dial = new TimeDial { Label = "Alarm", Value = new TimeSpan(6, 45, 0) };
        var window = TestHelpers.Show(dial);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(dial);
            peer.GetName().ShouldBe("Alarm");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
            peer.GetHelpText().ShouldBe("6:45 AM");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.Value.ShouldBe(405);
            range.Maximum.ShouldBe(1435);
            range.SmallChange.ShouldBe(5);
            range.SetValue(600);
            dial.Value.ShouldBe(TimeSpan.FromHours(10));
        }
        finally
        {
            window.Close();
        }
    }
}
