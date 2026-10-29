using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ContextWindow"/> (Arc Context window, design reference) and its one accent-independent derived
/// color. Surface, borders, text, series and status colors, the card radius and the type come from the ArcDesign tokens
/// in the theme; the summary cells' color is derived from series 1 in code because it follows the accent.
/// </summary>
[ControlDesignToken]
internal sealed class ContextWindowToken : AbstractControlDesignToken
{
    /// <summary>The card: 24 padding, 24 between header, body and footer, at most 744 wide.</summary>
    public Thickness CardPadding { get; set; }

    public double SectionGap { get; set; }

    public double CardMaxWidth { get; set; }

    /// <summary>The readout: a 36px display number on a 39.6 line with −1.08 letter spacing, then 14/19.6 text, lines 4 apart.</summary>
    public double ReadoutNumberSize { get; set; }

    public double ReadoutNumberLineHeight { get; set; }

    public double ReadoutNumberLetterSpacing { get; set; }

    public double ReadoutLineGap { get; set; }

    /// <summary>Body text: 14/19.6 with −0.14 letter spacing; small text 12/16.8.</summary>
    public double BodyLineHeight { get; set; }

    public double BodyLetterSpacing { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>Small text letter spacing (−0.01em of 12px).</summary>
    public double SmallLetterSpacing { get; set; }

    /// <summary>The model pill sits 16 right of the readout, or 16 under it when the header stacks.</summary>
    public Thickness PillMargin { get; set; }

    public Thickness StackedPillMargin { get; set; }

    /// <summary>The caption under the grid: 16 between its two parts, 6 between lines when it wraps.</summary>
    public double CaptionColumnGap { get; set; }

    public double CaptionRowGap { get; set; }

    /// <summary>Grid cells: 3 apart with radius 3 beside the legend, 2 and 2 when stacked; the threshold row is 5 high.</summary>
    public double CellGap { get; set; }

    public double StackedCellGap { get; set; }

    public double CellRadius { get; set; }

    public double StackedCellRadius { get; set; }

    public double ThresholdRowHeight { get; set; }

    /// <summary>The caption sits 12 under the grid.</summary>
    public double GridCaptionGap { get; set; }

    /// <summary>The legend: 260 wide and 32 from the grid; stacked under it 20 apart.</summary>
    public double LegendWidth { get; set; }

    public double BodyGap { get; set; }

    public double StackedBodyGap { get; set; }

    /// <summary>A legend row: 34 high, columns 10 / 1fr / auto / 47.6 with 10 between them, a 10px swatch with radius 3.</summary>
    public double LegendRowHeight { get; set; }

    public double LegendColumnGap { get; set; }

    public double PercentColumnWidth { get; set; }

    public double SwatchSize { get; set; }

    public CornerRadius SwatchRadius { get; set; }

    /// <summary>The footer: 20 above the actions, 12 between the actions and the status, 8 between actions.</summary>
    public Thickness FooterPadding { get; set; }

    public double FooterGap { get; set; }

    public double ActionGap { get; set; }

    /// <summary>The status icon is 15 with a 1.75 stroke, 8 before the text.</summary>
    public double StatusIconSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The model pill: 36 high, 12 left and 10 right, 8 between its parts, a 16px mark and a 15px chevron.</summary>
    public double PillHeight { get; set; }

    public Thickness PillPadding { get; set; }

    public double PillGap { get; set; }

    public double PillMarkSize { get; set; }

    public double ChevronSize { get; set; }

    /// <summary>Dimming while a legend row is hovered: other cells 0.28, other rows 0.45.</summary>
    public double CellDimOpacity { get; set; }

    public double RowDimOpacity { get; set; }

    /// <summary>The disabled model pill (while summarizing).</summary>
    public double PillDisabledOpacity { get; set; }

    /// <summary>Free cells after the threshold line: SurfaceMuted mixed 45% back into the surface (OKLab).</summary>
    public Color FreeAfterFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardPadding = new Thickness(24);
        SectionGap = 24;
        CardMaxWidth = 744;
        ReadoutNumberSize = 36;
        ReadoutNumberLineHeight = 39.6;
        ReadoutNumberLetterSpacing = -1.08;
        ReadoutLineGap = 4;
        BodyLineHeight = 19.6;
        BodyLetterSpacing = -0.14;
        SmallLineHeight = 16.8;
        SmallLetterSpacing = -0.12;
        PillMargin = new Thickness(16, 0, 0, 0);
        StackedPillMargin = new Thickness(0, 16, 0, 0);
        CaptionColumnGap = 16;
        CaptionRowGap = 6;
        CellGap = 3;
        StackedCellGap = 2;
        CellRadius = 3;
        StackedCellRadius = 2;
        ThresholdRowHeight = 5;
        GridCaptionGap = 12;
        LegendWidth = 260;
        BodyGap = 32;
        StackedBodyGap = 20;
        LegendRowHeight = 34;
        LegendColumnGap = 10;
        PercentColumnWidth = 47.6;
        SwatchSize = 10;
        SwatchRadius = new CornerRadius(3);
        FooterPadding = new Thickness(0, 20, 0, 0);
        FooterGap = 12;
        ActionGap = 8;
        StatusIconSize = 15;
        StatusGap = 8;
        PillHeight = 36;
        PillPadding = new Thickness(12, 0, 10, 0);
        PillGap = 8;
        PillMarkSize = 16;
        ChevronSize = 15;
        CellDimOpacity = 0.28;
        RowDimOpacity = 0.45;
        PillDisabledOpacity = 0.55;
        FreeAfterFill = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.55);
    }
}
