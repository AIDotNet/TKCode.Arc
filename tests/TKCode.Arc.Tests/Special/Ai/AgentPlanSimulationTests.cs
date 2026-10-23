using Avalonia.Collections;
using Xunit;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The arithmetic, formats and the built-in simulated run of <see cref="AgentPlan"/> (no UI).</summary>
public class AgentPlanSimulationTests
{
    private sealed class FakeHost : IAgentPlanSimulationHost
    {
        public AvaloniaList<AgentPlanStep> Steps { get; } = new();
        public AgentPlanStatus Status { get; private set; }
        public bool Busy { get; private set; }
        public bool CanKeep { get; private set; } = true;
        public List<AgentPlanStatus> History { get; } = new();
        public void SetSimulatedStatus(AgentPlanStatus status) { Status = status; History.Add(status); }
        public void SetPlanning(bool busy, bool canKeepPlanning) { Busy = busy; CanKeep = canKeepPlanning; }
        public void InsertSuggestion(AgentPlanStep step, int index) => Steps.Insert(index, step);
    }

    private static (AgentPlanSimulation Sim, FakeHost Host, AgentPlanManualScheduler Clock) Start()
    {
        var clock = new AgentPlanManualScheduler();
        var sim = new AgentPlanSimulation(clock);
        var host = new FakeHost();
        sim.Attach(host);
        return (sim, host, clock);
    }

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(21, "21s")]
    [InlineData(59.4, "59s")]
    [InlineData(60, "1m 00s")]
    [InlineData(63, "1m 03s")]
    [InlineData(71, "1m 11s")]
    [InlineData(241, "4m 01s")]
    [InlineData(305, "5m 05s")]
    public void Durations_Use_Seconds_Then_Minutes_And_Two_Digit_Seconds(double seconds, string expected)
    {
        Assert.Equal(expected, AgentPlanFormat.Duration(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Total_Counts_Only_Done_Steps_And_Progress_Counts_Skipped()
    {
        var steps = new List<AgentPlanStep>
        {
            new("a", "A") { State = AgentPlanStepState.Done, Duration = TimeSpan.FromSeconds(21) },
            new("b", "B") { State = AgentPlanStepState.Skipped, Duration = TimeSpan.FromSeconds(99) },
            new("c", "C") { State = AgentPlanStepState.Done, Duration = TimeSpan.FromSeconds(48) },
            new("d", "D") { State = AgentPlanStepState.Failed },
            new("e", "E")
        };
        Assert.Equal(TimeSpan.FromSeconds(69), AgentPlanFormat.Total(steps));
        Assert.Equal(3, AgentPlanFormat.Finished(steps));
        Assert.Equal(0.6, AgentPlanFormat.Progress(steps), 6);
        Assert.Equal(4, AgentPlanFormat.Position(steps));
        Assert.Equal(3, AgentPlanFormat.FailedIndex(steps));
        Assert.Equal(1, AgentPlanFormat.Position(new List<AgentPlanStep>()));
    }

    [Fact]
    public void Position_Is_The_Count_When_Every_Step_Is_Finished()
    {
        var steps = new List<AgentPlanStep>
        {
            new("a", "A") { State = AgentPlanStepState.Done },
            new("b", "B") { State = AgentPlanStepState.Skipped }
        };
        Assert.Equal(2, AgentPlanFormat.Position(steps));
    }

    [Fact]
    public void Number_Splitting_Finds_The_Rolling_Parts()
    {
        var (before, number, after) = AgentPlanFormat.SplitOne("6 steps", 6);
        Assert.Equal(("", 6, " steps"), (before, number, after));
        var two = AgentPlanFormat.SplitTwo(new AgentPlanLabels().Position);
        Assert.NotNull(two);
        Assert.True(two.Value.FirstComesFirst);
        Assert.Equal(["", " of ", ""], two.Value.Words);
    }

    [Fact]
    public void The_Sample_Has_Six_Steps_Three_Suggestions_And_One_Failure()
    {
        Assert.Equal(6, AgentPlanSamples.Steps.Count);
        Assert.Equal(3, AgentPlanSamples.Suggestions.Count);
        Assert.Equal(1, AgentPlanSamples.Steps.Count(s => s.Script.FailureReason is not null));
        Assert.Single(AgentPlanSamples.Steps, s => s.Script.Inserts is not null);
    }

    [Fact]
    public void Attach_Fills_The_Sample_Draft_When_There_Are_No_Steps()
    {
        var (sim, host, _) = Start();
        Assert.Equal(6, host.Steps.Count);
        Assert.Equal(AgentPlanStatus.Draft, host.Status);
        Assert.True(sim.CanKeepPlanning);
        Assert.False(sim.IsWaiting);
    }

    [Fact]
    public void Approve_Starts_The_First_Step_After_280ms_And_Reports_Lines_On_The_Measured_Beats()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        Assert.Equal(AgentPlanStatus.Running, host.Status);
        Assert.All(host.Steps, s => Assert.Equal(AgentPlanStepState.Pending, s.State));
        clock.Advance(Ms(279));
        Assert.Equal(AgentPlanStepState.Pending, host.Steps[0].State);
        clock.Advance(Ms(1));
        Assert.Equal(AgentPlanStepState.Active, host.Steps[0].State);
        clock.Advance(Ms(371));
        Assert.Empty(host.Steps[0].Activities);
        clock.Advance(Ms(1));
        Assert.Single(host.Steps[0].Activities);
        clock.Advance(Ms(540));
        Assert.Equal(2, host.Steps[0].Activities.Count);
        clock.Advance(Ms(540));
        Assert.Equal(3, host.Steps[0].Activities.Count);
        clock.Advance(Ms(469));
        Assert.Equal(AgentPlanStepState.Active, host.Steps[0].State);
        clock.Advance(Ms(1));
        Assert.Equal(AgentPlanStepState.Done, host.Steps[0].State);
        Assert.Equal(TimeSpan.FromSeconds(23), host.Steps[0].Duration);
        // The next step starts 290ms later.
        clock.Advance(Ms(289));
        Assert.Equal(AgentPlanStepState.Pending, host.Steps[1].State);
        clock.Advance(Ms(1));
        Assert.Equal(AgentPlanStepState.Active, host.Steps[1].State);
    }

    private static void RunUntilFailure(FakeHost host, AgentPlanManualScheduler clock)
    {
        for (var i = 0; i < 400 && host.Steps.All(s => s.State != AgentPlanStepState.Failed); i++)
        {
            clock.RunNext();
        }
    }

    private static void RunUntilDone(FakeHost host, AgentPlanManualScheduler clock)
    {
        for (var i = 0; i < 600 && host.Status != AgentPlanStatus.Done; i++)
        {
            if (!clock.RunNext())
            {
                break;
            }
        }
    }

    [Fact]
    public void The_Agent_Inserts_A_Step_After_The_Third_Steps_Second_Line()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        while (host.Steps.Count == 6)
        {
            Assert.True(clock.RunNext());
        }
        Assert.Equal(7, host.Steps.Count);
        Assert.Equal("cache", host.Steps[2].Id);
        Assert.Equal(2, host.Steps[2].Activities.Count);
        Assert.Equal("fallback", host.Steps[3].Id);
        Assert.Equal(AgentPlanNoteKind.AddedByAgent, host.Steps[3].NoteKind);
        Assert.Equal(AgentPlanStepState.Pending, host.Steps[3].State);
        // The step's next line follows 578ms after the insert.
        clock.Advance(Ms(577));
        Assert.Equal(2, host.Steps[2].Activities.Count);
        clock.Advance(Ms(1));
        Assert.Equal(3, host.Steps[2].Activities.Count);
    }

    [Fact]
    public void The_Test_Step_Fails_Once_And_The_Run_Waits_For_A_Decision()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        RunUntilFailure(host, clock);
        var failed = host.Steps.Single(s => s.State == AgentPlanStepState.Failed);
        Assert.Equal("tests", failed.Id);
        Assert.Equal(2, failed.Activities.Count);
        Assert.NotNull(failed.Error);
        Assert.Equal(AgentPlanStatus.Running, host.Status);
        // No timer waits and nothing times out.
        Assert.False(sim.IsWaiting);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(AgentPlanStepState.Failed, failed.State);
        Assert.Equal(AgentPlanStatus.Running, host.Status);
    }

    [Fact]
    public void Skip_Moves_On_Counts_Skipped_As_Finished_And_Totals_Only_Done_Steps()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        RunUntilFailure(host, clock);
        var failed = host.Steps.Single(s => s.State == AgentPlanStepState.Failed);
        sim.OnStepAction(AgentPlanStepActionKind.Skip, failed);
        Assert.Equal(AgentPlanStepState.Skipped, failed.State);
        // The last step starts 277ms after Skip.
        clock.Advance(Ms(276));
        Assert.Equal(AgentPlanStepState.Pending, host.Steps[^1].State);
        clock.Advance(Ms(1));
        Assert.Equal(AgentPlanStepState.Active, host.Steps[^1].State);
        RunUntilDone(host, clock);
        Assert.Equal(AgentPlanStatus.Done, host.Status);
        // 23 + 46 + 35 + 31 + 26 + 18 = 179 s.
        Assert.Equal(TimeSpan.FromSeconds(179), AgentPlanFormat.Total(host.Steps));
        Assert.Equal("2m 59s", AgentPlanFormat.Duration(AgentPlanFormat.Total(host.Steps)));
        Assert.Equal(host.Steps.Count, AgentPlanFormat.Finished(host.Steps));
    }

    [Fact]
    public void Retry_Keeps_The_Lines_Adds_New_Ones_And_Finishes_With_The_Retry_Time()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        RunUntilFailure(host, clock);
        var failed = host.Steps.Single(s => s.State == AgentPlanStepState.Failed);
        sim.OnStepAction(AgentPlanStepActionKind.Retry, failed);
        Assert.Equal(AgentPlanStepState.Active, failed.State);
        Assert.Null(failed.Error);
        Assert.Equal(2, failed.Activities.Count);
        clock.Advance(Ms(554));
        Assert.Equal(2, failed.Activities.Count);
        clock.Advance(Ms(1));
        Assert.Equal(3, failed.Activities.Count);
        clock.Advance(Ms(577));
        Assert.Equal(4, failed.Activities.Count);
        clock.Advance(Ms(464));
        Assert.Equal(AgentPlanStepState.Active, failed.State);
        clock.Advance(Ms(1));
        Assert.Equal(AgentPlanStepState.Done, failed.State);
        Assert.Equal(TimeSpan.FromSeconds(67), failed.Duration);
        RunUntilDone(host, clock);
        // 23 + 46 + 35 + 31 + 26 + 67 + 18 = 246 s.
        Assert.Equal("4m 06s", AgentPlanFormat.Duration(AgentPlanFormat.Total(host.Steps)));
    }

    [Fact]
    public void Pause_Drops_The_Timer_And_Resume_Continues_Where_It_Stopped()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        clock.Advance(Ms(280 + 372 + 289));
        Assert.Single(host.Steps[0].Activities);
        sim.OnPauseChanged(true);
        Assert.Equal(AgentPlanStatus.Paused, host.Status);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Single(host.Steps[0].Activities);
        sim.OnPauseChanged(false);
        Assert.Equal(AgentPlanStatus.Running, host.Status);
        // 540 - 289 = 251ms remain until the second line.
        clock.Advance(Ms(250));
        Assert.Single(host.Steps[0].Activities);
        clock.Advance(Ms(1));
        Assert.Equal(2, host.Steps[0].Activities.Count);
    }

    [Fact]
    public void Pausing_Between_Steps_Leaves_The_Next_Step_Pending_Until_Resume()
    {
        var (sim, host, clock) = Start();
        sim.OnApproved();
        while (host.Steps[0].State != AgentPlanStepState.Done)
        {
            clock.RunNext();
        }
        sim.OnPauseChanged(true);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(AgentPlanStepState.Pending, host.Steps[1].State);
        Assert.Equal(2, AgentPlanFormat.Position(host.Steps));
        sim.OnPauseChanged(false);
        clock.Advance(Ms(291));
        Assert.Equal(AgentPlanStepState.Active, host.Steps[1].State);
    }

    [Fact]
    public void Keep_Planning_Streams_The_Next_Suggestion_In_Before_The_Last_Step_After_720ms()
    {
        var (sim, host, clock) = Start();
        sim.OnKeepPlanning();
        Assert.True(host.Busy);
        clock.Advance(Ms(719));
        Assert.Equal(6, host.Steps.Count);
        clock.Advance(Ms(1));
        Assert.Equal(7, host.Steps.Count);
        Assert.False(host.Busy);
        Assert.Equal("highlight", host.Steps[5].Id);
        Assert.Equal(AgentPlanNoteKind.Suggested, host.Steps[5].NoteKind);
        Assert.Equal("docs", host.Steps[6].Id);
        sim.OnKeepPlanning();
        clock.Advance(Ms(720));
        sim.OnKeepPlanning();
        clock.Advance(Ms(720));
        Assert.Equal(9, host.Steps.Count);
        Assert.False(host.CanKeep);
        Assert.False(sim.CanKeepPlanning);
        sim.OnKeepPlanning();
        Assert.False(host.Busy);
    }

    [Fact]
    public void Start_Over_Goes_Back_To_The_Original_Draft_And_Renews_The_Suggestions()
    {
        var (sim, host, clock) = Start();
        sim.OnKeepPlanning();
        clock.Advance(Ms(720));
        host.Steps[0].Title = "Renamed";
        sim.OnApproved();
        RunUntilFailure(host, clock);
        sim.OnStepAction(AgentPlanStepActionKind.Skip, host.Steps.Single(s => s.State == AgentPlanStepState.Failed));
        RunUntilDone(host, clock);
        sim.OnStartOver();
        Assert.Equal(AgentPlanStatus.Draft, host.Status);
        Assert.Equal(6, host.Steps.Count);
        Assert.Equal("Inventory the article fields the help center stores", host.Steps[0].Title);
        Assert.All(host.Steps, s => Assert.Equal(AgentPlanStepState.Pending, s.State));
        Assert.True(host.CanKeep);
        Assert.False(sim.IsWaiting);
    }

    [Fact]
    public void A_Step_Without_A_Script_Runs_The_Generic_One_And_A_Removed_Step_Is_Skipped_Over()
    {
        var (sim, host, clock) = Start();
        host.Steps.RemoveAt(1);
        host.Steps.Add(new AgentPlanStep(null, "Add a changelog entry"));
        sim.OnApproved();
        RunUntilFailure(host, clock);
        sim.OnStepAction(AgentPlanStepActionKind.Skip, host.Steps.Single(s => s.State == AgentPlanStepState.Failed));
        RunUntilDone(host, clock);
        var added = host.Steps[^1];
        Assert.Equal(3, added.Activities.Count);
        Assert.Equal(TimeSpan.FromSeconds(30), added.Duration);
        Assert.DoesNotContain(host.Steps, s => s.Id == "index");
    }

    [Fact]
    public void An_Idle_Plan_Has_No_Timer()
    {
        var (sim, _, clock) = Start();
        Assert.Equal(0, clock.PendingCount);
        sim.OnApproved();
        Assert.Equal(1, clock.PendingCount);
        sim.Detach();
        Assert.Equal(0, clock.PendingCount);
    }
}
