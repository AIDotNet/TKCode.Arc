using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SearchResults"/>; colors come from the ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class SearchResultsToken : AbstractControlDesignToken
{
    /// <summary>Inset of the frame (space-6; space-4 when narrow).</summary>
    public Thickness FramePadding { get; set; }

    public Thickness NarrowFramePadding { get; set; }

    /// <summary>Frame corner (20, like the other page blocks).</summary>
    public CornerRadius FrameRadius { get; set; }

    /// <summary>The column never grows past 760 and stays centered.</summary>
    public double ContentMaxWidth { get; set; }

    /// <summary>Below this control width the frame tightens and rows fold their meta text into the location line.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Space between the field and the facets (space-4).</summary>
    public Thickness FacetsMargin { get; set; }

    /// <summary>Facet track: 1px border, radius-control, 4px inset (the Tabs list).</summary>
    public CornerRadius FacetTrackRadius { get; set; }

    public Thickness FacetTrackInset { get; set; }

    public double FacetGap { get; set; }

    public double FacetHeight { get; set; }

    public Thickness FacetInset { get; set; }

    /// <summary>Selection pill: track radius − inset − border.</summary>
    public CornerRadius FacetPillRadius { get; set; }

    public double FacetCountGap { get; set; }

    /// <summary>Facets without results fade back but keep their place.</summary>
    public double DimmedFacetOpacity { get; set; }

    public Thickness FacetFocusRingMargin { get; set; }

    public CornerRadius FacetFocusRingRadius { get; set; }

    /// <summary>Width of the edge fade on a facet row that scrolls.</summary>
    public double FacetEdgeFade { get; set; }

    /// <summary>The stats line between the facets and the results.</summary>
    public Thickness StatsMargin { get; set; }

    /// <summary>Result rows: 10/12 padding, radius 12, 2px apart.</summary>
    public Thickness RowInset { get; set; }

    public CornerRadius RowRadius { get; set; }

    public double RowGap { get; set; }

    /// <summary>Icon block: 32 square, radius 9, glyph 16.</summary>
    public double IconBlockSize { get; set; }

    public CornerRadius IconBlockRadius { get; set; }

    public Thickness IconBlockMargin { get; set; }

    public Thickness MetaMargin { get; set; }

    public Thickness LocationMargin { get; set; }

    public Thickness SnippetMargin { get; set; }

    public double TitleLineHeight { get; set; }

    public double SnippetLineHeight { get; set; }

    public double CaptionLineHeight { get; set; }

    /// <summary>Snippets show at most this many lines (1 or 2).</summary>
    public int SnippetMaxLines { get; set; }

    /// <summary>Characters kept before the first match when a long snippet is cut.</summary>
    public int SnippetLead { get; set; }

    /// <summary>Longest snippet window, in characters (the text block trims it further to its lines).</summary>
    public int SnippetMaxLength { get; set; }

    /// <summary>Highlight marks: radius 4, 2px wider than the match on each side.</summary>
    public double MarkRadius { get; set; }

    public double MarkInflate { get; set; }

    /// <summary>Group heading inset (the kind and its count above each group in All).</summary>
    public Thickness GroupHeaderInset { get; set; }

    public double GroupHeaderGap { get; set; }

    /// <summary>"See all" and "Show more" sit under the rows; their text lines up with the titles (56 − the button's 12px inset).</summary>
    public Thickness MoreMargin { get; set; }

    /// <summary>Skeleton rows while loading.</summary>
    public Thickness SkeletonInset { get; set; }

    public double SkeletonGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FramePadding = new Thickness(24, 24, 24, 16);
        NarrowFramePadding = new Thickness(16, 16, 16, 8);
        FrameRadius = new CornerRadius(20);
        ContentMaxWidth = 760;
        NarrowWidth = 560;
        FacetsMargin = new Thickness(0, 16, 0, 0);
        FacetTrackRadius = new CornerRadius(18);
        FacetTrackInset = new Thickness(4);
        FacetGap = 2;
        FacetHeight = 32;
        FacetInset = new Thickness(12, 0);
        FacetPillRadius = new CornerRadius(13);
        FacetCountGap = 6;
        DimmedFacetOpacity = 0.45;
        FacetFocusRingMargin = new Thickness(-2);
        FacetFocusRingRadius = new CornerRadius(15);
        FacetEdgeFade = 20;
        StatsMargin = new Thickness(12, 14, 12, 6);
        RowInset = new Thickness(12, 10);
        RowRadius = new CornerRadius(12);
        RowGap = 2;
        IconBlockSize = 32;
        IconBlockRadius = new CornerRadius(9);
        IconBlockMargin = new Thickness(0, 0, 12, 0);
        MetaMargin = new Thickness(12, 0, 0, 0);
        LocationMargin = new Thickness(0, 1, 0, 0);
        SnippetMargin = new Thickness(0, 4, 0, 0);
        TitleLineHeight = 20;
        SnippetLineHeight = 20;
        CaptionLineHeight = 17;
        SnippetMaxLines = 2;
        SnippetLead = 36;
        SnippetMaxLength = 220;
        MarkRadius = 4;
        MarkInflate = 2;
        GroupHeaderInset = new Thickness(12, 14, 12, 4);
        GroupHeaderGap = 6;
        MoreMargin = new Thickness(44, 0, 0, 4);
        SkeletonInset = new Thickness(12, 10);
        SkeletonGap = 6;
    }
}
