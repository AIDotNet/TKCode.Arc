using TKCode.Arc.Gallery.Catalog;

namespace TKCode.Arc.Gallery.Shell;

public enum GalleryRouteKind
{
    Home,
    Docs,
    Components,
    Blocks
}

/// <summary>
/// Where the gallery is: the home page, or the page of one entry of the Docs, Components or Blocks section. A route has
/// an address that the WebAssembly host keeps in the browser's address bar: <c>/</c>, <c>/docs/installation</c>,
/// <c>/components/split-button</c> and <c>/blocks/command-palette</c>, made of the section and the entry's slug.
/// </summary>
public sealed record GalleryRoute(GalleryRouteKind Kind, GalleryEntry? Entry = null)
{
    public const string Brand = "Arc Gallery";

    private static readonly Lazy<Dictionary<(GalleryRouteKind, string), GalleryEntry>> s_entries = new(BuildIndex);

    public static GalleryRoute Home { get; } = new(GalleryRouteKind.Home);

    /// <summary>The route of <paramref name="entry"/>'s page.</summary>
    public static GalleryRoute To(GalleryEntry entry) => new(KindOf(entry), entry);

    /// <summary>The section an entry belongs to.</summary>
    public static GalleryRouteKind KindOf(GalleryEntry entry)
    {
        if (GalleryCatalog.Docs.Entries.Contains(entry))
        {
            return GalleryRouteKind.Docs;
        }
        return s_entries.Value.TryGetValue((GalleryRouteKind.Blocks, entry.Slug), out var block) && block == entry
            ? GalleryRouteKind.Blocks
            : GalleryRouteKind.Components;
    }

    /// <summary>The address: <c>/</c> for the home page, otherwise <c>/&lt;section&gt;/&lt;slug&gt;</c>.</summary>
    public string Path => Entry is null
        ? "/"
        : $"/{Kind.ToString().ToLowerInvariant()}/{Entry.Slug}";

    /// <summary>The page title: the brand, preceded by the entry's title on a page of the gallery.</summary>
    public string Title => Entry is null ? Brand : $"{Entry.Title} · {Brand}";

    /// <summary>
    /// The route at <paramref name="path"/>, or null when no page has that address. A query, a fragment, a trailing slash
    /// and letter case are ignored.
    /// </summary>
    public static GalleryRoute? Parse(string? path)
    {
        if (path is null)
        {
            return null;
        }
        var end = path.IndexOfAny(['?', '#']);
        var segments = (end < 0 ? path : path[..end]).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return Home;
        }
        if (segments.Length != 2 || !Enum.TryParse<GalleryRouteKind>(segments[0], ignoreCase: true, out var kind) || kind == GalleryRouteKind.Home)
        {
            return null;
        }
        return s_entries.Value.TryGetValue((kind, segments[1].ToLowerInvariant()), out var entry) ? new GalleryRoute(kind, entry) : null;
    }

    private static Dictionary<(GalleryRouteKind, string), GalleryEntry> BuildIndex()
    {
        var index = new Dictionary<(GalleryRouteKind, string), GalleryEntry>();
        foreach (var entry in GalleryCatalog.Docs.Entries)
        {
            index[(GalleryRouteKind.Docs, entry.Slug)] = entry;
        }
        foreach (var entry in GalleryCatalog.Blocks.AllEntries)
        {
            index[(GalleryRouteKind.Blocks, entry.Slug)] = entry;
        }
        // Special components live under /components/ too, as on uiarc.dev.
        foreach (var entry in GalleryCatalog.AllCategories.Concat(GalleryCatalog.Special).SelectMany(c => c.AllEntries))
        {
            index[(GalleryRouteKind.Components, entry.Slug)] = entry;
        }
        return index;
    }
}
