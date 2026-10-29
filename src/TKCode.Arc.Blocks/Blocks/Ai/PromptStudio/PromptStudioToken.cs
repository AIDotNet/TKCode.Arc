using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="PromptStudio"/>, measured from the prompt studio of the AI group (design reference only, ADR 0015).
/// Colors, radii and fonts come from ArcDesign tokens in the theme. The breakpoints follow the card's own width (Arc's
/// container query on the block), not the window's.
/// </summary>
[ControlDesignToken]
internal sealed class PromptStudioToken : AbstractControlDesignToken
{
    /// <summary>The card is at most this wide (760).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Space inside the header (14 16 14 28).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Space inside the header of a compact card (14 12 14 24).</summary>
    public Thickness HeaderPaddingCompact { get; set; }

    /// <summary>Space between the title group and the model button on one row (12).</summary>
    public double HeaderColumnGap { get; set; }

    /// <summary>Space between the rows when the model button wraps under the title (8).</summary>
    public double HeaderRowGap { get; set; }

    /// <summary>Space between the title and the version button (10).</summary>
    public double TitleGap { get; set; }

    /// <summary>The title's size (16, Geist).</summary>
    public double TitleFontSize { get; set; }

    /// <summary>The title's line height (17.6).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>The title's letter spacing (−0.48).</summary>
    public double TitleLetterSpacing { get; set; }

    /// <summary>Space inside the editor section (10 16 12).</summary>
    public Thickness EditorPadding { get; set; }

    /// <summary>Space inside the editor section of a compact card (10 12 12).</summary>
    public Thickness EditorPaddingCompact { get; set; }

    /// <summary>Height of the row with the tabs and the token estimate (36).</summary>
    public double EditorBarHeight { get; set; }

    /// <summary>Space between the tab row and the editor's text (6, measured: the first line sits 6 lower than the editor's own padding puts it).</summary>
    public Thickness EditorStackMargin { get; set; }

    /// <summary>Space around the hint line under the editor (6 12 0).</summary>
    public Thickness HintMargin { get; set; }

    /// <summary>Space between the parts of the hint line (6).</summary>
    public double HintGap { get; set; }

    /// <summary>Space inside the variables section (14 16 16).</summary>
    public Thickness VarsPadding { get; set; }

    /// <summary>Space inside the variables section of a compact card (14 12 16).</summary>
    public Thickness VarsPaddingCompact { get; set; }

    /// <summary>Space above the row of test cases and Run (14).</summary>
    public Thickness RunRowMargin { get; set; }

    /// <summary>Space between the test cases and Run, side by side or stacked (10).</summary>
    public double RunRowGap { get; set; }

    /// <summary>The output panel's side inset (16).</summary>
    public Thickness OutputInset { get; set; }

    /// <summary>The output panel's side inset on a compact card (12).</summary>
    public Thickness OutputInsetCompact { get; set; }

    /// <summary>Height of a system / user tab (30).</summary>
    public double TabHeight { get; set; }

    /// <summary>Space inside a tab (0 12).</summary>
    public Thickness TabPadding { get; set; }

    /// <summary>Space between the two tabs (2).</summary>
    public double TabGap { get; set; }

    /// <summary>The dot after a tab's name when its part changed in the compare view (5).</summary>
    public double ChangedDotSize { get; set; }

    /// <summary>Space between a tab's name and its dot (6).</summary>
    public double ChangedDotGap { get; set; }

    /// <summary>Space between the word counts and Back to editing (8).</summary>
    public double CornerGap { get; set; }

    /// <summary>Height of Back to editing (32).</summary>
    public double BackButtonHeight { get; set; }

    /// <summary>From this card width down the paddings tighten (520).</summary>
    public double CompactWidth { get; set; }

    /// <summary>From this card width down the test cases and Run stack, each the full width (480).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>From this card width down the variable fields take two columns (560).</summary>
    public double TwoColumnWidth { get; set; }

    /// <summary>From this card width down the variable fields take one column (340).</summary>
    public double OneColumnWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardMaxWidth = 760;
        HeaderPadding = new Thickness(28, 14, 16, 14);
        HeaderPaddingCompact = new Thickness(24, 14, 12, 14);
        HeaderColumnGap = 12;
        HeaderRowGap = 8;
        TitleGap = 10;
        TitleFontSize = 16;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        EditorPadding = new Thickness(16, 10, 16, 12);
        EditorPaddingCompact = new Thickness(12, 10, 12, 12);
        EditorBarHeight = 36;
        EditorStackMargin = new Thickness(0, 6, 0, 0);
        HintMargin = new Thickness(12, 6, 12, 0);
        HintGap = 6;
        VarsPadding = new Thickness(16, 14, 16, 16);
        VarsPaddingCompact = new Thickness(12, 14, 12, 16);
        RunRowMargin = new Thickness(0, 14, 0, 0);
        RunRowGap = 10;
        OutputInset = new Thickness(16, 0);
        OutputInsetCompact = new Thickness(12, 0);
        TabHeight = 30;
        TabPadding = new Thickness(12, 0);
        TabGap = 2;
        ChangedDotSize = 5;
        ChangedDotGap = 6;
        CornerGap = 8;
        BackButtonHeight = 32;
        CompactWidth = 520;
        NarrowWidth = 480;
        TwoColumnWidth = 560;
        OneColumnWidth = 340;
    }
}
