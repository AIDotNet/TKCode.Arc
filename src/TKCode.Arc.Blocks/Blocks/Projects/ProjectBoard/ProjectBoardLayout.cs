using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The columns of a <see cref="ProjectBoard"/> at one width: how wide each is, where it starts and whether they scroll.
/// All values are in the board panel's coordinates (the panel includes the side padding).
/// </summary>
internal readonly record struct ProjectBoardGrid(int Count, double ColumnWidth, double Gap, double Padding, double ViewportWidth)
{
    /// <summary>The width of the columns and the gaps between them.</summary>
    public double GridWidth => Count <= 0 ? 0 : Count * ColumnWidth + (Count - 1) * Gap;

    /// <summary>The panel width: padding, columns, padding.</summary>
    public double ContentWidth => 2 * Padding + GridWidth;

    /// <summary>True when the columns are wider than the viewport and scroll sideways.</summary>
    public bool Scrolls => ContentWidth > ViewportWidth + 0.5;

    /// <summary>The farthest the viewport can scroll.</summary>
    public double MaxOffset => Math.Max(0, ContentWidth - ViewportWidth);

    /// <summary>The left edge of column <paramref name="index"/>.</summary>
    public double ColumnX(int index) => Padding + index * (ColumnWidth + Gap);

    /// <summary>The scroll offset that puts column <paramref name="index"/> at the viewport's start (after the padding), clamped.</summary>
    public double SnapPoint(int index) => Math.Clamp(index * (ColumnWidth + Gap), 0, MaxOffset);
}

/// <summary>
/// The measured layout rules of the Project board (Arc design reference): equal columns of at least 200 with 10 between
/// them inside 20 of padding; at 700 and below 16 of padding and columns 44 narrower than the content, so the next one
/// peeks in; slots of at least 320 with 6 of padding and 6 between cards, under a 28 high column heading.
/// </summary>
internal static class ProjectBoardLayout
{
    public const double Gap = 10;
    public const double WidePadding = 20;
    public const double NarrowPadding = 16;
    public const double NarrowPeek = 44;
    public const double MinNarrowColumnWidth = 120;
    public const double HeaderHeight = 28;
    public const double SlotPadding = 6;
    public const double CardGap = 6;
    public const double SlotMinHeight = 320;
    public const double SlotRadius = 16;

    /// <summary>Tabular figures for counts and due labels (Arc's <c>font-variant-numeric: tabular-nums</c>).</summary>
    public static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    /// <summary>The columns for a viewport <paramref name="viewportWidth"/> wide.</summary>
    /// <param name="viewportWidth">The width the columns scroll in (the board's inner width).</param>
    /// <param name="count">The number of stages.</param>
    /// <param name="narrow">True at the narrow breakpoint (700 and below).</param>
    /// <param name="minColumnWidth">The narrowest column of the wide layout (200).</param>
    public static ProjectBoardGrid Columns(double viewportWidth, int count, bool narrow, double minColumnWidth)
    {
        var padding = narrow ? NarrowPadding : WidePadding;
        if (count <= 0 || !double.IsFinite(viewportWidth))
        {
            var fallback = Math.Max(1, minColumnWidth);
            return new ProjectBoardGrid(Math.Max(0, count), fallback, Gap, padding, double.IsFinite(viewportWidth) ? viewportWidth : 0);
        }
        var content = Math.Max(0, viewportWidth - 2 * padding);
        double width;
        if (narrow)
        {
            width = Math.Max(MinNarrowColumnWidth, content - NarrowPeek);
        }
        else
        {
            width = Math.Max(Math.Max(1, minColumnWidth), (content - (count - 1) * Gap) / count);
        }
        return new ProjectBoardGrid(count, width, Gap, padding, viewportWidth);
    }

    /// <summary>The slot height for cards of the given heights (at least 320).</summary>
    public static double SlotHeight(IReadOnlyList<double> cardHeights)
    {
        if (cardHeights.Count == 0)
        {
            return SlotMinHeight;
        }
        var total = 2 * SlotPadding + (cardHeights.Count - 1) * CardGap;
        foreach (var h in cardHeights)
        {
            total += h;
        }
        return Math.Max(SlotMinHeight, total);
    }

    /// <summary>The distinct snap offsets of every column start (clamped to the scroll range), ascending.</summary>
    public static List<double> SnapPoints(ProjectBoardGrid grid)
    {
        var points = new List<double>(grid.Count);
        for (var i = 0; i < grid.Count; i++)
        {
            var p = grid.SnapPoint(i);
            if (points.Count == 0 || Math.Abs(points[^1] - p) > 0.5)
            {
                points.Add(p);
            }
        }
        return points;
    }
}
