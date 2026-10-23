using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AiSidePanel"/>, measured from the AI side panel of the AI group (design reference
/// only, ADR 0015; spec section 3). Colors, radii and fonts come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AiSidePanelToken : AbstractControlDesignToken
{
    /// <summary>Space inside the header (16 left, 12 elsewhere); with the 1px edge it is 87 high.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The row with the assistant's name and the close button (30).</summary>
    public double TitleRowHeight { get; set; }

    /// <summary>Left of the name and of the model: 7 from what precedes them.</summary>
    public Thickness IdentityGapMargin { get; set; }

    /// <summary>The assistant's mark in the header (15).</summary>
    public double HeaderMarkSize { get; set; }

    /// <summary>The close button (30 square).</summary>
    public double CloseButtonSize { get; set; }

    /// <summary>The close button's corners (10).</summary>
    public CornerRadius CloseButtonRadius { get; set; }

    /// <summary>The close button's glyph (16).</summary>
    public double CloseGlyphSize { get; set; }

    /// <summary>Above the context pills (8).</summary>
    public Thickness ChipsMargin { get; set; }

    /// <summary>The context pills (24 high).</summary>
    public double ChipHeight { get; set; }

    /// <summary>Between context pills and between suggestions (6).</summary>
    public double ChipGap { get; set; }

    /// <summary>Left of the selection pill: 6 from the "This page" pill.</summary>
    public Thickness ChipGapMargin { get; set; }

    /// <summary>Inside the "This page" pill (0 9).</summary>
    public Thickness PagePillPadding { get; set; }

    /// <summary>Inside the selection pill (9 left, 3 right of the remove button).</summary>
    public Thickness SelectionPillPadding { get; set; }

    /// <summary>The pills' icons (13).</summary>
    public double ChipGlyphSize { get; set; }

    /// <summary>Between a pill's icon and its text (5).</summary>
    public double ChipGlyphGap { get; set; }

    /// <summary>The selection pill's remove button (18 round).</summary>
    public double ChipRemoveSize { get; set; }

    /// <summary>Space inside the thread (16, 52 at the bottom for the undo pill).</summary>
    public Thickness ThreadPadding { get; set; }

    /// <summary>Between turns (18).</summary>
    public double TurnGap { get; set; }

    /// <summary>Between the parts of a turn (10).</summary>
    public double TurnBodyGap { get; set; }

    /// <summary>Between the context line and the bubble (4).</summary>
    public double AskGap { get; set; }

    /// <summary>Inside the person's bubble (12 7).</summary>
    public Thickness BubblePadding { get; set; }

    /// <summary>The bubble's corners (14).</summary>
    public CornerRadius BubbleRadius { get; set; }

    /// <summary>The space a question always leaves on its left (40).</summary>
    public Thickness BubbleInsetMargin { get; set; }

    /// <summary>The answer's line height (14 × 1.55 = 21.7); a blank line between paragraphs is one line.</summary>
    public double AnswerLineHeight { get; set; }

    /// <summary>The suggestion pills (30 high).</summary>
    public double SuggestionHeight { get; set; }

    /// <summary>Inside a suggestion pill (12 on each side).</summary>
    public Thickness SuggestionPadding { get; set; }

    /// <summary>Between the empty thread's title, its line and the suggestions (6).</summary>
    public double EmptyGap { get; set; }

    /// <summary>Around the box (12, nothing above).</summary>
    public Thickness ComposerAreaPadding { get; set; }

    /// <summary>The box (44 high).</summary>
    public double BoxHeight { get; set; }

    /// <summary>Inside the box (14 left, 6 right).</summary>
    public Thickness BoxPadding { get; set; }

    /// <summary>The send button (32 round).</summary>
    public double SendDiameter { get; set; }

    /// <summary>Under the undo pill: 8 above the box.</summary>
    public Thickness UndoPillMargin { get; set; }

    /// <summary>The change and error cards' corners (14).</summary>
    public CornerRadius CardRadius { get; set; }

    /// <summary>Around a change card's head (12, 10 on top, nothing below).</summary>
    public Thickness CardHeadPadding { get; set; }

    /// <summary>Around a change card's diff (12, 8 on top).</summary>
    public Thickness CardBodyPadding { get; set; }

    /// <summary>A change card's footer with its 1px top edge (43 + 1).</summary>
    public double CardFooterHeightWithEdge { get; set; }

    /// <summary>Inside a change card's footer (12 left, 7 right).</summary>
    public Thickness CardFooterPadding { get; set; }

    /// <summary>The cards' small buttons (30 high).</summary>
    public double CardButtonHeight { get; set; }

    /// <summary>Inside the cards' small buttons (12 on each side).</summary>
    public Thickness CardButtonPadding { get; set; }

    /// <summary>Inside the error card (12 left, 10 elsewhere).</summary>
    public Thickness ErrorCardPadding { get; set; }

    /// <summary>The shortest "Reading the page" (Arc's scripted reading: about 0.6s).</summary>
    public TimeSpan ReadingMinimum { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        HeaderPadding = new Thickness(16, 12, 12, 12);
        TitleRowHeight = 30;
        IdentityGapMargin = new Thickness(7, 0, 0, 0);
        HeaderMarkSize = 15;
        CloseButtonSize = 30;
        CloseButtonRadius = new CornerRadius(10);
        CloseGlyphSize = 16;
        ChipsMargin = new Thickness(0, 8, 0, 0);
        ChipHeight = 24;
        ChipGap = 6;
        ChipGapMargin = new Thickness(6, 0, 0, 0);
        PagePillPadding = new Thickness(9, 0);
        SelectionPillPadding = new Thickness(9, 0, 3, 0);
        ChipGlyphSize = 13;
        ChipGlyphGap = 5;
        ChipRemoveSize = 18;
        ThreadPadding = new Thickness(16, 16, 16, 52);
        TurnGap = 18;
        TurnBodyGap = 10;
        AskGap = 4;
        BubblePadding = new Thickness(12, 7);
        BubbleRadius = new CornerRadius(14);
        BubbleInsetMargin = new Thickness(40, 0, 0, 0);
        AnswerLineHeight = 21.7;
        SuggestionHeight = 30;
        SuggestionPadding = new Thickness(12, 0);
        EmptyGap = 6;
        ComposerAreaPadding = new Thickness(12, 0, 12, 12);
        BoxHeight = 44;
        BoxPadding = new Thickness(14, 0, 6, 0);
        SendDiameter = 32;
        UndoPillMargin = new Thickness(0, 0, 0, 8);
        CardRadius = new CornerRadius(14);
        CardHeadPadding = new Thickness(12, 10, 12, 0);
        CardBodyPadding = new Thickness(12, 8, 12, 12);
        CardFooterHeightWithEdge = 44;
        CardFooterPadding = new Thickness(12, 0, 7, 0);
        CardButtonHeight = 30;
        CardButtonPadding = new Thickness(12, 0);
        ErrorCardPadding = new Thickness(12, 10, 10, 10);
        ReadingMinimum = TimeSpan.FromMilliseconds(600);
    }
}
