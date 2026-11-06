using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs (ISC) the dock needs that the Core subset (<see cref="ArcIcons"/>) does not carry.</summary>
internal static class DockGlyphs
{
    private static Geometry? s_pin;
    private static Geometry? s_pinOff;

    /// <summary>Lucide <c>pin</c>: the flyout's button that pins an auto-hidden pane back.</summary>
    public static Geometry Pin => s_pin ??= StreamGeometry.Parse(
        "M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z");

    /// <summary>Lucide <c>pin-off</c>: the menu's Auto hide row.</summary>
    public static Geometry PinOff => s_pinOff ??= StreamGeometry.Parse(
        "M12 17v5 M15 9.34V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H7.89 M2 2l20 20 M9 9v1.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h11");
}
