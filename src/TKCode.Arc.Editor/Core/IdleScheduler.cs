// Adaptation of vscode (src/vs/base/common/async.ts: runWhenGlobalIdle, IdleDeadline and RunOnceScheduler's timer;
// src/vs/base/common/platform.ts: setTimeout0; src/vs/base/common/stopwatch.ts; commit b57d5c9e), MIT license.
//
// The engine has no UI thread and no event loop of its own, so the work upstream schedules on the browser's (background
// tokenization in idle time slices, the debounce of the visible lines) goes through this scheduler, which the creator of
// a model hands in. Differences from upstream:
//  - runWhenGlobalIdle and setTimeout0 are the same hook here, RunWhenIdle: run a callback later on the owning thread
//    when it has nothing more urgent to do (in Avalonia: Dispatcher.UIThread.Post at Background priority). Upstream
//    yields with setTimeout0 between 1 ms slices so that input gets in; the hook does the same;
//  - the deadline of an idle callback is IdleTimeBudget milliseconds from when it starts (a browser's requestIdleCallback
//    gives up to 50 ms, the default here);
//  - RunAfter is the timer of RunOnceScheduler; without one it falls back to RunWhenIdle;
//  - the clock (Now, in milliseconds) is injected as well, so that tests can make time slices deterministic;
//  - None never runs anything: a model created without a scheduler tokenizes only on demand (ForceTokenization,
//    the viewport of an attached view once it is stabilized), as upstream does in its synchronous tests.

using System.Diagnostics;

namespace TKCode.Arc.Editor;

/// <summary>An idle callback's deadline (upstream's IdleDeadline).</summary>
internal readonly struct IdleDeadline(IdleScheduler scheduler, double endTime)
{
    public bool DidTimeout => false;

    /// <summary>The milliseconds left before the callback should give the thread back.</summary>
    public double TimeRemaining() => Math.Max(0, endTime - scheduler.Now());
}

/// <summary>
/// Runs a model's deferred work on the thread that owns the model: idle callbacks, timers and a clock. Immutable; one
/// instance may serve any number of models.
/// </summary>
internal sealed class IdleScheduler
{
    private static readonly double s_tickToMs = 1000.0 / Stopwatch.Frequency;

    private readonly Func<Action, IDisposable>? _runWhenIdle;
    private readonly Func<TimeSpan, Action, IDisposable>? _runAfter;
    private readonly Func<double> _clock;

    /// <param name="runWhenIdle">
    /// Runs the callback once, later, on the owning thread, when it is idle; disposing the result cancels it. Null: never.
    /// </param>
    /// <param name="clock">A monotonic clock in milliseconds; a <see cref="Stopwatch"/> by default.</param>
    /// <param name="idleTimeBudget">The milliseconds an idle callback may use (upstream: the IdleDeadline's).</param>
    /// <param name="runAfter">Runs the callback once on the owning thread after a delay; <paramref name="runWhenIdle"/> by default.</param>
    public IdleScheduler(
        Func<Action, IDisposable>? runWhenIdle,
        Func<double>? clock = null,
        double idleTimeBudget = 50,
        Func<TimeSpan, Action, IDisposable>? runAfter = null)
    {
        _runWhenIdle = runWhenIdle;
        _runAfter = runAfter;
        _clock = clock ?? DefaultClock;
        IdleTimeBudget = idleTimeBudget;
    }

    /// <summary>A scheduler that never runs anything (the default of a model created without one).</summary>
    public static IdleScheduler None { get; } = new(null);

    /// <summary>The milliseconds an idle callback may use.</summary>
    public double IdleTimeBudget { get; }

    /// <summary>The clock, in milliseconds.</summary>
    public double Now() => _clock();

    /// <summary>Upstream's runWhenGlobalIdle: runs the callback when the owning thread is idle, with its deadline.</summary>
    public IDisposable RunWhenIdle(Action<IdleDeadline> callback)
    {
        if (_runWhenIdle is null)
        {
            return EmptyDisposable.Instance;
        }

        return _runWhenIdle(() => callback(new IdleDeadline(this, Now() + IdleTimeBudget)));
    }

    /// <summary>Upstream's setTimeout0: gives the thread back and continues as soon as possible.</summary>
    public IDisposable SetTimeout0(Action callback) =>
        _runWhenIdle is null ? EmptyDisposable.Instance : _runWhenIdle(callback);

    /// <summary>Runs the callback after a delay (the timer of upstream's RunOnceScheduler).</summary>
    public IDisposable RunAfter(TimeSpan delay, Action callback)
    {
        if (_runAfter is not null)
        {
            return _runAfter(delay, callback);
        }

        return SetTimeout0(callback);
    }

    private static double DefaultClock() => Stopwatch.GetTimestamp() * s_tickToMs;

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>Upstream's StopWatch on an <see cref="IdleScheduler"/>'s clock.</summary>
internal readonly struct IdleStopWatch(IdleScheduler scheduler)
{
    private readonly double _start = scheduler.Now();

    /// <summary>The milliseconds since the stop watch was created.</summary>
    public double Elapsed() => scheduler.Now() - _start;
}
