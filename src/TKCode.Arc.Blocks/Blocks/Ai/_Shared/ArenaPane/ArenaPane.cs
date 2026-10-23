using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One answer of the model arena (spec 3.5): the head (48 high, 20 4 12 0 inside) with the slot's identity on the left and
/// its status on the right, the answer area (<see cref="AnswerHeight"/>, 272) and the metrics row (8 20 14 inside). The
/// pane follows <see cref="State"/>: the status word, the metrics, and the error card of a failed answer (shown after the
/// text is completed when the status becomes Failed, hidden when it leaves Failed; its button says
/// <c>Labels.Retry(slot)</c> or <c>Labels.TrySample</c> and raises <see cref="ErrorActionRequested"/>). The text comes
/// through <see cref="Append"/>, <see cref="Complete"/> and <see cref="Reset"/>, from the arena's pacer.
/// </summary>
/// <remarks>
/// <para>
/// Privacy (blind test): the model is handed to the identity, and the pane's accessible name becomes
/// <c>Labels.PaneNameRevealed(slot, name)</c>, only while <see cref="IsRevealed"/> is true; until then nothing the pane
/// holds names it. Hiding takes effect at once, showing waits for the end of the dispatcher turn (<see cref="ArenaRevealGate"/>).
/// </para>
/// <para>
/// Text appended before the pane has its template (a pane that starts hidden is never measured) is kept and handed to the
/// answer once it has one; a pane shown again while it follows lands at the bottom without a chase.
/// </para>
/// </remarks>
[TemplatePart(HeadPart, typeof(Control))]
[TemplatePart(IdentityPart, typeof(ArenaIdentity))]
[TemplatePart(StatusPart, typeof(AiStatusPill))]
[TemplatePart(AnswerPart, typeof(ArenaAnswerView))]
[TemplatePart(MetricsPart, typeof(ArenaPaneGroup))]
[TemplatePart(MetricLinePart, typeof(AiMetricLine))]
[PseudoClasses(RevealedPseudoClass, FailedPseudoClass, NoHeaderPseudoClass)]
internal sealed class ArenaPane : TemplatedControl
{
    public const string HeadPart = "PART_Head";
    public const string IdentityPart = "PART_Identity";
    public const string StatusPart = "PART_Status";
    public const string AnswerPart = "PART_Answer";
    public const string MetricsPart = "PART_Metrics";
    public const string MetricLinePart = "PART_MetricLine";

    /// <summary>Set while the pane shows its model.</summary>
    public const string RevealedPseudoClass = ":revealed";

