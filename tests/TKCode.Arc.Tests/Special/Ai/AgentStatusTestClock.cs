namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>A manual <see cref="TimeProvider"/>: time only moves in <see cref="Advance"/>, which fires the timers that fall due in order.</summary>
internal sealed class AgentStatusTestClock : TimeProvider
{
    private readonly List<ManualTimer> _timers = new();
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Moves time forward, firing every timer due on the way at its own due time.</summary>
    public void Advance(TimeSpan span)
    {
        var target = _now + span;
        while (true)
        {
            ManualTimer? next = null;
            foreach (var timer in _timers)
            {
                if (timer.Due is { } due && due <= target && (next is null || due < next.Due))
                {
                    next = timer;
                }
            }
            if (next is null)
            {
                break;
            }
            _now = next.Due!.Value > _now ? next.Due.Value : _now;
            next.Fire();
        }
        _now = target;
        _timers.RemoveAll(t => t.IsDisposed);
    }

    public void Advance(double milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));

    public int ActiveTimers => _timers.Count(t => !t.IsDisposed && t.Due is not null);

    private sealed class ManualTimer : ITimer
    {
        private readonly AgentStatusTestClock _clock;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;

        public ManualTimer(AgentStatusTestClock clock, TimerCallback callback, object? state)
        {
            _clock = clock;
            _callback = callback;
            _state = state;
        }

        public DateTimeOffset? Due { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : _clock._now + dueTime;
            return true;
        }

        public void Fire()
        {
            Due = _period > TimeSpan.Zero && _period != Timeout.InfiniteTimeSpan ? Due + _period : null;
            _callback(_state);
        }

        public void Dispose()
        {
            IsDisposed = true;
            Due = null;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
