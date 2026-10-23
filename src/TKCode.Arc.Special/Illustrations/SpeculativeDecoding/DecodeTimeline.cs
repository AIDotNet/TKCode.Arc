namespace TKCode.Arc;

/// <summary>
/// When everything in a planned run happens, in seconds of story time from the start. Round r drafts its k chips one
/// every draftCost × pass, then the target verifies: its sweep crosses the k cells in 0.4 pass and gives cell j its
/// verdict as it passes the cell's middle; at 0.72 pass the round settles (the guesses fly into the answer, 20 ms apart)
/// and at one pass the next round starts. The target-only line writes token m (from 0) at m × pass + 0.72 pass.
/// </summary>
internal sealed class DecodeTimeline
{
    /// <summary>The share of a pass the verify phase takes before the round settles.</summary>
    public const double VerifyShare = 0.72;

    /// <summary>The share of a pass the target's sweep takes to cross every cell.</summary>
    public const double SweepShare = 0.4;

    /// <summary>Seconds between two tokens leaving for the answer in one round.</summary>
    public const double Stagger = 0.02;

    private readonly double[] _starts;

    public DecodeTimeline(DecodePlan plan, double draftCost, double pass)
    {
        Plan = plan;
        DraftCost = Math.Max(0, draftCost);
        Pass = Math.Max(0.01, pass);
        _starts = new double[plan.Rounds.Count + 1];
        var t = 0.0;
        for (var r = 0; r < plan.Rounds.Count; r++)
        {
            _starts[r] = t;
            t += plan.Rounds[r].Count * DraftCost * Pass + Pass;
        }
        _starts[plan.Rounds.Count] = t;
        MainEnd = t;
        var tokens = plan.Tokens.Count;
        CompareEnd = tokens > 0 ? (tokens - 1) * Pass + VerifyShare * Pass : 0;
    }

    public DecodePlan Plan { get; }

    /// <summary>One draft token as a share of a pass.</summary>
    public double DraftCost { get; }

    /// <summary>Seconds of one target pass.</summary>
    public double Pass { get; }

    /// <summary>When the speculative run is done (the end of its last round).</summary>
    public double MainEnd { get; }

    /// <summary>When the target-only line writes its last token.</summary>
    public double CompareEnd { get; }

    /// <summary>The number of rounds.</summary>
    public int Count => Plan.Rounds.Count;

    /// <summary>When round <paramref name="r"/> starts drafting.</summary>
    public double Start(int r) => _starts[Math.Clamp(r, 0, _starts.Length - 1)];

    /// <summary>When round <paramref name="r"/> starts verifying (its drafting is done).</summary>
    public double VerifyStart(int r) => Start(r) + Plan.Rounds[r].Count * DraftCost * Pass;

    /// <summary>
    /// When the sweep of round <paramref name="r"/> has crossed every cell (a bonus column appears then); a round without
    /// drafts has nothing to sweep, so its target column appears as soon as it verifies.
    /// </summary>
    public double SweepEnd(int r) => Plan.Rounds[r].Count == 0 ? VerifyStart(r) : VerifyStart(r) + SweepShare * Pass;

    /// <summary>When round <paramref name="r"/> settles: verdicts are in, tokens leave for the answer.</summary>
    public double Settle(int r) => VerifyStart(r) + VerifyShare * Pass;

    /// <summary>When round <paramref name="r"/> ends (the next one starts).</summary>
    public double End(int r) => Start(r + 1);

    /// <summary>When chip <paramref name="j"/> of round <paramref name="r"/> appears.</summary>
    public double ChipTime(int r, int j) => Start(r) + j * DraftCost * Pass;

    /// <summary>When cell <paramref name="j"/> of round <paramref name="r"/> gets its verdict (the sweep passes its middle).</summary>
    public double VerdictTime(int r, int j)
    {
        var k = Plan.Rounds[r].Count;
        return k == 0 ? VerifyStart(r) : VerifyStart(r) + (j + 0.5) / k * SweepShare * Pass;
    }

    /// <summary>When every verdict of round <paramref name="r"/> is in (the bonus column, or the last cell's verdict).</summary>
    public double LastVerdict(int r)
    {
        var round = Plan.Rounds[r];
        return round.IsBonus ? SweepEnd(r) : VerdictTime(r, round.Count - 1);
    }

    /// <summary>The sweep of round <paramref name="r"/> at <paramref name="t"/>: 0 to k cells.</summary>
    public double Sweep(int r, double t)
    {
        var k = Plan.Rounds[r].Count;
        if (k == 0)
        {
            return 0;
        }
        return k * Math.Clamp((t - VerifyStart(r)) / (SweepShare * Pass), 0, 1);
    }

    /// <summary>The round running at <paramref name="t"/> (−1 before the start, <see cref="Count"/> after the end).</summary>
    public int RoundAt(double t)
    {
        if (Count == 0 || t < 0)
        {
            return t < 0 ? -1 : Count;
        }
        for (var r = 0; r < Count; r++)
        {
            if (t < _starts[r + 1])
            {
                return r;
            }
        }
        return Count;
    }

    /// <summary>Rounds that have settled by <paramref name="t"/> (their tokens have left for the answer).</summary>
    public int SettledAt(double t)
    {
        var settled = 0;
        for (var r = 0; r < Count; r++)
        {
            if (Settle(r) <= t)
            {
                settled = r + 1;
            }
            else
            {
                break;
            }
        }
        return settled;
    }

    /// <summary>When answer token <paramref name="token"/> leaves for the answer (its round settles, then 20 ms per token).</summary>
    public double LandTime(int token)
    {
        var r = Plan.RoundOf(token);
        return Settle(r) + (token - Plan.Rounds[r].Start) * Stagger;
    }

    /// <summary>When the target-only line writes token <paramref name="token"/> (from 0).</summary>
    public double CompareTime(int token) => token * Pass + VerifyShare * Pass;

    /// <summary>Tokens the target-only line has written by <paramref name="t"/>.</summary>
    public int CompareWritten(double t)
    {
        var tokens = Plan.Tokens.Count;
        if (tokens == 0 || t < VerifyShare * Pass)
        {
            return 0;
        }
        return Math.Min(tokens, (int)Math.Floor((t - VerifyShare * Pass) / Pass + 1e-9) + 1);
    }
}
