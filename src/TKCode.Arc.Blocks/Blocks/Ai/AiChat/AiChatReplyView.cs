using System.Collections.Specialized;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One reply of <see cref="AiChat"/> (Arc 3.3, 3.6–3.12), 10px between its parts: the head (the model's mark in a 22px
/// cell, then "Thinking" or "Thought for 3s" with the reasoning under it), the tool chips (6px apart) with their detail
/// panels, the text, the stopped line or the error box, and the action bar with the version pager.
/// <para>
/// It follows its <see cref="Reply"/> while it is in a window: reasoning, tools, sources, status and feedback as they
/// change. The text has two modes. A reply the chat streams itself is driven (<see cref="BeginDriven"/>): the chat pushes
/// the deltas and decides when the text ends, and changes of <see cref="AiChatReply.Text"/> are ignored. Any other reply
/// follows its data: text that grows by appending is paced like a stream, other text is shown at once, and a finished
/// status ends the stream. The action bar keeps its height but shows nothing while the reply thinks or streams; a failed
/// reply has the error box instead (the bar stays only while there are versions to switch).
/// </para>
/// </summary>
[TemplatePart(ReasoningPart, typeof(AiReasoningDisclosure))]
[TemplatePart(ToolsPart, typeof(Panel))]
[TemplatePart(ChipsPart, typeof(Panel))]
[TemplatePart(DetailsPart, typeof(Panel))]
[TemplatePart(TextPart, typeof(AiReplyText))]
[TemplatePart(StatusPart, typeof(AiStatusNote))]
[TemplatePart(ActionsPart, typeof(AiMessageActions))]
internal sealed class AiChatReplyView : TemplatedControl
{
    public const string ReasoningPart = "PART_Reasoning";
    public const string ToolsPart = "PART_Tools";
    public const string ChipsPart = "PART_Chips";
    public const string DetailsPart = "PART_Details";
    public const string TextPart = "PART_Text";
    public const string StatusPart = "PART_Status";
    public const string ActionsPart = "PART_Actions";

    /// <summary>The reply shown.</summary>
    public static readonly StyledProperty<AiChatReply?> ReplyProperty =
        AvaloniaProperty.Register<AiChatReplyView, AiChatReply?>(nameof(Reply));

    /// <inheritdoc cref="ReplyProperty"/>
    public AiChatReply? Reply { get => GetValue(ReplyProperty); set => SetValue(ReplyProperty, value); }

    private readonly ModelMark _mark = new();
    private readonly List<(AiChatTool Tool, AiToolChip Chip, AiToolDetail Detail)> _tools = new();
    private AiChat? _owner;
    private AiReasoningDisclosure? _reasoning;
    private Panel? _toolsPanel;
    private Panel? _chips;
    private Panel? _details;
    private AiReplyText? _text;
    private AiStatusNote? _status;
    private AiMessageActions? _actions;
    private AiChatReply? _watched;
    private TextMode _mode;
    private string _pushed = string.Empty;
    private DateTimeOffset? _thinkingSince;
    private TimeSpan? _measured;
    private bool _isLast;
    private bool _isRevealed;
    private bool _hasVersions;
    private bool _alwaysShowsActions;
    private Control? _pager;

    public AiChatReplyView()
    {
        // A monochrome mark takes the secondary text color (Arc: currentColor in TextSecondary).
        ArcTokens.Bind(_mark, ModelMark.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
    }

    internal AiReasoningDisclosure? Reasoning => _reasoning;
    internal AiReplyText? Text => _text;
    internal AiStatusNote? Status => _status;
    internal AiMessageActions? Actions => _actions;
    internal ModelMark Mark => _mark;
    internal IReadOnlyList<AiToolChip> Chips => _tools.Select(t => t.Chip).ToList();
    internal IReadOnlyList<AiToolDetail> Details => _tools.Select(t => t.Detail).ToList();

    /// <summary>True while the chat drives the text of this reply.</summary>
    internal bool IsDriven => _mode == TextMode.Driven;

    /// <summary>The chat that shows the reply (labels, models, timing).</summary>
    internal AiChat? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            ApplyLabels();
        }
    }

    /// <summary>The reply is the last of the thread: its actions always show.</summary>
    internal bool IsLast
    {
        get => _isLast;
        set
        {
            _isLast = value;
            if (_actions is not null)
            {
                _actions.IsLast = value;
            }
        }
    }

    /// <summary>The pointer is over the turn or the focus is inside it: the actions show.</summary>
    internal bool IsRevealed
    {
        get => _isRevealed;
        set
        {
            _isRevealed = value;
            if (_actions is not null)
            {
                _actions.IsRevealed = value;
            }
        }
    }

    /// <summary>Touch input: the actions always show (nothing hovers).</summary>
    internal bool AlwaysShowsActions
    {
        get => _alwaysShowsActions;
        set
        {
            _alwaysShowsActions = value;
            if (_actions is not null)
            {
                _actions.AlwaysVisible = value;
            }
        }
    }

    /// <summary>The turn has more than one version (the bar stays for its pager even when the reply failed).</summary>
    internal bool HasVersions
    {
        get => _hasVersions;
        set
        {
            _hasVersions = value;
            UpdateStatus();
        }
    }

    /// <summary>The version pager shown first in the action bar.</summary>
    internal Control? VersionPager
    {
        get => _pager;
        set
        {
            _pager = value;
            if (_actions is not null)
            {
                _actions.VersionPager = value;
            }
        }
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ClearTools();
        if (_actions is not null)
        {
            _actions.VersionPager = null;
        }
        var scope = e.NameScope;
        _reasoning = scope.Find<AiReasoningDisclosure>(ReasoningPart);
        _toolsPanel = scope.Find<Panel>(ToolsPart);
        _chips = scope.Find<Panel>(ChipsPart);
        _details = scope.Find<Panel>(DetailsPart);
        _text = scope.Find<AiReplyText>(TextPart);
        _status = scope.Find<AiStatusNote>(StatusPart);
        _actions = scope.Find<AiMessageActions>(ActionsPart);
        if (_reasoning is not null)
        {
            _reasoning.Lead = _mark;
        }
        if (_actions is not null)
        {
            _actions.IsLast = _isLast;
            _actions.IsRevealed = _isRevealed;
            _actions.AlwaysVisible = _alwaysShowsActions;
            _actions.VersionPager = _pager;
        }
        ApplyLabels();
        ApplyTiming();
        Sync(rebuildText: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ReplyProperty)
        {
            Watch(IsAttachedToVisualTree() ? change.GetNewValue<AiChatReply?>() : null);
            _mode = TextMode.None;
            _measured = null;
            _thinkingSince = null;
            ClearTools();
            Sync(rebuildText: true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Reply);
        Sync(rebuildText: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    /// <summary>Every word from the chat's labels.</summary>
    internal void ApplyLabels()
    {
        var labels = _owner?.Labels;
        if (labels is null)
        {
            return;
        }
        if (_reasoning is not null)
        {
            _reasoning.ThinkingText = labels.Thinking;
        }
        if (_text is not null)
        {
            _text.CitationName = labels.SourceLink;
        }
        if (_actions is not null)
        {
            _actions.CopyText = labels.Copy;
            _actions.CopiedText = labels.Copied;
            _actions.RetryText = labels.Retry;
            _actions.GoodText = labels.GoodResponse;
            _actions.BadText = labels.BadResponse;
        }
        foreach (var (_, _, detail) in _tools)
        {
            ApplyDetailLabels(detail, labels);
        }
        UpdateThought();
        UpdateStatus();
    }

    /// <summary>The cadence of streamed text, from the chat.</summary>
    internal void ApplyTiming()
    {
        if (_text is not null && _owner is not null)
        {
            _text.RevealInterval = _owner.RevealInterval;
            _text.WordsPerStep = _owner.WordsPerStep;
        }
    }

    /// <summary>The model's mark, from the chat's models and providers (the reply keeps the mark of the model that wrote it).</summary>
    internal void UpdateMark()
    {
        var model = _owner?.FindModel(Reply?.ModelId);
        _mark.Mark = ModelMark.Resolve(model, _owner?.Providers, out var color);
        _mark.MarkColor = color;
    }

    #endregion

    #region Following the reply

    private void Watch(AiChatReply? reply)
    {
        if (ReferenceEquals(reply, _watched))
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnReplyPropertyChanged;
            _watched.Tools.CollectionChanged -= OnToolsChanged;
        }
        _watched = reply;
        if (reply is not null)
        {
            reply.PropertyChanged += OnReplyPropertyChanged;
            reply.Tools.CollectionChanged += OnToolsChanged;
        }
    }

    /// <summary>Brings everything in line with the reply.</summary>
    private void Sync(bool rebuildText)
    {
        UpdateMark();
        SyncTools();
        if (_reasoning is not null)
        {
            _reasoning.Reasoning = Reply?.Reasoning;
        }
        if (_text is not null)
        {
            _text.Sources = Reply?.Sources;
        }
        if (_actions is not null)
        {
            _actions.Feedback = Reply?.Feedback ?? AiChatFeedback.None;
        }
        if (rebuildText)
        {
            SyncText();
        }
        UpdateThought();
        UpdateStatus();
    }

    /// <summary>
    /// The text from scratch: a reply the chat streams starts driven with what arrived so far (and finishes if its stream
    /// already ended); any other reply follows its data.
    /// </summary>
    private void SyncText()
    {
        if (_owner is not null && _owner.TryGetDriven(Reply, out var raw, out var ended))
        {
            BeginDriven(raw);
            if (ended && raw.Length > 0)
            {
                CompleteText();
            }
        }
        else if (_mode != TextMode.Driven)
        {
            ShowText();
        }
    }

    private void OnReplyPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiChatReply.StatusProperty)
        {
            OnStatusChanged(e.GetNewValue<AiChatReplyStatus>());
        }
        else if (e.Property == AiChatReply.TextProperty)
        {
            OnTextChanged(e.GetNewValue<string>() ?? string.Empty);
        }
        else if (e.Property == AiChatReply.ReasoningProperty)
        {
            if (_reasoning is not null)
            {
                _reasoning.Reasoning = e.GetNewValue<string>();
            }
        }
        else if (e.Property == AiChatReply.ThoughtForProperty)
        {
            UpdateThought();
        }
        else if (e.Property == AiChatReply.ErrorMessageProperty)
        {
            UpdateStatus();
        }
        else if (e.Property == AiChatReply.FeedbackProperty)
        {
            if (_actions is not null)
            {
                _actions.Feedback = e.GetNewValue<AiChatFeedback>();
            }
        }
        else if (e.Property == AiChatReply.ModelIdProperty)
        {
            UpdateMark();
        }
    }

    private void OnStatusChanged(AiChatReplyStatus status)
    {
        if (_mode == TextMode.Following && _text is not null)
        {
            switch (status)
            {
                case AiChatReplyStatus.Done:
                    // Whatever the data holds now is the whole text: show the rest at the pace, then finish.
                    CatchUp(Reply?.Text ?? string.Empty);
                    _text.Complete();
                    break;
                case AiChatReplyStatus.Stopped:
                case AiChatReplyStatus.Failed:
                    _text.Stop();
                    break;
            }
        }
        else if (_mode == TextMode.Static && status is AiChatReplyStatus.Thinking or AiChatReplyStatus.Streaming)
        {
            // The host runs the reply again in place: start over.
            ShowText();
        }
        UpdateThought();
        UpdateStatus();
    }

    private void OnTextChanged(string text)
    {
        if (_mode == TextMode.Driven || _text is null)
        {
            return;
        }
        if (_mode == TextMode.Following)
        {
            CatchUp(text);
        }
        else
        {
            ShowText();
        }
        UpdateThought();
        UpdateStatus();
    }

    /// <summary>Pushes what was appended to the text since the last push; a text that does not start with it starts over.</summary>
    private void CatchUp(string text)
    {
        if (_text is null)
        {
            return;
        }
        if (text.StartsWith(_pushed, StringComparison.Ordinal))
        {
            if (text.Length > _pushed.Length)
            {
                _text.IsVisible = true;
                _text.Push(text[_pushed.Length..]);
                _pushed = text;
            }
            return;
        }
        _text.Reset();
        _pushed = string.Empty;
        CatchUp(text);
    }

    /// <summary>A reply that follows its data: streaming ones are paced from the start, finished ones show at once.</summary>
    private void ShowText()
    {
        if (_text is null)
        {
            return;
        }
        var reply = Reply;
        var text = reply?.Text ?? string.Empty;
        if (reply is { Status: AiChatReplyStatus.Thinking or AiChatReplyStatus.Streaming })
        {
            _mode = TextMode.Following;
            _text.Reset();
            _pushed = string.Empty;
            CatchUp(text);
        }
        else
        {
            _mode = TextMode.Static;
            _pushed = text;
            _text.SetText(text);
        }
        _text.IsVisible = text.Length > 0;
    }

    #endregion

    #region Driven text (the chat streams the reply)

    /// <summary>The chat streams this reply: <paramref name="soFar"/> is what arrived before this view existed (paced, catching up).</summary>
    internal void BeginDriven(string soFar)
    {
        _mode = TextMode.Driven;
        _pushed = string.Empty;
        if (_text is null)
        {
            return;
        }
        _text.Reset();
        _text.IsVisible = soFar.Length > 0;
        if (soFar.Length > 0)
        {
            _text.Push(soFar);
        }
    }

    /// <summary>More streamed text.</summary>
    internal void PushText(string delta)
    {
        if (_text is null || delta.Length == 0)
        {
            return;
        }
        _text.IsVisible = true;
        _text.Push(delta);
        UpdateThought();
    }

    /// <summary>The stream ended: the rest is shown at the pace and the text raises <c>RevealCompleted</c>.</summary>
    internal void CompleteText() => _text?.Complete();

    /// <summary>The reply stops: the backlog is dropped. Returns the text shown, as it was pushed.</summary>
    internal string StopText()
    {
        if (_text is null)
        {
            return string.Empty;
        }
        _text.Stop();
        return _text.RevealedSource;
    }

    /// <summary>The chat no longer drives the text: from now on it follows the data (which now holds the final text).</summary>
    internal void EndDriven()
    {
        if (_mode != TextMode.Driven)
        {
            return;
        }
        _mode = TextMode.Static;
        _pushed = Reply?.Text ?? string.Empty;
        UpdateStatus();
    }

    /// <summary>Whether the text view has anything to wait for (a paced backlog that has not been shown yet).</summary>
    internal bool IsRevealing => _text?.IsRevealing == true;

    #endregion

    #region Head and status

    /// <summary>
    /// The head: "Thinking" (shimmering) until the reasoning ended, a tool started or text came; then "Thought for Ns" from
    /// <see cref="AiChatReply.ThoughtFor"/>, else the time this view saw the reply think, at least a second.
    /// </summary>
    private void UpdateThought()
    {
        if (_reasoning is null || _owner is null)
        {
            return;
        }
        var reply = Reply;
        var thinking = reply is { Status: AiChatReplyStatus.Thinking, ThoughtFor: null } && reply.Tools.Count == 0 &&
                       string.IsNullOrEmpty(reply.Text) && !(_text?.HasText ?? false);
        var now = _owner.Time.GetUtcNow();
        if (thinking)
        {
            _thinkingSince ??= now;
        }
        else if (_thinkingSince is { } since && _measured is null)
        {
            _measured = now - since;
        }
        _reasoning.IsThinking = thinking;
        var duration = reply?.ThoughtFor ?? _measured;
        var seconds = duration is { } d ? Math.Max(1, (int)Math.Round(d.TotalSeconds, MidpointRounding.AwayFromZero)) : 1;
        _reasoning.ThoughtText = _owner.Labels.ThoughtFor(seconds);
    }

    /// <summary>The stopped line, the error box, and whether the action bar streams, shows or is replaced by the box.</summary>
    private void UpdateStatus()
    {
        var reply = Reply;
        var labels = _owner?.Labels;
        var status = reply?.Status ?? AiChatReplyStatus.Done;
        if (_status is not null && labels is not null)
        {
            switch (status)
            {
                case AiChatReplyStatus.Stopped:
                    var shown = !string.IsNullOrEmpty(reply?.Text) || (_mode == TextMode.Driven && _text?.HasText == true);
                    _status.Tone = AiStatusNoteTone.Stopped;
                    _status.Layout = AiStatusNoteLayout.Inline;
                    _status.Title = null;
                    _status.Text = shown ? labels.Stopped : labels.StoppedBeforeAnswering;
                    _status.ActionText = null;
                    Show(_status);
                    break;
                case AiChatReplyStatus.Failed:
                    _status.Tone = AiStatusNoteTone.Error;
                    _status.Layout = AiStatusNoteLayout.Card;
                    _status.Title = labels.ReplyFailed;
                    _status.Text = reply?.ErrorMessage;
                    _status.ActionText = labels.Retry;
                    Show(_status);
                    break;
                default:
                    _status.IsVisible = false;
                    _status.IsActive = false;
                    break;
            }
        }
        if (_actions is not null)
        {
            _actions.IsStreaming = status is AiChatReplyStatus.Thinking or AiChatReplyStatus.Streaming;
            _actions.IsVisible = status != AiChatReplyStatus.Failed || _hasVersions;
        }
        if (_text is not null)
        {
            _text.IsVisible = _text.HasText || _text.IsRevealing || (_mode != TextMode.Driven && !string.IsNullOrEmpty(reply?.Text));
        }
    }

    private static void Show(AiStatusNote note)
    {
        if (!note.IsVisible)
        {
            // It comes with the new state: in place at once, then (in a window) its own fade.
            note.IsActive = false;
            note.IsVisible = true;
        }
        note.IsActive = true;
    }

    #endregion

    #region Tools

    private void OnToolsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTools();
        UpdateThought();
    }

    /// <summary>One chip and one detail panel per tool, in order; chips of tools that stay are kept (with their open state).</summary>
    private void SyncTools()
    {
        if (_chips is null || _details is null)
        {
            return;
        }
        var tools = Reply?.Tools;
        var count = tools?.Count ?? 0;
        var labels = _owner?.Labels;
        var kept = new List<(AiChatTool, AiToolChip, AiToolDetail)>(count);
        for (var i = 0; i < count; i++)
        {
            var tool = tools![i];
            var index = _tools.FindIndex(t => ReferenceEquals(t.Tool, tool));
            if (index >= 0)
            {
                kept.Add(_tools[index]);
                _tools.RemoveAt(index);
                continue;
            }
            var chip = new AiToolChip { Tool = tool };
            var detail = new AiToolDetail { Tool = tool, DetailTemplate = _owner?.ToolDetailTemplate };
            if (labels is not null)
            {
                ApplyDetailLabels(detail, labels);
            }
            chip.PropertyChanged += OnChipPropertyChanged;
            kept.Add((tool, chip, detail));
        }
        foreach (var (_, chip, _) in _tools)
        {
            chip.PropertyChanged -= OnChipPropertyChanged;
        }
        _tools.Clear();
        _tools.AddRange(kept);
        Place(_chips, _tools.Select(t => (Control)t.Chip));
        Place(_details, _tools.Select(t => (Control)t.Detail));
        if (_toolsPanel is not null)
        {
            _toolsPanel.IsVisible = _tools.Count > 0;
        }
    }

    /// <summary>Puts <paramref name="wanted"/> in <paramref name="panel"/> in order, touching only what changed.</summary>
    private static void Place(Panel panel, IEnumerable<Control> wanted)
    {
        var list = wanted.ToList();
        var same = 0;
        while (same < list.Count && same < panel.Children.Count && ReferenceEquals(panel.Children[same], list[same]))
        {
            same++;
        }
        if (same == list.Count && same == panel.Children.Count)
        {
            return;
        }
        while (panel.Children.Count > same)
        {
            panel.Children.RemoveAt(panel.Children.Count - 1);
        }
        for (var i = same; i < list.Count; i++)
        {
            panel.Children.Add(list[i]);
        }
    }

    private void ClearTools()
    {
        foreach (var (_, chip, _) in _tools)
        {
            chip.PropertyChanged -= OnChipPropertyChanged;
        }
        _tools.Clear();
        _chips?.Children.Clear();
        _details?.Children.Clear();
        if (_toolsPanel is not null)
        {
            _toolsPanel.IsVisible = false;
        }
    }

    /// <summary>A chip opens and closes its own detail panel; several can be open at once.</summary>
    private void OnChipPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != AiToolChip.IsExpandedProperty || sender is not AiToolChip chip)
        {
            return;
        }
        foreach (var (_, c, detail) in _tools)
        {
            if (ReferenceEquals(c, chip))
            {
                detail.IsExpanded = e.GetNewValue<bool>();
            }
        }
    }

    /// <summary>The narrow chat hides the domains of source rows.</summary>
    internal void SetNarrow(bool narrow)
    {
        foreach (var (_, _, detail) in _tools)
        {
            detail.IsNarrow = narrow;
        }
    }

    private void ApplyDetailLabels(AiToolDetail detail, AiChatLabels labels)
    {
        detail.SearchesName = labels.Searches;
        detail.SourcesName = labels.Sources;
        detail.ResultText = labels.Result;
        detail.SourceLinkName = labels.SourceLink;
        detail.IsNarrow = _owner?.IsNarrowChat == true;
    }

    #endregion

    private enum TextMode
    {
        /// <summary>Nothing decided yet.</summary>
        None,

        /// <summary>A finished text shown at once.</summary>
        Static,

        /// <summary>The text follows the data, appended text paced.</summary>
        Following,

        /// <summary>The chat pushes the text.</summary>
        Driven
    }
}
