using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class ModelArena
{
    /// <summary>One model's answer in the current round: its stream, its cadence and what was measured on the raw stream.</summary>
    private sealed class PaneRun(ArenaSlot slot, AiModel model, string prompt, WordPacer pacer, DateTimeOffset started)
    {
        public ArenaSlot Slot { get; } = slot;
        public AiModel Model { get; } = model;
        public string Prompt { get; } = prompt;
        public WordPacer Pacer { get; } = pacer;
        public DateTimeOffset Started { get; } = started;
        public CancellationTokenSource Cancel { get; } = new();

        /// <summary>Everything the stream sent; <see cref="Shown"/> is always its beginning.</summary>
        public StringBuilder Raw { get; } = new();

        /// <summary>What the pacer has released to the pane.</summary>
        public StringBuilder Shown { get; } = new();

        /// <summary>A retry: the failed answer's text stays until this run's first words replace it.</summary>
        public bool KeepsOldText { get; set; }

        /// <summary>Characters received (the output estimate when the runner reports no tokens).</summary>
        public int Characters { get; set; }

        public int? ReportedInput { get; set; }

        public int? ReportedOutput { get; set; }

        /// <summary>From the start to the first non-empty text; null until it arrives.</summary>
        public TimeSpan? FirstToken { get; set; }

        /// <summary>When the last text or count arrived.</summary>
        public DateTimeOffset? LastEvent { get; set; }

        /// <summary>When the stream ended (all events received).</summary>
        public DateTimeOffset? Ended { get; set; }

        public bool StreamEnded { get; set; }

        /// <summary>The answer has its final status (done, stopped or failed); events that still arrive are ignored.</summary>
        public bool IsOver { get; set; }

        /// <summary>When the first-token timer shows next (every 100ms from the start, whatever the frame times).</summary>
        public DateTimeOffset NextTick { get; set; }

        /// <summary>When the speed and the cost are sampled next.</summary>
        public DateTimeOffset NextSample { get; set; }
    }

    /// <summary>Between the last answer and the vote row (spec 5.2: about 116ms after the second answer is complete).</summary>
    internal static readonly TimeSpan VoteDelay = TimeSpan.FromMilliseconds(116);

    /// <summary>How often the first-token timer is shown while a model thinks (spec 5.1: every 100ms).</summary>
    internal static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>How often speed and cost are sampled after the first token (spec 5.1: every 500ms).</summary>
    internal static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);

    private readonly LiveLoop _loop;
    private TimeSpan _clock;
    private PaneRun? _runA;
    private PaneRun? _runB;
    private (AiModel A, AiModel B)? _pair;
    private string _roundPrompt = string.Empty;
    private int _lastSampleIndex = -1;
    private TaskCompletionSource? _roundEnd;
    private TimeSpan? _voteDue;
    private bool _revealedByVote;
    private bool _prefilled;
    private bool _autoStarted;

    #region Seams (tests)

    /// <summary>The clock of the measured times (first token, speed); tests give their own.</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>The default pair picker's dice; tests give a seeded one.</summary>
    internal Random Random { get; set; } = Random.Shared;

    /// <summary>The shared stream clock: the seconds of every frame the loop ran, the time both pacers are fed.</summary>
    internal TimeSpan Clock => _clock;

    /// <summary>The two models of the current round.</summary>
    internal (AiModel A, AiModel B)? CurrentPair => _pair;

    /// <summary>The prompt the current round was asked.</summary>
    internal string RoundPrompt => _roundPrompt;

    /// <summary>Whether both answers are in and the vote row is about to replace the waiting row.</summary>
    internal bool IsVoteRowPending => _voteDue is not null;

    /// <summary>The loop that drives both pacers and the timers.</summary>
    internal LiveLoop Loop => _loop;

    /// <summary>Whether the models may be shown: Blind is off, or the round was voted on (and Blind not turned on since).</summary>
    internal bool IsRevealed => !IsBlind || _revealedByVote;

    #endregion

    #region Public methods

    /// <summary>
    /// Starts a round: <see cref="Round"/> grows by one, both answers clear, and <paramref name="prompt"/> (or the box's text)
    /// goes to the round's two models, the same pair as the last round (only <see cref="NextRound"/> draws a new one; the
    /// first round draws one). A prompt given here also goes in the box. A round that is still running is replaced: its
    /// streams are cancelled and it ends as stopped, quietly. Does nothing without a <see cref="Runner"/>, without two
    /// contestants or without a prompt. Completes when both answers are in (the vote is open), or the round stopped or failed.
    /// </summary>
    /// <param name="prompt">The prompt; null runs the box's text.</param>
    public Task RunAsync(string? prompt = null)
    {
        var text = prompt ?? PromptText;
        if (!CanStart || string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }
        if (prompt is not null && !string.Equals(PromptText, prompt, StringComparison.Ordinal))
        {
            SetCurrentValue(PromptTextProperty, prompt);
        }
        return StartRound(text.Trim(), newPair: false);
    }

    /// <summary>
    /// Stops the running round (the Stop button and Escape in the prompt row do this): both streams are cancelled, an
    /// answer that was not complete keeps its text and becomes Stopped, a complete one stays as it is, and
    /// <see cref="RunStopped"/> is raised.
    /// </summary>
    public void Stop()
    {
        if (Phase != ArenaPhase.Running)
        {
            return;
        }
        StopRunning(announce: true);
    }

    /// <summary>
    /// Starts a round with a new pair (from <see cref="PairPicker"/>, or two random models that are not the last pair) and,
    /// when there are sample prompts, the next one in the box. The focus moves to Stop if it was in the arena.
    /// </summary>
    public void NextRound()
    {
        if (!CanStart)
        {
            return;
        }
        var prompt = PromptText;
        if (Prompts.Count > 0)
        {
            var next = Prompts[(_lastSampleIndex + 1) % Prompts.Count].Text;
            if (!string.IsNullOrWhiteSpace(next))
            {
                prompt = next;
                SetCurrentValue(PromptTextProperty, next);
            }
        }
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }
        var hadFocus = IsKeyboardFocusWithin;
        var method = FocusMethod;
        _ = StartRound(prompt.Trim(), newPair: true);
        if (hadFocus)
        {
            // Next round leaves with its row: Run, now Stop, takes the focus (spec 6.2).
            _form?.FocusRun(method);
        }
    }

    /// <summary>
    /// Runs a failed answer again: only that model, with the round's prompt; its error card closes and it thinks again,
    /// while the round and the pair stay. Does nothing unless the answer in <paramref name="slot"/> failed.
    /// </summary>
    /// <param name="slot">The answer to retry.</param>
    public void Retry(ArenaSlot slot)
    {
        if (StateOf(slot).Status != ArenaPaneStatus.Failed || _pair is not { } pair || Runner is not { } runner)
        {
            return;
        }
        var run = StartPane(slot, slot == ArenaSlot.A ? pair.A : pair.B, _roundPrompt, Time.GetUtcNow(), retry: true);
        SetPhase(ArenaPhase.Running);
        SyncVoteBar();
        _ = ConsumeAsync(run, runner);
        _loop.Start();
    }

    /// <summary>Puts the keyboard focus in the prompt box.</summary>
    public void FocusPrompt()
    {
        ApplyTemplate();
        _form?.FocusBox(FocusMethod);
    }

    #endregion

    #region Starting

    /// <summary>Whether a round can start at all: a runner and two contestants.</summary>
    private bool CanStart => Runner is not null && Contestants.Count >= 2;

    private Task StartRound(string prompt, bool newPair)
    {
        var runner = Runner!;
        CancelQuietly();
        var pair = _pair is { } last && !newPair && Contestants.Contains(last.A) && Contestants.Contains(last.B)
            ? last
            : PickPair(_pair);
        _pair = pair;
        _roundPrompt = prompt;
        NoteSample(prompt);
        // Hidden before anything else changes: no new model may show for a moment (spec 9.3).
        _revealedByVote = false;
        SyncReveal();
        _voteDue = null;
        LastVote = null;
        Round++;
        var now = Time.GetUtcNow();
        var runA = StartPane(ArenaSlot.A, pair.A, prompt, now);
        var runB = StartPane(ArenaSlot.B, pair.B, prompt, now);
        var end = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _roundEnd = end;
        SetPhase(ArenaPhase.Running);
        SyncVoteBar();
        Announce(Labels.AnnounceStarted);
        RaiseEvent(new ArenaRoundEventArgs(RoundStartedEvent, this, Round, prompt, pair.A, pair.B));
        // A handler may have stopped or replaced the round.
        if (ReferenceEquals(_runA, runA) && !runA.IsOver)
        {
            _ = ConsumeAsync(runA, runner);
        }
        if (ReferenceEquals(_runB, runB) && !runB.IsOver)
        {
            _ = ConsumeAsync(runB, runner);
        }
        _loop.Start();
        return end.Task;
    }

    /// <summary>
    /// A pane starts over: it thinks again, its error clears, the first-token timer runs from 0 and the input cost shows. A
    /// new round clears the text at once (spec 5.1); a retry keeps the failed text while the error card folds away and
    /// clears it when the new first words arrive (spec 1 item 11). The status changes before the error fields, so a failed
    /// pane never shows its card again with empty words.
    /// </summary>
    private PaneRun StartPane(ArenaSlot slot, AiModel model, string prompt, DateTimeOffset now, bool retry = false)
    {
        var run = new PaneRun(slot, model, prompt, new WordPacer(PacerOptions()), now)
        {
            NextTick = now + TimerInterval,
            KeepsOldText = retry
        };
        if (slot == ArenaSlot.A)
        {
            _runA = run;
        }
        else
        {
            _runB = run;
        }
        var state = StateOf(slot);
        var view = PaneViewOf(slot);
        state.Model = model;
        state.Status = ArenaPaneStatus.Thinking;
        state.ErrorTitle = null;
        state.ErrorText = null;
        state.IsRetryable = true;
        state.Metrics = Snapshot(run);
        if (!retry)
        {
            state.Text = string.Empty;
            view?.Reset();
        }
        if (view is not null)
        {
            view.Elapsed = TimeSpan.Zero;
        }
        SyncTiming();
        return run;
    }

    /// <summary>The cadence of both answers: <see cref="Pacing"/> as <see cref="WordPacer"/> options (unusable values fall back to Arc's).</summary>
    private WordPacerOptions PacerOptions()
    {
        var pacing = Pacing ?? ArenaPacing.Default;
        var speed = pacing.WordsPerSecond is > 0 and < double.PositiveInfinity ? pacing.WordsPerSecond : ArenaPacing.Default.WordsPerSecond;
        return new WordPacerOptions
        {
            MinInterval = pacing.Interval > TimeSpan.Zero ? pacing.Interval : TimeSpan.Zero,
            WordsPerSecond = speed,
            MaxWordsPerBatch = Math.Max(1, pacing.MaxWordsPerStep)
        };
    }

    /// <summary>
    /// The pair of a round: <see cref="PairPicker"/>'s choice when it gives two different models; otherwise the first two
    /// contestants for the first round (a predictable start, spec 10.1) and after that two random ones that are not the last
    /// pair again (when the pool has another pair to offer).
    /// </summary>
    private (AiModel A, AiModel B) PickPair((AiModel A, AiModel B)? previous)
    {
        var pool = Contestants;
        if (PairPicker is { } picker)
        {
            var picked = picker.Pick(pool, previous);
            if (picked.A is not null && picked.B is not null && !ReferenceEquals(picked.A, picked.B))
            {
                return picked;
            }
        }
        if (previous is not { } last)
        {
            return (pool[0], pool[1]);
        }
        var avoid = pool.Count > 2;
        var options = new List<(int A, int B)>();
        for (var i = 0; i < pool.Count; i++)
        {
            for (var j = 0; j < pool.Count; j++)
            {
                if (i == j || avoid && IsSamePair(last, pool[i], pool[j]))
                {
                    continue;
                }
                options.Add((i, j));
            }
        }
        if (options.Count == 0)
        {
            return (pool[0], pool[1]);
        }
        var (a, b) = options[Random.Next(options.Count)];
        return (pool[a], pool[b]);
    }

    private static bool IsSamePair((AiModel A, AiModel B) pair, AiModel a, AiModel b) =>
        SameModel(pair.A, a) && SameModel(pair.B, b) || SameModel(pair.A, b) && SameModel(pair.B, a);

    private static bool SameModel(AiModel x, AiModel y) =>
        ReferenceEquals(x, y) || !string.IsNullOrEmpty(x.Id) && string.Equals(x.Id, y.Id, StringComparison.Ordinal);

    /// <summary>Remembers which sample prompt ran last, so that Next round takes the one after it.</summary>
    private void NoteSample(string prompt)
    {
        for (var i = 0; i < Prompts.Count; i++)
        {
            if (string.Equals(Prompts[i].Text.Trim(), prompt, StringComparison.Ordinal))
            {
                _lastSampleIndex = i;
                return;
            }
        }
    }

    private void TryAutoStart()
    {
        if (!AutoStart || _autoStarted || Round > 0 || !IsLoaded || !IsEffectivelyVisible)
        {
            return;
        }
        Prefill();
        if (!CanStart || string.IsNullOrWhiteSpace(PromptText))
        {
            return;
        }
        _autoStarted = true;
        _ = RunAsync();
    }

    #endregion

    #region Streams

    /// <summary>Reads one model's stream on the UI thread: text into the pane's pacer, counts and times into its metrics.</summary>
    private async Task ConsumeAsync(PaneRun run, IArenaRunner runner)
    {
        var token = run.Cancel.Token;
        try
        {
            await foreach (var item in runner.RunAsync(run.Model, run.Prompt, token).WithCancellation(token))
            {
                if (run.IsOver)
                {
                    return;
                }
                Receive(run, item);
            }
            if (run.IsOver)
            {
                return;
            }
            token.ThrowIfCancellationRequested();
            EndStream(run);
        }
        catch (OperationCanceledException) when (run.IsOver || token.IsCancellationRequested)
        {
            // Stopped, replaced or detached: whoever cancelled it already ended the answer.
        }
        catch (OperationCanceledException)
        {
            // The runner gave up by itself: a cancellation counts as stopped (contract).
            if (!run.IsOver)
            {
                run.IsOver = true;
                EndPane(run, ArenaPaneStatus.Stopped);
                TryEndRound();
            }
        }
        catch (Exception exception)
        {
            if (!run.IsOver)
            {
                FailPane(run, exception);
            }
        }
    }

    /// <summary>
    /// One event of a raw stream. Text goes to the pacer (shown later on the shared clock); the first text stops the
    /// first-token timer on its measured value; counts replace the estimates.
    /// </summary>
    private void Receive(PaneRun run, ArenaStreamEvent item)
    {
        var now = Time.GetUtcNow();
        var counted = false;
        if (item.InputTokens is { } input)
        {
            run.ReportedInput = Math.Max(0, input);
            counted = true;
        }
        if (item.OutputTokens is { } output)
        {
            run.ReportedOutput = Math.Max(0, output);
            counted = true;
        }
        if (string.IsNullOrEmpty(item.Text))
        {
            if (counted)
            {
                run.LastEvent = now;
            }
            return;
        }
        run.Characters += item.Text.Length;
        run.Raw.Append(item.Text);
        run.LastEvent = now;
        run.Pacer.Push(item.Text);
        if (run.FirstToken is null)
        {
            var first = now - run.Started;
            run.FirstToken = first < TimeSpan.Zero ? TimeSpan.Zero : first;
            run.NextSample = now + SampleInterval;
            // The timer stops; the measured value shows. Speed and cost follow at the first sample, 500ms later.
            var state = StateOf(run.Slot);
            state.Metrics = state.Metrics with { TimeToFirstToken = run.FirstToken };
            SyncTiming();
        }
        _loop.Start();
    }

    /// <summary>The stream ended: the metrics take their final values; the answer is complete once its pacer has shown everything.</summary>
    private void EndStream(PaneRun run)
    {
        run.StreamEnded = true;
        run.Ended = Time.GetUtcNow();
        run.Pacer.Complete();
        StateOf(run.Slot).Metrics = Snapshot(run);
        SyncTiming();
        _loop.Start();
    }

    /// <summary>
    /// What the raw stream measured (spec 2.4): first token; speed = output tokens over the time from the first token to the
    /// last event; cost = input and output tokens times the model's prices per million (a missing price counts as 0);
    /// tokens the runner did not report are estimated at four characters each (the input from the prompt).
    /// </summary>
    private static ArenaMetrics Snapshot(PaneRun run)
    {
        var input = run.ReportedInput ?? EstimateTokens(run.Prompt.Length);
        var output = run.ReportedOutput ?? EstimateTokens(run.Characters);
        double? speed = null;
        if (run.FirstToken is { } first && run.LastEvent is { } last && output > 0)
        {
            var seconds = (last - (run.Started + first)).TotalSeconds;
            if (seconds > 0)
            {
                speed = output / seconds;
            }
        }
        var model = run.Model;
        var cost = (input * (model.InputPrice ?? 0m) + output * (model.OutputPrice ?? 0m)) / 1_000_000m;
        TimeSpan? total = run.Ended is { } ended ? ended - run.Started < TimeSpan.Zero ? TimeSpan.Zero : ended - run.Started : null;
        return new ArenaMetrics(run.FirstToken, speed, total, input, output, cost, run.ReportedInput is null || run.ReportedOutput is null);
    }

    private static int EstimateTokens(int characters) => characters <= 0 ? 0 : (characters + 3) / 4;

    #endregion

    #region Clock

    /// <summary>
    /// One frame of the shared clock (spec 5.3, 8): the clock advances by the frame's seconds and both pacers release what is
    /// due at that time (so a slow frame releases more, and both answers keep one cadence whatever their streams do); the
    /// first-token timer shows every 100ms while a model thinks, speed and cost every 500ms after its first token. An answer
    /// is complete in the frame that shows its last words after its stream ended. Runs while a model thinks or writes, and
    /// until the vote row is due.
    /// </summary>
    private bool Step(double dt)
    {
        _clock += TimeSpan.FromSeconds(dt);
        var now = Time.GetUtcNow();
        var completeA = Advance(_runA, now);
        var completeB = Advance(_runB, now);
        if (completeA || completeB)
        {
            CompleteAnswers(completeA ? _runA : null, completeB ? _runB : null);
        }
        if (_voteDue is { } due && _clock >= due)
        {
            _voteDue = null;
            SyncVoteBar();
        }
        return IsActive(_runA) || IsActive(_runB) || _voteDue is not null;
    }

    /// <summary>Advances one answer; true when it became complete in this frame.</summary>
    private bool Advance(PaneRun? run, DateTimeOffset now)
    {
        if (run is null || run.IsOver)
        {
            return false;
        }
        var state = StateOf(run.Slot);
        if (run.FirstToken is null && !run.StreamEnded && now >= run.NextTick)
        {
            // The pane's running reading; the metrics keep no first token until it arrives.
            run.NextTick = Next(run.NextTick, now, TimerInterval);
            if (PaneViewOf(run.Slot) is { } view)
            {
                view.Elapsed = now - run.Started;
            }
        }
        if (run.Pacer.Tick(_clock) is { } words)
        {
            if (state.Status == ArenaPaneStatus.Thinking)
            {
                state.Status = ArenaPaneStatus.Writing;
            }
            Show(run, words);
        }
        if (run.FirstToken is not null && !run.StreamEnded && now >= run.NextSample)
        {
            run.NextSample = Next(run.NextSample, now, SampleInterval);
            state.Metrics = Snapshot(run);
        }
        return run.StreamEnded && run.Pacer.IsFinished;
    }

    private static bool IsActive(PaneRun? run) => run is { IsOver: false };

    /// <summary>Released words go to the pane (a retry's first words replace the failed text) and to the state.</summary>
    private void Show(PaneRun run, string words)
    {
        var view = PaneViewOf(run.Slot);
        if (run.KeepsOldText)
        {
            run.KeepsOldText = false;
            view?.Reset();
        }
        run.Shown.Append(words);
        StateOf(run.Slot).Text = run.Shown.ToString();
        view?.Append(words);
    }

    /// <summary>The next time on a fixed grid (frames land late, the grid does not drift); after a long pause, one step from now.</summary>
    private static DateTimeOffset Next(DateTimeOffset due, DateTimeOffset now, TimeSpan interval) =>
        now - due >= interval ? now + interval : due + interval;

    #endregion

    #region Ending

    /// <summary>
    /// Answers that became complete: the first to be complete in the round is Finished first (also when the other one
    /// failed), the other one Done; two that complete in the same frame are both Done.
    /// </summary>
    private void CompleteAnswers(PaneRun? a, PaneRun? b)
    {
        var first = !IsComplete(PaneA.Status) && !IsComplete(PaneB.Status) && (a is null || b is null);
        foreach (var run in new[] { a, b })
        {
            if (run is null)
            {
                continue;
            }
            run.IsOver = true;
            EndPane(run, first ? ArenaPaneStatus.FinishedFirst : ArenaPaneStatus.Done);
            if (first)
            {
                Announce(Labels.AnnounceFinishedFirst(SlotName(run.Slot)));
            }
        }
        TryEndRound();
    }

    private static bool IsComplete(ArenaPaneStatus status) => status is ArenaPaneStatus.Done or ArenaPaneStatus.FinishedFirst;

    /// <summary>An answer gets its final status: the pane's text is completed first (no caret stays), then the status shows.</summary>
    private void EndPane(PaneRun run, ArenaPaneStatus status)
    {
        run.IsOver = true;
        var state = StateOf(run.Slot);
        PaneViewOf(run.Slot)?.Complete();
        if (!run.KeepsOldText)
        {
            state.Text = run.Shown.ToString();
        }
        if (status is ArenaPaneStatus.Stopped or ArenaPaneStatus.Failed)
        {
            state.Metrics = Snapshot(run);
        }
        state.Status = status;
        SyncTiming();
    }

    /// <summary>
    /// A stream failed: the answer keeps what it showed, its error card says what the <see cref="ArenaRunException"/> says
    /// (or the labels' defaults; other exceptions can be retried), and the other answer goes on.
    /// </summary>
    private void FailPane(PaneRun run, Exception exception)
    {
        var labels = Labels;
        var known = exception as ArenaRunException;
        var state = StateOf(run.Slot);
        state.ErrorTitle = string.IsNullOrEmpty(known?.Title) ? labels.ErrorTitle : known.Title;
        state.ErrorText = known is not null && !string.IsNullOrEmpty(known.Message) ? known.Message : labels.ErrorText;
        state.IsRetryable = known?.IsRetryable ?? true;
        run.IsOver = true;
        // What arrived before the failure stays: the words still in the pacer show at once (the pane takes no text once
        // it shows the error card).
        if (run.Raw.Length > run.Shown.Length)
        {
            Show(run, run.Raw.ToString(run.Shown.Length, run.Raw.Length - run.Shown.Length));
        }
        EndPane(run, ArenaPaneStatus.Failed);
        var bothFailed = StateOf(Other(run.Slot)).Status == ArenaPaneStatus.Failed;
        Announce(bothFailed ? labels.AnnounceBothFailed : labels.AnnounceFailed(SlotName(run.Slot)));
        RaiseEvent(new ArenaPaneFailedEventArgs(PaneFailedEvent, this, run.Slot, run.Model, exception));
        TryEndRound();
    }

    /// <summary>
    /// Once neither answer is running the round ends: stopped if one was stopped, failed if one failed (no vote), otherwise
    /// the vote opens (its row about 116ms later).
    /// </summary>
    private void TryEndRound()
    {
        if (IsActive(_runA) || IsActive(_runB))
        {
            SetPhase(ArenaPhase.Running);
            SyncVoteBar();
            return;
        }
        var a = PaneA.Status;
        var b = PaneB.Status;
        if (a == ArenaPaneStatus.Stopped || b == ArenaPaneStatus.Stopped)
        {
            EnterStopped(announce: true);
            return;
        }
        if (a == ArenaPaneStatus.Failed || b == ArenaPaneStatus.Failed)
        {
            SetPhase(ArenaPhase.Failed);
            SyncVoteBar();
        }
        else
        {
            SetPhase(ArenaPhase.Voting);
            _voteDue = _clock + VoteDelay;
            _loop.Start();
            SyncVoteBar();
            Announce(Labels.AnnounceReady);
        }
        EndRoundTask();
    }

    /// <summary>Stops every answer still running, then the round.</summary>
    private void StopRunning(bool announce)
    {
        foreach (var run in new[] { _runA, _runB })
        {
            if (run is not { IsOver: false })
            {
                continue;
            }
            // Over before the cancel: a stream that ends inside Cancel() finds the answer already stopped.
            run.IsOver = true;
            run.Cancel.Cancel();
            EndPane(run, ArenaPaneStatus.Stopped);
        }
        EnterStopped(announce);
    }

    private void EnterStopped(bool announce)
    {
        _voteDue = null;
        SetPhase(ArenaPhase.Stopped);
        SyncVoteBar();
        if (announce)
        {
            Announce(Labels.AnnounceStopped);
        }
        if (_pair is { } pair)
        {
            RaiseEvent(new ArenaRoundEventArgs(RunStoppedEvent, this, Round, _roundPrompt, pair.A, pair.B));
        }
        EndRoundTask();
    }

    /// <summary>
    /// A new run replaces a running round: its streams are cancelled and <see cref="RunStopped"/> is raised for it, without
    /// touching the panes (they start over at once) and without an announcement.
    /// </summary>
    private void CancelQuietly()
    {
        if (Phase != ArenaPhase.Running)
        {
            return;
        }
        foreach (var run in new[] { _runA, _runB })
        {
            if (run is { IsOver: false })
            {
                run.IsOver = true;
                run.Cancel.Cancel();
            }
        }
        if (_pair is { } pair)
        {
            RaiseEvent(new ArenaRoundEventArgs(RunStoppedEvent, this, Round, _roundPrompt, pair.A, pair.B));
        }
        EndRoundTask();
    }

    private void EndRoundTask()
    {
        var end = _roundEnd;
        _roundEnd = null;
        end?.TrySetResult();
    }

    /// <summary>The arena left the window: the answers stop (no announcement), the vote row shows if it was due, the clock stops.</summary>
    private void Detached()
    {
        if (Phase == ArenaPhase.Running)
        {
            StopRunning(announce: false);
        }
        if (_voteDue is not null)
        {
            _voteDue = null;
            SyncVoteBar();
        }
        _loop.Stop();
        SetCurrentValue(IsLeaderboardOpenProperty, false);
    }

    #endregion

    #region State

    private void SetPhase(ArenaPhase phase)
    {
        Phase = phase;
        PseudoClasses.Set(RunningPseudoClass, phase == ArenaPhase.Running);
        PseudoClasses.Set(VotingPseudoClass, phase == ArenaPhase.Voting);
        PseudoClasses.Set(VotedPseudoClass, phase == ArenaPhase.Voted);
        SyncRunParts();
    }

    /// <summary>Run is Stop while a model runs; it needs a runner and two contestants (the form adds "and a prompt").</summary>
    private void SyncRunParts()
    {
        if (_form is null)
        {
            return;
        }
        _form.IsRunning = Phase == ArenaPhase.Running;
        _form.CanRun = CanStart;
    }

    /// <summary>The panes (and the tab strip) show the models only once they are revealed; the identities keep the privacy rule themselves.</summary>
    private void SyncReveal()
    {
        var revealed = IsRevealed;
        if (_paneViewA is not null)
        {
            _paneViewA.IsRevealed = revealed;
        }
        if (_paneViewB is not null)
        {
            _paneViewB.IsRevealed = revealed;
        }
        if (_tabs is not null)
        {
            _tabs.IsRevealed = revealed;
        }
    }

    /// <summary>The first-token timer runs while a model thinks.</summary>
    private void SyncTiming()
    {
        SyncTiming(_paneViewA, _runA);
        SyncTiming(_paneViewB, _runB);
    }

    /// <summary>A pane times while its model thinks (from its <see cref="ArenaPane.Elapsed"/> reading); then it shows the measured first token.</summary>
    private static void SyncTiming(ArenaPane? view, PaneRun? run)
    {
        if (view is null)
        {
            return;
        }
        var timing = IsTiming(run);
        view.IsTiming = timing;
        if (!timing)
        {
            view.Elapsed = null;
        }
        else if (view.Elapsed is null)
        {
            view.Elapsed = TimeSpan.Zero;
        }
    }

    private static bool IsTiming(PaneRun? run) => run is { IsOver: false, FirstToken: null, StreamEnded: false };

    private ArenaPaneState StateOf(ArenaSlot slot) => slot == ArenaSlot.A ? PaneA : PaneB;

    private ArenaPane? PaneViewOf(ArenaSlot slot) => slot == ArenaSlot.A ? _paneViewA : _paneViewB;

    private static ArenaSlot Other(ArenaSlot slot) => slot == ArenaSlot.A ? ArenaSlot.B : ArenaSlot.A;

    /// <summary>The slot as the labels take it: "A" or "B".</summary>
    private static string SlotName(ArenaSlot slot) => slot == ArenaSlot.A ? "A" : "B";

    #endregion
}
