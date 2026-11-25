// Upstream's runWithFakedTimers (src/vs/base/test/common/timeTravelScheduler.ts, commit b57d5c9e, MIT license) for
// the inline track's tests: an IdleScheduler whose timers and idle callbacks run only when the test advances virtual
// time. Provider tasks complete synchronously in these tests, so time is the only thing that waits.

namespace TKCode.Arc.Editor.Tests;

internal sealed class InlineFakeTimers
{
    private readonly List<(double Due, long Order, Action Run, Cancel Handle)> _timers = new();
    private long _order;

    public InlineFakeTimers()
    {
        Scheduler = new IdleScheduler(RunWhenIdle, () => Now, idleTimeBudget: 50, runAfter: RunAfter);
    }

    public IdleScheduler Scheduler { get; }

    public double Now { get; private set; }

    public int Pending => _timers.Count(t => !t.Handle.Cancelled);

    private IDisposable RunWhenIdle(Action action) => Add(0, action);

    private IDisposable RunAfter(TimeSpan delay, Action action) => Add(delay.TotalMilliseconds, action);

    private IDisposable Add(double delay, Action action)
    {
        var handle = new Cancel();
        _timers.Add((Now + delay, _order++, action, handle));
        return handle;
    }

    /// <summary>Runs every timer due within <paramref name="ms"/> milliseconds, in order, moving the clock along.</summary>
    public void Advance(double ms)
    {
        var end = Now + ms;
        while (true)
        {
            var next = _timers
                .Where(t => !t.Handle.Cancelled && t.Due <= end)
                .OrderBy(t => t.Due).ThenBy(t => t.Order)
                .Select(t => ((double Due, long Order, Action Run, Cancel Handle)?)t)
                .FirstOrDefault();
            if (next is not { } timer)
            {
                break;
            }

            _timers.Remove(timer);
            Now = Math.Max(Now, timer.Due);
            timer.Run();
        }

        _timers.RemoveAll(t => t.Handle.Cancelled);
        Now = end;
    }

    internal sealed class Cancel : IDisposable
    {
        public bool Cancelled { get; private set; }

        public void Dispose() => Cancelled = true;
    }
}
