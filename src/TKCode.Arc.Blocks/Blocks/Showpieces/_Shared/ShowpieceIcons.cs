using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide glyphs the Showpieces need that Core <see cref="ArcIcons"/> does not ship (24×24, stroked; draw with
/// <c>ArcGlyph</c>). Path data converted from lucide-static 1.59.0 the same way <c>tools/lucide/generate_lucide.py</c>
/// does (<c>&lt;rect&gt;</c> as arcs, leading relative moves made absolute) and parsed once through
/// <see cref="ArcIcons.Parse"/>. If Core later gains these glyphs, switch to the <c>ArcIcons</c> members.
/// </summary>
internal static class ShowpieceIcons
{
    // Lucide layout-dashboard, ISC.
    private const string LayoutDashboardData =
        "M4 3h5a1 1 0 0 1 1 1v7a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-7a1 1 0 0 1 1 -1z " +
        "M15 3h5a1 1 0 0 1 1 1v3a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-3a1 1 0 0 1 1 -1z " +
        "M15 12h5a1 1 0 0 1 1 1v7a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-7a1 1 0 0 1 1 -1z " +
        "M4 16h5a1 1 0 0 1 1 1v3a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-3a1 1 0 0 1 1 -1z";

    // Lucide gallery-horizontal-end, ISC.
    private const string GalleryHorizontalEndData =
        "M2 7v10 M6 5v14 M12 3h8a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-8a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z";

    // Lucide dna, ISC.
    private const string DnaData =
        "M10 16l1.5 1.5 M14 8l-1.5-1.5 M15 2c-1.798 1.998-2.518 3.995-2.807 5.993 M16.5 10.5l1 1 " +
        "M17 6l-2.891-2.891 M2 15c6.667-6 13.333 0 20-6 M20 9l.891.891 M3.109 14.109 4 15 M6.5 12.5l1 1 " +
        "M7 18l2.891 2.891 M9 22c1.798-1.998 2.518-3.995 2.807-5.993";

    // Lucide arrow-down-right, ISC.
    private const string ArrowDownRightData = "M7 7l10 10 M17 7v10H7";

    // Lucide map, ISC.
    private const string MapData =
        "M14.106 5.553a2 2 0 0 0 1.788 0l3.659-1.83A1 1 0 0 1 21 4.619v12.764a1 1 0 0 1-.553.894l-4.553 2.277a2 2 0 0 1-1.788 0" +
        "l-4.212-2.106a2 2 0 0 0-1.788 0l-3.659 1.83A1 1 0 0 1 3 19.381V6.618a1 1 0 0 1 .553-.894l4.553-2.277a2 2 0 0 1 1.788 0z " +
        "M15 5.764v15 M9 3.236v15";

    /// <summary>Lucide <c>layout-dashboard</c> (Layout morph: Masonry).</summary>
    public static Geometry LayoutDashboard => ArcIcons.Parse(LayoutDashboardData);

    /// <summary>Lucide <c>gallery-horizontal-end</c> (Layout morph: Carousel).</summary>
    public static Geometry GalleryHorizontalEnd => ArcIcons.Parse(GalleryHorizontalEndData);

    /// <summary>Lucide <c>dna</c> (Layout morph: Helix).</summary>
    public static Geometry Dna => ArcIcons.Parse(DnaData);

    /// <summary>Lucide <c>arrow-down-right</c> (Exploded view: the change row; falling deltas).</summary>
    public static Geometry ArrowDownRight => ArcIcons.Parse(ArrowDownRightData);

    /// <summary>Lucide <c>map</c> (Exploded view: the tab bar).</summary>
    public static Geometry Map => ArcIcons.Parse(MapData);
}
