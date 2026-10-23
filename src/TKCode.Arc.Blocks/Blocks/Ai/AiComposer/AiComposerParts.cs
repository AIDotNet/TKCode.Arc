using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The composer's round icon button (the plus that opens the attach menu): a 36px circle with a 20px icon in
/// TextSecondary; under a mouse or pen the icon turns to Foreground over a Foreground 6% tint, pressed or with its menu
/// open (<see cref="IsOpen"/>) the tint is 10%. Keyboard focus draws the 2px ring. In the chat's single row (class
/// <c>inline</c>) it is the paperclip: a 34px circle, an 18px icon, SurfaceMuted under the pointer.
/// </summary>
[PseudoClasses(OpenPseudoClass, HoverPseudoClass)]
internal sealed class AiComposerIconButton : AvaloniaButton
{
    /// <summary>The button's menu is open.</summary>
    public const string OpenPseudoClass = ":open";

    /// <summary>Mouse or pen over the button (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>A 24 × 24 icon geometry (Lucide).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiComposerIconButton, Geometry?>(nameof(Icon));

    /// <summary>The icon's size (the composer's plus: 20; the chat's paperclip: 18).</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<AiComposerIconButton, double>(nameof(IconSize), 20);

    /// <summary>Set by the owner while the button's menu is open: it keeps the pressed tint.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AiComposerIconButton, bool>(nameof(IsOpen));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IconSizeProperty"/>
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiComposerIconButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(OpenPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
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

    protected override AutomationPeer OnCreateAutomationPeer() => new IconButtonAutomationPeer(this);

    /// <summary>A button named by its accessible name; the icon inside is not read.</summary>
    private sealed class IconButtonAutomationPeer(AiComposerIconButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
