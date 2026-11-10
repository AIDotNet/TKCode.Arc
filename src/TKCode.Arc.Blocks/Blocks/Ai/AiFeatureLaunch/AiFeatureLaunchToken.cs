using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AiFeatureLaunch"/> (an original TKCode design). Colors, radii, type sizes and fonts come from
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiFeatureLaunchToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (420).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The card's padding (8): the demo sits 8 from the edge; the text below has its own padding.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Padding of the text and the buttons under the demo (12, 16, 12, 12) and its compact form.</summary>
    public Thickness BodyPadding { get; set; }

    public Thickness CompactBodyPadding { get; set; }

    /// <summary>At or below this width the buttons stack (360).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The demo's height (184).</summary>
    public double DemoHeight { get; set; }

    /// <summary>Space between the title line, the description, the highlights and the buttons (12).</summary>
    public double BodyGap { get; set; }

    /// <summary>Space between highlights (8) and where a highlight's text starts after its check (10).</summary>
    public double HighlightGap { get; set; }

    public Thickness HighlightTextMargin { get; set; }

    /// <summary>Line height of the description (14 / 21).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>The square icon buttons on the demo (32) and their glyphs (16).</summary>
    public double IconButtonSize { get; set; }

    public double GlyphSize { get; set; }

    /// <summary>The icon buttons sit 8 from the demo's corner.</summary>
    public Thickness IconButtonMargin { get; set; }

    /// <summary>Padding of the "New" badge (8, 2).</summary>
    public Thickness BadgePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 420;
        CardPadding = new Thickness(8);
        BodyPadding = new Thickness(12, 16, 12, 12);
        CompactBodyPadding = new Thickness(8, 14, 8, 8);
        CompactWidth = 360;
        DemoHeight = 184;
        BodyGap = 12;
        HighlightGap = 8;
        HighlightTextMargin = new Thickness(10, 0, 0, 0);
        BodyLineHeight = 21;
        IconButtonSize = 32;
        GlyphSize = 16;
        IconButtonMargin = new Thickness(8);
        BadgePadding = new Thickness(8, 2);
    }
}
