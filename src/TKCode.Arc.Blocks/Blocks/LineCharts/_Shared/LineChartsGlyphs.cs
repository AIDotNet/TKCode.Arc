using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs the Line charts blocks need that Core does not ship (24 × 24 strokes).</summary>
internal static class LineChartsGlyphs
{
    /// <summary>Lucide <c>arrow-down-right</c>: a falling trend.</summary>
    public static readonly Geometry ArrowDownRight = ArcIcons.Parse("M7 7l10 10 M17 7v10H7");

    /// <summary>Lucide <c>rocket</c>: a launch.</summary>
    public static readonly Geometry Rocket = ArcIcons.Parse(
        "M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5 " +
        "M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09 " +
        "M9 12a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.4 22.4 0 0 1-4 2z " +
        "M9 12H4s.55-3.03 2-4c1.62-1.08 5 .05 5 .05");

    /// <summary>Lucide <c>newspaper</c>: press coverage.</summary>
    public static readonly Geometry Newspaper = ArcIcons.Parse(
        "M15 18h-5 M18 14h-8 " +
        "M4 22h16a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16a2 2 0 0 1-4 0v-9a2 2 0 0 1 2-2h2 " +
        "M11 6h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1h-6a1 1 0 0 1-1-1v-2a1 1 0 0 1 1-1z");
}
