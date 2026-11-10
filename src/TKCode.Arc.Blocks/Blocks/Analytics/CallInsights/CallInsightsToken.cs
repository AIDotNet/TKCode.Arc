using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CallInsights"/> and the accent-independent tints it derives from the neutrals. Colors that
/// follow the accent, the type sizes, the radii and the fonts come from ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class CallInsightsToken : AbstractControlDesignToken
{
    /// <summary>At or below this width the side column moves under the transcript (760).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's padding (24) and its compact form (16).</summary>
    public Thickness Padding { get; set; }

    public Thickness CompactPadding { get; set; }

    /// <summary>Space between the sections (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>The side column's width in the wide layout (300).</summary>
    public double SideWidth { get; set; }

    /// <summary>The sentiment track's height (72) and one speaker lane (10, 4 apart).</summary>
    public double SentimentHeight { get; set; }

    public double LaneHeight { get; set; }

    public double LaneGap { get; set; }

    /// <summary>The transcript's height before it scrolls (380).</summary>
    public double TranscriptHeight { get; set; }

    /// <summary>A transcript line's padding (10, 8).</summary>
    public Thickness LinePadding { get; set; }

    /// <summary>The talk-time bar's height (10).</summary>
    public double TalkBarHeight { get; set; }

    /// <summary>The keyboard focus ring sits this far outside a pressable row (−2).</summary>
    public Thickness FocusRingMargin { get; set; }

    /// <summary>The tint of a hovered row: the muted surface mixed 60% into the surface.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The track behind the sentiment curve: the muted surface mixed 45% into the surface.</summary>
    public Color TrackBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CompactWidth = 760;
        Padding = new Thickness(24);
        CompactPadding = new Thickness(16);
        SectionGap = 20;
        SideWidth = 300;
        SentimentHeight = 72;
        LaneHeight = 10;
        LaneGap = 4;
        TranscriptHeight = 380;
        LinePadding = new Thickness(10, 8);
        TalkBarHeight = 10;
        FocusRingMargin = new Thickness(-2);
        RowHoverBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.6);
        TrackBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.45);
    }
}
