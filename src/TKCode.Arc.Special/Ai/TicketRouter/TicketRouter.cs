using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A support ticket router card (Arc Ticket router, design reference). Messages from several channels (email, chat,
/// messages, help desk) arrive one at a time, an AI classifier labels each with an intent, a sentiment and a confidence,
/// and the ticket travels into a team lane where it gets the next assignee in turn. Tickets below
/// <see cref="ReviewThreshold"/> wait in the review lane with the classifier's pick as a suggestion; one click routes them
/// there.
/// <para>
/// Give it <see cref="Tickets"/> and a <see cref="Classifier"/> (your model call); handle <see cref="TicketRouted"/> to assign
/// in your help desk. With no tickets of its own it plays its <see cref="Simulation"/>, an inbox you supply, with a
/// simulated classifier and loops; with neither it rests on "No tickets yet" in empty lanes. About two seconds
/// per ticket at <see cref="Speed"/> 1; the flow pauses with <see cref="IsPaused"/>, off screen and when unloaded, and
/// each pause cancels a running classifier call.
/// </para>
/// </summary>
[TemplatePart(MarkPart, typeof(ArcGlyph))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ModelPart, typeof(TextBlock))]
[TemplatePart(StatusTextPart, typeof(TextBlock))]
[TemplatePart(PausePart, typeof(AvaloniaButton))]
[TemplatePart(PauseGlyphPart, typeof(ArcGlyph))]
[TemplatePart(PauseTextPart, typeof(TextBlock))]
[TemplatePart(StagePart, typeof(Panel))]
[TemplatePart(TicketHostPart, typeof(Panel))]
[TemplatePart(TicketPart, typeof(TicketRouterTicketCard))]
[TemplatePart(NoticePart, typeof(TicketRouterNotice))]
[TemplatePart(StripPart, typeof(Grid))]
[TemplatePart(ClassifyingPart, typeof(ToolCallText))]
[TemplatePart(IntentPart, typeof(TicketRouterChip))]
[TemplatePart(SentimentPart, typeof(TicketRouterChip))]
[TemplatePart(ConfidencePart, typeof(TicketRouterMeter))]
[TemplatePart(RoutePart, typeof(Panel))]
[TemplatePart(RouteChipPart, typeof(TicketRouterChip))]
[TemplatePart(ErrorPart, typeof(Panel))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(RetryPart, typeof(AvaloniaButton))]
[TemplatePart(SendToReviewPart, typeof(AvaloniaButton))]
[TemplatePart(ConnectorPart, typeof(TicketRouterConnector))]
[TemplatePart(LanesPart, typeof(TicketRouterLanePanel))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(PausedPseudoClass, IdlePseudoClass, NarrowPseudoClass, CompactPseudoClass)]
public partial class TicketRouter : TemplatedControl
{
    internal const string MarkPart = "PART_Mark";
    internal const string TitlePart = "PART_Title";
    internal const string ModelPart = "PART_Model";
    internal const string StatusTextPart = "PART_StatusText";
    internal const string PausePart = "PART_Pause";
    internal const string PauseGlyphPart = "PART_PauseGlyph";
    internal const string PauseTextPart = "PART_PauseText";
    internal const string StagePart = "PART_Stage";
    internal const string TicketHostPart = "PART_TicketHost";
    internal const string TicketPart = "PART_Ticket";
    internal const string NoticePart = "PART_Notice";
    internal const string StripPart = "PART_Strip";
    internal const string ClassifyingPart = "PART_Classifying";
    internal const string IntentPart = "PART_Intent";
    internal const string SentimentPart = "PART_Sentiment";
    internal const string ConfidencePart = "PART_Confidence";
    internal const string RoutePart = "PART_Route";
    internal const string RouteChipPart = "PART_RouteChip";
    internal const string ErrorPart = "PART_Error";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string RetryPart = "PART_Retry";
    internal const string SendToReviewPart = "PART_SendToReview";
    internal const string ConnectorPart = "PART_Connector";
    internal const string LanesPart = "PART_Lanes";
    internal const string LivePart = "PART_Live";

    internal const string PausedPseudoClass = ":paused";
    internal const string IdlePseudoClass = ":idle";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Below this content width the lanes fold into a 2 by 2 grid, the connector hides and the labels stack.</summary>
    internal const double NarrowWidth = 600;

