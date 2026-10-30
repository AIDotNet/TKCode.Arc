using System.Text;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class ReplyDraft
{
    private readonly StringBuilder _buffer = new();
    private CancellationTokenSource? _run;
    private int _runId;
    private bool _started;
    private ReplyDraftStreamText? _streamView;
    private ReplyDraftEditor? _editor;
    private ReplyDraftStatusView? _shownStatus;
    private ReplyDraftMode _lastMode;
    private string? _failure;
    private string? _readyFailure;

    /// <summary>True while a draft or rewrite request runs.</summary>
    internal bool IsRunning => Status is ReplyDraftStatus.Drafting or ReplyDraftStatus.Rewriting;

    /// <summary>How many requests the card has made.</summary>
    internal int RequestCount { get; private set; }

    /// <summary>The last request made, for tests.</summary>
    internal ReplyDraftRequest? LastRequest { get; private set; }

    /// <summary>The streaming view, while a fresh draft streams with full motion.</summary>
    internal ReplyDraftStreamText? StreamView => _streamView;

    /// <summary>The editable draft, once one has finished.</summary>
    internal ReplyDraftEditor? Editor => _editor;

    /// <summary>The status line on screen.</summary>
    internal ReplyDraftStatusView? StatusView => _shownStatus;

    /// <summary>The last announcement.</summary>
    internal string? LastAnnouncement { get; private set; }

    /// <summary>
    /// Writes the first draft now (what Draft reply and auto start do). Does nothing once a draft exists; use
    /// <see cref="Regenerate"/> for a new one.
    /// </summary>
    public void StartDraft()
    {
        if (_started)
        {
            return;
        }
        Begin(ReplyDraftMode.Draft, hold: false);
    }

    /// <summary>Writes a fresh draft for the current quick reply, tone and length; the box keeps its height until it is done.</summary>
    public void Regenerate()
    {
        if (Status == ReplyDraftStatus.Inserting)
        {
            return;
        }
        Begin(ReplyDraftMode.Draft, hold: true);
    }

    /// <summary>
    /// Inserts the finished draft, with the reader's edits: raises <see cref="InsertRequested"/>, then awaits
    /// <see cref="InsertHandler"/> while Insert shows a spinner. Without either it is simulated and says so. Does nothing
    /// unless a draft is ready.
    /// </summary>
    public async Task Insert()
    {
        if (Status != ReplyDraftStatus.Ready || _editor is null || string.IsNullOrWhiteSpace(_editor.Text))
        {
            return;
        }
        var text = _editor.Text ?? string.Empty;
        var args = new ReplyDraftEventArgs(InsertRequestedEvent, text, Tone, Length, EffectiveSuggestion, _lastMode);
        RaiseEvent(args);
        _readyFailure = null;
        if (InsertHandler is { } handler)
        {
            SetStatus(ReplyDraftStatus.Inserting);
            try
            {
                await handler(args);
            }
            catch (Exception exception)
            {
                _readyFailure = Labels.InsertFailed;
                SetStatus(ReplyDraftStatus.Ready);
                Announce(string.IsNullOrWhiteSpace(exception.Message) ? Labels.InsertFailed : exception.Message.Trim(), simulated: false);
                return;
            }
            if (Status == ReplyDraftStatus.Inserting)
            {
                SetStatus(ReplyDraftStatus.Inserted);
                Announce(Labels.InsertedAnnouncement, simulated: false);
            }
            return;
        }
        SetStatus(ReplyDraftStatus.Inserted);
        Announce(Labels.InsertedAnnouncement, simulated: !args.Handled);
    }

    /// <summary>Restarts what a tone or length change asks for: a rewrite of a finished draft, or a restart of a running one.</summary>
    private void OnStyleChanged()
    {
        switch (Status)
        {
            case ReplyDraftStatus.Drafting:
            case ReplyDraftStatus.Failed:
                Begin(ReplyDraftMode.Draft, hold: true);
                break;
            case ReplyDraftStatus.Rewriting:
            case ReplyDraftStatus.Ready:
            case ReplyDraftStatus.Inserted:
                Begin(ReplyDraftMode.Rewrite, hold: false);
                break;
        }
    }

    /// <summary>Drops any draft and request and goes back to the start (a new message or generator, or the card leaving).</summary>
    private void Reset()
    {
        CancelRun();
        StopTourTimer();
        _started = false;
        _streamView = null;
        _editor = null;
        _failure = null;
        _readyFailure = null;
        Draft = string.Empty;
        if (_heightBox is not null)
        {
            _heightBox.IsHeld = false;
        }
        SetStatus(ReplyDraftStatus.Idle);
        RestoreDraftView();
        if (this.IsAttachedToVisualTree())
        {
            WatchViewport();
        }
    }

    private void CancelRun()
    {
        _runId++;
        _run?.Cancel();
        _run = null;
    }

    /// <summary>Starts a request. A rewrite needs a finished draft; otherwise it becomes a fresh draft.</summary>
    internal void Begin(ReplyDraftMode mode, bool hold)
    {
        if (_draftSwap is null || !CanDraft)
        {
            return;
        }
        var previous = _editor?.Text;
        if (mode == ReplyDraftMode.Rewrite && (_editor is null || string.IsNullOrWhiteSpace(previous)))
        {
            mode = ReplyDraftMode.Draft;
        }
        CancelRun();
        StopTourTimer();
        _started = true;
        _failure = null;
        _readyFailure = null;
        _lastMode = mode;
        _buffer.Clear();
        var run = new CancellationTokenSource();
        _run = run;
        var id = _runId;
        var labels = Labels;
        if (mode == ReplyDraftMode.Draft)
        {
            if (_heightBox is not null)
            {
                _heightBox.IsHeld = hold && _draftSwap.Content is ReplyDraftEditor or ReplyDraftStreamText or ReplyDraftAlert;
            }
            _streamView = null;
            if (_editor is not null)
            {
                _editor.TextChanged -= OnEditorTextChanged;
                _editor = null;
            }
            Draft = string.Empty;
            ShowDraftView(new ReplyDraftPending { Text = labels.Reading(ReplyDraftStreams.FirstName(DisplayedMessage.FromName)) },
                ContentSwapMode.Crossfade);
            SetStatus(ReplyDraftStatus.Drafting);
            Announce(labels.DraftingAnnouncement(DisplayedModel.Name), EffectiveSimulated);
        }
        else
        {
            _editor!.IsDimmed = true;
            _editor.IsReadOnly = true;
            SetStatus(ReplyDraftStatus.Rewriting);
            Announce(labels.RewritingAnnouncement(labels.ToneName(Tone), labels.LengthName(Length)), EffectiveSimulated);
        }
        RequestCount++;
        var request = new ReplyDraftRequest(DisplayedMessage, Tone, Length, EffectiveSuggestion, mode, previous, run.Token);
        LastRequest = request;
        _ = RunAsync(id, run, request);
    }

    private async Task RunAsync(int id, CancellationTokenSource run, ReplyDraftRequest request)
    {
        try
        {
            var stream = PlayingSimulation is { } simulation ? simulation.Generate(request) : Generator!(request);
            await foreach (var chunk in stream.WithCancellation(run.Token).ConfigureAwait(true))
            {
                if (id != _runId)
                {
                    return;
                }
                Receive(request, chunk);
            }
            if (id == _runId)
            {
                Finish(request);
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested || id != _runId)
        {
            // Replaced or detached: whoever cancelled has already updated the card.
        }
        catch (Exception exception)
        {
            if (id == _runId)
            {
                Fail(request, exception.Message);
            }
        }
        finally
        {
            run.Dispose();
        }
    }

    /// <summary>
    /// One delta: a fresh draft with full motion shows it at once as a fading chunk; reduced motion, Off and rewrites
    /// collect the text and show it whole when it is done.
    /// </summary>
    private void Receive(ReplyDraftRequest request, string? chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }
        _buffer.Append(chunk);
        if (request.Mode != ReplyDraftMode.Draft || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_streamView is null)
        {
            _streamView = new ReplyDraftStreamText();
            ShowDraftView(_streamView, ContentSwapMode.None);
        }
        _streamView.Append(chunk);
    }

    private void Finish(ReplyDraftRequest request)
    {
        _run = null;
        var text = _buffer.ToString().Trim();
        var full = ArcMotion.IsFull(this);
        var oldEditor = _editor;
        var editor = CreateEditor(text);
        ContentSwapMode swap;
        if (request.Mode == ReplyDraftMode.Rewrite)
        {
            // One crossfade: the dimmed draft fades out in 100ms, the new one fades in over 160ms rising 2px.
            swap = full ? ContentSwapMode.Label : ContentSwapMode.None;
        }
        else
        {
            // The streamed text becomes the editable text in place; whole drafts (reduced motion) appear at once.
            swap = ContentSwapMode.None;
        }
        _streamView = null;
        _editor = editor;
        ShowDraftView(editor, swap);
        if (oldEditor is not null)
        {
            oldEditor.TextChanged -= OnEditorTextChanged;
        }
        if (_heightBox is not null)
        {
            _heightBox.IsHeld = false;
        }
        Draft = text;
        SetStatus(ReplyDraftStatus.Ready);
        Announce(Labels.ReadyAnnouncement, EffectiveSimulated);
        RaiseEvent(new ReplyDraftEventArgs(DraftCompletedEvent, text, request.Tone, request.Length, request.Suggestion, request.Mode));
        ScheduleTour();
    }

    private void Fail(ReplyDraftRequest request, string? message)
    {
        _run = null;
        var text = string.IsNullOrWhiteSpace(message) ? Labels.FailedMessage : message.Trim();
        if (EffectiveSimulated)
        {
            text = text.TrimEnd('.') + ". " + Labels.Simulated + ".";
        }
        _failure = text;
        if (_heightBox is not null)
        {
            _heightBox.IsHeld = false;
        }
        if (request.Mode == ReplyDraftMode.Rewrite && _editor is not null)
        {
            // A failed rewrite keeps the draft as it was.
            _editor.IsDimmed = false;
            _editor.IsReadOnly = false;
            _readyFailure = Labels.Failed;
            SetStatus(ReplyDraftStatus.Ready);
        }
        else
        {
            _streamView = null;
            _editor = null;
            Draft = string.Empty;
            ShowDraftView(CreateAlert(text), ContentSwapMode.Crossfade);
            SetStatus(ReplyDraftStatus.Failed);
        }
        Announce(text, simulated: false);
    }

    private ReplyDraftEditor CreateEditor(string text)
    {
        var editor = new ReplyDraftEditor { Text = text, Label = Labels.DraftText };
        editor.TextChanged += OnEditorTextChanged;
        return editor;
    }

    private ReplyDraftAlert CreateAlert(string message)
    {
        var alert = new ReplyDraftAlert { Message = message, RetryText = Labels.TryAgain };
        alert.RetryRequested += (_, _) => Begin(ReplyDraftMode.Draft, hold: true);
        return alert;
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _editor) || _editor is null)
        {
            return;
        }
        Draft = _editor.Text ?? string.Empty;
        if (Status == ReplyDraftStatus.Inserted)
        {
            SetStatus(ReplyDraftStatus.Ready);
        }
        else
        {
            UpdateButtons();
        }
    }

    /// <summary>Puts the view that matches the state back in the draft box (a new template or new words).</summary>
    private void RestoreDraftView()
    {
        if (_draftSwap is null)
        {
            return;
        }
        Control view;
        if (_editor is not null)
        {
            view = _editor;
        }
        else if (_streamView is not null)
        {
            view = _streamView;
        }
        else if (_failure is not null && Status == ReplyDraftStatus.Failed)
        {
            view = CreateAlert(_failure);
        }
        else if (Status == ReplyDraftStatus.Drafting)
        {
            view = new ReplyDraftPending { Text = Labels.Reading(ReplyDraftStreams.FirstName(DisplayedMessage.FromName)) };
        }
        else if (!AutoStart || !CanDraft)
        {
            // With nothing to draft from (no email, or your email without a generator) Draft reply stays off.
            var empty = new ReplyDraftEmptyView { Text = Labels.EmptyPrompt(DisplayedModel.Name), ButtonText = Labels.DraftReply, CanDraft = CanDraft };
            empty.DraftRequested += (_, _) => StartDraft();
            view = empty;
        }
        else
        {
            view = new ReplyDraftPending { Text = Labels.Reading(ReplyDraftStreams.FirstName(DisplayedMessage.FromName)) };
        }
        ShowDraftView(view, ContentSwapMode.None);
    }

    private void ShowDraftView(Control view, ContentSwapMode mode)
    {
        if (_draftSwap is null || ReferenceEquals(_draftSwap.Content, view))
        {
            return;
        }
        _draftSwap.Mode = mode;
        _draftSwap.Content = view;
    }

    private void SetStatus(ReplyDraftStatus status)
    {
        Status = status;
        PseudoClasses.Set(BusyPseudoClass, IsRunning);
        UpdateStatusLine();
        UpdateButtons();
    }

    /// <summary>The status line: an icon and a word; a change crossfades, working words shimmer.</summary>
    private void UpdateStatusLine()
    {
        var labels = Labels;
        var (kind, text) = Status switch
        {
            ReplyDraftStatus.Drafting => (ReplyDraftStatusKind.Working, labels.Drafting),
            ReplyDraftStatus.Rewriting => (ReplyDraftStatusKind.Working, labels.Rewriting),
            ReplyDraftStatus.Inserting => (ReplyDraftStatusKind.Working, labels.Inserting),
            ReplyDraftStatus.Inserted => (ReplyDraftStatusKind.Done, labels.Inserted),
            ReplyDraftStatus.Failed => (ReplyDraftStatusKind.Failed, labels.Failed),
            ReplyDraftStatus.Ready when _readyFailure is { } failure => (ReplyDraftStatusKind.Failed, failure),
            ReplyDraftStatus.Ready => (ReplyDraftStatusKind.Ready, labels.Ready),
            _ => (ReplyDraftStatusKind.Idle, labels.NoDraft),
        };
        if (_status is null || (_shownStatus is not null && _shownStatus.Kind == kind && _shownStatus.Text == text))
        {
            return;
        }
        _shownStatus = new ReplyDraftStatusView(kind, text);
        _status.Content = _shownStatus;
    }

    /// <summary>Regenerate works once something was asked for; Insert only on a finished, non-empty draft.</summary>
    private void UpdateButtons()
    {
        if (_regenerate is not null)
        {
            _regenerate.IsEnabled = _started && Status != ReplyDraftStatus.Inserting;
        }
        if (_insert is not null)
        {
            _insert.IsEnabled = Status is ReplyDraftStatus.Ready or ReplyDraftStatus.Inserting
                && !string.IsNullOrWhiteSpace(_editor?.Text);
            _insert.IsLoading = Status == ReplyDraftStatus.Inserting;
        }
        foreach (var chip in _chips)
        {
            chip.IsEnabled = Status != ReplyDraftStatus.Inserting;
        }
    }

    /// <summary>
    /// Says <paramref name="text"/> in the live region, except while the tour plays on its own: the reader is not driving
    /// it, so it stays quiet until they press or type inside the card.
    /// </summary>
    private void Announce(string text, bool simulated)
    {
        var said = simulated ? text + ". " + Labels.Simulated : text;
        LastAnnouncement = said;
        if (!EffectiveAutoPlay || _touched)
        {
            _announcer?.Announce(said);
        }
    }
}
