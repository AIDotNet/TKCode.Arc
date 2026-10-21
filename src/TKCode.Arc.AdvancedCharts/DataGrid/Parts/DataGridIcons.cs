using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs the grid needs beyond Core's built-in set (24x24, stroked; ISC license).</summary>
internal static class DataGridIcons
{
    /// <summary>Lucide "pin".</summary>
    public static Geometry Pin => ArcIcons.Parse(
        "M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z");

    /// <summary>Lucide "pin-off".</summary>
    public static Geometry PinOff => ArcIcons.Parse(
        "M12 17v5 M15 9.34V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H7.89 m2 2 20 20 M9 9v1.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h11");

    /// <summary>Lucide "rows-4" (compact density).</summary>
    public static Geometry Rows4 => ArcIcons.Parse(
        "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M21 7.5H3 M21 12H3 M21 16.5H3");

    /// <summary>Lucide "rows-3" (standard density).</summary>
    public static Geometry Rows3 => ArcIcons.Parse(
        "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M21 9H3 M21 15H3");

    /// <summary>Lucide "group".</summary>
    public static Geometry Group => ArcIcons.Parse(
        "M3 7V5c0-1.1.9-2 2-2h2 M17 3h2c1.1 0 2 .9 2 2v2 M21 17v2c0 1.1-.9 2-2 2h-2 M7 21H5c-1.1 0-2-.9-2-2v-2 M8 7h5a1 1 0 0 1 1 1v3a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-3a1 1 0 0 1 1 -1z M11 12h5a1 1 0 0 1 1 1v3a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-3a1 1 0 0 1 1 -1z");

    /// <summary>Lucide "ungroup".</summary>
    public static Geometry Ungroup => ArcIcons.Parse(
        "M13 14h6a2 2 0 0 1 2 2v3a2 2 0 0 1 -2 2h-6a2 2 0 0 1 -2 -2v-3a2 2 0 0 1 2 -2z M5 3h6a2 2 0 0 1 2 2v3a2 2 0 0 1 -2 2h-6a2 2 0 0 1 -2 -2v-3a2 2 0 0 1 2 -2z");

    /// <summary>Lucide "file-spreadsheet".</summary>
    public static Geometry FileSpreadsheet => ArcIcons.Parse(
        "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M8 13h2 M14 13h2 M8 17h2 M14 17h2");
}
