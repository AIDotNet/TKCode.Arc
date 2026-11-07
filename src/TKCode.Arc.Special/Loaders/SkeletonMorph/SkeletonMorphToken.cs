using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SkeletonMorph"/> (Arc Skeleton morph, design reference): the padding of the <c>card</c> style
/// class and the low point of the skeleton pulse. Colors, the border and the card radius come from the ArcDesign tokens
/// in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SkeletonMorphToken : AbstractControlDesignToken
{
    /// <summary>Padding inside a <c>card</c> region (20, measured).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The skeletons' opacity at the bottom of a breath (0.55, measured; the top is 1).</summary>
    public double PulseMinOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(20);
        PulseMinOpacity = 0.55;
    }
}
