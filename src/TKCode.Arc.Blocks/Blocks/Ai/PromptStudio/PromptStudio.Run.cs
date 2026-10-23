using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class PromptStudio
{
    /// <summary>One run of the prompt: what was asked, its cancellation, the text received and the times.</summary>
    private sealed class StudioRun(PromptRunRequest request, bool simulated, DateTimeOffset started, string? caseLabel, string modelName)
    {
        public PromptRunRequest Request { get; } = request;
        public bool Simulated { get; } = simulated;
        public DateTimeOffset Started { get; } = started;
        public string? CaseLabel { get; } = caseLabel;
        public string ModelName { get; } = modelName;
        public CancellationTokenSource Cancel { get; } = new();
        public StringBuilder Raw { get; } = new();

        /// <summary>When the handler sent its last text.</summary>
        public DateTimeOffset? Ended { get; set; }

        /// <summary>The handler is done; the reply may still be revealing.</summary>
        public bool StreamEnded { get; set; }

        /// <summary>The run has its outcome.</summary>
        public bool Finished { get; set; }
    }

    /// <summary>How long the simulator thinks at each effort (Arc's measured 0.5 to 1.17 s).</summary>
    private static readonly TimeSpan[] s_thinking =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(634),
        TimeSpan.FromMilliseconds(799),
        TimeSpan.FromMilliseconds(982),
        TimeSpan.FromMilliseconds(1166)
    ];

    /// <summary>The simulator writes this many words at a time ...</summary>
    private const int SimulatedWordsPerChunk = 5;

    /// <summary>... this often (about a hundred words a second, a model's pace).</summary>
    private static readonly TimeSpan s_simulatedChunk = TimeSpan.FromMilliseconds(50);

    private StudioRun? _run;

    #region Seams (tests)

    /// <summary>The clock of the measured run times (tests give their own).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>The simulator's waits (tests make them instant or controlled).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (delay, token) => Task.Delay(delay, token);

    #endregion

    /// <summary>Whether a run is thinking or writing.</summary>
    internal bool IsBusy => RunState is PromptRunState.Thinking or PromptRunState.Streaming;

    #region Public methods

    /// <summary>
    /// Runs the prompt with the current values, model and effort: <see cref="RunStarted"/> is raised and the reply streams
    /// into the output panel from <see cref="RunHandler"/> (or the built-in simulator). A run still going is stopped first
    /// (one run at a time).
    /// </summary>
    public void Run()
    {
        if (_run is { } running)
        {
            // Replaced: it ends as stopped, quietly; the new run takes the panel.
            EndReplaced(running);
        }
        var request = BuildRequest();
        var simulated = RunHandler is null;
        var run = new StudioRun(request, simulated, Time.GetUtcNow(), SelectedTestCase?.Label, SelectedModel?.Name ?? string.Empty);
        _run = run;
        RaiseEvent(new PromptRunStartedEventArgs(RunStartedEvent, this, request));
        if (!ReferenceEquals(_run, run))
        {
            // A handler of RunStarted started or stopped a run itself.
            return;
        }
        var labels = Labels;
        if (_output is { } panel)
        {
            panel.Reset();
            panel.CaseText = run.CaseLabel;
            panel.State = PromptRunState.Thinking;
            panel.StatusText = labels.Thinking;
            panel.ErrorText = null;
            panel.FooterText = RunningFooter(run);
        }
        SetRunState(PromptRunState.Thinking);
        Announce(labels.AnnounceRunning);
        _ = RunAsync(run, RunHandler ?? Simulate);
    }

    /// <summary>Stops the run: what was shown stays and the run ends as <see cref="PromptRunState.Stopped"/>.</summary>
    public void Stop()
    {
        if (_run is { } run)
        {
            StopRun(run, announce: true);
        }
    }

    /// <summary>Runs the prompt again (the failed run's Retry).</summary>
    public void Retry() => Run();

    #endregion

    #region Request

    /// <summary>What the handler is asked: the draft as written and with its values, the values, model, effort, case and version.</summary>
    private PromptRunRequest BuildRequest()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in Values)
        {
            values[pair.Key] = pair.Value ?? string.Empty;
        }
        foreach (var name in _effective)
        {
            values.TryAdd(name, string.Empty);
        }
        var raw = new PromptTemplate(SystemText, UserText);
        var rendered = new PromptTemplate(PromptStudioText.Render(raw.System, values), PromptStudioText.Render(raw.User, values));
        return new PromptRunRequest(raw, rendered, new ReadOnlyDictionary<string, string>(values), SelectedModel, Effort,
            SelectedTestCase?.Id, EffectiveOpenVersion?.Id);
    }

    #endregion

    #region Pipeline

    /// <summary>Reads the handler's text on the UI thread into the output panel.</summary>
    private async Task RunAsync(StudioRun run, PromptRunHandler handler)
    {
        var token = run.Cancel.Token;
        try
        {
            await foreach (var delta in handler(run.Request, token).WithCancellation(token))
            {
                if (run.Finished)
                {
                    return;
                }
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }
                run.Raw.Append(delta);
                if (RunState == PromptRunState.Thinking)
                {
                    // The first text: the model is writing.
                    SetRunState(PromptRunState.Streaming);
                    if (_output is { } panel)
                    {
                        panel.State = PromptRunState.Streaming;
                        panel.StatusText = Labels.Writing;
                    }
                }
                _output?.Push(delta);
            }
            if (run.Finished)
            {
                return;
            }
            if (token.IsCancellationRequested)
            {
                StopRun(run, announce: true);
                return;
            }
            EndStream(run);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || run.Finished)
        {
            if (!run.Finished)
            {
                StopRun(run, announce: true);
            }
        }
        catch (Exception exception)
        {
            if (!run.Finished)
            {
                FailRun(run, exception);
            }
        }
    }

    /// <summary>
    /// The handler sent everything. The run is done once the panel has shown the whole reply (<c>RevealCompleted</c>), at
    /// once when there is no text or nothing shows it.
    /// </summary>
    private void EndStream(StudioRun run)
    {
        run.StreamEnded = true;
        run.Ended = Time.GetUtcNow();
        if (run.Raw.Length > 0 && _output is { } panel && panel.IsRevealing && this.IsAttachedToVisualTree())
        {
            panel.Complete();
            return;
        }
        if (_output is { } shown && !string.Equals(shown.Text, run.Raw.ToString(), StringComparison.Ordinal))
        {
            shown.Text = run.Raw.ToString();
        }
        Finish(run);
    }

    private void OnRevealCompleted(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _output))
        {
            return;
        }
        e.Handled = true;
        if (_run is { StreamEnded: true, Finished: false } run)
        {
            Finish(run);
        }
    }

    /// <summary>The reply is complete: its time, tokens and cost, "Reply", the footer and Copy.</summary>
    private void Finish(StudioRun run)
    {
        run.Finished = true;
        var text = run.Raw.ToString();
        var result = BuildResult(run, text, (run.Ended ?? Time.GetUtcNow()) - run.Started);
        LastResult = result;
        var labels = Labels;
        if (_output is { } panel)
        {
            panel.State = PromptRunState.Done;
            panel.StatusText = labels.Reply;
            panel.FooterText = labels.DoneFooter(result.Elapsed.TotalSeconds, result.OutputTokens,
                result.Cost is { } cost ? labels.Cost(cost) : null, result.IsSimulated);
        }
        EndRun(run, PromptRunState.Done);
        Announce(labels.AnnounceDone);
        RaiseEvent(new PromptRunCompletedEventArgs(RunCompletedEvent, this, PromptRunState.Done, result));
    }

    /// <summary>Stops a run: the stream is cancelled, the shown text stays (the rest is dropped), the footer keeps the model line.</summary>
    private void StopRun(StudioRun run, bool announce)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Cancel.Cancel();
        _output?.Stop();
        var shown = _output?.Text ?? run.Raw.ToString();
        var result = BuildResult(run, shown, Time.GetUtcNow() - run.Started);
        LastResult = result;
        if (_output is { } panel)
        {
            panel.State = PromptRunState.Stopped;
            panel.StatusText = Labels.Stopped;
        }
        EndRun(run, PromptRunState.Stopped);
        if (announce)
        {
            Announce(Labels.AnnounceStopped);
        }
        RaiseEvent(new PromptRunCompletedEventArgs(RunCompletedEvent, this, PromptRunState.Stopped, result));
    }

    /// <summary>A run replaced by a new one: cancelled and reported as stopped, without touching the panel.</summary>
    private void EndReplaced(StudioRun run)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Cancel.Cancel();
        var result = BuildResult(run, _output?.Text ?? run.Raw.ToString(), Time.GetUtcNow() - run.Started);
        LastResult = result;
        if (ReferenceEquals(_run, run))
        {
            _run = null;
        }
        RaiseEvent(new PromptRunCompletedEventArgs(RunCompletedEvent, this, PromptRunState.Stopped, result));
    }

    /// <summary>The handler threw: <see cref="RunFailed"/> words the error, the panel shows it with Retry.</summary>
    private void FailRun(StudioRun run, Exception exception)
    {
        run.Finished = true;
        run.Cancel.Cancel();
        var args = new PromptRunFailedEventArgs(RunFailedEvent, this, exception);
        RaiseEvent(args);
        _output?.Stop();
        var shown = _output?.Text ?? run.Raw.ToString();
        var result = shown.Length > 0 ? BuildResult(run, shown, Time.GetUtcNow() - run.Started) : null;
        LastResult = result;
        var labels = Labels;
        if (_output is { } panel)
        {
            panel.State = PromptRunState.Failed;
            panel.StatusText = labels.RunFailed;
            panel.ErrorText = args.Message;
            panel.FooterText = labels.FailedFooter(run.ModelName, run.Simulated);
        }
        EndRun(run, PromptRunState.Failed);
        Announce(labels.AnnounceFailed(args.Message));
        RaiseEvent(new PromptRunCompletedEventArgs(RunCompletedEvent, this, PromptRunState.Failed, result));
    }

    private void EndRun(StudioRun run, PromptRunState state)
    {
        if (ReferenceEquals(_run, run))
        {
            _run = null;
            SetRunState(state);
        }
    }

    /// <summary>
    /// What a run produced: the estimated tokens of the rendered prompt and of the reply, and the cost at the model's
    /// prices per million tokens (rounded to four decimals; null when the model has no prices).
    /// </summary>
    private PromptRunResult BuildResult(StudioRun run, string text, TimeSpan elapsed)
    {
        var request = run.Request;
        var input = Math.Max(0, Estimate(request.Rendered.System)) + Math.Max(0, Estimate(request.Rendered.User));
        var output = Math.Max(0, Estimate(text));
        decimal? cost = request.Model is { InputPrice: { } inputPrice, OutputPrice: { } outputPrice }
            ? Math.Round((input * inputPrice + output * outputPrice) / 1_000_000m, 4, MidpointRounding.AwayFromZero)
            : null;
        return new PromptRunResult(text, elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed, input, output, cost, run.Simulated);
    }

    #endregion

    #region Simulator

    /// <summary>
    /// The built-in simulator (no <see cref="RunHandler"/>): it thinks for a moment that grows with the effort (Arc 0.5 to
    /// 1.17 s), then writes <see cref="PromptStudioLabels.SimulatedReply"/> a few words at a time at a model's pace.
    /// </summary>
    private async IAsyncEnumerable<string> Simulate(PromptRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var level = Math.Clamp((int)request.Effort, 0, s_thinking.Length - 1);
        await Delay(s_thinking[level], cancellationToken);
        var first = true;
        foreach (var chunk in Chunks(Labels.SimulatedReply, SimulatedWordsPerChunk))
        {
            if (!first)
            {
                await Delay(s_simulatedChunk, cancellationToken);
            }
            first = false;
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
        }
    }

    /// <summary><paramref name="text"/> in pieces of <paramref name="words"/> whole words, each with the spaces after it.</summary>
    internal static IEnumerable<string> Chunks(string text, int words)
    {
        var start = 0;
        var count = 0;
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            count++;
            if (count == words)
            {
                yield return text.Substring(start, i - start);
                start = i;
                count = 0;
            }
        }
        if (start < text.Length)
        {
            yield return text.Substring(start);
        }
    }

    #endregion

    #region State and parts

    private void SetRunState(PromptRunState state)
    {
        RunState = state;
        UpdateRunClasses();
        UpdateRunParts();
    }

    private void UpdateRunClasses()
    {
        var state = RunState;
        var busy = state is PromptRunState.Thinking or PromptRunState.Streaming;
        PseudoClasses.Set(BusyPseudoClass, busy);
        PseudoClasses.Set(ThinkingPseudoClass, state == PromptRunState.Thinking);
        PseudoClasses.Set(StreamingPseudoClass, state == PromptRunState.Streaming);
        PseudoClasses.Set(DonePseudoClass, state == PromptRunState.Done);
        PseudoClasses.Set(StoppedPseudoClass, state == PromptRunState.Stopped);
        PseudoClasses.Set(FailedPseudoClass, state == PromptRunState.Failed);
        // Avalonia has no aria-busy: while busy the card's item status says what the run is doing.
        if (busy)
        {
            AutomationProperties.SetItemStatus(this, state == PromptRunState.Thinking ? Labels.Thinking : Labels.Writing);
        }
        else
        {
            ClearValue(AutomationProperties.ItemStatusProperty);
        }
    }

    /// <summary>Run becomes Stop while busy; the commands follow.</summary>
    private void UpdateRunParts()
    {
        var busy = IsBusy;
        if (_runButton is not null)
        {
            _runButton.IsRunning = busy;
        }
        _runCommand.CanExecuteNow = !busy;
        _stopCommand.CanExecuteNow = busy;
    }

    /// <summary>The idle line ("Run Broken spar to see the reply here. Runs are simulated.") and the case it names.</summary>
    private void UpdateRunTexts()
    {
        if (_output is not { } panel)
        {
            return;
        }
        var labels = Labels;
        var testCase = SelectedTestCase;
        var idle = testCase is null ? labels.IdleNoCase : labels.Idle(testCase.Label);
        if (RunHandler is null && !string.IsNullOrEmpty(labels.RunsAreSimulated))
        {
            idle += " " + labels.RunsAreSimulated;
        }
        panel.IdleText = idle;
        if (RunState == PromptRunState.Idle)
        {
            panel.CaseText = testCase?.Label;
        }
    }

    /// <summary>The footer while a run goes: "Simulated · model · High effort".</summary>
    private string RunningFooter(StudioRun run)
    {
        var effort = run.Request.Model is { SupportsEffort: true } ? Labels.EffortName(run.Request.Effort) : null;
        return Labels.RunningFooter(run.ModelName, effort, run.Simulated);
    }

    #endregion

    #region Part events

    private void OnRunRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!IsBusy)
        {
            Run();
        }
    }

    private void OnStopRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Stop();
    }

    /// <summary>Retry: the error box goes, so the focus moves to the Run button (now Stop), not to the page (Arc's gap).</summary>
    private void OnRetryRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var method = FocusMethod;
        var hadFocus = _output?.IsKeyboardFocusWithin == true;
        Retry();
        if (hadFocus)
        {
            _runButton?.Focus(method);
        }
    }

    private void OnCopyRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Announce(Labels.AnnounceCopied);
    }

    #endregion
}
