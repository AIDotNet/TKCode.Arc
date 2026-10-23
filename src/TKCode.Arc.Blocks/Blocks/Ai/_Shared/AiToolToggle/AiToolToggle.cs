using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A tool switch in an AI composer's toolbar ("Web search", "Extended thinking"): a 36 px bordered pill with a 16 px icon
/// and a label. On (<c>IsChecked</c>) it takes the accent tint: AccentSubtle fill, Accent edge at 32%, AccentStrong text and
/// icon. With <see cref="IsCompact"/> (a narrow composer) it keeps only the icon in a 36 px circle, border and tint
/// included, and the tooltip carries the label.
/// <code>
/// &lt;arc:AiToolToggle Label="Web search" Icon="{x:Static arc:ArcIcons.Globe}" IsChecked="{Binding WebSearch}" /&gt;
/// </code>
/// It is a <see cref="ToggleButton"/>, so Space and Enter toggle it and automation reports it as a toggle (ARIA's
/// <c>aria-pressed</c>); its accessible name is <see cref="Label"/> in both widths.
/// </summary>
[PseudoClasses(CompactPseudoClass, HoverPseudoClass)]
internal class AiToolToggle : ToggleButton
{
    /// <summary>Only the icon (set through <see cref="IsCompact"/>).</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Mouse or pen over the toggle (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>The text next to the icon and the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiToolToggle, string?>(nameof(Label));

    /// <summary>A 24 × 24 icon geometry (Lucide), drawn at 16 px with a 1.75 stroke.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<AiToolToggle, Geometry?>(nameof(Icon));

    /// <summary>The tooltip; null shows <see cref="Label"/> (so a compact toggle still says what it is).</summary>
    public static readonly StyledProperty<string?> TipProperty =
        AvaloniaProperty.Register<AiToolToggle, string?>(nameof(Tip));

    /// <summary>Icon only, in a 36 px circle (the owner sets it below its compact width).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<AiToolToggle, bool>(nameof(IsCompact));

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="TipProperty"/>
    public string? Tip { get => GetValue(TipProperty); set => SetValue(TipProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override Type StyleKeyOverride => typeof(AiToolToggle);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == TipProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A toggle disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    private void UpdateTexts()
    {
        var label = Label;
        if (string.IsNullOrEmpty(label))
        {
            ClearValue(AutomationProperties.NameProperty);
        }
        else
        {
            AutomationProperties.SetName(this, label);
        }
        var tip = string.IsNullOrEmpty(Tip) ? label : Tip;
        Tooltip.SetTip(this, string.IsNullOrEmpty(tip) ? null : tip);
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

    protected override AutomationPeer OnCreateAutomationPeer() => new AiToolToggleAutomationPeer(this);

    /// <summary>A toggle named by its label; the icon and the label inside are not read again.</summary>
    private sealed class AiToolToggleAutomationPeer(AiToolToggle owner) : ToggleButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
