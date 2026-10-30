namespace TKCode.Arc;

/// <summary>What <see cref="AiWorkflowRunner"/> needs from the workflow it runs.</summary>
internal interface IAiWorkflowRunHost
{
    /// <summary>The steps on screen (read again at every step, so steps added during a run are reached).</summary>
    IReadOnlyList<AiWorkflowStep> RunSteps { get; }

    /// <summary>The app's runner, or null to simulate.</summary>
    AiWorkflowStepRunner? Runner { get; }

    AiWorkflowLabels Labels { get; }

    /// <summary>The dot starts gliding down the connector into <paramref name="step"/>.</summary>
    void OnTravel(AiWorkflowStep step);

    /// <summary>A step's state changed.</summary>
    void OnStepChanged(AiWorkflowStep step);

    /// <summary>A branch picked <paramref name="pathId"/>.</summary>
    void OnRouted(AiWorkflowStep branch, string pathId);

    /// <summary>The run started, stopped, passed or failed.</summary>
    void OnRunChanged();

    void Announce(string text);

    /// <summary>The run passed, or stopped on a failed step.</summary>
    void OnCompleted(AiWorkflowRunStatus status, TimeSpan duration, IReadOnlyDictionary<string, AiWorkflowOutput> outputs, string? failedStepId);
}

/// <summary>The state of one step in a test run.</summary>
internal sealed class AiWorkflowStepState
{
    public AiWorkflowStepStatus Status { get; set; }

    public TimeSpan? Duration { get; set; }

    public AiWorkflowOutput? Output { get; set; }

    public string? Error { get; set; }

    public int Attempts { get; set; }
}

/// <summary>
/// The test run of an <see cref="AiWorkflow"/>, one thing at a time: the dot travels down the connector into the next step
/// (<see cref="TravelDuration"/>), the step runs (the app's <see cref="AiWorkflowStepRunner"/>, or a simulation from the
/// step's output and duration), then after <see cref="Gap"/> the next one starts. A branch routes the run into one
/// of its paths and marks the others Skipped. A failed step stops the run until <see cref="Retry"/>, which resumes from
/// it. Timers come from an <see cref="IAgentPlanScheduler"/> so tests can step them.
/// </summary>
internal sealed class AiWorkflowRunner
{
    /// <summary>How long the dot takes down a connector (also the pause before a step starts when motion is reduced).</summary>
    public static readonly TimeSpan TravelDuration = TimeSpan.FromMilliseconds(240);

    /// <summary>The pause after a step finishes, while its output opens, before the dot moves on.</summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(220);

    /// <summary>A simulated step without a duration of its own.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(600);

    private readonly IAiWorkflowRunHost _host;
    private readonly Dictionary<string, AiWorkflowStepState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AiWorkflowOutput> _outputs = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private IDisposable? _pending;
    private int _generation;
    private TimeSpan _startedAt;
    private TimeSpan _stepStartedAt;

    public AiWorkflowRunner(IAiWorkflowRunHost host, IAgentPlanScheduler scheduler)
    {
        _host = host;
        Scheduler = scheduler;
    }

    public IAgentPlanScheduler Scheduler { get; set; }

    /// <summary>True from Start until the run passes, fails or is stopped.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True once a run has started (until <see cref="Reset"/>).</summary>
    public bool HasRun { get; private set; }

    /// <summary>The step a failed run stopped on.</summary>
    public string? FailedStepId { get; private set; }

    /// <summary>How the last run ended (null while running, before any run, or after Stop).</summary>
    public AiWorkflowRunStatus? LastStatus { get; private set; }

    /// <summary>The last finished run's duration.</summary>
    public TimeSpan LastDuration { get; private set; }

    /// <summary>True after Stop until the next run.</summary>
    public bool WasStopped { get; private set; }

    /// <summary>The step the dot is gliding into, or null.</summary>
    public string? TravelingTo { get; private set; }

    /// <summary>The path the branch took in this run.</summary>
    public string? RoutePathId { get; private set; }

    /// <summary>The outputs so far.</summary>
    public IReadOnlyDictionary<string, AiWorkflowOutput> Outputs => _outputs;

