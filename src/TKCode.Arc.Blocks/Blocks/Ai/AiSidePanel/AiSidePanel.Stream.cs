using System.Text;
using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AiSidePanel
{
    /// <summary>An answer the panel is reading or writing.</summary>
    private sealed class Run(AiPanelTurn turn, AiPanelRequest request, AiPanelPacer pacer, TimeSpan started)
    {
        public AiPanelTurn Turn { get; } = turn;
        public AiPanelRequest Request { get; } = request;
        public AiPanelPacer Pacer { get; } = pacer;
        public TimeSpan Started { get; } = started;
        public CancellationTokenSource Cancel { get; } = new();
        public StringBuilder Shown { get; } = new();
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The responder sent everything; the backlog may still be released.</summary>
        public bool StreamEnded { get; set; }

        /// <summary>The proposed change, read after the text ended.</summary>
        public AiPanelChangeProposal? Proposal { get; set; }

        /// <summary>Why the responder failed (shown once the reading minimum passed).</summary>
        public string? Error { get; set; }

        /// <summary>The turn has its final state.</summary>
        public bool Finished { get; set; }
    }

    private readonly LiveLoop _loop;
    private readonly Dictionary<AiPanelTurn, AiPanelRequest> _requests = new(ReferenceEqualityComparer.Instance);
    private Run? _run;
    private TimeSpan _clock;

    /// <summary>The answer being read or written (tests).</summary>
    internal AiPanelTurn? RunningTurn => _run?.Turn;

    /// <summary>The stream clock (tests).</summary>
    internal TimeSpan StreamClock => _clock;

    #region Asking

    /// <summary>
    /// Asks <paramref name="prompt"/> as if the person sent it from the box: the question joins the thread with the selection
    /// as its context, "Reading the page" shimmers while <see cref="Responder"/> works (at least 0.6s), then the answer is
    /// written in whole-word steps and a proposed change shows as a card. Completes when the turn ends (done, stopped or
    /// failed). Ignored while <see cref="IsBusy"/>, without a responder or with nothing to ask.
    /// </summary>
    /// <param name="prompt">The question.</param>
    public Task AskAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy || Responder is null)
        {
            return Task.CompletedTask;
        }
        prompt = prompt.Trim();
        var selection = Selection;
        var turn = new AiPanelTurn { Prompt = prompt, ContextLabel = selection?.Label, State = AiPanelTurnState.Reading };
        if (Suggestions.Contains(prompt))
        {
            _used.Add(prompt);
        }
        _animateNext = turn;
        try
        {
            Turns.Add(turn);
        }
        finally
        {
            _animateNext = null;
        }
        // Sending scrolls to the end, even from far up (spec 5.4).
        _scroller?.ScrollToLatest();
        var request = new AiPanelRequest(prompt, Page ?? new AiPanelPage(), selection);
        return Start(turn, request);
    }

    /// <summary>Runs <paramref name="request"/> for <paramref name="turn"/> (a new question or a retry).</summary>
    private Task Start(AiPanelTurn turn, AiPanelRequest request)
    {
        var responder = Responder!;
        turn.State = AiPanelTurnState.Reading;
        turn.Answer = string.Empty;
        turn.ErrorMessage = null;
        turn.Change = null;
        var run = new Run(turn, request, new AiPanelPacer(StreamInterval, MinWordsPerStep, MaxWordsPerStep), _clock);
        _run = run;
        _requests[turn] = request;
        ViewOf(turn)?.BeginStream();
        UpdateBusy();
        RefreshSuggestions();
        Announce(Labels.AnnounceReading(ModelName ?? AssistantName));
        _loop.Start();
        _ = ReadAsync(run, responder);
        return run.Completion.Task;
    }

    /// <summary>Reads the responder on the UI thread: the reply, then its text increments into the pacer.</summary>
    private async Task ReadAsync(Run run, IAiPanelResponder responder)
    {
        var token = run.Cancel.Token;
        try
        {
            var reply = await responder.RespondAsync(run.Request, token);
            await foreach (var text in reply.Text.WithCancellation(token))
            {
                if (run.Finished)
                {
                    return;
                }
                if (!string.IsNullOrEmpty(text))
                {
                    run.Pacer.Push(text);
                    _loop.Start();
                }
            }
            if (run.Finished)
            {
                return;
            }
            token.ThrowIfCancellationRequested();
            run.Proposal = reply.Change;
            run.StreamEnded = true;
            run.Pacer.Complete();
            _loop.Start();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || run.Finished)
        {
            // Stopped: Stop() already ended the turn.
        }
        catch (Exception exception)
        {
            if (!run.Finished)
            {
                run.Error = string.IsNullOrEmpty(exception.Message) ? exception.GetType().Name : exception.Message;
                _loop.Start();
            }
        }
    }

    #endregion

    #region Cadence

    /// <summary>
    /// One frame of the stream clock: "Reading the page" holds for at least <see cref="ReadingMinimum"/>, then a failure
    /// shows, or the answer is written one step at a time (a step fades in at full motion only); the turn is done in the
    /// frame that shows the last step after the stream ended.
    /// </summary>
    private bool Step(double dt)
    {
        _clock += TimeSpan.FromSeconds(dt);
        if (_run is not { Finished: false } run)
        {
            return false;
        }
        if (_clock - run.Started < ReadingMinimum)
        {
            return true;
        }
        if (run.Error is { } error)
        {
            Fail(run, error);
            return false;
        }
        if (run.Pacer.Step(_clock) is { } step)
        {
            if (run.Turn.State == AiPanelTurnState.Reading)
            {
                run.Turn.State = AiPanelTurnState.Streaming;
            }
            run.Shown.Append(step);
            run.Turn.Answer = run.Shown.ToString();
            ViewOf(run.Turn)?.AppendAnswer(step, ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full);
        }
        if (run.StreamEnded && run.Pacer.IsFinished)
        {
            Finish(run);
            return false;
        }
        // Nothing to release until more text arrives: sleep until the responder pushes (Push starts the loop again).
        return run.Pacer.PendingWords > 0;
    }

    #endregion

    #region Ending

    /// <summary>The answer is written: a proposed change becomes a card, the unused suggestions follow, Done is announced.</summary>
    private void Finish(Run run)
    {
        var turn = run.Turn;
        var change = MakeChange(run.Proposal);
        End(run);
        turn.Change = change;
        turn.State = AiPanelTurnState.Done;
        RefreshSuggestions();
        Announce(change is null ? Labels.AnnounceDone : Labels.AnnounceProposed(AiPanelChangeCard.LabelOf(change.Before)));
    }

    private void Fail(Run run, string error)
    {
        var turn = run.Turn;
        End(run);
        turn.ErrorMessage = error;
        turn.State = AiPanelTurnState.Failed;
        RefreshSuggestions();
        Announce(Labels.AnnounceFailed(ModelName ?? AssistantName, error));
    }

    /// <summary>
    /// Stops the answer being read or written (the stop button and Escape in the box do this): the text shown so far
    /// stays, "Stopped" follows it, no change card is made and the unused suggestions follow.
    /// </summary>
    public void Stop()
    {
        if (_run is not { Finished: false })
        {
            return;
        }
        CancelRun(announce: true);
    }

    /// <summary>Ends the running answer as stopped (with or without the announcement).</summary>
    private void CancelRun(bool announce)
    {
        if (_run is not { Finished: false } run)
        {
            return;
        }
        var turn = run.Turn;
        run.Cancel.Cancel();
        End(run);
        turn.Answer = run.Shown.ToString();
        turn.State = AiPanelTurnState.Stopped;
        RefreshSuggestions();
        if (announce)
        {
            Announce(Labels.AnnounceStopped);
        }
    }

    private void End(Run run)
    {
        run.Finished = true;
        if (ReferenceEquals(_run, run))
        {
            _run = null;
        }
        _loop.Stop();
        run.Turn.Answer = run.Shown.ToString();
        if (ViewOf(run.Turn) is { } view)
        {
            view.IsDriven = false;
        }
        UpdateBusy();
        run.Cancel.Dispose();
        run.Completion.TrySetResult();
    }

    /// <summary>A failed turn asks again with the same question and selection, about the page as it is now.</summary>
    private void OnRetryRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsBusy || Responder is null)
        {
            return;
        }
        var view = (e.Source as Avalonia.Visual)?.FindAncestorOfType<AiPanelTurnView>();
        if (view is null || view.Turn.State != AiPanelTurnState.Failed)
        {
            return;
        }
        var turn = view.Turn;
        var page = Page ?? new AiPanelPage();
        var request = _requests.TryGetValue(turn, out var asked)
            ? asked with { Page = page }
            : new AiPanelRequest(turn.Prompt, page, null);
        // The focus was on Retry, which goes away: the box takes it.
        _box?.FocusInput(FocusMethod);
        _ = Start(turn, request);
    }

    /// <summary><see cref="IsBusy"/>: an answer is read or written; the box's button stops it.</summary>
    private void UpdateBusy()
    {
        var busy = _run is { Finished: false };
        IsBusy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_box is not null)
        {
            _box.IsBusy = busy;
            _box.CanSend = Responder is not null;
        }
    }

    #endregion
}
