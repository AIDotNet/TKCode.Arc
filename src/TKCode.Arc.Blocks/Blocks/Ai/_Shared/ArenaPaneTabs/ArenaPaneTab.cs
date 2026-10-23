using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One tab of <see cref="ArenaPaneTabs"/>: 52 high, 12 inside on the sides, two lines: the slot's identity (14 / 500; the
/// model's short name once revealed) and its compact status pill (20 high, pulled 7px left so its icon lines up with the
/// name). No background of its own (the strip's highlight shows under the selected tab); the name is Foreground when
/// selected or hovered, TextSecondary otherwise. Reads as a tab item named "Model A" (or "Model A, Lumen 3 Pro" once
/// revealed), with the status word as its item status.
/// </summary>
[TemplatePart(IdentityPart, typeof(ArenaIdentity))]
[TemplatePart(StatusPart, typeof(AiStatusPill))]
[PseudoClasses(SelectedPseudoClass, HoverPseudoClass, RevealedPseudoClass)]
internal sealed class ArenaPaneTab : AvaloniaButton
{
    public const string IdentityPart = "PART_Identity";
    public const string StatusPart = "PART_Status";

    public const string SelectedPseudoClass = ":selected";
    public const string HoverPseudoClass = ":hover";
    public const string RevealedPseudoClass = ":revealed";

    /// <summary>Whether this tab is the selected one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ArenaPaneTab, bool>(nameof(IsSelected));

    /// <summary>The answer this tab names (its slot, model and status); the tab follows its changes.</summary>
    public static readonly StyledProperty<ArenaPaneState?> StateProperty =
        AvaloniaProperty.Register<ArenaPaneTab, ArenaPaneState?>(nameof(State));

    /// <summary>The words.</summary>
    public static readonly StyledProperty<ModelArenaLabels?> LabelsProperty =
        AvaloniaProperty.Register<ArenaPaneTab, ModelArenaLabels?>(nameof(Labels));

    /// <summary>Whether the model is shown.</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<ArenaPaneTab, bool>(nameof(IsRevealed));

    private readonly ArenaRevealGate _reveal;
    private ArenaPaneState? _observed;
    private ArenaIdentity? _identity;
    private AiStatusPill? _status;

    public ArenaPaneTab()
    {
        _reveal = new ArenaRevealGate(this, () => IsRevealed ? State?.Model : null, ApplyReveal);
    }

    protected override Type StyleKeyOverride => typeof(ArenaPaneTab);

    /// <summary>The slot this tab selects (set by the strip).</summary>
    public ArenaSlot Slot { get; set; }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public ArenaPaneState? State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelArenaLabels? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <summary>The identity part.</summary>
    internal ArenaIdentity? Identity => _identity;

    /// <summary>The status part.</summary>
    internal AiStatusPill? StatusPill => _status;

    /// <summary>The model shown now (after the reveal gate); null while hidden.</summary>
    internal AiModel? ShownModel => _reveal.Shown;

    private string SlotLetter => ArenaSlots.Letter(Slot);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _identity = e.NameScope.Find<ArenaIdentity>(IdentityPart);
        _status = e.NameScope.Find<AiStatusPill>(StatusPart);
        // A model that already passed the gate shows at once on a new template.
        ApplyReveal(_reveal.Shown);
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(State);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            var selected = change.GetNewValue<bool>();
            PseudoClasses.Set(SelectedPseudoClass, selected);
            if (ControlAutomationPeer.FromElement(this) is TabPeer peer)
            {
                peer.RaiseSelectedChanged(selected);
            }
        }
        else if (change.Property == StateProperty)
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
        }
        else if (e.Property == ArenaPaneState.ModelProperty)
        {
            _reveal.Update();
        }
    }

    private void Sync()
    {
        var labels = Labels;
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
        UpdateStatus();
        ApplyReveal(_reveal.Shown);
        _reveal.Update();
    }

    private void UpdateStatus()
    {
        var status = State?.Status ?? ArenaPaneStatus.Idle;
        if (_status is { } pill)
        {
            pill.Status = status;
        }
        var labels = Labels;
        // The whole word for screen readers ("Finished first"), not the pill's short form.
        var word = status switch
        {
            ArenaPaneStatus.Thinking => labels?.Thinking,
            ArenaPaneStatus.Writing => labels?.Writing,
            ArenaPaneStatus.Done => labels?.Done,
            ArenaPaneStatus.FinishedFirst => labels?.FinishedFirst,
            ArenaPaneStatus.Stopped => labels?.Stopped,
            ArenaPaneStatus.Failed => labels?.Failed,
            _ => null
        };
        AutomationProperties.SetItemStatus(this, string.IsNullOrEmpty(word) ? null : word);
    }

    /// <summary>Shows <paramref name="model"/> (after the gate) or the blind name, on the identity and in the tab's name.</summary>
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

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    /// <summary>A tab item, selected while selected (Arc's <c>role="tab"</c> + <c>aria-selected</c>).</summary>
    private sealed class TabPeer(ArenaPaneTab owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        private ArenaPaneTab Tab => (ArenaPaneTab)Owner;

        public bool IsSelected => Tab.IsSelected;

        public ISelectionProvider? SelectionContainer =>
            Tab.FindAncestorOfType<ArenaPaneTabs>() is { } tabs ? CreatePeerForElement(tabs) as ISelectionProvider : null;

        public void Select() => Tab.FindAncestorOfType<ArenaPaneTabs>()?.Select(Tab.Slot);

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void RaiseSelectedChanged(bool selected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !selected, selected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionItemProvider) ? this : base.GetProviderCore(providerType);
    }
}
