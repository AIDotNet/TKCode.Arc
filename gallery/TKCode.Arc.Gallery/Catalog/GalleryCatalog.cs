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

    /// <summary>Arc's groups, then Blocks, then the extras: what the sidebar lists.</summary>
    public static IEnumerable<GalleryCategory> AllCategories => Categories.Append(Blocks).Concat(Extras);

    /// <summary>Every entry the gallery lists (Arc components, blocks and extras).</summary>
    public static IEnumerable<GalleryEntry> AllEntries => AllCategories.SelectMany(c => c.AllEntries);

    /// <summary>Number of Arc components (extras not included).</summary>
    public static int Count => Categories.Sum(c => c.Entries.Length);
}
