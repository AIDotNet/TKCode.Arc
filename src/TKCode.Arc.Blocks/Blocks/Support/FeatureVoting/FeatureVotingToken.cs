using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="FeatureVoting"/> (an original TKCode design). Colors, radii and fonts come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class FeatureVotingToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (420).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Padding inside the card (20).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Height of an option's share bar (6).</summary>
    public double BarHeight { get; set; }

    /// <summary>Diameter of an option's choice mark (20).</summary>
    public double IndicatorSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 420;
        CardPadding = new Thickness(20);
        BarHeight = 6;
        IndicatorSize = 20;
    }
}
