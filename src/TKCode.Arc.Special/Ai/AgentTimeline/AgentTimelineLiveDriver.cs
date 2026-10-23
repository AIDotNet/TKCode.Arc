using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Plays a finished run into an <see cref="AgentTimeline"/> as if it were happening now, for demos of live mode: it hands
/// the timeline a growing copy of the run (<see cref="LiveRun"/>), turns <see cref="AgentTimeline.IsLive"/> on, and on a
/// wall-clock timer appends each span when it starts (without an end), sets its end when it finishes and appends each
/// handoff at its moment, the way events would arrive from a backend. When the source run is over it turns live mode
/// off, so the timeline settles on its summary.
/// </summary>
public sealed class AgentTimelineLiveDriver : IDisposable
{
    private readonly AgentTimelineRun _source;
    private readonly TimeProvider _time;
    private readonly List<(AgentTimelineSpan Source, AgentTimelineSpan? Copy)> _spans;
    private readonly List<AgentTimelineHandoff> _handoffs;
    private readonly double _length;
    private DispatcherTimer? _timer;
    private AgentTimeline? _target;
    private DateTimeOffset _startedAt;
    private int _nextHandoff;

    /// <summary>Creates a driver for <paramref name="source"/> (left untouched; the timeline gets a copy).</summary>
    public AgentTimelineLiveDriver(AgentTimelineRun source)
        : this(source, null)
    {
    }

    internal AgentTimelineLiveDriver(AgentTimelineRun source, TimeProvider? time)
    {
        _source = source;
        _time = time ?? TimeProvider.System;
        _spans = source.Spans.OrderBy(s => s.Start).Select(s => (s, (AgentTimelineSpan?)null)).ToList();
        _handoffs = source.Handoffs.OrderBy(h => h.At).ToList();
        double end = 0;
        foreach (var span in source.Spans)
        {
            end = Math.Max(end, span.End ?? span.Start);
        }
        foreach (var handoff in source.Handoffs)
        {
            end = Math.Max(end, handoff.At);
        }
        _length = source.Duration ?? end;
        LiveRun = NewRun();
    }

    /// <summary>The growing copy the timeline shows.</summary>
    public AgentTimelineRun LiveRun { get; private set; }

    /// <summary>True between <see cref="Start"/> and the end of the run (or <see cref="Stop"/>).</summary>
    public bool IsRunning => _target is not null;

    /// <summary>Seconds since the run started.</summary>
    public double Elapsed => _target is null ? 0 : Math.Max(0, (_time.GetUtcNow() - _startedAt).TotalSeconds);

    /// <summary>Raised when the whole run has arrived and live mode was turned off.</summary>
    public event EventHandler? Finished;

    /// <summary>Starts the run over, now, in <paramref name="timeline"/>: it shows the growing copy in live mode.</summary>
    public void Start(AgentTimeline timeline)
    {
        Stop();
        _target = timeline;
        _startedAt = _time.GetUtcNow();
        _nextHandoff = 0;
        for (var i = 0; i < _spans.Count; i++)
        {
            _spans[i] = (_spans[i].Source, null);
        }
        LiveRun = NewRun();
        LiveRun.StartedAt = _startedAt;
        timeline.IsLive = false;
        timeline.Run = LiveRun;
        timeline.IsLive = true;
        Update();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => Update());
        _timer.Start();
    }

    /// <summary>Stops feeding events (the timeline stays in live mode until you turn it off).</summary>
    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _target = null;
    }

    /// <summary>Delivers every event up to now; at the end of the run, turns live mode off (tests call it directly).</summary>
    internal void Update()
    {
        if (_target is not { } target)
        {
            return;
        }
        var now = Elapsed;
        for (var i = 0; i < _spans.Count; i++)
        {
            var (source, copy) = _spans[i];
            if (copy is null && source.Start <= now)
            {
                copy = new AgentTimelineSpan(source.AgentId, source.Kind, source.Start, null, source.Label)
                {
                    Id = source.Id,
                    Detail = source.Detail,
                    Tool = source.Tool,
                    ToolMark = source.ToolMark,
                    Tokens = source.Tokens,
                    Cost = source.Cost,
                    Status = source.Status,
                    IsRetry = source.IsRetry
                };
                _spans[i] = (source, copy);
                LiveRun.Spans.Add(copy);
            }
            if (copy is not null && copy.End is null && source.End is { } end && end <= now)
            {
                copy.End = end;
            }
        }
        while (_nextHandoff < _handoffs.Count && _handoffs[_nextHandoff].At <= now)
        {
            var handoff = _handoffs[_nextHandoff++];
            LiveRun.Handoffs.Add(new AgentTimelineHandoff(handoff.From, handoff.To, handoff.At, handoff.Label));
        }
        if (now >= _length)
        {
            Stop();
            target.IsLive = false;
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    private AgentTimelineRun NewRun()
    {
        var run = new AgentTimelineRun { Title = _source.Title };
        run.Agents.AddRange(_source.Agents);
        return run;
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
