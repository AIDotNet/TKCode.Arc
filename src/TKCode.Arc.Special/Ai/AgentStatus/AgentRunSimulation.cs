using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>
/// Plays an <see cref="AgentRunScenario"/> as a live run: after <see cref="StartDelay"/> of Ready it works through the
/// steps on the scenario's timing, ticks counts in place, waits for as long as it takes on an approval or a failure, and
/// reacts to Pause, Resume, Stop, Approve, Deny, Retry and Open run. Nothing runs for real: every action only changes this
/// object's state and says so through <see cref="Notice"/>. It holds no UI: give it to <see cref="AgentStatus.Simulation"/>
/// (which copies its state into the pill and routes the pill's buttons back to it), or read its state yourself after each
/// <see cref="Changed"/>. Timing uses the given <see cref="System.TimeProvider"/> (the system clock by default; tests pass
/// a manual one), and its callbacks run on the synchronization context that created the simulation (the UI thread).
/// </summary>
public sealed class AgentRunSimulation : IDisposable
{
    private enum Phase
    {
        NotStarted,
        Starting,
        Running,
        Waiting,
        Failed,
        Paused,
        Ended
    }

    private enum BeatKind
    {
        Tick,
        Fail,
        Complete
    }

    private readonly record struct Beat(double At, BeatKind Kind, int Value);

    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _context;
    private readonly AvaloniaList<AgentStatusStep> _steps = new();
    private readonly List<Beat> _beats = new();
    private AgentRunScenario _scenario;
    private ITimer? _timer;
    private long _timerToken;
    private Phase _phase;
    private int _index = -1;
    private int _attempt;
    private int _beatIndex;
    private double _attemptElapsed;
    private double _stepActive;
    private int _progress;
    private DateTimeOffset _segmentStart;
    private bool _disposed;

    /// <summary>Creates a simulation of <see cref="AgentStatusSamples.Coding"/> on the system clock.</summary>
    public AgentRunSimulation()
        : this(AgentStatusSamples.Coding)
    {
    }

