namespace TKCode.Arc.Tests.Special;

/// <summary>The agent loop's story timeline and trail rule, checked against the measured numbers (speed 1).</summary>
public class AgentLoopTimelineTests
{
    private static AgentLoopTimeline Sample() => AgentLoopTimeline.Build(
    [
        new AgentLoopShape(1, false),
        new AgentLoopShape(2, true),
        new AgentLoopShape(2, true)
    ]);

    [Fact]
    public void The_Sample_Shaped_Run_Takes_17_67_Seconds()
    {
        var timeline = Sample();
        timeline.End.ShouldBe(17.67, 0.0005);
        timeline.IsStopped.ShouldBeFalse();
        timeline.Count.ShouldBe(3);
    }

    [Fact]
    public void Loop_One_Matches_The_Measured_Beats()
    {
        var loop = Sample().Loops[0];
        loop.GatherAt.ShouldBe(0.22, 0.0005);
        loop.BlockAt.ShouldBe(0.335, 0.0005);
        loop.LeaveGather.ShouldBe(1.07, 0.0005);
        loop.ActAt.ShouldBe(1.77, 0.0005);
        loop.CallIn.ShouldHaveSingleItem().ShouldBe(1.87, 0.0005);
        loop.Result[0].ShouldBe(2.62, 0.0005);
        loop.LeaveAct.ShouldBe(3.12, 0.0005);
        loop.VerifyAt.ShouldBe(3.62, 0.0005);
        loop.CheckIn.ShouldBe(3.72, 0.0005);
        loop.LeaveVerify.ShouldBe(4.57, 0.0005);
    }

    [Fact]
    public void A_Second_Call_Comes_1_15_Seconds_After_The_First_And_Act_Stays_2_5_Seconds()
    {
        var loop = Sample().Loops[1];
        loop.GatherAt.ShouldBe(5.27, 0.0005);
        loop.ActAt.ShouldBe(6.82, 0.0005);
        loop.CallIn[1].ShouldBe(loop.CallIn[0] + 1.15, 0.0005);
        loop.Result[1].ShouldBe(loop.ActAt + 0.1 + 1.15 + 0.75, 0.0005);
        (loop.LeaveAct - loop.ActAt).ShouldBe(2.5, 0.0005);
        loop.VerifyAt.ShouldBe(9.82, 0.0005);
        Sample().Loops[2].GatherAt.ShouldBe(11.47, 0.0005);
    }

    [Fact]
    public void Act_Dwell_Is_1_35_Plus_1_15_Per_Extra_Call()
    {
        AgentLoopTimeline.ActDwell(1).ShouldBe(1.35, 1e-9);
        AgentLoopTimeline.ActDwell(2).ShouldBe(2.5, 1e-9);
        AgentLoopTimeline.ActDwell(3).ShouldBe(3.65, 1e-9);
        AgentLoopTimeline.ActDwell(5).ShouldBe(3.65, 1e-9);
        AgentLoopTimeline.ActDwell(0).ShouldBe(1.35, 1e-9);
    }

    [Fact]
    public void The_Sample_Run_Has_Twelve_Step_Stops()
    {
        var timeline = Sample();
        // Each gather, each result, each check, and the end.
        timeline.Stops.Count.ShouldBe(12);
        timeline.Stops[0].ShouldBe(0.72, 0.0005);
        timeline.Stops[1].ShouldBe(2.87, 0.0005);
        timeline.Stops[2].ShouldBe(4.12, 0.0005);
        timeline.Stops[^1].ShouldBe(timeline.End);
        for (var i = 1; i < timeline.Stops.Count; i++)
        {
            timeline.Stops[i].ShouldBeGreaterThan(timeline.Stops[i - 1]);
        }
    }

    [Fact]
    public void Travel_Takes_0_7_0_5_And_0_7_Seconds()
    {
        var travels = Sample().Travels;
        travels.Count.ShouldBe(9);
        travels[0].ShouldBe(new AgentLoopTravel(1.07, 0.7, AgentLoopStation.Gather, AgentLoopStation.Act), new TravelComparer());
        travels[1].Duration.ShouldBe(0.5);
        travels[1].From.ShouldBe(AgentLoopStation.Act);
        travels[2].Duration.ShouldBe(0.7);
        travels[2].To.ShouldBe(AgentLoopStation.Gather);
        travels[^1].End.ShouldBe(17.67, 0.0005);
    }

