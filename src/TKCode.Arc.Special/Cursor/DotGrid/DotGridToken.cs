using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="DotGrid"/> (Arc Dot grid, design reference). The dot colors come from the ArcDesign tokens in
/// the theme (the strong border at rest, the accent when lit).
/// </summary>
[ControlDesignToken]
internal sealed class DotGridToken : AbstractControlDesignToken
{
    /// <summary>The field is at least 240 high, so an empty one still shows its dots.</summary>
    public double FieldMinHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FieldMinHeight = 240;
    }
}
