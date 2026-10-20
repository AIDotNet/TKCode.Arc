using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide glyphs used by the selects whose Core copies join separate SVG paths with a relative move
/// (<c>ArcIcons.X</c> draws one stroke in the wrong place); these restate the subpaths as absolute moves.
/// </summary>
internal static class SelectGlyphs
{
    /// <summary>Lucide <c>x</c>.</summary>
    public static Geometry X { get; } = ArcIcons.X;
}
