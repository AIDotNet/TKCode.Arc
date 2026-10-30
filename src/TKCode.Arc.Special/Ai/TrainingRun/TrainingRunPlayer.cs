using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The player of a <see cref="TrainingRunSimulation"/>: a fine-tuning run over 2,400 steps in three epochs, named and
/// placed by the simulation's data (the hardware it waits for and trains on). It queues for 2.8 seconds, starts up for 1.6 seconds,
/// then trains at 48 steps a second (about 50 seconds for the run; every step stands for 0.42 seconds of job time, so the
/// times read like a real 17 minute run), with a loss sample every 10 steps, a validation sample every 100, checkpoints,
/// evals, a warning at a loss spike and a done event. The OOM scenario fails once at step 940; Retry from checkpoint rolls
/// back to step 800. The loss curve is procedural.
/// <para>
/// It holds no UI and no timer: the card advances it from a Core <c>LiveLoop</c> (which pauses while the card is off
/// screen), and tests call <see cref="Advance"/> directly. Pure and deterministic: the noise is a hash of the step.
/// </para>
/// </summary>
internal sealed class TrainingRunPlayer
{
    public const int TotalSteps = 2400;
    public const int Epochs = 3;
    public const double QueueSeconds = 2.8;
    public const double StartupSeconds = 1.6;
    public const double StepsPerSecond = 48;
    public const double JobSecondsPerStep = 0.42;
    public const double StartupJobSeconds = 38;
    public const double QueuedEtaSeconds = 1120;
    public const int SampleEvery = 10;
    public const int ValidationEvery = 100;
    public const int CheckpointEvery = 400;
    public const int OomStep = 940;
    public const int SpikeStep = 1310;
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    private readonly List<TrainingRunLossPoint> _loss = [];
    private readonly List<TrainingRunEvent> _events = [];
    private double _phaseTime;
    private double _stepPosition;
    private int _lastCheckpoint;
    private bool _oomDone;
    private int _nextEventId;
    private bool _started;

    private readonly string _hardware;

    public TrainingRunPlayer(TrainingRunScenario scenario = TrainingRunScenario.Healthy, TrainingRunSimulation? data = null)
    {
        Data = data ?? new TrainingRunSimulation();
        _hardware = Data.Hardware is { Length: > 0 } hardware ? hardware : "GPUs";
        Scenario = scenario;
        Reset();
    }

    /// <summary>The run's data: its name, model, hardware and endpoint.</summary>
    public TrainingRunSimulation Data { get; }

    public TrainingRunScenario Scenario { get; private set; }

    public TrainingRunStatus Status { get; private set; }

    public int Step { get; private set; }

    public IReadOnlyList<TrainingRunLossPoint> Loss => _loss;

    public IReadOnlyList<TrainingRunEvent> Events => _events;

    public double? LearningRate { get; private set; }

    public TimeSpan? Elapsed { get; private set; }

    public TimeSpan? Eta { get; private set; }

    public string? Activity { get; private set; }

    public string? ErrorText { get; private set; }

    /// <summary>Counts changes of <see cref="Loss"/> (appends, rollbacks, resets).</summary>
    public int LossVersion { get; private set; }

    /// <summary>Counts changes of <see cref="Events"/>.</summary>
    public int EventsVersion { get; private set; }

    /// <summary>True while time moves the run (queued or running).</summary>
    public bool IsMoving => Status is TrainingRunStatus.Queued or TrainingRunStatus.Running;

    /// <summary>Plays <paramref name="scenario"/> from the start (queued).</summary>
    public void Play(TrainingRunScenario scenario)
    {
        Scenario = scenario;
        Reset();
    }

    /// <summary>Back to the start: queued, no samples, no events.</summary>
    public void Reset()
    {
        _loss.Clear();
        _events.Clear();
        LossVersion++;
        EventsVersion++;
        _phaseTime = 0;
        _stepPosition = 0;
        _lastCheckpoint = 0;
        _oomDone = false;
        _started = false;
        Step = 0;
        Status = TrainingRunStatus.Queued;
        LearningRate = null;
        Elapsed = null;
        Eta = TimeSpan.FromSeconds(QueuedEtaSeconds);
        Activity = "Waiting for " + _hardware + " · 2nd in queue";
        ErrorText = null;
    }

