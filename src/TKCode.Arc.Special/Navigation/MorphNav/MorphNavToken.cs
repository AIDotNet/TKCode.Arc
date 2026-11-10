using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MorphNav"/>: the pill, the open panel and its links. Colors, radii and shadows come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MorphNavToken : AbstractControlDesignToken
{
    /// <summary>Height of the closed pill.</summary>
    public double PillHeight { get; set; }

    /// <summary>Space around the pill's content.</summary>
    public Thickness PillPadding { get; set; }

    /// <summary>Gap between the pill's mark, title, current page and menu glyph.</summary>
    public double PillSpacing { get; set; }

    /// <summary>Width of the open panel when <see cref="MorphNav.PanelWidth"/> is not set.</summary>
    public double PanelWidth { get; set; }

    /// <summary>The open panel keeps at least this far from the sides of the window.</summary>
    public double PanelGutter { get; set; }

    /// <summary>Space around the open panel's content.</summary>
    public Thickness PanelPadding { get; set; }

    /// <summary>Gap between the panel's header, sections and footer.</summary>
    public double PanelSpacing { get; set; }

    /// <summary>A section column is at least this wide; narrower panels put fewer sections side by side.</summary>
    public double MinColumnWidth { get; set; }

    /// <summary>Gap between section columns and between rows of sections.</summary>
    public double ColumnGap { get; set; }

    /// <summary>Height of a link without a description.</summary>
    public double LinkHeight { get; set; }

    /// <summary>Height of a link with a description.</summary>
    public double LinkTallHeight { get; set; }

    /// <summary>Space between a link's edge and its glyph tile or text.</summary>
    public double LinkInset { get; set; }

    /// <summary>Size of a link's glyph tile.</summary>
    public double LinkTileSize { get; set; }

    /// <summary>Size of a link's glyph.</summary>
    public double LinkGlyphSize { get; set; }

    /// <summary>Tint of a link under the pointer.</summary>
    public Color LinkHoverColor { get; set; }

    /// <summary>Margin of the pill's keyboard focus ring: inside the pill, since the surface clips what is outside it.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        PillHeight = 44;
        PillPadding = new Thickness(16, 0, 14, 0);
        PillSpacing = 10;
        PanelWidth = 600;
        PanelGutter = 16;
        PanelPadding = new Thickness(16, 10, 16, 16);
        PanelSpacing = 12;
        MinColumnWidth = 176;
        ColumnGap = 12;
        LinkHeight = 36;
        LinkTallHeight = 52;
        LinkInset = 8;
        LinkTileSize = 32;
        LinkGlyphSize = 16;
        LinkHoverColor = Oklch.WithAlpha(neutrals.Foreground, 0.06);
        FocusRingMargin = new Thickness(2);
    }
}
