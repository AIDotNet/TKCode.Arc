using System.Collections.Specialized;
using System.Text;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AiAnswer
{
    /// <summary>A version the block writes from <see cref="Responder"/>.</summary>
    private sealed class AnswerRun(AiAnswerVersion version, AiAnswerRequest request, DateTimeOffset started)
    {
        public AiAnswerVersion Version { get; } = version;
        public AiAnswerRequest Request { get; } = request;
        public CancellationTokenSource Cancel { get; } = new();
        public StringBuilder Raw { get; } = new();
        public DateTimeOffset Started { get; } = started;

        /// <summary>How long the model thought; null while it still thinks.</summary>
        public TimeSpan? Thought { get; set; }

        public int? Tokens { get; set; }
        public TimeSpan? Duration { get; set; }

        /// <summary>The responder sent everything; the text may still be revealing.</summary>
        public bool StreamEnded { get; set; }

        /// <summary>The version has its outcome.</summary>
        public bool Finished { get; set; }

        /// <summary>Completes when the version is ready, stopped or failed (<see cref="AiAnswer.AskAsync"/>).</summary>
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private AnswerRun? _run;
    private AiAnswerVersion? _staticVersion;
    private bool _settingQuestion;
    private bool _questionPending;
    private bool _pendingAsked;
    private bool _staticSuppressed;
    private int _answerStamp;
    private readonly StringBuilder _reasoningShown = new();
    private WordPacer? _reasoningPacer;
    private LiveLoop? _reasoningLoop;
    private TimeSpan _reasoningClock;

    /// <summary>Whether a version is being written (tests).</summary>
    internal bool IsRunning => _run is not null;

    #region Asking

    /// <summary>
    /// Asks <paramref name="question"/> as if the person had: <see cref="QuestionAsked"/> is raised (set <c>Cancel</c> to ask
    /// nothing), the page is replaced in place (the title, the question, a new list of versions; an answer still being
    /// written is cancelled) and, with a <see cref="Responder"/>, the answer streams in. The task completes when the answer
    /// is ready, stopped or failed (at once when nothing was asked or there is no responder).
    /// </summary>
    /// <param name="question">The question.</param>
    public Task AskAsync(string question) => Ask(question, AiAnswerQuestionOrigin.Programmatic) ?? Task.CompletedTask;

    /// <summary>Asks a question from <paramref name="origin"/>; null when nothing was asked.</summary>
    internal Task? Ask(string? question, AiAnswerQuestionOrigin origin)
    {
        var text = question?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }
        var stamp = _answerStamp;
        var args = new AiAnswerQuestionEventArgs(QuestionAskedEvent, this, text, origin);
        RaiseEvent(args);
        if (args.Cancel)
        {
            return null;
        }
        if (origin != AiAnswerQuestionOrigin.Programmatic)
        {
            Run(AskCommand, text);
        }
        if (_run is { } running)
        {
            // The page is replaced: the old answer goes without a word.
            StopRun(running, announce: false);
        }
        _questionPending = false;
        _pendingAsked = false;
        // Without a responder the page shows what the host hands over while it is asked (a new Answer), or nothing.
        _staticSuppressed = stamp == _answerStamp;
        ClearVersions();
        SetQuestion(text);
        if (Responder is not null && this.IsAttachedToVisualTree())
        {
            return StartRun(text, AiAnswerMode.Answer, EffectiveModel, previousMarkdown: null, PageChange.Question);
        }
        if (Responder is not null)
        {
            // Asked (the event was raised) but there is no window yet: the answer starts once there is one.
            _questionPending = true;
            _pendingAsked = true;
        }
        RefreshStatic();
        ShowPage(PageChange.Question);
        return Task.CompletedTask;
    }

    /// <summary>Sets <see cref="Question"/> without asking it again.</summary>
    private void SetQuestion(string? question)
    {
        _settingQuestion = true;
        try
        {
            SetCurrentValue(QuestionProperty, question);
        }
        finally
        {
            _settingQuestion = false;
        }
    }

    /// <summary>
    /// Code set <see cref="Question"/>: with a responder (and no finished <see cref="Answer"/> to show) it is asked, once the
    /// block is in a window; otherwise it heads the finished answer. Clearing it shows the empty page.
    /// </summary>
    private void OnQuestionSet(string? oldValue, string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            if (_run is { } running)
            {
                StopRun(running, announce: false);
            }
            _questionPending = false;
            _pendingAsked = false;
            if (Versions.Count > 0 && !ReferenceEquals(Versions[0], _staticVersion))
            {
                ClearVersions();
            }
            RefreshStatic();
            ShowPage(PageChange.Question);
            return;
        }
        if (Responder is not null && string.IsNullOrEmpty(Answer))
        {
            if (this.IsAttachedToVisualTree())
            {
                if (Ask(newValue, AiAnswerQuestionOrigin.Programmatic) is null)
                {
                    // Cancelled: the question stays what it was.
                    SetQuestion(oldValue);
                }
                return;
            }
            _questionPending = true;
            _pendingAsked = false;
        }
        ShowPage(PageChange.Question);
    }

    /// <summary>A question set before there was a window or a responder is asked now.</summary>
    private void AskPendingQuestion()
    {
        if (!_questionPending || Responder is null || !this.IsAttachedToVisualTree() || string.IsNullOrWhiteSpace(Question) || _run is not null)
        {
            return;
        }
        _questionPending = false;
        if (_pendingAsked)
        {
            // QuestionAsked was raised when it was asked: start the answer without asking again.
            _pendingAsked = false;
            _staticSuppressed = true;
            ClearVersions();
            _ = StartRun(Question!.Trim(), AiAnswerMode.Answer, EffectiveModel, previousMarkdown: null, PageChange.Question);
            return;
        }
        _ = Ask(Question, AiAnswerQuestionOrigin.Programmatic);
    }

    #endregion

    #region Static answer

    private void OnStaticCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnStaticChanged();

    private void OnStaticChanged()
    {
        if (RefreshStatic())
        {
            ShowPage(PageChange.Static);
        }
    }

    /// <summary>
    /// The finished answer of <see cref="Answer"/> (with <see cref="Reasoning"/>, <see cref="Usage"/>, <see cref="Sources"/>,
    /// <see cref="FollowUps"/> and the model) as the only version, shown whole without the reveal. Versions the responder
    /// wrote take over; a new question without a responder hides it until a new answer is set. Returns whether the static
    /// version changed or went.
    /// </summary>
    private bool RefreshStatic()
    {
        if (_run is not null)
        {
            return false;
        }
        foreach (var version in Versions)
        {
            if (!ReferenceEquals(version, _staticVersion))
            {
                return false;
            }
        }
        if (_staticSuppressed || string.IsNullOrEmpty(Answer))
        {
            if (_staticVersion is null)
            {
                return false;
            }
            ClearVersions();
            return true;
        }
        var created = _staticVersion is null;
        var v = _staticVersion ??= new AiAnswerVersion { Mode = AiAnswerMode.Answer, Outcome = AiAnswerOutcome.Complete };
        v.Model = EffectiveModel;
        v.Markdown = Answer ?? string.Empty;
        v.Reasoning = Reasoning ?? string.Empty;
        v.Usage = Usage;
        v.Sources.Clear();
        Number(Sources);
        v.Sources.AddRange(Sources);
        v.FollowUps.Clear();
        v.FollowUps.AddRange(FollowUps);
        if (created || Versions.Count == 0)
        {
            AddVersion(v);
            SetIndex(0);
        }
        return true;
    }

    /// <summary>Sources without their own number take their place in the list (from 1); numbers that were set stay.</summary>
    private static void Number(IList<AiSource> sources)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] is { } source && (!source.IsSet(AiSource.IndexProperty) || source.Index <= 0))
            {
                source.Index = i + 1;
            }
        }
    }

    #endregion

    #region Writing a version

    /// <summary>Adds a version and writes it from the responder; the page moves to it.</summary>
    private Task StartRun(string question, AiAnswerMode mode, AiModel? model, string? previousMarkdown, PageChange change)
    {
        var version = new AiAnswerVersion { Mode = mode, Model = model };
        var request = new AiAnswerRequest(question, mode, model, previousMarkdown);
        var run = new AnswerRun(version, request, Time.GetUtcNow());
        _run = run;
        ResetReasoning();
        AddVersion(version);
        SetIndex(Versions.Count - 1);
        ShowPage(change);
        AnnounceStatus(Labels.AnnounceThinking);
        if (Responder is { } responder)
        {
            _ = RunAsync(run, responder);
        }
        return run.Completion.Task;
    }

    /// <summary>Reads the responder's events on the UI thread and writes them into the version.</summary>
    private async Task RunAsync(AnswerRun run, IAiAnswerResponder responder)
    {
        var token = run.Cancel.Token;
        try
        {
            await foreach (var item in responder.RespondAsync(run.Request, token).WithCancellation(token))
            {
                if (run.Finished)
                {
                    return;
                }
                Apply(run, item);
            }
            if (!run.Finished)
            {
                if (token.IsCancellationRequested)
                {
                    StopRun(run, announce: true);
                }
                else
                {
                    EndStream(run);
                }
            }
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
                FailRun(run, exception.Message);
            }
        }
    }

    /// <summary>One event of the stream: reasoning, sources, the end of thinking, text, the end.</summary>
    private void Apply(AnswerRun run, AiAnswerEvent item)
    {
        var version = run.Version;
        switch (item)
        {
            case AiAnswerReasoningEvent reasoning when !string.IsNullOrEmpty(reasoning.TextDelta):
                version.Reasoning += reasoning.TextDelta;
                PushReasoning(reasoning.TextDelta);
                break;
            case AiAnswerSourcesEvent sources:
                AddSources(version, sources.Sources);
                break;
            case AiAnswerThoughtEvent thought:
                EndThinking(run, thought.Duration);
                break;
            case AiAnswerTextEvent text when !string.IsNullOrEmpty(text.MarkdownDelta):
                EndThinking(run, null);
                run.Raw.Append(text.MarkdownDelta);
                // The data keeps everything received; a stop trims it to what was shown.
                version.Markdown = run.Raw.ToString();
                if (ReferenceEquals(_shownVersion, version))
                {
                    _document?.Push(text.MarkdownDelta);
                }
                break;
            case AiAnswerDoneEvent done:
                run.Tokens = done.Tokens ?? run.Tokens;
                run.Duration = done.Duration ?? run.Duration;
                if (done.FollowUps is { } followUps)
                {
                    version.FollowUps.Clear();
                    foreach (var question in followUps)
                    {
                        if (!string.IsNullOrWhiteSpace(question))
                        {
                            version.FollowUps.Add(question);
                        }
                    }
                }
                break;
        }
    }

    /// <summary>Sources join the version in order; one without its own number takes the next (from 1).</summary>
    private static void AddSources(AiAnswerVersion version, IReadOnlyList<AiSource> sources)
    {
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }
            if (!source.IsSet(AiSource.IndexProperty) || source.Index <= 0)
            {
                source.Index = version.Sources.Count + 1;
            }
            version.Sources.Add(source);
        }
    }

    /// <summary>
    /// The model is done thinking: "Thought for Ns" with <paramref name="duration"/>, or the time since the question was
    /// asked; the status turns to Writing. A later explicit duration still replaces a measured one.
    /// </summary>
    private void EndThinking(AnswerRun run, TimeSpan? duration)
    {
        if (run.Thought is not null)
        {
            if (duration is { } exact && exact != run.Thought)
            {
                run.Thought = exact;
                RefreshRunParts(run);
            }
            return;
        }
        run.Thought = duration ?? Time.GetUtcNow() - run.Started;
        // The last reasoning word waits for what follows it: there is nothing more to come.
        _reasoningPacer?.Complete();
        _reasoningLoop?.Start();
        RefreshRunParts(run);
        UpdateStatusFromState();
        AnnounceStatus(Labels.AnnounceWriting);
    }

    private void RefreshRunParts(AnswerRun run)
    {
        if (ReferenceEquals(_shownVersion, run.Version))
        {
            UpdateSteps(run.Version, running: true);
        }
    }

    /// <summary>
    /// The responder sent everything. The version is ready once its text has been shown (the document raises
    /// <c>RevealCompleted</c>), at once when there is no text or nothing shows it.
    /// </summary>
    private void EndStream(AnswerRun run)
    {
        run.StreamEnded = true;
        if (run.Raw.Length == 0 || _document is null || !ReferenceEquals(_shownVersion, run.Version) || !_document.IsRevealing)
        {
            Finish(run);
            return;
        }
        _document.Complete();
    }

    private void OnRevealCompleted(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _document))
        {
            return;
        }
        e.Handled = true;
        if (_run is { StreamEnded: true } run)
        {
            Finish(run);
        }
    }

    /// <summary>The version is complete: what it cost, the footer and the related questions, "Answer ready".</summary>
    private void Finish(AnswerRun run)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        var version = run.Version;
        version.Markdown = run.Raw.ToString();
        run.Thought ??= Time.GetUtcNow() - run.Started;
        version.Usage = new AiAnswerUsage(run.Tokens, run.Thought, run.Duration ?? Time.GetUtcNow() - run.Started);
        version.Outcome = AiAnswerOutcome.Complete;
        EndRun(run);
        AnnounceStatus(Labels.AnnounceReady);
    }

    /// <summary>
    /// Stops a version: the stream is cancelled, the text keeps what was shown (the backlog is dropped), the reasoning what
    /// was revealed, and the version is <see cref="AiAnswerOutcome.Stopped"/>.
    /// </summary>
    private void StopRun(AnswerRun run, bool announce)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Cancel.Cancel();
        KeepShown(run);
        run.Version.Outcome = AiAnswerOutcome.Stopped;
        EndRun(run);
        if (announce)
        {
            AnnounceStatus(Labels.AnnounceStopped);
        }
    }

    /// <summary>The version failed: what was shown stays, the note says why and offers Try again.</summary>
    private void FailRun(AnswerRun run, string message)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Cancel.Cancel();
        KeepShown(run);
        run.Version.ErrorMessage = message;
        run.Version.Outcome = AiAnswerOutcome.Failed;
        EndRun(run);
        AnnounceStatus(Labels.AnnounceFailed(message));
    }

    /// <summary>A version that ends early keeps what was on screen: the released text and reasoning, the thinking time so far.</summary>
    private void KeepShown(AnswerRun run)
    {
        var version = run.Version;
        if (_document is not null && ReferenceEquals(_shownVersion, version))
        {
            _document.Stop();
            version.Markdown = _document.RevealedMarkdown;
        }
        else
        {
            version.Markdown = run.Raw.ToString();
        }
        version.Reasoning = _reasoningShown.ToString();
        run.Thought ??= Time.GetUtcNow() - run.Started;
        version.Usage = new AiAnswerUsage(run.Tokens, run.Thought, null);
    }

    private void EndRun(AnswerRun run)
    {
        if (ReferenceEquals(_run, run))
        {
            _run = null;
        }
        _reasoningLoop?.Stop();
        run.Cancel.Dispose();
        ShowVersionParts(CurrentVersion);
        run.Completion.TrySetResult();
    }

    #endregion

    #region Reasoning pace

    /// <summary>A new version's reasoning starts empty and is shown on the same clock as the answer.</summary>
    private void ResetReasoning()
    {
        _reasoningLoop?.Stop();
        _reasoningShown.Clear();
        _reasoningClock = TimeSpan.Zero;
        _reasoningPacer = CreatePacer();
    }

    private void PushReasoning(string delta)
    {
        _reasoningPacer ??= CreatePacer();
        _reasoningPacer.Push(delta);
        _reasoningLoop ??= new LiveLoop(this, StepReasoning)
        {
            // The reasoning keeps its cadence at every motion level and while the panel is closed or scrolled away.
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        _reasoningLoop.Start();
    }

    private bool StepReasoning(double dt)
    {
        if (_reasoningPacer is not { } pacer)
        {
            return false;
        }
        _reasoningClock += TimeSpan.FromSeconds(dt);
        if (pacer.Tick(_reasoningClock) is { } batch)
        {
            _reasoningShown.Append(batch);
            if (_run is { } run && ReferenceEquals(_shownVersion, run.Version) && _reasoning is not null)
            {
                _reasoning.Reasoning = _reasoningShown.ToString();
                UpdateSteps(run.Version, running: true);
            }
        }
        return pacer.PendingWords > 0 || (pacer.IsCompleted && !pacer.IsFinished);
    }

    /// <summary>Arc's clock: a fixed number of whole words every interval (the answer text uses the same in its document).</summary>
    private WordPacer CreatePacer()
    {
        var interval = RevealInterval < TimeSpan.FromMilliseconds(4) ? TimeSpan.FromMilliseconds(4) : RevealInterval;
        var minimum = interval - TimeSpan.FromMilliseconds(2);
        var words = Math.Max(1, WordsPerStep);
        return new WordPacer(new WordPacerOptions
        {
            MinInterval = minimum,
            WordsPerSecond = words / minimum.TotalSeconds,
            MaxWordsPerBatch = words
        });
    }

    #endregion

    #region Stop, regenerate, new thread

    /// <summary>
    /// Stops the answer being written (the stop button and Escape in the box do this): the text stops where it was, a note
    /// says it may be incomplete and offers Try again, and the version keeps its place in <see cref="Versions"/>.
    /// </summary>
    public void Stop()
    {
        if (_run is { } run)
        {
            StopRun(run, announce: true);
        }
    }

    /// <summary>
    /// Answers the question again as a new version: <see cref="AiAnswerMode.Retry"/>, <see cref="AiAnswerMode.Concise"/>,
    /// <see cref="AiAnswerMode.Detailed"/>, or <see cref="AiAnswerMode.SwitchModel"/> with <paramref name="model"/> (which
    /// also becomes <see cref="Model"/>). The responder gets the shown answer as the previous one. Needs a responder.
    /// </summary>
    /// <param name="mode">How to answer again.</param>
    /// <param name="model">The model for <see cref="AiAnswerMode.SwitchModel"/>.</param>
    public void Regenerate(AiAnswerMode mode, AiModel? model = null)
    {
        var question = Question;
        if (Responder is null || string.IsNullOrWhiteSpace(question) || (mode == AiAnswerMode.SwitchModel && model is null))
        {
            return;
        }
        var shown = CurrentVersion;
        if (_run is { } running)
        {
            StopRun(running, announce: false);
        }
        if (mode == AiAnswerMode.Answer)
        {
            mode = AiAnswerMode.Retry;
        }
        var next = mode == AiAnswerMode.SwitchModel ? model : shown?.Model ?? EffectiveModel;
        if (mode == AiAnswerMode.SwitchModel)
        {
            SetCurrentValue(ModelProperty, model);
        }
        _ = StartRun(question, mode, next, shown?.Markdown, PageChange.Regenerate);
    }

    /// <summary>
    /// Starts a new thread as the top bar's button does: the answer being written stops, the versions go, the empty page
    /// shows with its suggestions, and the box takes the focus.
    /// </summary>
    public void NewThread()
    {
        if (_run is { } run)
        {
            StopRun(run, announce: false);
        }
        _questionPending = false;
        _pendingAsked = false;
        _staticSuppressed = true;
        ClearVersions();
        SetQuestion(null);
        ShowPage(PageChange.Question);
        AnnounceStatus(Labels.EmptyTitle);
        _composer?.FocusInput(_keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    private static void Run(ICommand? command, object? parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    #endregion
}
