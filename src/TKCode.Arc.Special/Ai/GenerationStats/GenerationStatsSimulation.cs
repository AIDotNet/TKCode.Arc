namespace TKCode.Arc;

/// <summary>
/// One response a <see cref="GenerationStatsSimulation"/> plays: the model and its prices, the token counts it ends on, how
/// long the first token takes and the peak speed. Give <see cref="FailAtTokens"/> to make it fail once at that output count
/// and succeed when retried.
/// </summary>
public sealed record GenerationStatsRun
{
    /// <summary>The model that answers; the strip is named after it.</summary>
    public GenerationModel? Model { get; init; }

    /// <summary>The prices the run's cost is worked out from; null leaves the Rate and Cost columns empty.</summary>
    public GenerationPricing? Pricing { get; init; }

    /// <summary>Input (prompt) tokens, cached ones included.</summary>
    public long InputTokens { get; init; }

    /// <summary>The part of the input read from the prompt cache.</summary>
    public long CachedTokens { get; init; }

    /// <summary>Output tokens the run ends on, reasoning tokens not included.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Reasoning (thinking) tokens; zero leaves the row out.</summary>
    public long ReasoningTokens { get; init; }

    /// <summary>Time from the request to the first token.</summary>
    public TimeSpan TimeToFirstToken { get; init; }

    /// <summary>The speed the stream ramps up to, in tokens per second.</summary>
    public double PeakTokensPerSecond { get; init; }

    /// <summary>The output count at which the first attempt fails; null never fails.</summary>
    public long? FailAtTokens { get; init; }

    /// <summary>The reason a failed attempt shows; null shows <see cref="GenerationStatsLabels.DefaultError"/>.</summary>
    public string? ErrorText { get; init; }
}

/// <summary>
/// A simulated stream for a <see cref="GenerationStats"/> that has no <see cref="GenerationStats.Status"/>: it plays
/// <see cref="Runs"/> in order on its own virtual clock, which the strip advances in 40ms steps while it is on screen
/// (full motion only), so leaving the screen pauses the run. A run waits for its first token, then streams with a ramp up
/// and a few percent of waver, in chunks of four or more tokens, and settles; a run with
/// <see cref="GenerationStatsRun.FailAtTokens"/> fails there once and succeeds when retried (by Retry, or on its own after a
/// short wait). A settled run stays on screen while the breakdown is open. Nothing is sent anywhere. Create one per strip:
/// it keeps the playback state.
/// </summary>
public sealed class GenerationStatsSimulation
{
    /// <summary>The step of the virtual clock.</summary>
    internal const double TickSeconds = 0.04;

    /// <summary>How long a settled run stays before the next one starts.</summary>
    internal const double SettledSeconds = 3.2;

    /// <summary>How long a failed run waits before it retries on its own.</summary>
    internal const double FailedSeconds = 2.8;

    /// <summary>The pause before the first run starts.</summary>
    internal const double FirstDelaySeconds = 0.6;

    private static readonly GenerationStatsRun s_none = new();

    private enum Phase
    {
        Pausing,
        Waiting,
        Streaming,
        Settled,
        Failed
    }

    private readonly HashSet<int> _failedOnce = new();
    private IReadOnlyList<GenerationStatsRun> _runs = [];
    private Random _random = new(7);
    private int _index;
    private Phase _phase = Phase.Pausing;
    private double _phaseTime;
    private double _runTime;
    private double _tokens;
    private long _emitted;
    private double _carry;
    private bool _retrying;
    private int _nextChunk = 4;

    /// <summary>Creates a simulation with no runs; set <see cref="Runs"/>.</summary>
    public GenerationStatsSimulation()
    {
        Restart();
    }

    /// <summary>The runs to play, in order. Setting it starts again from the first run.</summary>
    public IReadOnlyList<GenerationStatsRun> Runs
    {
        get => _runs;
        set
        {
            _runs = value ?? [];
            Restart();
        }
    }

    /// <summary>Plays the runs in a loop (true by default); otherwise it stops on the last settled run.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Keeps a settled (or failed) run on screen instead of moving on; the strip sets it while the breakdown is open.</summary>
    public bool Hold { get; set; }

    /// <summary>The virtual clock.</summary>
    public DateTimeOffset Now { get; private set; } = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The run on screen (an empty run while <see cref="Runs"/> is empty).</summary>
    public GenerationStatsRun Run { get; private set; } = s_none;

    /// <summary>The index of the run on screen.</summary>
    public int Index => _index;

    /// <summary>Where the run is.</summary>
    public GenerationStatus Status { get; private set; }

    /// <summary>When the run's request was sent (virtual time).</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>The run's usage so far.</summary>
    public GenerationUsage Usage { get; private set; } = new();

    /// <summary>The reason of a failed run.</summary>
    public string? Error { get; private set; }

    /// <summary>Raised whenever the frame (status, usage, error) changes.</summary>
    public event Action? Changed;

    /// <summary>Starts again from the first run, as on a fresh strip.</summary>
    public void Restart()
    {
        _failedOnce.Clear();
        _random = new Random(7);
        _index = 0;
        _phase = Phase.Pausing;
        _phaseTime = 0;
        _runTime = 0;
        _tokens = 0;
        _emitted = 0;
        _carry = 0;
        _retrying = false;
        _nextChunk = 4;
        Run = _runs.Count > 0 ? _runs[0] : s_none;
        Usage = new GenerationUsage();
        StartedAt = null;
        Error = null;
        Status = GenerationStatus.Idle;
        Changed?.Invoke();
    }

