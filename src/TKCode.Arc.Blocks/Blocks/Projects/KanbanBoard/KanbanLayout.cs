using Avalonia;

namespace TKCode.Arc;

/// <summary>Column widths and board paddings for one container width (Arc Kanban board, measured).</summary>
/// <param name="ColumnWidth">Width of every column.</param>
/// <param name="Padding">Space left and right of the columns, and under the last row.</param>
/// <param name="IsCompact">True at or below the compact breakpoint: fixed narrow columns and sideways snapping.</param>
/// <param name="ColumnCount">Number of columns.</param>
internal readonly record struct KanbanMetrics(double ColumnWidth, double Padding, bool IsCompact, int ColumnCount)
{
    /// <summary>Left edge of column <paramref name="index"/> in board coordinates.</summary>
    public double ColumnX(int index) => Padding + index * (ColumnWidth + KanbanLayout.ColumnGap);

    /// <summary>Width of the columns and the gaps between them.</summary>
    public double ContentWidth => ColumnCount * ColumnWidth + Math.Max(0, ColumnCount - 1) * KanbanLayout.ColumnGap;

    /// <summary>Scrollable width: the columns plus the paddings on both sides.</summary>
    public double ExtentWidth => Padding * 2 + ContentWidth;

    /// <summary>Width of a card: the column minus the cell's border and padding on both sides.</summary>
    public double CardWidth => Math.Max(0, ColumnWidth - 2 * (KanbanLayout.CellBorder + KanbanLayout.CellPadding));

    /// <summary>Distance between two neighbouring snap points (a column and its gap).</summary>
    public double SnapStep => ColumnWidth + KanbanLayout.ColumnGap;
}

/// <summary>One laid out swimlane (in Status view there is one lane without a head).</summary>
/// <param name="Top">Top of the lane (its head; equals <paramref name="CellsTop"/> without a head).</param>
/// <param name="CellsTop">Top of the row of cells.</param>
/// <param name="CellsHeight">Height of the row of cells (all cells of a lane are equally tall).</param>
/// <param name="HasHead">True when the lane shows a head (swimlanes).</param>
internal readonly record struct KanbanLaneRect(double Top, double CellsTop, double CellsHeight, bool HasHead)
{
    /// <summary>Bottom of the lane's cells.</summary>
    public double Bottom => CellsTop + CellsHeight;

    /// <summary>The lane head's rect content area (36 high, above the cells).</summary>
    public double HeadHeight => HasHead ? KanbanLayout.LaneHeadHeight : 0;
}

/// <summary>The geometry of one cell: its rect, the top of each item inside and the trailing row (Add card or composer).</summary>
internal sealed class KanbanCellRect
{
    public KanbanCellRect(int lane, int column, int itemCount)
    {
        Lane = lane;
        Column = column;
        ItemTops = new double[itemCount];
        ItemHeights = new double[itemCount];
    }

    public int Lane { get; }

    public int Column { get; }

    /// <summary>The cell's outer rect (border included).</summary>
    public Rect Bounds { get; set; }

    /// <summary>Top of each item (cards, the slot) in board coordinates.</summary>
    public double[] ItemTops { get; }

    /// <summary>Height of each item.</summary>
    public double[] ItemHeights { get; }

    /// <summary>The trailing row (Add card, or the open composer), or null.</summary>
    public Rect? Tail { get; set; }

    /// <summary>Height of the cell's content (border and padding included), before stretching to the lane or viewport.</summary>
    public double ContentHeight { get; set; }

    /// <summary>Left edge of the items.</summary>
    public double ItemX => Bounds.X + KanbanLayout.CellBorder + KanbanLayout.CellPadding;
}

/// <summary>What the layout engine needs to know about one cell.</summary>
/// <param name="ItemHeights">Heights of the items in order (cards and, while dragging, the slot).</param>
/// <param name="TailHeight">Height of the trailing row; NaN for none (0 is an empty composer that still takes its gap).</param>
internal readonly record struct KanbanCellInput(IReadOnlyList<double> ItemHeights, double TailHeight)
{
    public bool HasTail => !double.IsNaN(TailHeight);
}

/// <summary>The result of <see cref="KanbanLayout.Layout"/>.</summary>
internal sealed class KanbanBoardLayout
{
    public KanbanBoardLayout(KanbanMetrics metrics, KanbanLaneRect[] lanes, KanbanCellRect[] cells, Size extent)
    {
        Metrics = metrics;
        Lanes = lanes;
        Cells = cells;
        Extent = extent;
    }

