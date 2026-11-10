using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary><see cref="TaskBreakdown"/>: the built-in suggester, a custom suggester and its failure, totals, removing and creating.</summary>
public class TaskBreakdownTests
{
    [AvaloniaFact]
    public void Suggester_Splits_Estimates_Adds_Review_And_Balances_Load()
    {
        var people = new[] { new ProjectPerson("A"), new ProjectPerson("B") };
        var steps = TaskBreakdown.SuggestFromText("Launch", "- Design the page\n- Build the form. Test the form then release it", people);
        steps.Select(s => s.Title).ShouldBe(["Design the page", "Build the form", "Test the form", "Release it", "Review and hand off"]);
        steps.Select(s => s.EstimateHours).ShouldBe([4, 6, 3, 2, 1]);
        steps.All(s => s.IsSuggested).ShouldBeTrue();
        // The lighter person gets the next step: A design, B build, A test, B release, A review.
        steps[0].Assignee.ShouldBe(people[0]);
        steps[1].Assignee.ShouldBe(people[1]);
        TaskBreakdown.SuggestFromText("Write the docs", "", null).Count.ShouldBe(5);
        TaskBreakdown.SuggestFromText("x", "We need to review the plan.").Select(s => s.Title).ShouldBe(["Review the plan"]);
    }

    [AvaloniaFact]
    public void Samples_Load_Once_And_Totals_Follow_The_List()
    {
        var breakdown = new TaskBreakdown();
        var window = TestHelpers.Show(breakdown);
        window.Width = 900;
        Dispatcher.UIThread.RunJobs();
        breakdown.People.Count.ShouldBe(3);
        breakdown.Subtasks.Count.ShouldBeGreaterThan(3);
        breakdown.TotalHours.ShouldBe(breakdown.Subtasks.Sum(s => s.EstimateHours));
        var before = breakdown.TotalHours;
        breakdown.Subtasks.RemoveAt(0);
        breakdown.TotalHours.ShouldBeLessThan(before);
        breakdown.CreateButton!.Content.ShouldBe($"Create {breakdown.Subtasks.Count} subtasks");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Custom_Suggester_Replaces_The_List_And_Failure_Keeps_It()
    {
        var breakdown = new TaskBreakdown { IsSampleDataEnabled = false, TaskTitle = "T", Description = "D" };
        var window = TestHelpers.Show(breakdown);
        Dispatcher.UIThread.RunJobs();
        TaskBreakdownRequest? seen = null;
        breakdown.SuggestAction = (request, _) =>
        {
            seen = request;
            return Task.FromResult<IReadOnlyList<TaskBreakdownSubtask>>([new TaskBreakdownSubtask("One", 1), new TaskBreakdownSubtask("Two", 2)]);
        };
        (await breakdown.SuggestAsync()).ShouldBeTrue();
        seen!.Title.ShouldBe("T");
        breakdown.Subtasks.Select(s => s.Title).ShouldBe(["One", "Two"]);
        breakdown.TotalHours.ShouldBe(3);
        breakdown.Announcer!.Announcement.ShouldBe("2 subtasks suggested");

        breakdown.SuggestAction = (_, _) => throw new InvalidOperationException("down");
        (await breakdown.SuggestAsync()).ShouldBeFalse();
        breakdown.Subtasks.Count.ShouldBe(2);
        breakdown.Failure!.IsVisible.ShouldBeTrue();
        breakdown.IsSuggesting.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Create_Skips_Empty_Titles_And_Raises()
    {
        var breakdown = new TaskBreakdown { IsSampleDataEnabled = false, TaskTitle = "Ship" };
        breakdown.Subtasks.Add(new TaskBreakdownSubtask("Build", 4));
        breakdown.Subtasks.Add(new TaskBreakdownSubtask(" ", 1));
        var events = new List<TaskBreakdownCreatedEventArgs>();
        breakdown.Created += (_, e) => events.Add(e);
        var window = TestHelpers.Show(breakdown);
        Dispatcher.UIThread.RunJobs();
        breakdown.Create().Count.ShouldBe(1);
        events.Single().Title.ShouldBe("Ship");
        events.Single().Subtasks.Single().Title.ShouldBe("Build");
        breakdown.AddSubtask();
        breakdown.Subtasks.Count.ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_Width_Is_Compact()
    {
        var breakdown = new TaskBreakdown();
        var window = TestHelpers.Show(new Border { Width = 480, Child = breakdown });
        Dispatcher.UIThread.RunJobs();
        breakdown.IsCompact.ShouldBeTrue();
        window.Close();
    }
}
