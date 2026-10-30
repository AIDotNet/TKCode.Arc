using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="BuildingBlocks"/>: the three widths, the frame's proportions and radii (given at the 400px
/// reference width; the mini app scales them with its width), how strongly placeholders and fills take the tint, and
/// the status line. Colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class BuildingBlocksToken : AbstractControlDesignToken
{
    /// <summary>The width of <see cref="BuildingBlocksSize.Small"/>, <see cref="BuildingBlocksSize.Medium"/> and <see cref="BuildingBlocksSize.Large"/>.</summary>
    public double WidthSM { get; set; }

    public double WidthMD { get; set; }

    public double WidthLG { get; set; }

    /// <summary>The frame's height as a share of its width.</summary>
    public double FrameAspect { get; set; }

    /// <summary>The frame's corner at the reference width (scaled with the width).</summary>
    public CornerRadius FrameCornerRadius { get; set; }

    /// <summary>A block's corner at the reference width (scaled with the width).</summary>
    public CornerRadius BlockCornerRadius { get; set; }

    /// <summary>The width the radii above are given for.</summary>
    public double ReferenceWidth { get; set; }

    /// <summary>How opaque the tint is in a landed placeholder.</summary>
    public double PlaceholderOpacity { get; set; }

    /// <summary>How opaque the tint is under the chart line and in the price chip.</summary>
    public double FillOpacity { get; set; }

    /// <summary>The neutral content bars: titles and body lines, as shares of the muted text color.</summary>
    public double TitleInkOpacity { get; set; }

    public double LineInkOpacity { get; set; }

    /// <summary>The space between the frame and the status line, and the status line's fixed height.</summary>
    public double StatusGap { get; set; }

    public double StatusHeight { get; set; }

    /// <summary>The status mark, and the space after it.</summary>
    public double MarkSize { get; set; }

    public double MarkGap { get; set; }

    /// <summary>The phase label: 13px, 12px once the frame is narrower than <see cref="NarrowWidth"/>.</summary>
    public double LabelFontSize { get; set; }

    public double NarrowLabelFontSize { get; set; }

    public double NarrowWidth { get; set; }

    /// <summary>Replay: 28px high, 10px in from the sides.</summary>
    public double ReplayHeight { get; set; }

    public Thickness ReplayPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        WidthSM = 280;
        WidthMD = 400;
        WidthLG = 560;
        FrameAspect = 0.7;
        FrameCornerRadius = new CornerRadius(16);
        BlockCornerRadius = new CornerRadius(8);
        ReferenceWidth = 400;
        PlaceholderOpacity = isDarkMode ? 0.24 : 0.16;
        FillOpacity = isDarkMode ? 0.2 : 0.14;
        TitleInkOpacity = isDarkMode ? 0.62 : 0.5;
        LineInkOpacity = isDarkMode ? 0.34 : 0.26;
        StatusGap = 10;
        StatusHeight = 28;
        MarkSize = 16;
        MarkGap = 8;
        LabelFontSize = 13;
        NarrowLabelFontSize = 12;
        NarrowWidth = 320;
        ReplayHeight = 28;
        ReplayPadding = new Thickness(10, 0);
    }
}
