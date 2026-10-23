namespace TKCode.Arc;

/// <summary>The shape of one loop as far as timing is concerned: how many calls it makes and whether its check passes.</summary>
internal readonly record struct AgentLoopShape(int Calls, bool Passed);

/// <summary>The beat times of one loop, in seconds of story time at speed 1.</summary>
internal sealed class AgentLoopLoopTimes
{
    /// <summary>The head reaches Gather (the station turns active and the counter rolls).</summary>
    public double GatherAt { get; init; }

    /// <summary>The context block joins the stack and the token total starts counting.</summary>
    public double BlockAt { get; init; }

    /// <summary>The head leaves Gather.</summary>
    public double LeaveGather { get; init; }

    /// <summary>The head reaches Take action.</summary>
    public double ActAt { get; init; }

    /// <summary>When each call's chip drops into the lane (running).</summary>
    public required double[] CallIn { get; init; }

    /// <summary>When each call resolves.</summary>
    public required double[] Result { get; init; }

    /// <summary>The head leaves Take action.</summary>
    public double LeaveAct { get; init; }

    /// <summary>The head reaches Verify.</summary>
    public double VerifyAt { get; init; }

    /// <summary>The check's chip drops into its lane.</summary>
    public double CheckIn { get; init; }

    /// <summary>The head leaves Verify.</summary>
    public double LeaveVerify { get; init; }

    /// <summary>Where a step halts after the block joined.</summary>
    public double GatherStop => GatherAt + AgentLoopTimeline.GatherStopDelay;

    /// <summary>Where a step halts after the check.</summary>
    public double VerifyStop => VerifyAt + AgentLoopTimeline.VerifyStopDelay;

    /// <summary>Where a step halts after call <paramref name="call"/> resolved.</summary>
    public double ResultStop(int call) => Result[call] + AgentLoopTimeline.ResultStopDelay;
}

/// <summary>One trip of the head between two stations.</summary>
internal readonly record struct AgentLoopTravel(double Start, double Duration, AgentLoopStation From, AgentLoopStation To)
{
    /// <summary>When the head lands.</summary>
    public double End => Start + Duration;
}

/// <summary>Where the head is at a story time: resting at a station, or between two with a linear progress.</summary>
internal readonly record struct AgentLoopHead(AgentLoopStation From, AgentLoopStation To, double Progress, bool IsTravelling);

/// <summary>
/// The agent loop's story as a pure function of its iterations (measured at speed 1, in seconds): a 0.22 s lead, then
/// per loop Gather 0.85, travel 0.7, Take action 1.35 + 1.15 per extra call, travel 0.5, Verify 0.95, travel 0.7; the run
/// ends when the head lands back on Gather after the last loop. Built once when the data changes, so playing, pausing,
/// stepping and replaying are exact and repeatable.
/// </summary>
internal sealed class AgentLoopTimeline
{
    /// <summary>From Ready to the first Gather.</summary>
    public const double Lead = 0.22;

    /// <summary>The time at Gather.</summary>
    public const double GatherDwell = 0.85;

    /// <summary>The block joins this long after the head reaches Gather.</summary>
    public const double BlockDelay = 0.115;

    /// <summary>A call's chip drops this long after the head reaches Take action (or after the previous call's slot).</summary>
    public const double CallDelay = 0.1;

    /// <summary>The slot of each call after the first.</summary>
    public const double CallSpacing = 1.15;

    /// <summary>From a call's chip dropping to its result.</summary>
    public const double CallDuration = 0.75;

    /// <summary>The head stays this long after the last result.</summary>
    public const double AfterLastResult = 0.5;

    /// <summary>The time at Verify.</summary>
    public const double VerifyDwell = 0.95;

    /// <summary>The check drops this long after the head reaches Verify.</summary>
    public const double CheckDelay = 0.1;

    /// <summary>Travel Gather → Take action.</summary>
    public const double GatherToAct = 0.7;

    /// <summary>Travel Take action → Verify.</summary>
    public const double ActToVerify = 0.5;

    /// <summary>Travel Verify → Gather.</summary>
    public const double VerifyToGather = 0.7;

    /// <summary>A step halts this long after the head reaches Gather.</summary>
    public const double GatherStopDelay = 0.5;

    /// <summary>A step halts this long after a result.</summary>
    public const double ResultStopDelay = 0.25;

    /// <summary>A step halts this long after the head reaches Verify.</summary>
    public const double VerifyStopDelay = 0.5;

    /// <summary>The most calls a loop shows.</summary>
    public const int MaxCalls = 3;

    private readonly List<AgentLoopLoopTimes> _loops = new();
    private readonly List<AgentLoopTravel> _travels = new();
    private readonly List<double> _stops = new();

    private AgentLoopTimeline()
    {
    }

