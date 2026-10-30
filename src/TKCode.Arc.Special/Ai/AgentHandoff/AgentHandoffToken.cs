using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="AgentHandoff"/>: the card's size and radius, the header, the thread with its timeline gutter and
/// markers, the bubbles, the handoff card, the typing dots and the composer, with the tighter values used below
/// <see cref="CompactWidth"/>. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class AgentHandoffToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's height (Arc's default 600); the thread scrolls inside.</summary>
    public double CardHeight { get; set; }

    /// <summary>The widest the card gets; it fills its container up to this.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Below this width the card uses its compact padding and wider bubbles.</summary>
    public double CompactWidth { get; set; }

    /// <summary>The header: its padding (regular and compact), the space between its parts and the mark or avatar.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness HeaderPaddingCompact { get; set; }

    public double HeaderGap { get; set; }

    public double HeaderMarkSize { get; set; }

    public double HeaderMarkGlyphSize { get; set; }

    /// <summary>The status pill in the header: height, padding, glyph and gap.</summary>
    public double StatusHeight { get; set; }

    public Thickness StatusPadding { get; set; }

    public double StatusGlyphSize { get; set; }

    public double StatusGap { get; set; }

    /// <summary>The thread's padding (regular and compact).</summary>
    public Thickness ThreadPadding { get; set; }

    public Thickness ThreadPaddingCompact { get; set; }

    /// <summary>The timeline gutter at the left of every row, and the space between it and the row's content.</summary>
    public double GutterWidth { get; set; }

    public Thickness ContentMargin { get; set; }

    /// <summary>The space under every row (the timeline runs through it).</summary>
    public Thickness RowPadding { get; set; }

    /// <summary>A step marker (a mark, the provider's mark or an avatar) and the glyph inside it.</summary>
    public double MarkerSize { get; set; }

    public double MarkerGlyphSize { get; set; }

    /// <summary>The timeline's hairline width.</summary>
    public double RailLineWidth { get; set; }

    /// <summary>Where a step's marker center sits from the top of its row (the line meets it there).</summary>
    public double StepMarkerCenter { get; set; }

    /// <summary>The handoff card's marker: its top margin and center.</summary>
    public Thickness CardMarkerMargin { get; set; }

    public double CardMarkerCenter { get; set; }

    /// <summary>Where the line below a marker starts: a step's marker center, the card's marker center.</summary>
    public Thickness StepLineBelowMargin { get; set; }

    public Thickness CardLineBelowMargin { get; set; }

    /// <summary>Where the pending stretch with the travel dot starts: just under the card's marker.</summary>
    public Thickness TravelMargin { get; set; }

    /// <summary>A step's text: 13px on a line as tall as the marker.</summary>
    public double StepFontSize { get; set; }

    public double StepLineHeight { get; set; }

    /// <summary>A bubble: padding, radius, how much of the row it may take (regular and compact), and its type.</summary>
    public Thickness BubblePadding { get; set; }

    public CornerRadius BubbleCornerRadius { get; set; }

    public double BubbleFraction { get; set; }

    public double BubbleFractionCompact { get; set; }

    public double MessageLineHeight { get; set; }

    /// <summary>The time under a bubble.</summary>
    public double TimeFontSize { get; set; }

    public Thickness TimeMargin { get; set; }

    /// <summary>The typing dots: each dot, the gap between them and the bubble around them.</summary>
    public double TypingDotSize { get; set; }

    public double TypingDotGap { get; set; }

    public Thickness TypingPadding { get; set; }

    /// <summary>The handoff card: padding, radius, the space between its rows and the action button.</summary>
    public Thickness HandoffPadding { get; set; }

    public CornerRadius HandoffCornerRadius { get; set; }

    public double HandoffGap { get; set; }

    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    /// <summary>The summary inside the card: the toggle's height, the label column and the space between fields.</summary>
    public double SummaryToggleHeight { get; set; }

    public double SummaryLabelWidth { get; set; }

    public double SummaryFieldGap { get; set; }

    public Thickness SummaryMargin { get; set; }

    /// <summary>The accent dot that travels down the timeline while transferring.</summary>
    public double TravelDotSize { get; set; }

    /// <summary>The composer: padding (regular and compact), gap and the round send button.</summary>
    public Thickness ComposerPadding { get; set; }

    public Thickness ComposerPaddingCompact { get; set; }

    public double ComposerGap { get; set; }

    public double SendSize { get; set; }

    /// <summary>The Talk to a person quick reply: its height and padding.</summary>
    public double SuggestionHeight { get; set; }

    public Thickness SuggestionPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        CardHeight = 600;
        CardMaxWidth = 560;
        CompactWidth = 400;
        HeaderPadding = new Thickness(16, 12);
        HeaderPaddingCompact = new Thickness(12, 10);
        HeaderGap = 10;
        HeaderMarkSize = 32;
        HeaderMarkGlyphSize = 16;
        StatusHeight = 24;
        StatusPadding = new Thickness(8, 0, 10, 0);
        StatusGlyphSize = 14;
        StatusGap = 6;
        ThreadPadding = new Thickness(16, 16, 16, 8);
        ThreadPaddingCompact = new Thickness(10, 12, 10, 6);
        GutterWidth = 24;
        ContentMargin = new Thickness(10, 0, 0, 0);
        RowPadding = new Thickness(0, 0, 0, 12);
        MarkerSize = 24;
        MarkerGlyphSize = 14;
        RailLineWidth = 1;
        StepMarkerCenter = MarkerSize / 2;
        CardMarkerMargin = new Thickness(0, 10, 0, 0);
        CardMarkerCenter = 10 + MarkerSize / 2;
        StepLineBelowMargin = new Thickness(0, StepMarkerCenter, 0, 0);
        CardLineBelowMargin = new Thickness(0, CardMarkerCenter, 0, 0);
        TravelMargin = new Thickness(0, CardMarkerCenter + MarkerSize / 2 + 2, 0, 0);
        StepFontSize = 13;
        StepLineHeight = MarkerSize;
        BubblePadding = new Thickness(12, 8);
        BubbleCornerRadius = new CornerRadius(16);
        BubbleFraction = 0.8;
        BubbleFractionCompact = 0.92;
        MessageLineHeight = 20;
        TimeFontSize = 11;
        TimeMargin = new Thickness(4, 4, 4, 0);
        TypingDotSize = 6;
        TypingDotGap = 4;
        TypingPadding = new Thickness(12, 11);
        HandoffPadding = new Thickness(14, 12);
        HandoffCornerRadius = new CornerRadius(14);
        HandoffGap = 8;
        ActionHeight = 28;
        ActionPadding = new Thickness(10, 0);
        SummaryToggleHeight = 32;
        SummaryLabelWidth = 84;
        SummaryFieldGap = 4;
        SummaryMargin = new Thickness(0, 2, 0, 2);
        TravelDotSize = 6;
        ComposerPadding = new Thickness(12);
        ComposerPaddingCompact = new Thickness(10);
        ComposerGap = 8;
        SendSize = 36;
        SuggestionHeight = 30;
        SuggestionPadding = new Thickness(12, 0);
    }
}
