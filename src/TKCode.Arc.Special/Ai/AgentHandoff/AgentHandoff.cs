using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A support conversation where an AI assistant hands off to a person (Arc Agent handoff, design reference). The assistant
/// answers first; when it transfers, a card in the thread shows the summary it passes to the team and the typical wait.
/// The person then joins and takes over, and a thin timeline on the left marks each step: the assistant, the handoff
/// (with the help desk's mark), the person joining and the resolution.
/// <para>
/// Drive it from your support backend: set <see cref="Stage"/>, keep the assistant's messages in <see cref="Messages"/>
/// and everything after the handoff in <see cref="AgentMessages"/>, set <see cref="Typing"/>, <see cref="Summary"/> and
/// <see cref="Agent"/>, and handle the events. Stream assistant text by updating a message's
/// <see cref="AgentHandoffMessage.Text"/> with <see cref="AgentHandoffMessage.IsStreaming"/> set; each appended chunk
/// fades in. Leave <see cref="Stage"/> unset for an empty chat, or to play a <see cref="Simulation"/> you pass in
/// (<see cref="AgentHandoffSimulation"/>).
/// </para>
/// <para>
/// The card fills its container up to 560px and has a fixed height (600 by default, set <c>Height</c>); the thread
/// scrolls inside and stays pinned to the newest message, so the page never shifts and the width never follows the text.
/// </para>
/// </summary>
[TemplatePart(HeaderMarkPart, typeof(ArcContentSwap))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(SpringPart, typeof(AiHeightBox))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(ComposerPart, typeof(Control))]
[TemplatePart(InputPart, typeof(TextBox))]
[TemplatePart(SendPart, typeof(AvaloniaButton))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(CompactPseudoClass, ThreadFocusPseudoClass, AssistantPseudoClass, TransferringPseudoClass, ConnectedPseudoClass,
    ResolvedPseudoClass, UnavailablePseudoClass)]
public partial class AgentHandoff : TemplatedControl
{
    internal const string HeaderMarkPart = "PART_HeaderMark";
    internal const string TitlePart = "PART_Title";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string StatusPart = "PART_Status";
    internal const string ScrollerPart = "PART_Scroller";
    internal const string SpringPart = "PART_Spring";
    internal const string RowsPart = "PART_Rows";
    internal const string ComposerPart = "PART_Composer";
    internal const string InputPart = "PART_Input";
    internal const string SendPart = "PART_Send";
    internal const string LivePart = "PART_Live";

    internal const string CompactPseudoClass = ":compact";
    internal const string ThreadFocusPseudoClass = ":thread-focus";
    internal const string AssistantPseudoClass = ":assistant";
    internal const string TransferringPseudoClass = ":transferring";
    internal const string ConnectedPseudoClass = ":connected";
    internal const string ResolvedPseudoClass = ":resolved";
    internal const string UnavailablePseudoClass = ":unavailable";

    /// <summary>Where the conversation is. Leave it unset (null) to play the <see cref="Simulation"/>, if any.</summary>
    public static readonly StyledProperty<AgentHandoffStage?> StageProperty =
        AvaloniaProperty.Register<AgentHandoff, AgentHandoffStage?>(nameof(Stage));

    /// <summary>Who is typing: the assistant shows a short shimmering Thinking line, a person shows typing dots.</summary>
    public static readonly StyledProperty<AgentHandoffTyping> TypingProperty =
        AvaloniaProperty.Register<AgentHandoff, AgentHandoffTyping>(nameof(Typing));

    /// <summary>What the assistant passes to the person; shown in the handoff card from transferring on, collapsible.</summary>
    public static readonly StyledProperty<AgentHandoffSummary?> SummaryProperty =
        AvaloniaProperty.Register<AgentHandoff, AgentHandoffSummary?>(nameof(Summary));

