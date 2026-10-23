using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a reply is in its life.</summary>
public enum AiChatReplyStatus
{
    /// <summary>Waiting for the first thing the model sends ("Thinking").</summary>
    Thinking,

    /// <summary>Text is arriving.</summary>
    Streaming,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>The person stopped it; what had been shown stays.</summary>
    Stopped,

    /// <summary>It failed; <see cref="AiChatReply.ErrorMessage"/> says why and Retry runs it again in place.</summary>
    Failed
}

/// <summary>The person's rating of a reply.</summary>
public enum AiChatFeedback
{
    /// <summary>No rating.</summary>
    None,

    /// <summary>"Good response".</summary>
    Up,

    /// <summary>"Bad response".</summary>
    Down
}

/// <summary>What a tool call does; decides the layout of its detail panel.</summary>
public enum AiChatToolKind
{
    /// <summary>A search: the panel lists the queries and the sources found.</summary>
    Search,

    /// <summary>A query of data: the panel shows the statement and its result.</summary>
    Query,

    /// <summary>Anything else; the host supplies the detail through <c>AiChat.ToolDetailTemplate</c>.</summary>
    Custom
}

/// <summary>Where a tool call is.</summary>
public enum AiChatToolStatus
{
    /// <summary>Running: the chip spins, shimmers and cannot be opened.</summary>
    Running,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Failed; the chip shows a warning and the summary says why.</summary>
    Error
}

/// <summary>Who wrote a message of a <see cref="AiChatRequest"/>.</summary>
public enum AiChatRole
{
    /// <summary>The person.</summary>
    User,

    /// <summary>The assistant.</summary>
    Assistant
}

/// <summary>A prompt shown in the empty chat that sends itself when pressed.</summary>
public class AiChatSuggestion : AvaloniaObject
{
    /// <summary>The text of the button.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<AiChatSuggestion, string>(nameof(Label), string.Empty);

    /// <summary>What is sent; null sends the label.</summary>
    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<AiChatSuggestion, string?>(nameof(Prompt));

    /// <summary>A 24 × 24 icon geometry.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiChatSuggestion, Geometry?>(nameof(Icon));

    /// <summary>The text of the button.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>What is sent.</summary>
    public string? Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <summary>The icon of the button.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>A tool call of a reply, such as a search or a query, shown as a chip that can open a detail panel.</summary>
public class AiChatTool : AvaloniaObject
{
    /// <summary>Stable id; stream updates with the same id change the same tool.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiChatTool, string>(nameof(Id), string.Empty);

    /// <summary>What the tool does.</summary>
    public static readonly StyledProperty<AiChatToolKind> KindProperty =
        AvaloniaProperty.Register<AiChatTool, AiChatToolKind>(nameof(Kind), AiChatToolKind.Custom);

    /// <summary>Where the tool call is.</summary>
    public static readonly StyledProperty<AiChatToolStatus> StatusProperty =
        AvaloniaProperty.Register<AiChatTool, AiChatToolStatus>(nameof(Status), AiChatToolStatus.Running);

    /// <summary>The name while running, such as "Searching the web".</summary>
    public static readonly StyledProperty<string> RunningLabelProperty =
        AvaloniaProperty.Register<AiChatTool, string>(nameof(RunningLabel), string.Empty);

    /// <summary>The name once done, such as "Searched the web".</summary>
    public static readonly StyledProperty<string> DoneLabelProperty =
        AvaloniaProperty.Register<AiChatTool, string>(nameof(DoneLabel), string.Empty);

    /// <summary>A short result after the name: "4 sources", "212 rows" or the reason it failed.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<AiChatTool, string?>(nameof(Summary));

    /// <summary>The icon of a finished tool; null picks Search for searches and a database for queries.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<AiChatTool, object?>(nameof(Icon));

    /// <summary>The statement of a query, shown in a monospace block.</summary>
    public static readonly StyledProperty<string?> InputProperty =
        AvaloniaProperty.Register<AiChatTool, string?>(nameof(Input));

    /// <summary>The result of a query.</summary>
    public static readonly StyledProperty<string?> OutputProperty =
        AvaloniaProperty.Register<AiChatTool, string?>(nameof(Output));

