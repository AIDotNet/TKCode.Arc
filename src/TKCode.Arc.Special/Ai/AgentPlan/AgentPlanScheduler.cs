using System.Diagnostics;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The one-shot timers of <see cref="AgentPlanSimulation"/>: a clock and a way to run an action after a delay. The default
/// uses a <see cref="DispatcherTimer"/> on the UI thread; tests step a manual one.
/// </summary>
internal interface IAgentPlanScheduler
{
    /// <summary>Time since the scheduler started.</summary>
    TimeSpan Now { get; }

    /// <summary>Runs <paramref name="action"/> once after <paramref name="delay"/>; disposing the result cancels it.</summary>
    IDisposable Schedule(TimeSpan delay, Action action);
}

/// <summary>The default scheduler: a stopwatch and one-shot dispatcher timers.</summary>
internal sealed class AgentPlanDispatcherScheduler : IAgentPlanScheduler
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public TimeSpan Now => _clock.Elapsed;

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : delay };
        var handle = new TimerHandle(timer);
        timer.Tick += (_, _) =>
        {
            handle.Dispose();
            action();
        };
        timer.Start();
        return handle;
    }

    private sealed class TimerHandle(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}

/// <summary>A scheduler stepped by hand (tests): <see cref="Advance"/> runs every due action in time order.</summary>
internal sealed class AgentPlanManualScheduler : IAgentPlanScheduler
{
    private readonly List<Entry> _entries = new();
    private long _sequence;

    public TimeSpan Now { get; private set; }

    /// <summary>Actions waiting to run.</summary>
    public int PendingCount => _entries.Count(e => !e.Cancelled);

    /// <summary>When the next action is due, or null.</summary>
    public TimeSpan? NextDue => _entries.Where(e => !e.Cancelled).Select(e => (TimeSpan?)e.Due).Min();

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var entry = new Entry(Now + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay), _sequence++, action);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Moves the clock forward by <paramref name="duration"/>, running due actions (and the ones they schedule) in order.</summary>
    public void Advance(TimeSpan duration)
    {
        var end = Now + duration;
        while (true)
        {
            Entry? next = null;
            foreach (var entry in _entries)
            {
                if (!entry.Cancelled && entry.Due <= end && (next is null || entry.Due < next.Due || (entry.Due == next.Due && entry.Sequence < next.Sequence)))
                {
                    next = entry;
                }
            }
            if (next is null)
            {
                break;
            }
            _entries.Remove(next);
            Now = next.Due;
            next.Action();
        }
        _entries.RemoveAll(e => e.Cancelled);
        Now = end;
    }

    /// <summary>Moves the clock to the next due action and runs it (and any due at the same time); false when nothing waits.</summary>
    public bool RunNext()
    {
        if (NextDue is not { } due)
        {
            return false;
        }
        Advance(due - Now);
        return true;
    }

    private sealed class Entry(TimeSpan due, long sequence, Action action) : IDisposable
    {
        public TimeSpan Due { get; } = due;
        public long Sequence { get; } = sequence;
        public Action Action { get; } = action;
        public bool Cancelled { get; private set; }
        public void Dispose() => Cancelled = true;
    }
}
