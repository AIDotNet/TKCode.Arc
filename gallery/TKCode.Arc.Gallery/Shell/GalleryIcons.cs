using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Lucide glyphs the gallery chrome needs whose <see cref="ArcIcons"/> data draws wrongly: the generator joins an
/// icon's shapes into one path and leaves each later shape's leading <c>m</c> relative, so it starts from the end of
/// the previous shape instead of the origin. These are the same Lucide shapes with absolute starts. It also holds the few
/// Lucide glyphs the chrome needs that the Core subset lacks (the title bar's pin).
/// </summary>
internal static class GalleryIcons
{
    public static Geometry Sun { get; } = ArcIcons.Sun;

    public static Geometry Code { get; } = ArcIcons.Code;

    public static Geometry X { get; } = ArcIcons.X;

    /// <summary>Lucide <c>pin</c> (not in the Core subset): the title bar's keep-on-top button.</summary>
    public static Geometry Pin { get; } = ArcIcons.Parse(
        "M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z");

    /// <summary>Lucide <c>pin-off</c>: the keep-on-top button while the window is pinned.</summary>
    public static Geometry PinOff { get; } = ArcIcons.Parse(
        "M12 17v5 M15 9.34V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H7.89 M2 2l20 20 M9 9v1.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h11");
}
