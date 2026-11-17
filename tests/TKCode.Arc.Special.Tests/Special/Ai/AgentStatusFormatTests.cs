namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The number formats and the active-time clock of the agent status, against the measured values.</summary>
public class AgentStatusFormatTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(999, "0:00")]
    [InlineData(1000, "0:01")]
    [InlineData(10_000, "0:10")]
    [InlineData(85_000, "1:25")]
    [InlineData(140_900, "2:20")]
    [InlineData(3_727_000, "1:02:07")]
    public void Timer_Is_Whole_Seconds_Rounded_Down(double ms, string expected) =>
        AgentStatusFormat.Timer(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(142_940, "2m 23s")]
    [InlineData(13_400, "13s")]
    [InlineData(11_190, "11s")]
    [InlineData(14_450, "14s")]
    [InlineData(59_400, "59s")]
    [InlineData(60_000, "1m 0s")]
    [InlineData(7_000, "7s")]
    public void Total_Is_Rounded_To_Whole_Seconds(double ms, string expected) =>
        AgentStatusFormat.Total(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(142_940, "2 minutes 23 seconds")]
    [InlineData(14_450, "14 seconds")]
    [InlineData(1_000, "1 second")]
    [InlineData(60_000, "1 minute")]
    public void Spoken_Totals_Use_Whole_Words(double ms, string expected) =>
        AgentStatusFormat.TotalSpoken(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(1499, "1.5s")]
    [InlineData(1917, "1.9s")]
    [InlineData(2533, "2.5s")]
    [InlineData(3316, "3.3s")]
    [InlineData(1449, "1.4s")]
    [InlineData(40, "0.0s")]
    public void Step_Durations_Have_One_Decimal(double ms, string expected) =>
        AgentStatusFormat.StepDuration(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(1999, "1s")]
    [InlineData(129_000, "129s")]
    public void Live_Step_Times_Are_Whole_Seconds_Without_Minutes(double ms, string expected) =>
        AgentStatusFormat.LiveStepDuration(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Fact]
    public void Labels_Build_The_Measured_Sentences()
    {
        var labels = new AgentStatusLabels();
        labels.DoneIn("2m 23s", "5 files touched").ShouldBe("Done in 2m 23s · 5 files touched");
        labels.DoneIn("7s", null).ShouldBe("Done in 7s");
        labels.StoppedAfter("7s").ShouldBe("Stopped after 7s");
        labels.StepOf(3, 6).ShouldBe("Step 3 of 6");
        labels.StepsFinished(6, 6).ShouldBe("6 of 6 steps");
        labels.ProgressValueText(4, 6).ShouldBe("4 of 6 steps finished");
        labels.Count(8, 12).ShouldBe("8 of 12");
        labels.PillName("Atlas", "Checking specs 8 of 12", "0:09").ShouldBe("Atlas: Checking specs 8 of 12, 0:09 elapsed");
        labels.PillName("Atlas", "Ready", null).ShouldBe("Atlas: Ready");
        labels.Announcement("Atlas", "done in 14 seconds, 5 files touched", null).ShouldBe("Atlas: done in 14 seconds, 5 files touched");
        labels.Announcement("Atlas", "Paused", "Paused. Held.").ShouldBe("Atlas: Paused. Paused. Held.");
    }

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Run_Clock_Counts_Working_And_Waiting_Time()
    {
        var clock = new AgentStatusRunClock();
        clock.SetStartedAt(T0);
        clock.Observe(AgentStatusRunState.Working, T0);
        clock.Elapsed(T0.AddSeconds(10)).ShouldBe(TimeSpan.FromSeconds(10));
        clock.Observe(AgentStatusRunState.NeedsInput, T0.AddSeconds(10));
        clock.Elapsed(T0.AddSeconds(40)).ShouldBe(TimeSpan.FromSeconds(40));
    }

    [Fact]
    public void Run_Clock_Leaves_Out_Paused_And_Failed_Time()
    {
        var clock = new AgentStatusRunClock();
        clock.SetStartedAt(T0);
        clock.Observe(AgentStatusRunState.Working, T0);
        clock.Observe(AgentStatusRunState.Paused, T0.AddSeconds(10));
        clock.Elapsed(T0.AddSeconds(100)).ShouldBe(TimeSpan.FromSeconds(10));
        clock.Observe(AgentStatusRunState.Working, T0.AddSeconds(100));
        clock.Elapsed(T0.AddSeconds(105)).ShouldBe(TimeSpan.FromSeconds(15));
        clock.Observe(AgentStatusRunState.Failed, T0.AddSeconds(105));
        clock.Elapsed(T0.AddSeconds(500)).ShouldBe(TimeSpan.FromSeconds(15));
        clock.Observe(AgentStatusRunState.Working, T0.AddSeconds(500));
        clock.Elapsed(T0.AddSeconds(502)).ShouldBe(TimeSpan.FromSeconds(17));
    }

    [Fact]
    public void Run_Clock_Stops_At_The_End_And_Measures_Steps_By_Active_Time()
    {
        var clock = new AgentStatusRunClock();
        clock.SetStartedAt(T0);
        clock.Observe(AgentStatusRunState.Working, T0);
        clock.Observe(AgentStatusRunState.Paused, T0.AddSeconds(4));
        clock.Observe(AgentStatusRunState.Working, T0.AddSeconds(14));
        clock.Observe(AgentStatusRunState.Done, T0.AddSeconds(20));
        clock.Elapsed(T0.AddMinutes(9)).ShouldBe(TimeSpan.FromSeconds(10));
        clock.ActiveBetween(T0.AddSeconds(2), T0.AddSeconds(16)).ShouldBe(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public void Run_Clock_Starts_At_The_First_Live_State_Without_A_Given_Start()
    {
        var clock = new AgentStatusRunClock();
        clock.Elapsed(T0).ShouldBe(TimeSpan.Zero);
        clock.Observe(AgentStatusRunState.Idle, T0);
        clock.Observe(AgentStatusRunState.Working, T0.AddSeconds(3));
        clock.Elapsed(T0.AddSeconds(8)).ShouldBe(TimeSpan.FromSeconds(5));
    }
}
