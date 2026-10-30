using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SourceCards"/>: the status line, the gaps between the status line, the answer and the grid, the
/// cards (their height, padding, mark, number chip and 18px text lines, on which the skeleton bars sit too), the stack's
/// overlapping marks, the highlight outline and the hover preview. Colors, fonts and the focus ring come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SourceCardsToken : AbstractControlDesignToken
{
    /// <summary>The status line: its height, the gap between its parts, its type and the 14px icon.</summary>
    public double StatusHeight { get; set; }

    public double StatusGap { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusIconSize { get; set; }

    /// <summary>The space under the status line and under the answer.</summary>
    public Thickness StatusMargin { get; set; }

    public Thickness AnswerMargin { get; set; }

    /// <summary>The space between cells, both ways.</summary>
    public double GridGap { get; set; }

    /// <summary>A card: its fixed height (so slots and cards match), padding and radius.</summary>
    public double CardHeight { get; set; }

    public Thickness CardPadding { get; set; }

    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The 18px text line every row of a card sits on (domain, the two title lines, skeleton bars).</summary>
    public double LineHeight { get; set; }

    /// <summary>The domain line: 12px type, the 14px mark and the gap after it.</summary>
    public double DomainFontSize { get; set; }

    public double MarkSize { get; set; }

    public double MarkGap { get; set; }

    /// <summary>The title: 13px type on two 18px lines, 4px under the domain line.</summary>
    public double TitleFontSize { get; set; }

    public double TitleHeight { get; set; }

    public Thickness TitleMargin { get; set; }

    /// <summary>The number chip on a card and in the text: 16px high, at least 16px wide, 11px semibold digits.</summary>
    public double NumberHeight { get; set; }

    public double NumberMinWidth { get; set; }

    public double NumberFontSize { get; set; }

    public Thickness NumberPadding { get; set; }

    public CornerRadius NumberCornerRadius { get; set; }

    /// <summary>The stack's marks: 18px discs overlapping by 6px, with a 1.5px ring of the card's surface.</summary>
    public double StackMarkSize { get; set; }

    public double StackMarkOverlap { get; set; }

    public double StackMarkRing { get; set; }

    /// <summary>The skeleton bars: 8px tall on the domain line, 10px on the title lines.</summary>
    public double SkeletonDomainBar { get; set; }

    public double SkeletonTitleBar { get; set; }

    /// <summary>The highlight outline (2px, 3px outside the card) and the keyboard focus ring at the same place.</summary>
    public Thickness OutlineMargin { get; set; }

    public Thickness OutlineThickness { get; set; }

    public CornerRadius OutlineCornerRadius { get; set; }

    /// <summary>The preview: its width, padding, radius, the 16px mark and the snippet's four 18px lines.</summary>
    public double PreviewWidth { get; set; }

    public Thickness PreviewPadding { get; set; }

    public CornerRadius PreviewCornerRadius { get; set; }

    public double PreviewMarkSize { get; set; }

    public double PreviewGap { get; set; }

    public double PreviewSnippetFontSize { get; set; }

    public int PreviewSnippetLines { get; set; }

    /// <summary>The gap between a card and its preview.</summary>
    public double PreviewOffset { get; set; }

    /// <summary>The status line's buttons (Retry, Show all): 24px high, 8px in from the sides.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        StatusHeight = 24;
        StatusGap = 6;
        StatusFontSize = 13;
        StatusIconSize = 14;
        StatusMargin = new Thickness(0, 0, 0, 10);
        AnswerMargin = new Thickness(0, 0, 0, 12);
        GridGap = 8;
        LineHeight = 18;
        CardPadding = new Thickness(10);
        CardCornerRadius = new CornerRadius(12);
        DomainFontSize = 12;
        MarkSize = 14;
        MarkGap = 6;
        TitleFontSize = 13;
        TitleHeight = 2 * LineHeight;
        TitleMargin = new Thickness(0, 4, 0, 0);
        CardHeight = CardPadding.Top + LineHeight + TitleMargin.Top + TitleHeight + CardPadding.Bottom;
        NumberHeight = 16;
        NumberMinWidth = 16;
        NumberFontSize = 11;
        NumberPadding = new Thickness(4, 0);
        NumberCornerRadius = new CornerRadius(5);
        StackMarkSize = 18;
        StackMarkOverlap = 6;
        StackMarkRing = 1.5;
        SkeletonDomainBar = 8;
        SkeletonTitleBar = 10;
        OutlineMargin = new Thickness(-3);
        OutlineThickness = new Thickness(2);
        OutlineCornerRadius = new CornerRadius(15);
        PreviewWidth = 300;
        PreviewPadding = new Thickness(12);
        PreviewCornerRadius = new CornerRadius(14);
        PreviewMarkSize = 16;
        PreviewGap = 6;
        PreviewSnippetFontSize = 12.5;
        PreviewSnippetLines = 4;
        PreviewOffset = 8;
        ActionHeight = 24;
        ActionPadding = new Thickness(8, 0);
    }
}
