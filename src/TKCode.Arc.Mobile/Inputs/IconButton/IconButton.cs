using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>How prominent a mobile <see cref="IconButton"/> is.</summary>
public enum IconButtonVariant
{
    /// <summary>An accent icon with no background: bar actions.</summary>
    Plain,

    /// <summary>An accent icon on a soft accent-tinted circle: a secondary action.</summary>
    Tinted,

    /// <summary>An accent circle with a contrasting icon: the main action of a view.</summary>
    Filled,

    /// <summary>A floating action button: a 56px accent circle with a shadow and a spring press.</summary>
    Fab,
}

/// <summary>
/// Mobile icon button: a 44 by 44 icon-only button with a press state (it scales down while a finger is on it) and no hover
/// state. <see cref="Variant"/> picks plain, tinted, filled or floating (<see cref="IconButtonVariant.Fab"/>).
/// An icon-only button has no visible text, so always set <c>AutomationProperties.Name</c>.
/// </summary>
public class IconButton : AvaloniaButton
{
    /// <summary>The smallest width and height a touch target may have.</summary>
    public const double MinTouchTarget = 44;

    /// <summary>The glyph geometry shown in the button, for example a Lucide icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<IconButton, Geometry?>(nameof(Icon));

    /// <summary>How prominent the button is: plain, tinted, filled or a floating action button.</summary>
    public static readonly StyledProperty<IconButtonVariant> VariantProperty =
        AvaloniaProperty.Register<IconButton, IconButtonVariant>(nameof(Variant));

    static IconButton()
    {
        VariantProperty.Changed.AddClassHandler<IconButton>((button, _) => button.UpdateVariantClasses());
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="VariantProperty"/>
    public IconButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(IconButton);

    private void UpdateVariantClasses()
    {
        PseudoClasses.Set(":tinted", Variant == IconButtonVariant.Tinted);
        PseudoClasses.Set(":filled", Variant == IconButtonVariant.Filled);
        PseudoClasses.Set(":fab", Variant == IconButtonVariant.Fab);
    }
}
