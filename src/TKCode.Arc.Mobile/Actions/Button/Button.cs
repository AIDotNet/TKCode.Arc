using Avalonia;
using Avalonia.Controls;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>How prominent a mobile <see cref="Button"/> is.</summary>
public enum ButtonVariant
{
    /// <summary>A solid pill: the screen's main action.</summary>
    Filled,

    /// <summary>A soft accent-tinted pill: a secondary action.</summary>
    Tinted,

    /// <summary>Accent text with no background: bar actions and low-emphasis actions.</summary>
    Plain,
}

/// <summary>
/// Mobile button: a touch-first button with a press state (it scales down and dims while a finger is on it)
/// and no hover state. It is at least 44 by 44 so it is easy to hit. <see cref="Variant"/> picks how prominent it is.
/// </summary>
public class Button : AvaloniaButton
{
    /// <summary>The smallest width and height a touch target may have.</summary>
    public const double MinTouchTarget = 44;

    /// <summary>How prominent the button is: filled, tinted or plain text.</summary>
    public static readonly StyledProperty<ButtonVariant> VariantProperty =
        AvaloniaProperty.Register<Button, ButtonVariant>(nameof(Variant));

    static Button()
    {
        VariantProperty.Changed.AddClassHandler<Button>((button, _) => button.UpdateVariantClasses());
    }

    /// <inheritdoc cref="VariantProperty"/>
    public ButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Button);

    private void UpdateVariantClasses()
    {
        PseudoClasses.Set(":tinted", Variant == ButtonVariant.Tinted);
        PseudoClasses.Set(":plain", Variant == ButtonVariant.Plain);
    }
}
