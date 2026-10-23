using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>
/// The prompt box of an AI app: a text area that grows with its text, "/" commands and "@" mentions that become tokens,
/// attachments that upload in place (picked, pasted or dropped on the box), tool toggles, a model and effort menu, a
/// character counter and a send button that turns into a stop button while a reply is generated.
/// <code>
/// &lt;arc:AiComposer Placeholder="Ask a question, type / for actions, @ for a file" Sent="OnSent"&gt;
///     &lt;arc:AiComposer.Commands&gt;
///         &lt;arc:AiCommand Id="recap" Name="recap" Description="Boil a doc down to its main points" /&gt;
///     &lt;/arc:AiComposer.Commands&gt;
/// &lt;/arc:AiComposer&gt;
/// </code>
/// The composer reports a send through <see cref="Sent"/> (or <see cref="SendCommand"/>); your code starts the reply and
/// sets <see cref="IsGenerating"/> while it runs, and the send button becomes a stop button that raises
/// <see cref="StopRequested"/>. While a reply is generated the box stays editable but Enter does not send.
/// </summary>
public partial class AiComposer : TemplatedControl
{
    #region Properties

    /// <summary>The prompt text, tokens included (two-way).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiComposer, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The prompt with its tokens (two-way). Setting it replaces the text and the tokens.</summary>
    public static readonly StyledProperty<AiComposerValue> ValueProperty =
        AvaloniaProperty.Register<AiComposer, AiComposerValue>(nameof(Value), AiComposerValue.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The hint shown while the box is empty; null shows <see cref="AiComposerLabels.Placeholder"/>.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<AiComposer, string?>(nameof(Placeholder));

    /// <summary>The accessible name of the text area.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<AiComposer, string?>(nameof(AccessibleName));

    /// <summary>The box is this many lines high when it is empty.</summary>
    public static readonly StyledProperty<int> MinLinesProperty =
        AvaloniaProperty.Register<AiComposer, int>(nameof(MinLines), 1);

    /// <summary>The box grows to this many lines and then scrolls inside (8; in the <see cref="AiComposerLayout.Inline"/> layout 7 unless set).</summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<AiComposer, int>(nameof(MaxLines), 8);

    /// <summary>The longest prompt in characters; null means no limit and no counter. Over the limit, sending is blocked.</summary>
    public static readonly StyledProperty<int?> MaxLengthProperty =
        AvaloniaProperty.Register<AiComposer, int?>(nameof(MaxLength), 4000);

    /// <summary>From this share of <see cref="MaxLength"/> the counter shows how many characters are left.</summary>
    public static readonly StyledProperty<double> CounterThresholdProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(CounterThreshold), 0.8);

