using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The pointer's way from a row to the submenu it opened. When the pointer leaves the row heading for the submenu it
/// crosses other rows on the diagonal; those must not take the highlight (and close the submenu). The zone is the
/// polygon from the point where the pointer left the row (moved 5px back, so that point is surely inside) to the
/// submenu's near corners and around its far side. Pure geometry so it can be tested on its own.
/// </summary>
internal readonly record struct ModelMenuSafeZone(Point Exit, Rect Submenu, bool SubmenuOnRight)
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

    /// <summary>True when a horizontal move of <paramref name="dx"/> heads toward the submenu (no horizontal move keeps the last direction).</summary>
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

/// <summary>
/// Menu typeahead: typed letters build a search that resets after a pause; the next row whose name starts with it gets
/// the highlight, searching from the current row onward and wrapping. Repeating one letter cycles through the rows that
/// start with it instead of searching for "aa". Pure logic with an injectable clock.
/// </summary>
internal sealed class ModelMenuTypeahead
{
    /// <summary>A pause this long starts a new search.</summary>
    public static readonly TimeSpan ResetAfter = TimeSpan.FromSeconds(1);

    private readonly Func<DateTime> _now;
    private string _buffer = string.Empty;
    private DateTime _last;

    public ModelMenuTypeahead(Func<DateTime>? now = null)
    {
        _now = now ?? (static () => DateTime.UtcNow);
    }

    /// <summary>The search typed so far (empty after a pause).</summary>
    public string Buffer => _buffer;

    /// <summary>Forgets the search (the menu closed or changed page).</summary>
    public void Reset() => _buffer = string.Empty;

    /// <summary>
    /// Adds <paramref name="text"/> to the search and returns the index of the row to highlight, or null when nothing
    /// matches (the highlight stays).
    /// </summary>
    /// <param name="text">The typed character(s).</param>
    /// <param name="names">The rows' names in order (null for a row that cannot be found by name).</param>
    /// <param name="current">The highlighted row, or −1.</param>
    public int? Next(string text, IReadOnlyList<string?> names, int current)
    {
        if (string.IsNullOrEmpty(text) || names.Count == 0)
        {
            return null;
        }
        var now = _now();
        _buffer = now - _last > ResetAfter ? text : _buffer + text;
        _last = now;
        var search = IsOneLetterRepeated(_buffer) ? _buffer[..1] : _buffer;
        // A one-letter search moves on from the current row; a longer one may stay on it while it still matches.
        var start = current < 0 ? 0 : search.Length == 1 ? current + 1 : current;
        for (var step = 0; step < names.Count; step++)
        {
            var index = (start + step) % names.Count;
            if (names[index] is { } name && name.StartsWith(search, StringComparison.CurrentCultureIgnoreCase))
            {
                return index;
            }
        }
        return null;
    }

    private static bool IsOneLetterRepeated(string text)
    {
        for (var i = 1; i < text.Length; i++)
        {
            if (char.ToUpperInvariant(text[i]) != char.ToUpperInvariant(text[0]))
            {
                return false;
            }
        }
        return true;
    }
}