    /// <summary>Set while the answer failed (the error card shows).</summary>
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set while the head is hidden (<see cref="ShowsHeader"/> false: the narrow layout's tabs show it).</summary>
    public const string NoHeaderPseudoClass = ":no-header";

    #region Properties

    /// <summary>What the pane shows: slot, model, status, metrics and error. The pane follows its changes.</summary>
    public static readonly StyledProperty<ArenaPaneState?> StateProperty =
        AvaloniaProperty.Register<ArenaPane, ArenaPaneState?>(nameof(State));

    /// <summary>The words (status, metrics, names, error button); none shows no text.</summary>
    public static readonly StyledProperty<ModelArenaLabels?> LabelsProperty =
        AvaloniaProperty.Register<ArenaPane, ModelArenaLabels?>(nameof(Labels));

    /// <summary>Whether the model is shown (the blind switch is off, or the person voted). The owner sets it.</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<ArenaPane, bool>(nameof(IsRevealed));

    /// <summary>Whether the first-token timer runs (the owner's clock): the time shows in TextSecondary.</summary>
    public static readonly StyledProperty<bool> IsTimingProperty =
        AvaloniaProperty.Register<ArenaPane, bool>(nameof(IsTiming));

    /// <summary>
    /// The time since the run started, shown as the first-token time while <see cref="IsTiming"/> (the owner updates it,
    /// 10 times a second); null shows the state's first-token time, or the placeholder.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> ElapsedProperty =
        AvaloniaProperty.Register<ArenaPane, TimeSpan?>(nameof(Elapsed));

    /// <summary>The answer area's height (272, fixed: the card does not grow with the text).</summary>
    public static readonly StyledProperty<double> AnswerHeightProperty =
        AvaloniaProperty.Register<ArenaPane, double>(nameof(AnswerHeight), 272);

    /// <summary>Whether the head (identity and status) shows; the narrow layout's tabs show them instead.</summary>
    public static readonly StyledProperty<bool> ShowsHeaderProperty =
        AvaloniaProperty.Register<ArenaPane, bool>(nameof(ShowsHeader), true);

    /// <summary>Raised (bubbling) when the person presses the error card's button; the owner retries or offers a sample prompt.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ErrorActionRequestedEvent =
        RoutedEvent.Register<ArenaPane, RoutedEventArgs>(nameof(ErrorActionRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="StateProperty"/>
    public ArenaPaneState? State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelArenaLabels? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <inheritdoc cref="IsTimingProperty"/>
    public bool IsTiming { get => GetValue(IsTimingProperty); set => SetValue(IsTimingProperty, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan? Elapsed { get => GetValue(ElapsedProperty); set => SetValue(ElapsedProperty, value); }

    /// <inheritdoc cref="AnswerHeightProperty"/>
    public double AnswerHeight { get => GetValue(AnswerHeightProperty); set => SetValue(AnswerHeightProperty, value); }

    /// <inheritdoc cref="ShowsHeaderProperty"/>
    public bool ShowsHeader { get => GetValue(ShowsHeaderProperty); set => SetValue(ShowsHeaderProperty, value); }

    /// <inheritdoc cref="ErrorActionRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ErrorActionRequested
    {
        add => AddHandler(ErrorActionRequestedEvent, value);
        remove => RemoveHandler(ErrorActionRequestedEvent, value);
    }

    #endregion

    private readonly StringBuilder _text = new();
    private readonly ArenaRevealGate _reveal;
    private bool _completed;
    private bool _errorShown;
    private bool _landOnShow;
    private ArenaPaneState? _observed;
    private ArenaIdentity? _identity;
    private AiStatusPill? _status;
    private ArenaAnswerView? _answer;
    private ArenaPaneGroup? _metrics;
    private AiMetricLine? _metricLine;

    static ArenaPane()
    {
        FocusableProperty.OverrideDefaultValue<ArenaPane>(false);
        IsTabStopProperty.OverrideDefaultValue<ArenaPane>(false);
    }

    public ArenaPane()
    {
        _reveal = new ArenaRevealGate(this, () => IsRevealed ? State?.Model : null, ApplyReveal);
        PseudoClasses.Set(NoHeaderPseudoClass, false);
        AddHandler(ArenaAnswerView.ActionRequestedEvent, OnAnswerActionRequested);
    }

    /// <summary>The slot (A without a state).</summary>
    internal ArenaSlot Slot => State?.Slot ?? ArenaSlot.A;

    /// <summary>Every text appended since the last <see cref="Reset"/>.</summary>
    internal string Text => _text.ToString();

    /// <summary>The identity part.</summary>
    internal ArenaIdentity? Identity => _identity;

    /// <summary>The status part.</summary>
    internal AiStatusPill? StatusPill => _status;

    /// <summary>The answer part.</summary>
    internal ArenaAnswerView? Answer => _answer;

    /// <summary>The metrics row (the named group).</summary>
    internal ArenaPaneGroup? Metrics => _metrics;

    /// <summary>The metric line inside the metrics row.</summary>
    internal AiMetricLine? MetricLine => _metricLine;

    /// <summary>The model shown now (after the reveal gate); null while hidden.</summary>
    internal AiModel? ShownModel => _reveal.Shown;

    private string SlotLetter => ArenaSlots.Letter(Slot);

    #region Text

    /// <summary>
    /// Shows the next piece of the answer (the pacer's release) at the end, fading in at full motion. Ignored after
    /// <see cref="Complete"/> until <see cref="Reset"/>. Works while the pane is hidden: the text is there when it shows.
    /// </summary>
    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text) || _completed)
        {
            return;
        }
        _text.Append(text);
        _answer?.Append(text);
    }

    /// <summary>Ends the answer: nothing more is appended until <see cref="Reset"/>.</summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }
        _completed = true;
        _answer?.Complete();
    }

    /// <summary>
    /// Starts a new round without motion: the text, the error card and the scroll offset go (the answer follows again from
    /// the top). The state is not touched; a status that is still Failed shows its card again when its error changes.
    /// </summary>
    public void Reset()
    {
        _text.Clear();
        _completed = false;
        _errorShown = false;
        PseudoClasses.Set(FailedPseudoClass, false);
        _answer?.Reset();
    }

    /// <summary>Focuses the answer (a Tab stop that scrolls with the keys); false when it cannot take focus.</summary>
    public bool FocusAnswer(NavigationMethod method = NavigationMethod.Tab) =>
        _answer is { } answer && answer.IsEffectivelyVisible && answer.Focus(method);

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _identity = e.NameScope.Find<ArenaIdentity>(IdentityPart);
        _status = e.NameScope.Find<AiStatusPill>(StatusPart);
        _answer = e.NameScope.Find<ArenaAnswerView>(AnswerPart);
        _metrics = e.NameScope.Find<ArenaPaneGroup>(MetricsPart);
        _metricLine = e.NameScope.Find<AiMetricLine>(MetricLinePart);
        if (_answer is { } answer)
        {
            // Text that came before the template (or into an earlier one) shows as one piece.
            if (_text.Length > 0)
            {
                answer.Append(_text.ToString());
            }
            if (_completed)
            {
                answer.Complete();
            }
        }
        // The model already passed the gate: show it without waiting (and without a swap) on a new template.
        ApplyReveal(_reveal.Shown);
        _errorShown = false;
        Sync();
    }

