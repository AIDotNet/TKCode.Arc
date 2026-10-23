namespace TKCode.Arc;

/// <summary>A station's state: idle (muted), active (filled, ringed, shimmering) or visited (accent outline).</summary>
internal enum AgentLoopNodeState
{
    Idle,
    Active,
    Visited
}

/// <summary>How the run ended.</summary>
internal enum AgentLoopEnding
{
    None,
    Done,
    Stopped
}

/// <summary>A chip in a lane: a tool call (running or resolved) or a check.</summary>
internal sealed class AgentLoopChipState
{
    private AgentLoopChipState(AgentLoopStation station, AgentLoopCall? call, AgentLoopCheck? check, int serial)
    {
        Station = station;
        Call = call;
        Check = check;
        Serial = serial;
    }

    /// <summary>The station it dropped from (Take action for calls, Verify for checks).</summary>
    public AgentLoopStation Station { get; }

    /// <summary>The call, for a tool call chip.</summary>
    public AgentLoopCall? Call { get; }

    /// <summary>The check, for a check chip.</summary>
    public AgentLoopCheck? Check { get; }

    /// <summary>Counts drops, so a lane can tell a new chip from the one it shows.</summary>
    public int Serial { get; }

    /// <summary>True once a call has its result (checks are always resolved).</summary>
    public bool IsResolved { get; set; }

    public static AgentLoopChipState ForCall(AgentLoopCall call, int serial) => new(AgentLoopStation.Act, call, null, serial);

    public static AgentLoopChipState ForCheck(AgentLoopCheck check, int serial) =>
        new(AgentLoopStation.Verify, null, check, serial) { IsResolved = true };
}

/// <summary>
/// What the story has shown so far: the beats change it, the view (stage, ring, header) draws it. Kept apart from the
/// view so the story runs (and tests read it) before the template exists, and a new template shows the same moment.
/// </summary>
internal sealed class AgentLoopScene
{
    private readonly AgentLoopNodeState[] _stations = new AgentLoopNodeState[3];
    private int _serial;

    /// <summary>The zero-based loop the head is on; −1 before the first Gather.</summary>
    public int Loop { get; set; } = -1;

    /// <summary>How many context blocks have joined.</summary>
    public int Blocks { get; set; }

    /// <summary>The block that just joined (accent border while Gather is active), or −1.</summary>
    public int FreshBlock { get; set; } = -1;

    /// <summary>The context total in tokens.</summary>
    public long Tokens { get; set; }

    /// <summary>The latest tool call, or null.</summary>
    public AgentLoopChipState? CallChip { get; set; }

    /// <summary>The latest check, or null.</summary>
    public AgentLoopChipState? CheckChip { get; set; }

    /// <summary>The chip that dropped last (the one a single lane shows), or null.</summary>
    public AgentLoopChipState? LatestChip { get; set; }

    /// <summary>True while the head is shown (from the first Gather to the end).</summary>
    public bool HeadVisible { get; set; }

    /// <summary>How the run ended, or None while it runs.</summary>
    public AgentLoopEnding Ending { get; set; }

    /// <summary>True once any beat has run (the run left Ready).</summary>
    public bool HasStarted => Loop >= 0;

    /// <summary>A station's state.</summary>
    public AgentLoopNodeState this[AgentLoopStation station]
    {
        get => _stations[(int)station];
        set => _stations[(int)station] = value;
    }

    /// <summary>The active station, or null between stations.</summary>
    public AgentLoopStation? ActiveStation
    {
        get
        {
            for (var i = 0; i < _stations.Length; i++)
            {
                if (_stations[i] == AgentLoopNodeState.Active)
                {
                    return (AgentLoopStation)i;
                }
            }
            return null;
        }
    }

    /// <summary>A new serial for a dropped chip.</summary>
    public int NextSerial() => ++_serial;

    /// <summary>Back to Ready: no loop, no blocks, no chips, idle stations, no head.</summary>
    public void Reset()
    {
        Array.Clear(_stations);
        Loop = -1;
        Blocks = 0;
        FreshBlock = -1;
        Tokens = 0;
        CallChip = null;
        CheckChip = null;
        LatestChip = null;
        HeadVisible = false;
        Ending = AgentLoopEnding.None;
    }
}
