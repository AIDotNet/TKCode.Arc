namespace TKCode.Arc;

/// <summary>One agent row of the normalized run.</summary>
internal sealed class AgentTimelineRowInfo
{
    public AgentTimelineRowInfo(int index, AgentTimelineAgent agent)
    {
        Index = index;
        Agent = agent;
    }

    /// <summary>The row index (0 is the orchestrator).</summary>
    public int Index { get; }

    public AgentTimelineAgent Agent { get; }

    /// <summary>The index of the row's first span in <see cref="AgentTimelineModel.Spans"/>.</summary>
    public int First { get; internal set; }

    /// <summary>How many spans the row has.</summary>
    public int Count { get; internal set; }

    /// <summary>The agent's price per million tokens, or 0 when it has none.</summary>
    public double Price => Agent.Price is { } price && double.IsFinite(price) ? price : 0;
}

/// <summary>One span of the normalized run, with its times read once (the span object may change later; the model is rebuilt then).</summary>
internal sealed class AgentTimelineSpanInfo
{
    public AgentTimelineSpanInfo(AgentTimelineSpan span, int row)
    {
        Span = span;
        Row = row;
        Kind = span.Kind;
        Start = SafeTime(span.Start);
        IsOpen = span.End is not { } end || !double.IsFinite(end);
        End = IsOpen ? double.NaN : Math.Max(Start, SafeTime(span.End!.Value));
        IsFailed = span.Status == AgentTimelineSpanStatus.Failed;
        IsRetry = span.IsRetry;
        Tokens = Math.Max(0, span.Tokens ?? 0);
        Cost = span.Cost is { } cost && double.IsFinite(cost) ? Math.Max(0, cost) : null;
    }

    public AgentTimelineSpan Span { get; }

    /// <summary>The index in <see cref="AgentTimelineModel.Spans"/>.</summary>
    public int Index { get; internal set; }

    public int Row { get; }

    /// <summary>The position within its row (0 = earliest).</summary>
    public int IndexInRow { get; internal set; }

    public AgentTimelineSpanKind Kind { get; }

    public double Start { get; }

    /// <summary>The end, or NaN while the span is open.</summary>
    public double End { get; }

    public bool IsOpen { get; }

    public bool IsFailed { get; }

    public bool IsRetry { get; }

    public int Tokens { get; }

    public double? Cost { get; }

    /// <summary>The handoffs that start or end at this span (indices into <see cref="AgentTimelineModel.Handoffs"/>).</summary>
    public List<int> Handoffs { get; } = new();

    /// <summary>The end used for drawing and counting: an open span runs to <paramref name="openEnd"/>.</summary>
    public double EndAt(double openEnd) => IsOpen ? Math.Max(Start, openEnd) : End;

    private static double SafeTime(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
}

/// <summary>A handoff with its inferred shape.</summary>
internal sealed class AgentTimelineHandoffInfo
{
    public AgentTimelineHandoffInfo(AgentTimelineHandoff handoff, int fromRow, int toRow)
    {
        Handoff = handoff;
        FromRow = fromRow;
        ToRow = toRow;
        At = double.IsFinite(handoff.At) ? Math.Max(0, handoff.At) : 0;
        IsDispatch = fromRow < toRow;
        UpperRow = Math.Min(fromRow, toRow);
        LowerRow = Math.Max(fromRow, toRow);
    }

    public AgentTimelineHandoff Handoff { get; }

    public int Index { get; internal set; }

    public int FromRow { get; }

    public int ToRow { get; }

    public double At { get; }

    /// <summary>True when work goes down a row (it drops at <see cref="At"/> into the target's next span).</summary>
    public bool IsDispatch { get; }

    /// <summary>The row whose bar the vertical leaves or reaches (the dot sits on its bar's bottom edge).</summary>
    public int UpperRow { get; }

    /// <summary>The row the horizontal runs along (its color is the connector's).</summary>
    public int LowerRow { get; }

    /// <summary>The upper row's span at <see cref="At"/> (the one whose [start, end) holds it), or −1.</summary>
    public int UpperSpan { get; internal set; } = -1;

    /// <summary>The lower row's span the connector meets: the target's next span (dispatch) or the source's last (result); −1 when there is none yet.</summary>
    public int LowerSpan { get; internal set; } = -1;
}

/// <summary>Tokens, cost and busy agents at one moment.</summary>
internal readonly record struct AgentTimelineCounts(long Tokens, double Cost, int Working);

/// <summary>
/// An <see cref="AgentTimeline"/>'s run, normalized once per data change: rows in agent order, spans sorted by row and
/// start, each handoff's shape and its two endpoint spans, the totals; plus the queries the view needs at a time t
/// (counters, mounted spans, the keyboard model, the pinned path). Spans of unknown agents and handoffs between unknown
/// or identical agents are left out.
/// </summary>
internal sealed class AgentTimelineModel
{
    public static readonly AgentTimelineModel Empty = new([], [], [], null);

