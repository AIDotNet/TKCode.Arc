// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="PlanComparison"/> (values from Arc's plan-comparison.module.css).</summary>
[ControlDesignToken]
internal sealed class PlanComparisonToken : AbstractControlDesignToken
{
    /// <summary>Largest width of the framed block (<c>width: min(100%, 1060px)</c>).</summary>
    public double FrameMaxWidth { get; set; }

    /// <summary>Content width at or below which the block uses the medium layout (720).</summary>
    public double MediumWidth { get; set; }

    /// <summary>Content width at or below which the block uses the narrow layout (540).</summary>
    public double SmallWidth { get; set; }

    /// <summary>Content width at or below which the block uses the tightest padding (340).</summary>
    public double TinyWidth { get; set; }

    /// <summary>Space around the heading and the billing switch.</summary>
    public Thickness IntroInset { get; set; }

    /// <summary>Space around the heading in the medium and narrow layouts.</summary>
    public Thickness IntroInsetCompact { get; set; }

    /// <summary>Space around the heading in the tightest layout.</summary>
    public Thickness IntroInsetTiny { get; set; }

    /// <summary>Space between the heading and the billing switch beside it.</summary>
    public Thickness BillingMargin { get; set; }

    /// <summary>Space above the billing switch once it stacks under the heading.</summary>
    public Thickness BillingMarginCompact { get; set; }

    /// <summary>Gap between the billing switch and its note.</summary>
    public double BillingGap { get; set; }

    /// <summary>Largest width of the heading column.</summary>
    public double HeadingMaxWidth { get; set; }

    /// <summary>Largest width of the sentence under the heading (46ch).</summary>
    public double DescriptionMaxWidth { get; set; }

    /// <summary>Space above the sentence under the heading.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Line height of the heading (1.1 × 36).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Line height of the heading in the narrow layout (1.1 × 28).</summary>
    public double TitleLineHeightNarrow { get; set; }

    /// <summary>Letter spacing of the heading and prices (−0.03em of 36).</summary>
    public double DisplayLetterSpacing { get; set; }

    /// <summary>Letter spacing of the heading and prices in the narrow layout (−0.03em of 28).</summary>
    public double DisplayLetterSpacingNarrow { get; set; }

    /// <summary>Letter spacing of body text (−0.01em of 14).</summary>
    public double BodyLetterSpacing { get; set; }

    /// <summary>Letter spacing of plan names (−0.01em of 16).</summary>
    public double PlanNameLetterSpacing { get; set; }

    /// <summary>Line height of body text (1.4 × 14).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Line height of plan names (headings use 1.1: 1.1 × 16).</summary>
    public double HeadingLineHeight { get; set; }

    /// <summary>Line height of "Compare plans" (1.1 × 14).</summary>
    public double CompareTitleLineHeight { get; set; }

    /// <summary>Line height of small text (1.4 × 12).</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>Smallest height of the filter bar.</summary>
    public double FilterMinHeight { get; set; }

    /// <summary>Padding of the filter bar.</summary>
    public Thickness FilterInset { get; set; }

    /// <summary>Padding of the filter bar in the medium and narrow layouts.</summary>
    public Thickness FilterInsetCompact { get; set; }

    /// <summary>Padding of the filter bar in the tightest layout.</summary>
    public Thickness FilterInsetTiny { get; set; }

    /// <summary>Gap between "Compare plans" and the feature count.</summary>
    public double FilterTitleGap { get; set; }

    /// <summary>Gap between the filter title and the switch on one line.</summary>
    public double FilterColumnGap { get; set; }

    /// <summary>Gap between the filter title and the switch once it wraps.</summary>
    public double FilterRowGap { get; set; }

    /// <summary>Smallest width of the feature count (11ch), so the switch does not move while it counts.</summary>
    public double FeatureCountMinWidth { get; set; }

    /// <summary>Space under the feature column's header.</summary>
    public double FeatureHeaderBottom { get; set; }

    /// <summary>Side gutter of the block: heading, filter, feature names and footer (32, 20 compact, 16 tiny).</summary>
    public double SideInset { get; set; }

    /// <summary>Side gutter in the medium and narrow layouts.</summary>
    public double SideInsetCompact { get; set; }

    /// <summary>Side gutter in the tightest layout.</summary>
    public double SideInsetTiny { get; set; }

    /// <summary>Inline padding of plan and value cells (20, 14 compact, 12 tiny).</summary>
    public double CellInset { get; set; }

    /// <summary>Inline padding of plan and value cells in the medium and narrow layouts.</summary>
    public double CellInsetCompact { get; set; }

