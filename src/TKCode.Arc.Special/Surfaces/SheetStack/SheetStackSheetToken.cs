using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Panel geometry of <see cref="SheetStackSheet"/> (Arc Sheet stack, design reference), as measured: a 36×5 grabber 8
/// below the top edge (sheets only), a 52 high header (6 10 2 padding around a 44 row: back pill, centered title, 36
/// round close button), the body's 8 16 4 padding, the footer's 12 16 0 padding with 8 between buttons and 16 under
/// everything. The dim of a level below is black at 16% (light) or 34% (dark).
/// </summary>
[ControlDesignToken]
internal sealed class SheetStackSheetToken : AbstractControlDesignToken
{
    public double GrabberWidth { get; set; }

    public double GrabberHeight { get; set; }

    public Thickness GrabberMargin { get; set; }

    /// <summary>The header: a 44 high row inside 6 10 2 padding (52 in all), 8 between the three columns.</summary>
    public double HeaderRowHeight { get; set; }

    public Thickness HeaderPadding { get; set; }

    public double HeaderGap { get; set; }

    /// <summary>The back pill: 36 high, 0 10 0 4 padding, 2 between the chevron and the label.</summary>
    public double BackButtonHeight { get; set; }

    public Thickness BackButtonPadding { get; set; }

    public double BackButtonGap { get; set; }

    /// <summary>The round close button (36) and the header glyphs (18).</summary>
    public double CloseButtonSize { get; set; }

    public double HeaderGlyphSize { get; set; }

    /// <summary>The title's letter spacing (−0.48 at 16px).</summary>
    public double TitleLetterSpacing { get; set; }

    public Thickness DescriptionPadding { get; set; }

    public Thickness BodyPadding { get; set; }

    public Thickness FooterPadding { get; set; }

    public double FooterGap { get; set; }

    /// <summary>Space under the body or footer (16).</summary>
    public Thickness SurfacePadding { get; set; }

    /// <summary>The focus ring's corner radius around the 36 high header buttons (18 + 4).</summary>
    public CornerRadius HeaderFocusRadius { get; set; }

    /// <summary>The dim laid over a level below: black at 16% (light) or 34% (dark).</summary>
    public Color DimColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GrabberWidth = 36;
        GrabberHeight = 5;
        GrabberMargin = new Thickness(0, 8, 0, 0);
        HeaderRowHeight = 44;
        HeaderPadding = new Thickness(10, 6, 10, 2);
        HeaderGap = 8;
        BackButtonHeight = 36;
        BackButtonPadding = new Thickness(4, 0, 10, 0);
        BackButtonGap = 2;
        CloseButtonSize = 36;
        HeaderGlyphSize = 18;
        TitleLetterSpacing = -0.48;
        DescriptionPadding = new Thickness(16, 0, 16, 4);
        BodyPadding = new Thickness(16, 8, 16, 4);
        FooterPadding = new Thickness(16, 12, 16, 0);
        FooterGap = 8;
        SurfacePadding = new Thickness(0, 0, 0, 16);
        HeaderFocusRadius = new CornerRadius(22);
        DimColor = Oklch.ToColor(0, 0, 0, isDarkMode ? 0.34 : 0.16);
    }
}
