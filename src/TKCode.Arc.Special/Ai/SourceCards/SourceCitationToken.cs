using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SourceCitation"/>: the [n] chip (styled like the number on a card) and the transparent margin
/// around it that enlarges its hit area without moving the text around it.
/// </summary>
[ControlDesignToken]
internal sealed class SourceCitationToken : AbstractControlDesignToken
{
    /// <summary>The chip: 16px high, at least 16px wide, 11px semibold digits, radius 5.</summary>
    public double ChipHeight { get; set; }

    public double ChipMinWidth { get; set; }

    public double ChipFontSize { get; set; }

    public Thickness ChipPadding { get; set; }

    public CornerRadius ChipCornerRadius { get; set; }

    /// <summary>The space beside the chip in a line of text.</summary>
    public Thickness ChipMargin { get; set; }

    /// <summary>How far the hit area reaches past the chip (padding of the transparent hit box).</summary>
    public Thickness HitPadding { get; set; }

    /// <summary>The negative margin that keeps the enlarged hit box from pushing the text apart.</summary>
    public Thickness HitMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ChipHeight = 16;
        ChipMinWidth = 16;
        ChipFontSize = 11;
        ChipPadding = new Thickness(4, 0);
        ChipCornerRadius = new CornerRadius(5);
        ChipMargin = new Thickness(2, 0);
        HitPadding = new Thickness(4, 6);
        HitMargin = new Thickness(-4, -6);
    }
}
