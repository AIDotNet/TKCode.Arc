namespace TKCode.Arc;

/// <summary>
/// The active time of one run as <see cref="AgentStatus"/> counts it: from the run's start to now (or to its end), leaving
/// out every stretch spent paused or failed, which the clock records itself as the state changes (the host keeps one
/// fixed start for the whole run). Waiting for input counts. Pure bookkeeping on instants the owner passes in.
/// </summary>
internal sealed class AgentStatusRunClock
{
    private readonly List<(DateTimeOffset Start, DateTimeOffset End)> _excluded = new();
    private DateTimeOffset? _excludedSince;
    private DateTimeOffset? _autoStart;
    private DateTimeOffset? _autoEnd;

    /// <summary>The run's start given by the host (null: the first live state seen).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>The run's end given by the host (null: the moment done or stopped was seen).</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>The start the timer counts from, or null before a run.</summary>
    public DateTimeOffset? Start => StartedAt ?? _autoStart;

    /// <summary>The end the timer stops at, or null while the run goes on.</summary>
    public DateTimeOffset? End => EndedAt ?? _autoEnd;

    /// <summary>True while a paused or failed stretch is open.</summary>
    public bool IsExcluding => _excludedSince is not null;

    /// <summary>A new start from the host: a new run, so the recorded pauses no longer apply.</summary>
    public void SetStartedAt(DateTimeOffset? startedAt)
    {
        if (StartedAt == startedAt)
        {
            return;
        }
        StartedAt = startedAt;
        _excluded.Clear();
        _autoEnd = null;
        if (_excludedSince is not null && startedAt is { } start && _excludedSince < start)
        {
            _excludedSince = start;
        }
    }

    /// <summary>Records a state seen at <paramref name="now"/>: opens or closes the excluded stretch and marks the end.</summary>
    public void Observe(AgentStatusRunState state, DateTimeOffset now)
    {
        var excluding = state is AgentStatusRunState.Paused or AgentStatusRunState.Failed;
        if (excluding && _excludedSince is null)
        {
            _excludedSince = now;
        }
        else if (!excluding && _excludedSince is { } since)
        {
            if (now > since)
            {
                _excluded.Add((since, now));
            }
            _excludedSince = null;
        }
        switch (state)
        {
            case AgentStatusRunState.Working:
            case AgentStatusRunState.NeedsInput:
                _autoStart ??= now;
                _autoEnd = null;
                break;
            case AgentStatusRunState.Done:
            case AgentStatusRunState.Stopped:
                _autoStart ??= now;
                _autoEnd ??= now;
                break;
            case AgentStatusRunState.Idle:
                // Nothing runs: the next live state starts a new run unless the host gives a start.
                _autoStart = null;
                _autoEnd = null;
                if (StartedAt is null)
                {
                    _excluded.Clear();
                }
                break;
        }
    }

    /// <summary>Active time between two instants: the span minus the paused and failed stretches inside it.</summary>
    public TimeSpan ActiveBetween(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            return TimeSpan.Zero;
        }
        var total = to - from;
        foreach (var (start, end) in _excluded)
        {
            total -= Overlap(from, to, start, end);
        }
        if (_excludedSince is { } since)
        {
            total -= Overlap(from, to, since, to);
        }
        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }

    /// <summary>The run's active time at <paramref name="now"/> (zero before it started).</summary>
    public TimeSpan Elapsed(DateTimeOffset now)
    {
        if (Start is not { } start)
        {
            return TimeSpan.Zero;
        }
        var to = End is { } end && end < now ? end : now;
        return ActiveBetween(start, to);
    }

    private static TimeSpan Overlap(DateTimeOffset from, DateTimeOffset to, DateTimeOffset start, DateTimeOffset end)
    {
        var a = start > from ? start : from;
        var b = end < to ? end : to;
        return b > a ? b - a : TimeSpan.Zero;
    }
}
