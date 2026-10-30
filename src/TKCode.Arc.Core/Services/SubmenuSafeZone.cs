using Avalonia;

namespace TKCode.Arc.Primitives;

/// <summary>
/// The pointer's way from a menu row to the submenu it opened. When the pointer leaves the row heading for the submenu it
/// crosses other rows on the diagonal; those must not take the highlight (and close the submenu). The zone is the
/// polygon from the point where the pointer left the row (moved <see cref="Bleed"/> back, so that point is surely inside)
/// to the submenu's near corners and around its far side. Pure geometry, shared by the menu surfaces and the model menu.
/// </summary>
/// <param name="Exit">Where the pointer left the row that opened the submenu.</param>
/// <param name="Submenu">The submenu's bounds, in the same coordinates as <paramref name="Exit"/>.</param>
/// <param name="SubmenuOnRight">True when the submenu sits right of the row (false: it opened to the left).</param>
public readonly record struct SubmenuSafeZone(Point Exit, Rect Submenu, bool SubmenuOnRight)
{
    /// <summary>How far the exit point moves back toward the menu (Radix's "bleed").</summary>
    public const double Bleed = 5;

    /// <summary>How long a zone holds once the pointer left the row: a pointer that rests on a sibling gets it after this.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMilliseconds(300);

    /// <summary>True when <paramref name="point"/> is inside the zone (the submenu itself included).</summary>
    public bool Contains(Point point)
    {
        var apex = new Point(Exit.X + (SubmenuOnRight ? -Bleed : Bleed), Exit.Y);
        var near = SubmenuOnRight ? Submenu.Left : Submenu.Right;
        var far = SubmenuOnRight ? Submenu.Right : Submenu.Left;
        Span<Point> polygon =
        [
            apex,
            new Point(near, Submenu.Top),
            new Point(far, Submenu.Top),
            new Point(far, Submenu.Bottom),
            new Point(near, Submenu.Bottom)
        ];
        return IsInPolygon(point, polygon);
    }

    /// <summary>True when a horizontal move of <paramref name="dx"/> heads toward the submenu (no horizontal move keeps <paramref name="lastToward"/>).</summary>
    public bool IsToward(double dx, bool lastToward) => dx == 0 ? lastToward : SubmenuOnRight ? dx > 0 : dx < 0;

    /// <summary>Even-odd ray casting (the polygon is convex here, any rule works).</summary>
    internal static bool IsInPolygon(Point point, ReadOnlySpan<Point> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if (a.Y > point.Y != b.Y > point.Y && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
