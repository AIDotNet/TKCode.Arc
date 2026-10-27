using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and type sizes of <see cref="LineReplay"/>'s templated parts (Arc Line replay, design reference). Colors,
/// fonts and the type ramp come from ArcDesign tokens in the theme; the self-drawn chart and scrubber keep their metrics
/// in <see cref="LineReplayStage"/> and <see cref="LineReplayScrubber"/>.
/// </summary>
[ControlDesignToken]
internal sealed class LineReplayToken : AbstractControlDesignToken
{
    /// <summary>Space around the chart column (24; 16 below 560).</summary>
    public Thickness MainPadding { get; set; }

    public Thickness NarrowMainPadding { get; set; }

    /// <summary>The moments column beside the chart: 300 wide, padding 24 above, 16 at the sides and below.</summary>
    public double AsideWidth { get; set; }

    public Thickness AsidePadding { get; set; }

    /// <summary>The moments under the chart: padding 20 above, 16 at the sides and below (8 at the sides below 560).</summary>
    public Thickness StackedAsidePadding { get; set; }

    public Thickness NarrowAsidePadding { get; set; }

    /// <summary>The title: 22 on a 24 line; the description 14, 4 under it.</summary>
    public double TitleLineHeight { get; set; }

    public double DescriptionGap { get; set; }

    /// <summary>Between the header, the readout, the chart and the transport (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>A legend swatch (20 × 8), 8 before its text, 16 between the two entries.</summary>
    public double SwatchWidth { get; set; }

    public double SwatchTextGap { get; set; }

    public double LegendGap { get; set; }

    /// <summary>Between the play button, the scrubber and the speed control (16).</summary>
    public double TransportGap { get; set; }

    /// <summary>A moment row: padding 10 / 12, radius 18, 12 between the icon and the text, 2 under the title.</summary>
    public Thickness MomentPadding { get; set; }

    public CornerRadius MomentRadius { get; set; }

    public double MomentIconGap { get; set; }

    public double MomentLineGap { get; set; }

    /// <summary>Small text (time, detail, effect): 12 on a 16.8 line.</summary>
    public double CaptionLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MainPadding = new Thickness(24);
        NarrowMainPadding = new Thickness(16, 20, 16, 20);
        AsideWidth = 300;
        AsidePadding = new Thickness(16, 24, 16, 16);
        StackedAsidePadding = new Thickness(16, 20, 16, 16);
        NarrowAsidePadding = new Thickness(8, 20, 8, 16);
        TitleLineHeight = 24;
        DescriptionGap = 4;
        SectionGap = 20;
        SwatchWidth = 20;
        SwatchTextGap = 8;
        LegendGap = 16;
        TransportGap = 16;
        MomentPadding = new Thickness(12, 10);
        MomentRadius = new CornerRadius(18);
        MomentIconGap = 12;
        MomentLineGap = 2;
        CaptionLineHeight = 16.8;
    }
}
