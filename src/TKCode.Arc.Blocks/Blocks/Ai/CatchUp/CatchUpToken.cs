using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CatchUp"/> (an original TKCode design). Colors, radii, type sizes and fonts come from ArcDesign
/// tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CatchUpToken : AbstractControlDesignToken
{
    /// <summary>The widest the card grows (600).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The card's padding (20) and its compact form (16).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>At or below this width the paddings tighten and the mark-all button moves under the title (440).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Space between the card's sections (16).</summary>
    public double SectionGap { get; set; }

    /// <summary>Padding of the recap (14) and its line height (21).</summary>
    public Thickness RecapPadding { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Padding of an item (10, 10) and the leading mark's size (32).</summary>
    public Thickness RowPadding { get; set; }

    public double MarkSize { get; set; }

    /// <summary>The unread dot (8).</summary>
    public double DotSize { get; set; }

    /// <summary>The read button (32) and the glyphs (16).</summary>
    public double IconButtonSize { get; set; }

    public double GlyphSize { get; set; }

    /// <summary>Focus rings sit 2 outside the item.</summary>
    public Thickness FocusMargin { get; set; }

    /// <summary>A read item's text fades to this opacity (0.72).</summary>
    public double ReadOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 600;
        CardPadding = new Thickness(20);
        CompactCardPadding = new Thickness(16);
        CompactWidth = 440;
        SectionGap = 16;
        RecapPadding = new Thickness(14);
        BodyLineHeight = 21;
        RowPadding = new Thickness(10);
        MarkSize = 32;
        DotSize = 8;
        IconButtonSize = 32;
        GlyphSize = 16;
        FocusMargin = new Thickness(-2);
        ReadOpacity = 0.72;
    }
}