    /// <summary>Moves the virtual clock on by <paramref name="seconds"/>, in 40ms steps.</summary>
    public void Advance(double seconds)
    {
        if (_runs.Count == 0)
        {
            return;
        }
        _carry += Math.Max(0, seconds);
        while (_carry >= TickSeconds)
        {
            _carry -= TickSeconds;
            Step();
        }
    }

    /// <summary>Retries a failed run at once (the next attempt succeeds).</summary>
    public void Retry()
    {
        if (_phase != Phase.Failed)
        {
            return;
        }
        _retrying = true;
        Start(_index);
    }

    /// <summary>Shows the current run settled (done, with its final numbers) and stops there: reduced motion.</summary>
    public void Settle()
    {
        if (_runs.Count == 0)
        {
            return;
        }
        var run = Run;
        var ttft = run.TimeToFirstToken.TotalMilliseconds;
        var generation = run.PeakTokensPerSecond > 0 ? run.OutputTokens / (run.PeakTokensPerSecond * 0.92) * 1000 : 0;
        StartedAt = Now - TimeSpan.FromMilliseconds(ttft + generation);
        Usage = Base(run) with
        {
            OutputTokens = run.OutputTokens,
            ReasoningTokens = run.ReasoningTokens > 0 ? run.ReasoningTokens : null,
            Ttft = TimeSpan.FromMilliseconds(ttft),
            Duration = TimeSpan.FromMilliseconds(Math.Round(ttft + generation)),
        };
        Status = GenerationStatus.Done;
        Error = null;
        _phase = Phase.Settled;
        _phaseTime = 0;
        Changed?.Invoke();
    }

    private static GenerationUsage Base(GenerationStatsRun run) => new()
    {
        InputTokens = run.InputTokens,
        CachedTokens = run.CachedTokens > 0 ? run.CachedTokens : null,
    };

    private void Start(int index)
    {
        _index = index;
        Run = _runs[index];
        _random = new Random(7 + index * 31 + (_retrying ? 5 : 0));
        _phase = Phase.Waiting;
        _phaseTime = 0;
        _runTime = 0;
        _tokens = 0;
        _emitted = 0;
        _nextChunk = 4 + _random.Next(0, 4);
        StartedAt = Now;
        Usage = Base(Run);
        Error = null;
        Status = GenerationStatus.Waiting;
        Changed?.Invoke();
    }

    private void Step()
    {
        Now += TimeSpan.FromSeconds(TickSeconds);
        _phaseTime += TickSeconds;
        _runTime += TickSeconds;
        var run = Run;
        switch (_phase)
        {
            case Phase.Pausing:
                if (_phaseTime >= FirstDelaySeconds)
                {
                    Start(_index);
                }
                break;
            case Phase.Waiting:
                if (_runTime * 1000 >= run.TimeToFirstToken.TotalMilliseconds)
                {
                    _phase = Phase.Streaming;
                    _phaseTime = 0;
                    Status = GenerationStatus.Streaming;
                    Usage = Usage with
                    {
                        Ttft = TimeSpan.FromMilliseconds(Math.Round(_runTime * 1000)),
                        OutputTokens = 0,
                        ReasoningTokens = run.ReasoningTokens > 0 ? run.ReasoningTokens : null,
                    };
                    Changed?.Invoke();
                }
                break;
            case Phase.Streaming:
                Stream(run);
                break;
            case Phase.Settled:
                if (!Hold && _phaseTime >= SettledSeconds && (Loop || _index < _runs.Count - 1))
                {
                    _retrying = false;
                    Start((_index + 1) % _runs.Count);
                }
                break;
            case Phase.Failed:
                if (!Hold && _phaseTime >= FailedSeconds)
                {
                    Retry();
                }
                break;
        }
    }

    private void Stream(GenerationStatsRun run)
    {
        // Ramps up over 0.6s from 45% of the peak, then wavers a few percent.
        var ramp = Math.Min(1, 0.45 + 0.55 * _phaseTime / 0.6);
        var waver = 1 + 0.04 * Math.Sin(_phaseTime * 7.3) + 0.02 * (_random.NextDouble() - 0.5);
        _tokens += run.PeakTokensPerSecond * ramp * waver * TickSeconds;
        var target = run.OutputTokens;
        var failAt = run.FailAtTokens is { } f && !_retrying && !_failedOnce.Contains(_index) ? f : (long?)null;
        if (_tokens - _emitted < _nextChunk && _tokens < target)
        {
            return;
        }
        var next = Math.Min(target, (long)Math.Floor(_tokens));
        if (failAt is { } fail && next >= fail)
        {
            _failedOnce.Add(_index);
            _emitted = fail;
            Usage = Usage with { OutputTokens = fail, Duration = TimeSpan.FromMilliseconds(Math.Round(_runTime * 1000)) };
            Error = run.ErrorText;
            Status = GenerationStatus.Error;
            _phase = Phase.Failed;
            _phaseTime = 0;
            Changed?.Invoke();
            return;
        }
        _emitted = next;
        _nextChunk = 4 + _random.Next(0, 4);
        if (next >= target)
        {
            Usage = Usage with { OutputTokens = target, Duration = TimeSpan.FromMilliseconds(Math.Round(_runTime * 1000)) };
            Status = GenerationStatus.Done;
            _phase = Phase.Settled;
            _phaseTime = 0;
        }
        else
        {
            Usage = Usage with { OutputTokens = next };
        }
        Changed?.Invoke();
    }
}