    /// <summary>The label before the result; null shows <c>AiChatLabels.Result</c>.</summary>
    public static readonly StyledProperty<string?> OutputLabelProperty =
        AvaloniaProperty.Register<AiChatTool, string?>(nameof(OutputLabel));

    /// <summary>The id of the tool.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>What the tool does.</summary>
    public AiChatToolKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>Where the tool call is.</summary>
    public AiChatToolStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>The name while running.</summary>
    public string RunningLabel { get => GetValue(RunningLabelProperty); set => SetValue(RunningLabelProperty, value); }

    /// <summary>The name once done.</summary>
    public string DoneLabel { get => GetValue(DoneLabelProperty); set => SetValue(DoneLabelProperty, value); }

    /// <summary>The short result after the name.</summary>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <summary>The icon of a finished tool.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The statement of a query.</summary>
    public string? Input { get => GetValue(InputProperty); set => SetValue(InputProperty, value); }

    /// <summary>The result of a query.</summary>
    public string? Output { get => GetValue(OutputProperty); set => SetValue(OutputProperty, value); }

    /// <summary>The label before the result.</summary>
    public string? OutputLabel { get => GetValue(OutputLabelProperty); set => SetValue(OutputLabelProperty, value); }

    /// <summary>The search terms of a search.</summary>
    public AvaloniaList<string> Queries { get; } = new();

    /// <summary>The sources a search found; the chip shows their marks.</summary>
    public AvaloniaList<AiSource> Sources { get; } = new();
}

/// <summary>The assistant's answer to a prompt: reasoning, tool calls, sources, the text and how it ended.</summary>
public class AiChatReply : AvaloniaObject
{
    /// <summary>The id of the model that wrote it (an <see cref="AiModel.Id"/>); decides the mark next to the reply.</summary>
    public static readonly StyledProperty<string?> ModelIdProperty =
        AvaloniaProperty.Register<AiChatReply, string?>(nameof(ModelId));

    /// <summary>Where the reply is in its life.</summary>
    public static readonly StyledProperty<AiChatReplyStatus> StatusProperty =
        AvaloniaProperty.Register<AiChatReply, AiChatReplyStatus>(nameof(Status), AiChatReplyStatus.Done);

    /// <summary>The reasoning text, shown under "Thought for 3s" when opened.</summary>
    public static readonly StyledProperty<string> ReasoningProperty =
        AvaloniaProperty.Register<AiChatReply, string>(nameof(Reasoning), string.Empty);

    /// <summary>How long the model thought; null uses the measured time, rounded to seconds (at least 1).</summary>
    public static readonly StyledProperty<TimeSpan?> ThoughtForProperty =
        AvaloniaProperty.Register<AiChatReply, TimeSpan?>(nameof(ThoughtFor));

    /// <summary>The reply text in the Markdown subset: paragraphs, lists, short headings, bold, inline code, code blocks and "[n]" citations.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiChatReply, string>(nameof(Text), string.Empty);

    /// <summary>Why the reply failed.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<AiChatReply, string?>(nameof(ErrorMessage));

    /// <summary>The person's rating (two-way).</summary>
    public static readonly StyledProperty<AiChatFeedback> FeedbackProperty =
        AvaloniaProperty.Register<AiChatReply, AiChatFeedback>(nameof(Feedback), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The id of the model that wrote the reply.</summary>
    public string? ModelId { get => GetValue(ModelIdProperty); set => SetValue(ModelIdProperty, value); }

    /// <summary>Where the reply is in its life.</summary>
    public AiChatReplyStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>The reasoning text.</summary>
    public string Reasoning { get => GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }

    /// <summary>How long the model thought.</summary>
    public TimeSpan? ThoughtFor { get => GetValue(ThoughtForProperty); set => SetValue(ThoughtForProperty, value); }

    /// <summary>The reply text.</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Why the reply failed.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>The person's rating.</summary>
    public AiChatFeedback Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <summary>The tool calls of the reply, in the order they started.</summary>
    public AvaloniaList<AiChatTool> Tools { get; } = new();

    /// <summary>The sources of the reply, numbered from 1 in the order they arrived.</summary>
    public AvaloniaList<AiSource> Sources { get; } = new();
}

/// <summary>What the person wrote: the text and the files attached to it.</summary>
public class AiChatUserMessage : AvaloniaObject
{
    /// <summary>The text in the Markdown subset; the bubble shows inline code, the editor the source.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiChatUserMessage, string>(nameof(Text), string.Empty);

