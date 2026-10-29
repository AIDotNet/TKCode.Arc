using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ShowpieceHoverCard"/> (Arc Showpieces, design reference): padding 12 / 14, radius 12. The fill
/// (<c>SurfaceRaised</c>), border, <c>ShadowFloating</c> and type come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ShowpieceHoverCardToken : AbstractControlDesignToken
{
    /// <summary>Inner padding: 12px top and bottom, 14px left and right.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Corner radius (12px).</summary>
    public CornerRadius CardRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(14, 12);
        CardRadius = new CornerRadius(12);
    }
}
