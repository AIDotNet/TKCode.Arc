using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GlassCard"/>: the space inside the card and the perspective of its tilt. The glass material
/// comes from <see cref="GlassPaneToken"/>; the corners, the shadow and the text colors from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class GlassCardToken : AbstractControlDesignToken
{
    /// <summary>Space between the card's edge and its content.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Distance of the viewer from the card for the tilt's perspective, in px (larger is flatter).</summary>
    public double TiltDepth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(24);
        TiltDepth = 1200;
    }
}
