using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SpotlightGrid"/>: paddings, the grid's gaps and the reach of the light. Radii, fonts and every
/// color (the light is the accent) come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SpotlightGridToken : AbstractControlDesignToken
{
    /// <summary>The section's padding (24 80).</summary>
    public Thickness SectionPadding { get; set; }

    /// <summary>The section's padding below the compact width (16 48).</summary>
    public Thickness CompactSectionPadding { get; set; }

    /// <summary>The content is at most this wide (1120).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The heading column is at most this wide (640).</summary>
    public double HeaderMaxWidth { get; set; }

    /// <summary>The space between the eyebrow, the title and the description (12).</summary>
    public double HeaderGap { get; set; }

    /// <summary>The title's line height at 36 (44).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The title's line height at 28, below the compact width (34).</summary>
    public double CompactTitleLineHeight { get; set; }

    /// <summary>The space between the heading and the grid (0 0 0 48).</summary>
    public Thickness HeaderMargin { get; set; }

    /// <summary>The space between cards (16).</summary>
    public double Gap { get; set; }

    /// <summary>A card's padding (24).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The icon tile's edge (40).</summary>
    public double IconTile { get; set; }

    /// <summary>The space above the card's title (0 20 0 6).</summary>
    public Thickness CardTitleMargin { get; set; }

    /// <summary>The description's line height (21).</summary>
    public double DescriptionLineHeight { get; set; }

    /// <summary>How far the light reaches from its center (240).</summary>
    public double SpotlightRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SectionPadding = new Thickness(24, 80);
        CompactSectionPadding = new Thickness(16, 48);
        ContentMaxWidth = 1120;
        HeaderMaxWidth = 640;
        HeaderGap = 12;
        TitleLineHeight = 44;
        CompactTitleLineHeight = 34;
        HeaderMargin = new Thickness(0, 0, 0, 48);
        Gap = 16;
        CardPadding = new Thickness(24);
        IconTile = 40;
        CardTitleMargin = new Thickness(0, 20, 0, 6);
        DescriptionLineHeight = 21;
        SpotlightRadius = 240;
    }
}
