namespace TKCode.Arc;

public partial class AgentOrchestrator
{
    private CancellationTokenSource? _scriptCts;
    private int _stepIndex;
    private TimeSpan _accumulated;
    private TimeSpan _resumedAt;
    private bool _resetting;

    /// <summary>The run time so far, paused time left out.</summary>
    public TimeSpan Elapsed => RunState == OrchestratorRunState.Running ? _accumulated + (Clock() - _resumedAt) : _accumulated;

    /// <summary>The index of the next script step (tests and tools).</summary>
    internal int StepIndex => _stepIndex;

    /// <summary>
    /// Starts the run: gives the lead the goal as its task and plays the script, if there is one. A paused run resumes;
    /// a finished one starts over. Does nothing while running.
    /// </summary>
    public void Start()
    {
        switch (RunState)
        {
            case OrchestratorRunState.Running:
                return;
            case OrchestratorRunState.Paused:
                Resume();
                return;
            case OrchestratorRunState.Completed:
            case OrchestratorRunState.Failed:
                Reset();
                break;
        }
        if (Lead is { } lead && string.IsNullOrEmpty(lead.Task) && EffectiveGoal is { Length: > 0 } goal)
        {
            lead.Task = goal;
        }
        _accumulated = TimeSpan.Zero;
        _resumedAt = Clock();
        SetRunState(OrchestratorRunState.Running);
        PlayScript();
    }

    /// <summary>Pauses a running script; the step it was waiting for starts its wait again on <see cref="Resume"/>.</summary>
    public void Pause()
    {
        if (RunState != OrchestratorRunState.Running)
        {
            return;
        }
        _accumulated += Clock() - _resumedAt;
        StopScript();
        SetRunState(OrchestratorRunState.Paused);
    }

    /// <summary>Resumes a paused run.</summary>
    public void Resume()
    {
        if (RunState != OrchestratorRunState.Paused)
        {
            return;
        }
        _resumedAt = Clock();
        SetRunState(OrchestratorRunState.Running);
        PlayScript();
    }

    /// <summary>What the main button does: start, pause, resume, or start over after the end.</summary>
    public void Toggle()
    {
        if (RunState == OrchestratorRunState.Running)
        {
            Pause();
        }
        else
        {
            Start();
        }
    }

    /// <summary>Stops the run and clears it: every agent idle with no task, no messages, no lines between sub-agents.</summary>
    public void Reset()
    {
        StopScript();
        _stepIndex = 0;
        _accumulated = TimeSpan.Zero;
        _resetting = true;
        try
        {
            foreach (var agent in _ordered)
            {
                agent.ResetRun();
            }
        }
        finally
        {
            _resetting = false;
        }
        _messages.Clear();
        _graph?.ClearRun();
        SetRunState(OrchestratorRunState.Idle);
        UpdateTexts();
    }

    /// <summary>Ends the run as completed (your code drives the run, or you cut a script short).</summary>
    public void Complete() => Finish(OrchestratorRunState.Completed);

    /// <summary>Ends the run as failed.</summary>
    public void Fail() => Finish(OrchestratorRunState.Failed);

    private void Finish(OrchestratorRunState state)
    {
        if (RunState is OrchestratorRunState.Idle or OrchestratorRunState.Completed or OrchestratorRunState.Failed)
        {
            return;
        }
        if (RunState == OrchestratorRunState.Running)
        {
            _accumulated += Clock() - _resumedAt;
        }
        StopScript();
        SetRunState(state);
    }