    /// <summary>Inline padding of plan and value cells in the tightest layout.</summary>
    public double CellInsetTiny { get; set; }

    /// <summary>Padding above and below a plan's header cell (20, 16 compact).</summary>
    public double PlanInsetY { get; set; }

    /// <summary>Padding above and below a plan's header cell in the medium and narrow layouts.</summary>
    public double PlanInsetYCompact { get; set; }

    /// <summary>Space between a plan's name block and its price, and between the price and the button.</summary>
    public Thickness PriceMargin { get; set; }

    /// <summary>Space above a plan's description.</summary>
    public Thickness PlanDescriptionMargin { get; set; }

    /// <summary>Smallest width of a price (3ch of the display face).</summary>
    public double PriceMinWidth { get; set; }

    /// <summary>Smallest width of a price in the narrow layout.</summary>
    public double PriceMinWidthNarrow { get; set; }

    /// <summary>Gap between a price and "per seat / month".</summary>
    public double PriceGap { get; set; }

    /// <summary>Gap between a wrapped price and "per seat / month" under it.</summary>
    public double PriceRowGap { get; set; }

    /// <summary>Smallest height of a feature row in the wide layouts.</summary>
    public double RowMinHeight { get; set; }

    /// <summary>Vertical padding of feature names and values in the wide layouts.</summary>
    public double RowInsetY { get; set; }

    /// <summary>Height of the rail under the selected plan.</summary>
    public double RailHeight { get; set; }

    /// <summary>Size of the check before "Included".</summary>
    public double CheckSize { get; set; }

    /// <summary>Gap between the check and "Included".</summary>
    public double CheckGap { get; set; }

    /// <summary>Smallest height of the footer.</summary>
    public double FooterMinHeight { get; set; }

    /// <summary>Padding of the footer.</summary>
    public Thickness FooterInset { get; set; }

    /// <summary>Padding of the footer in the medium and narrow layouts.</summary>
    public Thickness FooterInsetCompact { get; set; }

    /// <summary>Padding of the footer in the tightest layout.</summary>
    public Thickness FooterInsetTiny { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameMaxWidth = 1060;
        MediumWidth = 720;
        SmallWidth = 540;
        TinyWidth = 340;
        IntroInset = new Thickness(32, 32, 32, 24);
        IntroInsetCompact = new Thickness(20, 24, 20, 20);
        IntroInsetTiny = new Thickness(16, 24, 16, 20);
        BillingMargin = new Thickness(32, 0, 0, 0);
        BillingMarginCompact = new Thickness(0, 16, 0, 0);
        BillingGap = 6;
        HeadingMaxWidth = 560;
        DescriptionMaxWidth = 400;
        DescriptionMargin = new Thickness(0, 8, 0, 0);
        TitleLineHeight = 39.6;
        TitleLineHeightNarrow = 30.8;
        DisplayLetterSpacing = -1.08;
        DisplayLetterSpacingNarrow = -0.84;
        BodyLetterSpacing = -0.14;
        PlanNameLetterSpacing = -0.16;
        BodyLineHeight = 19.6;
        SmallLineHeight = 16.8;
        HeadingLineHeight = 17.6;
        CompareTitleLineHeight = 15.4;
        FilterMinHeight = 52;
        FilterInset = new Thickness(32, 8);
        FilterInsetCompact = new Thickness(20, 8);
        FilterInsetTiny = new Thickness(16, 8);
        FilterTitleGap = 10;
        FilterColumnGap = 16;
        FilterRowGap = 8;
        FeatureCountMinWidth = 82;
        FeatureHeaderBottom = 20;
        SideInset = 32;
        SideInsetCompact = 20;
        SideInsetTiny = 16;
        CellInset = 20;
        CellInsetCompact = 14;
        CellInsetTiny = 12;
        PlanInsetY = 20;
        PlanInsetYCompact = 16;
        PriceMargin = new Thickness(0, 16, 0, 14);
        PlanDescriptionMargin = new Thickness(0, 2, 0, 0);
        PriceMinWidth = 71;
        PriceMinWidthNarrow = 55;
        PriceGap = 6;
        PriceRowGap = 2;
        RowMinHeight = 44;
        RowInsetY = 10;
        RailHeight = 2;
        CheckSize = 16;
        CheckGap = 6;
        FooterMinHeight = 48;
        FooterInset = new Thickness(32, 12);
        FooterInsetCompact = new Thickness(20, 12);
        FooterInsetTiny = new Thickness(16, 12);
    }
}