    public KanbanMetrics Metrics { get; }

    public KanbanLaneRect[] Lanes { get; }

    /// <summary>Cells in lane-major order: <c>Cells[lane * columns + column]</c>.</summary>
    public KanbanCellRect[] Cells { get; }

    /// <summary>The whole board (columns, lanes and paddings), at least the viewport.</summary>
    public Size Extent { get; }

    public KanbanCellRect Cell(int lane, int column) => Cells[lane * Metrics.ColumnCount + column];
}

/// <summary>
/// The Kanban board's geometry and hit rules as pure functions (Arc Kanban board, design reference, measured at 1440,
/// 1100, 900, 700 and 390): column widths, cells, swimlanes, which column / lane / position a pointer is over, the WIP
/// rule, the edge auto-scroll speed and the compact snap points. The panel and the tests share it.
/// </summary>
internal static class KanbanLayout
{
    /// <summary>Gap between columns.</summary>
    public const double ColumnGap = 12;

    /// <summary>Space left and right of the columns and under the board (wide), and in the compact width.</summary>
    public const double BoardPadding = 16;

    public const double CompactBoardPadding = 12;

    /// <summary>The sticky column head row: 2 above a 40 high head.</summary>
    public const double HeadRowHeight = 42;

    public const double HeadRowTopPadding = 2;

    public const double ColumnHeadHeight = 40;

    /// <summary>Gap between the head row and the first lane, and between lanes.</summary>
    public const double RowGap = 12;

    /// <summary>A swimlane's head (avatar, name, count) and the gap under it.</summary>
    public const double LaneHeadHeight = 36;

    public const double LaneHeadGap = 8;

    /// <summary>The shortest a swimlane cell gets.</summary>
    public const double LaneCellMinHeight = 96;

    /// <summary>A cell: 1px border, 8 padding, items 8 apart.</summary>
    public const double CellBorder = 1;

    public const double CellPadding = 8;

    public const double ItemGap = 8;

    /// <summary>The "Add card" row under a Status column's cards.</summary>
    public const double AddRowHeight = 34;

    /// <summary>A lane's hit range starts this far above its cells (the rest of its head is a dead zone).</summary>
    public const double LaneHitLead = 8;

    /// <summary>The auto-scroll edge zone and the per-frame speed at the very edge (60Hz frames).</summary>
    public const double AutoScrollEdge = 64;

    public const double AutoScrollMaxPerFrame = 20;

    /// <summary>Widths and paddings for a container <paramref name="width"/> wide.</summary>
    public static KanbanMetrics Measure(double width, int columns, double minColumnWidth = 251, double compactColumnWidth = 235,
                                        double compactBreakpoint = 560)
    {
        var count = Math.Max(0, columns);
        var compact = width > 0 && width <= compactBreakpoint;
        var padding = compact ? CompactBoardPadding : BoardPadding;
        double columnWidth;
        if (compact)
        {
            columnWidth = compactColumnWidth;
        }
        else
        {
            var n = Math.Max(1, count);
            var fill = (width - 2 * padding - (n - 1) * ColumnGap) / n;
            columnWidth = Math.Max(minColumnWidth, double.IsFinite(fill) ? fill : minColumnWidth);
        }
        return new KanbanMetrics(columnWidth, padding, compact, count);
    }

