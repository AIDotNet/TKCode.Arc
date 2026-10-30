using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class AskYourData
{
    /// <summary>How many tokens (SQL) or words (insight) one tick of a paced reveal adds.</summary>
    internal const int TokensPerTick = 4;

    /// <summary>The tick of the paced reveals.</summary>
    internal static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(34);

    /// <summary>The simulated model's pause before it starts writing.</summary>
    internal static readonly TimeSpan SampleThink = TimeSpan.FromMilliseconds(420);

    /// <summary>The simulated query: ten progress steps of 110ms.</summary>
    internal static readonly TimeSpan SampleStep = TimeSpan.FromMilliseconds(110);

    internal const int SampleSteps = 10;

    /// <summary>The insight starts once the bars have mostly risen.</summary>
    internal static readonly TimeSpan InsightDelay = TimeSpan.FromMilliseconds(280);

    private CancellationTokenSource? _cts;
    private int _run;
    private string? _asked;
    private bool _sqlOpen;
    private bool? _sqlChoice;
    private bool _sqlWritten;
    private string? _errorMessage;
    private AskYourDataErrorStage _errorStage;
    private long _rowsShown;

    /// <summary>
    /// Waits between the steps of a run (the reveal ticks and the simulated query). Tests replace it to run without time.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>The rows scanned shown in the running state.</summary>
    internal long RowsScanned => _rowsShown;

    /// <summary>The run in flight, for tests to await.</summary>
    internal Task? CurrentRun { get; private set; }

    /// <summary>
    /// Asks <see cref="Question"/> (or the simulation's). A run in progress is cancelled first. Does nothing while nothing
    /// can write the SQL or answer it.
    /// </summary>
    public void Ask()
    {
        if (CanAsk)
        {
            CurrentRun = RunAsync(EffectiveQuestion);
        }
    }

    /// <summary>Puts <paramref name="question"/> in the field and asks it.</summary>
    public void Ask(string question)
    {
        SetCurrentValue(QuestionProperty, question);
        Ask();
    }

    /// <summary>Stops the run in progress: the SQL writer and the query are cancelled and the stage reads "Stopped".</summary>
    public void Stop()
    {
        if (!Working)
        {
            return;
        }
        _run++;
        _cts?.Cancel();
        _cts = null;
        if (Stage == AskYourDataStage.Writing && _sqlText is not null && _sqlText.RevealedLength < _sqlText.FullText.Length)
        {
            // Stopped mid-reveal: the SQL is what had streamed, and the panel shrinks to it.
            var shown = _sqlText.FullText[.._sqlText.RevealedLength];
            Sql = shown;
            _sqlText.SetText(shown, "sql", shown.Length);
        }
        Stage = AskYourDataStage.Stopped;
        Refresh();
        Announce(Labels.Stopped);
    }

    /// <summary>Cancels the run in flight and goes back to the empty state, as if nothing had been asked.</summary>
    private void ResetToIdle()
    {
        _run++;
        _cts?.Cancel();
        _cts = null;
        _asked = null;
        _sqlChoice = null;
        _sqlWritten = false;
        _errorMessage = null;
        _rowsShown = 0;
        Result = null;
        Sql = string.Empty;
        _sqlText?.SetText(string.Empty, "sql", 0);
        _insight?.SetText(string.Empty, null, 0);
        ResetRows(null);
        ApplyResultToViews(replay: false);
        Stage = AskYourDataStage.Idle;
        SetSqlOpen(false);
        Refresh();
    }

    private async Task RunAsync(string? raw)
    {
        var question = raw?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            return;
        }
        _autoRan = true;
        _visibility?.Dispose();
        _visibility = null;
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        var token = cts.Token;
        var run = ++_run;
        var simulated = IsSimulated;

        _asked = question;
        _sqlChoice = null;
        _sqlWritten = false;
        _errorMessage = null;
        _rowsShown = 0;
        Result = null;
        Sql = string.Empty;
        _sqlText?.SetText(string.Empty, "sql", 0);
        _insight?.SetText(string.Empty, null, 0);
        ResetRows(null);
        ApplyResultToViews(replay: false);
        Stage = AskYourDataStage.Writing;
        SetSqlOpen(true);
        Announce(Labels.WritingSql);

        string sql;
        try
        {
            sql = await WriteSqlAsync(question, token, run);
        }
        catch (Exception error) when (error is OperationCanceledException && token.IsCancellationRequested || run != _run)
        {
            return;
        }
        catch (Exception error)
        {
            Fail(error, AskYourDataErrorStage.Sql, question, run);
            return;
        }
        if (run != _run)
        {
            return;
        }
        _sqlWritten = true;
        _sqlText?.RevealAll();
        Stage = AskYourDataStage.Running;
        // The panel folds as the query starts, before the chart builds, so only one region moves at a time.
        if (!KeepSqlOpen && _sqlChoice is null)
        {
            SetSqlOpen(false);
        }
        else
        {
            Refresh();
        }
        Announce(Labels.RunningQuery);

        AskYourDataResult result;
        try
        {
            if (RunQuery is { } runQuery)
            {
                var request = new AskYourDataQueryRequest(sql, question, token, (rows, total) => Progress(run, rows, total));
                result = await runQuery(request);
            }
            else
            {
                result = await SimulatedQueryAsync(Simulation!, question, token, run);
            }
        }
        catch (Exception error) when (error is OperationCanceledException && token.IsCancellationRequested || run != _run)
        {
            return;
        }
        catch (Exception error)
        {
            Fail(error, AskYourDataErrorStage.Query, question, run);
            return;
        }
        if (run != _run || token.IsCancellationRequested)
        {
            return;
        }
        Show(result, question, sql, simulated);
        if (result.HasData && !string.IsNullOrEmpty(result.Insight))
        {
            try
            {
                await RevealInsightAsync(result.Insight, token, run);
            }
            catch (OperationCanceledException)
            {
                // A new question took over; its run resets the insight.
            }
        }
    }

    /// <summary>Writes the SQL: chunks from the app's stream, or a whole string revealed four tokens a tick.</summary>
    private async Task<string> WriteSqlAsync(string question, CancellationToken token, int run)
    {
        var request = new AskYourDataSqlRequest(question, EffectiveSource, token);
        if (StreamSql is { } stream)
        {
            await foreach (var chunk in stream(request).WithCancellation(token))
            {
                if (run != _run)
                {
                    throw new OperationCanceledException(token);
                }
                if (string.IsNullOrEmpty(chunk))
                {
                    continue;
                }
                Sql += chunk;
                if (_sqlText is not null)
                {
                    _sqlText.Append(chunk);
                    _sqlText.RevealTo(_sqlText.FullText.Length, fade: true);
                }
                Refresh();
            }
            return Sql;
        }

        string text;
        if (GenerateSql is { } generate)
        {
            text = await generate(request);
        }
        else
        {
            var simulation = Simulation!;
            await Delay(SampleThink, token);
            text = simulation.SqlFor(question);
        }
        token.ThrowIfCancellationRequested();
        if (run != _run)
        {
            throw new OperationCanceledException(token);
        }
        text ??= string.Empty;
        // The whole query is laid out first, so the panel holds its final size and nothing below moves.
        Sql = text;
        _sqlText?.SetText(text, "sql", 0);
        Refresh();
        if (_sqlText is null || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _sqlText?.RevealAll();
            return text;
        }
        var ends = AskYourDataFormat.TokenEnds(text);
        for (var i = TokensPerTick - 1; ; i += TokensPerTick)
        {
            var end = ends[Math.Min(i, ends.Count - 1)];
            _sqlText.RevealTo(end, fade: true);
            if (end >= text.Length)
            {
                break;
            }
            await Delay(Tick, token);
            if (run != _run)
            {
                throw new OperationCanceledException(token);
            }
        }
        return text;
    }

    /// <summary>The simulated database: the count rolls up to the rows the query scans, then the answer's result shows.</summary>
    private async Task<AskYourDataResult> SimulatedQueryAsync(AskYourDataSimulation simulation, string question, CancellationToken token, int run)
    {
        var total = simulation.ScannedFor(question);
        Progress(run, 0, total);
        for (var step = 1; step <= SampleSteps; step++)
        {
            await Delay(SampleStep, token);
            var t = (double)step / SampleSteps;
            Progress(run, (long)Math.Round(total * (1 - Math.Pow(1 - t, 2))), total);
        }
        return simulation.ResultFor(question);
    }

    /// <summary>A progress report from any thread: the count rolls in reserved width.</summary>
    private void Progress(int run, long rows, long? total)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Progress(run, rows, total));
            return;
        }
        if (run != _run || Stage != AskYourDataStage.Running)
        {
            return;
        }
        _rowsShown = Math.Max(0, rows);
        ResetRows(total);
        if (_rows is not null)
        {
            _rows.Value = _rowsShown;
        }
    }

    /// <summary>Reserves the count's width for the total (or the count so far) in tabular numerals.</summary>
    private void ResetRows(long? total)
    {
        if (_rowsReserve is not null)
        {
            _rowsReserve.Text = AskYourDataFormat.Grouped(Math.Max(total ?? 0, _rowsShown));
        }
        if (total is null && _rows is not null)
        {
            _rows.IsInstant = true;
            _rows.Value = 0;
            _rows.IsInstant = false;
        }
    }

    private void Fail(Exception error, AskYourDataErrorStage stage, string question, int run)
    {
        if (run != _run)
        {
            return;
        }
        _errorStage = stage;
        _errorMessage = error.Message;
        _sqlText?.RevealAll();
        Stage = AskYourDataStage.Error;
        Refresh();
        var (title, _) = Header();
        Announce(title + ". " + error.Message);
        RaiseEvent(new AskYourDataErrorEventArgs(FailedEvent, error, question, stage));
    }

    /// <summary>Charts the result (or the no-data state) and narrates it.</summary>
    private void Show(AskYourDataResult result, string question, string sql, bool simulated)
    {
        Result = result;
        if (!result.HasData)
        {
            Stage = AskYourDataStage.Empty;
            ApplyResultToViews(replay: false);
            Refresh();
            Announce(Labels.NoData + (simulated ? ". " + Labels.Simulated : string.Empty));
            return;
        }
        var view = Chart ?? result.Chart ?? DefaultChart;
        _insight?.SetText(result.Insight ?? string.Empty, null, 0);
        Stage = AskYourDataStage.Done;
        ApplyResultToViews(replay: false);
        Refresh();
        ShowView(view, animate: false);
        if (ViewControl(view) is AskYourDataChart chart)
        {
            chart.Replay();
        }
        var parts = new List<string> { result.Title };
        if (result.RowCount is { } rows)
        {
            parts.Add(Labels.Rows(rows));
        }
        if (!string.IsNullOrEmpty(result.Insight))
        {
            parts.Add(result.Insight);
        }
        if (simulated)
        {
            parts.Add(Labels.Simulated);
        }
        Announce(string.Join(". ", parts.Select(p => p.TrimEnd('.'))) + ".");
        RaiseEvent(new AskYourDataResultEventArgs(ResultReadyEvent, result, question, sql));
    }

    /// <summary>The insight streams four words a tick with its two lines reserved; whole without full motion.</summary>
    private async Task RevealInsightAsync(string insight, CancellationToken token, int run)
    {
        if (_insight is null || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _insight?.RevealAll();
            return;
        }
        await Delay(InsightDelay, token);
        var ends = AskYourDataFormat.WordEnds(insight);
        for (var i = TokensPerTick - 1; ; i += TokensPerTick)
        {
            if (run != _run)
            {
                return;
            }
            var end = ends[Math.Min(i, ends.Count - 1)];
            _insight.RevealTo(end, fade: true);
            if (end >= insight.Length)
            {
                return;
            }
            await Delay(Tick, token);
        }
    }
}