    private AgentTimelineModel(AgentTimelineRowInfo[] rows, AgentTimelineSpanInfo[] spans, AgentTimelineHandoffInfo[] handoffs, double? duration)
    {
        Rows = rows;
        Spans = spans;
        Handoffs = handoffs;
        double end = 0;
        foreach (var span in spans)
        {
            end = Math.Max(end, span.IsOpen ? span.Start : span.End);
            HasOpenSpans |= span.IsOpen;
        }
        foreach (var handoff in handoffs)
        {
            end = Math.Max(end, handoff.At);
        }
        DataEnd = end;
        Duration = duration is { } d && double.IsFinite(d) && d > 0 ? Math.Max(d, 0) : end;
    }

    public AgentTimelineRowInfo[] Rows { get; }

    /// <summary>All spans, row by row, each row by start.</summary>
    public AgentTimelineSpanInfo[] Spans { get; }

    /// <summary>All handoffs by time.</summary>
    public AgentTimelineHandoffInfo[] Handoffs { get; }

    /// <summary>The last thing that happened: the latest span end (an open span's start) or handoff.</summary>
    public double DataEnd { get; }

    /// <summary>The run's length: the explicit duration, or <see cref="DataEnd"/>.</summary>
    public double Duration { get; }

    /// <summary>True when a span has no end yet.</summary>
    public bool HasOpenSpans { get; }

    /// <summary>Normalizes a run.</summary>
    public static AgentTimelineModel Build(IReadOnlyList<AgentTimelineAgent> agents, IReadOnlyList<AgentTimelineSpan> spans,
        IReadOnlyList<AgentTimelineHandoff> handoffs, double? duration)
    {
        var rows = new List<AgentTimelineRowInfo>(agents.Count);
        var rowOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var agent in agents)
        {
            if (agent is null || rowOf.ContainsKey(agent.Id))
            {
                continue;
            }
            rowOf[agent.Id] = rows.Count;
            rows.Add(new AgentTimelineRowInfo(rows.Count, agent));
        }

        var list = new List<AgentTimelineSpanInfo>(spans.Count);
        foreach (var span in spans)
        {
            if (span is not null && rowOf.TryGetValue(span.AgentId, out var row))
            {
                list.Add(new AgentTimelineSpanInfo(span, row));
            }
        }
        // Stable: equal starts keep their data order.
        var sorted = list.Select((s, i) => (s, i)).OrderBy(p => p.s.Row).ThenBy(p => p.s.Start).ThenBy(p => p.i).Select(p => p.s).ToArray();
        for (var i = 0; i < sorted.Length; i++)
        {
            sorted[i].Index = i;
        }
        var position = 0;
        foreach (var row in rows)
        {
            row.First = position;
            var count = 0;
            while (position < sorted.Length && sorted[position].Row == row.Index)
            {
                sorted[position].IndexInRow = count++;
                position++;
            }
            row.Count = count;
        }

        var shapes = new List<AgentTimelineHandoffInfo>(handoffs.Count);
        foreach (var handoff in handoffs)
        {
            if (handoff is null || !rowOf.TryGetValue(handoff.From, out var from) || !rowOf.TryGetValue(handoff.To, out var to) || from == to)
            {
                continue;
            }
            shapes.Add(new AgentTimelineHandoffInfo(handoff, from, to));
        }
        var ordered = shapes.Select((h, i) => (h, i)).OrderBy(p => p.h.At).ThenBy(p => p.i).Select(p => p.h).ToArray();
        var model = new AgentTimelineModel(rows.ToArray(), sorted, ordered, duration);
        model.Connect();
        return model;
    }

    /// <summary>Finds each handoff's two endpoint spans and lists the handoffs on their spans.</summary>
    private void Connect()
    {
        for (var i = 0; i < Handoffs.Length; i++)
        {
            var handoff = Handoffs[i];
            handoff.Index = i;
            handoff.UpperSpan = SpanHolding(handoff.UpperRow, handoff.At);
            handoff.LowerSpan = handoff.IsDispatch
                ? FirstStartingAtOrAfter(handoff.LowerRow, handoff.At)
                : LastEndingAtOrBefore(handoff.LowerRow, handoff.At);
            if (handoff.UpperSpan >= 0)
            {
                Spans[handoff.UpperSpan].Handoffs.Add(i);
            }
            if (handoff.LowerSpan >= 0 && handoff.LowerSpan != handoff.UpperSpan)
            {
                Spans[handoff.LowerSpan].Handoffs.Add(i);
            }
        }
    }

