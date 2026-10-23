using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and type sizes of <see cref="ActivityTerrain"/> (Arc Activity terrain, design reference). Colors, shadows
/// and fonts come from ArcDesign tokens in the theme; the plot's colors are resolved from them in code.
/// </summary>
[ControlDesignToken]
internal sealed class ActivityTerrainToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block: 16 above, 20 at the sides and below.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Space inside the block below 560: 16 all round.</summary>
    public Thickness NarrowBlockPadding { get; set; }

    /// <summary>Gap between the header, the toolbar and the body (16; 12 below 560).</summary>
    public double SectionGap { get; set; }

    public double NarrowSectionGap { get; set; }

    /// <summary>Corner radius of the block's card.</summary>
    public CornerRadius BlockRadius { get; set; }

    /// <summary>Body text size (13/18.2) and its line height.</summary>
    public double BodyTextSize { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Title type: 16/17.6, tracking −0.48.</summary>
    public double TitleSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double TitleTracking { get; set; }

    /// <summary>Small type (legend, card, captions): 12/16.8.</summary>
    public double CaptionSize { get; set; }

    public double CaptionLineHeight { get; set; }

    /// <summary>Gap between the plot and the legend row.</summary>
    public double LegendGap { get; set; }

    /// <summary>Corner radius of the plot (and its focus ring).</summary>
    public CornerRadius PlotRadius { get; set; }

    /// <summary>A legend swatch: 18 × 8, radius 2, 2 apart.</summary>
    public double SwatchWidth { get; set; }

    public double SwatchHeight { get; set; }

    public CornerRadius SwatchRadius { get; set; }

    public double SwatchGap { get; set; }

    /// <summary>The hover card: 208 wide, padding 10 / 12.</summary>
    public double CardWidth { get; set; }

    public Thickness CardPadding { get; set; }

    /// <summary>Width of the summary column beside the plot (880 and wider) and the gap before it.</summary>
    public double StatsColumnWidth { get; set; }

    public double StatsGap { get; set; }

    /// <summary>The big total: 28/32.2, tracking −0.56.</summary>
    public double TotalSize { get; set; }

    /// <summary>The busiest day's name: 16/22.4.</summary>
    public double DayNameSize { get; set; }

    /// <summary>A peak hour row: padding 7 / 8, radius 8, list pulled out 8px each side.</summary>
    public Thickness PeakRowPadding { get; set; }

    public CornerRadius PeakRowRadius { get; set; }

    public Thickness PeakListMargin { get; set; }

    /// <summary>The "Reset view" button: 32 high, padding 10 / 12, radius 10, 10px from the plot's corner.</summary>
    public double ResetHeight { get; set; }

    public Thickness ResetPadding { get; set; }

    public CornerRadius ResetRadius { get; set; }

    public Thickness ResetMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(20, 16, 20, 20);
        NarrowBlockPadding = new Thickness(16);
        SectionGap = 16;
        NarrowSectionGap = 12;
        BlockRadius = new CornerRadius(16);
        BodyTextSize = 13;
        BodyLineHeight = 18.2;
        TitleSize = 16;
        TitleLineHeight = 17.6;
        TitleTracking = -0.48;
        CaptionSize = 12;
        CaptionLineHeight = 16.8;
        LegendGap = 12;
        PlotRadius = new CornerRadius(12);
        SwatchWidth = 18;
        SwatchHeight = 8;
        SwatchRadius = new CornerRadius(2);
        SwatchGap = 2;
        CardWidth = 208;
        CardPadding = new Thickness(12, 10);
        StatsColumnWidth = 224;
        StatsGap = 20;
        TotalSize = 28;
        DayNameSize = 16;
        PeakRowPadding = new Thickness(8, 7);
        PeakRowRadius = new CornerRadius(8);
        PeakListMargin = new Thickness(-8, 2, -8, 0);
        ResetHeight = 32;
        ResetPadding = new Thickness(10, 0, 12, 0);
        ResetRadius = new CornerRadius(10);
        ResetMargin = new Thickness(0, 10, 10, 0);
    }
}
