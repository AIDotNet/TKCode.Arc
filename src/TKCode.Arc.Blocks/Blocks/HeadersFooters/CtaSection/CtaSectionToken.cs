using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="CtaSection"/> block (cta-section.module.css).</summary>
[ControlDesignToken]
internal sealed class CtaSectionToken : AbstractControlDesignToken
{
    /// <summary>The block's content is at most 1120 wide (the banner's gutter adds 2 × 32).</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>The description of the centered card is at most 34rem (544).</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>The setup card is as wide as its stage, at most 360.</summary>
    public double SetupMaxWidth { get; set; }

    /// <summary>The split stage's minimum height (380; 320 when the block is at most 860 wide).</summary>
    public double StageMinHeight { get; set; }

    /// <summary>The corner radius of the banner (20).</summary>
    public CornerRadius BannerRadius { get; set; }

    /// <summary>The corner radius of a card on a narrow block (28).</summary>
    public CornerRadius NarrowCardRadius { get; set; }

    /// <summary>The banner's padding: 10 at the edge of the action side, 20 at the text.</summary>
    public Thickness BannerPadding { get; set; }

    /// <summary>The setup card's step: at least 44 high, radius 14, padding 0 10.</summary>
    public double StepHeight { get; set; }

    public CornerRadius StepRadius { get; set; }

    /// <summary>The 20 px mark of a step.</summary>
    public double MarkSize { get; set; }

    /// <summary>The face size on the note line (26) and on the setup card (22).</summary>
    public double NoteFaceSize { get; set; }

    public double SetupFaceSize { get; set; }

    /// <summary>The keyboard focus ring around the dismiss button (36 round, ring 2 px outside).</summary>
    public Thickness DismissFocusRingMargin { get; set; }

    /// <summary>The banner's content plus its 2 × 32 gutter (1184).</summary>
    public double BannerMaxWidth { get; set; }

    /// <summary>The centered card's quiet fill: halfway-ish from Surface to SurfaceMuted (45%).</summary>
    public Color CenteredBackground { get; set; }

    /// <summary>The split stage's fill (55% of the way from Surface to SurfaceMuted).</summary>
    public Color StageBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CenteredBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.45);
        StageBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.55);
        ContentMaxWidth = 1120;
        BannerMaxWidth = 1184;
        DescriptionMaxWidth = 544;
        SetupMaxWidth = 360;
        StageMinHeight = 380;
        BannerRadius = new CornerRadius(20);
        NarrowCardRadius = new CornerRadius(28);
        BannerPadding = new Thickness(20, 10, 10, 10);
        StepHeight = 44;
        StepRadius = new CornerRadius(14);
        MarkSize = 20;
        NoteFaceSize = 26;
        SetupFaceSize = 22;
        DismissFocusRingMargin = new Thickness(-2);
    }
}