    [Fact]
    public void The_Head_Rests_At_Stations_And_Travels_Between_Them()
    {
        var timeline = Sample();
        timeline.HeadAt(0).ShouldBe(new AgentLoopHead(AgentLoopStation.Gather, AgentLoopStation.Gather, 0, false));
        timeline.HeadAt(1.0).IsTravelling.ShouldBeFalse();
        var mid = timeline.HeadAt(1.07 + 0.35);
        mid.IsTravelling.ShouldBeTrue();
        mid.From.ShouldBe(AgentLoopStation.Gather);
        mid.To.ShouldBe(AgentLoopStation.Act);
        mid.Progress.ShouldBe(0.5, 1e-6);
        timeline.HeadAt(2.0).From.ShouldBe(AgentLoopStation.Act);
        timeline.HeadAt(20).ShouldBe(new AgentLoopHead(AgentLoopStation.Gather, AgentLoopStation.Gather, 0, false));
    }

    [Fact]
    public void A_Failed_Last_Check_Ends_Stopped()
    {
        var timeline = AgentLoopTimeline.Build([new AgentLoopShape(1, true), new AgentLoopShape(1, false)]);
        timeline.IsStopped.ShouldBeTrue();
        timeline.End.ShouldBe(0.22 + 2 * (3.7 + 1.35), 0.0005);
    }

    [Fact]
    public void No_Loops_Make_An_Empty_Story()
    {
        var timeline = AgentLoopTimeline.Build([]);
        timeline.Count.ShouldBe(0);
        timeline.End.ShouldBe(0);
        timeline.Stops.ShouldBeEmpty();
        timeline.HeadAt(3).IsTravelling.ShouldBeFalse();
    }

    [Fact]
    public void The_Trail_Gap_Follows_The_Measured_Sine()
    {
        // 120° trip: peaks at 72° halfway; p = 0.164 gives about 35.5°.
        AgentLoopTimeline.TrailGap(120, 0.5).ShouldBe(72, 1e-9);
        AgentLoopTimeline.TrailGap(120, 0.164).ShouldBe(35.5, 0.2);
        AgentLoopTimeline.TrailGap(120, 0.3).ShouldBe(58.3, 0.1);
        // 140° trips are capped at 72°; 80° trips at 0.7 × 80 = 56°.
        AgentLoopTimeline.TrailGap(140, 0.5).ShouldBe(72, 1e-9);
        AgentLoopTimeline.TrailGap(80, 0.5).ShouldBe(56, 1e-9);
        // No trail at rest.
        AgentLoopTimeline.TrailGap(120, 0).ShouldBe(0);
        AgentLoopTimeline.TrailGap(120, 1).ShouldBe(0);
    }

    [Fact]
    public void The_Head_Eases_In_And_Out_Cubically()
    {
        AgentLoopTimeline.EaseInOutCubic(0).ShouldBe(0);
        AgentLoopTimeline.EaseInOutCubic(0.25).ShouldBe(0.0625, 1e-9);
        AgentLoopTimeline.EaseInOutCubic(0.5).ShouldBe(0.5, 1e-9);
        AgentLoopTimeline.EaseInOutCubic(0.75).ShouldBe(0.9375, 1e-9);
        AgentLoopTimeline.EaseInOutCubic(1).ShouldBe(1);
    }

    private sealed class TravelComparer : IEqualityComparer<AgentLoopTravel>
    {
        public bool Equals(AgentLoopTravel x, AgentLoopTravel y) =>
            Math.Abs(x.Start - y.Start) < 0.0005 && Math.Abs(x.Duration - y.Duration) < 0.0005 && x.From == y.From && x.To == y.To;

        public int GetHashCode(AgentLoopTravel obj) => 0;
    }
}
