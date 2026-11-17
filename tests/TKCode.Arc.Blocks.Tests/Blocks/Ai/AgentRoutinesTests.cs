using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AgentRoutines"/> and <see cref="AgentRoutineSchedule"/>: schedules, rows, switching, Run now and history.</summary>
public class AgentRoutinesTests
{
    // A Monday.
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Theory]
    [InlineData("0 8 * * *", "Every day at 08:00")]
    [InlineData("30 8 * * 1-5", "Weekdays at 08:30")]
    [InlineData("0 10 * * 0,6", "Weekends at 10:00")]
    [InlineData("0 7 * * 1", "Every Monday at 07:00")]
    [InlineData("0 18 * * 1,3,5", "Mon, Wed and Fri at 18:00")]
    [InlineData("0 6 1 * *", "Monthly on day 1 at 06:00")]
    [InlineData("15 * * * *", "Every hour at :15")]
    [InlineData("0 */4 * * *", "Every 4 hours at :00")]
    [InlineData("*/15 * * * *", "Every 15 minutes")]
    [InlineData("0 9 * 3 *", "0 9 * 3 *")]
    [InlineData("nonsense", "nonsense")]
    public void Describe_Puts_Common_Schedules_In_Words(string cron, string expected) =>
        AgentRoutineSchedule.Describe(cron).ShouldBe(expected);

    [Fact]
    public void Next_Finds_The_Following_Run()
    {
        AgentRoutineSchedule.Next("0 8 * * 1-5", Now).ShouldBe(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero));
        AgentRoutineSchedule.Next("30 10 * * *", Now).ShouldBe(new DateTimeOffset(2026, 11, 9, 10, 30, 0, TimeSpan.Zero));
        AgentRoutineSchedule.Next("0 10 * * *", Now).ShouldBe(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero));
        AgentRoutineSchedule.Next("0 9 * * 6", Now).ShouldBe(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero));
        AgentRoutineSchedule.Next("0 0 1 1 *", Now).ShouldBe(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        // Classic cron: day of month or day of week.
        AgentRoutineSchedule.Next("0 12 13 * 3", Now).ShouldBe(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        AgentRoutineSchedule.Next("0 0 30 2 *", Now).ShouldBeNull();
        AgentRoutineSchedule.Next("61 * * * *", Now).ShouldBeNull();
        AgentRoutineSchedule.IsValid("0 9 * * 7").ShouldBeTrue();
        AgentRoutineSchedule.IsValid("0 9 * *").ShouldBeFalse();
    }

    private static (AgentRoutines List, IReadOnlyList<AgentRoutine> Routines, Window Window) Show(double width = 720)
    {
        var routines = AgentRoutinesSamples.Routines(Now);
        var list = new AgentRoutines { Width = width, Routines = routines, Clock = () => Now };
        var window = TestHelpers.Show(list);
        Settle(window);
        return (list, routines, window);
    }

    [AvaloniaFact]
    public void Rows_Show_Schedule_Next_Run_And_History()
    {
        var (list, routines, _) = Show();
        var item = list.ItemOf(routines[0])!;
        item.Header!.ScheduleText.ShouldBe("Weekdays at 08:00");
        item.Header.StatusText.ShouldBe("Next in 22h");
        item.History!.Outcomes.Count.ShouldBe(10);
        item.History.Outcomes[^1].ShouldBe(AgentRoutineOutcome.Succeeded);
        item.History.Outcomes[^2].ShouldBe(AgentRoutineOutcome.Failed);
        AutomationProperties.GetName(item.History).ShouldBe("9 of 10 recent runs succeeded");
        list.ItemOf(routines[3])!.Header!.StatusText.ShouldBe("Paused");
    }

    [AvaloniaFact]
    public void Switch_Pauses_And_Resumes_With_An_Event()
    {
        var (list, routines, window) = Show();
        var events = new List<bool>();
        list.EnabledChanged += (_, e) => events.Add(e.Routine.IsEnabled);
        var item = list.ItemOf(routines[0])!;
        item.EnableSwitch!.IsChecked = false;
        Settle(window);
        routines[0].IsEnabled.ShouldBeFalse();
        events.ShouldBe([false]);
        list.ItemOf(routines[0])!.Header!.StatusText.ShouldBe("Paused");
    }

    [AvaloniaFact]
    public async Task Run_Now_Adds_The_Run_In_Front()
    {
        var (list, routines, _) = Show();
        list.Delay = (_, _) => Task.CompletedTask;
        AgentRoutineEventArgs? completed = null;
        list.RunCompleted += (_, e) => completed = e;
        var count = routines[2].Runs!.Count;
        await list.RunNowAsync(routines[2]);
        routines[2].Runs!.Count.ShouldBe(count + 1);
        routines[2].Runs![0].Outcome.ShouldBe(AgentRoutineOutcome.Succeeded);
        completed!.Run.ShouldBe(routines[2].Runs![0]);
        list.Announcer!.Announcement.ShouldBe("Weekly spend report: Succeeded");
    }

    [AvaloniaFact]
    public async Task A_Throwing_Handler_Records_A_Failed_Run()
    {
        var (list, routines, _) = Show();
        list.RunHandler = (_, _) => throw new InvalidOperationException("inbox offline");
        await list.RunNowAsync(routines[0]);
        routines[0].Runs![0].Outcome.ShouldBe(AgentRoutineOutcome.Failed);
        routines[0].Runs![0].Summary.ShouldBe("inbox offline");
    }

    [AvaloniaFact]
    public void Opening_A_Row_Shows_Instructions_And_Runs()
    {
        var (list, routines, window) = Show();
        var item = list.ItemOf(routines[1])!;
        item.IsExpanded = true;
        Settle(window);
        item.Header!.IsExpanded.ShouldBeTrue();
        item.Classes.ShouldContain(":expanded");
    }

    [AvaloniaFact]
    public void Narrow_List_Is_Compact()
    {
        var (list, routines, _) = Show(480);
        list.IsCompact.ShouldBeTrue();
        list.ItemOf(routines[0])!.IsCompact.ShouldBeTrue();
    }
}