    /// <summary>Whether the summary in the handoff card is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsSummaryExpandedProperty =
        AvaloniaProperty.Register<AgentHandoff, bool>(nameof(IsSummaryExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The assistant's name on the first step.</summary>
    public static readonly StyledProperty<string> AssistantNameProperty =
        AvaloniaProperty.Register<AgentHandoff, string>(nameof(AssistantName), "Assistant");

    /// <summary>The model behind the assistant, shown muted after its name; null (the default) hides it.</summary>
    public static readonly StyledProperty<string?> AssistantModelProperty =
        AvaloniaProperty.Register<AgentHandoff, string?>(nameof(AssistantModel));

    /// <summary>The assistant's mark: a 24 × 24 stroked glyph drawn at 14px (the sparkle by default). Pass your model's own mark.</summary>
    public static readonly StyledProperty<Geometry?> AssistantMarkProperty =
        AvaloniaProperty.Register<AgentHandoff, Geometry?>(nameof(AssistantMark), ArcIcons.Sparkles);

    /// <summary>The assistant mark's color; the accent by default.</summary>
    public static readonly StyledProperty<IBrush?> AssistantMarkBrushProperty =
        AvaloniaProperty.Register<AgentHandoff, IBrush?>(nameof(AssistantMarkBrush));

    /// <summary>The person who takes over: shown on the join step, in the card and in the header from connected on.</summary>
    public static readonly StyledProperty<AgentHandoffAgent?> AgentProperty =
        AvaloniaProperty.Register<AgentHandoff, AgentHandoffAgent?>(nameof(Agent));

    /// <summary>The name of the help desk the conversation moves to.</summary>
    public static readonly StyledProperty<string> ProviderNameProperty =
        AvaloniaProperty.Register<AgentHandoff, string>(nameof(ProviderName), "Help desk");

    /// <summary>The help desk's mark on the timeline next to the handoff card: a 24 × 24 stroked glyph (an inbox by default).</summary>
    public static readonly StyledProperty<Geometry?> ProviderMarkProperty =
        AvaloniaProperty.Register<AgentHandoff, Geometry?>(nameof(ProviderMark), ArcIcons.Inbox);

    /// <summary>The provider mark's color; the secondary text color by default.</summary>
    public static readonly StyledProperty<IBrush?> ProviderMarkBrushProperty =
        AvaloniaProperty.Register<AgentHandoff, IBrush?>(nameof(ProviderMarkBrush));

    /// <summary>The typical wait shown while transferring, such as "2 min"; null (the default) shows none.</summary>
    public static readonly StyledProperty<string?> ReplyEstimateProperty =
        AvaloniaProperty.Register<AgentHandoff, string?>(nameof(ReplyEstimate));

    /// <summary>The header title, such as the order or ticket number; null shows "Support chat".</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentHandoff, string?>(nameof(Title));

    /// <summary>A muted line under the title.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<AgentHandoff, string?>(nameof(Subtitle));

    /// <summary>Shows the composer; sending raises <see cref="SendRequested"/> with the trimmed text.</summary>
    public static readonly StyledProperty<bool> ShowsComposerProperty =
        AvaloniaProperty.Register<AgentHandoff, bool>(nameof(ShowsComposer));

    /// <summary>Shows a Talk to a person reply under the assistant's last answer while the stage is Assistant; it raises <see cref="HumanRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsRequestHumanProperty =
        AvaloniaProperty.Register<AgentHandoff, bool>(nameof(AllowsRequestHuman));

    /// <summary>Shows Cancel on the handoff card while transferring; it raises <see cref="CancelTransferRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsCancelTransferProperty =
        AvaloniaProperty.Register<AgentHandoff, bool>(nameof(AllowsCancelTransfer));

    /// <summary>Shows Try again on the handoff card when the stage is Unavailable; it raises <see cref="RetryRequested"/>.</summary>
    public static readonly StyledProperty<bool> AllowsRetryProperty =
        AvaloniaProperty.Register<AgentHandoff, bool>(nameof(AllowsRetry));

    /// <summary>The composer placeholder; null follows the stage ("Ask a question", "Message Jordan").</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<AgentHandoff, string?>(nameof(Placeholder));

    /// <summary>A one-off message for screen readers, such as "Refund issued"; announced once each time it changes.</summary>
    public static readonly StyledProperty<string?> NoticeProperty =
        AvaloniaProperty.Register<AgentHandoff, string?>(nameof(Notice));

    /// <summary>
    /// A simulated chat to play while <see cref="Stage"/> is unset (demos and prototypes). The card plays it from the top
    /// each time it is shown, with the composer and the Talk to a person, Cancel and Try again actions; null shows the
    /// card's own state.
    /// </summary>
    public static readonly StyledProperty<AgentHandoffSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AgentHandoff, AgentHandoffSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<AgentHandoffLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentHandoff, AgentHandoffLabels>(nameof(Labels), AgentHandoffLabels.Default);

    /// <summary>How much of a row a bubble may take (set by the theme; wider when compact).</summary>
    internal static readonly StyledProperty<double> BubbleFractionProperty =
        AvaloniaProperty.Register<AgentHandoff, double>(nameof(BubbleFraction), 0.8);

    /// <summary>Below this width the card is compact (set by the theme).</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentHandoff, double>(nameof(CompactWidth), 400);

    /// <summary>Raised when the customer sends a message, with the trimmed text. Bubbles. Append it to <see cref="Messages"/> or <see cref="AgentMessages"/> yourself.</summary>
    public static readonly RoutedEvent<AgentHandoffSendEventArgs> SendRequestedEvent =
        RoutedEvent.Register<AgentHandoff, AgentHandoffSendEventArgs>(nameof(SendRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the customer presses Talk to a person. Bubbles. Escalate, then set <see cref="Stage"/> to Transferring.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> HumanRequestedEvent =
        RoutedEvent.Register<AgentHandoff, RoutedEventArgs>(nameof(HumanRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the customer presses Cancel on the handoff card. Bubbles. Cancel the escalation, then set <see cref="Stage"/> back to Assistant.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CancelTransferRequestedEvent =
        RoutedEvent.Register<AgentHandoff, RoutedEventArgs>(nameof(CancelTransferRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the customer presses Try again on the handoff card. Bubbles. Escalate again, then set <see cref="Stage"/> to Transferring.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<AgentHandoff, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    private readonly AvaloniaList<AgentHandoffMessage> _messages = [];
    private readonly AvaloniaList<AgentHandoffMessage> _agentMessages = [];
    private readonly AgentHandoffMessageWatch _messagesWatch;
    private readonly AgentHandoffMessageWatch _agentMessagesWatch;
    private AgentHandoffSimulation? _simulation;
    private ArcContentSwap? _headerMark;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private ArcContentSwap? _status;
    private ScrollViewer? _scroller;
    private AiHeightBox? _spring;
    private StackPanel? _rows;
    private Control? _composer;
    private TextBox? _input;
    private AvaloniaButton? _send;
    private ExplainerAnnouncer? _live;
    private string? _headerMarkKey;
    private (AgentHandoffStatusKind Kind, string Text)? _shownStatus;
    private AgentHandoffStage? _announcedStage;
    private string? _announcedNotice;
    private string? _pendingAnnouncement;
    private bool _pinned = true;
    private double _ownOffset = double.NaN;

    static AgentHandoff()
    {
        FocusableProperty.OverrideDefaultValue<AgentHandoff>(false);
    }

    /// <summary>Creates the card.</summary>
    public AgentHandoff()
    {
        _messagesWatch = new AgentHandoffMessageWatch(OnMessageChanged);
        _agentMessagesWatch = new AgentHandoffMessageWatch(OnMessageChanged);
        _messagesWatch.Watch(_messages);
        _agentMessagesWatch.Watch(_agentMessages);
    }

    /// <inheritdoc cref="StageProperty"/>
    public AgentHandoffStage? Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }

    /// <inheritdoc cref="TypingProperty"/>
    public AgentHandoffTyping Typing { get => GetValue(TypingProperty); set => SetValue(TypingProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public AgentHandoffSummary? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <inheritdoc cref="IsSummaryExpandedProperty"/>
    public bool IsSummaryExpanded { get => GetValue(IsSummaryExpandedProperty); set => SetValue(IsSummaryExpandedProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="AssistantModelProperty"/>
    public string? AssistantModel { get => GetValue(AssistantModelProperty); set => SetValue(AssistantModelProperty, value); }

    /// <inheritdoc cref="AssistantMarkProperty"/>
    public Geometry? AssistantMark { get => GetValue(AssistantMarkProperty); set => SetValue(AssistantMarkProperty, value); }

    /// <inheritdoc cref="AssistantMarkBrushProperty"/>
    public IBrush? AssistantMarkBrush { get => GetValue(AssistantMarkBrushProperty); set => SetValue(AssistantMarkBrushProperty, value); }

    /// <inheritdoc cref="AgentProperty"/>
    public AgentHandoffAgent? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    /// <inheritdoc cref="ProviderNameProperty"/>
    public string ProviderName { get => GetValue(ProviderNameProperty); set => SetValue(ProviderNameProperty, value); }

    /// <inheritdoc cref="ProviderMarkProperty"/>
    public Geometry? ProviderMark { get => GetValue(ProviderMarkProperty); set => SetValue(ProviderMarkProperty, value); }

    /// <inheritdoc cref="ProviderMarkBrushProperty"/>
    public IBrush? ProviderMarkBrush { get => GetValue(ProviderMarkBrushProperty); set => SetValue(ProviderMarkBrushProperty, value); }

    /// <inheritdoc cref="ReplyEstimateProperty"/>
    public string? ReplyEstimate { get => GetValue(ReplyEstimateProperty); set => SetValue(ReplyEstimateProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="ShowsComposerProperty"/>
    public bool ShowsComposer { get => GetValue(ShowsComposerProperty); set => SetValue(ShowsComposerProperty, value); }

    /// <inheritdoc cref="AllowsRequestHumanProperty"/>
    public bool AllowsRequestHuman { get => GetValue(AllowsRequestHumanProperty); set => SetValue(AllowsRequestHumanProperty, value); }

    /// <inheritdoc cref="AllowsCancelTransferProperty"/>
    public bool AllowsCancelTransfer { get => GetValue(AllowsCancelTransferProperty); set => SetValue(AllowsCancelTransferProperty, value); }

    /// <inheritdoc cref="AllowsRetryProperty"/>
    public bool AllowsRetry { get => GetValue(AllowsRetryProperty); set => SetValue(AllowsRetryProperty, value); }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice { get => GetValue(NoticeProperty); set => SetValue(NoticeProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AgentHandoffSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentHandoffLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double BubbleFraction { get => GetValue(BubbleFractionProperty); set => SetValue(BubbleFractionProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <summary>The conversation with the assistant, before the handoff. Change a message's text in place to stream it.</summary>
    public AvaloniaList<AgentHandoffMessage> Messages => _messages;

    /// <summary>
    /// Everything after the handoff card: notes the customer adds while waiting and the conversation with the person. The
    /// join step sits before the person's first message.
    /// </summary>
    public AvaloniaList<AgentHandoffMessage> AgentMessages => _agentMessages;

    /// <inheritdoc cref="SendRequestedEvent"/>
    public event EventHandler<AgentHandoffSendEventArgs>? SendRequested
    {
        add => AddHandler(SendRequestedEvent, value);
        remove => RemoveHandler(SendRequestedEvent, value);
    }

    /// <inheritdoc cref="HumanRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? HumanRequested
    {
        add => AddHandler(HumanRequestedEvent, value);
        remove => RemoveHandler(HumanRequestedEvent, value);
    }

    /// <inheritdoc cref="CancelTransferRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CancelTransferRequested
    {
        add => AddHandler(CancelTransferRequestedEvent, value);
        remove => RemoveHandler(CancelTransferRequestedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <summary>The simulation playing while <see cref="Stage"/> is unset and the card is on screen.</summary>
    internal AgentHandoffSimulation? PlayingSimulation => _simulation;

    /// <summary>Whether the <see cref="Simulation"/> drives the card: one is set and <see cref="Stage"/> is not.</summary>
    internal bool IsSimulated => Stage is null && Simulation is not null;

    /// <summary>The stage on screen.</summary>
    internal AgentHandoffStage DisplayedStage => Stage ?? (IsSimulated ? _simulation?.Stage : null) ?? AgentHandoffStage.Assistant;

    internal AgentHandoffTyping DisplayedTyping => IsSimulated ? _simulation?.Typing ?? AgentHandoffTyping.None : Typing;

    internal AgentHandoffSummary? DisplayedSummary => IsSimulated ? _simulation?.Summary : Summary;

    internal AgentHandoffAgent? DisplayedAgent => IsSimulated ? _simulation?.Agent : Agent;

    internal IReadOnlyList<AgentHandoffMessage> DisplayedMessages =>
        IsSimulated ? _simulation?.Messages ?? (IReadOnlyList<AgentHandoffMessage>)[] : _messages;

    internal IReadOnlyList<AgentHandoffMessage> DisplayedAgentMessages =>
        IsSimulated ? _simulation?.AgentMessages ?? (IReadOnlyList<AgentHandoffMessage>)[] : _agentMessages;

    internal bool DisplayedComposer => IsSimulated || ShowsComposer;

    internal bool DisplayedRequestHuman => IsSimulated || AllowsRequestHuman;

    internal bool DisplayedCancel => IsSimulated || AllowsCancelTransfer;

    internal bool DisplayedRetry => IsSimulated || AllowsRetry;

    /// <summary>The status in the header: its kind and text ("Transferring", "With Jordan").</summary>
    internal (AgentHandoffStatusKind Kind, string Text) DisplayedStatus => DisplayedStage switch
    {
        AgentHandoffStage.Transferring => (AgentHandoffStatusKind.Transferring, Labels.Transferring),
        AgentHandoffStage.Connected => (AgentHandoffStatusKind.Connected, Labels.With(AgentFirstName)),
        AgentHandoffStage.Resolved => (AgentHandoffStatusKind.Resolved, Labels.Resolved),
        AgentHandoffStage.Unavailable => (AgentHandoffStatusKind.Unavailable, Labels.Unavailable),
        _ => (AgentHandoffStatusKind.Assistant, Labels.AssistantStatus),
    };

    /// <summary>The composer placeholder on screen.</summary>
    internal string DisplayedPlaceholder => Placeholder ?? DisplayedStage switch
    {
        AgentHandoffStage.Transferring => Labels.PlaceholderTransferring,
        AgentHandoffStage.Connected or AgentHandoffStage.Resolved => Labels.PlaceholderAgent(AgentFirstName),
        AgentHandoffStage.Unavailable => Labels.PlaceholderUnavailable,
        _ => Labels.PlaceholderAssistant,
    };

    /// <summary>Whether the card is narrower than <see cref="CompactWidth"/> (tighter padding, wider bubbles).</summary>
    internal bool IsCompact => PseudoClasses.Contains(CompactPseudoClass);

    private string AgentName => DisplayedAgent is { Name.Length: > 0 } agent ? agent.Name : Labels.AgentFallback;

    private string AgentFirstName => DisplayedAgent is { Name.Length: > 0 } agent ? agent.FirstName : Labels.AgentFallback;

    internal ScrollViewer? Scroller => _scroller;

    internal AiHeightBox? ThreadSpring => _spring;

    internal ExplainerAnnouncer? LiveRegion => _live;

    internal bool IsPinned => _pinned;

    /// <summary>Sends the composer's text (trimmed; empty text does nothing).</summary>
    public void SendComposer()
    {
        var text = _input?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        if (_input is not null)
        {
            _input.Text = string.Empty;
        }
        _pinned = true;
        RaiseEvent(new AgentHandoffSendEventArgs(SendRequestedEvent, text));
        if (IsSimulated)
        {
            _simulation?.Send(text);
        }
    }

    /// <summary>Scrolls the thread to the newest message and keeps it pinned there.</summary>
    public void ScrollToNewest()
    {
        _pinned = true;
        PinToEnd();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _headerMark = e.NameScope.Find<ArcContentSwap>(HeaderMarkPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _spring = e.NameScope.Find<AiHeightBox>(SpringPart);
        if (_spring is not null)
        {
            _spring.HeightSpring = AgentHandoffMotion.ThreadSpring;
        }
        _rows = e.NameScope.Find<StackPanel>(RowsPart);
        _composer = e.NameScope.Find<Control>(ComposerPart);
        _input = e.NameScope.Find<TextBox>(InputPart);
        _send = e.NameScope.Find<AvaloniaButton>(SendPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
            _scroller.AddHandler(KeyDownEvent, OnThreadKeyDown);
            _scroller.GotFocus += OnThreadGotFocus;
            _scroller.LostFocus += OnThreadLostFocus;
        }
        if (_input is not null)
        {
            _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        }
        if (_send is not null)
        {
            _send.Click += OnSend;
        }
        _headerMarkKey = null;
        _shownStatus = null;
        ResetRows();
        UpdateStagePseudoClasses();
        Refresh();
    }

    private void Unhook()
    {
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
            _scroller.RemoveHandler(KeyDownEvent, OnThreadKeyDown);
            _scroller.GotFocus -= OnThreadGotFocus;
            _scroller.LostFocus -= OnThreadLostFocus;
        }
        if (_input is not null)
        {
            _input.RemoveHandler(KeyDownEvent, OnInputKeyDown);
        }
        if (_send is not null)
        {
            _send.Click -= OnSend;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StageProperty)
        {
            UpdateSimulation();
            UpdateStagePseudoClasses();
            Refresh();
        }
        else if (change.Property == SimulationProperty)
        {
            UpdateSimulation();
            UpdateStagePseudoClasses();
            Refresh();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateCompact();
        }
        else if (change.Property == BubbleFractionProperty)
        {
            Refresh();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact();
        }
        else if (change.Property == TypingProperty || change.Property == SummaryProperty || change.Property == IsSummaryExpandedProperty
                 || change.Property == AssistantNameProperty || change.Property == AssistantModelProperty
                 || change.Property == AssistantMarkProperty || change.Property == AssistantMarkBrushProperty
                 || change.Property == AgentProperty || change.Property == ProviderNameProperty
                 || change.Property == ProviderMarkProperty || change.Property == ProviderMarkBrushProperty
                 || change.Property == ReplyEstimateProperty || change.Property == TitleProperty
                 || change.Property == SubtitleProperty || change.Property == ShowsComposerProperty
                 || change.Property == AllowsRequestHumanProperty || change.Property == AllowsCancelTransferProperty
                 || change.Property == AllowsRetryProperty || change.Property == PlaceholderProperty
                 || change.Property == NoticeProperty || change.Property == LabelsProperty)
        {
            if (change.Property == AgentProperty || change.Property == AssistantMarkProperty
                || change.Property == AssistantMarkBrushProperty || change.Property == ProviderMarkProperty
                || change.Property == ProviderMarkBrushProperty || change.Property == LabelsProperty)
            {
                // Markers are built from these values: rebuild the rows that carry them.
                _headerMarkKey = null;
                ResetMarkers();
            }
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSimulation();
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSimulation();
    }

    /// <summary>
    /// Takes the <see cref="Simulation"/> up while the card is on screen with no <see cref="Stage"/> (from the top, with a
    /// clock gated by this card), and lets it go when the stage is set, the simulation is replaced or the card leaves.
    /// </summary>
    private void UpdateSimulation()
    {
        if (_simulation is not null && !ReferenceEquals(_simulation, Simulation))
        {
            StopSimulation();
            ResetRows();
        }
        if (Stage is null && VisualRoot is not null && Simulation is { } simulation)
        {
            if (_simulation is null)
            {
                _simulation = simulation;
                _simulation.IsReduced = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
                _simulation.Attach(this);
                _simulation.Changed += OnSimulationChanged;
                _messagesWatch.Watch(_simulation.Messages);
                _agentMessagesWatch.Watch(_simulation.AgentMessages);
                ResetRows();
                _announcedStage = null;
                _simulation.Start();
                // Said once, as soon as the status region exists (the template may apply after this).
                _pendingAnnouncement = Labels.SimulatedNotice;
                _announcedStage = AgentHandoffStage.Assistant;
            }
        }
        else if (Stage is not null && _simulation is not null)
        {
            StopSimulation();
            ResetRows();
        }
    }

    private void StopSimulation()
    {
        if (_simulation is null)
        {
            return;
        }
        _simulation.Changed -= OnSimulationChanged;
        // The app owns the simulation: it is stopped and loses this card's clock, not disposed.
        _simulation.Stop();
        _simulation.Detach();
        _simulation = null;
        _messagesWatch.Watch(_messages);
        _agentMessagesWatch.Watch(_agentMessages);
    }

    private void OnSimulationChanged(object? sender, EventArgs e)
    {
        UpdateStagePseudoClasses();
        Refresh();
    }

    private void OnMessageChanged(AgentHandoffMessage? message)
    {
        if (message is not null && _rowsByKey.TryGetValue(message, out var row) && row.Content is AgentHandoffBubble bubble)
        {
            // A streamed chunk only touches its own bubble; a finished message can change the rows after it (Talk to a person).
            if (bubble.Sender == message.From)
            {
                bubble.Update(fade: message.IsStreaming);
                if (message.IsStreaming)
                {
                    return;
                }
            }
            else
            {
                // Another sender: the bubble is rebuilt on its side.
                _rowsByKey.Remove(message);
            }
        }
        Refresh();
    }

    private void UpdateStagePseudoClasses()
    {
        var stage = DisplayedStage;
        PseudoClasses.Set(AssistantPseudoClass, stage == AgentHandoffStage.Assistant);
        PseudoClasses.Set(TransferringPseudoClass, stage == AgentHandoffStage.Transferring);
        PseudoClasses.Set(ConnectedPseudoClass, stage == AgentHandoffStage.Connected);
        PseudoClasses.Set(ResolvedPseudoClass, stage == AgentHandoffStage.Resolved);
        PseudoClasses.Set(UnavailablePseudoClass, stage == AgentHandoffStage.Unavailable);
    }

    private void UpdateCompact()
    {
        var width = Bounds.Width;
        var compact = width > 0 && width < CompactWidth;
        if (PseudoClasses.Contains(CompactPseudoClass) != compact)
        {
            PseudoClasses.Set(CompactPseudoClass, compact);
        }
    }

    /// <summary>Brings the header, the rows and the composer in line with the state.</summary>
    private void Refresh()
    {
        if (_rows is null)
        {
            return;
        }
        var stage = DisplayedStage;
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title ?? labels.Title;
        }
        if (_subtitle is not null)
        {
            _subtitle.Text = Subtitle;
            _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle);
        }
        RefreshHeaderMark(stage);
        var status = DisplayedStatus;
        if (_status is not null && _shownStatus != status)
        {
            _shownStatus = status;
            _status.Content = new AgentHandoffStatusLabel(status.Kind, status.Text);
        }
        SyncRows(stage);
        if (_composer is not null)
        {
            _composer.IsVisible = DisplayedComposer;
        }
        if (_input is not null)
        {
            if (_input is Input field)
            {
                field.Placeholder = DisplayedPlaceholder;
            }
            else
            {
                _input.PlaceholderText = DisplayedPlaceholder;
            }
            AutomationProperties.SetName(_input, labels.Composer);
        }
        if (_send is not null)
        {
            AutomationProperties.SetName(_send, labels.Send);
        }
        if (_scroller is not null)
        {
            AutomationProperties.SetName(_scroller, labels.Thread);
        }
        Announce(stage);
    }

    private void RefreshHeaderMark(AgentHandoffStage stage)
    {
        if (_headerMark is null)
        {
            return;
        }
        var person = stage is AgentHandoffStage.Connected or AgentHandoffStage.Resolved;
        var key = person ? "person:" + AgentName : "assistant";
        if (_headerMarkKey == key)
        {
            return;
        }
        _headerMarkKey = key;
        var marker = person
            ? AgentHandoffMarker.ForPerson(AgentName, DisplayedAgent?.Avatar)
            : AgentHandoffMarker.ForGlyph(AssistantMark ?? ArcIcons.Sparkles, AssistantMarkBrush, accent: true);
        marker.Classes.Add("header");
        _headerMark.Content = marker;
    }

    private void Announce(AgentHandoffStage stage)
    {
        if (_live is null)
        {
            return;
        }
        if (_pendingAnnouncement is { } pending)
        {
            _pendingAnnouncement = null;
            _live.Announce(pending);
        }
        if (_announcedStage is null)
        {
            // The stage the card opens with is not news.
            _announcedStage = stage;
        }
        else if (_announcedStage != stage)
        {
            _announcedStage = stage;
            _live.Announce(stage switch
            {
                AgentHandoffStage.Transferring => Labels.AnnounceTransferring,
                AgentHandoffStage.Connected => Labels.AnnounceConnected(AgentName),
                AgentHandoffStage.Resolved => Labels.AnnounceResolved,
                AgentHandoffStage.Unavailable => Labels.AnnounceUnavailable,
                _ => Labels.AnnounceAssistant,
            });
        }
        var notice = IsSimulated ? _simulation?.Notice : Notice;
        if (notice != _announcedNotice)
        {
            _announcedNotice = notice;
            _live.Announce(notice);
        }
    }

    private void OnSend(object? sender, RoutedEventArgs e) => SendComposer();

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            SendComposer();
        }
    }

    private void OnRequestHuman(object? sender, RoutedEventArgs e)
    {
        _pinned = true;
        RaiseEvent(new RoutedEventArgs(HumanRequestedEvent));
        if (IsSimulated)
        {
            _simulation?.RequestHuman(Labels.TalkToPerson);
        }
    }

    private void OnCardAction(object? sender, EventArgs e)
    {
        var stage = DisplayedStage;
        if (stage == AgentHandoffStage.Transferring)
        {
            RaiseEvent(new RoutedEventArgs(CancelTransferRequestedEvent));
            if (IsSimulated)
            {
                _simulation?.CancelTransfer();
            }
        }
        else if (stage == AgentHandoffStage.Unavailable)
        {
            RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));
            if (IsSimulated)
            {
                _simulation?.Retry();
            }
        }
    }

    private void OnSummaryToggle(object? sender, EventArgs e) => SetCurrentValue(IsSummaryExpandedProperty, !IsSummaryExpanded);

    private void OnThreadGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, _scroller))
        {
            PseudoClasses.Set(ThreadFocusPseudoClass, e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
        }
    }

    private void OnThreadLostFocus(object? sender, FocusChangedEventArgs e) => PseudoClasses.Set(ThreadFocusPseudoClass, false);

    /// <summary>Keyboard users scroll the focused thread: arrows by a line, Page Up / Page Down by a screen, Home and End.</summary>
    private void OnThreadKeyDown(object? sender, KeyEventArgs e)
    {
        if (_scroller is null || !ReferenceEquals(e.Source, _scroller))
        {
            return;
        }
        var offset = _scroller.Offset.Y;
        var max = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        var page = Math.Max(40, _scroller.Viewport.Height * 0.85);
        double? target = e.Key switch
        {
            Key.Up => offset - 40,
            Key.Down => offset + 40,
            Key.PageUp => offset - page,
            Key.PageDown => offset + page,
            Key.Home => 0,
            Key.End => max,
            _ => null,
        };
        if (target is not { } y)
        {
            return;
        }
        e.Handled = true;
        _scroller.Offset = new Vector(_scroller.Offset.X, Math.Clamp(y, 0, max));
    }

    /// <summary>
    /// Keeps the thread pinned to the newest message: a growing extent scrolls to the end while pinned; an offset the
    /// person set (wheel, scroll bar, keys) re-decides whether it is pinned from the distance to the bottom.
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null)
        {
            return;
        }
        var max = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        var offset = _scroller.Offset.Y;
        if (e.OffsetDelta.Y != 0 && !(Math.Abs(offset - _ownOffset) < 0.5))
        {
            _pinned = max - offset < 24;
        }
        if (_pinned && (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0) && Math.Abs(offset - max) > 0.5)
        {
            PinToEnd();
        }
    }

    private void PinToEnd()
    {
        if (_scroller is null)
        {
            return;
        }
        var max = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        _ownOffset = max;
        _scroller.Offset = new Vector(_scroller.Offset.X, max);
    }
}
