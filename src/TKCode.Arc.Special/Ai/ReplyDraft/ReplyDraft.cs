using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ReplyDraft"/> is.</summary>
public enum ReplyDraftStatus
{
    /// <summary>No draft yet: waiting to start, or showing Draft reply.</summary>
    Idle,

    /// <summary>A fresh draft is being written and streams into the draft box.</summary>
    Drafting,

    /// <summary>A tone or length rewrite runs; the old draft is dimmed until the new one crossfades in.</summary>
    Rewriting,

    /// <summary>The draft is finished and editable.</summary>
    Ready,

    /// <summary>The insert handler runs; Insert shows a spinner.</summary>
    Inserting,

    /// <summary>The draft was inserted (or, without a handler, the insert was simulated).</summary>
    Inserted,

    /// <summary>The generator failed; the draft box offers Try again.</summary>
    Failed
}

/// <summary>
/// An AI email reply drafter (Arc Reply draft, design reference): a received email with an AI reply streaming under it.
/// Quick reply chips steer a fresh draft, which streams in chunks of 3 to 8 words that each fade in over 140ms while the
/// draft box springs to its height. Tone (Friendly, Concise, Formal) and length (Short, Medium, Long) rewrite a finished
/// draft with one crossfade: the old text dims, fades out in 100ms and the new one fades in over 160ms rising 2px. The
/// finished draft is plain editable text; Regenerate writes a new one, and Insert (or Cmd/Ctrl+Enter anywhere in the
/// card) hands the text, with the reader's edits, to <see cref="InsertHandler"/> and <see cref="InsertRequested"/>.
/// <para>
/// Give it a <see cref="Message"/>, <see cref="Suggestions"/> and a <see cref="Generator"/> that streams your model's
/// reply. Without a message it plays a <see cref="Simulation"/> (an email and the replies you wrote for it, nothing is
/// sent) and, unless the reader touches it, tours tones, length, a second quick reply and Insert on a loop while it is on
/// screen. With neither it shows an empty email and a draft box that cannot draft.
/// </para>
/// </summary>
[TemplatePart(SubjectPart, typeof(TextBlock))]
[TemplatePart(TimePart, typeof(TextBlock))]
[TemplatePart(MailMarkPart, typeof(ArcGlyph))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(SenderPart, typeof(TextBlock))]
[TemplatePart(AddressPart, typeof(TextBlock))]
[TemplatePart(RecipientPart, typeof(TextBlock))]
[TemplatePart(BodyPart, typeof(StackPanel))]
[TemplatePart(ReplyPart, typeof(Control))]
[TemplatePart(ModelMarkPart, typeof(ArcGlyph))]
[TemplatePart(HeadingPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(SuggestionsPart, typeof(WrapPanel))]
[TemplatePart(DraftBoxPart, typeof(Border))]
[TemplatePart(HeightBoxPart, typeof(AiHeightBox))]
[TemplatePart(DraftPart, typeof(ArcContentSwap))]
[TemplatePart(ControlsPart, typeof(ReplyDraftControlsPanel))]
[TemplatePart(ToneLabelPart, typeof(TextBlock))]
[TemplatePart(TonePart, typeof(ReplyDraftOptionGroup))]
[TemplatePart(LengthLabelPart, typeof(TextBlock))]
[TemplatePart(LengthPart, typeof(ReplyDraftOptionGroup))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(RegeneratePart, typeof(Button))]
[TemplatePart(RegenerateTextPart, typeof(TextBlock))]
[TemplatePart(InsertPart, typeof(Button))]
[TemplatePart(InsertTextPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, BusyPseudoClass)]
public partial class ReplyDraft : TemplatedControl
{
    internal const string SubjectPart = "PART_Subject";
    internal const string TimePart = "PART_Time";
    internal const string MailMarkPart = "PART_MailMark";
    internal const string AvatarPart = "PART_Avatar";
    internal const string SenderPart = "PART_Sender";
    internal const string AddressPart = "PART_Address";
    internal const string RecipientPart = "PART_Recipient";
    internal const string BodyPart = "PART_Body";
    internal const string ReplyPart = "PART_Reply";
    internal const string ModelMarkPart = "PART_ModelMark";
    internal const string HeadingPart = "PART_Heading";
    internal const string StatusPart = "PART_Status";
    internal const string SuggestionsPart = "PART_Suggestions";
    internal const string DraftBoxPart = "PART_DraftBox";
    internal const string HeightBoxPart = "PART_HeightBox";
    internal const string DraftPart = "PART_Draft";
    internal const string ControlsPart = "PART_Controls";
    internal const string ToneLabelPart = "PART_ToneLabel";
    internal const string TonePart = "PART_Tone";
    internal const string LengthLabelPart = "PART_LengthLabel";
    internal const string LengthPart = "PART_Length";
    internal const string HintPart = "PART_Hint";
    internal const string RegeneratePart = "PART_Regenerate";
    internal const string RegenerateTextPart = "PART_RegenerateText";
    internal const string InsertPart = "PART_Insert";
    internal const string InsertTextPart = "PART_InsertText";
    internal const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set below <see cref="CompactWidth"/>: 16px padding and no avatar indent on the body.</summary>
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Set below <see cref="NarrowWidth"/>: the groups' hairline hides and Length moves to its own row.</summary>
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while a draft or rewrite runs (Arc's aria-busy).</summary>
    internal const string BusyPseudoClass = ":busy";

    internal const double CompactWidth = 480;

    internal const double NarrowWidth = 460;

    private static readonly ReplyDraftMessage s_noMessage = new();

    private static readonly AgentModel s_noModel = new(string.Empty);

    /// <summary>The received email; null plays <see cref="Simulation"/>, or shows an empty email without one.</summary>
    public static readonly StyledProperty<ReplyDraftMessage?> MessageProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftMessage?>(nameof(Message));

    /// <summary>
    /// The quick replies shown as chips above the draft; choosing one streams a fresh draft steered by it. Null shows the
    /// <see cref="Simulation"/>'s while one plays; null or an empty list hides the row.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<ReplyDraftSuggestion>?> SuggestionsProperty =
        AvaloniaProperty.Register<ReplyDraft, IReadOnlyList<ReplyDraftSuggestion>?>(nameof(Suggestions));

    /// <summary>
    /// The id of the quick reply steering the draft (two-way). Null, or an id that is not in the list, means the first
    /// suggestion. Setting it after the first draft writes a fresh one.
    /// </summary>
    public static readonly StyledProperty<string?> SelectedSuggestionIdProperty =
        AvaloniaProperty.Register<ReplyDraft, string?>(nameof(SelectedSuggestionId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The model credited on the reply, with its 16px mark; null credits the <see cref="Simulation"/>'s model while one plays, or none.</summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<ReplyDraft, AgentModel?>(nameof(Model));

    /// <summary>
    /// The tone (two-way). Changing it rewrites a finished draft with one crossfade, or restarts a draft that is still
    /// streaming.
    /// </summary>
    public static readonly StyledProperty<ReplyDraftTone> ToneProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftTone>(nameof(Tone), ReplyDraftTone.Friendly, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The length (two-way). Works like <see cref="Tone"/>.</summary>
    public static readonly StyledProperty<ReplyDraftLength> LengthProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftLength>(nameof(Length), ReplyDraftLength.Medium, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Writes the draft as a stream of text deltas. Every new action cancels the previous request's token. Without it a
    /// message of yours cannot be drafted; a playing <see cref="Simulation"/> does not use it.
    /// </summary>
    public static readonly StyledProperty<ReplyDraftGenerator?> GeneratorProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftGenerator?>(nameof(Generator));

    /// <summary>
    /// Puts the draft into your composer. While its task runs, Insert shows a spinner. With neither this nor a handler
    /// that marks <see cref="InsertRequested"/> handled, Insert is simulated and says that nothing was sent.
    /// </summary>
    public static readonly StyledProperty<ReplyDraftInsertHandler?> InsertHandlerProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftInsertHandler?>(nameof(InsertHandler));

    /// <summary>
    /// Starts the first draft when the card scrolls into view. When false the draft box shows an empty state with a Draft
    /// reply button.
    /// </summary>
    public static readonly StyledProperty<bool> AutoStartProperty =
        AvaloniaProperty.Register<ReplyDraft, bool>(nameof(AutoStart), true);

    /// <summary>
    /// Tours tones, length, a second quick reply and Insert on a loop. It stops for good on the first pointer or key press
    /// inside the card and pauses off screen. Null turns it on only while a <see cref="Simulation"/> plays; leave it off
    /// in products.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<ReplyDraft, bool?>(nameof(AutoPlay));

    /// <summary>
    /// An email to play while the card has no <see cref="Message"/>: the message, quick replies, model and the replies
    /// written for them, streamed as a simulation (nothing is sent). Your own <see cref="Message"/> wins over it; null stops
    /// it and shows the empty card.
    /// </summary>
    public static readonly StyledProperty<ReplyDraftSimulation?> SimulationProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftSimulation?>(nameof(Simulation));

    /// <summary>
    /// Adds "Simulated, nothing was sent" to announcements and errors. Null turns it on while a <see cref="Simulation"/>
    /// plays or there is no <see cref="Generator"/>.
    /// </summary>
    public static readonly StyledProperty<bool?> IsSimulatedProperty =
        AvaloniaProperty.Register<ReplyDraft, bool?>(nameof(IsSimulated));

    /// <summary>The 24 × 24 stroked mark before the subject; null draws a generic mail glyph.</summary>
    public static readonly StyledProperty<Geometry?> SourceMarkProperty =
        AvaloniaProperty.Register<ReplyDraft, Geometry?>(nameof(SourceMark));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<ReplyDraftLabels> LabelsProperty =
        AvaloniaProperty.Register<ReplyDraft, ReplyDraftLabels>(nameof(Labels), ReplyDraftLabels.Default);

    /// <summary>The draft on screen, including the reader's edits; empty before the first draft.</summary>
    public static readonly DirectProperty<ReplyDraft, string> DraftProperty =
        AvaloniaProperty.RegisterDirect<ReplyDraft, string>(nameof(Draft), o => o.Draft);

    /// <summary>Where the card is: idle, drafting, rewriting, ready, inserting, inserted or failed.</summary>
    public static readonly DirectProperty<ReplyDraft, ReplyDraftStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<ReplyDraft, ReplyDraftStatus>(nameof(Status), o => o.Status);

    /// <summary>Raised when the reader chooses a quick reply chip, before its draft starts. Bubbles.</summary>
    public static readonly RoutedEvent<ReplyDraftSuggestionEventArgs> SuggestionSelectedEvent =
        RoutedEvent.Register<ReplyDraft, ReplyDraftSuggestionEventArgs>(nameof(SuggestionSelected), RoutingStrategies.Bubble);

    /// <summary>Raised when the reader picks a tone (not when the app or the tour sets it). Bubbles.</summary>
    public static readonly RoutedEvent<ReplyDraftToneChangedEventArgs> ToneChangedEvent =
        RoutedEvent.Register<ReplyDraft, ReplyDraftToneChangedEventArgs>(nameof(ToneChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the reader picks a length (not when the app or the tour sets it). Bubbles.</summary>
    public static readonly RoutedEvent<ReplyDraftLengthChangedEventArgs> LengthChangedEvent =
        RoutedEvent.Register<ReplyDraft, ReplyDraftLengthChangedEventArgs>(nameof(LengthChanged), RoutingStrategies.Bubble);

    /// <summary>Raised each time a draft or rewrite finishes. Cancelled and failed requests do not raise it. Bubbles.</summary>
    public static readonly RoutedEvent<ReplyDraftEventArgs> DraftCompletedEvent =
        RoutedEvent.Register<ReplyDraft, ReplyDraftEventArgs>(nameof(DraftCompleted), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when Insert is pressed (or Cmd/Ctrl+Enter), with the final draft and the reader's edits, before
    /// <see cref="InsertHandler"/> runs. Mark it handled when you insert the text here, so the card does not say it
    /// was simulated. Bubbles.
    /// </summary>
    public static readonly RoutedEvent<ReplyDraftEventArgs> InsertRequestedEvent =
        RoutedEvent.Register<ReplyDraft, ReplyDraftEventArgs>(nameof(InsertRequested), RoutingStrategies.Bubble);

    private TextBlock? _subject;
    private TextBlock? _time;
    private ArcGlyph? _mailMark;
    private Avatar? _avatar;
    private TextBlock? _sender;
    private TextBlock? _address;
    private TextBlock? _recipient;
    private StackPanel? _body;
    private Control? _reply;
    private ArcGlyph? _modelMark;
    private TextBlock? _heading;
    private ArcContentSwap? _status;
    private WrapPanel? _suggestions;
    private Border? _draftBox;
    private AiHeightBox? _heightBox;
    private ArcContentSwap? _draftSwap;
    private ReplyDraftControlsPanel? _controls;
    private TextBlock? _toneLabel;
    private ReplyDraftOptionGroup? _tone;
    private TextBlock? _lengthLabel;
    private ReplyDraftOptionGroup? _length;
    private TextBlock? _hint;
    private Button? _regenerate;
    private TextBlock? _regenerateText;
    private Button? _insert;
    private TextBlock? _insertText;
    private ExplainerAnnouncer? _announcer;
    private readonly List<ReplyDraftChip> _chips = [];
    private string _draft = string.Empty;
    private ReplyDraftStatus _statusValue;
    private bool _coarse;
    private bool _quiet;

    static ReplyDraft()
    {
        AffectsMeasure<ReplyDraft>(MaxWidthProperty);
    }

    /// <summary>Creates the card.</summary>
    public ReplyDraft()
    {
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <inheritdoc cref="MessageProperty"/>
    public ReplyDraftMessage? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IReadOnlyList<ReplyDraftSuggestion>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="SelectedSuggestionIdProperty"/>
    public string? SelectedSuggestionId { get => GetValue(SelectedSuggestionIdProperty); set => SetValue(SelectedSuggestionIdProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AgentModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public ReplyDraftTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="LengthProperty"/>
    public ReplyDraftLength Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    /// <inheritdoc cref="GeneratorProperty"/>
    public ReplyDraftGenerator? Generator { get => GetValue(GeneratorProperty); set => SetValue(GeneratorProperty, value); }

    /// <inheritdoc cref="InsertHandlerProperty"/>
    public ReplyDraftInsertHandler? InsertHandler { get => GetValue(InsertHandlerProperty); set => SetValue(InsertHandlerProperty, value); }

    /// <inheritdoc cref="AutoStartProperty"/>
    public bool AutoStart { get => GetValue(AutoStartProperty); set => SetValue(AutoStartProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public ReplyDraftSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool? IsSimulated { get => GetValue(IsSimulatedProperty); set => SetValue(IsSimulatedProperty, value); }

    /// <inheritdoc cref="SourceMarkProperty"/>
    public Geometry? SourceMark { get => GetValue(SourceMarkProperty); set => SetValue(SourceMarkProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ReplyDraftLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="DraftProperty"/>
    public string Draft
    {
        get => _draft;
        private set => SetAndRaise(DraftProperty, ref _draft, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public ReplyDraftStatus Status
    {
        get => _statusValue;
        private set => SetAndRaise(StatusProperty, ref _statusValue, value);
    }

    /// <inheritdoc cref="SuggestionSelectedEvent"/>
    public event EventHandler<ReplyDraftSuggestionEventArgs>? SuggestionSelected
    {
        add => AddHandler(SuggestionSelectedEvent, value);
        remove => RemoveHandler(SuggestionSelectedEvent, value);
    }

    /// <inheritdoc cref="ToneChangedEvent"/>
    public event EventHandler<ReplyDraftToneChangedEventArgs>? ToneChanged
    {
        add => AddHandler(ToneChangedEvent, value);
        remove => RemoveHandler(ToneChangedEvent, value);
    }

    /// <inheritdoc cref="LengthChangedEvent"/>
    public event EventHandler<ReplyDraftLengthChangedEventArgs>? LengthChanged
    {
        add => AddHandler(LengthChangedEvent, value);
        remove => RemoveHandler(LengthChangedEvent, value);
    }

    /// <inheritdoc cref="DraftCompletedEvent"/>
    public event EventHandler<ReplyDraftEventArgs>? DraftCompleted
    {
        add => AddHandler(DraftCompletedEvent, value);
        remove => RemoveHandler(DraftCompletedEvent, value);
    }

    /// <inheritdoc cref="InsertRequestedEvent"/>
    public event EventHandler<ReplyDraftEventArgs>? InsertRequested
    {
        add => AddHandler(InsertRequestedEvent, value);
        remove => RemoveHandler(InsertRequestedEvent, value);
    }

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no <see cref="Message"/>.</summary>
    internal ReplyDraftSimulation? PlayingSimulation => Message is null ? Simulation : null;

    /// <summary>The email on screen: <see cref="Message"/>, the simulation's, or an empty one.</summary>
    internal ReplyDraftMessage DisplayedMessage => Message ?? PlayingSimulation?.Message ?? s_noMessage;

    /// <summary>The quick replies on screen: <see cref="Suggestions"/>, the simulation's, or none.</summary>
    internal IReadOnlyList<ReplyDraftSuggestion> DisplayedSuggestions => Suggestions ?? PlayingSimulation?.Suggestions ?? [];

    /// <summary>The model credited: <see cref="Model"/>, the simulation's, or none (an empty name).</summary>
    internal AgentModel DisplayedModel => Model ?? PlayingSimulation?.Model ?? s_noModel;

    /// <summary>Whether a draft can be written: an email to answer and a simulation or a <see cref="Generator"/> to write it.</summary>
    internal bool CanDraft => PlayingSimulation is { Message: not null } || (Message is not null && Generator is not null);

    /// <summary>The quick reply steering the draft: the selected id's, else the first, else none.</summary>
    internal ReplyDraftSuggestion? EffectiveSuggestion
    {
        get
        {
            var list = DisplayedSuggestions;
            if (list.Count == 0)
            {
                return null;
            }
            foreach (var suggestion in list)
            {
                if (suggestion.Id == SelectedSuggestionId)
                {
                    return suggestion;
                }
            }
            return list[0];
        }
    }

    internal bool EffectiveSimulated => IsSimulated ?? (PlayingSimulation is not null || Generator is null);

    internal IReadOnlyList<ReplyDraftChip> Chips => _chips;

    internal ReplyDraftOptionGroup? ToneGroup => _tone;

    internal ReplyDraftOptionGroup? LengthGroup => _length;

    internal AiHeightBox? HeightBox => _heightBox;

    internal ArcContentSwap? DraftSwap => _draftSwap;

    internal ArcContentSwap? StatusSwap => _status;

    internal ReplyDraftControlsPanel? ControlsPanel => _controls;

    internal Button? RegenerateButton => _regenerate;

    internal Button? InsertButton => _insert;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal StackPanel? BodyPanel => _body;

    /// <summary>Whether a touch press made the targets grow.</summary>
    internal bool IsCoarse => _coarse;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_tone is not null)
        {
            _tone.Picked -= OnTonePicked;
        }
        if (_length is not null)
        {
            _length.Picked -= OnLengthPicked;
        }
        if (_regenerate is not null)
        {
            _regenerate.Click -= OnRegenerateClick;
        }
        if (_insert is not null)
        {
            _insert.Click -= OnInsertClick;
        }
        _subject = e.NameScope.Find<TextBlock>(SubjectPart);
        _time = e.NameScope.Find<TextBlock>(TimePart);
        _mailMark = e.NameScope.Find<ArcGlyph>(MailMarkPart);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _sender = e.NameScope.Find<TextBlock>(SenderPart);
        _address = e.NameScope.Find<TextBlock>(AddressPart);
        _recipient = e.NameScope.Find<TextBlock>(RecipientPart);
        _body = e.NameScope.Find<StackPanel>(BodyPart);
        _reply = e.NameScope.Find<Control>(ReplyPart);
        _modelMark = e.NameScope.Find<ArcGlyph>(ModelMarkPart);
        _heading = e.NameScope.Find<TextBlock>(HeadingPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _suggestions = e.NameScope.Find<WrapPanel>(SuggestionsPart);
        _draftBox = e.NameScope.Find<Border>(DraftBoxPart);
        _heightBox = e.NameScope.Find<AiHeightBox>(HeightBoxPart);
        if (_heightBox is not null)
        {
            _heightBox.HeightSpring = AiGlyphMotion.Settle;
        }
        _draftSwap = e.NameScope.Find<ArcContentSwap>(DraftPart);
        _controls = e.NameScope.Find<ReplyDraftControlsPanel>(ControlsPart);
        _toneLabel = e.NameScope.Find<TextBlock>(ToneLabelPart);
        _tone = e.NameScope.Find<ReplyDraftOptionGroup>(TonePart);
        _lengthLabel = e.NameScope.Find<TextBlock>(LengthLabelPart);
        _length = e.NameScope.Find<ReplyDraftOptionGroup>(LengthPart);
        _hint = e.NameScope.Find<TextBlock>(HintPart);
        _regenerate = e.NameScope.Find<Button>(RegeneratePart);
        _regenerateText = e.NameScope.Find<TextBlock>(RegenerateTextPart);
        _insert = e.NameScope.Find<Button>(InsertPart);
        _insertText = e.NameScope.Find<TextBlock>(InsertTextPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_tone is not null)
        {
            _tone.Picked += OnTonePicked;
        }
        if (_length is not null)
        {
            _length.Picked += OnLengthPicked;
        }
        if (_regenerate is not null)
        {
            _regenerate.Click += OnRegenerateClick;
        }
        if (_insert is not null)
        {
            _insert.Click += OnInsertClick;
        }
        ApplyLabels();
        BuildMessage();
        BuildChips();
        SyncOptions();
        RestoreDraftView();
        UpdateStatusLine();
        UpdateButtons();
        SetCoarse(_coarse);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty || change.Property == LengthProperty)
        {
            SyncOptions();
            if (!_quiet)
            {
                OnStyleChanged();
            }
        }
        else if (change.Property == SelectedSuggestionIdProperty)
        {
            SyncChips();
            if (!_quiet && _started)
            {
                Begin(ReplyDraftMode.Draft, hold: true);
            }
        }
        else if (change.Property == SuggestionsProperty)
        {
            BuildChips();
        }
        else if (change.Property == MessageProperty || change.Property == SimulationProperty)
        {
            BuildMessage();
            BuildChips();
            ApplyLabels();
            Reset();
        }
        else if (change.Property == GeneratorProperty)
        {
            Reset();
        }
        else if (change.Property == ModelProperty)
        {
            ApplyLabels();
            UpdateStatusLine();
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            BuildMessage();
            UpdateStatusLine();
            RestoreDraftView();
        }
        else if (change.Property == SourceMarkProperty)
        {
            BuildMessage();
        }
        else if (change.Property == AutoStartProperty)
        {
            if (!_started)
            {
                RestoreDraftView();
                WatchViewport();
            }
        }
        else if (change.Property == AutoPlayProperty)
        {
            ScheduleTour();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchViewport();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopWatching();
        // Leaving the window aborts a running request; a card that had not finished starts again when it comes back.
        if (IsRunning)
        {
            Reset();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            SetWidthClasses(Math.Min(availableSize.Width, MaxWidth));
        }
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        SetWidthClasses(e.NewSize.Width);
    }

    private void SetWidthClasses(double width)
    {
        PseudoClasses.Set(CompactPseudoClass, width < CompactWidth);
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
    }

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        StopTour();
        if (e.Pointer.Type == PointerType.Touch && !_coarse)
        {
            SetCoarse(true);
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        StopTour();
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (e.Key == Key.Enter && command && !e.Handled)
        {
            e.Handled = true;
            _ = Insert();
        }
    }

    /// <summary>Grows chips to 36px and options to 32px for touch.</summary>
    private void SetCoarse(bool coarse)
    {
        _coarse = coarse;
        foreach (var chip in _chips)
        {
            chip.SetCoarse(coarse);
        }
        _tone?.SetCoarse(coarse);
        _length?.SetCoarse(coarse);
    }

    /// <summary>The words of the heading, the groups, the buttons and the card's names.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        var model = DisplayedModel;
        if (_heading is not null)
        {
            _heading.Text = labels.ReplyWith(model.Name);
        }
        if (_modelMark is not null)
        {
            _modelMark.Data = model.Mark ?? ArcIcons.Sparkles;
            if (model.MarkBrush is { } brush)
            {
                _modelMark.Foreground = brush;
            }
            else
            {
                _modelMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_reply is not null)
        {
            AutomationProperties.SetName(_reply, labels.ReplySection);
        }
        if (_suggestions is not null)
        {
            AutomationProperties.SetName(_suggestions, labels.QuickReplies);
        }
        if (_toneLabel is not null)
        {
            _toneLabel.Text = labels.Tone;
        }
        if (_lengthLabel is not null)
        {
            _lengthLabel.Text = labels.Length;
        }
        if (_tone is not null)
        {
            _tone.Labels = [labels.Friendly, labels.Concise, labels.Formal];
            AutomationProperties.SetName(_tone, labels.Tone);
        }
        if (_length is not null)
        {
            _length.Labels = [labels.Short, labels.Medium, labels.Long];
            AutomationProperties.SetName(_length, labels.Length);
        }
        if (_hint is not null)
        {
            _hint.Text = labels.EffectiveInsertShortcut;
        }
        if (_regenerateText is not null)
        {
            _regenerateText.Text = labels.Regenerate;
        }
        if (_regenerate is not null)
        {
            AutomationProperties.SetName(_regenerate, labels.Regenerate);
        }
        if (_insertText is not null)
        {
            _insertText.Text = labels.Insert;
        }
        if (_insert is not null)
        {
            AutomationProperties.SetName(_insert, labels.Insert);
            Tooltip.SetTip(_insert, labels.EffectiveInsertShortcut);
        }
        if (_editor is not null)
        {
            _editor.Label = labels.DraftText;
        }
    }

    /// <summary>The email: subject and time, the sender's avatar, name and address, and the body's paragraphs.</summary>
    private void BuildMessage()
    {
        var message = DisplayedMessage;
        var labels = Labels;
        AutomationProperties.SetName(this, labels.Article(message.FromName));
        if (_subject is not null)
        {
            _subject.Text = message.Subject;
        }
        if (_time is not null)
        {
            _time.Text = message.ReceivedAt is { } at ? labels.ReceivedAt(at) : null;
            _time.IsVisible = message.ReceivedAt is not null;
        }
        if (_mailMark is not null)
        {
            _mailMark.Data = SourceMark ?? ArcIcons.Mail;
        }
        if (_avatar is not null)
        {
            _avatar.DisplayName = message.FromName;
            _avatar.Source = message.FromAvatar;
            _avatar.IsVisible = !string.IsNullOrWhiteSpace(message.FromName) || message.FromAvatar is not null;
        }
        if (_sender is not null)
        {
            _sender.Text = message.FromName;
        }
        if (_address is not null)
        {
            _address.Text = string.IsNullOrWhiteSpace(message.FromEmail) ? null : "<" + message.FromEmail + ">";
            _address.IsVisible = !string.IsNullOrWhiteSpace(message.FromEmail);
        }
        if (_recipient is not null)
        {
            _recipient.Text = string.IsNullOrWhiteSpace(message.To) ? labels.ToMe : message.To;
        }
        if (_body is not null)
        {
            _body.Children.Clear();
            foreach (var paragraph in message.Paragraphs)
            {
                _body.Children.Add(new ReplyDraftParagraph { Text = paragraph });
            }
        }
    }

    /// <summary>One chip per quick reply; the row hides when there are none.</summary>
    private void BuildChips()
    {
        if (_suggestions is null)
        {
            return;
        }
        foreach (var chip in _chips)
        {
            chip.Click -= OnChipClick;
        }
        _chips.Clear();
        _suggestions.Children.Clear();
        foreach (var suggestion in DisplayedSuggestions)
        {
            var chip = new ReplyDraftChip { Suggestion = suggestion, Content = suggestion.Label };
            AutomationProperties.SetName(chip, suggestion.Label);
            chip.SetCoarse(_coarse);
            chip.Click += OnChipClick;
            _chips.Add(chip);
            _suggestions.Children.Add(chip);
        }
        _suggestions.IsVisible = _chips.Count > 0;
        SyncChips();
    }

    private void SyncChips()
    {
        var current = EffectiveSuggestion;
        foreach (var chip in _chips)
        {
            chip.IsChecked = ReferenceEquals(chip.Suggestion, current);
        }
    }

    private void SyncOptions()
    {
        if (_tone is not null)
        {
            _tone.SelectedIndex = (int)Tone;
        }
        if (_length is not null)
        {
            _length.SelectedIndex = (int)Length;
        }
    }

    private void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ReplyDraftChip { Suggestion: { } suggestion })
        {
            SelectSuggestion(suggestion, fromReader: true);
        }
    }

    /// <summary>Makes <paramref name="suggestion"/> steer the draft and writes a fresh one.</summary>
    internal void SelectSuggestion(ReplyDraftSuggestion suggestion, bool fromReader)
    {
        _quiet = true;
        try
        {
            SetCurrentValue(SelectedSuggestionIdProperty, suggestion.Id);
        }
        finally
        {
            _quiet = false;
        }
        SyncChips();
        if (fromReader)
        {
            RaiseEvent(new ReplyDraftSuggestionEventArgs(SuggestionSelectedEvent, suggestion));
        }
        Begin(ReplyDraftMode.Draft, hold: true);
    }

    private void OnTonePicked(object? sender, int index)
    {
        var tone = (ReplyDraftTone)index;
        if (tone == Tone)
        {
            return;
        }
        SetCurrentValue(ToneProperty, tone);
        RaiseEvent(new ReplyDraftToneChangedEventArgs(ToneChangedEvent, tone));
    }

    private void OnLengthPicked(object? sender, int index)
    {
        var length = (ReplyDraftLength)index;
        if (length == Length)
        {
            return;
        }
        SetCurrentValue(LengthProperty, length);
        RaiseEvent(new ReplyDraftLengthChangedEventArgs(LengthChangedEvent, length));
    }

    private void OnRegenerateClick(object? sender, RoutedEventArgs e) => Regenerate();

    private void OnInsertClick(object? sender, RoutedEventArgs e) => _ = Insert();
}
