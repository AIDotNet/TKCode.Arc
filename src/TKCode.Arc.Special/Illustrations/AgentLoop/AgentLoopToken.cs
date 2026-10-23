using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AgentLoop"/> (Arc Agent loop, design reference): the header, the chips and context blocks, the
/// lane placeholders and the footer. Colors, radii of the card, shadows and type come from the ArcDesign tokens in the
/// theme; the ring's geometry is solved from the measured width (<c>AgentLoopGeometry</c>).
/// </summary>
[ControlDesignToken]
internal sealed class AgentLoopToken : AbstractControlDesignToken
{
    /// <summary>The title: 16/17.6, letter spacing −0.16.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>The header's first row (title and status) is 22.4 high, 4 above the caption.</summary>
    public double HeaderRowHeight { get; set; }

    public Thickness CaptionMargin { get; set; }

    /// <summary>The caption always keeps two lines (2 × 19.6).</summary>
    public double CaptionMinHeight { get; set; }

    /// <summary>Body text line height (14/19.6) and small text line height (12/16.8).</summary>
    public double BodyLineHeight { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>Space between the header, the stage and the footer.</summary>
    public double SectionGap { get; set; }

    /// <summary>Chips, blocks and placeholders: radius 14, padding 8 / 12.</summary>
    public CornerRadius ChipCornerRadius { get; set; }

    public Thickness ChipPadding { get; set; }

    /// <summary>The gap between a chip's mark and its title (the result line sits under the title, 24 in).</summary>
    public double ChipMarkGap { get; set; }

    public Thickness ChipBodyMargin { get; set; }

    /// <summary>A context block's columns: mark, label, tokens 10 apart.</summary>
    public double BlockColumnGap { get; set; }

    /// <summary>Marks in chips and blocks (16), the station icons (14) and the center mark (22, 20 compact).</summary>
    public double MarkSize { get; set; }

    public double StationIconSize { get; set; }

    public double CenterMarkSize { get; set; }

    public double CompactCenterMarkSize { get; set; }

    /// <summary>The footer: 16 above its row(s) under a 1px border, buttons 6 apart, the speed words 12 before the control.</summary>
    public double FooterTopPadding { get; set; }

    public double ButtonGap { get; set; }

    public double SpeedGap { get; set; }

    /// <summary>The opacity of a chip whose station is not active.</summary>
    public double DimChipOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        HeaderRowHeight = 22.4;
        CaptionMargin = new Thickness(0, 4, 0, 0);
        CaptionMinHeight = 39.2;
        BodyLineHeight = 19.6;
        SmallLineHeight = 16.8;
        SectionGap = 20;
        ChipCornerRadius = new CornerRadius(14);
        ChipPadding = new Thickness(12, 8);
        ChipMarkGap = 8;
        ChipBodyMargin = new Thickness(24, 1, 0, 0);
        BlockColumnGap = 10;
        MarkSize = 16;
        StationIconSize = 14;
        CenterMarkSize = 22;
        CompactCenterMarkSize = 20;
        FooterTopPadding = 16;
        ButtonGap = 6;
        SpeedGap = 12;
        DimChipOpacity = 0.62;
    }
}