    /// <summary>The loops' beat times.</summary>
    public IReadOnlyList<AgentLoopLoopTimes> Loops => _loops;

    /// <summary>Every trip of the head, in order.</summary>
    public IReadOnlyList<AgentLoopTravel> Travels => _travels;

    /// <summary>Every step stop, in order (each gather, each result, each check, the end).</summary>
    public IReadOnlyList<double> Stops => _stops;

    /// <summary>When the run ends (the head is back on Gather after the last loop); 0 with no loops.</summary>
    public double End { get; private set; }

    /// <summary>True when the last loop's check fails, so the run ends stopped rather than done.</summary>
    public bool IsStopped { get; private set; }

    /// <summary>The number of loops.</summary>
    public int Count => _loops.Count;

    /// <summary>The time the head spends at Take action for <paramref name="calls"/> calls (at least one call's time).</summary>
    public static double ActDwell(int calls) => 1.35 + CallSpacing * (Math.Clamp(calls, 1, MaxCalls) - 1);

    /// <summary>Builds the story of <paramref name="shapes"/>.</summary>
    public static AgentLoopTimeline Build(IReadOnlyList<AgentLoopShape> shapes)
    {
        var timeline = new AgentLoopTimeline();
        if (shapes.Count == 0)
        {
            return timeline;
        }
        var t = Lead;
        foreach (var shape in shapes)
        {
            var calls = Math.Clamp(shape.Calls, 0, MaxCalls);
            var gatherAt = t;
            var leaveGather = gatherAt + GatherDwell;
            var actAt = leaveGather + GatherToAct;
            var callIn = new double[calls];
            var result = new double[calls];
            for (var k = 0; k < calls; k++)
            {
                callIn[k] = actAt + CallDelay + CallSpacing * k;
                result[k] = callIn[k] + CallDuration;
            }
            var leaveAct = actAt + ActDwell(calls);
            var verifyAt = leaveAct + ActToVerify;
            var leaveVerify = verifyAt + VerifyDwell;
            var times = new AgentLoopLoopTimes
            {
                GatherAt = gatherAt,
                BlockAt = gatherAt + BlockDelay,
                LeaveGather = leaveGather,
                ActAt = actAt,
                CallIn = callIn,
                Result = result,
                LeaveAct = leaveAct,
                VerifyAt = verifyAt,
                CheckIn = verifyAt + CheckDelay,
                LeaveVerify = leaveVerify
            };
            timeline._loops.Add(times);
            timeline._travels.Add(new AgentLoopTravel(leaveGather, GatherToAct, AgentLoopStation.Gather, AgentLoopStation.Act));
            timeline._travels.Add(new AgentLoopTravel(leaveAct, ActToVerify, AgentLoopStation.Act, AgentLoopStation.Verify));
            timeline._travels.Add(new AgentLoopTravel(leaveVerify, VerifyToGather, AgentLoopStation.Verify, AgentLoopStation.Gather));
            timeline._stops.Add(times.GatherStop);
            for (var k = 0; k < calls; k++)
            {
                timeline._stops.Add(times.ResultStop(k));
            }
            timeline._stops.Add(times.VerifyStop);
            t = leaveVerify + VerifyToGather;
        }
        timeline.End = t;
        timeline._stops.Add(t);
        timeline.IsStopped = !shapes[^1].Passed;
        return timeline;
    }

    /// <summary>Where the head is at story time <paramref name="time"/>.</summary>
    public AgentLoopHead HeadAt(double time)
    {
        // The trips are few (three per loop) and ordered: a linear scan is cheap and allocation free.
        var at = AgentLoopStation.Gather;
        for (var i = 0; i < _travels.Count; i++)
        {
            var travel = _travels[i];
            if (time < travel.Start)
            {
                return new AgentLoopHead(at, at, 0, false);
            }
            if (time < travel.End)
            {
                return new AgentLoopHead(travel.From, travel.To, (time - travel.Start) / travel.Duration, true);
            }
            at = travel.To;
        }
        return new AgentLoopHead(at, at, 0, false);
    }

    /// <summary>The easeInOutCubic curve the head travels on.</summary>
    public static double EaseInOutCubic(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
    }

    /// <summary>
    /// How far (degrees) the trail's tail lags behind the head on a trip of <paramref name="sweepDegrees"/> at linear
    /// progress <paramref name="progress"/>: min(72°, 0.7 × sweep) · sin(π · progress). It grows and shrinks with the
    /// trip, peaks halfway (where the head is fastest) and is zero when the head rests, whatever the speed.
    /// </summary>
    public static double TrailGap(double sweepDegrees, double progress)
    {
        if (progress <= 0 || progress >= 1 || sweepDegrees <= 0)
        {
            return 0;
        }
        return Math.Min(72, 0.7 * sweepDegrees) * Math.Sin(Math.PI * progress);
    }
}
