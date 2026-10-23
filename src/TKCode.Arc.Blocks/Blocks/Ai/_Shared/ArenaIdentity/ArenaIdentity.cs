using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The name slot of an arena pane (Arc model arena), 14 / 19.6 medium in Foreground: <see cref="BlindText"/> ("Model A")
/// while the models are hidden, the model's mark (16px) and name once <see cref="IsRevealed"/> is true and
/// <see cref="Model"/> is set. Each change swaps in Arc's label mode (the old name fades out in 100ms out of layout, the new
/// one fades in over 160ms rising 2px) while the slot's width follows on <see cref="ArcSpring.Quick"/>; reduced motion
/// crossfades in 120ms and jumps the width, Off jumps. A name too long for the slot ends in an ellipsis.
/// </summary>
/// <remarks>
/// Privacy (blind test): the model's name and mark are created only when the identity shows them; while hidden nothing
/// in the visual, logical or automation tree carries them (no hidden copy for measuring, no tooltip), and the accessible
/// name is <see cref="BlindText"/>. Hiding takes effect at once; showing a model waits for the end of the current
/// dispatcher turn, so an owner that changes <see cref="Model"/> for a new round and then turns <see cref="IsRevealed"/>
/// off (in the same turn, in either order) never puts the new model on screen. A face that was shown fades out when hidden,
/// out of the automation tree.
/// </remarks>
[TemplatePart(FacePart, typeof(ArcContentSwap))]
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[PseudoClasses(RevealedPseudoClass)]
internal sealed class ArenaIdentity : TemplatedControl
{
    public const string FacePart = "PART_Face";
    public const string SizePart = "PART_Size";

    /// <summary>Set while the model is shown.</summary>
    public const string RevealedPseudoClass = ":revealed";

    /// <summary>The name while the models are hidden ("Model A").</summary>
    public static readonly StyledProperty<string?> BlindTextProperty =
        AvaloniaProperty.Register<ArenaIdentity, string?>(nameof(BlindText));

    /// <summary>The model in this slot; shown only while <see cref="IsRevealed"/>.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<ArenaIdentity, AiModel?>(nameof(Model));

    /// <summary>Whether the model is shown (after the vote, or with the blind switch off).</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<ArenaIdentity, bool>(nameof(IsRevealed));

    /// <summary>The accessible name while the model is shown ("Model A, Lumen 3 Pro"); null uses the model's name.</summary>
    public static readonly StyledProperty<string?> RevealedAutomationNameProperty =
        AvaloniaProperty.Register<ArenaIdentity, string?>(nameof(RevealedAutomationName));

    /// <summary>Show the model's short name (<see cref="AiModel.DisplayName"/>, the narrow tabs) instead of its full name.</summary>
    public static readonly StyledProperty<bool> ShowsShortNameProperty =
        AvaloniaProperty.Register<ArenaIdentity, bool>(nameof(ShowsShortName));

    private ArcContentSwap? _swap;
    private ArenaIdentityFace? _face;
    private AiModel? _observed;
    private bool _pending;

    static ArenaIdentity()
    {
        FocusableProperty.OverrideDefaultValue<ArenaIdentity>(false);
        IsTabStopProperty.OverrideDefaultValue<ArenaIdentity>(false);
    }

    /// <inheritdoc cref="BlindTextProperty"/>
    public string? BlindText { get => GetValue(BlindTextProperty); set => SetValue(BlindTextProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <inheritdoc cref="RevealedAutomationNameProperty"/>
    public string? RevealedAutomationName { get => GetValue(RevealedAutomationNameProperty); set => SetValue(RevealedAutomationNameProperty, value); }

    /// <inheritdoc cref="ShowsShortNameProperty"/>
    public bool ShowsShortName { get => GetValue(ShowsShortNameProperty); set => SetValue(ShowsShortNameProperty, value); }

    /// <summary>The face on screen now.</summary>
    internal ArenaIdentityFace? Face => _face;

    /// <summary>Whether a model is shown now (it can lag <see cref="IsRevealed"/> until the end of the dispatcher turn).</summary>
    internal bool ShowsModel { get; private set; }

    private bool WantsModel => IsRevealed && Model is not null;

    private bool IsInWindow => TopLevel.GetTopLevel(this) is not null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(FacePart);
        _face = null;
        Apply(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRevealedProperty || change.Property == ModelProperty || change.Property == ShowsShortNameProperty)
        {
            Request();
        }
        else if (change.Property == BlindTextProperty)
        {
            if (!ShowsModel)
            {
                Apply(instant: false);
            }
        }
        else if (change.Property == RevealedAutomationNameProperty)
        {
            UpdateName();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(ShowsModel ? Model : null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The model is app data that can outlive the slot: it is followed only while the slot shows it in a window.
        Observe(null);
    }

    /// <summary>Hides at once; shows a model (or another one) once the current dispatcher turn is over.</summary>
    private void Request()
    {
        if (!WantsModel || _swap is null || !IsInWindow)
        {
            Apply(instant: false);
            return;
        }
        if (_pending)
        {
            return;
        }
        _pending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _pending = false;
            Apply(instant: false);
        }, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Brings the slot in line with the properties: a change between blind and revealed, or another model, is a new face
    /// that swaps in (at once when <paramref name="instant"/>); a renamed model or a new blind name changes the face in place.
    /// </summary>
    private void Apply(bool instant)
    {
        var model = WantsModel ? Model : null;
        var shows = model is not null;
        ShowsModel = shows;
        PseudoClasses.Set(RevealedPseudoClass, shows);
        Observe(shows && IsInWindow ? model : null);
        var text = shows ? (ShowsShortName ? model!.DisplayName : model!.Name) : BlindText;
        object? mark = null;
        IBrush? color = null;
        if (shows)
        {
            mark = ModelMark.Resolve(model, null, out color);
        }
        UpdateName();
        if (_swap is null)
        {
            return;
        }
        if (_face is { } current && current.ShowsModel == shows && ReferenceEquals(current.Model, model))
        {
            current.Text = text;
            current.Mark = mark;
            current.MarkColor = color;
            return;
        }
        if (_face is { ShowsModel: true } leaving)
        {
            // A model that is no longer shown is not read out while it fades.
            AutomationProperties.SetAccessibilityView(leaving, AccessibilityView.Raw);
        }
        _face = new ArenaIdentityFace { ShowsModel = shows, Model = model, Text = text, Mark = mark, MarkColor = color };
        if (instant)
        {
            // SetCurrentValue keeps the template's Mode the value a theme can still change.
            var mode = _swap.Mode;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _swap.Content = _face;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _swap.Content = _face;
        }
    }

    private void UpdateName()
    {
        var name = ShowsModel && Model is { } model
            ? string.IsNullOrEmpty(RevealedAutomationName) ? (ShowsShortName ? model.DisplayName : model.Name) : RevealedAutomationName
            : BlindText;
        AutomationProperties.SetName(this, name ?? string.Empty);
    }

    private void Observe(AiModel? model)
    {
        if (ReferenceEquals(model, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnModelPropertyChanged;
        }
        _observed = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (ShowsModel && (e.Property == AiModel.NameProperty || e.Property == AiModel.ShortNameProperty || e.Property == AiModel.MarkProperty
                           || e.Property == AiModel.MarkColorProperty))
        {
            Apply(instant: false);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new IdentityPeer(this);

    /// <summary>One piece of text named by what the slot shows; the face inside is not read again.</summary>
    private sealed class IdentityPeer(ArenaIdentity owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
