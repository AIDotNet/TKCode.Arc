using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and lightbox colors of <see cref="LightboxGallery"/>. The grid's colors, corner radii and shadows come from
/// the ArcDesign tokens in the theme; the lightbox is always dark, whatever the theme, so its backdrop and text are set
/// here.
/// </summary>
[ControlDesignToken]
internal sealed class LightboxGalleryToken : AbstractControlDesignToken
{
    /// <summary>Space between two tiles of the grid.</summary>
    public double TileSpacing { get; set; }

    /// <summary>Space between the picture and the edges of the window in the lightbox.</summary>
    public Thickness PhotoMargin { get; set; }

    /// <summary>The same space on a narrow window.</summary>
    public Thickness PhotoMarginCompact { get; set; }

    /// <summary>Padding of the lightbox's top bar.</summary>
    public Thickness BarPadding { get; set; }

    /// <summary>Space between the buttons of the top bar.</summary>
    public double BarSpacing { get; set; }

    /// <summary>Space between the edge of the window and the side arrows.</summary>
    public Thickness ArrowMargin { get; set; }

    /// <summary>Margin of the caption under the picture.</summary>
    public Thickness CaptionMargin { get; set; }

    /// <summary>The backdrop behind the lightbox.</summary>
    public Color BackdropColor { get; set; }

    /// <summary>Text on the backdrop: the counter and the caption.</summary>
    public Color ChromeTextColor { get; set; }

    /// <summary>Quieter text on the backdrop.</summary>
    public Color ChromeMutedColor { get; set; }

    /// <summary>Margin of a tile's keyboard focus ring.</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>How much a tile's picture grows under the pointer.</summary>
    public double TileHoverScale { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TileSpacing = 8;
        PhotoMargin = new Thickness(72, 64);
        PhotoMarginCompact = new Thickness(0, 56);
        BarPadding = new Thickness(16, 12);
        BarSpacing = 8;
        ArrowMargin = new Thickness(16);
        CaptionMargin = new Thickness(24, 0, 24, 18);
        BackdropColor = Color.FromArgb(0xF0, 0x0B, 0x0B, 0x0D);
        ChromeTextColor = Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF);
        ChromeMutedColor = Color.FromArgb(0xA6, 0xFF, 0xFF, 0xFF);
        FocusRingMargin = new Thickness(-3);
        TileHoverScale = 1.04;
    }
}
