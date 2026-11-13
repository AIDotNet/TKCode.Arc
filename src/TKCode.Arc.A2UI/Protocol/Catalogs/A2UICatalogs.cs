namespace TKCode.Arc.A2UI;

/// <summary>The catalogs that ship with the renderer.</summary>
public static class A2UICatalogs
{
    /// <summary>The id of the A2UI 1.0 basic catalog.</summary>
    public const string BasicCatalogId = "https://a2ui.org/specification/v1_0/catalogs/basic/catalog.json";

    private static readonly Lazy<A2UICatalog> s_basic = new(CreateBasic);

    /// <summary>
    /// The A2UI 1.0 basic catalog (18 components, 14 functions), read from the official schema document, with the
    /// renderer-side function implementations attached.
    /// </summary>
    public static A2UICatalog Basic => s_basic.Value;

    /// <summary>A fresh copy of the basic catalog, for apps that attach their own function implementations.</summary>
    public static A2UICatalog CreateBasic()
    {
        var catalog = A2UICatalog.FromJson(A2UICatalogReader.LoadEmbedded("basic_catalog.json"));
        A2UIBasicFunctions.ImplementAll(catalog);
        return catalog;
    }

    /// <summary>The id of the Arc catalog.</summary>
    public const string ArcCatalogId = "https://github.com/AIDotNet/TKCode.Arc/a2ui/catalogs/arc/v1";

    private static readonly Lazy<A2UICatalog> s_arc = new(CreateArc);

    /// <summary>
    /// The Arc catalog: 18 components drawn with Arc controls (charts, metric, table, timeline, stepper, progress, alert,
    /// badge, avatar, Markdown, code block, accordion, switch, segmented control, rating). Agents use it beside the basic
    /// catalog by writing <c>"@catalogId"</c> on a component; it has no functions of its own.
    /// </summary>
    public static A2UICatalog Arc => s_arc.Value;

    /// <summary>A fresh copy of the Arc catalog.</summary>
    public static A2UICatalog CreateArc() => A2UICatalog.FromJson(A2UICatalogReader.LoadEmbedded("arc_catalog.json"));
}