    public AiWorkflowStepState State(string id)
    {
        if (!_states.TryGetValue(id, out var state))
        {
            state = new AiWorkflowStepState();
            _states[id] = state;
        }
        return state;
    }

    /// <summary>The state of a step, or null when the run has not touched it.</summary>
    public AiWorkflowStepState? Peek(string id) => _states.TryGetValue(id, out var s) ? s : null;

    public AiWorkflowStepStatus StatusOf(string id) => _states.TryGetValue(id, out var s) ? s.Status : AiWorkflowStepStatus.Idle;

    /// <summary>Starts a new run from the first step (stopping one in progress).</summary>
    public void Start()
    {
        var steps = _host.RunSteps;
        if (steps.Count == 0)
        {
            return;
        }
        Cancel();
        ResetStates();
        HasRun = true;
        IsRunning = true;
        WasStopped = false;
        LastStatus = null;
        FailedStepId = null;
        _startedAt = Scheduler.Now;
        _host.OnRunChanged();
        _host.Announce(_host.Labels.AnnounceStarted);
        Begin(steps[0], _generation);
    }

    /// <summary>Stops the run: the running step goes back to idle and the app's runner is cancelled.</summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }
        Cancel();
        IsRunning = false;
        WasStopped = true;
        foreach (var (id, state) in _states)
        {
            if (state.Status == AiWorkflowStepStatus.Running)
            {
                state.Status = AiWorkflowStepStatus.Idle;
                if (Find(id) is { } step)
                {
                    _host.OnStepChanged(step);
                }
            }
        }
        if (TravelingTo is not null)
        {
            TravelingTo = null;
        }
        _host.OnRunChanged();
        _host.Announce(_host.Labels.AnnounceStopped);
    }

    /// <summary>Runs the failed step again and carries on from it.</summary>
    public void Retry()
    {
        if (IsRunning || FailedStepId is not { } id || Find(id) is not { } step)
        {
            return;
        }
        Cancel();
        IsRunning = true;
        LastStatus = null;
        FailedStepId = null;
        _host.OnRunChanged();
        Begin(step, _generation);
    }

    /// <summary>Forgets the last run (every step idle) and cancels one in progress without announcing.</summary>
    public void Reset()
    {
        Cancel();
        var wasRunning = IsRunning;
        IsRunning = false;
        HasRun = false;
        WasStopped = false;
        LastStatus = null;
        FailedStepId = null;
        ResetStates();
        if (wasRunning)
        {
            _host.OnRunChanged();
        }
    }

    /// <summary>Drops the state of steps that are gone.</summary>
    public void Forget(string id)
    {
        _states.Remove(id);
        _outputs.Remove(id);
    }

    private void Cancel()
    {
        _generation++;
        _pending?.Dispose();
        _pending = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        TravelingTo = null;
    }

    private void ResetStates()
    {
        var changed = _states.Where(p => p.Value.Status != AiWorkflowStepStatus.Idle || p.Value.Output is not null).Select(p => p.Key).ToList();
        _states.Clear();
        _outputs.Clear();
        RoutePathId = null;
        foreach (var id in changed)
        {
            if (Find(id) is { } step)
            {
                _host.OnStepChanged(step);
            }
        }
    }

    private AiWorkflowStep? Find(string id) => AiWorkflowFormat.All(_host.RunSteps).FirstOrDefault(s => s.Id == id);

    private void Travel(AiWorkflowStep step, int generation)
    {
        TravelingTo = step.Id;
        _host.OnTravel(step);
        _pending = Scheduler.Schedule(TravelDuration, () =>
        {
            if (generation != _generation)
            {
                return;
            }
            TravelingTo = null;
            Begin(step, generation);
        });
    }

    private void Begin(AiWorkflowStep step, int generation)
    {
        var state = State(step.Id);
        state.Status = AiWorkflowStepStatus.Running;
        state.Error = null;
        state.Output = null;
        state.Duration = null;
        state.Attempts++;
        _stepStartedAt = Scheduler.Now;
        _host.OnStepChanged(step);
        if (_host.Runner is { } runner)
        {
            _cts = new CancellationTokenSource();
            var context = new AiWorkflowRunStepContext(_cts.Token, new Dictionary<string, AiWorkflowOutput>(_outputs), state.Attempts);
            _ = RunHostedAsync(runner, step, context, generation);
            return;
        }
        var duration = step.Duration ?? DefaultDuration;
        _pending = Scheduler.Schedule(duration, () =>
        {
            if (generation != _generation)
            {
                return;
            }
            if (!string.IsNullOrEmpty(step.Error) && state.Attempts == 1)
            {
                Fail(step, step.Error, duration);
            }
            else
            {
                Complete(step, step.Output, duration, generation);
            }
        });
    }

    private async Task RunHostedAsync(AiWorkflowStepRunner runner, AiWorkflowStep step, AiWorkflowRunStepContext context, int generation)
    {
        AiWorkflowOutput? output;
        try
        {
            output = await runner(step, context);
        }
        catch (OperationCanceledException) when (context.Cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (generation == _generation)
            {
                Fail(step, string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message, Scheduler.Now - _stepStartedAt);
            }
            return;
        }
        if (generation == _generation)
        {
            Complete(step, output, Scheduler.Now - _stepStartedAt, generation);
        }
    }

    private void Complete(AiWorkflowStep step, AiWorkflowOutput? output, TimeSpan duration, int generation)
    {
        _pending = null;
        var state = State(step.Id);
        state.Status = AiWorkflowStepStatus.Done;
        state.Duration = duration;
        state.Output = output;
        if (output is not null)
        {
            _outputs[step.Id] = output;
        }
        _host.OnStepChanged(step);
        var labels = _host.Labels;
        string? route = null;
        if (step.Kind == AiWorkflowStepKind.Branch && step.Paths.Count > 0)
        {
            var picked = output is AiWorkflowRouteOutput r && step.Paths.Any(p => p.Id == r.PathId) ? r.PathId : step.Paths[0].Id;
            RoutePathId = picked;
            foreach (var path in step.Paths.Where(p => p.Id != picked))
            {
                foreach (var skipped in AiWorkflowFormat.All(path.Steps))
                {
                    State(skipped.Id).Status = AiWorkflowStepStatus.Skipped;
                    _host.OnStepChanged(skipped);
                }
            }
            route = step.Paths.First(p => p.Id == picked).Label;
            _host.OnRouted(step, picked);
        }
        var said = route is not null ? labels.Route(route) : output?.Summary;
        _host.Announce(labels.AnnounceStepDone(step.Title, duration, said));
        var next = AiWorkflowFormat.Next(_host.RunSteps, step, RoutePathId);
        if (next is null)
        {
            Finish();
            return;
        }
        _pending = Scheduler.Schedule(Gap, () =>
        {
            if (generation == _generation)
            {
                Travel(next, generation);
            }
        });
    }

    private void Fail(AiWorkflowStep step, string message, TimeSpan duration)
    {
        _pending = null;
        var state = State(step.Id);
        state.Status = AiWorkflowStepStatus.Failed;
        state.Error = message;
        state.Duration = duration;
        IsRunning = false;
        FailedStepId = step.Id;
        LastStatus = AiWorkflowRunStatus.Failed;
        LastDuration = Scheduler.Now - _startedAt;
        _host.OnStepChanged(step);
        _host.OnRunChanged();
        _host.Announce(_host.Labels.AnnounceStepFailed(step.Title, message));
        _host.OnCompleted(AiWorkflowRunStatus.Failed, LastDuration, new Dictionary<string, AiWorkflowOutput>(_outputs), step.Id);
    }

    private void Finish()
    {
        IsRunning = false;
        LastStatus = AiWorkflowRunStatus.Passed;
        LastDuration = Scheduler.Now - _startedAt;
        _host.OnRunChanged();
        var labels = _host.Labels;
        var said = labels.AnnouncePassed(LastDuration);
        _host.Announce(_host.Runner is null ? said + " " + labels.AnnounceSimulated : said);
        _host.OnCompleted(AiWorkflowRunStatus.Passed, LastDuration, new Dictionary<string, AiWorkflowOutput>(_outputs), null);
    }
}
