
namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="InkFluid"/> (Arc Ink fluid, design reference). The ink colors come from the ArcDesign tokens in
/// the theme (the foreground for neutral ink, the accent for tinted ink).
/// </summary>
[ControlDesignToken]
internal sealed class InkFluidToken : AbstractControlDesignToken
{
    /// <summary>The field is at least 240 high, so the ink has room to swirl.</summary>
    public double FieldMinHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FieldMinHeight = 240;
    }
}
