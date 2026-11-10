using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pressable rows and cards of the agent blocks (a run step, a task card, an approval request, a routine): a plain
/// Avalonia button whose keyboard focus ring is built the first time it is needed (<see cref="LazyFocusRing"/>), placed
/// under the face in the template's <see cref="RingHostPart"/> panel. Pointer presses never show the ring. Touch
/// pointers do not set <c>:pointerover</c> styling of their own: the subclasses' themes use <c>:hover</c>, which this
/// base sets for mouse and pen only.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(HoverPseudoClass)]
internal class AiAgentButton : AvaloniaButton
{
    /// <summary>The panel of the template that receives the ring, under the face.</summary>
    public const string RingHostPart = "PART_RingHost";

    /// <summary>Set while a mouse or pen points at the button (Arc's hover is for fine pointers only).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The design token of the focus ring's corner radius (the ring sits 3px outside the face, so pick a step 3px larger than the face's).</summary>
    public static readonly StyledProperty<ArcDesignTokenKind> RingRadiusProperty =
        AvaloniaProperty.Register<AiAgentButton, ArcDesignTokenKind>(nameof(RingRadius), ArcDesignTokenKind.RadiusStep14);

    private LazyFocusRing? _ring;

    static AiAgentButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<AiAgentButton>(false);
        FocusAdornerProperty.OverrideDefaultValue<AiAgentButton>(null);
    }

    /// <inheritdoc cref="RingRadiusProperty"/>
    public ArcDesignTokenKind RingRadius { get => GetValue(RingRadiusProperty); set => SetValue(RingRadiusProperty, value); }

    /// <summary>True while the keyboard focus ring shows.</summary>
    internal bool IsFocusRingVisible => _ring?.Ring?.IsVisible == true;

    protected override Type StyleKeyOverride => GetType();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = new LazyFocusRing(this, AiAgentButtonTokens.Key(AiAgentButtonTokenKind.RingMargin), ArcDesignTokens.Key(RingRadius));
        _ring.Attach(e.NameScope.Find<Panel>(RingHostPart), below: true);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(HoverPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}
