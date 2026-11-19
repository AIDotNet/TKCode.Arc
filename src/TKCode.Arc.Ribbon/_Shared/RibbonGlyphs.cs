using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Glyphs the ribbon needs that are not in Core's <see cref="ArcIcons"/> subset (Lucide, ISC).</summary>
internal static class RibbonGlyphs
{
    /// <summary>Lucide <c>arrow-down-right</c>: the launcher of a group's more options.</summary>
    public static readonly Geometry Launcher = ArcIcons.Parse("m7 7 10 10M17 7v10H7");
}