    private const double Epsilon = 1e-6;

    /// <summary>The row's span whose [start, end) holds <paramref name="at"/>; else the last one starting at or before it; else −1.</summary>
    public int SpanHolding(int row, double at)
    {
        var info = Rows[row];
        var fallback = -1;
        for (var i = info.First; i < info.First + info.Count; i++)
        {
            var span = Spans[i];
            if (span.Start > at + Epsilon)
            {
                break;
            }
            if (span.IsOpen || at < span.End - Epsilon)
            {
                return i;
            }
            fallback = i;
        }
        return fallback;
    }

    private int FirstStartingAtOrAfter(int row, double at)
    {
        var info = Rows[row];
        for (var i = info.First; i < info.First + info.Count; i++)
        {
            if (Spans[i].Start >= at - Epsilon)
            {
                return i;
            }
        }
        return -1;
    }

    private int LastEndingAtOrBefore(int row, double at)
    {
        var info = Rows[row];
        var found = -1;
        for (var i = info.First; i < info.First + info.Count; i++)
        {
            var span = Spans[i];
            if (!span.IsOpen && span.End <= at + Epsilon)
            {
                found = i;
            }
        }
        return found;
    }

    // ---- Counters and totals --------------------------------------------------------------------------------------

    /// <summary>
    /// Tokens and cost at <paramref name="t"/>: each span's tokens accrue evenly from its start to its end
    /// (tokens(t) = Σ tokens × clamp((t − start) / (end − start), 0, 1)), cost likewise from the span's own cost or its
    /// tokens times the agent's price per million; and the number of agents thinking or calling a tool at t.
    /// </summary>
    public AgentTimelineCounts CountsAt(double t, double openEnd)
    {
        double tokens = 0;
        double cost = 0;
        var working = 0;
        var lastRow = -1;
        var rowWorking = false;
        foreach (var span in Spans)
        {
            if (span.Row != lastRow)
            {
                if (rowWorking)
                {
                    working++;
                }
                lastRow = span.Row;
                rowWorking = false;
            }
            var end = span.EndAt(openEnd);
            var fraction = Fraction(span.Start, end, t);
            if (fraction > 0)
            {
                tokens += span.Tokens * fraction;
                cost += (span.Cost ?? span.Tokens * Rows[span.Row].Price / 1e6) * fraction;
            }
            if (span.Kind != AgentTimelineSpanKind.Waiting && span.Start <= t && t < end)
            {
                rowWorking = true;
            }
        }
        if (rowWorking)
        {
            working++;
        }
        return new AgentTimelineCounts(AgentTimelineMath.RoundTokens(tokens), cost, working);
    }

    private static double Fraction(double start, double end, double t)
    {
        if (t < start)
        {
            return 0;
        }
        if (end <= start)
        {
            return 1;
        }
        return Math.Clamp((t - start) / (end - start), 0, 1);
    }

    /// <summary>
    /// The run's totals: the agent count, its length, the speedup (time spent thinking and calling tools, summed over all
    /// agents, divided by the run's length; waiting is idle time and does not count), all tokens and the cost.
    /// </summary>
    public AgentTimelineSummary Summary(double length, double openEnd)
    {
        double busy = 0;
        foreach (var span in Spans)
        {
            if (span.Kind != AgentTimelineSpanKind.Waiting)
            {
                busy += Math.Max(0, span.EndAt(openEnd) - span.Start);
            }
        }
        var counts = CountsAt(double.MaxValue, openEnd);
        return new AgentTimelineSummary(Rows.Length, length, length > 0 ? busy / length : 0, counts.Tokens, counts.Cost);
    }

    // ---- Mounting and the keyboard model --------------------------------------------------------------------------

    /// <summary>A span exists on screen once the replay reaches its start.</summary>
    public bool IsMounted(int span, double t) => Spans[span].Start <= t + 1e-9;

    /// <summary>The middle of what is drawn of a span at <paramref name="t"/> (its start up to now or its end).</summary>
    public double VisibleCenter(int span, double t, double openEnd)
    {
        var info = Spans[span];
        return (info.Start + Math.Max(info.Start, Math.Min(info.EndAt(openEnd), t))) / 2;
    }

