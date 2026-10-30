using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class AgentHandoff
{
    /// <summary>The keys of the rows that are not messages.</summary>
    internal static class RowKeys
    {
        public static readonly object AssistantStep = new();
        public static readonly object AssistantTyping = new();
        public static readonly object Suggestion = new();
        public static readonly object Card = new();
        public static readonly object JoinStep = new();
        public static readonly object AgentTyping = new();
        public static readonly object LateAssistantTyping = new();
        public static readonly object ResolvedStep = new();
    }

    private readonly Dictionary<object, AgentHandoffRow> _rowsByKey = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _plan = [];
    private AgentHandoffCard? _card;
    private bool _rowsBuilt;

    /// <summary>The rows on screen, top to bottom (tests).</summary>
    internal IReadOnlyList<AgentHandoffRow> Rows =>
        _rows?.Children.OfType<AgentHandoffRow>().ToArray() ?? [];

    internal AgentHandoffCard? Card => _card;

    /// <summary>
    /// Plans the thread top to bottom: the assistant step, its conversation (with the Thinking line and Talk to a person
    /// under the last answer), then from transferring on the handoff card, the notes and the conversation with the person
    /// (the join step before the person's first message), the typing dots and, when resolved, the last step.
    /// </summary>
    internal static void Plan(
        List<object> plan,
        AgentHandoffStage stage,
        IReadOnlyList<AgentHandoffMessage> messages,
        IReadOnlyList<AgentHandoffMessage> agentMessages,
        AgentHandoffTyping typing,
        bool offersHuman)
    {
        plan.Clear();
        plan.Add(RowKeys.AssistantStep);
        foreach (var message in messages)
        {
            plan.Add(message);
        }
        if (stage == AgentHandoffStage.Assistant)
        {
            if (typing == AgentHandoffTyping.Assistant)
            {
                plan.Add(RowKeys.AssistantTyping);
            }
            else if (offersHuman && messages.Count > 0 && messages[^1] is { From: AgentHandoffSender.Assistant, IsStreaming: false })
            {
                plan.Add(RowKeys.Suggestion);
            }
            return;
        }
        plan.Add(RowKeys.Card);
        var personPresent = stage is AgentHandoffStage.Connected or AgentHandoffStage.Resolved;
        var joined = false;
        foreach (var message in agentMessages)
        {
            if (!joined && personPresent && message.From == AgentHandoffSender.Agent)
            {
                plan.Add(RowKeys.JoinStep);
                joined = true;
            }
            plan.Add(message);
        }
        if (personPresent && !joined)
        {
            plan.Add(RowKeys.JoinStep);
        }
        if (typing == AgentHandoffTyping.Agent)
        {
            plan.Add(RowKeys.AgentTyping);
        }
        else if (typing == AgentHandoffTyping.Assistant)
        {
            plan.Add(RowKeys.LateAssistantTyping);
        }
        if (stage == AgentHandoffStage.Resolved)
        {
            plan.Add(RowKeys.ResolvedStep);
        }
    }

    private static bool IsMarkerKey(object key) =>
        ReferenceEquals(key, RowKeys.AssistantStep) || ReferenceEquals(key, RowKeys.Card) ||
        ReferenceEquals(key, RowKeys.JoinStep) || ReferenceEquals(key, RowKeys.ResolvedStep);

    private void ResetRows()
    {
        _rowsByKey.Clear();
        _rows?.Children.Clear();
        if (_card is not null)
        {
            _card.ActionInvoked -= OnCardAction;
            _card.ToggleInvoked -= OnSummaryToggle;
            _card = null;
        }
        _rowsBuilt = false;
        _pinned = true;
    }

    /// <summary>Drops the rows whose marker is built from a value that changed; they are rebuilt on the next sync.</summary>
    private void ResetMarkers()
    {
        foreach (var key in new[] { RowKeys.AssistantStep, RowKeys.Card, RowKeys.JoinStep })
        {
            if (_rowsByKey.TryGetValue(key, out var row))
            {
                row.Marker = null;
            }
        }
    }

    private void SyncRows(AgentHandoffStage stage)
    {
        if (_rows is null)
        {
            return;
        }
        var typing = DisplayedTyping;
        Plan(_plan, stage, DisplayedMessages, DisplayedAgentMessages, typing, DisplayedRequestHuman);

        var fades = _rowsBuilt && ArcMotion.IsFull(this);
        var rows = new List<AgentHandoffRow>(_plan.Count);
        var created = new List<AgentHandoffRow>();
        foreach (var key in _plan)
        {
            if (!_rowsByKey.TryGetValue(key, out var row))
            {
                row = CreateRow(key);
                _rowsByKey[key] = row;
                created.Add(row);
            }
            UpdateRow(row, stage);
            rows.Add(row);
        }
        if (_rowsByKey.Count != rows.Count)
        {
            var keep = new HashSet<object>(_plan, ReferenceEqualityComparer.Instance);
            foreach (var key in _rowsByKey.Keys.Where(k => !keep.Contains(k)).ToArray())
            {
                if (ReferenceEquals(key, RowKeys.Card) && _card is not null)
                {
                    _card.ActionInvoked -= OnCardAction;
                    _card.ToggleInvoked -= OnSummaryToggle;
                    _card = null;
                }
                _rowsByKey.Remove(key);
            }
        }

        // Lines: markers are joined by the timeline; a row between two markers has the line running through it.
        var first = _plan.FindIndex(IsMarkerKey);
        var last = _plan.FindLastIndex(IsMarkerKey);
        for (var i = 0; i < rows.Count; i++)
        {
            var key = _plan[i];
            var pending = ReferenceEquals(key, RowKeys.Card) && i == last && stage == AgentHandoffStage.Transferring;
            if (IsMarkerKey(key))
            {
                rows[i].SetLines(i > first, i < last, pending, pending);
            }
            else
            {
                var through = i > first && i < last;
                rows[i].SetLines(through, through, false, false);
            }
        }

        var same = _rows.Children.Count == rows.Count;
        for (var i = 0; same && i < rows.Count; i++)
        {
            same = ReferenceEquals(_rows.Children[i], rows[i]);
        }
        if (!same)
        {
            _rows.Children.Clear();
            _rows.Children.AddRange(rows);
        }
        if (fades)
        {
            foreach (var row in created)
            {
                // New rows fade in while the thread's height spring makes room for them.
                row.Opacity = 0;
                var fade = row;
                Dispatcher.UIThread.Post(() => fade.Opacity = 1, DispatcherPriority.Loaded);
            }
        }
        _rowsBuilt = true;
    }

    private AgentHandoffRow CreateRow(object key)
    {
        var row = new AgentHandoffRow(key);
        if (key is AgentHandoffMessage message)
        {
            row.Content = new AgentHandoffBubble(message);
        }
        else if (ReferenceEquals(key, RowKeys.AssistantStep) || ReferenceEquals(key, RowKeys.JoinStep) || ReferenceEquals(key, RowKeys.ResolvedStep))
        {
            row.Content = new AgentHandoffStep();
        }
        else if (ReferenceEquals(key, RowKeys.AssistantTyping) || ReferenceEquals(key, RowKeys.LateAssistantTyping))
        {
            row.Content = new AgentHandoffTypingView();
        }
        else if (ReferenceEquals(key, RowKeys.AgentTyping))
        {
            row.Content = new AgentHandoffTypingView { IsAgent = true };
        }
        else if (ReferenceEquals(key, RowKeys.Suggestion))
        {
            var suggestion = new AgentHandoffSuggestion();
            suggestion.Click += OnRequestHuman;
            row.Content = suggestion;
        }
        else if (ReferenceEquals(key, RowKeys.Card))
        {
            _card = new AgentHandoffCard();
            _card.ActionInvoked += OnCardAction;
            _card.ToggleInvoked += OnSummaryToggle;
            row.Content = _card;
            row.SetCard(true);
        }
        return row;
    }

    /// <summary>Brings one row in line with the state; a value that did not change is left alone.</summary>
    private void UpdateRow(AgentHandoffRow row, AgentHandoffStage stage)
    {
        var labels = Labels;
        var key = row.Key;
        switch (row.Content)
        {
            case AgentHandoffBubble bubble:
                bubble.Fraction = BubbleFraction;
                bubble.Update(fade: bubble.Message.IsStreaming);
                break;
            case AgentHandoffStep step when ReferenceEquals(key, RowKeys.AssistantStep):
                step.Title = AssistantName;
                step.Meta = AssistantModel;
                row.Marker ??= AgentHandoffMarker.ForGlyph(AssistantMark ?? ArcIcons.Sparkles, AssistantMarkBrush, accent: true);
                break;
            case AgentHandoffStep step when ReferenceEquals(key, RowKeys.JoinStep):
                var agent = DisplayedAgent;
                step.Title = labels.Joined(AgentName);
                step.Meta = JoinMeta(agent);
                if (row.Marker is not AgentHandoffMarker { PersonName: { } shown } || shown != AgentName)
                {
                    row.Marker = AgentHandoffMarker.ForPerson(AgentName, agent?.Avatar);
                }
                break;
            case AgentHandoffStep step:
                step.Title = labels.ResolvedStep;
                step.Meta = null;
                row.Marker ??= AgentHandoffMarker.ForGlyph(ArcIcons.CircleCheck, success: true);
                break;
            case AgentHandoffTypingView typingView:
                typingView.Text = typingView.IsAgent ? labels.IsTyping(AgentFirstName) : labels.Thinking;
                break;
            case AgentHandoffSuggestion suggestion:
                suggestion.Text = labels.TalkToPerson;
                break;
            case AgentHandoffCard card:
                row.Marker ??= AgentHandoffMarker.ForGlyph(ProviderMark ?? ArcIcons.Inbox, ProviderMarkBrush);
                UpdateCard(card, stage);
                break;
        }
    }

    private static string? JoinMeta(AgentHandoffAgent? agent)
    {
        if (agent is null)
        {
            return null;
        }
        var role = string.IsNullOrEmpty(agent.Role) ? null : agent.Role;
        var time = string.IsNullOrEmpty(agent.JoinedAt) ? null : agent.JoinedAt;
        return role is not null && time is not null ? role + " · " + time : role ?? time;
    }

    private void UpdateCard(AgentHandoffCard card, AgentHandoffStage stage)
    {
        var labels = Labels;
        var provider = ProviderName;
        switch (stage)
        {
            case AgentHandoffStage.Transferring:
                card.SetTitle(AgentHandoffStatusKind.Transferring, labels.CardTransferring);
                card.Detail = string.IsNullOrEmpty(ReplyEstimate) ? null : labels.TypicalReply(ReplyEstimate);
                card.SetAction(DisplayedCancel ? labels.Cancel : null);
                break;
            case AgentHandoffStage.Connected:
                card.SetTitle(AgentHandoffStatusKind.Connected, labels.CardConnected(AgentFirstName, provider));
                card.Detail = labels.ViaProvider(provider);
                card.SetAction(null);
                break;
            case AgentHandoffStage.Resolved:
                card.SetTitle(AgentHandoffStatusKind.Resolved, labels.CardResolved(AgentFirstName));
                card.Detail = labels.ViaProvider(provider);
                card.SetAction(null);
                break;
            default:
                card.SetTitle(AgentHandoffStatusKind.Unavailable, labels.CardUnavailable);
                card.Detail = labels.UnavailableHint;
                card.SetAction(DisplayedRetry ? labels.TryAgain : null);
                break;
        }
        var person = stage is AgentHandoffStage.Connected or AgentHandoffStage.Resolved;
        var agent = DisplayedAgent;
        card.PersonName = person ? AgentName : null;
        card.PersonRole = person ? agent?.Role : null;
        card.PersonPhoto = person ? agent?.Avatar : null;
        card.ToggleText = labels.SummaryShared;
        card.SetSummary(DisplayedSummary, IsSummaryExpanded, labels.ShowSummary, labels.HideSummary, instant: !_rowsBuilt);
    }
}