    private void StopScript()
    {
        if (_scriptCts is { } cts)
        {
            _scriptCts = null;
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async void PlayScript()
    {
        var script = EffectiveScript;
        if (script is null)
        {
            return;
        }
        StopScript();
        var cts = new CancellationTokenSource();
        _scriptCts = cts;
        var token = cts.Token;
        try
        {
            while (_stepIndex < script.Count)
            {
                var step = script[_stepIndex];
                var speed = Speed > 0 ? Speed : 1;
                var delay = TimeSpan.FromTicks((long)(step.Delay.Ticks / speed));
                if (delay > TimeSpan.Zero)
                {
                    await Delay(delay, token);
                }
                if (token.IsCancellationRequested || !ReferenceEquals(_scriptCts, cts))
                {
                    return;
                }
                _stepIndex++;
                Apply(step);
            }
            if (ReferenceEquals(_scriptCts, cts))
            {
                _scriptCts = null;
                cts.Dispose();
                Complete();
            }
        }
        catch (OperationCanceledException)
        {
            // Paused, reset or detached: the step index says where to go on.
        }
    }

    private void Apply(OrchestratorStep step)
    {
        if (step.Message is { } message && IndexOf(message.From) >= 0 && IndexOf(message.To) >= 0)
        {
            Post(message.Copy());
        }
        if (step.AgentId is { } id && Find(id) is { } agent)
        {
            if (step.Activity is { } activity)
            {
                agent.Activity = activity;
            }
            if (step.Result is { } result)
            {
                agent.Result = result;
            }
            if (step.Status is { } status)
            {
                agent.Status = status;
            }
        }
    }

    /// <summary>
    /// Posts a message: it joins the log, a dot travels the line between the two agents, and its kind updates them (a task
    /// sets the receiver working on it, a progress note becomes the sender's activity, a question makes the sender wait,
    /// an answer sends the receiver back to work, a result or an error ends the sender's part). Posting while idle starts
    /// the run when there is no script.
    /// </summary>
    /// <param name="message">The message; it must not have been posted before.</param>
    /// <exception cref="ArgumentException">An agent id is unknown, or the message was already posted.</exception>
    public void Post(OrchestratorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var from = IndexOf(message.From);
        var to = IndexOf(message.To);
        if (from < 0 || to < 0)
        {
            throw new ArgumentException($"Unknown agent id '{(from < 0 ? message.From : message.To)}'.", nameof(message));
        }
        if (message.Sequence != 0)
        {
            throw new ArgumentException("The message was already posted; post a Copy().", nameof(message));
        }
        if (RunState == OrchestratorRunState.Idle && EffectiveScript is null)
        {
            Start();
        }
        message.Sequence = _messages.Count + 1;
        message.Elapsed = Elapsed;
        var sender = _ordered[from];
        var receiver = _ordered[to];
        switch (message.Kind)
        {
            case OrchestratorMessageKind.Delegate:
                receiver.Task = message.Text;
                receiver.Activity = null;
                receiver.Result = null;
                receiver.Status = OrchestratorAgentStatus.Working;
                break;
            case OrchestratorMessageKind.Progress:
                sender.Activity = message.Text;
                if (sender.Status is OrchestratorAgentStatus.Idle or OrchestratorAgentStatus.Thinking or OrchestratorAgentStatus.Waiting)
                {
                    sender.Status = OrchestratorAgentStatus.Working;
                }
                break;
            case OrchestratorMessageKind.Question:
                sender.Activity = message.Text;
                if (sender.Status is not (OrchestratorAgentStatus.Done or OrchestratorAgentStatus.Failed))
                {
                    sender.Status = OrchestratorAgentStatus.Waiting;
                }
                break;
            case OrchestratorMessageKind.Answer:
                if (receiver.Status is not (OrchestratorAgentStatus.Done or OrchestratorAgentStatus.Failed))
                {
                    receiver.Activity = null;
                    receiver.Status = OrchestratorAgentStatus.Working;
                }
                break;
            case OrchestratorMessageKind.Result:
                sender.Result = message.Text;
                sender.Activity = null;
                sender.Status = OrchestratorAgentStatus.Done;
                break;
            case OrchestratorMessageKind.Error:
                sender.Result = message.Text;
                sender.Activity = null;
                sender.Status = OrchestratorAgentStatus.Failed;
                break;
        }
        _messages.Add(message);
        _graph?.Launch(from, to, message.Kind);
        OnMessageAdded(message);
        RaiseEvent(new OrchestratorMessageEventArgs(MessagePostedEvent, this, message));
    }

    /// <summary>Sets an agent's status and, optionally, what it is doing now.</summary>
    /// <param name="agentId">The agent's id.</param>
    /// <param name="status">The new status.</param>
    /// <param name="activity">What it is doing now; null keeps the current activity.</param>
    /// <exception cref="ArgumentException">The id is unknown.</exception>
    public void SetStatus(string agentId, OrchestratorAgentStatus status, string? activity = null)
    {
        var agent = Find(agentId) ?? throw new ArgumentException($"Unknown agent id '{agentId}'.", nameof(agentId));
        if (activity is not null)
        {
            agent.Activity = activity;
        }
        agent.Status = status;
    }

    private void SetRunState(OrchestratorRunState state, bool announce = true)
    {
        var old = RunState;
        if (old == state)
        {
            return;
        }
        RunState = state;
        UpdateRunParts();
        RaiseEvent(new OrchestratorRunStateEventArgs(RunStateChangedEvent, this, old, state));
        if (announce)
        {
            _announcer.Announce(Labels.RunAnnouncement(state));
        }
    }
}
