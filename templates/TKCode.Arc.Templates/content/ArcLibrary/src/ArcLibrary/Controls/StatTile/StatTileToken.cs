using Avalonia;
using TKCode.Arc;

namespace ArcLibrary;

/// <summary>
/// The design tokens of <see cref="StatTile"/>: its geometry. Colors, corner radii, shadows and fonts come from Arc's
/// global tokens ({arc:ArcDesignTokenResource ...}) in the theme, so they follow the accent and the appearance.
/// </summary>
/// <remarks>
/// A token identity must be unique among every package an app registers, Arc's included, or the app stops at start-up
/// (ARCF004). The class name without "Token" would be "StatTile"; the library's name in front keeps it apart from
/// Arc's controls and other libraries. Keep the prefix on every control you add.
/// </remarks>
[ControlDesignToken(Identity = "ArcLibraryIdentifierStatTile")]
public sealed class StatTileToken : AbstractControlDesignToken
{
    /// <summary>Space between the border and the text.</summary>
    public Thickness Padding { get; set; }

    /// <summary>Space between the title, the value and the caption.</summary>
    public double Spacing { get; set; }

    /// <summary>The narrowest the tile gets.</summary>
    public double MinWidth { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Padding = new Thickness(16, 14);
        Spacing = 6;
        MinWidth = 160;
    }
}