    /// <summary>Below this content width the confidence meter and the model name hide; the percentage stays.</summary>
    internal const double CompactWidth = 400;

    /// <summary>The most routed tickets kept in the history.</summary>
    internal const int HistoryCap = 80;

    /// <summary>
    /// The incoming messages, processed in order. Null plays the <see cref="Simulation"/>'s inbox, or rests on
    /// "No tickets yet" without one; append to keep the flow going.
    /// </summary>
    public static readonly StyledProperty<IList<TicketMessage>?> TicketsProperty =
        AvaloniaProperty.Register<TicketRouter, IList<TicketMessage>?>(nameof(Tickets));

    /// <summary>
    /// The lanes left to right. Null uses the simulation's lanes while it plays, else the default lanes: Billing,
    /// Engineering, Success and Needs review (the ids <see cref="DefaultRoute"/> picks), with no assignees.
    /// </summary>
    public static readonly StyledProperty<IList<TicketLane>?> LanesProperty =
        AvaloniaProperty.Register<TicketRouter, IList<TicketLane>?>(nameof(Lanes));

    /// <summary>Tickets already in the lanes on first show, newest first. Null uses the simulation's while it plays.</summary>
    public static readonly StyledProperty<IList<RoutedTicket>?> InitialRoutedProperty =
        AvaloniaProperty.Register<TicketRouter, IList<RoutedTicket>?>(nameof(InitialRouted));

    /// <summary>
    /// Your classifier: return the intent, sentiment and confidence, or throw to show the error state with Retry and Send to
    /// review. The token cancels on pause, off screen and unload. Null reads each message's <see cref="TicketMessage.Sample"/>.
    /// </summary>
    public static readonly StyledProperty<Func<TicketMessage, CancellationToken, Task<TicketClassification>>?> ClassifierProperty =
        AvaloniaProperty.Register<TicketRouter, Func<TicketMessage, CancellationToken, Task<TicketClassification>>?>(nameof(Classifier));

    /// <summary>Picks the lane id for a classification; null uses <see cref="DefaultRoute"/>. An unknown id falls back to the first team lane.</summary>
    public static readonly StyledProperty<Func<TicketMessage, TicketClassification, string>?> RouteProperty =
        AvaloniaProperty.Register<TicketRouter, Func<TicketMessage, TicketClassification, string>?>(nameof(Route));

    /// <summary>Confidence below this sends a ticket to the review lane, with the picked lane as its suggestion.</summary>
    public static readonly StyledProperty<double> ReviewThresholdProperty =
        AvaloniaProperty.Register<TicketRouter, double>(nameof(ReviewThreshold), 0.7);

    /// <summary>Starts over when the queue runs out. Null loops a simulation and stops after your own tickets ("All caught up").</summary>
    public static readonly StyledProperty<bool?> LoopProperty =
        AvaloniaProperty.Register<TicketRouter, bool?>(nameof(Loop));

    /// <summary>Whether the flow is paused (two-way); the Pause and Resume button sets it.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<TicketRouter, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Rows shown per lane. Narrow cards show at most 2.</summary>
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<TicketRouter, int>(nameof(MaxVisible), 3, coerce: (_, value) => Math.Max(1, value));

