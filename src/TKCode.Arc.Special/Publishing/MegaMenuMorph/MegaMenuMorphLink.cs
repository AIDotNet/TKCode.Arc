using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A destination inside a <see cref="MegaMenuMorph"/> panel: an icon on a quiet tile, a title and an optional one-line
/// description. It is a button, so it has the usual click, command and keyboard behavior; the menu raises
/// <see cref="MegaMenuMorph.LinkInvoked"/> and closes when one is clicked. Up and Down move between the links of a panel.
/// </summary>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(HasIconPseudoClass, HasDescriptionPseudoClass)]
public class MegaMenuMorphLink : AvaloniaButton
{
    internal const string RootPart = "PART_Root";
    internal const string HasIconPseudoClass = ":has-icon";
    internal const string HasDescriptionPseudoClass = ":has-description";

    /// <summary>The link's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MegaMenuMorphLink, string?>(nameof(Title));

    /// <summary>One short line under the title, or null for none.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<MegaMenuMorphLink, string?>(nameof(Description));

    /// <summary>A 24×24 stroke icon (for example one of <c>ArcIcons</c>) shown on a tile before the text, or null for none.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MegaMenuMorphLink, Geometry?>(nameof(Icon));

    private LazyFocusRing? _focusRing;

    static MegaMenuMorphLink()
    {
        ClipToBoundsProperty.OverrideDefaultValue<MegaMenuMorphLink>(false);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(MegaMenuMorphLink);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, MegaMenuMorphLinkTokens.Key(MegaMenuMorphLinkTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep14));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart), below: true);
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
        {
            PseudoClasses.Set(HasIconPseudoClass, Icon is not null);
        }
        else if (change.Property == DescriptionProperty)
        {
            PseudoClasses.Set(HasDescriptionPseudoClass, !string.IsNullOrEmpty(Description));
            UpdateName();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateName();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MegaMenuMorphLinkAutomationPeer(this);

    private void UpdateName()
    {
        AutomationProperties.SetName(this, Title ?? string.Empty);
        AutomationProperties.SetHelpText(this, Description);
    }

    private sealed class MegaMenuMorphLinkAutomationPeer(MegaMenuMorphLink owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    }
}