    /// <summary>Creates a simulation of <paramref name="scenario"/>.</summary>
    /// <param name="scenario">The run to play.</param>
    /// <param name="timeProvider">The clock and timers (null: <see cref="System.TimeProvider.System"/>).</param>
    public AgentRunSimulation(AgentRunScenario scenario, TimeProvider? timeProvider = null)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _time = timeProvider ?? TimeProvider.System;
        _context = SynchronizationContext.Current;
    }

    /// <summary>
    /// The run to play. Setting another run starts over with <see cref="StartDelay"/> of Ready (once started); setting the
    /// same run does nothing.
    /// </summary>
    public AgentRunScenario Scenario
    {
        get => _scenario;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(value, _scenario))
            {
                return;
            }
            _scenario = value;
            if (_phase != Phase.NotStarted)
            {
                Restart(StartDelay);
            }
        }
    }

    /// <summary>Ready time before a run starts (0.9 seconds), on <see cref="Start"/> and after a scenario change.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromMilliseconds(900);

    /// <summary>Ready time before the run starts again after <see cref="Replay"/> (0.5 seconds).</summary>
    public TimeSpan ReplayDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>The clock and timers the simulation runs on.</summary>
    public TimeProvider TimeProvider => _time;

    /// <summary>The agent of the current run.</summary>
    public AgentModel Agent => _scenario.Agent;

    /// <summary>The run's state now.</summary>
    public AgentStatusRunState Status { get; private set; }

    /// <summary>What the agent does now (the request, the outcome or the failure in those states); null while Ready or stopped.</summary>
    public AgentStatusActivity? Activity { get; private set; }

    /// <summary>Every step of the current run, pending ones included; empty while Ready before a run.</summary>
    public IReadOnlyList<AgentStatusStep> Steps => _steps;

    /// <summary>When the current run started working; null while Ready.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>When the current run ended (done or stopped); null while it goes on.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>The latest action's message for screen readers ("Paused. The run holds where it is."), or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>Counts notices, so a repeated message is still a new one.</summary>
    internal int NoticeVersion { get; private set; }

    /// <summary>True once <see cref="Start"/> was called.</summary>
    public bool IsStarted => _phase != Phase.NotStarted;

    /// <summary>Raised after every change of the state (a beat, an action, a restart).</summary>
    public event EventHandler? Changed;

    /// <summary>Starts the run after <see cref="StartDelay"/> of Ready; does nothing once started.</summary>
    public void Start()
    {
        if (_phase == Phase.NotStarted && !_disposed)
        {
            Restart(StartDelay);
        }
    }

    /// <summary>Clears the run and plays it again after <see cref="ReplayDelay"/> of Ready.</summary>
    public void Replay() => Restart(ReplayDelay);

    /// <summary>Pauses a working run: the script, the timer and the step's time freeze.</summary>
    public void Pause()
    {
        if (_phase != Phase.Running)
        {
            return;
        }
        _attemptElapsed += (Now - _segmentStart).TotalMilliseconds;
        CancelTimer();
        _phase = Phase.Paused;
        Status = AgentStatusRunState.Paused;
        SetNotice(_scenario.PausedNotice);
        RaiseChanged();
    }

    /// <summary>Resumes a paused run where it stopped.</summary>
    public void Resume()
    {
        if (_phase != Phase.Paused)
        {
            return;
        }
        _segmentStart = Now;
        _phase = Phase.Running;
        Status = AgentStatusRunState.Working;
        SetNotice(_scenario.ResumedNotice);
        Schedule();
        RaiseChanged();
    }

    /// <summary>Stops a working, paused or waiting run: the current step is cancelled and the run ends.</summary>
    public void Stop()
    {
        if (_phase is not (Phase.Running or Phase.Paused or Phase.Waiting))
        {
            return;
        }
        var now = Now;
        CancelTimer();
        if (CurrentRow is { } row)
        {
            row.State = AgentStatusStepState.Skipped;
            row.Label = _scenario.StoppedStepLabel;
            row.Duration = null;
        }
        _phase = Phase.Ended;
        Status = AgentStatusRunState.Stopped;
        Activity = null;
        EndedAt = now;
        SetNotice(_scenario.StoppedNotice);
        RaiseChanged();
    }

    /// <summary>Approves the request the run waits on; the step then runs.</summary>
    public void Approve()
    {
        if (_phase != Phase.Waiting || CurrentStep is not { } step || CurrentRow is not { } row)
        {
            return;
        }
        var now = Now;
        row.State = AgentStatusStepState.Active;
        row.Label = step.ActiveLabel;
        row.StartedAt = now;
        Status = AgentStatusRunState.Working;
        Activity = new AgentStatusActivity(step.ActiveLabel, step.Icon);
        BeginAttempt(now, approved: true);
        SetNotice(_scenario.ApprovedNotice);
        RaiseChanged();
    }

    /// <summary>Denies the request the run waits on; the step is skipped and the run ends.</summary>
    public void Deny()
    {
        if (_phase != Phase.Waiting || CurrentStep is not { } step || CurrentRow is not { } row)
        {
            return;
        }
        row.State = AgentStatusStepState.Skipped;
        row.Label = step.SkippedLabel ?? step.WaitingLabel ?? step.ActiveLabel;
        row.Duration = null;
        _phase = Phase.Ended;
        Status = AgentStatusRunState.Done;
        Activity = new AgentStatusActivity(_scenario.DeniedOutcome ?? _scenario.Outcome);
        EndedAt = Now;
        SetNotice(_scenario.DeniedNotice);
        RaiseChanged();
    }

    /// <summary>Retries the failed step; it counts on from where it failed.</summary>
    public void Retry()
    {
        if (_phase != Phase.Failed || CurrentStep is not { } step || CurrentRow is not { } row)
        {
            return;
        }
        var now = Now;
        _attempt++;
        row.State = AgentStatusStepState.Active;
        row.Label = step.ActiveLabel;
        Status = AgentStatusRunState.Working;
        Activity = new AgentStatusActivity(step.ActiveLabel, step.Icon, step.ProgressTotal is null ? null : _progress,
            step.ProgressTotal);
        BeginAttempt(now, approved: false);
        SetNotice(_scenario.RetryNotice);
        RaiseChanged();
    }

    /// <summary>Says that the run log would open; the run is not affected.</summary>
    public void OpenRun()
    {
        SetNotice(_scenario.OpenRunNotice);
        RaiseChanged();
    }

    /// <summary>Stops the timers; the state stays as it is.</summary>
    public void Dispose()
    {
        _disposed = true;
        CancelTimer();
    }

    private DateTimeOffset Now => _time.GetUtcNow();

    private AgentRunScenarioStep? CurrentStep =>
        _index >= 0 && _index < _scenario.Steps.Count ? _scenario.Steps[_index] : null;

    private AgentStatusStep? CurrentRow => _index >= 0 && _index < _steps.Count ? _steps[_index] : null;

    private void SetNotice(string? notice)
    {
        Notice = notice;
        NoticeVersion++;
    }

    private void Restart(TimeSpan delay)
    {
        if (_disposed)
        {
            return;
        }
        CancelTimer();
        _phase = Phase.Starting;
        _index = -1;
        _attempt = 0;
        _beats.Clear();
        _beatIndex = 0;
        _steps.Clear();
        Status = AgentStatusRunState.Idle;
        Activity = null;
        StartedAt = null;
        EndedAt = null;
        Notice = null;
        NoticeVersion++;
        ScheduleIn(delay);
        RaiseChanged();
    }

    /// <summary>The Ready delay ended: every step is listed as pending and the first one starts.</summary>
    private void BeginRun(DateTimeOffset now)
    {
        _steps.Clear();
        foreach (var step in _scenario.Steps)
        {
            _steps.Add(new AgentStatusStep(step.Id, step.ActiveLabel, AgentStatusStepState.Pending, step.Icon));
        }
        StartedAt = now;
        EndedAt = null;
        if (_scenario.Steps.Count == 0)
        {
            Finish(now);
            return;
        }
        BeginStep(0, now);
    }

    private void BeginStep(int index, DateTimeOffset now)
    {
        _index = index;
        _attempt = 0;
        _stepActive = 0;
        var step = _scenario.Steps[index];
        var row = _steps[index];
        row.StartedAt = now;
        row.Duration = null;
        if (step.Request is not null)
        {
            row.State = AgentStatusStepState.Waiting;
            row.Label = step.WaitingLabel ?? step.ActiveLabel;
            _phase = Phase.Waiting;
            Status = AgentStatusRunState.NeedsInput;
            Activity = new AgentStatusActivity(step.Request, step.Icon);
            CancelTimer();
            return;
        }
        row.State = AgentStatusStepState.Active;
        row.Label = step.ActiveLabel;
        Status = AgentStatusRunState.Working;
        _progress = 1;
        Activity = new AgentStatusActivity(step.ActiveLabel, step.Icon, step.ProgressTotal is null ? null : 1, step.ProgressTotal);
        BeginAttempt(now, approved: false);
    }

    /// <summary>Lays out the beats of the current attempt and starts its clock at <paramref name="now"/>.</summary>
    private void BeginAttempt(DateTimeOffset now, bool approved)
    {
        var step = CurrentStep!;
        _beats.Clear();
        _beatIndex = 0;
        _attemptElapsed = 0;
        _segmentStart = now;
        _phase = Phase.Running;
        var interval = step.ProgressInterval.TotalMilliseconds;
        if (approved || step.ProgressTotal is not { } total || interval <= 0)
        {
            _beats.Add(new Beat(step.Duration.TotalMilliseconds, BeatKind.Complete, 0));
        }
        else if (_attempt == 0)
        {
            var failAt = step.FailAtProgress is { } f && f >= 1 && f < total ? f : (int?)null;
            var last = failAt ?? total;
            for (var k = 2; k <= last; k++)
            {
                _beats.Add(new Beat((k - 1) * interval, BeatKind.Tick, k));
            }
            _beats.Add(failAt is { } fail
                ? new Beat(fail * interval, BeatKind.Fail, fail)
                : new Beat(Math.Max(step.Duration.TotalMilliseconds, (total - 1) * interval), BeatKind.Complete, 0));
        }
        else
        {
            // The retried attempt counts on from the failure: the first tick follows almost at once (a fifth of an
            // interval), the rest spread evenly until one interval before the attempt ends.
            var from = Math.Clamp(_progress, 1, total);
            var duration = step.RetryDuration > TimeSpan.Zero ? step.RetryDuration.TotalMilliseconds : step.Duration.TotalMilliseconds;
            var remaining = total - from;
            var first = interval / 5;
            var lastAt = Math.Max(first, duration - interval);
            for (var i = 0; i < remaining; i++)
            {
                var at = remaining == 1 ? first : first + (lastAt - first) * i / (remaining - 1);
                _beats.Add(new Beat(at, BeatKind.Tick, from + 1 + i));
            }
            _beats.Add(new Beat(duration, BeatKind.Complete, 0));
        }
        _beats.Sort((a, b) => a.At.CompareTo(b.At));
        Schedule();
    }

    private void Schedule()
    {
        if (_phase == Phase.Running && _beatIndex < _beats.Count)
        {
            var elapsed = _attemptElapsed + (Now - _segmentStart).TotalMilliseconds;
            ScheduleIn(TimeSpan.FromMilliseconds(Math.Max(0, _beats[_beatIndex].At - elapsed)));
        }
    }

    private void ScheduleIn(TimeSpan due)
    {
        CancelTimer();
        if (_disposed)
        {
            return;
        }
        var token = ++_timerToken;
        _timer = _time.CreateTimer(_ => Dispatch(token), null, due, Timeout.InfiniteTimeSpan);
    }

    private void CancelTimer()
    {
        _timerToken++;
        _timer?.Dispose();
        _timer = null;
    }

    private void Dispatch(long token)
    {
        if (_context is null || SynchronizationContext.Current == _context)
        {
            OnTimer(token);
        }
        else
        {
            _context.Post(static state =>
            {
                var (self, t) = ((AgentRunSimulation, long))state!;
                self.OnTimer(t);
            }, (this, token));
        }
    }

    private void OnTimer(long token)
    {
        if (token != _timerToken || _disposed)
        {
            return;
        }
        _timer?.Dispose();
        _timer = null;
        var now = Now;
        if (_phase == Phase.Starting)
        {
            BeginRun(now);
            RaiseChanged();
            return;
        }
        if (_phase != Phase.Running)
        {
            return;
        }
        var changed = false;
        while (_phase == Phase.Running && _beatIndex < _beats.Count)
        {
            var elapsed = _attemptElapsed + (now - _segmentStart).TotalMilliseconds;
            var beat = _beats[_beatIndex];
            if (beat.At > elapsed + 0.5)
            {
                break;
            }
            _beatIndex++;
            changed = true;
            // Late timers do not drift the script: what follows a beat starts at the beat's own time.
            var at = now - TimeSpan.FromMilliseconds(Math.Max(0, elapsed - beat.At));
            Apply(beat, at);
        }
        Schedule();
        if (changed)
        {
            RaiseChanged();
        }
    }

    private void Apply(Beat beat, DateTimeOffset at)
    {
        var step = CurrentStep!;
        var row = CurrentRow!;
        switch (beat.Kind)
        {
            case BeatKind.Tick:
                _progress = beat.Value;
                Activity = Activity?.WithProgress(beat.Value) ?? new AgentStatusActivity(step.ActiveLabel, step.Icon, beat.Value, step.ProgressTotal);
                break;
            case BeatKind.Fail:
                _stepActive += beat.At;
                _phase = Phase.Failed;
                row.State = AgentStatusStepState.Failed;
                row.Label = step.FailedLabel ?? step.ActiveLabel;
                Status = AgentStatusRunState.Failed;
                Activity = new AgentStatusActivity(step.Failure ?? step.FailedLabel ?? step.ActiveLabel);
                CancelTimer();
                break;
            case BeatKind.Complete:
                _stepActive += beat.At;
                row.State = AgentStatusStepState.Done;
                row.Label = step.DoneLabel;
                row.Duration = TimeSpan.FromMilliseconds(Math.Round(_stepActive));
                if (_index + 1 < _scenario.Steps.Count)
                {
                    BeginStep(_index + 1, at);
                }
                else
                {
                    Finish(at);
                }
                break;
        }
    }

    private void Finish(DateTimeOffset at)
    {
        CancelTimer();
        _phase = Phase.Ended;
        Status = AgentStatusRunState.Done;
        Activity = new AgentStatusActivity(_scenario.Outcome);
        EndedAt = at;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