    /// <summary>Playback speed multiplier of the flow (1 is about two seconds per ticket).</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<TicketRouter, double>(nameof(Speed), 1,
            coerce: (_, value) => double.IsFinite(value) && value > 0 ? value : 1);

    /// <summary>The card's title.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TicketRouter, string>(nameof(Title), "Ticket routing");

    /// <summary>
    /// The classifier model's name, in the header and the classifying label. Null shows the simulation's while it plays,
    /// and no name otherwise.
    /// </summary>
    public static readonly StyledProperty<string?> ModelProperty =
        AvaloniaProperty.Register<TicketRouter, string?>(nameof(Model));

    /// <summary>The mark before the title (a 24 × 24 stroked glyph); the sparkle by default.</summary>
    public static readonly StyledProperty<Geometry?> ModelMarkProperty =
        AvaloniaProperty.Register<TicketRouter, Geometry?>(nameof(ModelMark));

    /// <summary>
    /// The inbox the router plays on its own while <see cref="Tickets"/> is null: the model's name, the lanes, the messages
    /// with their simulated answers and the tickets already routed, played exactly like your own stream (one ticket about
    /// every two seconds, pausing off screen, looping). Null rests on the empty state instead.
    /// </summary>
    public static readonly StyledProperty<TicketRouterSimulation?> SimulationProperty =
        AvaloniaProperty.Register<TicketRouter, TicketRouterSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<TicketRouterLabels> LabelsProperty =
        AvaloniaProperty.Register<TicketRouter, TicketRouterLabels>(nameof(Labels), TicketRouterLabels.Default);

    /// <summary>Raised when a ticket lands in a lane, with its classification, lane, assignee and suggested lane. Bubbles.</summary>
    public static readonly RoutedEvent<TicketRoutedEventArgs> TicketRoutedEvent =
        RoutedEvent.Register<TicketRouter, TicketRoutedEventArgs>(nameof(TicketRouted), RoutingStrategies.Bubble);

    /// <summary>Raised when someone confirms a review ticket's suggested lane. Bubbles.</summary>
    public static readonly RoutedEvent<TicketReviewConfirmedEventArgs> ReviewConfirmedEvent =
        RoutedEvent.Register<TicketRouter, TicketReviewConfirmedEventArgs>(nameof(ReviewConfirmed), RoutingStrategies.Bubble);

    /// <summary>Raised when the classifier throws. Bubbles.</summary>
    public static readonly RoutedEvent<TicketClassificationFailedEventArgs> ClassificationFailedEvent =
        RoutedEvent.Register<TicketRouter, TicketClassificationFailedEventArgs>(nameof(ClassificationFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="IsPaused"/> changes, by the Pause and Resume button or by code. Bubbles.</summary>
    public static readonly RoutedEvent<TicketRouterPausedChangedEventArgs> PausedChangedEvent =
        RoutedEvent.Register<TicketRouter, TicketRouterPausedChangedEventArgs>(nameof(PausedChanged), RoutingStrategies.Bubble);

    private ArcGlyph? _mark;
    private TextBlock? _title;
    private TextBlock? _model;
    private TextBlock? _statusText;
    private AvaloniaButton? _pause;
    private ArcGlyph? _pauseGlyph;
    private TextBlock? _pauseText;
    private Panel? _stage;
    private Panel? _ticketHost;
    private TicketRouterTicketCard? _ticket;
    private TicketRouterNotice? _notice;
    private Grid? _strip;
    private ToolCallText? _classifying;
    private TicketRouterChip? _intent;
    private TicketRouterChip? _sentiment;
    private TicketRouterMeter? _confidence;
    private Panel? _route;
    private TicketRouterChip? _routeChip;
    private Panel? _error;
    private TextBlock? _errorText;
    private AvaloniaButton? _retry;
    private AvaloniaButton? _sendToReview;
    private TicketRouterConnector? _connector;
    private TicketRouterLanePanel? _lanes;
    private ExplainerAnnouncer? _live;
    private TicketRouterFade? _ticketFade;
    private TicketRouterFade[] _labelFades = [];
    private INotifyCollectionChanged? _observed;
    private bool _narrow;
    private bool _compact;

    /// <inheritdoc cref="TicketsProperty"/>
    public IList<TicketMessage>? Tickets { get => GetValue(TicketsProperty); set => SetValue(TicketsProperty, value); }

    /// <inheritdoc cref="LanesProperty"/>
    public IList<TicketLane>? Lanes { get => GetValue(LanesProperty); set => SetValue(LanesProperty, value); }

    /// <inheritdoc cref="InitialRoutedProperty"/>
    public IList<RoutedTicket>? InitialRouted { get => GetValue(InitialRoutedProperty); set => SetValue(InitialRoutedProperty, value); }

    /// <inheritdoc cref="ClassifierProperty"/>
    public Func<TicketMessage, CancellationToken, Task<TicketClassification>>? Classifier
    {
        get => GetValue(ClassifierProperty);
        set => SetValue(ClassifierProperty, value);
    }

    /// <inheritdoc cref="RouteProperty"/>
    public Func<TicketMessage, TicketClassification, string>? Route { get => GetValue(RouteProperty); set => SetValue(RouteProperty, value); }

    /// <inheritdoc cref="ReviewThresholdProperty"/>
    public double ReviewThreshold { get => GetValue(ReviewThresholdProperty); set => SetValue(ReviewThresholdProperty, value); }

    /// <inheritdoc cref="LoopProperty"/>
    public bool? Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="MaxVisibleProperty"/>
    public int MaxVisible { get => GetValue(MaxVisibleProperty); set => SetValue(MaxVisibleProperty, value); }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public string? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public TicketRouterSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="ModelMarkProperty"/>
    public Geometry? ModelMark { get => GetValue(ModelMarkProperty); set => SetValue(ModelMarkProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TicketRouterLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="TicketRoutedEvent"/>
    public event EventHandler<TicketRoutedEventArgs>? TicketRouted
    {
        add => AddHandler(TicketRoutedEvent, value);
        remove => RemoveHandler(TicketRoutedEvent, value);
    }

    /// <inheritdoc cref="ReviewConfirmedEvent"/>
    public event EventHandler<TicketReviewConfirmedEventArgs>? ReviewConfirmed
    {
        add => AddHandler(ReviewConfirmedEvent, value);
        remove => RemoveHandler(ReviewConfirmedEvent, value);
    }

    /// <inheritdoc cref="ClassificationFailedEvent"/>
    public event EventHandler<TicketClassificationFailedEventArgs>? ClassificationFailed
    {
        add => AddHandler(ClassificationFailedEvent, value);
        remove => RemoveHandler(ClassificationFailedEvent, value);
    }

    /// <inheritdoc cref="PausedChangedEvent"/>
    public event EventHandler<TicketRouterPausedChangedEventArgs>? PausedChanged
    {
        add => AddHandler(PausedChangedEvent, value);
        remove => RemoveHandler(PausedChangedEvent, value);
    }

    /// <summary>The tickets in the lanes, newest first (at most 80).</summary>
    public IReadOnlyList<RoutedTicket> RoutedTickets => _history;

    /// <summary>The id of the default Billing lane, where <see cref="DefaultRoute"/> sends billing and refund tickets.</summary>
    public const string BillingLaneId = "billing";

    /// <summary>The id of the default Engineering lane, where <see cref="DefaultRoute"/> sends bugs.</summary>
    public const string EngineeringLaneId = "engineering";

    /// <summary>The id of the default Success lane, where <see cref="DefaultRoute"/> sends feature requests and questions.</summary>
    public const string SuccessLaneId = "success";

    /// <summary>The id of the default Needs review lane.</summary>
    public const string ReviewLaneId = "review";

    /// <summary>
    /// The lane the default route picks: billing and refund to <see cref="BillingLaneId"/>, bugs to
    /// <see cref="EngineeringLaneId"/>, feature requests and questions to <see cref="SuccessLaneId"/>.
    /// </summary>
    public static string DefaultRoute(TicketMessage message, TicketClassification classification) => classification.Intent switch
    {
        TicketIntent.Billing or TicketIntent.Refund => BillingLaneId,
        TicketIntent.Bug => EngineeringLaneId,
        _ => SuccessLaneId,
    };

    internal bool IsNarrow => _narrow;

    internal bool IsCompact => _compact;

    internal TicketRouterTicketCard? TicketCard => _ticket;

    internal TicketRouterNotice? Notice => _notice;

    internal TicketRouterChip? IntentChip => _intent;

    internal TicketRouterChip? SentimentChip => _sentiment;

    internal TicketRouterMeter? ConfidenceMeter => _confidence;

    internal TicketRouterChip? RouteChip => _routeChip;

    internal ToolCallText? ClassifyingText => _classifying;

    internal Panel? ErrorPanel => _error;

    internal AvaloniaButton? PauseButton => _pause;

    internal AvaloniaButton? RetryButton => _retry;

    internal AvaloniaButton? SendToReviewButton => _sendToReview;

    internal TicketRouterConnector? Connector => _connector;

    internal TicketRouterLanePanel? LanePanel => _lanes;

    internal TextBlock? StatusText => _statusText;

    internal TextBlock? ModelText => _model;

    internal string? Announcement => _live?.Announcement;

    internal IReadOnlyList<TicketRouterLaneView> LaneViews => _laneViews;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _mark = e.NameScope.Find<ArcGlyph>(MarkPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _model = e.NameScope.Find<TextBlock>(ModelPart);
        _statusText = e.NameScope.Find<TextBlock>(StatusTextPart);
        _pause = e.NameScope.Find<AvaloniaButton>(PausePart);
        _pauseGlyph = e.NameScope.Find<ArcGlyph>(PauseGlyphPart);
        _pauseText = e.NameScope.Find<TextBlock>(PauseTextPart);
        _stage = e.NameScope.Find<Panel>(StagePart);
        _ticketHost = e.NameScope.Find<Panel>(TicketHostPart);
        _ticket = e.NameScope.Find<TicketRouterTicketCard>(TicketPart);
        _notice = e.NameScope.Find<TicketRouterNotice>(NoticePart);
        _strip = e.NameScope.Find<Grid>(StripPart);
        _classifying = e.NameScope.Find<ToolCallText>(ClassifyingPart);
        _intent = e.NameScope.Find<TicketRouterChip>(IntentPart);
        _sentiment = e.NameScope.Find<TicketRouterChip>(SentimentPart);
        _confidence = e.NameScope.Find<TicketRouterMeter>(ConfidencePart);
        _route = e.NameScope.Find<Panel>(RoutePart);
        _routeChip = e.NameScope.Find<TicketRouterChip>(RouteChipPart);
        _error = e.NameScope.Find<Panel>(ErrorPart);
        _errorText = e.NameScope.Find<TextBlock>(ErrorTextPart);
        _retry = e.NameScope.Find<AvaloniaButton>(RetryPart);
        _sendToReview = e.NameScope.Find<AvaloniaButton>(SendToReviewPart);
        _connector = e.NameScope.Find<TicketRouterConnector>(ConnectorPart);
        _lanes = e.NameScope.Find<TicketRouterLanePanel>(LanesPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);

        if (_pause is not null)
        {
            _pause.Click += OnPauseClick;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_sendToReview is not null)
        {
            _sendToReview.Click += OnSendToReviewClick;
        }
        _ticketFade = _ticketHost is null ? null : new TicketRouterFade(_ticketHost, true);
        Control?[] labels = [_intent, _sentiment, _confidence, _route];
        _labelFades = labels.Where(c => c is not null).Select(c => new TicketRouterFade(c!, false)).ToArray();
        PseudoClasses.Set(PausedPseudoClass, IsPaused);
        ApplyBreakpoints(Bounds.Width, force: true);
        BuildLanes();
        Restart();
    }

    private void Unhook()
    {
        if (_pause is not null)
        {
            _pause.Click -= OnPauseClick;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_sendToReview is not null)
        {
            _sendToReview.Click -= OnSendToReviewClick;
        }
        foreach (var view in _laneViews)
        {
            view.ReviewPressed -= OnReviewPressed;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPausedProperty)
        {
            PseudoClasses.Set(PausedPseudoClass, IsPaused);
            OnPausedChanged();
            RaiseEvent(new TicketRouterPausedChangedEventArgs(PausedChangedEvent, IsPaused));
        }
        else if (change.Property == TicketsProperty)
        {
            Observe(Tickets as INotifyCollectionChanged);
            BuildLanes();
            Restart();
        }
        else if (change.Property == SimulationProperty)
        {
            // Another inbox (or none): the classifier call is cancelled and the flow starts over with its lanes.
            BuildLanes();
            Restart();
        }
        else if (change.Property == LanesProperty)
        {
            BuildLanes();
            Restart();
        }
        else if (change.Property == InitialRoutedProperty || change.Property == LoopProperty && _phase == TicketRouterPhase.CaughtUp)
        {
            Restart();
        }
        else if (change.Property == MaxVisibleProperty || change.Property == ReviewThresholdProperty)
        {
            SyncLanes(animate: false);
        }
        else if (change.Property == LabelsProperty || change.Property == TitleProperty || change.Property == ModelProperty
                 || change.Property == ModelMarkProperty)
        {
            if (change.Property == LabelsProperty)
            {
                // The default lanes are named by the labels.
                _defaultLanes = null;
                if (UsesDefaultLanes)
                {
                    BuildLanes();
                }
            }
            RefreshTexts();
            SyncLanes(animate: false, relabel: true);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyBreakpoints(e.NewSize.Width, force: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLoop();
    }

    /// <summary>
    /// Folds the layout at 600px of content (2 by 2 lanes, no connector, labels above the route target, at most two rows per
    /// lane) and at 400px (no confidence meter, no model name).
    /// </summary>
    private void ApplyBreakpoints(double width, bool force)
    {
        if (width <= 0)
        {
            if (!force)
            {
                return;
            }
            width = 840;
        }
        var content = width - Padding.Left - Padding.Right;
        var narrow = content < NarrowWidth;
        var compact = content < CompactWidth;
        if (!force && narrow == _narrow && compact == _compact)
        {
            return;
        }
        var foldsRows = narrow != _narrow;
        _narrow = narrow;
        _compact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_lanes is not null)
        {
            _lanes.Columns = narrow ? 2 : 4;
        }
        if (_connector is not null)
        {
            _connector.IsVisible = !narrow;
        }
        if (_route is not null)
        {
            Grid.SetRow(_route, narrow ? 1 : 0);
            Grid.SetColumn(_route, narrow ? 0 : 1);
        }
        if (_confidence is not null)
        {
            _confidence.ShowsBar = !compact;
        }
        if (_model is not null)
        {
            _model.IsVisible = !compact && !string.IsNullOrEmpty(EffectiveModel);
        }
        if (_classifying is not null)
        {
            _classifying.Text = ClassifyingLabel();
        }
        if (foldsRows || force)
        {
            SyncLanes(animate: false);
        }
    }

    private string ClassifyingLabel() => _compact ? Labels.Classifying : Labels.ClassifyingWith(EffectiveModel ?? string.Empty);

    private void RefreshTexts()
    {
        var labels = Labels;
        if (_mark is not null)
        {
            _mark.Data = ModelMark ?? ArcIcons.Sparkles;
        }
        if (_title is not null)
        {
            _title.Text = Title;
        }
        if (_model is not null)
        {
            _model.Text = EffectiveModel;
            _model.IsVisible = !_compact && !string.IsNullOrEmpty(EffectiveModel);
        }
        AutomationProperties.SetName(this, Title);
        if (_classifying is not null)
        {
            _classifying.Text = ClassifyingLabel();
        }
        if (_retry is not null)
        {
            _retry.Content = labels.Retry;
        }
        if (_sendToReview is not null)
        {
            _sendToReview.Content = labels.SendToReview;
        }
        if (_errorText is not null)
        {
            _errorText.Text = labels.Failed;
        }
        UpdateStatus();
        UpdateStageLabels();
    }

    /// <summary>The header status and the Pause / Resume button.</summary>
    private void UpdateStatus()
    {
        var labels = Labels;
        var idle = _phase is TicketRouterPhase.CaughtUp or TicketRouterPhase.Empty;
        PseudoClasses.Set(IdlePseudoClass, idle && !IsPaused);
        if (_statusText is not null)
        {
            _statusText.Text = IsPaused ? labels.Paused
                : _phase == TicketRouterPhase.Empty ? labels.Empty
                : _phase == TicketRouterPhase.CaughtUp ? labels.CaughtUp
                : labels.Routing;
        }
        if (_pauseGlyph is not null)
        {
            _pauseGlyph.Data = IsPaused ? ArcIcons.Play : ArcIcons.Pause;
        }
        if (_pauseText is not null)
        {
            _pauseText.Text = IsPaused ? labels.Resume : labels.Pause;
        }
        if (_pause is not null)
        {
            AutomationProperties.SetName(_pause, IsPaused ? labels.Resume : labels.Pause);
        }
    }

    private void OnPauseClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsPausedProperty, !IsPaused);

    private void OnRetryClick(object? sender, RoutedEventArgs e) => Retry();

    private void OnSendToReviewClick(object? sender, RoutedEventArgs e) => SendToReview();

    private void OnReviewPressed(RoutedTicket ticket) => ConfirmReview(ticket);

    private void Observe(INotifyCollectionChanged? collection)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnTicketsChanged;
        }
        _observed = collection;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnTicketsChanged;
        }
    }

    private void OnTicketsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Restart();
            return;
        }
        if (_phase is TicketRouterPhase.CaughtUp or TicketRouterPhase.Empty)
        {
            // Appended tickets resume a flow that ran out.
            Next();
        }
    }

    private void Announce(string text)
    {
        if (_live is null)
        {
            return;
        }
        var said = Classifier is null ? text + ". " + Labels.Simulated : text;
        if (said != _live.Announcement)
        {
            _live.Announce(said);
        }
    }
}
