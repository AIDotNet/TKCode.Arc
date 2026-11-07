namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MorphBlock"/> skeletons (Arc Skeleton morph, design reference): the text bars and the blur the
/// content sharpens out of. The skeleton fill is the ArcDesign <c>SurfaceMuted</c> token.
/// </summary>
[ControlDesignToken]
internal sealed class MorphBlockToken : AbstractControlDesignToken
{
    /// <summary>Tallest text bar (12, measured in 18 and 20 px lines).</summary>
    public double LineBarMaxHeight { get; set; }

    /// <summary>A text bar is this much shorter than its line box, up to the maximum (6: a 16 px line gets a 10 px bar).</summary>
    public double LineBarInset { get; set; }

    /// <summary>The last bar of a multi-line text skeleton is this fraction of the width (0.62, measured).</summary>
    public double LastLineRatio { get; set; }

    /// <summary>The blur, in pixels, the content sharpens out of when it resolves (4, measured).</summary>
    public double RevealBlur { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LineBarMaxHeight = 12;
        LineBarInset = 6;
        LastLineRatio = 0.62;
        RevealBlur = 4;
    }
}
