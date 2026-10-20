namespace TKCode.Arc.Gallery.Catalog;

/// <param name="IsArc">True for Arc components (uiarc.dev originals); false for TKCode extras such as Markdown.</param>
public sealed record GalleryEntry(string ClassName, string Title, string Slug, int Phase, string Description, bool IsArc = true)
{
    /// <summary>The Arc original on uiarc.dev; null for extras, which have none.</summary>
    public string? ArcUrl => IsArc ? $"https://uiarc.dev/components/{Slug}" : null;
}

/// <param name="IsArc">True for Arc's sidebar groups; false for the TKCode extras group.</param>
/// <param name="Subcategories">Groups nested under this one (Blocks); null for a flat group.</param>
public sealed record GalleryCategory(string Code, string Title, string ChineseTitle, GalleryEntry[] Entries, bool IsArc = true,
    GalleryCategory[]? Subcategories = null)
{
    /// <summary>The entries of this group and of all its subgroups.</summary>
    public IEnumerable<GalleryEntry> AllEntries => Entries.Concat((Subcategories ?? Array.Empty<GalleryCategory>()).SelectMany(s => s.AllEntries));
}

public static partial class GalleryCatalog
{
    /// <summary>
    /// TKCode components with no Arc original, listed after Arc's groups. They are not part of the 110 Arc components
    /// (<see cref="Categories"/> / <see cref="Count"/>).
    /// </summary>
    public static readonly GalleryCategory[] Extras =
    [
        new("Content", "Extras", "扩展",
        [
            new("Markdown", "Markdown", "markdown", 0,
                "Render Markdown documents and streamed replies in Arc's style, with fenced code highlighted in Arc's syntax palette.",
                IsArc: false),
        ], IsArc: false),
        new("Illustrations", "Illustrations", "插图",
        [
            new("DataFlow", "Data flow", "data-flow", 0,
                "Events stream from their sources through a queue and a batch transform into a warehouse and live dashboards.",
                IsArc: false),
            new("PaymentFlow", "Payment flow", "payment-flow", 0,
                "A card payment from checkout through tokenization, authorization and the issuer's decision to settlement.",
                IsArc: false),
            new("MultiRegionFailover", "Multi-region failover", "multi-region-failover", 0,
                "A global load balancer shifts traffic to a standby region and promotes its database when the primary region goes down.",
                IsArc: false),
            new("VectorSearch", "Vector search", "vector-search", 0,
                "A question is embedded as a vector and answered with its nearest documents by cosine similarity.",
                IsArc: false),
        ], IsArc: false),
    ];

    /// <summary>
    /// The Blocks group under Components, with its subgroups from the TKCode.Arc.Blocks catalog. Not part of the 110
    /// Arc components (<see cref="Categories"/> / <see cref="Count"/>), and it holds no blocks yet.
    /// </summary>
    public static readonly GalleryCategory Blocks = new("Blocks", "Blocks", "区块", [],
        Subcategories: BlockCatalog.Groups.Select(g => new GalleryCategory(g.Code, g.Title, g.ChineseTitle, [])).ToArray());

    /// <summary>
    /// The Docs section of the sidebar: the gallery's own documentation pages, listed first, as on uiarc.dev. Not Arc
    /// components, and their pages are planned.
    /// </summary>
    public static readonly GalleryCategory Docs = new("Docs", "Docs", "文档",
    [
        new("DocsIntroduction", "Introduction", "introduction", 0, "What Arc is and how the gallery is organized.", IsArc: false),
        new("DocsInstallation", "Installation", "installation", 0, "Add the Arc packages to a project.", IsArc: false),
        new("DocsTheming", "Theming", "theming", 0, "Themes, design tokens and appearance.", IsArc: false),
        new("DocsMotion", "Motion", "motion", 0, "The motion engine and its presets.", IsArc: false),
        new("DocsAiAndMcp", "AI and MCP", "ai-and-mcp", 0, "Use Arc with AI assistants through MCP.", IsArc: false),
        new("DocsChangelog", "Changelog", "changelog", 0, "Release notes.", IsArc: false),
    ], IsArc: false);

    /// <summary>The Components section of the sidebar: Arc's groups, then the extras.</summary>
    public static IEnumerable<GalleryCategory> AllCategories => Categories.Concat(Extras);

    /// <summary>Every entry the gallery lists: the Docs, Components and Blocks sections.</summary>
    public static IEnumerable<GalleryEntry> AllEntries =>
        Docs.AllEntries.Concat(AllCategories.SelectMany(c => c.AllEntries)).Concat(Blocks.AllEntries);

    /// <summary>Number of Arc components (extras not included).</summary>
    public static int Count => Categories.Sum(c => c.Entries.Length);
}