    /// <summary>Moves the run on by <paramref name="seconds"/> of wall time. Returns true when anything changed.</summary>
    public bool Advance(double seconds)
    {
        if (!IsMoving || seconds <= 0)
        {
            return false;
        }
        seconds = Math.Min(seconds, 0.25);
        _phaseTime += seconds;
        if (Status == TrainingRunStatus.Queued)
        {
            if (_phaseTime >= QueueSeconds)
            {
                _phaseTime = 0;
                Status = TrainingRunStatus.Running;
                Eta = TimeSpan.FromSeconds(TotalSteps * JobSecondsPerStep + StartupJobSeconds);
                Elapsed = TimeSpan.Zero;
                Activity = "Loading the base model onto " + _hardware;
            }
            else if (_phaseTime >= QueueSeconds / 2)
            {
                Activity = "Waiting for " + _hardware + " · next in queue";
            }
            return true;
        }
        if (!_started)
        {
            // Starting up: running, no samples yet.
            var share = Math.Min(1, _phaseTime / StartupSeconds);
            Elapsed = TimeSpan.FromSeconds(share * StartupJobSeconds);
            Eta = TimeSpan.FromSeconds(TotalSteps * JobSecondsPerStep + (1 - share) * StartupJobSeconds);
            if (_phaseTime >= StartupSeconds)
            {
                _started = true;
                Activity = null;
                AddEvent(TrainingRunEventKind.Start, "Training started on " + _hardware, 0);
                Emit(0);
            }
            return true;
        }
        var before = Step;
        _stepPosition = Math.Min(TotalSteps, _stepPosition + seconds * StepsPerSecond);
        var after = (int)Math.Floor(_stepPosition);
        for (var step = before + 1; step <= after; step++)
        {
            Step = step;
            Emit(step);
            if (Status != TrainingRunStatus.Running)
            {
                return true;
            }
        }
        Step = after;
        UpdateClock();
        return true;
    }

    /// <summary>What happens at <paramref name="step"/>: samples, events, the failure and the end.</summary>
    private void Emit(int step)
    {
        if (Scenario == TrainingRunScenario.Oom && !_oomDone && step == OomStep)
        {
            _oomDone = true;
            Status = TrainingRunStatus.Failed;
            ErrorText = "CUDA out of memory on GPU 3 at step 940. Lower the micro-batch size or retry from the last checkpoint.";
            Eta = null;
            AddEvent(TrainingRunEventKind.Error, "Out of memory on GPU 3", step);
            return;
        }
        if (step % SampleEvery == 0 && step > 0)
        {
            double? validation = step % ValidationEvery == 0 ? Math.Round(ValidationLoss(step), 4) : null;
            _loss.Add(new TrainingRunLossPoint(step, Math.Round(TrainLoss(step), 4), validation));
            LossVersion++;
        }
        var perEpoch = TotalSteps / Epochs;
        if (step == 100)
        {
            AddEvent(TrainingRunEventKind.Info, "Warmup finished, learning rate at peak", step);
        }
        else if (step == SpikeStep && Scenario == TrainingRunScenario.Healthy)
        {
            AddEvent(TrainingRunEventKind.Warning, "Loss spike, gradients clipped", step);
        }
        else if (step > 0 && step % perEpoch == 0)
        {
            var epoch = step / perEpoch;
            _lastCheckpoint = step;
            if (step == TotalSteps)
            {
                Status = TrainingRunStatus.Completed;
                Eta = null;
                UpdateClock();
                AddEvent(TrainingRunEventKind.Done, "Training complete, final checkpoint saved", step);
                return;
            }
            AddEvent(TrainingRunEventKind.Epoch, string.Create(s_invariant, $"Epoch {epoch} of {Epochs} complete, checkpoint saved"), step);
        }
        else if (step > 0 && step % CheckpointEvery == 0)
        {
            _lastCheckpoint = step;
            AddEvent(TrainingRunEventKind.Checkpoint, "Checkpoint saved", step);
        }
        else if (step > perEpoch && step % perEpoch == 20)
        {
            var loss = TrainingRunFormat.Loss(ValidationLoss(step - 20));
            AddEvent(TrainingRunEventKind.Eval, "Eval · val loss " + loss, step - 20);
        }
    }

