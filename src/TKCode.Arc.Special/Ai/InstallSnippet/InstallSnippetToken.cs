using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="InstallSnippet"/>: the card (its radius, padding, widest width and the two breakpoints), the
/// header, the framework and package manager tabs, the numbered markers and their rail, the steps, the code blocks and
/// the key status line. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class InstallSnippetToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card's padding, and the tighter padding below <see cref="NarrowBreakpoint"/>.</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CardPaddingNarrow { get; set; }

    /// <summary>The card fills its container up to this width.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Below this width the padding tightens and the package manager tabs show marks only.</summary>
    public double NarrowBreakpoint { get; set; }

    /// <summary>Below this width the framework tabs drop their marks and the version label hides.</summary>
    public double CompactBreakpoint { get; set; }

    /// <summary>The title (16/24) and the line under it (13/20).</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double DescriptionFontSize { get; set; }

    public double DescriptionLineHeight { get; set; }

    /// <summary>The version label at the top right: 12px monospace in a hairline pill.</summary>
    public double VersionFontSize { get; set; }

    public Thickness VersionPadding { get; set; }

    public Thickness VersionMargin { get; set; }

    /// <summary>The space above the framework tabs and above the steps.</summary>
    public Thickness TabsMargin { get; set; }

    public Thickness StepsMargin { get; set; }

    /// <summary>A tab list's muted track: its padding and radius, and the raised highlight's radius.</summary>
    public Thickness TrackPadding { get; set; }

    public CornerRadius TrackCornerRadius { get; set; }

    public CornerRadius HighlightCornerRadius { get; set; }

    /// <summary>A framework tab: 30px high, 12px in from the sides, 13px medium text, a 14px mark 6px from its name.</summary>
    public double TabHeight { get; set; }

    public Thickness TabPadding { get; set; }

    public double TabFontSize { get; set; }

    public double TabMarkSize { get; set; }

    public double TabGap { get; set; }

    /// <summary>A package manager tab: 24px high, 8px in from the sides, 12px text, on a smaller track.</summary>
    public double SmallTabHeight { get; set; }

    public Thickness SmallTabPadding { get; set; }

    public double SmallTabFontSize { get; set; }

    public Thickness SmallTrackPadding { get; set; }

    public CornerRadius SmallTrackCornerRadius { get; set; }

    public CornerRadius SmallHighlightCornerRadius { get; set; }

    /// <summary>A package manager's monogram: a 14px tinted square with its first letter.</summary>
    public double MonogramSize { get; set; }

    public double MonogramFontSize { get; set; }

    public CornerRadius MonogramCornerRadius { get; set; }

    /// <summary>The step marker: a 24px circle with the number (12px) or a 14px check, 2px below the title row's top.</summary>
    public double MarkerSize { get; set; }

    public double MarkerFontSize { get; set; }

    public double MarkerGlyphSize { get; set; }

    public Thickness MarkerMargin { get; set; }

    /// <summary>The rail from under a marker to the next step.</summary>
    public double RailWidth { get; set; }

    public Thickness RailMargin { get; set; }

    /// <summary>The step's body: 14px from the marker, 20px between steps.</summary>
    public Thickness StepBodyMargin { get; set; }

    public Thickness StepGapMargin { get; set; }

    /// <summary>The step's title row (28px, so its controls never change its height) and title (14/20 medium).</summary>
    public double TitleRowHeight { get; set; }

    public double StepTitleFontSize { get; set; }

    public double StepTitleLineHeight { get; set; }

    public Thickness AccessoryMargin { get; set; }

    /// <summary>The step's line: 13/20, 2px under the title.</summary>
    public double StepDescriptionFontSize { get; set; }

    public double StepDescriptionLineHeight { get; set; }

    public Thickness StepDescriptionMargin { get; set; }

    /// <summary>The code block: 10px under the line, 12px radius, a 36px header with the file name (12px) and copy.</summary>
    public Thickness CodeMargin { get; set; }

    public CornerRadius CodeCornerRadius { get; set; }

    public double CodeHeaderHeight { get; set; }

    public Thickness CodeHeaderPadding { get; set; }

    public double FileFontSize { get; set; }

    /// <summary>The code: 13/20 monospace inside 14 × 12px of padding.</summary>
    public double CodeFontSize { get; set; }

    public double CodeLineHeight { get; set; }

    public Thickness CodePadding { get; set; }

    /// <summary>The key status line under step 2's code: 12/18 with a 14px glyph.</summary>
    public Thickness StatusMargin { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusLineHeight { get; set; }

    public double StatusGlyphSize { get; set; }

    /// <summary>Generate key: 28px high, 10px in from the sides, 12px medium text.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(20);
        CardPadding = new Thickness(24);
        CardPaddingNarrow = new Thickness(16);
        CardMaxWidth = 640;
        NarrowBreakpoint = 460;
        CompactBreakpoint = 380;
        TitleFontSize = 16;
        TitleLineHeight = 24;
        DescriptionFontSize = 13;
        DescriptionLineHeight = 20;
        VersionFontSize = 12;
        VersionPadding = new Thickness(8, 2);
        VersionMargin = new Thickness(12, 2, 0, 0);
        TabsMargin = new Thickness(0, 16, 0, 0);
        StepsMargin = new Thickness(0, 20, 0, 0);
        TrackPadding = new Thickness(3);
        TrackCornerRadius = new CornerRadius(10);
        HighlightCornerRadius = new CornerRadius(7);
        TabHeight = 30;
        TabPadding = new Thickness(12, 0);
        TabFontSize = 13;
        TabMarkSize = 14;
        TabGap = 6;
        SmallTabHeight = 24;
        SmallTabPadding = new Thickness(8, 0);
        SmallTabFontSize = 12;
        SmallTrackPadding = new Thickness(2);
        SmallTrackCornerRadius = new CornerRadius(8);
        SmallHighlightCornerRadius = new CornerRadius(6);
        MonogramSize = 14;
        MonogramFontSize = 9;
        MonogramCornerRadius = new CornerRadius(4);
        MarkerSize = 24;
        MarkerFontSize = 12;
        MarkerGlyphSize = 14;
        MarkerMargin = new Thickness(0, 2, 0, 0);
        RailWidth = 1;
        RailMargin = new Thickness(0, MarkerMargin.Top + MarkerSize + 6, 0, 4);
        StepBodyMargin = new Thickness(14, 0, 0, 0);
        StepGapMargin = new Thickness(0, 0, 0, 20);
        TitleRowHeight = 28;
        StepTitleFontSize = 14;
        StepTitleLineHeight = 20;
        AccessoryMargin = new Thickness(12, 0, 0, 0);
        StepDescriptionFontSize = 13;
        StepDescriptionLineHeight = 20;
        StepDescriptionMargin = new Thickness(0, 2, 0, 0);
        CodeMargin = new Thickness(0, 10, 0, 0);
        CodeCornerRadius = new CornerRadius(12);
        CodeHeaderHeight = 36;
        CodeHeaderPadding = new Thickness(12, 0, 4, 0);
        FileFontSize = 12;
        CodeFontSize = 13;
        CodeLineHeight = 20;
        CodePadding = new Thickness(14, 12);
        StatusMargin = new Thickness(0, 8, 0, 0);
        StatusFontSize = 12;
        StatusLineHeight = 18;
        StatusGlyphSize = 14;
        ActionHeight = 28;
        ActionPadding = new Thickness(10, 0);
    }
}