    /// <summary>
    /// Lays out the board: the head row, then one row of cells per lane (with a head per lane in swimlanes). In Status
    /// view the single row stretches to the bottom of the viewport; swimlane rows are as tall as their tallest cell, at
    /// least <see cref="LaneCellMinHeight"/>.
    /// </summary>
    /// <param name="metrics">From <see cref="Measure"/>.</param>
    /// <param name="viewport">The scroll viewport.</param>
    /// <param name="laneCount">Lanes (1 in Status view).</param>
    /// <param name="hasLaneHeads">True in swimlanes.</param>
    /// <param name="cells">Per cell, lane-major.</param>
    public static KanbanBoardLayout Layout(KanbanMetrics metrics, Size viewport, int laneCount, bool hasLaneHeads,
                                           IReadOnlyList<KanbanCellInput> cells)
    {
        var columns = metrics.ColumnCount;
        var lanes = new KanbanLaneRect[Math.Max(0, laneCount)];
        var rects = new KanbanCellRect[lanes.Length * columns];
        var y = HeadRowHeight;
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            y += RowGap;
            var top = y;
            if (hasLaneHeads)
            {
                y += LaneHeadHeight + LaneHeadGap;
            }
            var cellsTop = y;
            var rowHeight = hasLaneHeads ? LaneCellMinHeight : 0;
            for (var column = 0; column < columns; column++)
            {
                var input = cells[lane * columns + column];
                var rect = LayoutCell(lane, column, metrics.ColumnX(column), cellsTop, metrics.ColumnWidth, input);
                rects[lane * columns + column] = rect;
                rowHeight = Math.Max(rowHeight, rect.ContentHeight);
            }
            if (!hasLaneHeads)
            {
                // Status view: the row fills the viewport under the head row (Arc's 1fr row), growing past it with content.
                rowHeight = Math.Max(rowHeight, viewport.Height - HeadRowHeight - RowGap - metrics.Padding);
            }
            for (var column = 0; column < columns; column++)
            {
                var rect = rects[lane * columns + column];
                rect.Bounds = new Rect(rect.Bounds.X, cellsTop, metrics.ColumnWidth, rowHeight);
            }
            lanes[lane] = new KanbanLaneRect(top, cellsTop, rowHeight, hasLaneHeads);
            y = cellsTop + rowHeight;
        }
        var height = y + metrics.Padding;
        var extent = new Size(Math.Max(metrics.ExtentWidth, viewport.Width), Math.Max(height, viewport.Height));
        return new KanbanBoardLayout(metrics, lanes, rects, extent);
    }

    /// <summary>Stacks one cell's items and its trailing row from <paramref name="top"/>.</summary>
    public static KanbanCellRect LayoutCell(int lane, int column, double x, double top, double width, KanbanCellInput input)
    {
        var items = input.ItemHeights;
        var rect = new KanbanCellRect(lane, column, items.Count);
        var y = top + CellBorder + CellPadding;
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                y += ItemGap;
            }
            rect.ItemTops[i] = y;
            rect.ItemHeights[i] = items[i];
            y += items[i];
        }
        if (input.HasTail)
        {
            if (items.Count > 0)
            {
                y += ItemGap;
            }
            var tail = Math.Max(0, input.TailHeight);
            rect.Tail = new Rect(x + CellBorder + CellPadding, y, Math.Max(0, width - 2 * (CellBorder + CellPadding)), tail);
            y += tail;
        }
        y += CellPadding + CellBorder;
        rect.ContentHeight = y - top;
        rect.Bounds = new Rect(x, top, width, rect.ContentHeight);
        return rect;
    }

    /// <summary>
    /// The column under board x <paramref name="x"/>: columns meet in the middle of their gaps, and a pointer left of
    /// the first or right of the last column counts as that column (the height does not matter).
    /// </summary>
    public static int ColumnAt(in KanbanMetrics metrics, double x)
    {
        var count = metrics.ColumnCount;
        if (count <= 0)
        {
            return -1;
        }
        var column = 0;
        for (var i = 0; i < count - 1; i++)
        {
            var boundary = metrics.ColumnX(i) + metrics.ColumnWidth + ColumnGap / 2;
            if (x >= boundary)
            {
                column = i + 1;
            }
        }
        return column;
    }

    /// <summary>
    /// The swimlane under board y <paramref name="y"/>: a lane owns its cells, the gap under them and 8px above them;
    /// the rest of its head is a dead zone where the lane stays <paramref name="current"/>. Above the first lane is the
    /// first lane, below the last the last.
    /// </summary>
    public static int LaneAt(IReadOnlyList<KanbanLaneRect> lanes, double y, int current)
    {
        if (lanes.Count == 0)
        {
            return -1;
        }
        if (y < lanes[0].Top)
        {
            return 0;
        }
        for (var i = 0; i < lanes.Count; i++)
        {
            var hitTop = lanes[i].HasHead ? lanes[i].CellsTop - LaneHitLead : lanes[i].Top;
            var next = i + 1 < lanes.Count ? lanes[i + 1].Top : double.PositiveInfinity;
            if (y >= hitTop && y < next)
            {
                return i;
            }
            if (y >= lanes[i].Top && y < hitTop)
            {
                // The head above the hit range: keep the lane the pointer came from.
                return Math.Clamp(current, 0, lanes.Count - 1);
            }
        }
        return lanes.Count - 1;
    }

    /// <summary>
    /// The insertion index for board y <paramref name="y"/> in a cell: the number of the cell's other cards whose middle
    /// is above the pointer. The cards are where the current layout put them (with the slot already open), so a card
    /// has to be passed by its full height before the slot moves back: built-in hysteresis.
    /// </summary>
    /// <param name="midlines">The vertical middles of the cell's other cards, top to bottom.</param>
    /// <param name="y">The pointer's board y.</param>
    public static int IndexAt(IReadOnlyList<double> midlines, double y)
    {
        var index = 0;
        for (var i = 0; i < midlines.Count; i++)
        {
            if (midlines[i] < y)
            {
                index = i + 1;
            }
            else
            {
                break;
            }
        }
        return index;
    }

    /// <summary>The WIP rule: a column is over its limit only when it holds more cards than the limit.</summary>
    public static bool IsOverLimit(int count, int? limit) => limit is { } l && count > l;

    /// <summary>
    /// Auto-scroll speed at <paramref name="distance"/> px from an edge, in px per 60Hz frame: ceil(20·((64 − d)/64)²)
    /// inside the 64px zone (outside the viewport counts as the edge), 0 outside it.
    /// </summary>
    public static double AutoScrollPerFrame(double distance)
    {
        if (double.IsNaN(distance) || distance >= AutoScrollEdge)
        {
            return 0;
        }
        var d = Math.Max(0, distance);
        var t = (AutoScrollEdge - d) / AutoScrollEdge;
        return Math.Ceiling(AutoScrollMaxPerFrame * t * t);
    }

    /// <summary>The auto-scroll speed in px/s (Arc steps once per 60Hz frame; we integrate over real frame time).</summary>
    public static double AutoScrollSpeed(double distance) => AutoScrollPerFrame(distance) * 60;

    /// <summary>
    /// The scroll velocity (px/s) along one axis for a pointer at <paramref name="position"/> in a viewport
    /// <paramref name="length"/> long: negative toward the start edge, positive toward the end edge, 0 outside the zones.
    /// </summary>
    public static double AutoScrollVelocity(double position, double length)
    {
        if (!(length > 0))
        {
            return 0;
        }
        var toStart = position;
        var toEnd = length - position;
        var back = AutoScrollSpeed(toStart);
        var forward = AutoScrollSpeed(toEnd);
        if (back > 0 && forward > 0)
        {
            // A viewport shorter than two zones: the nearer edge wins.
            return toStart <= toEnd ? -back : forward;
        }
        return forward > 0 ? forward : -back;
    }

    /// <summary>
    /// The compact width's horizontal snap points: every column's left edge flush with the viewport's left edge (the
    /// first one leaves the 12px padding out of view), clamped to the scroll range.
    /// </summary>
    public static IReadOnlyList<double> SnapPoints(in KanbanMetrics metrics, double maxOffset)
    {
        var points = new List<double>(metrics.ColumnCount);
        for (var i = 0; i < metrics.ColumnCount; i++)
        {
            var point = Math.Min(metrics.ColumnX(i), Math.Max(0, maxOffset));
            if (points.Count == 0 || point > points[^1] + 0.5)
            {
                points.Add(point);
            }
        }
        return points;
    }

    /// <summary>The snap point nearest to <paramref name="offset"/> (proximity snapping, as Arc's phone layout).</summary>
    public static double NearestSnap(IReadOnlyList<double> points, double offset)
    {
        if (points.Count == 0)
        {
            return offset;
        }
        var best = points[0];
        foreach (var point in points)
        {
            if (Math.Abs(point - offset) < Math.Abs(best - offset))
            {
                best = point;
            }
        }
        return best;
    }

    /// <summary>
    /// Where a card dropped at position <paramref name="index"/> of a cell goes in the board's flat list (which does not
    /// contain it): before the card it lands above, after the cell's last card, or, in an empty cell, back where it was.
    /// </summary>
    /// <param name="flat">The board order without the moved card.</param>
    /// <param name="cellCards">The target cell's cards in order, without the moved card.</param>
    /// <param name="index">The position in the cell.</param>
    /// <param name="originalIndex">The card's index in the board order before it was taken out.</param>
    public static int FlatInsertionIndex<T>(IReadOnlyList<T> flat, IReadOnlyList<T> cellCards, int index, int originalIndex)
        where T : class
    {
        if (cellCards.Count == 0)
        {
            return Math.Clamp(originalIndex, 0, flat.Count);
        }
        if (index < cellCards.Count)
        {
            var before = IndexOf(flat, cellCards[Math.Max(0, index)]);
            return before < 0 ? flat.Count : before;
        }
        var last = IndexOf(flat, cellCards[^1]);
        return last < 0 ? flat.Count : last + 1;
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
            {
                return i;
            }
        }
        return -1;
    }
}