    private void UpdateClock()
    {
        LearningRate = LearningRateAt(Step);
        Elapsed = TimeSpan.FromSeconds(StartupJobSeconds + _stepPosition * JobSecondsPerStep);
        if (Status == TrainingRunStatus.Running)
        {
            Eta = TimeSpan.FromSeconds((TotalSteps - _stepPosition) * JobSecondsPerStep);
        }
    }

    private void AddEvent(TrainingRunEventKind kind, string label, int? step)
    {
        _nextEventId++;
        _events.Add(new TrainingRunEvent("e" + _nextEventId.ToString(s_invariant), kind, label, step));
        EventsVersion++;
    }

    public void Pause()
    {
        if (Status != TrainingRunStatus.Running)
        {
            return;
        }
        Status = TrainingRunStatus.Paused;
        AddEvent(TrainingRunEventKind.Info, "Paused", Step);
    }

    public void Resume()
    {
        if (Status != TrainingRunStatus.Paused)
        {
            return;
        }
        Status = TrainingRunStatus.Running;
        AddEvent(TrainingRunEventKind.Start, "Resumed", Step);
    }

    public void Cancel()
    {
        if (Status is not (TrainingRunStatus.Queued or TrainingRunStatus.Running or TrainingRunStatus.Paused))
        {
            return;
        }
        Status = TrainingRunStatus.Cancelled;
        Eta = null;
        Activity = null;
        AddEvent(TrainingRunEventKind.Info, "Cancelled", _started ? Step : null);
    }

    /// <summary>After a failure: back to the last checkpoint (the samples after it are dropped) and on from there.</summary>
    public void Retry()
    {
        if (Status != TrainingRunStatus.Failed)
        {
            return;
        }
        var removed = _loss.RemoveAll(p => p.Step > _lastCheckpoint);
        if (removed > 0)
        {
            LossVersion++;
        }
        Step = _lastCheckpoint;
        _stepPosition = _lastCheckpoint;
        ErrorText = null;
        Status = TrainingRunStatus.Running;
        UpdateClock();
        AddEvent(TrainingRunEventKind.Start, string.Create(s_invariant, $"Resumed from checkpoint at step {_lastCheckpoint:N0}"), _lastCheckpoint);
    }

    /// <summary>Starts the same run over, queued.</summary>
    public void Restart() => Reset();

    /// <summary>The simulated deploy: 1.6 seconds, then the simulation's endpoint.</summary>
    public async Task<string?> DeployAsync(CancellationToken cancellation)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1600), cancellation).ConfigureAwait(true);
        return Data.Endpoint;
    }

    /// <summary>The simulated train loss at a step: a fast and a slow decay, a drop per epoch, shrinking noise, one spike.</summary>
    public static double TrainLoss(int step)
    {
        var epoch = TrainingRunFormat.EpochOf(step, TotalSteps, Epochs) - 1;
        var noise = Noise(step) * 0.06 * (0.4 + 0.6 * Math.Exp(-step / 900.0));
        var spike = step == SpikeStep ? 0.34 : step == SpikeStep + SampleEvery ? 0.12 : 0;
        return 0.36 + 1.95 * Math.Exp(-step / 320.0) + 0.1 * Math.Exp(-step / 1400.0) - 0.035 * epoch + noise + spike;
    }

    /// <summary>The simulated validation loss at a step: a little above the smoothed train loss.</summary>
    public static double ValidationLoss(int step)
    {
        var epoch = TrainingRunFormat.EpochOf(step, TotalSteps, Epochs) - 1;
        return 0.43 + 1.88 * Math.Exp(-step / 350.0) + 0.1 * Math.Exp(-step / 1400.0) - 0.02 * epoch + 0.012 * Noise(step + 7);
    }

    /// <summary>Linear warmup over 100 steps to 2e-4, then a cosine decay to 2e-5.</summary>
    public static double LearningRateAt(int step)
    {
        const double peak = 2e-4;
        const double floor = 2e-5;
        if (step < 100)
        {
            return peak * Math.Max(1, step) / 100;
        }
        var t = Math.Clamp((step - 100) / (double)(TotalSteps - 100), 0, 1);
        return floor + 0.5 * (peak - floor) * (1 + Math.Cos(Math.PI * t));
    }

    private static double Noise(int step)
    {
        var h = unchecked((uint)step * 2654435761u);
        h ^= h >> 15;
        h = unchecked(h * 2246822519u);
        h ^= h >> 13;
        return (h & 0xFFFF) / 32767.5 - 1;
    }
}
