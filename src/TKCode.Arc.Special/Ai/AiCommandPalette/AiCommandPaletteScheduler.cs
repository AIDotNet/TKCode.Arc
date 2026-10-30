using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The timers of <see cref="AiCommandPalette"/>: the answer's word pacing, the simulated model and the simulated actions.
/// The default uses one-shot <see cref="DispatcherTimer"/>s on the UI thread; tests step a manual one.
/// </summary>
internal interface IAiCommandPaletteScheduler
{
    /// <summary>Runs <paramref name="action"/> once after <paramref name="delay"/>; disposing the result cancels it.</summary>
    IDisposable Schedule(TimeSpan delay, Action action);
}

/// <summary>Awaitable delays on a scheduler.</summary>
internal static class AiCommandPaletteSchedulerExtensions
{
    /// <summary>Completes after <paramref name="delay"/>, or throws <see cref="OperationCanceledException"/> when cancelled.</summary>
    public static Task Delay(this IAiCommandPaletteScheduler scheduler, TimeSpan delay, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return Task.FromCanceled(token);
        }
        var source = new TaskCompletionSource();
        IDisposable? timer = null;
        var registration = token.Register(() =>
        {
            timer?.Dispose();
            source.TrySetCanceled(token);
        });
        timer = scheduler.Schedule(delay, () =>
        {
            registration.Dispose();
            source.TrySetResult();
        });
        return source.Task;
    }
}

/// <summary>The default scheduler: one-shot dispatcher timers.</summary>
internal sealed class AiCommandPaletteDispatcherScheduler : IAiCommandPaletteScheduler
{
    public static AiCommandPaletteDispatcherScheduler Instance { get; } = new();

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : delay
        };
        var handle = new Handle(timer);
        timer.Tick += (_, _) =>
        {
            handle.Dispose();
            action();
        };
        timer.Start();
        return handle;
    }

    private sealed class Handle(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}

/// <summary>A scheduler stepped by hand (tests): <see cref="Advance"/> runs every due action in time order.</summary>
internal sealed class AiCommandPaletteManualScheduler : IAiCommandPaletteScheduler
{
    private readonly List<Entry> _entries = [];
    private long _sequence;

    /// <summary>The scheduler's clock.</summary>
    public TimeSpan Now { get; private set; }

    /// <summary>Actions still waiting.</summary>
    public int PendingCount => _entries.Count(e => !e.Cancelled);

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var entry = new Entry(Now + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay), _sequence++, action);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Moves the clock forward, running due actions (and the ones they schedule) in order.</summary>
    public void Advance(TimeSpan duration)
    {
        var end = Now + duration;
        while (true)
        {
            Entry? next = null;
            foreach (var entry in _entries)
            {
                if (!entry.Cancelled && entry.Due <= end &&
                    (next is null || entry.Due < next.Due || (entry.Due == next.Due && entry.Sequence < next.Sequence)))
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
            // Continuations of awaited delays run on the dispatcher.
            Dispatcher.UIThread.RunJobs();
        }
        _entries.RemoveAll(e => e.Cancelled);
        Now = end;
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