    /// <summary>The text of the message.</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The files sent with it (they are listed above the text).</summary>
    public AvaloniaList<AiAttachment> Attachments { get; } = new();
}

/// <summary>
/// One version of a turn: a prompt, its reply and what happened after it. Editing a prompt or retrying a finished
/// reply adds a version; the turns that came after belong to the version they followed.
/// </summary>
public class AiChatTurnVersion : AvaloniaObject
{
    /// <summary>What the person asked in this version.</summary>
    public static readonly StyledProperty<AiChatUserMessage> PromptProperty =
        AvaloniaProperty.Register<AiChatTurnVersion, AiChatUserMessage>(nameof(Prompt), new AiChatUserMessage());

    /// <summary>The reply to it.</summary>
    public static readonly StyledProperty<AiChatReply> ReplyProperty =
        AvaloniaProperty.Register<AiChatTurnVersion, AiChatReply>(nameof(Reply), new AiChatReply());

    /// <summary>What the person asked in this version.</summary>
    public AiChatUserMessage Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <summary>The reply to the prompt.</summary>
    public AiChatReply Reply { get => GetValue(ReplyProperty); set => SetValue(ReplyProperty, value); }

    /// <summary>The turns that followed this version.</summary>
    public AvaloniaList<AiChatTurn> Continuation { get; } = new();
}

/// <summary>A question and its answer, with every version of them (edits and retries).</summary>
public class AiChatTurn : AvaloniaObject
{
    /// <summary>Which version is shown (two-way).</summary>
    public static readonly StyledProperty<int> SelectedVersionIndexProperty =
        AvaloniaProperty.Register<AiChatTurn, int>(nameof(SelectedVersionIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Creates a turn with one version.</summary>
    public AiChatTurn() { }

    /// <summary>Creates a turn with one version.</summary>
    /// <param name="prompt">The first version's prompt.</param>
    /// <param name="reply">The first version's reply.</param>
    public AiChatTurn(AiChatUserMessage prompt, AiChatReply reply)
    {
        Versions.Add(new AiChatTurnVersion { Prompt = prompt, Reply = reply });
    }

    /// <summary>The versions in the order they were made.</summary>
    public AvaloniaList<AiChatTurnVersion> Versions { get; } = new();

    /// <summary>The index of the shown version.</summary>
    public int SelectedVersionIndex { get => GetValue(SelectedVersionIndexProperty); set => SetValue(SelectedVersionIndexProperty, value); }

    /// <summary>The shown version, or null when there is none.</summary>
    public AiChatTurnVersion? SelectedVersion =>
        SelectedVersionIndex >= 0 && SelectedVersionIndex < Versions.Count ? Versions[SelectedVersionIndex] : null;
}

/// <summary>A conversation: a title and the turns of its first version of events.</summary>
public class AiChatConversation : AvaloniaObject
{
    /// <summary>Stable id.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiChatConversation, string>(nameof(Id), string.Empty);

    /// <summary>The title in the menu; empty takes the first six words of the first message, then "Untitled chat".</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AiChatConversation, string>(nameof(Title), string.Empty);

    /// <summary>The heading of the menu group the conversation is listed under, such as "Today".</summary>
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<AiChatConversation, string?>(nameof(Group));

    /// <summary>The id of the model the conversation last used.</summary>
    public static readonly StyledProperty<string?> ModelIdProperty =
        AvaloniaProperty.Register<AiChatConversation, string?>(nameof(ModelId));

    /// <summary>The id of the conversation.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The title of the conversation.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The menu group of the conversation.</summary>
    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    /// <summary>The model the conversation last used.</summary>
    public string? ModelId { get => GetValue(ModelIdProperty); set => SetValue(ModelIdProperty, value); }

    /// <summary>The turns that start the conversation; later turns live in the <see cref="AiChatTurnVersion.Continuation"/> of a version.</summary>
    public AvaloniaList<AiChatTurn> Turns { get; } = new();
}
