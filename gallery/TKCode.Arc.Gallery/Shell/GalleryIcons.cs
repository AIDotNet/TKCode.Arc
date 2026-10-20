using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Lucide glyphs the gallery chrome needs whose <see cref="ArcIcons"/> data draws wrongly: the generator joins an
/// icon's shapes into one path and leaves each later shape's leading <c>m</c> relative, so it starts from the end of
/// the previous shape instead of the origin. These are the same Lucide shapes with absolute starts.
/// </summary>
internal static class GalleryIcons
{
    public static Geometry Sun { get; } = ArcIcons.Sun;

    public static Geometry Code { get; } = ArcIcons.Code;

    public static Geometry X { get; } = ArcIcons.X;
}
