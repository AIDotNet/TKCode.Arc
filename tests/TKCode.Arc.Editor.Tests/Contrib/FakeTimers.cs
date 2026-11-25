// Stand-in for upstream's runWithFakedTimers (src/vs/base/test/common/timeTravelScheduler.ts, commit b57d5c9e, MIT
// license): a virtual clock for an IdleScheduler's timers and a single-threaded synchronization context, so that
// timers, awaits and their continuations run in a deterministic order on the test thread. Time only moves when nothing
// else can run (the next timer fires then), as upstream's time travel scheduler does.

namespace TKCode.Arc.Editor.Tests;

internal sealed class FakeTimers : SynchronizationContext
{
    private readonly List<Timer> _timers = [];
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _posts = new();
    private long _seq;

    public FakeTimers() =>
        Scheduler = new IdleScheduler(
            runWhenIdle: callback => SetTimeout(0, callback),
            clock: () => Now,
            runAfter: (delay, callback) => SetTimeout(delay.TotalMilliseconds, callback));

    /// <summary>The virtual time in milliseconds.</summary>
    public double Now { get; private set; }

    /// <summary>A scheduler whose timers run on this clock.</summary>
    public IdleScheduler Scheduler { get; }

    /// <summary>Upstream's setTimeout on the virtual clock.</summary>
    public IDisposable SetTimeout(double ms, Action callback)
    {
        var timer = new Timer(this, Now + Math.Max(0, ms), _seq++, callback);
        _timers.Add(timer);
        return timer;
    }

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state) => _posts.Enqueue((d, state));

    /// <inheritdoc />
    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <inheritdoc />
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>Runs the queued continuations.</summary>
    public void RunPosts()
    {
        while (_posts.TryDequeue(out var post))
        {
            post.Callback(post.State);
        }
    }

    /// <summary>Runs one step: the queued continuations, else the next timer. False when nothing is left.</summary>
    public bool Step()
    {
        if (_posts.Count > 0)
        {
            RunPosts();
            return true;
        }

        if (_timers.Count == 0)
        {
            return false;
        }

        var next = _timers.OrderBy(t => t.Due).ThenBy(t => t.Seq).First();
        _timers.Remove(next);
        Now = Math.Max(Now, next.Due);
        next.Callback();
        RunPosts();
        return true;
    }

    /// <summary>Moves the clock <paramref name="ms"/> forward, running what falls due on the way.</summary>
    public void Advance(double ms)
    {
        var end = Now + ms;
        RunPosts();
        while (true)
        {
            var next = _timers.Where(t => t.Due <= end).OrderBy(t => t.Due).ThenBy(t => t.Seq).FirstOrDefault();
            if (next is null)
            {
                break;
            }

            _timers.Remove(next);
            Now = Math.Max(Now, next.Due);
            next.Callback();
            RunPosts();
        }

        Now = end;
    }

    /// <summary>Runs <paramref name="body"/> with this context until it completes, moving time when it waits.</summary>
    public static void Run(Func<FakeTimers, Task> body, int maxSteps = 100_000)
    {
        var timers = new FakeTimers();
        var previous = Current;
        SetSynchronizationContext(timers);
        try
        {
            var task = body(timers);
            var steps = 0;
            while (!task.IsCompleted)
            {
                if (!timers.Step())
                {
                    throw new InvalidOperationException("The test waits for something that never happens.");
                }

                if (++steps > maxSteps)
                {
                    throw new InvalidOperationException("The test does not finish.");
                }
            }

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <summary>Runs <paramref name="body"/> synchronously with this context (no waiting).</summary>
    public static void Run(Action<FakeTimers> body) => Run(t =>
    {
        body(t);
        return Task.CompletedTask;
    });

    private sealed class Timer(FakeTimers owner, double due, long seq, Action callback) : IDisposable
    {
        public double Due { get; } = due;

        public long Seq { get; } = seq;

        public Action Callback { get; } = callback;

        public void Dispose() => owner._timers.Remove(this);
    }
}
