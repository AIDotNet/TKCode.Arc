using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CodeSuggestion"/>: the card, its header with the model mark, the editor panel with its file tab,
/// the fixed 20px code rows and their gutter, the review bars under the hunks and the footer with its status and actions,
/// plus the narrow sizes below a 440px card. Colors, fonts and the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class CodeSuggestionToken : AbstractControlDesignToken
{
    /// <summary>The card's corner radius and its widest size.</summary>
    public CornerRadius CardCornerRadius { get; set; }

    public double CardMaxWidth { get; set; }

    /// <summary>The header: its padding, the gap after the mark, the mark's tinted square and glyph.</summary>
    public Thickness HeaderPadding { get; set; }

    public double HeaderGap { get; set; }

    /// <summary>The space between the mark and the title (from <see cref="HeaderGap"/>).</summary>
    public Thickness HeaderGapMargin { get; set; }

    public double MarkBoxSize { get; set; }

    public CornerRadius MarkBoxCornerRadius { get; set; }

    public double MarkSize { get; set; }

    /// <summary>The instruction (14/20, medium) and the model line under it (12/16).</summary>
    public double TitleFontSize { get; set; }

    public double TitleLineHeight { get; set; }

    public double ModelFontSize { get; set; }

    public double ModelLineHeight { get; set; }

    /// <summary>The editor panel inside the card: its margin and corner radius.</summary>
    public Thickness PanelMargin { get; set; }

    public CornerRadius PanelCornerRadius { get; set; }

    /// <summary>The file tab: its height and padding, the gap between its parts and the language mark.</summary>
    public double TabHeight { get; set; }

    public Thickness TabPadding { get; set; }

    public double TabGap { get; set; }

    /// <summary>The space after the language mark, and around the folder (from <see cref="TabGap"/>).</summary>
    public Thickness TabGapMargin { get; set; }

    public Thickness FolderMargin { get; set; }

    public double FileMarkSize { get; set; }

    public CornerRadius FileMarkCornerRadius { get; set; }

    public double FileMarkFontSize { get; set; }

    public double FileFontSize { get; set; }

    /// <summary>The +/− counts at the end of the tab.</summary>
    public double CountFontSize { get; set; }

    /// <summary>One code row: always this high, so folds animate between known heights.</summary>
    public double RowHeight { get; set; }

    /// <summary>The code's size, and its size below a 440px card.</summary>
    public double CodeFontSize { get; set; }

    public double CodeFontSizeNarrow { get; set; }

    /// <summary>The line numbers' column, and its width below a 440px card.</summary>
    public double GutterWidth { get; set; }

    public double GutterWidthNarrow { get; set; }

    /// <summary>The column of the minus and plus signs after the line numbers.</summary>
    public double SignWidth { get; set; }

    /// <summary>The space before the code after the signs, and after the longest line.</summary>
    public double CodeInset { get; set; }

    public double CodeEndPadding { get; set; }

    /// <summary>The space above the first row and below the last.</summary>
    public Thickness CodePadding { get; set; }

    /// <summary>A review bar under a hunk: its height and padding, its buttons and the gap between them.</summary>
    public double BarHeight { get; set; }

    public Thickness BarPadding { get; set; }

    public double BarButtonHeight { get; set; }

    public Thickness BarButtonPadding { get; set; }

    public double BarGap { get; set; }

    /// <summary>The space before each bar button (from <see cref="BarGap"/>).</summary>
    public Thickness BarButtonMargin { get; set; }

    /// <summary>The keyboard ring inside the code area (the panel's radius less its 2px inset).</summary>
    public CornerRadius FocusCornerRadius { get; set; }

    public double BarFontSize { get; set; }

    /// <summary>The footer: its padding and least height, the status icon and label, and the gap above stacked actions.</summary>
    public Thickness FooterPadding { get; set; }

    public double FooterMinHeight { get; set; }

    public double StatusGap { get; set; }

    public double StatusGlyphSize { get; set; }

    public double StatusFontSize { get; set; }

    public double StatusDotSize { get; set; }

    /// <summary>The space between the status icon and its label, and between the status and the actions beside it.</summary>
    public Thickness StatusLabelMargin { get; set; }

    public Thickness ActionsMargin { get; set; }

    public Thickness StackedActionsMargin { get; set; }

    /// <summary>The footer's buttons: their height, padding and spacing, and the shortcut hint's size.</summary>
    public double ActionHeight { get; set; }

    public Thickness ActionInlinePadding { get; set; }

    public double ActionGap { get; set; }

    public double HintFontSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(16);
        CardMaxWidth = 720;
        HeaderPadding = new Thickness(16, 14, 16, 12);
        HeaderGap = 10;
        HeaderGapMargin = new Thickness(HeaderGap, 0, 0, 0);
        MarkBoxSize = 28;
        MarkBoxCornerRadius = new CornerRadius(8);
        MarkSize = 16;
        TitleFontSize = 14;
        TitleLineHeight = 20;
        ModelFontSize = 12;
        ModelLineHeight = 16;
        PanelMargin = new Thickness(12, 0, 12, 0);
        PanelCornerRadius = new CornerRadius(12);
        FocusCornerRadius = new CornerRadius(10);
        TabHeight = 36;
        TabPadding = new Thickness(12, 0, 12, 0);
        TabGap = 8;
        TabGapMargin = new Thickness(0, 0, TabGap, 0);
        FolderMargin = new Thickness(TabGap, 0, TabGap, 0);
        FileMarkSize = 16;
        FileMarkCornerRadius = new CornerRadius(4);
        FileMarkFontSize = 7.5;
        FileFontSize = 12;
        CountFontSize = 12;
        RowHeight = 20;
        CodeFontSize = 13;
        CodeFontSizeNarrow = 12;
        GutterWidth = 40;
        GutterWidthNarrow = 30;
        SignWidth = 16;
        CodeInset = 4;
        CodeEndPadding = 24;
        CodePadding = new Thickness(0, 8, 0, 8);
        BarHeight = 38;
        BarPadding = new Thickness(12, 0, 8, 0);
        BarButtonHeight = 26;
        BarButtonPadding = new Thickness(10, 0);
        BarGap = 6;
        BarButtonMargin = new Thickness(BarGap, 0, 0, 0);
        BarFontSize = 12;
        FooterPadding = new Thickness(16, 12, 12, 12);
        FooterMinHeight = 56;
        StatusGap = 8;
        StatusGlyphSize = 14;
        StatusFontSize = 13;
        StatusDotSize = 7;
        StatusLabelMargin = new Thickness(StatusGap, 0, 0, 0);
        ActionsMargin = new Thickness(12, 0, 0, 0);
        StackedActionsMargin = new Thickness(0, 10, 0, 0);
        ActionHeight = 30;
        ActionInlinePadding = new Thickness(12, 0);
        ActionGap = 8;
        HintFontSize = 11;
    }
}
