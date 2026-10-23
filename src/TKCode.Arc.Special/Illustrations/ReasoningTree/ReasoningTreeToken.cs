using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the <see cref="ReasoningTree"/> card (Arc Reasoning tree, design reference): the card and footer padding,
/// the header row, the gaps between header, stage and footer, and the footer groups. Colors, radii, type and shadows come
/// from the ArcDesign tokens in the theme; the drawn tree's geometry lives in <c>ReasoningTreeLayout</c>.
/// </summary>
[ControlDesignToken]
internal sealed class ReasoningTreeToken : AbstractControlDesignToken
{
    /// <summary>Space inside the card: 20 at the top, 24 at the sides, none at the bottom (the footer bleeds to the edge).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The card padding under 520px: 16 at the top and sides.</summary>
    public Thickness CompactCardPadding { get; set; }

    /// <summary>The header row: 24 high, 8 between the model and the title, at least 12 before the status.</summary>
    public double HeaderRowHeight { get; set; }

    public double TitleGap { get; set; }

    public Thickness StatusMargin { get; set; }

    /// <summary>The stage sits 20 under the header and 20 above the footer.</summary>
    public Thickness StageMargin { get; set; }

    /// <summary>The footer: a 1px rule across the card, 12 above and below its controls, 24 at the sides (16 compact).</summary>
    public Thickness FooterPadding { get; set; }

    public Thickness CompactFooterPadding { get; set; }

    /// <summary>The footer's negative side margin that makes its rule meet the card border.</summary>
    public Thickness FooterBleed { get; set; }

    public Thickness CompactFooterBleed { get; set; }

    /// <summary>Gaps: 8 between the playback buttons, 12 between the beam label and its control.</summary>
    public double TransportGap { get; set; }

    public double BeamGap { get; set; }

    /// <summary>At least 16 between the playback group and the beam group; 12 above the beam group once it wraps under.</summary>
    public Thickness BeamGroupMargin { get; set; }

    public Thickness StackedBeamGroupMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardPadding = new Thickness(24, 20, 24, 0);
        CompactCardPadding = new Thickness(16, 16, 16, 0);
        HeaderRowHeight = 24;
        TitleGap = 8;
        StatusMargin = new Thickness(12, 0, 0, 0);
        StageMargin = new Thickness(0, 20);
        FooterPadding = new Thickness(24, 12);
        CompactFooterPadding = new Thickness(16, 12);
        FooterBleed = new Thickness(-24, 0);
        CompactFooterBleed = new Thickness(-16, 0);
        TransportGap = 8;
        BeamGap = 12;
        BeamGroupMargin = new Thickness(16, 0, 0, 0);
        StackedBeamGroupMargin = new Thickness(0, 12, 0, 0);
    }
}
