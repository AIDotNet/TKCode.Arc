using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class AiChat
{
    /// <summary>A reply the chat streams from <see cref="Responder"/>.</summary>
    private sealed class ReplyRun(AiChatConversation conversation, AiChatTurn turn, AiChatReply reply, DateTimeOffset started)
    {
        public AiChatConversation Conversation { get; } = conversation;
        public AiChatTurn Turn { get; } = turn;
        public AiChatReply Reply { get; } = reply;
        public CancellationTokenSource Cancel { get; } = new();
        public StringBuilder Raw { get; } = new();
        public DateTimeOffset Started { get; } = started;

        /// <summary>The responder sent everything; the text may still be revealing.</summary>
        public bool StreamEnded { get; set; }

        /// <summary>The reply has its final status.</summary>
        public bool Finished { get; set; }
    }

    private readonly Dictionary<AiChatReply, ReplyRun> _runs = new(ReferenceEqualityComparer.Instance);

    /// <summary>Whether a reply is being streamed by the chat (tests).</summary>
    internal bool IsStreaming(AiChatReply reply) => _runs.ContainsKey(reply);

    /// <summary>
    /// What a reply the chat streams has received so far and whether its stream ended, for a view that shows it (made or
    /// templated after the stream began). False for replies the chat does not stream.
    /// </summary>
    internal bool TryGetDriven(AiChatReply? reply, out string raw, out bool ended)
    {
        if (reply is not null && _runs.TryGetValue(reply, out var run))
        {
            raw = run.Raw.ToString();
            ended = run.StreamEnded;
            return true;
        }
        raw = string.Empty;
        ended = false;
        return false;
    }

    #region Sending

    /// <summary>
    /// Sends a message as if the person typed it: an empty chat first gets a conversation (<see cref="ConversationCreated"/>),
    /// the turn joins the end of the visible branch with its reply thinking, <see cref="MessageSent"/> is raised, the thread
    /// follows the newest message and, with a <see cref="Responder"/>, the reply streams. Ignored while a reply of the shown
    /// conversation runs, or when there is nothing to send.
    /// </summary>
    /// <param name="text">The message (the Markdown subset).</param>
    /// <param name="attachments">Files sent with it.</param>
    public void Send(string text, IReadOnlyList<AiAttachment>? attachments = null)
    {
        attachments ??= Array.Empty<AiAttachment>();
        if ((string.IsNullOrWhiteSpace(text) && attachments.Count == 0) || IsBusy)
        {
            return;
        }
        var conversation = CurrentConversation;
        var created = false;
        if (conversation is null)
        {
            conversation = new AiChatConversation { Id = Guid.NewGuid().ToString("N") };
            Conversations.Insert(0, conversation);
            _creating = true;
            try
            {
                SetCurrentValue(CurrentConversationProperty, conversation);
            }
            finally
            {
                _creating = false;
            }
            created = true;
        }
        var prompt = new AiChatUserMessage { Text = text ?? string.Empty };
        prompt.Attachments.AddRange(attachments);
        var turn = new AiChatTurn(prompt, NewReply());
        using (Batch())
        {
            AiChatBranch.Append(conversation, turn);
        }
        conversation.ModelId = SelectedModel?.Id;
        UpdateTitle();
        if (created)
        {
            RaiseEvent(new AiChatConversationEventArgs(ConversationCreatedEvent, this, conversation));
        }
        RaiseEvent(new AiChatMessageSentEventArgs(MessageSentEvent, this, conversation, turn, prompt));
        _scroller?.ScrollToLatest();
        StartReply(conversation, turn);
    }

    private AiChatReply NewReply() => new() { Status = AiChatReplyStatus.Thinking, ModelId = SelectedModel?.Id };

    #endregion

    #region Running a reply

    /// <summary>
    /// Answers the selected version of <paramref name="turn"/>: with a responder the reply streams (any other reply of the
    /// conversation stops first); without one it stays thinking for your code to fill.
    /// </summary>
    private void StartReply(AiChatConversation conversation, AiChatTurn turn)
    {
        if (turn.SelectedVersion is not { } version)
        {
            return;
        }
        var reply = version.Reply;
        if (Responder is not { } responder)
        {
            UpdateBusy();
            return;
        }
        // A new reply in the same conversation stops the one still running (Arc: retry and edit abort the stream).
        foreach (var other in _runs.Values.Where(r => ReferenceEquals(r.Conversation, conversation) && !ReferenceEquals(r.Reply, reply)).ToList())
        {
            StopRun(other, announce: false);
        }
        var branch = AiChatBranch.Visible(conversation);
        var index = branch.FindIndex(entry => ReferenceEquals(entry.Turn, turn));
        var messages = index >= 0
            ? AiChatBranch.Request(branch, index)
            : new List<AiChatRequestMessage> { new(AiChatRole.User, version.Prompt.Text, version.Prompt.Attachments.ToArray()) };
        var request = new AiChatRequest(conversation, SelectedModel, messages);
        reply.Status = AiChatReplyStatus.Thinking;
        var run = new ReplyRun(conversation, turn, reply, Time.GetUtcNow());
        _runs[reply] = run;
        FindReplyView(run)?.BeginDriven(string.Empty);
        UpdateBusy();
        Announce(Labels.Replying(FindModel(reply.ModelId)?.Name ?? SelectedModel?.Name ?? Labels.Chat));
        _ = RunAsync(run, responder, request);
    }

    /// <summary>Reads the responder's events on the UI thread and writes them into the reply.</summary>
    private async Task RunAsync(ReplyRun run, IAiChatResponder responder, AiChatRequest request)
    {
        var token = run.Cancel.Token;
        try
        {
            await foreach (var item in responder.RespondAsync(request, token).WithCancellation(token))
            {
                if (run.Finished)
                {
                    return;
                }
                if (item is AiChatErrorEvent error)
                {
                    FailRun(run, error.Message);
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

    /// <summary>One event of the stream: reasoning, the end of thinking, a tool, sources, text.</summary>
    private void Apply(ReplyRun run, AiChatStreamEvent item)
    {
        var reply = run.Reply;
        switch (item)
        {
            case AiChatReasoningDelta delta:
                reply.Reasoning = (reply.Reasoning ?? string.Empty) + delta.Text;
                break;
            case AiChatReasoningEnd end:
                if (end.Duration is { } duration)
                {
                    reply.ThoughtFor = duration;
                }
                else
                {
                    EndThinking(run);
                }
                break;
            case AiChatToolUpdate update:
                EndThinking(run);
                Upsert(reply, update);
                break;
            case AiChatSourcesEvent sources:
                AddSources(reply, sources.Sources);
                break;
            case AiChatTextDelta text:
                EndThinking(run);
                if (string.IsNullOrEmpty(text.Text))
                {
                    break;
                }
                if (reply.Status == AiChatReplyStatus.Thinking)
                {
                    reply.Status = AiChatReplyStatus.Streaming;
                }
                run.Raw.Append(text.Text);
                // The data keeps the whole text received (the view, driven, ignores this); a stop trims it to what was shown.
                reply.Text = run.Raw.ToString();
                FindReplyView(run)?.PushText(text.Text);
                break;
        }
    }

    /// <summary>The model stopped thinking: "Thought for Ns" with the time since the reply started, unless the stream said.</summary>
    private void EndThinking(ReplyRun run)
    {
        if (run.Reply.ThoughtFor is null)
        {
            run.Reply.ThoughtFor = Time.GetUtcNow() - run.Started;
        }
    }

    /// <summary>A tool update changes the tool with the same id, or adds one (in the order tools started).</summary>
    private static void Upsert(AiChatReply reply, AiChatToolUpdate update)
    {
        AiChatTool? tool = null;
        foreach (var existing in reply.Tools)
        {
            if (existing.Id == update.Id)
            {
                tool = existing;
                break;
            }
        }
        var added = tool is null;
        tool ??= new AiChatTool { Id = update.Id };
        tool.Kind = update.Kind;
        if (update.Status == AiChatToolStatus.Done)
        {
            tool.DoneLabel = update.Label;
        }
        else
        {
            // Running, and an error (the chip says what it was doing when it failed).
            tool.RunningLabel = update.Label;
        }
        if (update.Summary is not null)
        {
            tool.Summary = update.Summary;
        }
        if (update.Queries is { } queries)
        {
            tool.Queries.Clear();
            tool.Queries.AddRange(queries);
        }
        if (update.Sources is { } sources)
        {
            tool.Sources.Clear();
            tool.Sources.AddRange(sources);
        }
        if (update.Input is not null)
        {
            tool.Input = update.Input;
        }
        if (update.Output is not null)
        {
            tool.Output = update.Output;
        }
        // The status last: a chip that turns done shows the summary and sources already in place.
        tool.Status = update.Status;
        if (added)
        {
            reply.Tools.Add(tool);
        }
    }

    /// <summary>Sources join the reply in order; a source without its own number takes the next one (from 1).</summary>
    private static void AddSources(AiChatReply reply, IReadOnlyList<AiSource> sources)
    {
        foreach (var source in sources)
        {
            if (!source.IsSet(AiSource.IndexProperty) || source.Index <= 0)
            {
                source.Index = reply.Sources.Count + 1;
            }
            reply.Sources.Add(source);
        }
    }

    /// <summary>
    /// The responder sent everything. The reply is done once its text has been shown (the text view raises
    /// <c>RevealCompleted</c>), at once when there is no text or no view to show it.
    /// </summary>
    private void EndStream(ReplyRun run)
    {
        run.StreamEnded = true;
        run.Reply.Text = run.Raw.ToString();
        EndThinking(run);
        var view = FindReplyView(run);
        if (run.Raw.Length == 0 || view is null || !view.IsDriven || view.Text is null)
        {
            FinishRun(run);
            return;
        }
        view.CompleteText();
    }

    /// <summary>The text of a streamed reply has been shown: it is done.</summary>
    private void OnRevealCompleted(object? sender, RoutedEventArgs e)
    {
        if ((e.Source as AiReplyText)?.TemplatedParent is AiChatReplyView { Reply: { } reply } &&
            _runs.TryGetValue(reply, out var run) && run.StreamEnded)
        {
            FinishRun(run);
        }
    }

    /// <summary>A view of a streamed reply left the thread (another conversation or version): an ended stream is done at once.</summary>
    private void OnReplyViewGone(AiChatReply reply)
    {
        if (_runs.TryGetValue(reply, out var run) && run.StreamEnded)
        {
            FinishRun(run);
        }
    }

    private void FinishRun(ReplyRun run)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Reply.Text = run.Raw.ToString();
        run.Reply.Status = AiChatReplyStatus.Done;
        EndRun(run);
        RaiseEvent(new AiChatReplyEventArgs(ReplyCompletedEvent, this, run.Reply));
        if (ReferenceEquals(run.Conversation, CurrentConversation))
        {
            Announce(Labels.ReplyFinished);
        }
    }

    /// <summary>
    /// Stops a reply: the stream is cancelled, the text keeps what was shown (the backlog is dropped; without a view, what
    /// arrived), tools still running are marked failed, and the reply is <see cref="AiChatReplyStatus.Stopped"/>.
    /// </summary>
    private void StopRun(ReplyRun run, bool announce)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Cancel.Cancel();
        var view = FindReplyView(run);
        run.Reply.Text = view is { IsDriven: true, Text: not null } ? view.StopText() : run.Raw.ToString();
        EndThinking(run);
        FailRunningTools(run.Reply);
        run.Reply.Status = AiChatReplyStatus.Stopped;
        EndRun(run);
        RaiseEvent(new AiChatReplyEventArgs(ReplyCompletedEvent, this, run.Reply));
        if (announce)
        {
            Announce(Labels.ReplyStopped);
        }
    }

    /// <summary>The reply failed: what was shown stays, the error box says why and offers Retry.</summary>
    private void FailRun(ReplyRun run, string message)
    {
        if (run.Finished)
        {
            return;
        }
        run.Finished = true;
        run.Cancel.Cancel();
        var view = FindReplyView(run);
        run.Reply.Text = view is { IsDriven: true, Text: not null } ? view.StopText() : run.Raw.ToString();
        EndThinking(run);
        FailRunningTools(run.Reply);
        run.Reply.ErrorMessage = message;
        run.Reply.Status = AiChatReplyStatus.Failed;
        EndRun(run);
        RaiseEvent(new AiChatReplyEventArgs(ReplyCompletedEvent, this, run.Reply));
        Announce(Labels.ReplyFailedAnnounce(message));
    }

    private void EndRun(ReplyRun run)
    {
        _runs.Remove(run.Reply);
        FindReplyView(run)?.EndDriven();
        run.Cancel.Dispose();
        UpdateBusy();
    }

    /// <summary>A tool still spinning when its reply ends did not finish: it shows as failed.</summary>
    private static void FailRunningTools(AiChatReply reply)
    {
        foreach (var tool in reply.Tools)
        {
            if (tool.Status == AiChatToolStatus.Running)
            {
                tool.Status = AiChatToolStatus.Error;
            }
        }
    }

    /// <summary>The reply view showing the run's reply, when it is on screen.</summary>
    private AiChatReplyView? FindReplyView(ReplyRun run) =>
        _views.TryGetValue(run.Turn, out var view) && view.ReplyView is { } replyView && ReferenceEquals(replyView.Reply, run.Reply)
            ? replyView
            : null;

    private void CancelAllRuns()
    {
        foreach (var run in _runs.Values.ToList())
        {
            StopRun(run, announce: false);
        }
    }

    #endregion

    #region Stop, busy

    /// <summary>
    /// Stops the reply of the shown conversation that is thinking or streaming (the stop button and Escape in the message
    /// box do this). A reply your code drives is marked stopped and <see cref="ReplyCompleted"/> tells you.
    /// </summary>
    public void StopReply()
    {
        var conversation = CurrentConversation;
        var stopped = false;
        foreach (var run in _runs.Values.Where(r => ReferenceEquals(r.Conversation, conversation)).ToList())
        {
            StopRun(run, announce: false);
            stopped = true;
        }
        foreach (var entry in _branch)
        {
            var reply = entry.Version.Reply;
            if (reply.Status is AiChatReplyStatus.Thinking or AiChatReplyStatus.Streaming && !_runs.ContainsKey(reply))
            {
                reply.Status = AiChatReplyStatus.Stopped;
                RaiseEvent(new AiChatReplyEventArgs(ReplyCompletedEvent, this, reply));
                stopped = true;
            }
        }
        if (stopped)
        {
            Announce(Labels.ReplyStopped);
        }
    }

    /// <summary><see cref="IsBusy"/>: a reply of the shown conversation thinks or streams; the composer shows the stop button.</summary>
    private void UpdateBusy()
    {
        var busy = false;
        foreach (var run in _runs.Values)
        {
            if (ReferenceEquals(run.Conversation, CurrentConversation))
            {
                busy = true;
                break;
            }
        }
        if (!busy)
        {
            foreach (var entry in _branch)
            {
                if (entry.Version.Reply.Status is AiChatReplyStatus.Thinking or AiChatReplyStatus.Streaming)
                {
                    busy = true;
                    break;
                }
            }
        }
        IsBusy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_composer is not null)
        {
            _composer.IsGenerating = busy;
        }
    }

    #endregion

    #region Retry and edit

    /// <summary>
    /// Runs the reply of <paramref name="turn"/> again: a failed reply in place (the reply is replaced, no version); a
    /// finished or stopped one as a new version of the turn (the same prompt, a new reply, nothing after it).
    /// </summary>
    internal void Retry(AiChatTurn turn)
    {
        var conversation = CurrentConversation;
        if (conversation is null || turn.SelectedVersion is not { } version)
        {
            return;
        }
        if (_runs.TryGetValue(version.Reply, out var running))
        {
            StopRun(running, announce: false);
        }
        using (Batch())
        {
            if (version.Reply.Status == AiChatReplyStatus.Failed)
            {
                version.Reply = NewReply();
            }
            else
            {
                AiChatBranch.AddVersion(conversation, turn, AiChatBranch.CopyPrompt(version.Prompt), NewReply());
            }
        }
        var current = turn.SelectedVersion!;
        RaiseEvent(new AiChatMessageSentEventArgs(MessageSentEvent, this, conversation, turn, current.Prompt));
        StartReply(conversation, turn);
    }

    /// <summary>The person sent an edited message: a new version of the turn with the new prompt, answered again.</summary>
    internal void Edit(AiChatTurn turn, string text)
    {
        var conversation = CurrentConversation;
        if (conversation is null || turn.SelectedVersion is not { } version)
        {
            return;
        }
        if (_runs.TryGetValue(version.Reply, out var running))
        {
            StopRun(running, announce: false);
        }
        AiChatTurnVersion added;
        using (Batch())
        {
            added = AiChatBranch.AddVersion(conversation, turn, AiChatBranch.CopyPrompt(version.Prompt, text), NewReply());
        }
        UpdateTitle();
        RaiseEvent(new AiChatMessageSentEventArgs(MessageSentEvent, this, conversation, turn, added.Prompt));
        StartReply(conversation, turn);
    }

    #endregion
}
