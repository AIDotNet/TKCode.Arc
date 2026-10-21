using System.Collections.Specialized;
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
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A considerate cancellation flow in one card: it asks why, offers an alternative that fits the reason, shows what
/// ends and when, and asks the person to hold a button to confirm; the last step thanks them or lets them reactivate.
/// A step bar on top shows where they are. Steps slide in the direction of travel while the card's height follows on a
/// spring; each step's controls are built once and reused. Enter runs the step's main action, Alt+Left goes back, and
/// focus moves to the new step's title after every change.
/// </summary>
[TemplatePart(StagePart, typeof(ArcContentSwap))]
[TemplatePart(StepStorePart, typeof(Panel))]
[TemplatePart(BackButtonPart, typeof(AvaloniaButton))]
[TemplatePart(StepBarPart, typeof(Grid))]
[TemplatePart(ReasonStepPart, typeof(Control))]
[TemplatePart(OfferStepPart, typeof(Control))]
[TemplatePart(ConfirmStepPart, typeof(Control))]
[TemplatePart(ResultStepPart, typeof(Control))]
[PseudoClasses(NarrowPseudoClass)]
public class CancelFlow : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string StepStorePart = "PART_StepStore";
    public const string BackButtonPart = "PART_BackButton";
    public const string StepBarPart = "PART_StepBar";
    public const string ReasonStepPart = "PART_ReasonStep";
    public const string OfferStepPart = "PART_OfferStep";
    public const string ConfirmStepPart = "PART_ConfirmStep";
    public const string ResultStepPart = "PART_ResultStep";
    public const string NarrowPseudoClass = ":narrow";

    internal const string HeaderHostPart = "PART_HeaderHost";
    internal const string BackHostPart = "PART_BackHost";
    internal const string ReasonSegmentPart = "PART_ReasonSegment";
    internal const string OfferSegmentPart = "PART_OfferSegment";
    internal const string ConfirmSegmentPart = "PART_ConfirmSegment";
    internal const string StepCounterPart = "PART_StepCounter";
    internal const string ReasonTitlePart = "PART_ReasonTitle";
    internal const string ReasonDescriptionPart = "PART_ReasonDescription";
    internal const string ReasonsPart = "PART_Reasons";
    internal const string FeedbackHostPart = "PART_FeedbackHost";
    internal const string FeedbackPart = "PART_Feedback";
    internal const string ContinueButtonPart = "PART_ContinueButton";
    internal const string KeepButtonPart = "PART_KeepButton";
    internal const string OfferIconPart = "PART_OfferIcon";
    internal const string OfferTitlePart = "PART_OfferTitle";
    internal const string OfferDescriptionPart = "PART_OfferDescription";
    internal const string AcceptButtonPart = "PART_AcceptButton";
    internal const string DeclineButtonPart = "PART_DeclineButton";
    internal const string ConfirmTitlePart = "PART_ConfirmTitle";
    internal const string ConfirmDescriptionPart = "PART_ConfirmDescription";
    internal const string LossesBoxPart = "PART_LossesBox";
    internal const string LossesPart = "PART_Losses";
    internal const string HoldButtonPart = "PART_HoldButton";
    internal const string ConfirmKeepButtonPart = "PART_ConfirmKeepButton";
    internal const string ResultGlyphPart = "PART_ResultGlyph";
    internal const string ResultTitlePart = "PART_ResultTitle";
    internal const string ResultDescriptionPart = "PART_ResultDescription";
    internal const string ReactivateButtonPart = "PART_ReactivateButton";
    internal const string DoneButtonPart = "PART_DoneButton";

    private const string DoneClass = "done";
    private const string CurrentClass = "current";

    // Lucide "gift" (ISC); ArcIcons has no gift. The rect is written as a path.
    private static Geometry GiftIcon => ArcIcons.Parse(
        "M4 8h16a1 1 0 0 1 1 1v2a1 1 0 0 1 -1 1h-16a1 1 0 0 1 -1 -1v-2a1 1 0 0 1 1 -1z M12 8v13 " +
        "M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7 " +
        "M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5");

    #region Properties

    /// <summary>The plan's name as people know it, such as "Studio"; used in the titles and sentences.</summary>
    public static readonly StyledProperty<string?> PlanNameProperty =
        AvaloniaProperty.Register<CancelFlow, string?>(nameof(PlanName));

    /// <summary>The last day of paid access after canceling (usually the renewal date); null when access ends at once.</summary>
    public static readonly StyledProperty<DateTime?> AccessEndsOnProperty =
        AvaloniaProperty.Register<CancelFlow, DateTime?>(nameof(AccessEndsOn));

    /// <summary>
    /// The reasons to choose from, written inline or bound. Empty (the default) uses the five reasons in
    /// <see cref="CancelFlowLabels.DefaultReasons"/>.
    /// </summary>
    public static readonly DirectProperty<CancelFlow, ICollection<CancelFlowReason>> ReasonsProperty =
        AvaloniaProperty.RegisterDirect<CancelFlow, ICollection<CancelFlowReason>>(nameof(Reasons), o => o.Reasons, (o, v) => o.Reasons = v);

    /// <summary>The offer for reasons without one of their own; null skips the offer step for them.</summary>
    public static readonly StyledProperty<CancelFlowOffer?> DefaultOfferProperty =
        AvaloniaProperty.Register<CancelFlow, CancelFlowOffer?>(nameof(DefaultOffer));

    /// <summary>What ends with the plan, listed on the confirmation step (for example "Shared projects become read-only").</summary>
    public static readonly DirectProperty<CancelFlow, ICollection<string>> LossesProperty =
        AvaloniaProperty.RegisterDirect<CancelFlow, ICollection<string>>(nameof(Losses), o => o.Losses, (o, v) => o.Losses = v);

    /// <summary>The step on screen: Reason, Offer, Confirm, Canceled or Retained. Changes through the flow's buttons, <see cref="GoBack"/> and <see cref="Reset"/>.</summary>
    public static readonly DirectProperty<CancelFlow, CancelFlowStep> StepProperty =
        AvaloniaProperty.RegisterDirect<CancelFlow, CancelFlowStep>(nameof(Step), o => o.Step);

    /// <summary>The chosen reason (two-way); Continue is disabled while it is null.</summary>
    public static readonly StyledProperty<CancelFlowReason?> SelectedReasonProperty =
        AvaloniaProperty.Register<CancelFlow, CancelFlowReason?>(nameof(SelectedReason), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the person wrote in the optional feedback field (two-way); null when empty.</summary>
    public static readonly StyledProperty<string?> FeedbackProperty =
        AvaloniaProperty.Register<CancelFlow, string?>(nameof(Feedback), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The built-in text and date culture; replace the whole object to localize the flow.</summary>
    public static readonly StyledProperty<CancelFlowLabels> LabelsProperty =
        AvaloniaProperty.Register<CancelFlow, CancelFlowLabels>(nameof(Labels), new CancelFlowLabels());

    /// <summary>Card (default) draws the flow's own frame; Plain drops it for a flow inside a dialog or another card.</summary>
    public static readonly StyledProperty<CancelFlowAppearance> AppearanceProperty =
        AvaloniaProperty.Register<CancelFlow, CancelFlowAppearance>(nameof(Appearance));

    /// <summary>Below this width the buttons stack and the padding tightens (set by the theme from the token).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<CancelFlow, double>(nameof(NarrowWidth), 440);

    /// <inheritdoc cref="PlanNameProperty"/>
    public string? PlanName
    {
        get => GetValue(PlanNameProperty);
        set => SetValue(PlanNameProperty, value);
    }

    /// <inheritdoc cref="AccessEndsOnProperty"/>
    public DateTime? AccessEndsOn
    {
        get => GetValue(AccessEndsOnProperty);
        set => SetValue(AccessEndsOnProperty, value);
    }

    private ICollection<CancelFlowReason> _reasonList = new AvaloniaList<CancelFlowReason>();

    /// <inheritdoc cref="ReasonsProperty"/>
    public ICollection<CancelFlowReason> Reasons
    {
        get => _reasonList;
        set
        {
            var old = _reasonList;
            if (SetAndRaise(ReasonsProperty, ref _reasonList, value ?? new AvaloniaList<CancelFlowReason>()))
            {
                if (_isAttached)
                {
                    Unsubscribe(old, OnReasonsChanged);
                    Subscribe(_reasonList, OnReasonsChanged);
                }
                RebuildReasonOptions();
            }
        }
    }

    /// <inheritdoc cref="DefaultOfferProperty"/>
    public CancelFlowOffer? DefaultOffer
    {
        get => GetValue(DefaultOfferProperty);
        set => SetValue(DefaultOfferProperty, value);
    }

    private ICollection<string> _lossList = new AvaloniaList<string>();

    /// <inheritdoc cref="LossesProperty"/>
    public ICollection<string> Losses
    {
        get => _lossList;
        set
        {
            var old = _lossList;
            if (SetAndRaise(LossesProperty, ref _lossList, value ?? new AvaloniaList<string>()))
            {
                if (_isAttached)
                {
                    Unsubscribe(old, OnLossesChanged);
                    Subscribe(_lossList, OnLossesChanged);
                }
                if (_losses is not null)
                {
                    _losses.ItemsSource = _lossList;
                }
                UpdateTexts();
            }
        }
    }

    private CancelFlowStep _step;

    /// <inheritdoc cref="StepProperty"/>
    public CancelFlowStep Step
    {
        get => _step;
        private set => SetAndRaise(StepProperty, ref _step, value);
    }

    /// <inheritdoc cref="SelectedReasonProperty"/>
    public CancelFlowReason? SelectedReason
    {
        get => GetValue(SelectedReasonProperty);
        set => SetValue(SelectedReasonProperty, value);
    }

    /// <inheritdoc cref="FeedbackProperty"/>
    public string? Feedback
    {
        get => GetValue(FeedbackProperty);
        set => SetValue(FeedbackProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public CancelFlowLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="AppearanceProperty"/>
    public CancelFlowAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    internal double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    #endregion

    #region Events

    /// <summary>Raised after the step changes, with the old and the new step.</summary>
    public static readonly RoutedEvent<CancelFlowStepChangedEventArgs> StepChangedEvent =
        RoutedEvent.Register<CancelFlow, CancelFlowStepChangedEventArgs>(nameof(StepChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person accepts the offer (the flow moves to Retained), with the reason and the offer.</summary>
    public static readonly RoutedEvent<CancelFlowOfferEventArgs> OfferAcceptedEvent =
        RoutedEvent.Register<CancelFlow, CancelFlowOfferEventArgs>(nameof(OfferAccepted), RoutingStrategies.Bubble);

    /// <summary>Raised when the hold to cancel completes (the flow moves to Canceled), with the reason and the feedback.</summary>
    public static readonly RoutedEvent<CancelFlowCancelEventArgs> CancellationConfirmedEvent =
        RoutedEvent.Register<CancelFlow, CancelFlowCancelEventArgs>(nameof(CancellationConfirmed), RoutingStrategies.Bubble);

    /// <summary>Raised by "Keep my plan" and by "Done" after an accepted offer: the person is staying, so close the flow.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> KeepPlanRequestedEvent =
        RoutedEvent.Register<CancelFlow, RoutedEventArgs>(nameof(KeepPlanRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised by "Reactivate plan" on the canceled step. Unless a handler marks it handled, the flow then calls
    /// <see cref="Reset"/>.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> ReactivateRequestedEvent =
        RoutedEvent.Register<CancelFlow, RoutedEventArgs>(nameof(ReactivateRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="StepChangedEvent"/>
    public event EventHandler<CancelFlowStepChangedEventArgs>? StepChanged
    {
        add => AddHandler(StepChangedEvent, value);
        remove => RemoveHandler(StepChangedEvent, value);
    }

    /// <inheritdoc cref="OfferAcceptedEvent"/>
    public event EventHandler<CancelFlowOfferEventArgs>? OfferAccepted
    {
        add => AddHandler(OfferAcceptedEvent, value);
        remove => RemoveHandler(OfferAcceptedEvent, value);
    }

    /// <inheritdoc cref="CancellationConfirmedEvent"/>
    public event EventHandler<CancelFlowCancelEventArgs>? CancellationConfirmed
    {
        add => AddHandler(CancellationConfirmedEvent, value);
        remove => RemoveHandler(CancellationConfirmedEvent, value);
    }

    /// <inheritdoc cref="KeepPlanRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? KeepPlanRequested
    {
        add => AddHandler(KeepPlanRequestedEvent, value);
        remove => RemoveHandler(KeepPlanRequestedEvent, value);
    }

    /// <inheritdoc cref="ReactivateRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ReactivateRequested
    {
        add => AddHandler(ReactivateRequestedEvent, value);
        remove => RemoveHandler(ReactivateRequestedEvent, value);
    }

    #endregion

    private ArcContentSwap? _stage;
    private PresenceHost? _headerHost;
    private PresenceHost? _backHost;
    private AvaloniaButton? _back;
    private Grid? _stepBar;
    private Border? _reasonSegment;
    private Border? _offerSegment;
    private Border? _confirmSegment;
    private TextBlock? _counter;
    private Control? _reasonView;
    private Control? _offerView;
    private Control? _confirmView;
    private Control? _resultView;
    private TextBlock? _reasonTitle;
    private TextBlock? _reasonDescription;
    private RadioGroup? _reasons;
    private SpringSizeDecorator? _feedbackHost;
    private Textarea? _feedback;
    private AvaloniaButton? _continue;
    private AvaloniaButton? _keep;
    private ArcGlyph? _offerIcon;
    private TextBlock? _offerTitle;
    private TextBlock? _offerDescription;
    private AvaloniaButton? _accept;
    private AvaloniaButton? _decline;
    private TextBlock? _confirmTitle;
    private TextBlock? _confirmDescription;
    private Control? _lossesBox;
    private ItemsControl? _losses;
    private HoldToConfirm? _hold;
    private AvaloniaButton? _confirmKeep;
    private StrokeDrawGlyph? _resultGlyph;
    private TextBlock? _resultTitle;
    private TextBlock? _resultDescription;
    private AvaloniaButton? _reactivate;
    private AvaloniaButton? _done;

    private CancelFlowReason[] _effectiveReasons = [];
    private CancelFlowOffer? _acceptedOffer;
    private int _segmentColumns;
    private bool _syncingReason;
    private bool _syncingFeedback;
    private bool _isAttached;
    private bool _isNarrow;
    private bool _enterDown;
    private bool _suppressEnter;

    public CancelFlow()
    {
        // Tunneling, so Enter reaches the flow before a radio row takes it (a second Enter on the chosen row continues).
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        RebuildReasonOptions();
    }

    protected override Type StyleKeyOverride => typeof(CancelFlow);

    /// <summary>Whether the flow is narrower than its narrow width (buttons stacked).</summary>
    internal bool IsNarrow
    {
        get => _isNarrow;
        private set
        {
            if (_isNarrow != value)
            {
                _isNarrow = value;
                PseudoClasses.Set(NarrowPseudoClass, value);
            }
        }
    }

    /// <summary>The number of segments in the step bar: 3 when the path includes an offer, otherwise 2.</summary>
    internal int StepCount => HasOfferStep() ? 3 : 2;

    #region Public methods

    /// <summary>
    /// Starts over: clears the reason and the feedback and returns to the first step (sliding back). Called by
    /// "Reactivate plan" unless <see cref="ReactivateRequested"/> is handled.
    /// </summary>
    public void Reset()
    {
        _acceptedOffer = null;
        SetCurrentValue(SelectedReasonProperty, null);
        SetCurrentValue(FeedbackProperty, null);
        _hold?.SetCurrentValue(HoldToConfirm.IsConfirmedProperty, false);
        GoTo(CancelFlowStep.Reason, -1);
    }

    /// <summary>
    /// Goes back one step from the offer or the confirmation (the same as the back button and Alt+Left). Returns false
    /// on the other steps, where there is nothing to go back to.
    /// </summary>
    public bool GoBack()
    {
        switch (Step)
        {
            case CancelFlowStep.Offer:
                GoTo(CancelFlowStep.Reason, -1);
                return true;
            case CancelFlowStep.Confirm:
                GoTo(OfferFor(SelectedReason) is null ? CancelFlowStep.Reason : CancelFlowStep.Offer, -1);
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _stage = scope.Find<ArcContentSwap>(StagePart);
        _headerHost = scope.Find<PresenceHost>(HeaderHostPart);
        _backHost = scope.Find<PresenceHost>(BackHostPart);
        _back = scope.Find<AvaloniaButton>(BackButtonPart);
        _stepBar = scope.Find<Grid>(StepBarPart);
        _reasonSegment = scope.Find<Border>(ReasonSegmentPart);
        _offerSegment = scope.Find<Border>(OfferSegmentPart);
        _confirmSegment = scope.Find<Border>(ConfirmSegmentPart);
        _counter = scope.Find<TextBlock>(StepCounterPart);
        _reasonView = scope.Find<Control>(ReasonStepPart);
        _offerView = scope.Find<Control>(OfferStepPart);
        _confirmView = scope.Find<Control>(ConfirmStepPart);
        _resultView = scope.Find<Control>(ResultStepPart);
        _reasonTitle = scope.Find<TextBlock>(ReasonTitlePart);
        _reasonDescription = scope.Find<TextBlock>(ReasonDescriptionPart);
        _reasons = scope.Find<RadioGroup>(ReasonsPart);
        _feedbackHost = scope.Find<SpringSizeDecorator>(FeedbackHostPart);
        _feedback = scope.Find<Textarea>(FeedbackPart);
        _continue = scope.Find<AvaloniaButton>(ContinueButtonPart);
        _keep = scope.Find<AvaloniaButton>(KeepButtonPart);
        _offerIcon = scope.Find<ArcGlyph>(OfferIconPart);
        _offerTitle = scope.Find<TextBlock>(OfferTitlePart);
        _offerDescription = scope.Find<TextBlock>(OfferDescriptionPart);
        _accept = scope.Find<AvaloniaButton>(AcceptButtonPart);
        _decline = scope.Find<AvaloniaButton>(DeclineButtonPart);
        _confirmTitle = scope.Find<TextBlock>(ConfirmTitlePart);
        _confirmDescription = scope.Find<TextBlock>(ConfirmDescriptionPart);
        _lossesBox = scope.Find<Control>(LossesBoxPart);
        _losses = scope.Find<ItemsControl>(LossesPart);
        _hold = scope.Find<HoldToConfirm>(HoldButtonPart);
        _confirmKeep = scope.Find<AvaloniaButton>(ConfirmKeepButtonPart);
        _resultGlyph = scope.Find<StrokeDrawGlyph>(ResultGlyphPart);
        _resultTitle = scope.Find<TextBlock>(ResultTitlePart);
        _resultDescription = scope.Find<TextBlock>(ResultDescriptionPart);
        _reactivate = scope.Find<AvaloniaButton>(ReactivateButtonPart);
        _done = scope.Find<AvaloniaButton>(DoneButtonPart);

        // The step views are declared in the template, so the theme styles them like any template part; they wait in
        // a hidden store and move into the stage one at a time, keeping their state between visits.
        scope.Find<Panel>(StepStorePart)?.Children.Clear();
        _segmentColumns = 0;
        AttachParts();
        if (_losses is not null)
        {
            _losses.ItemsSource = Losses;
        }
        RebuildReasonOptions();
        SyncFeedback();
        UpdateTexts();
        UpdateChrome();
        ShowStep(Step, 1, animate: false);
    }

    private void AttachParts()
    {
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_reasons is not null)
        {
            _reasons.SelectionChanged += OnReasonSelectionChanged;
        }
        if (_feedback is not null)
        {
            _feedback.PropertyChanged += OnFeedbackPropertyChanged;
        }
        if (_continue is not null)
        {
            _continue.Click += OnContinueClick;
        }
        if (_keep is not null)
        {
            _keep.Click += OnKeepPlanClick;
        }
        if (_accept is not null)
        {
            _accept.Click += OnAcceptClick;
        }
        if (_decline is not null)
        {
            _decline.Click += OnDeclineClick;
        }
        if (_hold is not null)
        {
            _hold.Confirmed += OnHoldConfirmed;
        }
        if (_confirmKeep is not null)
        {
            _confirmKeep.Click += OnKeepPlanClick;
        }
        if (_reactivate is not null)
        {
            _reactivate.Click += OnReactivateClick;
        }
        if (_done is not null)
        {
            _done.Click += OnKeepPlanClick;
        }
        if (_resultGlyph is not null)
        {
            _resultGlyph.AttachedToVisualTree += OnResultGlyphAttached;
        }
    }

    private void DetachParts()
    {
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_reasons is not null)
        {
            _reasons.SelectionChanged -= OnReasonSelectionChanged;
        }
        if (_feedback is not null)
        {
            _feedback.PropertyChanged -= OnFeedbackPropertyChanged;
        }
        if (_continue is not null)
        {
            _continue.Click -= OnContinueClick;
        }
        if (_keep is not null)
        {
            _keep.Click -= OnKeepPlanClick;
        }
        if (_accept is not null)
        {
            _accept.Click -= OnAcceptClick;
        }
        if (_decline is not null)
        {
            _decline.Click -= OnDeclineClick;
        }
        if (_hold is not null)
        {
            _hold.Confirmed -= OnHoldConfirmed;
        }
        if (_confirmKeep is not null)
        {
            _confirmKeep.Click -= OnKeepPlanClick;
        }
        if (_reactivate is not null)
        {
            _reactivate.Click -= OnReactivateClick;
        }
        if (_done is not null)
        {
            _done.Click -= OnKeepPlanClick;
        }
        if (_resultGlyph is not null)
        {
            _resultGlyph.AttachedToVisualTree -= OnResultGlyphAttached;
        }
    }

    #endregion

    #region Lifetime and properties

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Subscribe(_reasonList, OnReasonsChanged);
        Subscribe(_lossList, OnLossesChanged);
        RebuildReasonOptions();
        UpdateTexts();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        Unsubscribe(_reasonList, OnReasonsChanged);
        Unsubscribe(_lossList, OnLossesChanged);
        _enterDown = false;
        _suppressEnter = false;
    }

    private static void Subscribe<T>(ICollection<T> list, NotifyCollectionChangedEventHandler handler)
    {
        if (list is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += handler;
        }
    }

    private static void Unsubscribe<T>(ICollection<T> list, NotifyCollectionChangedEventHandler handler)
    {
        if (list is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= handler;
        }
    }

    private void OnReasonsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildReasonOptions();

    private void OnLossesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateTexts();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedReasonProperty)
        {
            SyncSelection();
            UpdateTexts();
        }
        else if (change.Property == FeedbackProperty)
        {
            SyncFeedback();
        }
        else if (change.Property == LabelsProperty)
        {
            RebuildReasonOptions();
            UpdateTexts();
        }
        else if (change.Property == PlanNameProperty || change.Property == AccessEndsOnProperty ||
                 change.Property == DefaultOfferProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == NarrowWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !IsKeyboardFocusWithin)
        {
            // A key-up that lands outside the flow must not leave Enter blocked.
            _enterDown = false;
            _suppressEnter = false;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width comes from the parent, not from the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            IsNarrow = availableSize.Width < NarrowWidth;
        }
        return base.MeasureOverride(availableSize);
    }

    #endregion

    #region Reasons and feedback

    private void RebuildReasonOptions()
    {
        var source = _reasonList.Count > 0 ? (IEnumerable<CancelFlowReason>)_reasonList : Labels.DefaultReasons;
        _effectiveReasons = source.ToArray();
        if (_reasons is null)
        {
            return;
        }
        _syncingReason = true;
        try
        {
            _reasons.ItemsSource = _effectiveReasons.Select(r => new RadioOption(r, r.Title, r.Description)).ToArray();
        }
        finally
        {
            _syncingReason = false;
        }
        SyncSelection();
        UpdateStepBar();
    }

    private void SyncSelection()
    {
        if (_reasons is null)
        {
            return;
        }
        var index = SelectedReason is { } reason ? Array.IndexOf(_effectiveReasons, reason) : -1;
        if (_reasons.SelectedIndex != index)
        {
            _syncingReason = true;
            try
            {
                _reasons.SelectedIndex = index;
            }
            finally
            {
                _syncingReason = false;
            }
        }
    }

    private void OnReasonSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingReason || _reasons is null)
        {
            return;
        }
        var index = _reasons.SelectedIndex;
        SetCurrentValue(SelectedReasonProperty, index >= 0 && index < _effectiveReasons.Length ? _effectiveReasons[index] : null);
    }

    private void SyncFeedback()
    {
        if (_feedback is null)
        {
            return;
        }
        var text = Feedback ?? string.Empty;
        if ((_feedback.Text ?? string.Empty) != text)
        {
            _syncingFeedback = true;
            try
            {
                _feedback.Text = text;
            }
            finally
            {
                _syncingFeedback = false;
            }
        }
    }

    // The text property rather than TextChanged, which is raised later: Feedback stays in step with every edit.
    private void OnFeedbackPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncingFeedback || _feedback is null)
        {
            return;
        }
        var text = _feedback.Text;
        SetCurrentValue(FeedbackProperty, string.IsNullOrEmpty(text) ? null : text);
    }

    #endregion

    #region Flow

    private CancelFlowOffer? OfferFor(CancelFlowReason? reason) => reason?.Offer ?? DefaultOffer;

    /// <summary>Whether the path through the flow includes an offer: for the chosen reason, or for any reason before one is chosen.</summary>
    private bool HasOfferStep()
    {
        if (SelectedReason is { } reason)
        {
            return OfferFor(reason) is not null;
        }
        return DefaultOffer is not null || _effectiveReasons.Any(r => r.Offer is not null);
    }

    private void Continue()
    {
        if (Step != CancelFlowStep.Reason || SelectedReason is not { } reason)
        {
            return;
        }
        GoTo(OfferFor(reason) is null ? CancelFlowStep.Confirm : CancelFlowStep.Offer, 1);
    }

    private void AcceptOffer()
    {
        if (Step != CancelFlowStep.Offer || SelectedReason is not { } reason || OfferFor(reason) is not { } offer)
        {
            return;
        }
        _acceptedOffer = offer;
        GoTo(CancelFlowStep.Retained, 1);
        RaiseEvent(new CancelFlowOfferEventArgs(OfferAcceptedEvent, reason, offer));
    }

    private void RequestKeepPlan() => RaiseEvent(new RoutedEventArgs(KeepPlanRequestedEvent));

    private void OnBackClick(object? sender, RoutedEventArgs e) => GoBack();

    private void OnContinueClick(object? sender, RoutedEventArgs e) => Continue();

    private void OnKeepPlanClick(object? sender, RoutedEventArgs e) => RequestKeepPlan();

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => AcceptOffer();

    private void OnDeclineClick(object? sender, RoutedEventArgs e)
    {
        if (Step == CancelFlowStep.Offer)
        {
            GoTo(CancelFlowStep.Confirm, 1);
        }
    }

    private void OnHoldConfirmed(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Step != CancelFlowStep.Confirm)
        {
            return;
        }
        if (SelectedReason is not { } reason)
        {
            _hold?.SetCurrentValue(HoldToConfirm.IsConfirmedProperty, false);
            return;
        }
        var feedback = Feedback;
        GoTo(CancelFlowStep.Canceled, 1);
        RaiseEvent(new CancelFlowCancelEventArgs(CancellationConfirmedEvent, reason, feedback));
    }

    private void OnReactivateClick(object? sender, RoutedEventArgs e)
    {
        var args = new RoutedEventArgs(ReactivateRequestedEvent);
        RaiseEvent(args);
        if (!args.Handled)
        {
            Reset();
        }
    }

    private void GoTo(CancelFlowStep step, int direction)
    {
        var old = Step;
        if (old == step)
        {
            return;
        }
        var hadFocus = IsKeyboardFocusWithin;
        var enterHeld = _enterDown;
        Step = step;
        UpdateTexts();
        UpdateChrome();
        ShowStep(step, direction, animate: true);
        if (hadFocus)
        {
            FocusTitle();
        }
        // An Enter that is still held (auto-repeat) must not press the next step's main button. Set after the swap:
        // focus briefly leaves the flow as the old step goes, which clears the key state.
        _enterDown = enterHeld;
        _suppressEnter = enterHeld;
        RaiseEvent(new CancelFlowStepChangedEventArgs(StepChangedEvent, old, step));
    }

    private Control? ViewFor(CancelFlowStep step) => step switch
    {
        CancelFlowStep.Reason => _reasonView,
        CancelFlowStep.Offer => _offerView,
        CancelFlowStep.Confirm => _confirmView,
        _ => _resultView
    };

    internal TextBlock? TitleFor(CancelFlowStep step) => step switch
    {
        CancelFlowStep.Reason => _reasonTitle,
        CancelFlowStep.Offer => _offerTitle,
        CancelFlowStep.Confirm => _confirmTitle,
        _ => _resultTitle
    };

    private void ShowStep(CancelFlowStep step, int direction, bool animate)
    {
        if (_stage is null || ViewFor(step) is not { } view)
        {
            return;
        }
        if (step == CancelFlowStep.Confirm)
        {
            _hold?.SetCurrentValue(HoldToConfirm.IsConfirmedProperty, false);
        }
        if (_stage.Content is CancelFlowStepHost { Child: { } shown } && ReferenceEquals(shown, view))
        {
            return;
        }
        // A view can still sit in the host that is sliding out (quick back and forth): take it from there.
        if (view.Parent is CancelFlowStepHost previous)
        {
            previous.Child = null;
        }
        var host = new CancelFlowStepHost { Child = view };
        _stage.Direction = direction;
        if (animate)
        {
            _stage.Content = host;
        }
        else
        {
            var mode = _stage.Mode;
            _stage.Mode = ContentSwapMode.None;
            _stage.Content = host;
            _stage.Mode = mode;
        }
    }

    private void FocusTitle()
    {
        if (TitleFor(Step) is not { } title)
        {
            return;
        }
        if (!title.Focus())
        {
            // The new view joins the tree with the stage's next layout.
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(TitleFor(Step), title))
                {
                    title.Focus();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnResultGlyphAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_resultGlyph is null)
        {
            return;
        }
        // P15: the check draws itself as the last step arrives; below full motion it is simply there. Code-driven because
        // the draw restarts on each arrival (the view is reused), which a theme transition on Progress cannot express.
        if (ArcMotion.IsFull(this))
        {
            _resultGlyph.Progress = 0;
            _ = _resultGlyph.Play(ArcMotionPresets.Considered, ArcEasings.Standard, ArcMotionPresets.Fast);
        }
        else
        {
            _resultGlyph.Progress = 1;
        }
    }

    #endregion

    #region Keyboard

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _enterDown = true;
            if (_suppressEnter)
            {
                e.Handled = true;
                return;
            }
            if (HandleEnter(e))
            {
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt && GoBack())
        {
            e.Handled = true;
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _enterDown = false;
            _suppressEnter = false;
        }
    }

    /// <summary>Enter runs the step's main action unless the focused control answers Enter itself.</summary>
    private bool HandleEnter(KeyEventArgs e)
    {
        var source = e.Source as Control;
        if (source is AvaloniaButton)
        {
            // Buttons press themselves; the hold button starts a hold.
            return false;
        }
        if (source is TextBox)
        {
            // Enter adds a line to the feedback; Ctrl/Cmd+Enter continues.
            if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
            {
                return false;
            }
        }
        else if (_reasons is not null && source is not null)
        {
            // Enter on an unchosen row chooses it; on the chosen row it continues.
            var index = _reasons.IndexFromContainer(source);
            if (index >= 0 && index != _reasons.SelectedIndex)
            {
                return false;
            }
        }
        switch (Step)
        {
            case CancelFlowStep.Reason when SelectedReason is not null:
                Continue();
                return true;
            case CancelFlowStep.Offer:
                AcceptOffer();
                return true;
            case CancelFlowStep.Retained:
                RequestKeepPlan();
                return true;
            default:
                return false;
        }
    }

    #endregion

    #region Text and chrome

    private void UpdateChrome()
    {
        var inProgress = Step is CancelFlowStep.Reason or CancelFlowStep.Offer or CancelFlowStep.Confirm;
        var canGoBack = Step is CancelFlowStep.Offer or CancelFlowStep.Confirm;
        if (_headerHost is not null)
        {
            _headerHost.IsPresent = inProgress;
        }
        if (_backHost is not null)
        {
            _backHost.IsPresent = canGoBack;
        }
        if (_back is not null)
        {
            _back.IsEnabled = canGoBack;
            KeyboardNavigation.SetIsTabStop(_back, canGoBack);
        }
        UpdateStepBar();
    }

    private void UpdateStepBar()
    {
        if (_stepBar is null || _reasonSegment is null || _offerSegment is null || _confirmSegment is null)
        {
            return;
        }
        var total = StepCount;
        var current = Step switch
        {
            CancelFlowStep.Reason => 1,
            CancelFlowStep.Offer => 2,
            _ => total
        };
        if (_segmentColumns != total)
        {
            _segmentColumns = total;
            _stepBar.ColumnDefinitions = new ColumnDefinitions(total == 3 ? "*,*,*" : "*,*");
            Grid.SetColumn(_confirmSegment, total - 1);
        }
        _offerSegment.IsVisible = total == 3;
        SetSegment(_reasonSegment, 1, current);
        SetSegment(_offerSegment, 2, current);
        SetSegment(_confirmSegment, total, current);
        if (_counter is not null)
        {
            _counter.Text = Labels.StepCounter(current, total);
        }
    }

    private static void SetSegment(Border segment, int position, int current)
    {
        segment.Classes.Set(DoneClass, position < current);
        segment.Classes.Set(CurrentClass, position == current);
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        var plan = PlanName;
        var date = AccessEndsOn is { } end ? SettingsFormat.Date(end, labels.Culture) : null;

        if (_back is not null)
        {
            AutomationProperties.SetName(_back, labels.Back);
            Tooltip.SetTip(_back, labels.Back);
        }

        // Reason
        if (_reasonTitle is not null)
        {
            _reasonTitle.Text = labels.ReasonTitle;
        }
        if (_reasonDescription is not null)
        {
            _reasonDescription.Text = labels.ReasonDescription(plan);
        }
        if (_reasons is not null)
        {
            AutomationProperties.SetName(_reasons, labels.ReasonTitle);
        }
        var reason = SelectedReason;
        if (_feedbackHost is not null)
        {
            _feedbackHost.IsExpanded = reason is not null;
        }
        if (_feedback is not null)
        {
            _feedback.Label = reason is { AsksForDetails: true } ? reason.DetailsPrompt ?? labels.DetailsPrompt : labels.FeedbackLabel;
            _feedback.Placeholder = labels.FeedbackPlaceholder;
            // The collapsed field is out of the tab order.
            KeyboardNavigation.SetIsTabStop(_feedback, reason is not null);
        }
        if (_continue is not null)
        {
            _continue.Content = labels.Continue;
            _continue.IsEnabled = reason is not null;
        }
        if (_keep is not null)
        {
            _keep.Content = labels.KeepPlan;
        }

        // Offer: the chosen reason's, or the one accepted on the way to Retained.
        var offer = Step == CancelFlowStep.Retained ? _acceptedOffer ?? OfferFor(reason) : OfferFor(reason);
        if (_offerIcon is not null)
        {
            _offerIcon.Data = offer?.Icon ?? GiftIcon;
        }
        if (_offerTitle is not null)
        {
            _offerTitle.Text = offer?.Title;
        }
        if (_offerDescription is not null)
        {
            _offerDescription.Text = offer?.Description;
            _offerDescription.IsVisible = !string.IsNullOrEmpty(offer?.Description);
        }
        if (_accept is not null)
        {
            _accept.Content = string.IsNullOrWhiteSpace(offer?.AcceptLabel) ? labels.AcceptOffer : offer.AcceptLabel;
        }
        if (_decline is not null)
        {
            _decline.Content = labels.ContinueCanceling;
        }

        // Confirm
        var hasLosses = _lossList.Count > 0;
        if (_confirmTitle is not null)
        {
            _confirmTitle.Text = labels.ConfirmTitle(plan);
        }
        if (_confirmDescription is not null)
        {
            var access = labels.AccessUntil(date);
            _confirmDescription.Text = hasLosses ? $"{access} {labels.AfterThat}" : access;
        }
        if (_lossesBox is not null)
        {
            _lossesBox.IsVisible = hasLosses;
        }
        if (_losses is not null)
        {
            AutomationProperties.SetName(_losses, labels.LossesName);
        }
        if (_hold is not null)
        {
            _hold.Label = labels.HoldToCancel;
            _hold.ConfirmedLabel = labels.HoldConfirmed;
        }
        if (_confirmKeep is not null)
        {
            _confirmKeep.Content = labels.KeepPlan;
        }

        // Canceled or Retained
        var retained = Step == CancelFlowStep.Retained && offer is not null;
        if (_resultTitle is not null)
        {
            _resultTitle.Text = retained ? offer!.AcceptedTitle ?? labels.RetainedTitle(offer.Title) : labels.CanceledTitle;
        }
        if (_resultDescription is not null)
        {
            _resultDescription.Text = retained
                ? offer!.AcceptedDescription ?? labels.RetainedDescription
                : labels.CanceledDescription(plan, date);
        }
        if (_reactivate is not null)
        {
            _reactivate.Content = labels.Reactivate;
            _reactivate.IsVisible = !retained;
        }
        if (_done is not null)
        {
            _done.Content = labels.Done;
            _done.IsVisible = retained;
        }

        UpdateStepBar();
    }

    #endregion
}
