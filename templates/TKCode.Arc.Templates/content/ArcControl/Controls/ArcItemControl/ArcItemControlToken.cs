using Avalonia;
using TKCode.Arc;

namespace ArcItemNamespace.Controls;

/// <summary>
/// The design tokens of <see cref="ArcItemControl"/>: its geometry. Colors, corner radii, shadows and fonts come from
/// Arc's global tokens ({arc:ArcDesignTokenResource ...}) in the theme, so they follow the accent and the appearance.
/// The theme reads these through the generated {tokens:ArcItemControlTokenResource ...} extension.
/// </summary>
/// <remarks>
/// The token identity is the class name without "Token" ("ArcItemControl"). It must be unique among every registered
/// package, Arc's included: a control named like an Arc one (Card, Badge) needs
/// <c>[ControlDesignToken(Identity = "MyCard")]</c>, or the app stops at start-up with a duplicate identity error.
/// </remarks>
[ControlDesignToken]
internal sealed class ArcItemControlToken : AbstractControlDesignToken
{
    /// <summary>Space between the border and the text.</summary>
    public Thickness Padding { get; set; }

    /// <summary>Space between the title, the value and the caption.</summary>
    public double Spacing { get; set; }

    /// <summary>The narrowest the tile gets.</summary>
    public double MinWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Padding = new Thickness(16, 14);
        Spacing = 6;
        MinWidth = 160;
    }
}
