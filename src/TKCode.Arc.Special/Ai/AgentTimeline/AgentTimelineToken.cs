using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="AgentTimeline"/> (Arc Agent timeline, design reference): the
/// header, the label column and track paddings (wide and narrow), the footer gaps, the span card, and the tints the drawn
/// spans mix from the agent colors. The plot's row, bar and axis geometry is fixed in <c>AgentTimelineLayout</c>; colors,
/// radii, shadows and type come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AgentTimelineToken : AbstractControlDesignToken
{
    /// <summary>The title: Geist 16/17.6, letter spacing −0.16.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    /// <summary>Body text 14/19.6 and small text 12/16.8 line heights.</summary>
    public double BodyLineHeight { get; set; }

    public double SmallLineHeight { get; set; }

    /// <summary>The card's padding (20 above and below), the 16 between header, body and footer, and the 2 under the title.</summary>
    public Thickness SectionPadding { get; set; }

    public double SectionGap { get; set; }

    public double HeaderGap { get; set; }

    /// <summary>Header and footer side padding: 24, or 16 below a 520 wide container.</summary>
    public Thickness EdgePadding { get; set; }

    public Thickness NarrowEdgePadding { get; set; }

    /// <summary>The track around the plot: 12 left, 24 right (16 narrow).</summary>
    public Thickness TrackPadding { get; set; }

    public Thickness NarrowTrackPadding { get; set; }

    /// <summary>Footer: playback buttons 6 apart, controls and legend 16 apart (12 between wrapped rows), legend items 14 apart (4 between lines).</summary>
    public double ControlGap { get; set; }

    public double FooterColumnGap { get; set; }

    public double FooterRowGap { get; set; }

    public double LegendItemGap { get; set; }

    public double LegendLineGap { get; set; }

    /// <summary>The span card: 280 wide, padding 12 / 14, rows 3 apart.</summary>
    public double HoverCardWidth { get; set; }

    public Thickness HoverCardPadding { get; set; }

    public double HoverCardRowGap { get; set; }

    /// <summary>The card's fill: the surface in light mode, the raised surface in dark mode.</summary>
    public Color HoverCardBackground { get; set; }

    /// <summary>A thinking span mixes this much of the agent color into the surface (24% light, 34% dark).</summary>
    public double ThinkingTint { get; set; }

    /// <summary>A hovered thinking span mixes this much (34% light, 44% dark).</summary>
    public double ThinkingHoverTint { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        BodyLineHeight = 19.6;
        SmallLineHeight = 16.8;
        SectionPadding = new Thickness(0, 20);
        SectionGap = 16;
        HeaderGap = 2;
        EdgePadding = new Thickness(24, 0);
        NarrowEdgePadding = new Thickness(16, 0);
        TrackPadding = new Thickness(12, 0, 24, 0);
        NarrowTrackPadding = new Thickness(12, 0, 16, 0);
        ControlGap = 6;
        FooterColumnGap = 16;
        FooterRowGap = 12;
        LegendItemGap = 14;
        LegendLineGap = 4;
        HoverCardWidth = 280;
        HoverCardPadding = new Thickness(14, 12);
        HoverCardRowGap = 3;
        HoverCardBackground = isDarkMode ? neutrals.SurfaceRaised : neutrals.Surface;
        ThinkingTint = isDarkMode ? 0.34 : 0.24;
        ThinkingHoverTint = isDarkMode ? 0.44 : 0.34;
    }
}
