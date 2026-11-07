namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MorphLoader"/> (Arc morph loader, design reference): a 24px square by default, drawn on a 24
/// unit grid; the ring, square, check and cross use a 2.5 unit stroke, the dots are 4 units wide and the bars 3. The
/// result tones come from the Success and Danger ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MorphLoaderToken : AbstractControlDesignToken
{
    /// <summary>The default rendered side in px (24).</summary>
    public double DefaultSize { get; set; }

    /// <summary>The default stroke width of the ring, square, check and cross in grid units (2.5).</summary>
    public double DefaultStrokeWidth { get; set; }

    /// <summary>The diameter of a dot in grid units (4).</summary>
    public double DotWidth { get; set; }

    /// <summary>The width of a bar in grid units (3).</summary>
    public double BarWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DefaultSize = 24;
        DefaultStrokeWidth = 2.5;
        DotWidth = 4;
        BarWidth = 3;
    }
}
