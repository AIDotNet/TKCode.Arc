using System.Diagnostics;
using Avalonia;

namespace TKCode.Arc;

public partial class EvalSuite
{
    private CancellationTokenSource? _runCancellation;
    private bool _inRun;
    private bool _writingResults;

    /// <summary>
    /// Runs every case on the baseline and the candidate version, <see cref="MaxConcurrency"/> cases at a time. The two
    /// versions' earlier results for these cases are dropped first; each case flips from queued to running to its status
    /// as both results arrive, and <see cref="Results"/> is replaced with a new list each time. Completes when every case
    /// has finished or the run was stopped (<see cref="Stop"/>). Does nothing while a run is in progress or when either
    /// version is unknown.
    /// </summary>
    public async Task RunAsync()
    {
        if (_inRun)
        {
            return;
        }
        var baseline = VersionById(BaselineVersionId);
        var candidate = VersionById(CandidateVersionId);
        var cases = _allRows.Select(r => r.Case).ToArray();
        if (baseline is null || candidate is null || cases.Length == 0)
        {
            return;
        }
        _inRun = true;
        var cancellation = new CancellationTokenSource();
        _runCancellation = cancellation;
        var token = cancellation.Token;
        var clock = Stopwatch.StartNew();

        // Drop the two versions' old results for these cases, and queue every case.
        var ids = new HashSet<string>(cases.Select(c => c.Id), StringComparer.Ordinal);
        var kept = (Results ?? Array.Empty<EvalResult>())
            .Where(r => !(ids.Contains(r.CaseId) && (r.VersionId == baseline.Id || r.VersionId == candidate.Id)))
            .ToList();
        foreach (var evalCase in cases)
        {
            _live[evalCase.Id] = EvalCaseStatus.Queued;
        }
        RunTotal = cases.Length;
        FinishedCount = 0;
        Phase = EvalRunPhase.Running;
        WriteResults(kept);
        RaiseEvent(new EvalSuiteRunStartedEventArgs(RunStartedEvent, this, baseline, candidate, cases));
        _announcer.Announce(string.Format(Culture, Labels.AnnounceStartFormat, cases.Length, baseline.Name, candidate.Name));

        var next = 0;
        async Task Worker()
        {
            while (!token.IsCancellationRequested && next < cases.Length)
            {
                var evalCase = cases[next++];
                await RunCase(evalCase, baseline, candidate, token);
            }
        }

        var workers = new Task[Math.Clamp(MaxConcurrency, 1, cases.Length)];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Worker();
        }
        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // Stopped: finished cases keep their results, the rest go back to "not run".
        }
        clock.Stop();
        _lastElapsed = clock.Elapsed;
        var stopped = token.IsCancellationRequested || FinishedCount < cases.Length;
        _live.Clear();
        _inRun = false;
        if (ReferenceEquals(_runCancellation, cancellation))
        {
            _runCancellation = null;
        }
        cancellation.Dispose();
        Phase = stopped ? EvalRunPhase.Stopped : EvalRunPhase.Completed;
        Rebuild();
        SyncProgress();
        var summary = Summary;
        RaiseEvent(new EvalSuiteRunCompletedEventArgs(RunCompletedEvent, this, summary, stopped, FinishedCount, _lastElapsed));
        if (stopped)
        {
            _announcer.Announce(string.Format(Culture, Labels.AnnounceStoppedFormat, FinishedCount, cases.Length));
        }
        else
        {
            var delta = ShownRates().Delta;
            var signed = delta > 0 ? "+" + delta.ToString(Culture) : delta.ToString(Culture);
            _announcer.Announce(string.Format(Culture, Labels.AnnounceDoneFormat, candidate.Name, summary.CandidatePassed,
                summary.Compared, signed, summary.Regressions));
        }
    }

    /// <summary>Stops the run in progress: cases already running are cancelled and the rest are not started.</summary>
    public void Stop()
    {
        _runCancellation?.Cancel();
    }

    private async Task RunCase(EvalCase evalCase, EvalVersion baseline, EvalVersion candidate, CancellationToken token)
    {
        _live[evalCase.Id] = EvalCaseStatus.Running;
        Rebuild();
        SyncProgress();
        var baseTask = RunVersion(evalCase, baseline, token);
        var candTask = RunVersion(evalCase, candidate, token);
        EvalResult baseResult, candResult;
        try
        {
            baseResult = await baseTask;
            candResult = await candTask;
        }
        catch (OperationCanceledException)
        {
            _live.Remove(evalCase.Id);
            throw;
        }
        token.ThrowIfCancellationRequested();
        var list = new List<EvalResult>(Results ?? Array.Empty<EvalResult>()) { baseResult, candResult };
        _live.Remove(evalCase.Id);
        FinishedCount++;
        WriteResults(list);
        if (_rowsById.TryGetValue(evalCase.Id, out var row))
        {
            row.Reveal = 0;
            _list?.Refresh();
        }
        RaiseEvent(new EvalSuiteCaseCompletedEventArgs(CaseCompletedEvent, this, evalCase, baseResult, candResult,
            EvalSuiteGrader.Compare(baseResult, candResult)));
    }

    private async Task<EvalResult> RunVersion(EvalCase evalCase, EvalVersion version, CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            EvalResult result;
            if (RunCaseAsync is { } run)
            {
                result = await run(evalCase, version, token) ?? throw new InvalidOperationException("RunCaseAsync returned no result.");
            }
            else
            {
                result = await Simulate(evalCase, version, token);
            }
            result.CaseId = evalCase.Id;
            result.VersionId = version.Id;
            if (result.Duration <= TimeSpan.Zero)
            {
                result.Duration = clock.Elapsed;
            }
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EvalResult
            {
                CaseId = evalCase.Id,
                VersionId = version.Id,
                Output = string.Empty,
                Passed = false,
                IsError = true,
                Detail = ex.Message,
                Duration = clock.Elapsed
            };
        }
    }

    private async Task<EvalResult> Simulate(EvalCase evalCase, EvalVersion version, CancellationToken token)
    {
        var sample = EvalSuiteSamples.Output(evalCase.Id, version.Id) ??
                     (evalCase.Expected, evalCase.Check == EvalCheckKind.Rubric ? 0.8 : null);
        var jitter = (evalCase.Id.Length * 37 + version.Id.Length * 53 + (evalCase.Id.Length > 0 ? evalCase.Id[0] : 0)) % 380;
        var time = EvalSuiteSamples.SimulatedCaseTime + TimeSpan.FromMilliseconds(jitter);
        await Delay(time, token);
        return EvalSuiteGrader.Grade(evalCase, version.Id, sample.Text, sample.Score, time, Labels, Culture);
    }

    private void WriteResults(IReadOnlyList<EvalResult> results)
    {
        _writingResults = true;
        try
        {
            SetCurrentValue(ResultsProperty, results);
        }
        finally
        {
            _writingResults = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }
}
