using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SearchResults"/>; colors come from the ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class SearchResultsToken : AbstractControlDesignToken
{
    /// <summary>Below this control width the filters sit behind a button and the layout is one column.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>The search field's row: the inset around the large field.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness NarrowHeaderPadding { get; set; }

    /// <summary>The field: inset, space after the glass, glass and clear sizes, and the shortcut keycap.</summary>
    public Thickness FieldInset { get; set; }

    public double FieldIconGap { get; set; }

    public double FieldIconSize { get; set; }

    public double ClearButtonSize { get; set; }

    public double ClearIconSize { get; set; }

    public double KeycapSize { get; set; }

    public CornerRadius KeycapRadius { get; set; }

    public double FieldMinHeight { get; set; }

    public CornerRadius FieldRadius { get; set; }

    /// <summary>The filters column: width (with its divider), inset, and the spaces between its parts.</summary>
    public double FiltersWidth { get; set; }

    public Thickness FiltersPadding { get; set; }

    public double FiltersHeaderGap { get; set; }

    public double FilterGroupGap { get; set; }

    public double FilterGroupTitleGap { get; set; }

    public double FilterRowHeight { get; set; }

    public Thickness FilterCountMargin { get; set; }

    public Thickness FilterCheckMargin { get; set; }

    public double DisabledOpacity { get; set; }

    public double DateTitleGap { get; set; }

    public double DateCaptionGap { get; set; }

    /// <summary>The results pane: inset, header, bar and list spacing.</summary>
    public Thickness PanePadding { get; set; }

    public Thickness NarrowPanePadding { get; set; }

    public double HeaderRowGap { get; set; }

    public Thickness ChipBarMargin { get; set; }

    public Thickness ChipBarPadding { get; set; }

    public CornerRadius ChipBarRadius { get; set; }

    public double ChipGap { get; set; }

    public double ChipHeight { get; set; }

    public Thickness ChipInset { get; set; }

    public double ListGap { get; set; }

    public Thickness ListMargin { get; set; }

    /// <summary>Space between the results and the key hints under them.</summary>
    public double HintsGap { get; set; }

    public double HintKeySize { get; set; }

    public CornerRadius HintKeyRadius { get; set; }

    public double HintGap { get; set; }

    /// <summary>Result rows: inset, radius, the avatar / icon column, and the spaces between the lines of text.</summary>
    public Thickness RowInset { get; set; }

    public CornerRadius RowRadius { get; set; }

    public double LeadSize { get; set; }

    public Thickness LeadMargin { get; set; }

    public double TitleLineHeight { get; set; }

    public double SubtitleLineHeight { get; set; }

    public double SnippetLineHeight { get; set; }

    public Thickness SubtitleMargin { get; set; }

    public Thickness SnippetMargin { get; set; }

    public Thickness CaptionMargin { get; set; }

    /// <summary>Snippets show at most this many lines (1 or 2).</summary>
    public int SnippetMaxLines { get; set; }

    /// <summary>Characters kept before the first match when a long snippet is cut.</summary>
    public int SnippetLead { get; set; }

    /// <summary>Longest snippet window, in characters (the text block trims it further to its lines).</summary>
    public int SnippetMaxLength { get; set; }

    /// <summary>Space a row spends outside its snippet text (insets, avatar column, pane padding).</summary>
    public double RowTextInset { get; set; }

    /// <summary>Highlight marks: radius 4, 2px wider than the match on each side.</summary>
    public double MarkRadius { get; set; }

    public double MarkInflate { get; set; }

    /// <summary>Skeleton rows while loading.</summary>
    public Thickness SkeletonInset { get; set; }

    public double SkeletonGap { get; set; }

    /// <summary>The suggestions popup: inset, rows, labels.</summary>
    public Thickness SuggestionPadding { get; set; }

    public CornerRadius SuggestionSurfaceRadius { get; set; }

    /// <summary>Space between the field and the popup under it.</summary>
    public double SuggestionOffset { get; set; }

    public double SuggestionRowHeight { get; set; }

    public CornerRadius SuggestionRowRadius { get; set; }

    public double SuggestionRowGap { get; set; }

    public Thickness SuggestionRowInset { get; set; }

    public double SuggestionIconGap { get; set; }

    public Thickness SuggestionLabelMargin { get; set; }

    /// <summary>The detail and the sheets: panel radius, insets, and the table.</summary>
    public CornerRadius SurfaceRadius { get; set; }

    public CornerRadius SheetRadius { get; set; }

    public Thickness SurfaceHeaderPadding { get; set; }

    public Thickness SurfaceBodyPadding { get; set; }

    public Thickness SurfaceActionsPadding { get; set; }

    public double SurfaceCloseSize { get; set; }

    public double GrabberWidth { get; set; }

    public double GrabberHeight { get; set; }

    public double DetailTableRowHeight { get; set; }

    public double DetailParagraphGap { get; set; }

    public double DetailAvatarSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        NarrowWidth = 560;
        HeaderPadding = new Thickness(24, 24, 24, 18);
        NarrowHeaderPadding = new Thickness(16, 16, 16, 12);
        FieldInset = new Thickness(16, 0, 12, 0);
        FieldIconGap = 12;
        FieldIconSize = 18;
        ClearButtonSize = 24;
        ClearIconSize = 16;
        KeycapSize = 24;
        KeycapRadius = new CornerRadius(8);
        FieldMinHeight = 50;
        FieldRadius = new CornerRadius(18);
        FiltersWidth = 244;
        FiltersPadding = new Thickness(24, 22, 24, 24);
        FiltersHeaderGap = 28;
        FilterGroupGap = 25;
        FilterGroupTitleGap = 5;
        FilterRowHeight = 34;
        FilterCountMargin = new Thickness(12, 0, 0, 0);
        FilterCheckMargin = new Thickness(-13, -4.75, 0, -4.75);
        DisabledOpacity = 0.5;
        DateTitleGap = 10;
        DateCaptionGap = 10;
        PanePadding = new Thickness(24, 18, 24, 16);
        NarrowPanePadding = new Thickness(16, 12, 16, 12);
        HeaderRowGap = 8;
        ChipBarMargin = new Thickness(0, 14, 0, 0);
        ChipBarPadding = new Thickness(18, 14, 18, 14);
        ChipBarRadius = new CornerRadius(28);
        ChipGap = 8;
        ChipHeight = 34;
        ChipInset = new Thickness(12, 0, 6, 0);
        ListGap = 4;
        ListMargin = new Thickness(-4, 12, -4, 8);
        HintsGap = 14;
        HintKeySize = 22;
        HintKeyRadius = new CornerRadius(7);
        HintGap = 4;
        RowInset = new Thickness(20, 14, 20, 12);
        RowRadius = new CornerRadius(24);
        LeadSize = 36;
        LeadMargin = new Thickness(0, 0, 16, 0);
        TitleLineHeight = 22;
        SubtitleLineHeight = 17;
        SnippetLineHeight = 20;
        SubtitleMargin = new Thickness(0, 2, 0, 0);
        SnippetMargin = new Thickness(0, 6, 0, 0);
        CaptionMargin = new Thickness(0, 10, 0, 0);
        SnippetMaxLines = 2;
        SnippetLead = 36;
        SnippetMaxLength = 220;
        RowTextInset = 96;
        MarkRadius = 4;
        MarkInflate = 2;
        SkeletonInset = new Thickness(20, 14);
        SkeletonGap = 8;
        SuggestionPadding = new Thickness(6);
        SuggestionSurfaceRadius = new CornerRadius(16);
        SuggestionOffset = 6;
        SuggestionRowHeight = 40;
        SuggestionRowRadius = new CornerRadius(12);
        SuggestionRowGap = 3;
        SuggestionRowInset = new Thickness(12, 0);
        SuggestionIconGap = 12;
        SuggestionLabelMargin = new Thickness(12, 7, 12, 5);
        SurfaceRadius = new CornerRadius(30);
        SheetRadius = new CornerRadius(30, 30, 0, 0);
        SurfaceHeaderPadding = new Thickness(24, 24, 24, 0);
        SurfaceBodyPadding = new Thickness(24, 20, 24, 0);
        SurfaceActionsPadding = new Thickness(24, 24, 24, 24);
        SurfaceCloseSize = 36;
        GrabberWidth = 40;
        GrabberHeight = 4;
        DetailTableRowHeight = 40;
        DetailParagraphGap = 12;
        DetailAvatarSize = 22;
    }
}
