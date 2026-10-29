using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the <see cref="OnboardingIllustration"/> card (measured on Arc's preview; colors come from the design tokens).</summary>
[ControlDesignToken]
internal sealed class OnboardingIllustrationToken : AbstractControlDesignToken
{
    /// <summary>The card is at most 440 wide; radius 20.</summary>
    public double CardMaxWidth { get; set; }

    public CornerRadius CardRadius { get; set; }

    /// <summary>The stage's top corners follow the card's inner radius (19); square when the card is not framed.</summary>
    public CornerRadius StageRadius { get; set; }

    /// <summary>Stage padding 40 above, 20 at the sides, 16 below; compact 42 / 12 / 12.</summary>
    public Thickness StageInset { get; set; }

    public Thickness CompactStageInset { get; set; }

    /// <summary>The drawing is at most 352 wide (8:5).</summary>
    public double ArtMaxWidth { get; set; }

    /// <summary>Stroke of the drawing in view-box units (1.5px at 352 wide); the check is a little heavier.</summary>
    public double ArtStrokeWidth { get; set; }

    public double ArtCheckStrokeWidth { get; set; }

    /// <summary>Skip sits 10 from the stage's top right corner: 28 high, 10 at the sides.</summary>
    public Thickness SkipOffset { get; set; }

    public double SkipHeight { get; set; }

    public Thickness SkipInset { get; set; }

    /// <summary>Body padding 20 / 24 (compact 16 all round) and the gap between copy and buttons (20, compact 16).</summary>
    public Thickness BodyInset { get; set; }

    public Thickness CompactBodyInset { get; set; }

    public double BodyGap { get; set; }

    public double CompactBodyGap { get; set; }

    /// <summary>The copy keeps at least 72 (compact 88) so the buttons do not jump between one- and two-line sentences.</summary>
    public double CopyMinHeight { get; set; }

    public double CompactCopyMinHeight { get; set; }

    /// <summary>6 between title and sentence.</summary>
    public double CopyLineGap { get; set; }

    /// <summary>Title 18 / 19.8 (compact 16 / 17.6), tracking −0.01em; sentence line height 19.6.</summary>
    public double TitleLineHeight { get; set; }

    public double CompactTitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double CompactTitleLetterSpacing { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The card's text tracking (−0.16px, inherited from 16px × −0.01em).</summary>
    public double TextLetterSpacing { get; set; }

    /// <summary>Back and the main button: 36 high, radius 12, 8 apart, 12 from the dots.</summary>
    public double ActionHeight { get; set; }

    public CornerRadius ActionRadius { get; set; }

    public double ActionGap { get; set; }

    public Thickness ActionsOffset { get; set; }

    /// <summary>Back: 14 at the sides (compact: a 36 square arrow). The main button: 12 at the sides, 136 wide (compact 128).</summary>
    public Thickness BackInset { get; set; }

    public double CompactBackWidth { get; set; }

    public Thickness PrimaryInset { get; set; }

    public double PrimaryWidth { get; set; }

    public double CompactPrimaryWidth { get; set; }

    /// <summary>6 between a glyph and its label; glyphs 16 with a 1.75 stroke.</summary>
    public double LabelGap { get; set; }

    public double ActionGlyphSize { get; set; }

    public double ActionGlyphStroke { get; set; }

    /// <summary>The 2px focus ring sits 2px outside a button: radius 12 + 4.</summary>
    public CornerRadius ActionFocusRingRadius { get; set; }

    public Thickness FocusRingOutset { get; set; }

    /// <summary>Step dots: 32 high hit areas with 5 at the sides, a 6 pill (20 on the current step); the row starts 6 to the left.</summary>
    public double DotHitHeight { get; set; }

    public Thickness DotInset { get; set; }

    public double DotSize { get; set; }

    public double DotCurrentWidth { get; set; }

    public Thickness DotsOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 440;
        CardRadius = new CornerRadius(20);
        StageRadius = new CornerRadius(19, 19, 0, 0);
        StageInset = new Thickness(20, 40, 20, 16);
        CompactStageInset = new Thickness(12, 42, 12, 12);
        ArtMaxWidth = 352;
        ArtStrokeWidth = 1.36;
        ArtCheckStrokeWidth = 1.6;
        SkipOffset = new Thickness(0, 10, 10, 0);
        SkipHeight = 28;
        SkipInset = new Thickness(10, 0);
        BodyInset = new Thickness(24, 20);
        CompactBodyInset = new Thickness(16);
        BodyGap = 20;
        CompactBodyGap = 16;
        CopyMinHeight = 72;
        CompactCopyMinHeight = 88;
        CopyLineGap = 6;
        TitleLineHeight = 19.8;
        CompactTitleLineHeight = 17.6;
        TitleLetterSpacing = -0.18;
        CompactTitleLetterSpacing = -0.16;
        DescriptionLineHeight = 19.6;
        TextLetterSpacing = -0.16;
        ActionHeight = 36;
        ActionRadius = new CornerRadius(12);
        ActionGap = 8;
        ActionsOffset = new Thickness(12, 0, 0, 0);
        BackInset = new Thickness(14, 0);
        CompactBackWidth = 36;
        PrimaryInset = new Thickness(12, 0);
        PrimaryWidth = 136;
        CompactPrimaryWidth = 128;
        LabelGap = 6;
        ActionGlyphSize = 16;
        ActionGlyphStroke = 1.75;
        ActionFocusRingRadius = new CornerRadius(16);
        FocusRingOutset = new Thickness(-4);
        DotHitHeight = 32;
        DotInset = new Thickness(5, 0);
        DotSize = 6;
        DotCurrentWidth = 20;
        DotsOffset = new Thickness(-6, 0, 0, 0);
    }
}
