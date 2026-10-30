using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ImageReveal"/>: the canvas's largest width and its corners per size, the band, the grain's
/// strength, the progress pill (inset, height, padding, ring and type per size) and the error state. The surface tone and
/// the queued shimmer's light are accent-independent colors derived from the neutrals; the accent, the text colors and the
/// danger color come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ImageRevealToken : AbstractControlDesignToken
{
    /// <summary>The canvas's largest width (it fills its container up to this).</summary>
    public double CanvasMaxWidth { get; set; }

    /// <summary>The canvas's corners: Small (grids, thumbnails), Medium and Large.</summary>
    public CornerRadius CanvasCornerRadiusSM { get; set; }

    public CornerRadius CanvasCornerRadius { get; set; }

    public CornerRadius CanvasCornerRadiusLG { get; set; }

    /// <summary>The calm surface tone of the canvas: the muted surface, a touch deeper so it reads on a page.</summary>
    public Color SurfaceTone { get; set; }

    /// <summary>The light of the queued shimmer.</summary>
    public Color ShimmerLight { get; set; }

    /// <summary>The soft-edged band's height as a share of the canvas's height.</summary>
    public double BandRatio { get; set; }

    /// <summary>The film grain's strength (multiplies the grain texture's alpha).</summary>
    public double GrainOpacity { get; set; }

    /// <summary>The pill's inset from the canvas's top left corner, per size.</summary>
    public Thickness PillMarginSM { get; set; }

    public Thickness PillMargin { get; set; }

    public Thickness PillMarginLG { get; set; }

    /// <summary>The pill's height, per size.</summary>
    public double PillHeightSM { get; set; }

    public double PillHeight { get; set; }

    public double PillHeightLG { get; set; }

    /// <summary>The pill's inner padding, per size.</summary>
    public Thickness PillPaddingSM { get; set; }

    public Thickness PillPadding { get; set; }

    public Thickness PillPaddingLG { get; set; }

    /// <summary>The space between the ring, the label and the count.</summary>
    public double PillGap { get; set; }

    /// <summary>The pill's type, per size.</summary>
    public double PillFontSizeSM { get; set; }

    public double PillFontSize { get; set; }

    public double PillFontSizeLG { get; set; }

    /// <summary>The ring (and the check and spinner in its place), per size.</summary>
    public double RingSizeSM { get; set; }

    public double RingSize { get; set; }

    public double RingSizeLG { get; set; }

    public double RingThickness { get; set; }

    /// <summary>The translucent pill's fill opacity over the image.</summary>
    public double PillFillOpacity { get; set; }

    /// <summary>The error state: the alert's size, the gap between its parts and the message's width.</summary>
    public double ErrorGlyphSize { get; set; }

    public double ErrorGap { get; set; }

    public double ErrorMaxWidth { get; set; }

    public Thickness ErrorPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CanvasMaxWidth = 560;
        CanvasCornerRadiusSM = new CornerRadius(12);
        CanvasCornerRadius = new CornerRadius(18);
        CanvasCornerRadiusLG = new CornerRadius(24);
        SurfaceTone = Oklch.MixOklab(n.SurfaceMuted, n.Foreground, isDarkMode ? 0.03 : 0.035);
        ShimmerLight = isDarkMode
            ? Oklch.WithAlpha(Oklch.MixOklab(n.SurfaceMuted, n.Foreground, 0.12), 0.9)
            : Oklch.WithAlpha(n.Surface, 0.85);
        BandRatio = 0.24;
        GrainOpacity = 1;
        PillMarginSM = new Thickness(8);
        PillMargin = new Thickness(12);
        PillMarginLG = new Thickness(16);
        PillHeightSM = 24;
        PillHeight = 28;
        PillHeightLG = 32;
        PillPaddingSM = new Thickness(6, 0, 8, 0);
        PillPadding = new Thickness(8, 0, 10, 0);
        PillPaddingLG = new Thickness(10, 0, 12, 0);
        PillGap = 6;
        PillFontSizeSM = 11;
        PillFontSize = 12;
        PillFontSizeLG = 13;
        RingSizeSM = 12;
        RingSize = 14;
        RingSizeLG = 16;
        RingThickness = 2;
        PillFillOpacity = 0.78;
        ErrorGlyphSize = 20;
        ErrorGap = 8;
        ErrorMaxWidth = 260;
        ErrorPadding = new Thickness(16);
    }
}