    /// <summary>The first span the keyboard lands on: the earliest mounted span of the top-most row that has one, or −1.</summary>
    public int FirstMounted(double t)
    {
        foreach (var row in Rows)
        {
            if (row.Count > 0 && IsMounted(row.First, t))
            {
                return row.First;
            }
        }
        return -1;
    }

    /// <summary>
    /// Where an arrow key or Home / End moves the roving span: left / right to the previous or next mounted span of the
    /// same row (no wrapping), up / down to the mounted span of the nearest row above or below whose middle is closest in
    /// time (rows with nothing mounted yet are skipped), Home / End to the row's first or last mounted span. Returns the
    /// same span when it cannot move.
    /// </summary>
    public int Navigate(int span, AgentTimelineKeyMove move, double t, double openEnd)
    {
        if (span < 0 || span >= Spans.Length)
        {
            return FirstMounted(t);
        }
        var info = Spans[span];
        var row = Rows[info.Row];
        switch (move)
        {
            case AgentTimelineKeyMove.Previous:
                return info.IndexInRow > 0 ? span - 1 : span;
            case AgentTimelineKeyMove.Next:
                return info.IndexInRow < row.Count - 1 && IsMounted(span + 1, t) ? span + 1 : span;
            case AgentTimelineKeyMove.First:
                return row.First;
            case AgentTimelineKeyMove.Last:
                return LastMountedInRow(row.Index, t) is var last && last >= 0 ? last : span;
            case AgentTimelineKeyMove.Up:
            case AgentTimelineKeyMove.Down:
                var step = move == AgentTimelineKeyMove.Up ? -1 : 1;
                var center = VisibleCenter(span, t, openEnd);
                for (var r = info.Row + step; r >= 0 && r < Rows.Length; r += step)
                {
                    var best = NearestInRow(r, center, t, openEnd);
                    if (best >= 0)
                    {
                        return best;
                    }
                }
                return span;
            default:
                return span;
        }
    }

    private int LastMountedInRow(int row, double t)
    {
        var info = Rows[row];
        for (var i = info.First + info.Count - 1; i >= info.First; i--)
        {
            if (IsMounted(i, t))
            {
                return i;
            }
        }
        return -1;
    }

    private int NearestInRow(int row, double center, double t, double openEnd)
    {
        var info = Rows[row];
        var best = -1;
        var distance = double.MaxValue;
        for (var i = info.First; i < info.First + info.Count; i++)
        {
            if (!IsMounted(i, t))
            {
                break;
            }
            var d = Math.Abs(VisibleCenter(i, t, openEnd) - center);
            if (d < distance - 1e-9)
            {
                distance = d;
                best = i;
            }
        }
        return best;
    }

    // ---- Pinning ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// The handoff path of a pinned span. A span of the orchestrator (the first row) lights itself, every handoff that
    /// starts or ends during it, and the span at the other end of each. A span of any other agent lights that agent's whole
    /// row, every handoff to or from the agent, and the span at the other end of each. Everything else dims.
    /// </summary>
    public void PathOf(int span, bool[] spansOn, bool[] handoffsOn)
    {
        Array.Clear(spansOn);
        Array.Clear(handoffsOn);
        if (span < 0 || span >= Spans.Length)
        {
            return;
        }
        var info = Spans[span];
        spansOn[span] = true;
        if (info.Row == 0)
        {
            foreach (var h in info.Handoffs)
            {
                handoffsOn[h] = true;
                LightEnds(Handoffs[h], spansOn);
            }
            return;
        }
        var row = Rows[info.Row];
        for (var i = row.First; i < row.First + row.Count; i++)
        {
            spansOn[i] = true;
        }
        foreach (var handoff in Handoffs)
        {
            if (handoff.FromRow == info.Row || handoff.ToRow == info.Row)
            {
                handoffsOn[handoff.Index] = true;
                LightEnds(handoff, spansOn);
            }
        }
    }

    private static void LightEnds(AgentTimelineHandoffInfo handoff, bool[] spansOn)
    {
        if (handoff.UpperSpan >= 0)
        {
            spansOn[handoff.UpperSpan] = true;
        }
        if (handoff.LowerSpan >= 0)
        {
            spansOn[handoff.LowerSpan] = true;
        }
    }

    /// <summary>The model index of a span object, or −1.</summary>
    public int IndexOf(AgentTimelineSpan? span)
    {
        if (span is null)
        {
            return -1;
        }
        foreach (var info in Spans)
        {
            if (ReferenceEquals(info.Span, span))
            {
                return info.Index;
            }
        }
        return -1;
    }
}

/// <summary>A keyboard move between spans.</summary>
internal enum AgentTimelineKeyMove
{
    Previous,
    Next,
    Up,
    Down,
    First,
    Last
}
