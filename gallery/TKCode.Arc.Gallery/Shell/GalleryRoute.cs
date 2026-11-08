using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.DemoApp;

namespace TKCode.Arc.Gallery.Shell;

public enum GalleryRouteKind
{
    Home,
    Docs,
    Components,
    Blocks,
    Friends,
    Demo,

    /// <summary>The StreamMark product page.</summary>
    StreamMark,

    /// <summary>The open source projects.</summary>
    Projects,

    /// <summary>The overview of the components, blocks, effects and mobile controls.</summary>
    ComponentsIndex,

    /// <summary>The desktop app's settings; the site has no such pages.</summary>
    Settings
}

/// <summary>
/// Where the gallery is: the home page, or the page of one entry of the Docs, Components or Blocks section. A route has
/// an address that the WebAssembly host keeps in the browser's address bar: <c>/</c>, <c>/docs/installation</c>,
/// <c>/components/split-button</c> and <c>/blocks/command-palette</c>, made of the section and the entry's slug. The
/// Northstar dashboard demo lives under <c>/demo</c>: <c>/demo/overview</c>, <c>/demo/login</c> (<see cref="Page"/>);
/// <c>/demo</c> itself names no page and opens the one the demo's session leads to. <c>/components</c> is the overview of the
/// library, <c>/projects</c> the open source projects, <c>/streammark</c> the StreamMark product page, and <c>/settings/about</c> a page of the desktop app's settings (<see cref="Page"/>).
/// </summary>
public sealed record GalleryRoute(GalleryRouteKind Kind, GalleryEntry? Entry = null, string? Page = null)
{
    public const string Brand = "TKCode Gallery";

    private static readonly Lazy<Dictionary<(GalleryRouteKind, string), GalleryEntry>> s_entries = new(BuildIndex);

    public static GalleryRoute Home { get; } = new(GalleryRouteKind.Home);

    /// <summary>The friends and sponsors page.</summary>
    public static GalleryRoute Friends { get; } = new(GalleryRouteKind.Friends);

    /// <summary>The StreamMark product page.</summary>
    public static GalleryRoute StreamMark { get; } = new(GalleryRouteKind.StreamMark);

    /// <summary>The open source projects.</summary>
    public static GalleryRoute Projects { get; } = new(GalleryRouteKind.Projects);

    /// <summary>The overview of the library.</summary>
    public static GalleryRoute ComponentsIndex { get; } = new(GalleryRouteKind.ComponentsIndex);

    /// <summary>The desktop app's settings page <paramref name="page"/> (one of <see cref="GallerySettingsView.Pages"/>).</summary>
    public static GalleryRoute Settings(string page) => new(GalleryRouteKind.Settings, Page: page);

    /// <summary>
    /// The route of the dashboard demo's <paramref name="page"/> (a slug of <see cref="DemoPages"/>), or of <c>/demo</c>,
    /// which opens the overview, or the sign-in page when the demo's visitor signed out.
    /// </summary>
    public static GalleryRoute Demo(string? page = null) => new(GalleryRouteKind.Demo, Page: page);

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
    public string Path => Kind switch
    {
        GalleryRouteKind.Friends => "/friends",
        GalleryRouteKind.StreamMark => "/streammark",
        GalleryRouteKind.Projects => "/projects",
        GalleryRouteKind.ComponentsIndex => "/components",
        GalleryRouteKind.Settings => $"/settings/{Page}",
        GalleryRouteKind.Demo => Page is null ? "/demo" : $"/demo/{Page}",
        _ => Entry is null ? "/" : $"/{Kind.ToString().ToLowerInvariant()}/{Entry.Slug}"
    };

    /// <summary>The page title: the brand, preceded by the entry's title on a page of the gallery.</summary>
    public string Title => Kind switch
    {
        GalleryRouteKind.Friends => $"Friends · {Brand}",
        GalleryRouteKind.StreamMark => $"StreamMark · {Brand}",
        GalleryRouteKind.Projects => $"Open source · {Brand}",
        GalleryRouteKind.ComponentsIndex => $"Components · {Brand}",
        GalleryRouteKind.Settings => $"Settings · {Brand}",
        GalleryRouteKind.Demo => DemoPages.Find(Page) is { } page ? $"{page.Title} · Demo · {Brand}" : $"Demo · {Brand}",
        _ => Entry is null ? Brand : $"{Entry.Title} · {Brand}"
    };

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
        if (segments.Length == 1 && segments[0].Equals("friends", StringComparison.OrdinalIgnoreCase))
        {
            return Friends;
        }
        if (segments.Length == 1 && segments[0].Equals("streammark", StringComparison.OrdinalIgnoreCase))
        {
            return StreamMark;
        }
        if (segments.Length == 1 && segments[0].Equals("projects", StringComparison.OrdinalIgnoreCase))
        {
            return Projects;
        }
        if (segments.Length == 1 && segments[0].Equals("components", StringComparison.OrdinalIgnoreCase))
        {
            return ComponentsIndex;
        }
        if (segments[0].Equals("settings", StringComparison.OrdinalIgnoreCase))
        {
            // The site has no settings, so the address means nothing there.
            return GalleryHost.IsDesktop && segments.Length == 2 && GallerySettingsView.Pages.Contains(segments[1].ToLowerInvariant())
                ? Settings(segments[1].ToLowerInvariant())
                : null;
        }
        if (segments[0].Equals("demo", StringComparison.OrdinalIgnoreCase))
        {
            return segments.Length switch
            {
                1 => Demo(),
                2 when DemoPages.Find(segments[1].ToLowerInvariant()) is { } page => Demo(page.Slug),
                _ => null
            };
        }
        if (segments.Length != 2 || !Enum.TryParse<GalleryRouteKind>(segments[0], ignoreCase: true, out var kind) || kind is GalleryRouteKind.Home or GalleryRouteKind.Friends or GalleryRouteKind.Demo or GalleryRouteKind.StreamMark or GalleryRouteKind.Projects or GalleryRouteKind.ComponentsIndex or GalleryRouteKind.Settings)
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
        // Special components live under /components/ too, as on uiarc.dev; so do the Mobile components.
        foreach (var entry in GalleryCatalog.AllCategories.Concat(GalleryCatalog.Special).Append(GalleryCatalog.Mobile).SelectMany(c => c.AllEntries))
        {
            index[(GalleryRouteKind.Components, entry.Slug)] = entry;
        }
        return index;
    }
}