    /// <summary>From this share of <see cref="MaxLength"/> the counter turns to the warning color.</summary>
    public static readonly StyledProperty<double> WarningThresholdProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(WarningThreshold), 0.95);

    /// <summary>Whether the plus menu, pasting files and dropping files are on.</summary>
    public static readonly StyledProperty<bool> IsAttachmentEnabledProperty =
        AvaloniaProperty.Register<AiComposer, bool>(nameof(IsAttachmentEnabled), true);

    /// <summary>The file types that may be attached, in the syntax of <c>FileDropzone.Accept</c> (".pdf,image/*"); null accepts any.</summary>
    public static readonly StyledProperty<string?> AcceptedFileTypesProperty =
        AvaloniaProperty.Register<AiComposer, string?>(nameof(AcceptedFileTypes));

    /// <summary>The largest file in bytes (30 MB by default); a larger one is attached in the error state with its reason. Null means no limit.</summary>
    public static readonly StyledProperty<long?> MaxFileSizeProperty =
        AvaloniaProperty.Register<AiComposer, long?>(nameof(MaxFileSize), 30L * 1024 * 1024);

    /// <summary>A pasted text of at least this many characters becomes an attachment instead of text; 0 turns the rule off.</summary>
    public static readonly StyledProperty<int> LongPasteCharactersProperty =
        AvaloniaProperty.Register<AiComposer, int>(nameof(LongPasteCharacters), 1200);

    /// <summary>A pasted text of at least this many lines becomes an attachment instead of text; 0 turns the rule off.</summary>
    public static readonly StyledProperty<int> LongPasteLinesProperty =
        AvaloniaProperty.Register<AiComposer, int>(nameof(LongPasteLines), 12);

    /// <summary>The selected model (two-way). Null at first selects the first model once and writes it back.</summary>
    public static readonly StyledProperty<AiModel?> SelectedModelProperty =
        AvaloniaProperty.Register<AiComposer, AiModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected effort (two-way); null follows the model's default.</summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<AiComposer, ModelEffort?>(nameof(Effort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether a reply is being generated (two-way): the send button shows a stop square and a progress ring.</summary>
    public static readonly StyledProperty<bool> IsGeneratingProperty =
        AvaloniaProperty.Register<AiComposer, bool>(nameof(IsGenerating), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How far the reply is, from 0 to 1: the target of the ring around the stop button. The ring never goes backwards while generating.</summary>
    public static readonly StyledProperty<double> GenerationProgressProperty =
        AvaloniaProperty.Register<AiComposer, double>(nameof(GenerationProgress));

    /// <summary>Whether the prompt can be sent, is blocked, or a reply is being generated (read-only).</summary>
    public static readonly DirectProperty<AiComposer, AiSendState> SendStateProperty =
        AvaloniaProperty.RegisterDirect<AiComposer, AiSendState>(nameof(SendState), o => o.SendState);

    /// <summary>Run when the person sends the prompt, with the <see cref="AiComposerMessage"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> SendCommandProperty =
        AvaloniaProperty.Register<AiComposer, ICommand?>(nameof(SendCommand));

    /// <summary>Run when the person stops a reply being generated.</summary>
    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<AiComposer, ICommand?>(nameof(StopCommand));

    /// <summary>
    /// The full composer with its toolbar (default), or the AI chat's single row: paperclip, text and send, with compact
    /// chips above (no toolbar, tools, model pill, counter, "/" or "@" lists; the progress is not shown). Events, the sent
    /// message and the labels are the same in both. It can change at any time (the template is swapped; the text and the
    /// attachments stay).
    /// </summary>
    public static readonly StyledProperty<AiComposerLayout> LayoutProperty =
        AvaloniaProperty.Register<AiComposer, AiComposerLayout>(nameof(Layout));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<AiComposerLabels> LabelsProperty =
        AvaloniaProperty.Register<AiComposer, AiComposerLabels>(nameof(Labels), new AiComposerLabels());

    #endregion

    #region Events

    /// <summary>Raised when the person sends the prompt.</summary>
    public static readonly RoutedEvent<AiComposerSentEventArgs> SentEvent =
        RoutedEvent.Register<AiComposer, AiComposerSentEventArgs>(nameof(Sent), RoutingStrategies.Bubble);

    /// <summary>Raised when the person stops a reply being generated.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<AiComposer, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when an attachment is added (while it may still be uploading).</summary>
    public static readonly RoutedEvent<AiAttachmentEventArgs> AttachmentAddedEvent =
        RoutedEvent.Register<AiComposer, AiAttachmentEventArgs>(nameof(AttachmentAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when an attachment is removed or its upload is cancelled.</summary>
    public static readonly RoutedEvent<AiAttachmentEventArgs> AttachmentRemovedEvent =
        RoutedEvent.Register<AiComposer, AiAttachmentEventArgs>(nameof(AttachmentRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when the person turns a tool on or off.</summary>
    public static readonly RoutedEvent<AiComposerToolToggledEventArgs> ToolToggledEvent =
        RoutedEvent.Register<AiComposer, AiComposerToolToggledEventArgs>(nameof(ToolToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when the person picks another model.</summary>
    public static readonly RoutedEvent<ModelPickerModelChangedEventArgs> ModelChangedEvent =
        RoutedEvent.Register<AiComposer, ModelPickerModelChangedEventArgs>(nameof(ModelChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the effort changes.</summary>
    public static readonly RoutedEvent<ModelPickerEffortChangedEventArgs> EffortChangedEvent =
        RoutedEvent.Register<AiComposer, ModelPickerEffortChangedEventArgs>(nameof(EffortChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person sends the prompt.</summary>
    public event EventHandler<AiComposerSentEventArgs>? Sent { add => AddHandler(SentEvent, value); remove => RemoveHandler(SentEvent, value); }

    /// <summary>Raised when the person stops a reply being generated.</summary>
    public event EventHandler<RoutedEventArgs>? StopRequested { add => AddHandler(StopRequestedEvent, value); remove => RemoveHandler(StopRequestedEvent, value); }

    /// <summary>Raised when an attachment is added.</summary>
    public event EventHandler<AiAttachmentEventArgs>? AttachmentAdded { add => AddHandler(AttachmentAddedEvent, value); remove => RemoveHandler(AttachmentAddedEvent, value); }

    /// <summary>Raised when an attachment is removed or its upload is cancelled.</summary>
    public event EventHandler<AiAttachmentEventArgs>? AttachmentRemoved { add => AddHandler(AttachmentRemovedEvent, value); remove => RemoveHandler(AttachmentRemovedEvent, value); }

    /// <summary>Raised when the person turns a tool on or off.</summary>
    public event EventHandler<AiComposerToolToggledEventArgs>? ToolToggled { add => AddHandler(ToolToggledEvent, value); remove => RemoveHandler(ToolToggledEvent, value); }

    /// <summary>Raised when the person picks another model.</summary>
    public event EventHandler<ModelPickerModelChangedEventArgs>? ModelChanged { add => AddHandler(ModelChangedEvent, value); remove => RemoveHandler(ModelChangedEvent, value); }

    /// <summary>Raised when the effort changes.</summary>
    public event EventHandler<ModelPickerEffortChangedEventArgs>? EffortChanged { add => AddHandler(EffortChangedEvent, value); remove => RemoveHandler(EffortChangedEvent, value); }

    #endregion

    private AiSendState _sendState;

    /// <summary>The commands the person can start a line with ("/"). None or empty means "/" is plain text.</summary>
    public AvaloniaList<AiCommand> Commands { get; } = new();

    /// <summary>The items the person can mention with "@". None or empty means "@" is plain text.</summary>
    public AvaloniaList<AiContextItem> ContextItems { get; } = new();

    /// <summary>The headings of the mention list in the order they are shown; the default is "Files" then "Docs".</summary>
    public AvaloniaList<string> MentionGroups { get; } = new();

    /// <summary>The attachments of the prompt. Add through <c>AddFilesAsync</c> or the plus menu; remove through the chips or <c>Clear</c>.</summary>
    public AvaloniaList<AiAttachment> Attachments { get; } = new();

    /// <summary>Entries of the plus menu after the built-in "Upload file" (such as "Add from cloud drive"); the person sees them above or below it as their separators say.</summary>
    public AvaloniaList<AiComposerAction> AttachActions { get; } = new();

    /// <summary>The toggles of the toolbar, such as "Web search".</summary>
    public AvaloniaList<AiComposerTool> Tools { get; } = new();

    /// <summary>The models the person can pick. Empty hides the model pill.</summary>
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>The providers of the models, for the marks of models that have none of their own.</summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>
    /// Uploads an attachment: report progress from 0 to 1, honor the token (the person cancelled) and throw to refuse it
    /// (the message shows on the chip). Null makes an attachment ready at once without an upload step.
    /// </summary>
    public Func<AiAttachment, IProgress<double>, CancellationToken, Task>? UploadHandler { get; set; }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public AiComposerValue Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="MinLinesProperty"/>
    public int MinLines { get => GetValue(MinLinesProperty); set => SetValue(MinLinesProperty, value); }

    /// <inheritdoc cref="MaxLinesProperty"/>
    public int MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <inheritdoc cref="MaxLengthProperty"/>
    public int? MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    /// <inheritdoc cref="CounterThresholdProperty"/>
    public double CounterThreshold { get => GetValue(CounterThresholdProperty); set => SetValue(CounterThresholdProperty, value); }

    /// <inheritdoc cref="WarningThresholdProperty"/>
    public double WarningThreshold { get => GetValue(WarningThresholdProperty); set => SetValue(WarningThresholdProperty, value); }

    /// <inheritdoc cref="IsAttachmentEnabledProperty"/>
    public bool IsAttachmentEnabled { get => GetValue(IsAttachmentEnabledProperty); set => SetValue(IsAttachmentEnabledProperty, value); }

    /// <inheritdoc cref="AcceptedFileTypesProperty"/>
    public string? AcceptedFileTypes { get => GetValue(AcceptedFileTypesProperty); set => SetValue(AcceptedFileTypesProperty, value); }

    /// <inheritdoc cref="MaxFileSizeProperty"/>
    public long? MaxFileSize { get => GetValue(MaxFileSizeProperty); set => SetValue(MaxFileSizeProperty, value); }

    /// <inheritdoc cref="LongPasteCharactersProperty"/>
    public int LongPasteCharacters { get => GetValue(LongPasteCharactersProperty); set => SetValue(LongPasteCharactersProperty, value); }

    /// <inheritdoc cref="LongPasteLinesProperty"/>
    public int LongPasteLines { get => GetValue(LongPasteLinesProperty); set => SetValue(LongPasteLinesProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public AiModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="IsGeneratingProperty"/>
    public bool IsGenerating { get => GetValue(IsGeneratingProperty); set => SetValue(IsGeneratingProperty, value); }

    /// <inheritdoc cref="GenerationProgressProperty"/>
    public double GenerationProgress { get => GetValue(GenerationProgressProperty); set => SetValue(GenerationProgressProperty, value); }

    /// <inheritdoc cref="SendStateProperty"/>
    public AiSendState SendState
    {
        get => _sendState;
        private set => SetAndRaise(SendStateProperty, ref _sendState, value);
    }

    /// <inheritdoc cref="SendCommandProperty"/>
    public ICommand? SendCommand { get => GetValue(SendCommandProperty); set => SetValue(SendCommandProperty, value); }

    /// <inheritdoc cref="StopCommandProperty"/>
    public ICommand? StopCommand { get => GetValue(StopCommandProperty); set => SetValue(StopCommandProperty, value); }

    /// <inheritdoc cref="LayoutProperty"/>
    public AiComposerLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiComposerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
}
