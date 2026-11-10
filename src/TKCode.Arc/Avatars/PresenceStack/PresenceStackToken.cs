namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PresenceStack"/>: how far each avatar covers the next, the space before the count, the idle
/// avatar's opacity and the chip's count size. Colors, radii and type come from the ArcDesign tokens in the theme; the
/// avatar sizes from the Arc <c>Avatar</c>.
/// </summary>
[ControlDesignToken]
internal sealed class PresenceStackToken : AbstractControlDesignToken
{
    /// <summary>How far each person covers the one after them (10).</summary>
    public double Overlap { get; set; }

    /// <summary>Space between the avatars and the count (8).</summary>
    public double CountGap { get; set; }

    /// <summary>An idle viewer's avatar opacity (0.45).</summary>
    public double IdleOpacity { get; set; }

    /// <summary>The chip's count (11).</summary>
    public double ChipFontSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Overlap = 10;
        CountGap = 8;
        IdleOpacity = 0.45;
        ChipFontSize = 11;
    }
}
