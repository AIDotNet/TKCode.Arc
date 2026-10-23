using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The button of an <see cref="AiSourcesRow"/> (Arc answer's sources disclosure): a 34px pill with 10px on the left and 8px
/// on the right, set 10px out on both sides so the mark stack lines up with the text column; the mark stack, the label
/// ("8 sources", 14 / 19.6, tabular figures) and a 15px chevron 8px apart. Secondary text at rest; on hover a SurfaceMuted
/// fill, Foreground text and the stack fans out; keyboard focus fans it too and shows the ring; pressed, the fill is
/// SurfaceMuted and Border half and half. The chevron turns 180° while open on Arc's 580ms CSS spring. Automation reads the
/// label as the name and reports expanded or collapsed.
/// </summary>
[TemplatePart(StackPart, typeof(AiSourceStack))]
[PseudoClasses(ExpandedPseudoClass, HoverPseudoClass)]
internal sealed class AiSourcesToggle : AvaloniaButton
{
    public const string StackPart = "PART_Stack";
    public const string LabelPart = "PART_Label";
    public const string ChevronPart = "PART_Chevron";

    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>Mouse or pen over the button (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The sources whose marks the stack shows.</summary>
    public static readonly StyledProperty<IList<AiSource>?> SourcesProperty =
        AvaloniaProperty.Register<AiSourcesToggle, IList<AiSource>?>(nameof(Sources));

    /// <summary>The label ("8 sources").</summary>
    public static readonly StyledProperty<string?> LabelTextProperty =
        AvaloniaProperty.Register<AiSourcesToggle, string?>(nameof(LabelText));

    /// <summary>Whether the cards are open (the owner sets it).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiSourcesToggle, bool>(nameof(IsExpanded));

    /// <summary>The fill while pressed: SurfaceMuted and Border half and half (mixed here; the theme uses it).</summary>
    public static readonly StyledProperty<IBrush?> PressedFillProperty =
        AvaloniaProperty.Register<AiSourcesToggle, IBrush?>(nameof(PressedFill));

    private static readonly StyledProperty<IBrush?> MutedProbeProperty =
        AvaloniaProperty.Register<AiSourcesToggle, IBrush?>("MutedProbe");

    private static readonly StyledProperty<IBrush?> BorderProbeProperty =
        AvaloniaProperty.Register<AiSourcesToggle, IBrush?>("BorderProbe");

    public AiSourcesToggle()
    {
        ArcTokens.Bind(this, MutedProbeProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(this, BorderProbeProperty, ArcDesignTokenKind.Border);
    }

    /// <inheritdoc cref="SourcesProperty"/>
    public IList<AiSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="LabelTextProperty"/>
    public string? LabelText { get => GetValue(LabelTextProperty); set => SetValue(LabelTextProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="PressedFillProperty"/>
    public IBrush? PressedFill { get => GetValue(PressedFillProperty); set => SetValue(PressedFillProperty, value); }

    private AiSourceStack? _stack;

    /// <summary>The mark stack of the template.</summary>
    internal AiSourceStack? Stack => _stack;

    protected override Type StyleKeyOverride => typeof(AiSourcesToggle);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stack = e.NameScope.Find<AiSourceStack>(StackPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelTextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == IsExpandedProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, open);
            (ControlAutomationPeer.FromElement(this) as AiSourcesToggleAutomationPeer)?.RaiseExpandCollapseChanged(open);
        }
        else if (change.Property == MutedProbeProperty || change.Property == BorderProbeProperty)
        {
            if (GetValue(MutedProbeProperty) is ISolidColorBrush muted && GetValue(BorderProbeProperty) is ISolidColorBrush border)
            {
                PressedFill = new ImmutableSolidColorBrush(Oklch.MixOklab(muted.Color, border.Color, 0.5));
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch && IsEffectivelyEnabled);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiSourcesToggleAutomationPeer(this);

    /// <summary>
    /// A button that opens a region (<c>aria-expanded</c>), named by its label; the stack and chevron are not exposed.
    /// Expand and Collapse act like a press.
    /// </summary>
    private sealed class AiSourcesToggleAutomationPeer(AiSourcesToggle owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private AiSourcesToggle Toggle => (AiSourcesToggle)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Toggle.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            EnsureEnabled();
            if (!Toggle.IsExpanded)
            {
                ((IInvokeProvider)this).Invoke();
            }
        }

        public void Collapse()
        {
            EnsureEnabled();
            if (Toggle.IsExpanded)
            {
                ((IInvokeProvider)this).Invoke();
            }
        }

        public void RaiseExpandCollapseChanged(bool open) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
