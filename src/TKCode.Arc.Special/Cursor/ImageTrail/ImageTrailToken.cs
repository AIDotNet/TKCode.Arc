using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ImageTrail"/>. The picture corners, placeholder, outline, shadow and focus ring come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ImageTrailToken : AbstractControlDesignToken
{
    /// <summary>The keyboard focus ring sits 4px outside the surface.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>The smallest height of the surface, so there is room to move across it.</summary>
    public double SurfaceMinHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FocusRingMargin = new Thickness(-4);
        SurfaceMinHeight = 240;
    }
}
