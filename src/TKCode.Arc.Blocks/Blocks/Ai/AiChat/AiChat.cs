using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A chat with an assistant: a title menu, a model button and a new chat button on top, a thread of turns in the middle and
/// a message box at the bottom. Replies stream in as Markdown with collapsible reasoning, tool chips, numbered citations
/// that preview their source, and actions to copy, retry, rate and switch between versions. Editing a message or retrying
/// a reply makes a new version of the turn; the turns after it belong to the version they followed.
/// <code>
/// &lt;arc:AiChat Responder="{Binding Responder}" CurrentConversation="{Binding Conversation}"&gt;
///     &lt;arc:AiChat.Models&gt;
///         &lt;arc:AiModel Id="orbit-pro" Name="Orbit Pro 3" Note="Northwind Labs" /&gt;
///     &lt;/arc:AiChat.Models&gt;
/// &lt;/arc:AiChat&gt;
/// </code>
/// The chat calls <see cref="Responder"/> to answer, shows what it sends as it arrives and lets the person stop a reply.
/// Without a responder, a sent message only appears in the thread and raises <see cref="MessageSent"/>, and your code fills
/// the <see cref="AiChatReply"/> itself.
/// </summary>
public partial class AiChat : TemplatedControl
{
    #region Properties

    /// <summary>The conversation that is shown (two-way). Null shows the empty chat; the first message then creates one.</summary>
    public static readonly StyledProperty<AiChatConversation?> CurrentConversationProperty =
        AvaloniaProperty.Register<AiChat, AiChatConversation?>(nameof(CurrentConversation), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected model (two-way); null selects the first model once.</summary>
    public static readonly StyledProperty<AiModel?> SelectedModelProperty =
        AvaloniaProperty.Register<AiChat, AiModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Provides the replies; null leaves answering to your code.</summary>
    public static readonly StyledProperty<IAiChatResponder?> ResponderProperty =
        AvaloniaProperty.Register<AiChat, IAiChatResponder?>(nameof(Responder));

    /// <summary>The width of the column of turns (760 by default); the thread is this plus 28 on each side.</summary>
    public static readonly StyledProperty<double> ColumnWidthProperty =
        AvaloniaProperty.Register<AiChat, double>(nameof(ColumnWidth), 760);

    /// <summary>The heading of the empty chat; null shows <see cref="AiChatLabels.Greeting"/>.</summary>
    public static readonly StyledProperty<string?> GreetingTextProperty =
        AvaloniaProperty.Register<AiChat, string?>(nameof(GreetingText));

    /// <summary>The placeholder of the message box; null shows <see cref="AiChatLabels.Placeholder"/>.</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<AiChat, string?>(nameof(PlaceholderText));

    /// <summary>Whether the attach button and dropping files are on.</summary>
    public static readonly StyledProperty<bool> IsAttachmentEnabledProperty =
        AvaloniaProperty.Register<AiChat, bool>(nameof(IsAttachmentEnabled), true);

    /// <summary>The file types that may be attached, in the syntax of <c>FileDropzone.Accept</c>; null accepts any.</summary>
    public static readonly StyledProperty<string?> AcceptedFileTypesProperty =
        AvaloniaProperty.Register<AiChat, string?>(nameof(AcceptedFileTypes));

    /// <summary>How often streamed text is released (34 ms): each step shows a few whole words.</summary>
    public static readonly StyledProperty<TimeSpan> RevealIntervalProperty =
        AvaloniaProperty.Register<AiChat, TimeSpan>(nameof(RevealInterval), TimeSpan.FromMilliseconds(34));

    /// <summary>How many whole words each step shows (3).</summary>
    public static readonly StyledProperty<int> WordsPerStepProperty =
        AvaloniaProperty.Register<AiChat, int>(nameof(WordsPerStep), 3);

    /// <summary>Shows the detail of a <see cref="AiChatToolKind.Custom"/> tool; the data context is the <see cref="AiChatTool"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> ToolDetailTemplateProperty =
        AvaloniaProperty.Register<AiChat, IDataTemplate?>(nameof(ToolDetailTemplate));

    /// <summary>Whether the thread follows new content at the bottom (read-only).</summary>
    public static readonly DirectProperty<AiChat, bool> IsFollowingLatestProperty =
        AvaloniaProperty.RegisterDirect<AiChat, bool>(nameof(IsFollowingLatest), o => o.IsFollowingLatest);

    /// <summary>Whether a reply of the shown conversation is thinking or streaming (read-only).</summary>
    public static readonly DirectProperty<AiChat, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<AiChat, bool>(nameof(IsBusy), o => o.IsBusy);

    /// <summary>Run when the person starts a new chat.</summary>
    public static readonly StyledProperty<ICommand?> NewChatCommandProperty =
        AvaloniaProperty.Register<AiChat, ICommand?>(nameof(NewChatCommand));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<AiChatLabels> LabelsProperty =
        AvaloniaProperty.Register<AiChat, AiChatLabels>(nameof(Labels), new AiChatLabels());

    #endregion

    #region Events

    /// <summary>
    /// Raised when the person sends a message, including an edited one, and when a reply is retried (the turn's new or
    /// renewed reply is thinking: without a <see cref="Responder"/> your code answers it).
    /// </summary>
    public static readonly RoutedEvent<AiChatMessageSentEventArgs> MessageSentEvent =
        RoutedEvent.Register<AiChat, AiChatMessageSentEventArgs>(nameof(MessageSent), RoutingStrategies.Bubble);

    /// <summary>Raised when a reply ends: done, stopped or failed.</summary>
    public static readonly RoutedEvent<AiChatReplyEventArgs> ReplyCompletedEvent =
        RoutedEvent.Register<AiChat, AiChatReplyEventArgs>(nameof(ReplyCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when the person rates a reply or takes the rating back.</summary>
    public static readonly RoutedEvent<AiChatFeedbackEventArgs> FeedbackChangedEvent =
        RoutedEvent.Register<AiChat, AiChatFeedbackEventArgs>(nameof(FeedbackChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person picks another model.</summary>
    public static readonly RoutedEvent<ModelPickerModelChangedEventArgs> ModelChangedEvent =
        RoutedEvent.Register<AiChat, ModelPickerModelChangedEventArgs>(nameof(ModelChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person asks for a new chat; set <see cref="RoutedEventArgs.Handled"/> to do it yourself.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> NewChatRequestedEvent =
        RoutedEvent.Register<AiChat, RoutedEventArgs>(nameof(NewChatRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person opens a conversation from the menu.</summary>
    public static readonly RoutedEvent<AiChatConversationEventArgs> ConversationOpenedEvent =
        RoutedEvent.Register<AiChat, AiChatConversationEventArgs>(nameof(ConversationOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when the first message of an empty chat creates a conversation.</summary>
    public static readonly RoutedEvent<AiChatConversationEventArgs> ConversationCreatedEvent =
        RoutedEvent.Register<AiChat, AiChatConversationEventArgs>(nameof(ConversationCreated), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses a citation or a source row; set <see cref="RoutedEventArgs.Handled"/> to open it yourself.</summary>
    public static readonly RoutedEvent<AiChatSourceEventArgs> CitationInvokedEvent =
        RoutedEvent.Register<AiChat, AiChatSourceEventArgs>(nameof(CitationInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the person sends a message, edits one or retries a reply.</summary>
    public event EventHandler<AiChatMessageSentEventArgs>? MessageSent { add => AddHandler(MessageSentEvent, value); remove => RemoveHandler(MessageSentEvent, value); }

    /// <summary>Raised when a reply ends.</summary>
    public event EventHandler<AiChatReplyEventArgs>? ReplyCompleted { add => AddHandler(ReplyCompletedEvent, value); remove => RemoveHandler(ReplyCompletedEvent, value); }

    /// <summary>Raised when the person rates a reply.</summary>
    public event EventHandler<AiChatFeedbackEventArgs>? FeedbackChanged { add => AddHandler(FeedbackChangedEvent, value); remove => RemoveHandler(FeedbackChangedEvent, value); }

    /// <summary>Raised when the person picks another model.</summary>
    public event EventHandler<ModelPickerModelChangedEventArgs>? ModelChanged { add => AddHandler(ModelChangedEvent, value); remove => RemoveHandler(ModelChangedEvent, value); }

    /// <summary>Raised when the person asks for a new chat.</summary>
    public event EventHandler<RoutedEventArgs>? NewChatRequested { add => AddHandler(NewChatRequestedEvent, value); remove => RemoveHandler(NewChatRequestedEvent, value); }

    /// <summary>Raised when the person opens a conversation from the menu.</summary>
    public event EventHandler<AiChatConversationEventArgs>? ConversationOpened { add => AddHandler(ConversationOpenedEvent, value); remove => RemoveHandler(ConversationOpenedEvent, value); }

    /// <summary>Raised when the first message of an empty chat creates a conversation.</summary>
    public event EventHandler<AiChatConversationEventArgs>? ConversationCreated { add => AddHandler(ConversationCreatedEvent, value); remove => RemoveHandler(ConversationCreatedEvent, value); }

    /// <summary>Raised when the person presses a citation or a source row.</summary>
    public event EventHandler<AiChatSourceEventArgs>? CitationInvoked { add => AddHandler(CitationInvokedEvent, value); remove => RemoveHandler(CitationInvokedEvent, value); }

    #endregion

    private bool _isFollowingLatest = true;
    private bool _isBusy;

    /// <summary>The conversations listed in the title menu (the current one included), grouped by their <see cref="AiChatConversation.Group"/>.</summary>
    public AvaloniaList<AiChatConversation> Conversations { get; } = new();

    /// <summary>The models of the model menu. Empty hides the model button.</summary>
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>The providers of the models, for the marks of models that have none of their own.</summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>The prompts of the empty chat; at most three are shown.</summary>
    public AvaloniaList<AiChatSuggestion> Suggestions { get; } = new();

    /// <inheritdoc cref="CurrentConversationProperty"/>
    public AiChatConversation? CurrentConversation { get => GetValue(CurrentConversationProperty); set => SetValue(CurrentConversationProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public AiModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <inheritdoc cref="ResponderProperty"/>
    public IAiChatResponder? Responder { get => GetValue(ResponderProperty); set => SetValue(ResponderProperty, value); }

    /// <inheritdoc cref="ColumnWidthProperty"/>
    public double ColumnWidth { get => GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

    /// <inheritdoc cref="GreetingTextProperty"/>
    public string? GreetingText { get => GetValue(GreetingTextProperty); set => SetValue(GreetingTextProperty, value); }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <inheritdoc cref="IsAttachmentEnabledProperty"/>
    public bool IsAttachmentEnabled { get => GetValue(IsAttachmentEnabledProperty); set => SetValue(IsAttachmentEnabledProperty, value); }

    /// <inheritdoc cref="AcceptedFileTypesProperty"/>
    public string? AcceptedFileTypes { get => GetValue(AcceptedFileTypesProperty); set => SetValue(AcceptedFileTypesProperty, value); }

    /// <inheritdoc cref="RevealIntervalProperty"/>
    public TimeSpan RevealInterval { get => GetValue(RevealIntervalProperty); set => SetValue(RevealIntervalProperty, value); }

    /// <inheritdoc cref="WordsPerStepProperty"/>
    public int WordsPerStep { get => GetValue(WordsPerStepProperty); set => SetValue(WordsPerStepProperty, value); }

    /// <inheritdoc cref="ToolDetailTemplateProperty"/>
    public IDataTemplate? ToolDetailTemplate { get => GetValue(ToolDetailTemplateProperty); set => SetValue(ToolDetailTemplateProperty, value); }

    /// <inheritdoc cref="IsFollowingLatestProperty"/>
    public bool IsFollowingLatest { get => _isFollowingLatest; private set => SetAndRaise(IsFollowingLatestProperty, ref _isFollowingLatest, value); }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy { get => _isBusy; private set => SetAndRaise(IsBusyProperty, ref _isBusy, value); }

    /// <inheritdoc cref="NewChatCommandProperty"/>
    public ICommand? NewChatCommand { get => GetValue(NewChatCommandProperty); set => SetValue(NewChatCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiChatLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
}
