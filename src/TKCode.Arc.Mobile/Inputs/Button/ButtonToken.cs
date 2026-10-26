using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc.Mobile;

/// <summary>
/// Geometry of the mobile <see cref="Button"/>: a 44px minimum touch target, padding, and the press feedback.
/// Colors, radii and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ButtonToken : AbstractControlDesignToken
{
    /// <summary>Minimum width and height: 44.</summary>
    public double TouchTarget { get; set; }

    /// <summary>Inner padding: 16 by 10.</summary>
    public Thickness ContentPadding { get; set; }

    /// <summary>Inner padding of the plain variant, which has no pill to fill: 8 by 10.</summary>
    public Thickness PlainPadding { get; set; }

    /// <summary>Scale while pressed: 0.97.</summary>
    public double PressedScale { get; set; }

    /// <summary>Opacity while pressed: 0.85.</summary>
    public double PressedOpacity { get; set; }

    /// <summary>Opacity when disabled: 0.5.</summary>
    public double DisabledOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TouchTarget = 44;
        ContentPadding = new Thickness(16, 10);
        PlainPadding = new Thickness(8, 10);
        PressedScale = 0.97;
        PressedOpacity = 0.85;
        DisabledOpacity = 0.5;
    }
}
