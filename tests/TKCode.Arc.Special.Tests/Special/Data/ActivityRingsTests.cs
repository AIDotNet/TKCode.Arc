using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Data;

/// <summary>Activity rings: progress past the goal, the legend, the goal event, layout and automation.</summary>
public class ActivityRingsTests
{
    private static ActivityRings Rings(double move = 380, double exercise = 22) => new()
    {
        Label = "Today",
        Rings =
        {
            new ActivityRing { Label = "Move", Value = move, Goal = 520, Unit = "kcal" },
            new ActivityRing { Label = "Exercise", Value = exercise, Goal = 30, Unit = "min" }
        }
    };

    [Fact]
    public void Progress_is_value_over_goal_and_keeps_going_past_it()
    {
        ActivityRings.ProgressOf(50, 100).ShouldBe(0.5);
        ActivityRings.ProgressOf(150, 100).ShouldBe(1.5);
        ActivityRings.ProgressOf(10, 0).ShouldBe(0);
        ActivityRings.ProgressOf(-5, 100).ShouldBe(0);
        ActivityRings.ProgressOf(double.NaN, 100).ShouldBe(0);
        ActivityRings.ProgressOf(1000, 100).ShouldBe(ActivityRings.MaxProgress);
        new ActivityRing { Value = 45, Goal = 30 }.Progress.ShouldBe(1.5);
    }

    [Fact]
    public void A_lap_past_the_goal_draws_only_its_own_part()
    {
        ActivityRingsCanvas.Fraction(0.4).ShouldBe(0.4, 1e-9);
        ActivityRingsCanvas.Fraction(1.3).ShouldBe(0.3, 1e-9);
        ActivityRingsCanvas.Fraction(1).ShouldBe(1);
        ActivityRingsCanvas.Fraction(2).ShouldBe(1);
        ActivityRingsCanvas.Fraction(0).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Shows_each_ring_at_its_progress_without_motion()
    {
        var rings = Rings(780, 15);
        var window = TestHelpers.Show(rings);
        try
        {
            var shown = rings.GetShownProgress();
            shown.Count.ShouldBe(2);
            shown[0].ShouldBe(1.5, 1e-9);
            shown[1].ShouldBe(0.5, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_legend_names_each_goal_and_follows_its_value()
    {
        var rings = Rings();
        var window = TestHelpers.Show(rings);
        try
        {
            var rows = rings.LegendRows;
            rows.Count.ShouldBe(2);
            rows[0].LabelText.ShouldBe("Move");
            rows[0].GoalText.ShouldBe("/ 520 kcal");
            rows[0].ValueDigits.Value.ShouldBe(380);
            rows[0].Mark.Value.ShouldBe(380 / 520.0, 1e-9);

            rings.Rings[0].Value = 600;
            rows[0].ValueDigits.Value.ShouldBe(600);
            rows[0].Mark.Value.ShouldBe(1);
            rings.GetShownProgress()[0].ShouldBe(600 / 520.0, 1e-9);

            rings.Rings.Add(new ActivityRing { Label = "Stand", Value = 3, Goal = 12 });
            rings.LegendRows.Count.ShouldBe(3);
            rings.LegendRows[2].GoalText.ShouldBe("/ 12");
            rings.Rings.RemoveAt(0);
            rings.LegendRows.Count.ShouldBe(2);
            rings.LegendRows[0].LabelText.ShouldBe("Exercise");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rings_take_the_series_colors_unless_they_have_their_own()
    {
        var rings = Rings();
        rings.Rings[1].Brush = Brushes.Orange;
        var window = TestHelpers.Show(rings);
        try
        {
            rings.Tracks[0].Brush.ShouldNotBeNull();
            rings.Tracks[0].Brush.ShouldBeSameAs(rings.Series1);
            rings.Tracks[1].Brush.ShouldBe(Brushes.Orange);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reaching_a_goal_raises_the_event_once_and_announces_it()
    {
        var rings = Rings(500);
        var reached = new List<string>();
        rings.GoalReached += (_, e) => reached.Add(e.Ring.Label);
        var window = TestHelpers.Show(rings);
        try
        {
            rings.Rings[0].Value = 510;
            reached.ShouldBeEmpty();
            rings.Rings[0].Value = 530;
            reached.ShouldBe(["Move"]);
            rings.Rings[0].Value = 700;
            reached.Count.ShouldBe(1);
            rings.Announcement.ShouldBe("Move goal reached");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_ring_already_past_its_goal_on_first_view_raises_nothing()
    {
        var rings = Rings(900);
        var reached = 0;
        rings.GoalReached += (_, _) => reached++;
        var window = TestHelpers.Show(rings);
        try
        {
            reached.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_legend_sits_beside_the_rings_when_wide_and_under_them_when_narrow()
    {
        var rings = Rings();
        var window = TestHelpers.Show(rings);
        try
        {
            var layout = TestHelpers.Descendant<ActivityRingsLayout>(rings);
            window.Width = 600;
            Dispatcher.UIThread.RunJobs();
            layout.IsSideBySide.ShouldBeTrue();
            window.Width = 280;
            Dispatcher.UIThread.RunJobs();
            layout.IsSideBySide.ShouldBeFalse();
            rings.IsLegendVisible = false;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<StackPanel>(rings, ActivityRings.LegendPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_rings_shrink_to_fit_a_small_space()
    {
        var rings = Rings();
        rings.Width = 100;
        rings.IsLegendVisible = false;
        var window = TestHelpers.Show(rings);
        try
        {
            rings.Canvas!.Bounds.Width.ShouldBeLessThanOrEqualTo(100);
            rings.Canvas.Bounds.Width.ShouldBe(rings.Canvas.Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Assistive_technology_reads_every_goal()
    {
        var rings = Rings();
        var window = TestHelpers.Show(rings);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(rings);
            peer.GetName().ShouldBe("Today");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Image);
            peer.GetHelpText().ShouldBe("Move: 380 of 520 kcal, 73 percent. Exercise: 22 of 30 min, 73 percent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_full_motion_the_rings_fill_from_empty_on_a_spring()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var rings = Rings(520, 30);
        var window = TestHelpers.Show(rings, Motion.ArcMotionLevel.Full);
        try
        {
            rings.Tracks[0].Spring.ShouldNotBeNull();
            rings.GetShownProgress()[0].ShouldBeLessThan(0.5);
            var peak = 0.0;
            for (var i = 0; i < 180; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(16));
                Dispatcher.UIThread.RunJobs();
                peak = Math.Max(peak, rings.GetShownProgress()[0]);
            }
            rings.GetShownProgress()[0].ShouldBe(1, 1e-3);
            // The spring overshoots a little before it settles.
            peak.ShouldBeGreaterThan(1.0);
        }
        finally
        {
            window.Close();
        }
    }
}
