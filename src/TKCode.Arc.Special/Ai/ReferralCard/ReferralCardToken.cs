using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ReferralCard"/>: the card's width limit, narrow breakpoint and padding, the header, the link and
/// email fields with their chips, the funnel numbers, the milestone segments and the fixed-height list of people. Colors,
/// fonts and radii of the surfaces come from the ArcDesign tokens.
/// </summary>
[ControlDesignToken]
internal sealed class ReferralCardToken : AbstractControlDesignToken
{
    /// <summary>The card is fluid up to this width.</summary>
    public double CardMaxWidth { get; set; }

    public Thickness CardPadding { get; set; }

    public Thickness NarrowCardPadding { get; set; }

    /// <summary>The space between the card's sections.</summary>
    public double SectionGap { get; set; }

    /// <summary>The gift mark beside the heading and its glyph.</summary>
    public double MarkSize { get; set; }

    public double MarkGlyphSize { get; set; }

    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The small labels over the link and the email field, and the list heading.</summary>
    public double LabelFontSize { get; set; }

    public double LabelGap { get; set; }

    /// <summary>The link field and the email field share one height.</summary>
    public double FieldHeight { get; set; }

    public Thickness LinkPadding { get; set; }

    public Thickness EmailPadding { get; set; }

    /// <summary>The space between a field and the buttons beside it.</summary>
    public double InlineGap { get; set; }

    /// <summary>A share button is a square of this size.</summary>
    public double ShareSize { get; set; }

    public double ShareGlyphSize { get; set; }

    /// <summary>A chip: its height, padding, gap and the editor's minimum width beside the chips.</summary>
    public double ChipHeight { get; set; }

    public Thickness ChipPadding { get; set; }

    public double ChipGap { get; set; }

    public double ChipFontSize { get; set; }

    public double EditorMinWidth { get; set; }

    /// <summary>The hint under the email field (one line, reserved).</summary>
    public double HintLineHeight { get; set; }

    public double HintGlyphSize { get; set; }

    /// <summary>The funnel numbers and their labels.</summary>
    public double NumberFontSize { get; set; }

    public double NumberHeight { get; set; }

    /// <summary>The milestone segments: height, the gap between them and the line under them.</summary>
    public double SegmentHeight { get; set; }

    public double SegmentGap { get; set; }

    public CornerRadius SegmentCornerRadius { get; set; }

    public double MilestoneLineHeight { get; set; }

    /// <summary>The height the milestone line reserves on a narrow card (two lines).</summary>
    public double MilestoneTwoLineHeight { get; set; }

    /// <summary>The list of people: a fixed height (the card never grows), one row's height and the stage column.</summary>
    public double ListHeight { get; set; }

    public double RowHeight { get; set; }

    public double ListRowGap { get; set; }

    public double StageWidth { get; set; }

    public double StageGlyphSize { get; set; }

    public double RowFontSize { get; set; }

    public double RowDetailFontSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 560;
        CardPadding = new Thickness(24);
        NarrowCardPadding = new Thickness(18);
        SectionGap = 20;
        MarkSize = 40;
        MarkGlyphSize = 20;
        TitleFontSize = 18;
        TitleLineHeight = 24;
        DescriptionLineHeight = 20;
        LabelFontSize = 12;
        LabelGap = 6;
        FieldHeight = 44;
        LinkPadding = new Thickness(12, 0, 4, 0);
        EmailPadding = new Thickness(4, 0, 8, 0);
        InlineGap = 8;
        ShareSize = 44;
        ShareGlyphSize = 16;
        ChipHeight = 28;
        ChipPadding = new Thickness(10, 0, 4, 0);
        ChipGap = 4;
        ChipFontSize = 13;
        EditorMinWidth = 140;
        HintLineHeight = 18;
        HintGlyphSize = 14;
        NumberFontSize = 22;
        NumberHeight = 30;
        SegmentHeight = 8;
        SegmentGap = 4;
        SegmentCornerRadius = new CornerRadius(4);
        MilestoneLineHeight = 20;
        MilestoneTwoLineHeight = 2 * MilestoneLineHeight;
        RowHeight = 52;
        ListRowGap = 2;
        ListHeight = 4 * RowHeight + 3 * ListRowGap;
        StageWidth = 112;
        StageGlyphSize = 14;
        RowFontSize = 14;
        RowDetailFontSize = 12;
    }
}
