using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide glyphs the agent status needs that Core <see cref="ArcIcons"/> does not ship (24×24, stroked). Path data is
/// lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c> (the same strings as
/// <c>TKCode.Arc.Icons.Lucide</c>, which a control package must not depend on), parsed once through
/// <see cref="ArcIcons.Parse"/>. The check and cross drawn inside the orb are our own short strokes (see
/// <see cref="AgentStatusFace"/>), not icons.
/// </summary>
internal static class AgentStatusIcons
{
    // Lucide bot, ISC.
    private const string BotData =
        "M12 8V4H8 M6 8h12a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2z M2 14h2 M20 14h2 M15 13v2 M9 13v2";

    // Lucide file-search, ISC.
    private const string FileSearchData =
        "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z " +
        "M14 2v5a1 1 0 0 0 1 1h5 M9 14.5a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0 -5 0z M13.3 16.3 15 18";

    // Lucide file-pen, ISC.
    private const string FilePenData =
        "M12.659 22H18a2 2 0 0 0 2-2V8a2.4 2.4 0 0 0-.706-1.706l-3.588-3.588A2.4 2.4 0 0 0 14 2H6a2 2 0 0 0-2 2v9.34 " +
        "M14 2v5a1 1 0 0 0 1 1h5 M10.378 12.622a1 1 0 0 1 3 3.003L8.36 20.637a2 2 0 0 1-.854.506l-2.867.837a.5.5 0 0 1-.62-.62l.836-2.869a2 2 0 0 1 .506-.853z";

    // Lucide files, ISC.
    private const string FilesData =
        "M15 2h-4a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V8 M16.706 2.706A2.4 2.4 0 0 0 15 2v5a1 1 0 0 0 1 1h5a2.4 2.4 0 0 0-.706-1.706z " +
        "M5 7a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h8a2 2 0 0 0 1.732-1";

    // Lucide flask-conical, ISC.
    private const string FlaskConicalData =
        "M14 2v6a2 2 0 0 0 .245.96l5.51 10.08A2 2 0 0 1 18 22H6a2 2 0 0 1-1.755-2.96l5.51-10.08A2 2 0 0 0 10 8V2 M6.453 15h11.094 M8.5 2h7";

    // Lucide scale, ISC.
    private const string ScaleData =
        "M12 3v18 M19 8l3 8a5 5 0 0 1-6 0zV7 M3 7h1a17 17 0 0 0 8-2 17 17 0 0 0 8 2h1 M5 8l3 8a5 5 0 0 1-6 0zV7 M7 21h10";

    // Lucide notebook-pen, ISC.
    private const string NotebookPenData =
        "M13.4 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-7.4 M2 6h4 M2 10h4 M2 14h4 M2 18h4 " +
        "M21.378 5.626a1 1 0 1 0-3.004-3.004l-5.01 5.012a2 2 0 0 0-.506.854l-.837 2.87a.5.5 0 0 0 .62.62l2.87-.837a2 2 0 0 0 .854-.506z";

    // Lucide hexagon, ISC.
    private const string HexagonData =
        "M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z";

    // Lucide orbit, ISC.
    private const string OrbitData =
        "M20.341 6.484A10 10 0 0 1 10.266 21.85 M3.659 17.516A10 10 0 0 1 13.74 2.152 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z " +
        "M17 5a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M3 19a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z";

    /// <summary>A robot head: the default agent mark.</summary>
    public static Geometry Bot => ArcIcons.Parse(BotData);

    /// <summary>A page with a magnifier: scanning files.</summary>
    public static Geometry FileSearch => ArcIcons.Parse(FileSearchData);

    /// <summary>A page with a pen: editing a file.</summary>
    public static Geometry FilePen => ArcIcons.Parse(FilePenData);

    /// <summary>Two pages: several files.</summary>
    public static Geometry Files => ArcIcons.Parse(FilesData);

    /// <summary>A lab flask: tests.</summary>
    public static Geometry FlaskConical => ArcIcons.Parse(FlaskConicalData);

    /// <summary>A balance: weighing options.</summary>
    public static Geometry Scale => ArcIcons.Parse(ScaleData);

    /// <summary>A notebook with a pen: writing.</summary>
    public static Geometry NotebookPen => ArcIcons.Parse(NotebookPenData);

    /// <summary>A hexagon: the coding sample's mark.</summary>
    public static Geometry Hexagon => ArcIcons.Parse(HexagonData);

    /// <summary>An orbit: the research sample's mark.</summary>
    public static Geometry Orbit => ArcIcons.Parse(OrbitData);
}
