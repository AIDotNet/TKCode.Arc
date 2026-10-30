using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// AI notes for one call (Arc Meeting summary, design reference): a header with the platform's mark, the title, the
/// platform, date and length and the attendee stack, then Summary, Decisions and Action items that stream in after a short
/// summarizing pass. Hovering a line, or focusing its timestamp, shows the transcript quote it came from. Action items can
/// be checked off and sent to an issue tracker or a docs app (<see cref="SendRequested"/>); the footer credits the model
/// and offers copy and regenerate.
/// <para>
/// Give it the notes in <see cref="Meeting"/> and drive <see cref="Status"/> yourself, or leave <see cref="Status"/> unset
/// and the card plays its own summarizing pass when it scrolls into view. With <see cref="Summarizer"/> the card calls
/// your async streaming delegate instead and streams each snapshot it yields. With neither it plays a
/// <see cref="Simulation"/> (a call's notes you pass); with no data at all it shows the no transcript state. A send with no
/// handler is simulated.
/// </para>
/// </summary>
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(TextBlock))]
[TemplatePart(AttendeesPart, typeof(AvatarGroup))]
[TemplatePart(BodyPart, typeof(AiHeightBox))]
[TemplatePart(LoadingPart, typeof(Panel))]
[TemplatePart(LoadingLabelPart, typeof(ToolCallText))]
[TemplatePart(NotesPart, typeof(Panel))]
[TemplatePart(ErrorPart, typeof(Panel))]
[TemplatePart(EmptyPart, typeof(Panel))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(RegeneratePart, typeof(AvaloniaButton))]
[TemplatePart(TryAgainPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(SummarizingPseudoClass, ReadyPseudoClass, ErrorPseudoClass, EmptyPseudoClass, NarrowPseudoClass, CompactPseudoClass)]
public partial class MeetingSummary : TemplatedControl
{
    internal const string MarkPart = "PART_Mark";
    internal const string TitlePart = "PART_Title";
    internal const string MetaPart = "PART_Meta";
    internal const string AttendeesPart = "PART_Attendees";
    internal const string BodyPart = "PART_Body";
    internal const string LoadingPart = "PART_Loading";
    internal const string LoadingLabelPart = "PART_LoadingLabel";
    internal const string NotesPart = "PART_Notes";
    internal const string ErrorPart = "PART_Error";
    internal const string EmptyPart = "PART_Empty";
    internal const string CopyPart = "PART_Copy";
    internal const string RegeneratePart = "PART_Regenerate";
    internal const string TryAgainPart = "PART_TryAgain";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string SummarizingPseudoClass = ":summarizing";
    internal const string ReadyPseudoClass = ":ready";
    internal const string ErrorPseudoClass = ":error";
    internal const string EmptyPseudoClass = ":empty";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>The video glyph (Lucide <c>video</c>), the default platform mark.</summary>
    internal static readonly Geometry VideoGlyph = ArcIcons.Parse(
        "M16 13l5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5 " +
        "M4 6h10a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z");

    private static readonly IReadOnlyList<MeetingSummarySendTarget> s_defaultTargets =
        [MeetingSummarySendTarget.Issues, MeetingSummarySendTarget.Docs];

    /// <summary>The notes a card with no data shows: none (the no transcript state).</summary>
    private static readonly MeetingSummaryData s_noMeeting = new() { Platform = string.Empty };

    /// <summary>The notes of the call. Null plays <see cref="Simulation"/>, or shows the no transcript state without one.</summary>
    public static readonly StyledProperty<MeetingSummaryData?> MeetingProperty =
        AvaloniaProperty.Register<MeetingSummary, MeetingSummaryData?>(nameof(Meeting));

    /// <summary>
    /// The status, when you drive it. Null (the default) lets the card play its own summarizing pass when it scrolls into
    /// view, or run <see cref="Summarizer"/>. Ready with empty lists shows the no transcript state.
    /// </summary>
    public static readonly StyledProperty<MeetingSummaryStatus?> StatusProperty =
        AvaloniaProperty.Register<MeetingSummary, MeetingSummaryStatus?>(nameof(Status));

    /// <summary>The message under the error title when the status is Error.</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<MeetingSummary, string?>(nameof(ErrorText));

    /// <summary>When the card drives its own status: start summarizing and play the pass. False starts on the finished notes.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<MeetingSummary, bool>(nameof(AutoPlay), true);

    /// <summary>Stream the notes in, a few words at a time, when the status moves from Summarizing to Ready.</summary>
    public static readonly StyledProperty<bool> StreamInProperty =
        AvaloniaProperty.Register<MeetingSummary, bool>(nameof(StreamIn), true);

    /// <summary>The model credited in the footer. Null credits the <see cref="Simulation"/>'s model while one plays, or hides the credit.</summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<MeetingSummary, AgentModel?>(nameof(Model));

    /// <summary>Where action items can be sent (Issues and Docs by default). An empty list hides the send buttons.</summary>
    public static readonly StyledProperty<IReadOnlyList<MeetingSummarySendTarget>?> SendTargetsProperty =
        AvaloniaProperty.Register<MeetingSummary, IReadOnlyList<MeetingSummarySendTarget>?>(nameof(SendTargets), s_defaultTargets);

    /// <summary>
    /// An async streaming delegate that produces the notes: the card calls it when it drives its own status (on first view
    /// and on regenerate), shows the summarizing pass until the first snapshot arrives, then streams every snapshot it
    /// yields (each one the notes so far). A fault shows the error state with its message. Takes the place of
    /// <see cref="Meeting"/> while it runs.
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, IAsyncEnumerable<MeetingSummaryData>>?> SummarizerProperty =
        AvaloniaProperty.Register<MeetingSummary, Func<CancellationToken, IAsyncEnumerable<MeetingSummaryData>>?>(nameof(Summarizer));

    /// <summary>
    /// A call to play while the card has no <see cref="Meeting"/> and no <see cref="Summarizer"/>: its notes and model,
    /// with the summarizing pass, the notes streaming in and a tick and a send played once (sends are simulated). Your own
    /// data wins over it; null stops it and shows the no transcript state.
    /// </summary>
    public static readonly StyledProperty<MeetingSummarySimulation?> SimulationProperty =
        AvaloniaProperty.Register<MeetingSummary, MeetingSummarySimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<MeetingSummaryLabels> LabelsProperty =
        AvaloniaProperty.Register<MeetingSummary, MeetingSummaryLabels>(nameof(Labels), MeetingSummaryLabels.Default);

    internal static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<MeetingSummary, double>(nameof(NarrowBreakpoint), 460);

    internal static readonly StyledProperty<double> CompactBreakpointProperty =
        AvaloniaProperty.Register<MeetingSummary, double>(nameof(CompactBreakpoint), 520);

    internal static readonly StyledProperty<double> QuoteGapProperty =
        AvaloniaProperty.Register<MeetingSummary, double>(nameof(QuoteGap), 6);

    internal static readonly StyledProperty<double> QuoteEnterOffsetProperty =
        AvaloniaProperty.Register<MeetingSummary, double>(nameof(QuoteEnterOffset), 3);

    /// <summary>
    /// Raised when the person sends an action item. Bubbles. Set <see cref="MeetingSummarySendEventArgs.Completion"/> to
    /// your request before the handler returns; leave it null and the card simulates the send.
    /// </summary>
    public static readonly RoutedEvent<MeetingSummarySendEventArgs> SendRequestedEvent =
        RoutedEvent.Register<MeetingSummary, MeetingSummarySendEventArgs>(nameof(SendRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a send succeeded (or a simulated send finished). Bubbles.</summary>
    public static readonly RoutedEvent<MeetingSummarySentEventArgs> ActionItemSentEvent =
        RoutedEvent.Register<MeetingSummary, MeetingSummarySentEventArgs>(nameof(ActionItemSent), RoutingStrategies.Bubble);

    /// <summary>Raised when the person checks or unchecks an action item. The card keeps the checked state itself. Bubbles.</summary>
    public static readonly RoutedEvent<MeetingActionItemChangedEventArgs> ActionItemChangedEvent =
        RoutedEvent.Register<MeetingSummary, MeetingActionItemChangedEventArgs>(nameof(ActionItemChanged), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised by Regenerate in the footer and Try again in the error state. Bubbles. A card that drives its own status
    /// also replays its pass (or runs <see cref="Summarizer"/> again).
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> RegenerateRequestedEvent =
        RoutedEvent.Register<MeetingSummary, RoutedEventArgs>(nameof(RegenerateRequested), RoutingStrategies.Bubble);

    private ArcGlyph? _mark;
    private TextBlock? _title;
    private TextBlock? _meta;
    private AvatarGroup? _attendees;
    private AiHeightBox? _body;
    private Panel? _loading;
    private ToolCallText? _loadingLabel;
    private Panel? _notes;
    private Panel? _error;
    private Panel? _empty;
    private Panel? _summarySection;
    private Panel? _decisionsSection;
    private Panel? _actionsSection;
    private Panel? _summaryList;
    private Panel? _decisionsList;
    private Panel? _actionsList;
    private TextBlock? _summaryHeading;
    private TextBlock? _decisionsHeading;
    private TextBlock? _actionsHeading;
    private TextBlock? _errorTitle;
    private TextBlock? _errorText;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyText;
    private ArcGlyph? _modelMark;
    private TextBlock? _credit;
    private CopyButton? _copy;
    private AvaloniaButton? _regenerate;
    private AvaloniaButton? _tryAgain;
    private ExplainerAnnouncer? _announcer;
    private Panel? _shownPanel;

    static MeetingSummary()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<MeetingSummary>(AutomationControlType.Group);
    }

    /// <summary>Creates the card.</summary>
    public MeetingSummary()
    {
        AddHandler(KeyDownEvent, OnCardKeyDown, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnCardPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        SizeChanged += (_, e) => UpdateBreakpoints(e.NewSize.Width);
    }

    /// <inheritdoc cref="MeetingProperty"/>
    public MeetingSummaryData? Meeting { get => GetValue(MeetingProperty); set => SetValue(MeetingProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public MeetingSummaryStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="StreamInProperty"/>
    public bool StreamIn { get => GetValue(StreamInProperty); set => SetValue(StreamInProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AgentModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="SendTargetsProperty"/>
    public IReadOnlyList<MeetingSummarySendTarget>? SendTargets { get => GetValue(SendTargetsProperty); set => SetValue(SendTargetsProperty, value); }

    /// <inheritdoc cref="SummarizerProperty"/>
    public Func<CancellationToken, IAsyncEnumerable<MeetingSummaryData>>? Summarizer
    {
        get => GetValue(SummarizerProperty);
        set => SetValue(SummarizerProperty, value);
    }

    /// <inheritdoc cref="SimulationProperty"/>
    public MeetingSummarySimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public MeetingSummaryLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    internal double CompactBreakpoint { get => GetValue(CompactBreakpointProperty); set => SetValue(CompactBreakpointProperty, value); }

    internal double QuoteGap { get => GetValue(QuoteGapProperty); set => SetValue(QuoteGapProperty, value); }

    internal double QuoteEnterOffset { get => GetValue(QuoteEnterOffsetProperty); set => SetValue(QuoteEnterOffsetProperty, value); }

    /// <inheritdoc cref="SendRequestedEvent"/>
    public event EventHandler<MeetingSummarySendEventArgs>? SendRequested
    {
        add => AddHandler(SendRequestedEvent, value);
        remove => RemoveHandler(SendRequestedEvent, value);
    }

    /// <inheritdoc cref="ActionItemSentEvent"/>
    public event EventHandler<MeetingSummarySentEventArgs>? ActionItemSent
    {
        add => AddHandler(ActionItemSentEvent, value);
        remove => RemoveHandler(ActionItemSentEvent, value);
    }

    /// <inheritdoc cref="ActionItemChangedEvent"/>
    public event EventHandler<MeetingActionItemChangedEventArgs>? ActionItemChanged
    {
        add => AddHandler(ActionItemChangedEvent, value);
        remove => RemoveHandler(ActionItemChangedEvent, value);
    }

    /// <inheritdoc cref="RegenerateRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RegenerateRequested
    {
        add => AddHandler(RegenerateRequestedEvent, value);
        remove => RemoveHandler(RegenerateRequestedEvent, value);
    }

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no <see cref="Meeting"/> and no <see cref="Summarizer"/>.</summary>
    internal MeetingSummarySimulation? PlayingSimulation => Meeting is null && Summarizer is null ? Simulation : null;

    /// <summary>The notes on screen: the summarizer's latest snapshot, <see cref="Meeting"/>, the simulation's, or none.</summary>
    internal MeetingSummaryData EffectiveMeeting => _snapshot ?? Meeting ?? PlayingSimulation?.Meeting ?? s_noMeeting;

    /// <summary>True while a simulation's notes are on screen.</summary>
    internal bool UsesSimulation => _snapshot is null && PlayingSimulation is { Meeting: not null };

    /// <summary>Whether there are notes to summarize: your <see cref="Meeting"/>, a <see cref="Summarizer"/> or a simulation's.</summary>
    internal bool HasNotesSource => Meeting is not null || Summarizer is not null || PlayingSimulation is { Meeting: not null };

    /// <summary>The model credited: <see cref="Model"/>, or the simulation's while it plays.</summary>
    internal AgentModel? EffectiveModel => Model ?? PlayingSimulation?.Model;

    /// <summary>The status on screen: <see cref="Status"/>, or the card's own.</summary>
    internal MeetingSummaryStatus EffectiveStatus => Status ?? _ownStatus;

    /// <summary>The body's spring box.</summary>
    internal AiHeightBox? BodyBox => _body;

    /// <summary>The visible body panel.</summary>
    internal Panel? ShownPanel => _shownPanel;

    internal Panel? LoadingPanel => _loading;

    internal Panel? NotesPanel => _notes;

    internal Panel? ErrorPanel => _error;

    internal Panel? EmptyPanel => _empty;

    internal TextBlock? MetaText => _meta;

    internal TextBlock? TitleText => _title;

    internal TextBlock? CreditText => _credit;

    internal TextBlock? ErrorMessageText => _errorText;

    internal CopyButton? CopyButtonPart => _copy;

    internal AvaloniaButton? RegenerateButton => _regenerate;

    internal AvaloniaButton? TryAgainButton => _tryAgain;

    internal AvatarGroup? AttendeeStack => _attendees;

    /// <summary>The last thing the live region said.</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_regenerate is not null)
        {
            _regenerate.Click -= OnRegenerateClick;
        }
        if (_tryAgain is not null)
        {
            _tryAgain.Click -= OnRegenerateClick;
        }
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _meta = e.NameScope.Find<TextBlock>(MetaPart);
        _attendees = e.NameScope.Find<AvatarGroup>(AttendeesPart);
        _body = e.NameScope.Find<AiHeightBox>(BodyPart);
        if (_body is not null)
        {
            _body.HeightSpring = AiHeightBox.PanelSpring;
        }
        _loading = e.NameScope.Find<Panel>(LoadingPart);
        _loadingLabel = e.NameScope.Find<ToolCallText>(LoadingLabelPart);
        _notes = e.NameScope.Find<Panel>(NotesPart);
        _error = e.NameScope.Find<Panel>(ErrorPart);
        _empty = e.NameScope.Find<Panel>(EmptyPart);
        _summarySection = e.NameScope.Find<Panel>("PART_SummarySection");
        _decisionsSection = e.NameScope.Find<Panel>("PART_DecisionsSection");
        _actionsSection = e.NameScope.Find<Panel>("PART_ActionsSection");
        _summaryList = e.NameScope.Find<Panel>("PART_SummaryList");
        _decisionsList = e.NameScope.Find<Panel>("PART_DecisionsList");
        _actionsList = e.NameScope.Find<Panel>("PART_ActionsList");
        _summaryHeading = e.NameScope.Find<TextBlock>("PART_SummaryHeading");
        _decisionsHeading = e.NameScope.Find<TextBlock>("PART_DecisionsHeading");
        _actionsHeading = e.NameScope.Find<TextBlock>("PART_ActionsHeading");
        _errorTitle = e.NameScope.Find<TextBlock>("PART_ErrorTitle");
        _errorText = e.NameScope.Find<TextBlock>("PART_ErrorText");
        _emptyTitle = e.NameScope.Find<TextBlock>("PART_EmptyTitle");
        _emptyText = e.NameScope.Find<TextBlock>("PART_EmptyText");
        _modelMark = e.NameScope.Find<ArcGlyph>("PART_ModelMark");
        _credit = e.NameScope.Find<TextBlock>("PART_Credit");
        _copy = e.NameScope.Find<CopyButton>(CopyPart);
        _regenerate = e.NameScope.Find<AvaloniaButton>(RegeneratePart);
        _tryAgain = e.NameScope.Find<AvaloniaButton>(TryAgainPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_regenerate is not null)
        {
            _regenerate.Click += OnRegenerateClick;
        }
        if (_tryAgain is not null)
        {
            _tryAgain.Click += OnRegenerateClick;
        }
        _shownPanel = null;
        ClearRows();
        SyncRows(stream: false);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StatusProperty)
        {
            OnStatusChanged(change.GetOldValue<MeetingSummaryStatus?>() ?? _ownStatus);
        }
        else if (property == MeetingProperty)
        {
            OnMeetingChanged();
        }
        else if (property == SimulationProperty || property == SummarizerProperty)
        {
            OnSourceChanged();
        }
        else if (property == LabelsProperty || property == ModelProperty || property == ErrorTextProperty)
        {
            Refresh();
            SyncRows(stream: false);
        }
        else if (property == SendTargetsProperty)
        {
            SyncRows(stream: false);
        }
        else if (property == CompactBreakpointProperty || property == NarrowBreakpointProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private void OnRegenerateClick(object? sender, RoutedEventArgs e) => Regenerate();

    /// <summary>Raises <see cref="RegenerateRequested"/>; a card that drives its own status plays its pass again.</summary>
    public void Regenerate()
    {
        _interacted = true;
        CloseQuote();
        RaiseEvent(new RoutedEventArgs(RegenerateRequestedEvent));
        CancelWork();
        if (Status is null)
        {
            _done.Clear();
            _seeded.Clear();
            _sends.Clear();
            _snapshot = null;
            ClearRows();
            StartPass();
        }
    }

    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowBreakpoint);
        var compact = width < CompactBreakpoint;
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var row in _rows)
        {
            if (row is MeetingSummaryActionRow action)
            {
                action.SetCompact(compact);
            }
        }
    }

    /// <summary>Brings the header, the body's state and the footer in line with the data and the status.</summary>
    private void Refresh()
    {
        if (_title is null)
        {
            return;
        }
        var data = EffectiveMeeting;
        var labels = Labels;
        var status = EffectiveStatus;
        _title.Text = data.Title;
        AutomationProperties.SetName(this, data.Title);
        if (_mark is not null)
        {
            _mark.Data = data.PlatformMark ?? VideoGlyph;
        }
        if (_meta is not null)
        {
            var parts = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(data.Platform))
            {
                parts.Add(data.Platform);
            }
            if (data.StartsAt is { } at)
            {
                parts.Add(labels.StartsAt(at));
            }
            if (data.DurationMinutes > 0)
            {
                parts.Add(labels.Duration(data.DurationMinutes));
            }
            _meta.Text = string.Join(" · ", parts);
        }
        if (_attendees is not null)
        {
            _attendees.Label = labels.Attendees;
            _attendees.IsVisible = data.Attendees.Count > 0;
            var members = data.Attendees.Select(a => (object)new AvatarGroupMember(a.Name, a.Portrait)).ToList();
            _attendees.ItemsSource = members;
        }

        if (_summaryHeading is not null)
        {
            _summaryHeading.Text = labels.Summary;
        }
        if (_decisionsHeading is not null)
        {
            _decisionsHeading.Text = labels.Decisions;
        }
        if (_actionsHeading is not null)
        {
            _actionsHeading.Text = labels.ActionItems;
        }
        if (_loadingLabel is not null)
        {
            _loadingLabel.Text = labels.Summarizing;
            _loadingLabel.IsShimmering = status == MeetingSummaryStatus.Summarizing;
        }
        if (_errorTitle is not null)
        {
            _errorTitle.Text = labels.ErrorTitle;
        }
        if (_errorText is not null)
        {
            _errorText.Text = ErrorText ?? _ownError ?? labels.ErrorFallback;
        }
        if (_tryAgain is not null)
        {
            _tryAgain.Content = labels.TryAgain;
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = labels.EmptyTitle;
        }
        if (_emptyText is not null)
        {
            _emptyText.Text = labels.EmptyText;
        }

        var model = EffectiveModel;
        if (_modelMark is not null)
        {
            _modelMark.IsVisible = model is not null;
            _modelMark.Data = model?.Mark ?? ArcIcons.Sparkles;
            if (model?.MarkBrush is { } brush)
            {
                _modelMark.Foreground = brush;
            }
            else
            {
                _modelMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_credit is not null)
        {
            var parts = new List<string>(2);
            if (model is not null && !string.IsNullOrEmpty(model.Name))
            {
                parts.Add(labels.SummarizedBy(model.Name));
            }
            if (data.TranscriptWords > 0)
            {
                parts.Add(labels.Words(data.TranscriptWords));
            }
            _credit.Text = string.Join(" · ", parts);
        }
        var ready = status == MeetingSummaryStatus.Ready && !data.IsEmpty;
        if (_copy is not null)
        {
            _copy.IsEnabled = ready;
            _copy.Value = ready ? MeetingSummaryFormat.PlainText(data, labels, IsDone) : null;
            AutomationProperties.SetName(_copy, labels.Copy);
        }
        if (_regenerate is not null)
        {
            _regenerate.IsEnabled = status != MeetingSummaryStatus.Summarizing;
            AutomationProperties.SetName(_regenerate, labels.Regenerate);
            Tooltip.SetTip(_regenerate, labels.Regenerate);
        }
        ShowState();
    }

    /// <summary>Shows the panel for the status; a panel that appears after load fades in while the body springs to its height.</summary>
    private void ShowState()
    {
        var status = EffectiveStatus;
        var empty = status == MeetingSummaryStatus.Ready && EffectiveMeeting.IsEmpty;
        PseudoClasses.Set(SummarizingPseudoClass, status == MeetingSummaryStatus.Summarizing);
        PseudoClasses.Set(ReadyPseudoClass, status == MeetingSummaryStatus.Ready);
        PseudoClasses.Set(ErrorPseudoClass, status == MeetingSummaryStatus.Error);
        PseudoClasses.Set(EmptyPseudoClass, empty);
        var panel = status switch
        {
            MeetingSummaryStatus.Summarizing => _loading,
            MeetingSummaryStatus.Error => _error,
            _ => empty ? _empty : _notes,
        };
        if (ReferenceEquals(panel, _shownPanel))
        {
            return;
        }
        var fades = _shownPanel is not null && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        foreach (var candidate in new[] { _loading, _notes, _error, _empty })
        {
            if (candidate is not null)
            {
                candidate.IsVisible = ReferenceEquals(candidate, panel);
            }
        }
        _shownPanel = panel;
        if (panel is not null && fades)
        {
            panel.Opacity = 0;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => panel.Opacity = 1, Avalonia.Threading.DispatcherPriority.Loaded);
        }
        else if (panel is not null)
        {
            panel.Opacity = 1;
        }
        if (_summarySection is not null)
        {
            _summarySection.IsVisible = EffectiveMeeting.Summary.Count > 0;
        }
        if (_decisionsSection is not null)
        {
            _decisionsSection.IsVisible = EffectiveMeeting.Decisions.Count > 0;
        }
        if (_actionsSection is not null)
        {
            _actionsSection.IsVisible = EffectiveMeeting.ActionItems.Count > 0;
        }
    }

    /// <summary>Says <paramref name="text"/> in the polite live region (a repeat is still announced).</summary>
    internal void Announce(string text)
    {
        // The same sentence twice is still read again.
        _announcer?.Announce(text);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MeetingSummaryPeer(this);

    private sealed class MeetingSummaryPeer(MeetingSummary owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => AutomationProperties.GetName(Owner) ?? ((MeetingSummary)Owner).EffectiveMeeting.Title;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "article";
    }
}
