using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A live document where an AI agent edits like a collaborator (Arc Agent cursor, design reference). The agent's labeled
/// cursor comes out of its avatar, selects a paragraph and streams in a rewrite, adds a heading and fixes a typo, while a
/// teammate's cursor reads along. The changes stay tinted in the agent's color until someone accepts or undoes them from a
/// floating review bar.
/// <para>
/// Give it the document as <see cref="Blocks"/> (title, heading and paragraph blocks with stable ids) and the edits as
/// <see cref="Edits"/>, or fetch them from your agent with <see cref="Run"/> (cancelled when someone presses Stop). Store
/// the document from <see cref="Accepted"/> and pass it back as <see cref="Blocks"/>. With none of them it plays a
/// <see cref="Simulation"/> (a document, its edits, an agent and a teammate you pass), looping; with no data at all it
/// shows an empty document.
/// </para>
/// </summary>
[TemplatePart(StagePart, typeof(Panel))]
[TemplatePart(AgentAvatarPart, typeof(Border))]
[TemplatePart(AgentMarkPart, typeof(ArcGlyph))]
[TemplatePart(TeammateRingPart, typeof(Border))]
[TemplatePart(TeammateAvatarPart, typeof(Avatar))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(ActionPart, typeof(AvaloniaButton))]
[TemplatePart(DocumentPart, typeof(StackPanel))]
[TemplatePart(OverlayPart, typeof(Canvas))]
[TemplatePart(ReviewBarPart, typeof(Border))]
[TemplatePart(ReviewTextPart, typeof(TextBlock))]
[TemplatePart(UndoPart, typeof(AvaloniaButton))]
[TemplatePart(AcceptPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(CompactPseudoClass, BusyPseudoClass, ReviewPseudoClass)]
public partial class AgentCursor : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string AgentAvatarPart = "PART_AgentAvatar";
    internal const string AgentMarkPart = "PART_AgentMark";
    internal const string TeammateRingPart = "PART_TeammateRing";
    internal const string TeammateAvatarPart = "PART_TeammateAvatar";
    internal const string StatusPart = "PART_Status";
    internal const string ActionPart = "PART_Action";
    internal const string DocumentPart = "PART_Document";
    internal const string OverlayPart = "PART_Overlay";
    internal const string ReviewBarPart = "PART_ReviewBar";
    internal const string ReviewTextPart = "PART_ReviewText";
    internal const string UndoPart = "PART_Undo";
    internal const string AcceptPart = "PART_Accept";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string CompactPseudoClass = ":compact";
    internal const string BusyPseudoClass = ":busy";
    internal const string ReviewPseudoClass = ":review";

    /// <summary>The document as title, heading and paragraph blocks with stable ids; null shows the <see cref="Simulation"/>'s while one plays, or an empty document. Compared by content, so passing back an equal list does nothing.</summary>
    public static readonly StyledProperty<IList<AgentCursorBlock>?> BlocksProperty =
        AvaloniaProperty.Register<AgentCursor, IList<AgentCursorBlock>?>(nameof(Blocks));

    /// <summary>A muted line under the title, such as the owner and a target date.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AgentCursor, string?>(nameof(Meta));

    /// <summary>The edits to play in order (rewrite, insert, fix); ignored when <see cref="Run"/> is set. Null plays the <see cref="Simulation"/>'s while one plays, or none.</summary>
    public static readonly StyledProperty<IList<AgentCursorEdit>?> EditsProperty =
        AvaloniaProperty.Register<AgentCursor, IList<AgentCursorEdit>?>(nameof(Edits));

    /// <summary>Fetches the edits from your agent. The token is cancelled when someone presses Stop; a failed task shows the error with Retry.</summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<IReadOnlyList<AgentCursorEdit>>>?> RunProperty =
        AvaloniaProperty.Register<AgentCursor, Func<CancellationToken, Task<IReadOnlyList<AgentCursorEdit>>>?>(nameof(Run));

    /// <summary>The agent: its name on the cursor, its 24 × 24 stroked mark in the avatar and, from a solid <see cref="AgentModel.MarkBrush"/>, the color of its cursor, selection and change tint (the accent otherwise). Null is the <see cref="Simulation"/>'s agent while one plays, or an unnamed one.</summary>
    public static readonly StyledProperty<AgentModel?> AgentProperty =
        AvaloniaProperty.Register<AgentCursor, AgentModel?>(nameof(Agent));

    /// <summary>A person in the document, drawn as a second cursor with their initials and first name. Null is the <see cref="Simulation"/>'s teammate while one plays, or nobody.</summary>
    public static readonly StyledProperty<AgentCursorCollaborator?> CollaboratorProperty =
        AvaloniaProperty.Register<AgentCursor, AgentCursorCollaborator?>(nameof(Collaborator));

    /// <summary>Whether the collaborator is shown; false hides their cursor and avatar.</summary>
    public static readonly StyledProperty<bool> IsCollaboratorVisibleProperty =
        AvaloniaProperty.Register<AgentCursor, bool>(nameof(IsCollaboratorVisible), true);

    /// <summary>Starts a run as soon as the card is on screen. Off, the header shows an Ask button instead.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<AgentCursor, bool>(nameof(AutoPlay), true);

    /// <summary>Replays the run: the collaborator accepts after a short pause (never while the pointer is over the card or focus is in it), then the document resets. Null loops only while a <see cref="Simulation"/> plays. Never loops below full motion.</summary>
    public static readonly StyledProperty<bool?> LoopProperty =
        AvaloniaProperty.Register<AgentCursor, bool?>(nameof(Loop));

    /// <summary>Playback speed multiplier for cursor travel, streaming and pauses (above zero).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AgentCursor, double>(nameof(Speed), 1.0, coerce: (_, v) => double.IsFinite(v) && v > 0 ? v : 1);

    /// <summary>Adds "Simulated, nothing was saved" to every announcement. Null: only while a <see cref="Simulation"/> plays.</summary>
    public static readonly StyledProperty<bool?> IsSimulatedProperty =
        AvaloniaProperty.Register<AgentCursor, bool?>(nameof(IsSimulated));

    /// <summary>
    /// A session to play while the card has no <see cref="Blocks"/>, <see cref="Edits"/> or <see cref="Run"/>: its document,
    /// edits, agent, teammate and meta line, looping with full motion (nothing is saved). Your own data wins over it; null
    /// stops it and shows the empty document.
    /// </summary>
    public static readonly StyledProperty<AgentCursorSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AgentCursor, AgentCursorSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<AgentCursorLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentCursor, AgentCursorLabels>(nameof(Labels), AgentCursorLabels.Default);

    /// <summary>Where the run is: idle, thinking, editing, review, accepted, reverted, stopped, empty or error.</summary>
    public static readonly DirectProperty<AgentCursor, AgentCursorPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<AgentCursor, AgentCursorPhase>(nameof(Phase), o => o.Phase);

    /// <summary>How many edits are waiting for review.</summary>
    public static readonly DirectProperty<AgentCursor, int> PendingChangesProperty =
        AvaloniaProperty.RegisterDirect<AgentCursor, int>(nameof(PendingChanges), o => o.PendingChanges);

    /// <summary>Raised with the edited document when someone accepts the changes. Bubbles.</summary>
    public static readonly RoutedEvent<AgentCursorDocumentEventArgs> AcceptedEvent =
        RoutedEvent.Register<AgentCursor, AgentCursorDocumentEventArgs>(nameof(Accepted), RoutingStrategies.Bubble);

    /// <summary>Raised with the restored document when someone undoes the changes. Bubbles.</summary>
    public static readonly RoutedEvent<AgentCursorDocumentEventArgs> UndoneEvent =
        RoutedEvent.Register<AgentCursor, AgentCursorDocumentEventArgs>(nameof(Undone), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="Phase"/> changes. Bubbles.</summary>
    public static readonly RoutedEvent<AgentCursorPhaseChangedEventArgs> PhaseChangedEvent =
        RoutedEvent.Register<AgentCursor, AgentCursorPhaseChangedEventArgs>(nameof(PhaseChanged), RoutingStrategies.Bubble);

    /// <summary>The agent and teammate of a card with neither: an unnamed agent (the labels' name) and nobody reading along.</summary>
    private static readonly AgentModel s_noAgent = new(string.Empty);
    private static readonly AgentCursorCollaborator s_noCollaborator = new();

    private AgentCursorPhase _phase;
    private int _pendingChanges;
    private Panel? _stage;
    private Border? _agentAvatar;
    private ArcGlyph? _agentMark;
    private Border? _teammateRing;
    private Avatar? _teammateAvatar;
    private ArcContentSwap? _status;
    private AvaloniaButton? _action;
    private StackPanel? _document;
    private Canvas? _overlay;
    private Border? _reviewBar;
    private TextBlock? _reviewText;
    private AvaloniaButton? _undo;
    private AvaloniaButton? _accept;
    private ExplainerAnnouncer? _announcer;
    private string? _statusKey;
    private ArcGlyph? _statusGlyph;
    private bool _statusGlyphTinted;

    static AgentCursor()
    {
        FocusableProperty.OverrideDefaultValue<AgentCursor>(false);
    }

    /// <summary>Creates the card; with no data it plays the sample.</summary>
    public AgentCursor()
    {
        // The agent's default color is the accent and the teammate's a series color: both follow theme and accent changes.
        ActualThemeVariantChanged += (_, _) => ApplyColors();
        ResourcesChanged += (_, _) => ApplyColors();
    }

    /// <inheritdoc cref="BlocksProperty"/>
    public IList<AgentCursorBlock>? Blocks { get => GetValue(BlocksProperty); set => SetValue(BlocksProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="EditsProperty"/>
    public IList<AgentCursorEdit>? Edits { get => GetValue(EditsProperty); set => SetValue(EditsProperty, value); }

    /// <inheritdoc cref="RunProperty"/>
    public Func<CancellationToken, Task<IReadOnlyList<AgentCursorEdit>>>? Run { get => GetValue(RunProperty); set => SetValue(RunProperty, value); }

    /// <inheritdoc cref="AgentProperty"/>
    public AgentModel? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    /// <inheritdoc cref="CollaboratorProperty"/>
    public AgentCursorCollaborator? Collaborator { get => GetValue(CollaboratorProperty); set => SetValue(CollaboratorProperty, value); }

    /// <inheritdoc cref="IsCollaboratorVisibleProperty"/>
    public bool IsCollaboratorVisible { get => GetValue(IsCollaboratorVisibleProperty); set => SetValue(IsCollaboratorVisibleProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="LoopProperty"/>
    public bool? Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool? IsSimulated { get => GetValue(IsSimulatedProperty); set => SetValue(IsSimulatedProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AgentCursorSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentCursorLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public AgentCursorPhase Phase
    {
        get => _phase;
        private set => SetAndRaise(PhaseProperty, ref _phase, value);
    }

    /// <inheritdoc cref="PendingChangesProperty"/>
    public int PendingChanges
    {
        get => _pendingChanges;
        private set => SetAndRaise(PendingChangesProperty, ref _pendingChanges, value);
    }

    /// <inheritdoc cref="AcceptedEvent"/>
    public event EventHandler<AgentCursorDocumentEventArgs>? Accepted
    {
        add => AddHandler(AcceptedEvent, value);
        remove => RemoveHandler(AcceptedEvent, value);
    }

    /// <inheritdoc cref="UndoneEvent"/>
    public event EventHandler<AgentCursorDocumentEventArgs>? Undone
    {
        add => AddHandler(UndoneEvent, value);
        remove => RemoveHandler(UndoneEvent, value);
    }

    /// <inheritdoc cref="PhaseChangedEvent"/>
    public event EventHandler<AgentCursorPhaseChangedEventArgs>? PhaseChanged
    {
        add => AddHandler(PhaseChangedEvent, value);
        remove => RemoveHandler(PhaseChangedEvent, value);
    }

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no blocks, edits or run of the app's own.</summary>
    internal AgentCursorSimulation? PlayingSimulation => Blocks is null && Edits is null && Run is null ? Simulation : null;

    internal AgentModel EffectiveAgent => Agent ?? PlayingSimulation?.Agent ?? s_noAgent;

    internal AgentCursorCollaborator EffectiveCollaborator => Collaborator ?? PlayingSimulation?.Collaborator ?? s_noCollaborator;

    internal IReadOnlyList<AgentCursorBlock> EffectiveBlocks => Blocks is { } blocks ? blocks.ToList() : PlayingSimulation?.Blocks ?? [];

    internal IReadOnlyList<AgentCursorEdit> EffectiveEdits =>
        Edits is { } edits ? edits.ToList() : Blocks is null ? PlayingSimulation?.Edits ?? [] : [];

    internal string? EffectiveMeta => Meta ?? PlayingSimulation?.Meta;

    internal bool EffectiveSimulated => IsSimulated ?? PlayingSimulation is not null;

    internal bool EffectiveLoop => (Loop ?? PlayingSimulation is not null) && ArcMotion.IsFull(this);

    internal string AgentName => string.IsNullOrWhiteSpace(EffectiveAgent.Name) ? Labels.Agent : EffectiveAgent.Name;

    internal string? StatusText { get; private set; }

    internal Geometry? StatusGlyph { get; private set; }

    internal bool StatusShimmers { get; private set; }

    internal AvaloniaButton? ActionButton => _action;

    internal AvaloniaButton? UndoButton => _undo;

    internal AvaloniaButton? AcceptButton => _accept;

    internal Border? ReviewBar => _reviewBar;

    internal TextBlock? ReviewText => _reviewText;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal bool IsCompact => PseudoClasses.Contains(CompactPseudoClass);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _stage = e.NameScope.Find<Panel>(StagePart);
        _agentAvatar = e.NameScope.Find<Border>(AgentAvatarPart);
        _agentMark = e.NameScope.Find<ArcGlyph>(AgentMarkPart);
        _teammateRing = e.NameScope.Find<Border>(TeammateRingPart);
        _teammateAvatar = e.NameScope.Find<Avatar>(TeammateAvatarPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _action = e.NameScope.Find<AvaloniaButton>(ActionPart);
        _document = e.NameScope.Find<StackPanel>(DocumentPart);
        _overlay = e.NameScope.Find<Canvas>(OverlayPart);
        _reviewBar = e.NameScope.Find<Border>(ReviewBarPart);
        _reviewText = e.NameScope.Find<TextBlock>(ReviewTextPart);
        _undo = e.NameScope.Find<AvaloniaButton>(UndoPart);
        _accept = e.NameScope.Find<AvaloniaButton>(AcceptPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_undo is not null)
        {
            _undo.Click += OnUndoClick;
        }
        if (_accept is not null)
        {
            _accept.Click += OnAcceptClick;
        }
        if (_stage is not null)
        {
            _stage.SizeChanged += OnStageSizeChanged;
        }
        if (_document is not null)
        {
            AutomationProperties.SetName(_document, Labels.Document);
        }
        if (_reviewBar is not null)
        {
            _reviewBar.RenderTransform = _barShift;
            _reviewBar.IsVisible = false;
            _reviewBar.Opacity = 0;
        }
        _statusKey = null;
        EndScript();
        BuildCursors();
        LoadDocument(EffectiveBlocks);
        ApplyIdentity();
        ApplyColors();
        if (Phase != AgentCursorPhase.Idle)
        {
            // A new template mid-run starts over from the document.
            SetPhase(AgentCursorPhase.Idle);
        }
        RefreshChrome();
        RefreshStatus();
        StartAutoPlay();
    }

    /// <summary>Starts the first run on its own once the card is attached and has its template.</summary>
    private void StartAutoPlay()
    {
        if (AutoPlay && _document is not null && Phase == AgentCursorPhase.Idle && _script is null && this.IsAttachedToVisualTree()
            && EffectiveBlocks.Count > 0)
        {
            Ask();
        }
    }

    private void Unhook()
    {
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        if (_undo is not null)
        {
            _undo.Click -= OnUndoClick;
        }
        if (_accept is not null)
        {
            _accept.Click -= OnAcceptClick;
        }
        if (_stage is not null)
        {
            _stage.SizeChanged -= OnStageSizeChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BlocksProperty || change.Property == MetaProperty)
        {
            OnDocumentInputChanged();
        }
        else if (change.Property == SimulationProperty)
        {
            OnSimulationChanged();
        }
        else if (change.Property == AgentProperty || change.Property == CollaboratorProperty || change.Property == IsCollaboratorVisibleProperty)
        {
            ApplyIdentity();
            ApplyColors();
            RefreshStatus();
            RefreshChrome();
        }
        else if (change.Property == LabelsProperty || change.Property == AutoPlayProperty)
        {
            if (_document is not null)
            {
                AutomationProperties.SetName(_document, Labels.Document);
            }
            _statusKey = null;
            RefreshStatus();
            RefreshChrome();
            if (change.Property == AutoPlayProperty)
            {
                StartAutoPlay();
            }
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateCompact();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyColors();
        StartAutoPlay();
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        switch (Phase)
        {
            case AgentCursorPhase.Thinking:
            case AgentCursorPhase.Editing:
                Stop();
                break;
            default:
                Ask();
                break;
        }
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Undo();

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => Accept();

    private void UpdateCompact()
    {
        var limit = this.TryFindResource(AgentCursorTokenKind.CompactWidth, ActualThemeVariant, out var value) && value is double d ? d : 480;
        var compact = Bounds.Width > 0 && Bounds.Width < limit;
        if (PseudoClasses.Contains(CompactPseudoClass) != compact)
        {
            PseudoClasses.Set(CompactPseudoClass, compact);
            RefreshReviewText();
        }
    }

    /// <summary>The agent's color: a solid mark brush of the agent, otherwise the accent.</summary>
    internal Color AgentColor => EffectiveAgent.MarkBrush is ISolidColorBrush solid ? solid.Color : ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);

    /// <summary>The teammate's color: their own, otherwise the theme's second series color.</summary>
    internal Color TeammateColor => EffectiveCollaborator.Color ?? ArcTokens.GetColor(this, ArcDesignTokenKind.Series2);

    private void ApplyColors()
    {
        var agent = AgentColor;
        if (_agentAvatar is not null)
        {
            _agentAvatar.Background = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(agent, 0.14);
        }
        if (_agentMark is not null)
        {
            _agentMark.Foreground = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(agent);
        }
        if (_agentFlight is not null)
        {
            _agentFlight.Pointer.Color = agent;
        }
        if (_teammateFlight is not null)
        {
            _teammateFlight.Pointer.Color = TeammateColor;
        }
        foreach (var block in _blocks)
        {
            block.View.SetAccent(agent);
        }
        if (_statusGlyph is not null && _statusGlyphTinted)
        {
            _statusGlyph.Foreground = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(agent);
        }
    }

    private void ApplyIdentity()
    {
        var agent = EffectiveAgent;
        if (_agentMark is not null)
        {
            _agentMark.Data = agent.Mark ?? ArcIcons.Sparkles;
        }
        if (_agentAvatar is not null)
        {
            AutomationProperties.SetName(_agentAvatar, AgentName);
        }
        var teammate = EffectiveCollaborator;
        if (_teammateAvatar is not null)
        {
            _teammateAvatar.DisplayName = teammate.Name;
            _teammateAvatar.Source = teammate.Source;
        }
        if (_teammateRing is not null)
        {
            _teammateRing.IsVisible = IsCollaboratorVisible && !string.IsNullOrWhiteSpace(teammate.Name);
        }
        if (_agentFlight is not null)
        {
            _agentFlight.Pointer.Label = AgentName;
        }
        if (_teammateFlight is not null)
        {
            _teammateFlight.Pointer.Label = AgentCursorText.FirstName(teammate.Name);
            _teammateFlight.Pointer.Initials = AgentCursorText.Initials(teammate.Name);
            if (!TeammateShown)
            {
                _teammateFlight.SetShown(false);
            }
        }
        RefreshReviewText();
    }

    /// <summary>Whether the teammate is in the document.</summary>
    internal bool TeammateShown => IsCollaboratorVisible && !string.IsNullOrWhiteSpace(EffectiveCollaborator.Name);

    /// <summary>The header button and the review bar for the current phase.</summary>
    private void RefreshChrome()
    {
        var phase = Phase;
        var busy = phase is AgentCursorPhase.Thinking or AgentCursorPhase.Editing;
        PseudoClasses.Set(BusyPseudoClass, busy);
        PseudoClasses.Set(ReviewPseudoClass, _barShown);
        if (_document is not null)
        {
            // Arc marks the document aria-busy while the agent edits; the closest Avalonia has is the help text.
            AutomationProperties.SetHelpText(_document, busy ? Labels.Reading(AgentName) : null);
        }
        if (_action is null)
        {
            return;
        }
        string? text = phase switch
        {
            AgentCursorPhase.Idle => AutoPlay ? null : Labels.Ask(AgentName),
            AgentCursorPhase.Thinking or AgentCursorPhase.Editing => Labels.Stop,
            AgentCursorPhase.Review => null,
            AgentCursorPhase.Error => Labels.Retry,
            _ => Labels.RunAgain,
        };
        _action.IsVisible = text is not null;
        if (text is not null && !Equals(_action.Content, text))
        {
            _action.Content = text;
        }
        if (_action is Button arcButton)
        {
            arcButton.Variant = phase == AgentCursorPhase.Idle ? ButtonVariant.Primary : ButtonVariant.Secondary;
        }
        if (_undo is not null)
        {
            _undo.Content = Labels.Undo;
        }
        if (_accept is not null)
        {
            _accept.Content = Labels.Accept;
        }
        if (_reviewBar is not null)
        {
            AutomationProperties.SetName(_reviewBar, Labels.Review);
        }
        RefreshReviewText();
    }

    private void RefreshReviewText()
    {
        if (_reviewText is null)
        {
            return;
        }
        var count = Math.Max(_pendingChanges, _shownChanges);
        var text = IsCompact ? Labels.Changes(count) : Labels.Changes(count) + " " + Labels.By(AgentName);
        if (_reviewText.Text != text)
        {
            _reviewText.Text = text;
        }
    }

    /// <summary>The status line for the current phase (and the current edit while editing).</summary>
    private void RefreshStatus()
    {
        var phase = Phase;
        (string Text, Geometry Glyph, ArcDesignTokenKind? Tone, bool Working) status = phase switch
        {
            AgentCursorPhase.Idle => (Labels.Idle(AgentName), EffectiveAgent.Mark ?? ArcIcons.Sparkles, null, false),
            AgentCursorPhase.Thinking => (Labels.Reading(AgentName), ArcIcons.ScanText, null, true),
            AgentCursorPhase.Editing => (_editStatus ?? Labels.Rewriting, ArcIcons.PenLine, null, true),
            AgentCursorPhase.Review => (Labels.Ready(_pendingChanges), ArcIcons.ListChecks, null, false),
            AgentCursorPhase.Accepted => (_acceptedBy is { } who ? Labels.AcceptedBy(who) : Labels.Accepted, ArcIcons.Check, ArcDesignTokenKind.Success, false),
            AgentCursorPhase.Reverted => (Labels.Reverted, ArcIcons.Undo2, ArcDesignTokenKind.TextMuted, false),
            AgentCursorPhase.Stopped => (Labels.Stopped, ArcIcons.Square, ArcDesignTokenKind.TextMuted, false),
            AgentCursorPhase.Empty => (Labels.Empty, ArcIcons.CircleCheck, ArcDesignTokenKind.TextMuted, false),
            _ => (Labels.Error, ArcIcons.CircleAlert, ArcDesignTokenKind.Danger, false),
        };
        StatusText = status.Text;
        StatusGlyph = status.Glyph;
        StatusShimmers = status.Working;
        var key = status.Text + "|" + status.Tone?.ToString() + "|" + status.Working;
        if (_status is null || key == _statusKey)
        {
            return;
        }
        _statusKey = key;
        var glyph = new ArcGlyph { Data = status.Glyph, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        glyph.Bind(ArcGlyph.SizeProperty, glyph.GetResourceObservable(AgentCursorTokenKind.StatusGlyphSize));
        if (status.Tone is { } tone)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, tone);
        }
        else
        {
            glyph.Foreground = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(AgentColor);
        }
        var label = new ToolCallText
        {
            Text = status.Text,
            IsShimmering = status.Working,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        ArcTokens.Bind(label, ToolCallText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(label, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { glyph, label } };
        row.Bind(StackPanel.SpacingProperty, row.GetResourceObservable(AgentCursorTokenKind.StatusGap));
        _statusGlyph = glyph;
        _statusGlyphTinted = status.Tone is null;
        _status.Content = row;
    }

    private void Announce(string text)
    {
        _announcer?.Announce(EffectiveSimulated ? text + ". " + Labels.Simulated : text);
    }

    private void SetPhase(AgentCursorPhase phase)
    {
        var old = _phase;
        if (old == phase)
        {
            RefreshStatus();
            return;
        }
        Phase = phase;
        RefreshChrome();
        RefreshStatus();
        if (StatusText is { } text)
        {
            Announce(text);
        }
        RaiseEvent(new AgentCursorPhaseChangedEventArgs(PhaseChangedEvent, old, phase));
    }
}
