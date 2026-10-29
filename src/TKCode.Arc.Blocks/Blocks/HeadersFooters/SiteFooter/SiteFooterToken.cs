using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

// Geometry from Arc's free site-footer block (site-footer.module.css; MIT, Copyright (c) 2026 Elia Kuratli).
/// <summary>Geometry and brand-gradient colors of the <see cref="SiteFooter"/> block.</summary>
[ControlDesignToken]
internal sealed class SiteFooterToken : AbstractControlDesignToken
{
    /// <summary>Columns and Logo: 48 above, 24 at the sides and below.</summary>
    public Thickness FooterPadding { get; set; }

    /// <summary>Logo: no padding below, the arch is cropped by the edge.</summary>
    public Thickness FooterPaddingLogo { get; set; }

    /// <summary>Minimal: 32 above, 24 at the sides and below.</summary>
    public Thickness FooterPaddingMinimal { get; set; }

    /// <summary>The gap between the intro and the link columns (48; 40 below 760 wide).</summary>
    public double TopGap { get; set; }

    public double TopGapStacked { get; set; }

    /// <summary>The gap between the link columns (24), and 32 / 16 in two columns (row / column).</summary>
    public double ColumnGap { get; set; }

    public double ColumnRowGapNarrow { get; set; }

    public double ColumnColumnGapNarrow { get; set; }

    /// <summary>The gap under a column title (12) and between its links (8).</summary>
    public double ColumnTitleGap { get; set; }

    public double ColumnLinkGap { get; set; }

    /// <summary>The intro stacks its lines 12 apart; the brand mark is 22 and sits 8 from the name.</summary>
    public double IntroGap { get; set; }

    public double BrandGap { get; set; }

    public double BrandMarkSize { get; set; }

    /// <summary>The tagline wraps at 320.</summary>
    public double TaglineMaxWidth { get; set; }

    /// <summary>A link is 2 high above and below its text; the external arrow is 13, 3 from the text.</summary>
    public Thickness LinkPadding { get; set; }

    public double ExternalArrowSize { get; set; }

    public double ExternalArrowGap { get; set; }

    /// <summary>The line height of 14px body text (leading 1.4) and of the 16px brand name.</summary>
    public double LineHeightSM { get; set; }

    public double LineHeightBase { get; set; }

    /// <summary>Body text tracks -0.16px at every size (measured on uiarc.dev); headings (display face, 14 / 1.1) -0.42px.</summary>
    public double BodyLetterSpacing { get; set; }

    public double HeadingLetterSpacing { get; set; }

    public double HeadingLineHeight { get; set; }

    /// <summary>The legal row: 48 above, a hairline, 20 below it; 12 / 24 between wrapped lines and groups.</summary>
    public double BottomTopMargin { get; set; }

    public double BottomPaddingTop { get; set; }

    public double BottomRowGap { get; set; }

    public double BottomColumnGap { get; set; }

    /// <summary>The groups of the legal row: 8 between lines, 20 between items; inline link lists: 4 / 16.</summary>
    public double GroupLineGap { get; set; }

    public double GroupItemGap { get; set; }

    public double InlineLineGap { get; set; }

    public double InlineItemGap { get; set; }

    /// <summary>Minimal: 24 between the two rows, which are 12 / 24 apart when they wrap.</summary>
    public double MinimalRowMargin { get; set; }

    /// <summary>The status dot (7) with a 3px halo at 18%, 8 from the text.</summary>
    public double StatusDotSize { get; set; }

    public double StatusHaloWidth { get; set; }

    public double StatusHaloOpacity { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The newsletter: at most 400 wide, 20 below the tagline, its parts 8 apart.</summary>
    public double NewsletterMaxWidth { get; set; }

    public double NewsletterTopMargin { get; set; }

    public double NewsletterGap { get; set; }

    /// <summary>The input row: 1px border, padding 3 3 3 16, 8 between input and button.</summary>
    public Thickness NewsletterRowPadding { get; set; }

    public double NewsletterRowGap { get; set; }

    /// <summary>The button's corners: the row's radius minus the 3px padding.</summary>
    public CornerRadius NewsletterButtonRadius { get; set; }

    /// <summary>The message slot is at least 20 high; its text is 12 / 20.</summary>
    public double MessageHeight { get; set; }

    /// <summary>The Logo arch: 40 below the legal row, reaching 24 past the padding on both sides (to the footer's edges); strokes 3.4 of 64 in the brand gradient.</summary>
    public Thickness MarkMargin { get; set; }

    public double MarkStroke { get; set; }

    public Color MarkFrom { get; set; }

    public Color MarkTo { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FooterPadding = new Thickness(24, 48, 24, 24);
        FooterPaddingLogo = new Thickness(24, 48, 24, 0);
        FooterPaddingMinimal = new Thickness(24, 32, 24, 24);
        TopGap = 48;
        TopGapStacked = 40;
        ColumnGap = 24;
        ColumnRowGapNarrow = 32;
        ColumnColumnGapNarrow = 16;
        ColumnTitleGap = 12;
        ColumnLinkGap = 8;
        IntroGap = 12;
        BrandGap = 8;
        BrandMarkSize = 22;
        TaglineMaxWidth = 320;
        LinkPadding = new Thickness(0, 2);
        ExternalArrowSize = 13;
        ExternalArrowGap = 3;
        LineHeightSM = 19.6;
        LineHeightBase = 22.4;
        BodyLetterSpacing = -0.16;
        HeadingLetterSpacing = -0.42;
        HeadingLineHeight = 15.4;
        BottomTopMargin = 48;
        BottomPaddingTop = 20;
        BottomRowGap = 12;
        BottomColumnGap = 24;
        GroupLineGap = 8;
        GroupItemGap = 20;
        InlineLineGap = 4;
        InlineItemGap = 16;
        MinimalRowMargin = 24;
        StatusDotSize = 7;
        StatusHaloWidth = 3;
        StatusHaloOpacity = 0.18;
        StatusGap = 8;
        NewsletterMaxWidth = 400;
        NewsletterTopMargin = 20;
        NewsletterGap = 8;
        NewsletterRowPadding = new Thickness(16, 3, 3, 3);
        NewsletterRowGap = 8;
        NewsletterButtonRadius = new CornerRadius(15);
        MessageHeight = 20;
        MarkMargin = new Thickness(-24, 40, -24, 0);
        MarkStroke = 3.4;
        var palette = SiteBrandPalette.For(isDarkMode);
        MarkFrom = palette.From;
        MarkTo = palette.To;
    }
}