    #endregion

    #region State

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(State);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The state is the arena's and can outlive this pane: it is followed only while the pane is in a window.
        Observe(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Observe(State);
            }
            Sync();
        }
        else if (change.Property == LabelsProperty)
        {
            Sync();
        }
        else if (change.Property == IsRevealedProperty)
        {
            _reveal.Update();
        }
        else if (change.Property == IsTimingProperty || change.Property == ElapsedProperty)
        {
            UpdateMetrics();
        }
        else if (change.Property == ShowsHeaderProperty)
        {
            PseudoClasses.Set(NoHeaderPseudoClass, !change.GetNewValue<bool>());
        }
        else if (change.Property == IsVisibleProperty && change.GetNewValue<bool>())
        {
            LandWhenShown();
        }
    }

    private void Observe(ArenaPaneState? state)
    {
        if (ReferenceEquals(state, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnStatePropertyChanged;
        }
        _observed = state;
        if (state is not null)
        {
            state.PropertyChanged += OnStatePropertyChanged;
        }
    }

    private void OnStatePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ArenaPaneState.StatusProperty)
        {
            UpdateStatus();
            UpdateMetrics();
            UpdateError();
        }
        else if (e.Property == ArenaPaneState.MetricsProperty)
        {
            UpdateMetrics();
        }
        else if (e.Property == ArenaPaneState.ModelProperty)
        {
            _reveal.Update();
        }
        else if (e.Property == ArenaPaneState.ErrorTitleProperty || e.Property == ArenaPaneState.ErrorTextProperty
                 || e.Property == ArenaPaneState.IsRetryableProperty)
        {
            UpdateError();
        }
        else if (e.Property == ArenaPaneState.SlotProperty)
        {
            Sync();
        }
    }

    /// <summary>Brings every part in line with the state and the labels.</summary>
    private void Sync()
    {
        UpdateTexts();
        UpdateStatus();
        UpdateMetrics();
        UpdateError();
        _reveal.Update();
    }

    /// <summary>The words and names from the labels (none without labels).</summary>
    private void UpdateTexts()
    {
        var labels = Labels;
        var slot = SlotLetter;
        if (_status is { } pill)
        {
            pill.ThinkingText = labels?.Thinking;
            pill.WritingText = labels?.Writing;
            pill.DoneText = labels?.Done;
            pill.FinishedFirstText = labels?.FinishedFirst;
            pill.FinishedFirstShortText = labels?.FinishedFirstShort;
            pill.StoppedText = labels?.Stopped;
            pill.FailedText = labels?.Failed;
        }
        if (_answer is { } answer)
        {
            answer.AnswerName = labels?.AnswerName(slot);
        }
        if (_metrics is { } metrics)
        {
            AutomationProperties.SetName(metrics, labels?.MetricsName(slot));
        }
        if (_metricLine is { } line)
        {
            line.FirstTokenText = labels?.FirstToken;
            line.SpeedUnitText = labels?.TokensPerSecond;
            line.SpeedName = labels?.Speed;
            line.CostName = labels?.Cost;
        }
        // The identity's blind name and the pane's own name.
        ApplyReveal(_reveal.Shown);
    }

    private void UpdateStatus()
    {
        var status = State?.Status ?? ArenaPaneStatus.Idle;
        if (_status is { } pill)
        {
            pill.Status = status;
        }
        if (_answer is { } answer)
        {
            answer.IsStreaming = status is ArenaPaneStatus.Thinking or ArenaPaneStatus.Writing;
            answer.StreamingStatus = status switch
            {
                ArenaPaneStatus.Thinking => Labels?.Thinking,
                ArenaPaneStatus.Writing => Labels?.Writing,
                _ => null
            };
        }
    }

    private void UpdateMetrics()
    {
        if (_metricLine is not { } line)
        {
            return;
        }
        var metrics = State?.Metrics ?? ArenaMetrics.Empty;
        var status = State?.Status ?? ArenaPaneStatus.Idle;
        line.IsTiming = IsTiming;
        line.FirstToken = IsTiming ? Elapsed ?? metrics.TimeToFirstToken : metrics.TimeToFirstToken;
        line.TokensPerSecond = metrics.TokensPerSecond;
        // A run shows its cost from the start ($0.0000 included); a pane that never ran shows the placeholder.
        line.CostUsd = status == ArenaPaneStatus.Idle ? null : metrics.CostUsd;
    }

    /// <summary>
    /// The error card follows the status: Failed completes the text (StreamMark draws a caret at the end of an open stream)
    /// and shows the card, or changes its words in place; any other status hides it.
    /// </summary>
    private void UpdateError()
    {
        var state = State;
        if (state is { Status: ArenaPaneStatus.Failed })
        {
            Complete();
            var labels = Labels;
            var retry = state.IsRetryable;
            var action = retry ? labels?.Retry(SlotLetter) : labels?.TrySample;
            _errorShown = true;
            PseudoClasses.Set(FailedPseudoClass, true);
            _answer?.ShowError(state.ErrorTitle ?? labels?.ErrorTitle ?? string.Empty, state.ErrorText ?? labels?.ErrorText ?? string.Empty,
                action ?? string.Empty, retry);
            return;
        }
        if (!_errorShown)
        {
            return;
        }
        _errorShown = false;
        PseudoClasses.Set(FailedPseudoClass, false);
        _answer?.HideError();
    }

    private void OnAnswerActionRequested(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(e.Source, _answer))
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(ErrorActionRequestedEvent, this));
        }
    }

    #endregion

    #region Reveal

    /// <summary>
    /// Shows <paramref name="model"/> (after the gate) or the blind name: the identity's model and revealed name, and the
    /// pane's own accessible name ("Model A" or "Model A, Lumen 3 Pro").
    /// </summary>
    private void ApplyReveal(AiModel? model)
    {
        var labels = Labels;
        var slot = SlotLetter;
        var blind = labels?.PaneName(slot);
        var revealed = model is null ? null : labels?.PaneNameRevealed(slot, model.Name) ?? model.Name;
        PseudoClasses.Set(RevealedPseudoClass, model is not null);
        AutomationProperties.SetName(this, revealed ?? blind);
        if (_identity is not { } identity)
        {
            return;
        }
        identity.BlindText = blind;
        if (model is null)
        {
            // Hide first, then let go of the model: the identity never names one it does not show.
            identity.IsRevealed = false;
            identity.Model = null;
            identity.RevealedAutomationName = null;
        }
        else
        {
            identity.Model = model;
            identity.RevealedAutomationName = revealed;
            identity.IsRevealed = true;
        }
    }

    #endregion

    #region Showing again

    /// <summary>
    /// A pane shown again (the narrow layout's other tab) may have received text while hidden: if it follows, it lands at
    /// the bottom after the next layout instead of chasing there from where it was.
    /// </summary>
    private void LandWhenShown()
    {
        if (_answer is not { IsFollowing: true } || _landOnShow)
        {
            return;
        }
        _landOnShow = true;
        LayoutUpdated += OnLayoutUpdatedOnce;
    }

    private void OnLayoutUpdatedOnce(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedOnce;
        _landOnShow = false;
        if (_answer is { IsFollowing: true, Follow: { } follow })
        {
            follow.ScrollToLatest(animate: false);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new PanePeer(this);

    /// <summary>A named region (Arc's <c>section aria-label="Model A"</c>) with the head, the answer and the metrics inside.</summary>
    private sealed class PanePeer(ArenaPane owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
