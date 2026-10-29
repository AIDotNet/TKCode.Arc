using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AiAnswer"/>, measured from the AI answer of the AI group (design reference only, ADR
/// 0015). Colors, radii and fonts come from ArcDesign tokens in the theme. The column's paddings and the question's size
/// follow the block's own width (Arc's container query units), not the window's.
/// </summary>
[ControlDesignToken]
internal sealed class AiAnswerToken : AbstractControlDesignToken
{
    /// <summary>Height of the top bar, its 1px bottom edge included (52).</summary>
    public double TopBarHeight { get; set; }

    /// <summary>Space inside the top bar (0 10).</summary>
    public Thickness TopBarPadding { get; set; }

    /// <summary>Space between the new thread button, the title and the share button (8).</summary>
    public double TopBarGap { get; set; }

    /// <summary>Height of the share pill (32).</summary>
    public double ShareHeight { get; set; }

    /// <summary>The share pill's padding (0 12 0 10).</summary>
    public Thickness SharePadding { get; set; }

    /// <summary>Space between the share pill's glyph and its label (6).</summary>
    public double ShareGap { get; set; }

    /// <summary>The share pill's glyph (14).</summary>
    public double ShareGlyphSize { get; set; }

    /// <summary>The column is at most this wide, its side paddings included (768).</summary>
    public double ColumnMaxWidth { get; set; }

    /// <summary>The column's side padding is this share of the block's width (5%) ...</summary>
    public double SideRatio { get; set; }

    /// <summary>... but at least this (16) ...</summary>
    public double SideMin { get; set; }

    /// <summary>... and at most this (32).</summary>
    public double SideMax { get; set; }

    /// <summary>Space above the question is this share of the block's width (6%) ...</summary>
    public double TopRatio { get; set; }

    /// <summary>... but at least this (28).</summary>
    public double TopMin { get; set; }

    /// <summary>Space below the column's content (48).</summary>
    public double ColumnBottomPadding { get; set; }

    /// <summary>The empty page adds this share of the block's width above its heading (8%).</summary>
    public double EmptyTopRatio { get; set; }

    /// <summary>The question's size is this (1.1rem) ...</summary>
    public double QuestionFontBase { get; set; }

    /// <summary>... plus this share of the block's width (1.6%) ...</summary>
    public double QuestionFontRatio { get; set; }

    /// <summary>... but at least this (22) ...</summary>
    public double QuestionFontMin { get; set; }

    /// <summary>... and at most this (28).</summary>
    public double QuestionFontMax { get; set; }

    /// <summary>The question's line height as a multiple of its size (1.1).</summary>
    public double QuestionLeading { get; set; }

    /// <summary>The question's tracking in em (−0.02).</summary>
    public double QuestionTracking { get; set; }

    /// <summary>Space between the question and the reasoning row (14 above).</summary>
    public Thickness StepsMargin { get; set; }

    /// <summary>Space under the reasoning button that is always there (6 above the summary; the sources row brings its own 8).</summary>
    public Thickness ReasoningGap { get; set; }

    /// <summary>Space under the reasoning summary while it is open (12).</summary>
    public Thickness ReasoningPanelMargin { get; set; }

    /// <summary>Space between the rows above the answer and the answer (20 above).</summary>
    public Thickness AnswerMargin { get; set; }

    /// <summary>Space between the answer and its stopped or failed note (16).</summary>
    public double NoteMargin { get; set; }

    /// <summary>Space between the answer and the footer (20 above).</summary>
    public Thickness FooterMargin { get; set; }

    /// <summary>The footer keeps this height while the answer streams (36).</summary>
    public double FooterHeight { get; set; }

    /// <summary>Space between the footer and the related questions (36 above).</summary>
    public Thickness RelatedMargin { get; set; }

    /// <summary>The empty page's line is at most this wide (544).</summary>
    public double EmptyDescriptionMaxWidth { get; set; }

    /// <summary>Space between the empty page's heading and its line (8 above).</summary>
    public Thickness EmptyDescriptionMargin { get; set; }

    /// <summary>Space between the empty page's line and the suggestions (32 above).</summary>
    public Thickness EmptyListMargin { get; set; }

    /// <summary>Space above the follow-up box (6).</summary>
    public double DockTopPadding { get; set; }

    /// <summary>Space below the follow-up box (14).</summary>
    public double DockBottomPadding { get; set; }

    /// <summary>The follow-up box's padding around its row (14 7 7 7: with the text's own 4, 18 on the left).</summary>
    public Thickness ComposerPadding { get; set; }

    /// <summary>The follow-up box's corners (26).</summary>
    public CornerRadius ComposerCornerRadius { get; set; }

    /// <summary>How long the share pill says "Copied" or "Failed" (1.6s).</summary>
    public TimeSpan CopiedDuration { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TopBarHeight = 52;
        TopBarPadding = new Thickness(10, 0);
        TopBarGap = 8;
        ShareHeight = 32;
        SharePadding = new Thickness(10, 0, 12, 0);
        ShareGap = 6;
        ShareGlyphSize = 14;
        ColumnMaxWidth = 768;
        SideRatio = 0.05;
        SideMin = 16;
        SideMax = 32;
        TopRatio = 0.06;
        TopMin = 28;
        ColumnBottomPadding = 48;
        EmptyTopRatio = 0.08;
        QuestionFontBase = 17.6;
        QuestionFontRatio = 0.016;
        QuestionFontMin = 22;
        QuestionFontMax = 28;
        QuestionLeading = 1.1;
        QuestionTracking = -0.02;
        StepsMargin = new Thickness(0, 14, 0, 0);
        ReasoningGap = new Thickness(0, 6, 0, 0);
        ReasoningPanelMargin = new Thickness(0, 0, 0, 12);
        AnswerMargin = new Thickness(0, 20, 0, 0);
        NoteMargin = 16;
        FooterMargin = new Thickness(0, 20, 0, 0);
        FooterHeight = 36;
        RelatedMargin = new Thickness(0, 36, 0, 0);
        EmptyDescriptionMaxWidth = 544;
        EmptyDescriptionMargin = new Thickness(0, 8, 0, 0);
        EmptyListMargin = new Thickness(0, 32, 0, 0);
        DockTopPadding = 6;
        DockBottomPadding = 14;
        ComposerPadding = new Thickness(14, 7, 7, 7);
        ComposerCornerRadius = new CornerRadius(26);
        CopiedDuration = TimeSpan.FromSeconds(1.6);
    }
}
