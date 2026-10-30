namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The scripted runs behind the preview: their beats, waits, actions and numbers, on a manual clock.</summary>
public class AgentRunSimulationTests
{
    private static (AgentRunSimulation Sim, AgentStatusTestClock Clock) Start(AgentRunScenario scenario)
    {
        var clock = new AgentStatusTestClock();
        var sim = new AgentRunSimulation(scenario, clock);
        sim.Start();
        return (sim, clock);
    }

    private static AgentStatusStep Step(AgentRunSimulation sim, int index) => sim.Steps[index];

    [Fact]
    public void Starts_After_The_Start_Delay_With_Every_Step_Listed()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        sim.Status.ShouldBe(AgentStatusRunState.Idle);
        sim.Steps.Count.ShouldBe(0);
        clock.Advance(899);
        sim.Status.ShouldBe(AgentStatusRunState.Idle);
        clock.Advance(1);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Steps.Count.ShouldBe(6);
        Step(sim, 0).State.ShouldBe(AgentStatusStepState.Active);
        Step(sim, 1).State.ShouldBe(AgentStatusStepState.Pending);
        sim.Activity!.Label.ShouldBe("Scanning 9 modules");
        sim.Activity.HasProgress.ShouldBeFalse();
        sim.StartedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(1916, 0, "Scanning 9 modules")]
    [InlineData(1917, 1, "Looking up parseDueDate")]
    [InlineData(3415, 1, "Looking up parseDueDate")]
    [InlineData(3416, 2, "Patching lib/dates/parse-due.ts")]
    [InlineData(5949, 3, "Adjusting 4 test suites")]
    [InlineData(7866, 4, "Checking specs")]
    public void The_Coding_Run_Moves_On_At_The_Measured_Times(double after, int step, string label)
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900);
        clock.Advance(after);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        Step(sim, step).State.ShouldBe(AgentStatusStepState.Active);
        sim.Activity!.Label.ShouldBe(label);
    }

    [Fact]
    public void Finished_Steps_Show_Their_Past_Tense_And_Duration()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 1917);
        Step(sim, 0).State.ShouldBe(AgentStatusStepState.Done);
        Step(sim, 0).Label.ShouldBe("Scanned 9 modules");
        AgentStatusFormat.StepDuration(Step(sim, 0).Duration!.Value).ShouldBe("1.9s");
        clock.Advance(1499);
        AgentStatusFormat.StepDuration(Step(sim, 1).Duration!.Value).ShouldBe("1.5s");
    }

    [Fact]
    public void The_Count_Ticks_In_Place_Every_279_Milliseconds()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 7866);
        sim.Activity!.ProgressValue.ShouldBe(1);
        sim.Activity.ProgressTotal.ShouldBe(12);
        var seen = new List<int>();
        for (var i = 0; i < 3316; i++)
        {
            clock.Advance(1);
            if (sim.Activity?.ProgressValue is { } v && (seen.Count == 0 || seen[^1] != v))
            {
                seen.Add(v);
            }
            if (sim.Status != AgentStatusRunState.Working)
            {
                break;
            }
        }
        seen.ShouldBe(Enumerable.Range(1, 12).ToList());
        sim.Activity?.Label.ShouldNotBeNull();
    }

    [Fact]
    public void It_Waits_Forever_For_An_Approval_While_The_Timer_Would_Keep_Running()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 11182);
        sim.Status.ShouldBe(AgentStatusRunState.NeedsInput);
        sim.Activity!.Label.ShouldBe("Waiting on you to deploy to staging");
        Step(sim, 4).State.ShouldBe(AgentStatusStepState.Done);
        AgentStatusFormat.StepDuration(Step(sim, 4).Duration!.Value).ShouldBe("3.3s");
        Step(sim, 5).State.ShouldBe(AgentStatusStepState.Waiting);
        Step(sim, 5).Label.ShouldBe("Approval needed to deploy to staging");
        clock.Advance(TimeSpan.FromMinutes(30));
        sim.Status.ShouldBe(AgentStatusRunState.NeedsInput);
        sim.EndedAt.ShouldBeNull();
    }

    [Fact]
    public void Approve_Deploys_For_1440_Milliseconds_Then_The_Run_Is_Done()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 11182 + 5000);
        sim.Approve();
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Activity!.Label.ShouldBe("Deploying build 2.4.1");
        sim.Notice.ShouldBe("Approved. The deploy is only simulated here.");
        clock.Advance(1439);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        clock.Advance(1);
        sim.Status.ShouldBe(AgentStatusRunState.Done);
        sim.Activity!.Label.ShouldBe("5 files touched");
        Step(sim, 5).State.ShouldBe(AgentStatusStepState.Done);
        Step(sim, 5).Label.ShouldBe("Deployed build 2.4.1");
        AgentStatusFormat.StepDuration(Step(sim, 5).Duration!.Value).ShouldBe("1.4s");
        sim.EndedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Deny_Skips_The_Step_And_Finishes()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 11182);
        sim.Deny();
        sim.Status.ShouldBe(AgentStatusRunState.Done);
        Step(sim, 5).State.ShouldBe(AgentStatusStepState.Skipped);
        Step(sim, 5).Label.ShouldBe("Deploy skipped");
        Step(sim, 5).Duration.ShouldBeNull();
        sim.Activity!.Label.ShouldBe("5 files touched, not deployed");
        sim.Notice.ShouldBe("Denied. Nothing was deployed.");
    }

    [Fact]
    public void Pause_Freezes_The_Script_And_Resume_Continues_From_The_Same_Point()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 3416 + 584);
        sim.Pause();
        sim.Status.ShouldBe(AgentStatusRunState.Paused);
        clock.Advance(TimeSpan.FromSeconds(20));
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Active);
        sim.Resume();
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        clock.Advance(2532 - 584);
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Active);
        clock.Advance(1);
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Done);
        Step(sim, 3).State.ShouldBe(AgentStatusStepState.Active);
    }

    [Fact]
    public void Stop_Cancels_The_Running_Step()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Research);
        clock.Advance(900 + 1433 + 1917 + 500);
        sim.Stop();
        sim.Status.ShouldBe(AgentStatusRunState.Stopped);
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Skipped);
        Step(sim, 2).Label.ShouldBe("Cancelled at your request");
        Step(sim, 3).State.ShouldBe(AgentStatusStepState.Pending);
        sim.Activity.ShouldBeNull();
        clock.Advance(TimeSpan.FromMinutes(1));
        sim.Status.ShouldBe(AgentStatusRunState.Stopped);
    }

    [Fact]
    public void The_Research_Run_Fails_Once_And_Retry_Continues_The_Count()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Research);
        clock.Advance(900 + 1433 + 1917);
        sim.Activity!.Label.ShouldBe("Opening pages");
        sim.Activity.ProgressValue.ShouldBe(1);
        clock.Advance(1814);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Activity!.ProgressValue.ShouldBe(5);
        clock.Advance(1);
        sim.Status.ShouldBe(AgentStatusRunState.Failed);
        sim.Activity!.Label.ShouldBe("Fetch failed · 2 pages did not load");
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Failed);
        Step(sim, 2).Label.ShouldBe("2 pages did not load");
        clock.Advance(TimeSpan.FromMinutes(5));
        sim.Status.ShouldBe(AgentStatusRunState.Failed);

        sim.Retry();
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Activity!.ProgressValue.ShouldBe(5);
        sim.Activity.Label.ShouldBe("Opening pages");
        clock.Advance(73);
        sim.Activity!.ProgressValue.ShouldBe(6);
        clock.Advance(1456 - 73 - 1);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Activity!.ProgressValue.ShouldBe(9);
        clock.Advance(1);
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Done);
        AgentStatusFormat.StepDuration(Step(sim, 2).Duration!.Value).ShouldBe("3.3s");
        Step(sim, 3).State.ShouldBe(AgentStatusStepState.Active);
        clock.Advance(2217 + 2350);
        sim.Status.ShouldBe(AgentStatusRunState.Done);
        sim.Activity!.Label.ShouldBe("8 pages cited");
    }

    [Fact]
    public void Open_Run_Only_Says_So_And_Changes_Nothing()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(2000);
        var steps = sim.Steps.Select(s => s.State).ToList();
        sim.OpenRun();
        sim.Notice.ShouldBe("The run log would open here; this preview only simulates it.");
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Steps.Select(s => s.State).ShouldBe(steps);
    }

    [Fact]
    public void Replay_Clears_The_Run_And_Starts_After_Half_A_Second()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 4000);
        sim.Replay();
        sim.Status.ShouldBe(AgentStatusRunState.Idle);
        sim.Steps.Count.ShouldBe(0);
        clock.Advance(499);
        sim.Status.ShouldBe(AgentStatusRunState.Idle);
        clock.Advance(1);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Steps.Count.ShouldBe(6);
    }

    [Fact]
    public void Changing_The_Scenario_Starts_Over_After_The_Start_Delay()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 4000);
        sim.Scenario = AgentStatusTestRuns.Research;
        sim.Status.ShouldBe(AgentStatusRunState.Idle);
        sim.Agent.Name.ShouldBe("Atlas Scout 2");
        clock.Advance(900);
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Activity!.Label.ShouldBe("Drafting 3 sub-questions");
        sim.Scenario = AgentStatusTestRuns.Research;
        sim.Status.ShouldBe(AgentStatusRunState.Working);
    }

    [Fact]
    public void A_Late_Timer_Does_Not_Drift_The_Script()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900);
        clock.Advance(1917 + 1499 + 400);
        Step(sim, 1).State.ShouldBe(AgentStatusStepState.Done);
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Active);
        clock.Advance(2533 - 400);
        Step(sim, 2).State.ShouldBe(AgentStatusStepState.Done);
    }

    [Fact]
    public void Actions_In_The_Wrong_State_Do_Nothing()
    {
        var (sim, clock) = Start(AgentStatusTestRuns.Coding);
        clock.Advance(900 + 1000);
        sim.Approve();
        sim.Deny();
        sim.Retry();
        sim.Resume();
        sim.Status.ShouldBe(AgentStatusRunState.Working);
        sim.Pause();
        sim.Pause();
        sim.Status.ShouldBe(AgentStatusRunState.Paused);
    }
}
