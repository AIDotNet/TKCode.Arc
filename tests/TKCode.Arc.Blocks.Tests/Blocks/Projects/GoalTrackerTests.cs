using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary><see cref="GoalTracker"/>: progress math, the expected mark, check-ins, the team filter, views and the compact layout.</summary>
public class GoalTrackerTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static GoalTracker Own()
    {
        var tracker = new GoalTracker { PeriodStart = Start, PeriodEnd = Start.AddDays(100), Today = Start.AddDays(50), CurrentUser = "Mia" };
        var kr = new GoalTrackerKeyResult("Reply rate", 40, 80, 60, "%") { Confidence = GoalTrackerConfidence.OnTrack };
        var falling = new GoalTrackerKeyResult("Tickets", 400, 200, 350) { Confidence = GoalTrackerConfidence.AtRisk };
        tracker.Goals.Add(new GoalTrackerGoal("Faster support", "Support") { KeyResults = [kr, falling] });
        tracker.Goals.Add(new GoalTrackerGoal("Grow", "Growth") { KeyResults = [new GoalTrackerKeyResult("Teams", 0, 10, 2)] });
        return tracker;
    }

    [AvaloniaFact]
    public void Progress_Handles_Rising_And_Falling_Targets()
    {
        new GoalTrackerKeyResult("x", 40, 80, 60).Progress.ShouldBe(0.5);
        new GoalTrackerKeyResult("x", 400, 200, 350).Progress.ShouldBe(0.25);
        new GoalTrackerKeyResult("x", 0, 10, 20).Progress.ShouldBe(1);
        new GoalTrackerKeyResult("x", 5, 5, 4).Progress.ShouldBe(0);
        var goal = new GoalTrackerGoal("g") { KeyResults = [new("a", 0, 10, 5) { Confidence = GoalTrackerConfidence.OnTrack }, new("b", 0, 10, 10) { Confidence = GoalTrackerConfidence.OffTrack }] };
        goal.Progress.ShouldBe(0.75);
        goal.Confidence.ShouldBe(GoalTrackerConfidence.OffTrack);
    }

    [AvaloniaFact]
    public void Shows_Samples_And_The_Expected_Mark()
    {
        var tracker = new GoalTracker();
        var window = TestHelpers.Show(tracker);
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        tracker.IsShowingSamples.ShouldBeTrue();
        tracker.EffectiveGoals.Count.ShouldBe(3);
        tracker.EffectiveStart.Day.ShouldBe(1);
        Own().ExpectedProgress.ShouldBe(0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Check_In_Updates_The_Result_And_Raises()
    {
        var tracker = Own();
        var events = new List<GoalTrackerCheckedInEventArgs>();
        tracker.CheckedIn += (_, e) => events.Add(e);
        var window = TestHelpers.Show(tracker);
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        var kr = tracker.Goals[0].KeyResults[0];
        tracker.OpenCheckIn(kr);
        tracker.Editing.ShouldBe(kr);
        tracker.List!.GetLogicalDescendants().OfType<NumberField>().Count().ShouldBe(1);
        tracker.CheckIn(kr, 72, GoalTrackerConfidence.AtRisk, " Hired two ");
        kr.Current.ShouldBe(72);
        kr.Confidence.ShouldBe(GoalTrackerConfidence.AtRisk);
        var checkIn = kr.CheckIns.Single();
        checkIn.Date.ShouldBe(Start.AddDays(50));
        checkIn.Note.ShouldBe("Hired two");
        checkIn.Author.ShouldBe("Mia");
        events.Single().Goal.Title.ShouldBe("Faster support");
        tracker.Editing.ShouldBeNull();
        tracker.Announcer!.Announcement.ShouldNotBeNull().ShouldContain("Reply rate");
        Should.Throw<ArgumentException>(() => tracker.CheckIn(new GoalTrackerKeyResult(), 1, GoalTrackerConfidence.OnTrack));
        window.Close();
    }

    [AvaloniaFact]
    public void Team_Filter_And_View_Switch()
    {
        var tracker = Own();
        var window = TestHelpers.Show(tracker);
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        tracker.VisibleGoals.Count.ShouldBe(2);
        tracker.TeamFilter = "Growth";
        tracker.VisibleGoals.Single().Title.ShouldBe("Grow");
        tracker.TeamSelect!.SelectedIndex.ShouldBe(2);
        tracker.View = GoalTrackerView.Timeline;
        tracker.TimelineView!.IsVisible.ShouldBeTrue();
        tracker.List!.IsVisible.ShouldBeFalse();
        tracker.TimelineView.Rows.Count.ShouldBe(1);
        tracker.ViewSwitch!.SelectedIndex.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_Width_Is_Compact()
    {
        var tracker = Own();
        var window = TestHelpers.Show(new Border { Width = 500, Child = tracker });
        Dispatcher.UIThread.RunJobs();
        tracker.IsCompact.ShouldBeTrue();
        window.Close();
    }
}
