using Avalonia;

namespace TKCode.Arc;

/// <summary>How the card arranges its ring and its context stack at a width.</summary>
internal enum AgentLoopArrangement
{
    /// <summary>The context stack in a 264px column beside the ring (from a 640px container).</summary>
    Wide,

    /// <summary>The context stack under the ring, blocks in two columns (460 to 640px).</summary>
    StackedTwoColumns,

    /// <summary>The context stack under the ring, one block per row (below 460px).</summary>
    StackedOneColumn
}

/// <summary>
/// The agent loop's layout rules as pure functions of the measured widths (all in px): the breakpoints of the card, the
/// ring's radius and station angles, and the positions of the nodes, labels, pins and lanes. The ring is laid out in real
/// pixels (strokes and type never scale): the radius is the largest, up to 94, that leaves room for the lower station
/// labels beside their nodes, and on the narrowest columns the lower stations move further down the ring.
/// </summary>
internal sealed class AgentLoopGeometry
{
    /// <summary>The largest ring radius.</summary>
    public const double MaxRadius = 94;

    /// <summary>The smallest ring radius (labels are cut instead below it).</summary>
    public const double MinRadius = 36;

    /// <summary>A radius below this at 120° moves the lower stations to 140° / 220°.</summary>
    public const double LowRadius = 67;

    /// <summary>The container width from which the context stack sits beside the ring.</summary>
    public const double WideBreakpoint = 640;

    /// <summary>The container width from which stacked context blocks pair up in two columns.</summary>
    public const double TwoColumnBreakpoint = 460;

    /// <summary>The container width below which the "Speed" words hide.</summary>
    public const double SpeedLabelBreakpoint = 400;

    /// <summary>The ring column width below which labels drop their icons and shrink and the two lanes merge into one.</summary>
    public const double CompactBreakpoint = 360;

    /// <summary>The ring column width below which the lower stations sit at 140° / 220°.</summary>
    public const double LowStationsBreakpoint = 270;

    /// <summary>The context column beside the ring (border and padding included).</summary>
    public const double ContextColumnWidth = 264;

    /// <summary>The gap between the ring column and the context column.</summary>
    public const double ColumnGap = 24;

    /// <summary>The context column's padding after its left border.</summary>
    public const double ContextInset = 24;

    /// <summary>The height of the context heading row.</summary>
    public const double ContextHeadingHeight = 19.8;

    /// <summary>The space between the heading and the blocks.</summary>
    public const double ContextHeadingGap = 12;

    /// <summary>The height of a context block, a lane and a chip.</summary>
    public const double RowHeight = 56;

    /// <summary>The space between blocks (rows and columns) and between the two lanes.</summary>
    public const double BlockGap = 8;

    /// <summary>The space between the two lanes.</summary>
    public const double LaneGap = 12;

    /// <summary>The space between the ring area and the lanes.</summary>
    public const double LaneTopGap = 14;

    /// <summary>The space between the ring column and the stacked context (above its border).</summary>
    public const double StackGap = 20;

    /// <summary>The stacked context's padding under its top border.</summary>
    public const double StackInset = 20;

    private AgentLoopGeometry()
    {
    }

    /// <summary>The ring column's width.</summary>
    public double Width { get; private init; }

    /// <summary>True below the compact breakpoint: 12px labels without icons, one lane.</summary>
    public bool IsCompact { get; private init; }

    /// <summary>The ring's radius.</summary>
    public double Radius { get; private init; }

    /// <summary>The clock angle of Take action (120° or 140°); Verify mirrors it.</summary>
    public double LowerAngle { get; private init; }

    /// <summary>The room above the ring for the Gather label (34, or 30 when compact).</summary>
    public double Top { get; private init; }

    /// <summary>The space between a node and its label (14, or 13 when compact).</summary>
    public double LabelGap { get; private init; }

    /// <summary>The ring's center.</summary>
    public Point Center { get; private init; }

    /// <summary>The height of the ring area (labels and ring).</summary>
    public double RingHeight => Top + 2 * Radius + 6;

    /// <summary>The top of the lanes, where the pins end.</summary>
    public double LaneTop => RingHeight + LaneTopGap;

    /// <summary>The height of the whole ring column (ring area, gap, lanes).</summary>
    public double ColumnHeight => LaneTop + RowHeight;

    /// <summary>The clock angle of a station.</summary>
    public double AngleOf(AgentLoopStation station) => station switch
    {
        AgentLoopStation.Act => LowerAngle,
        AgentLoopStation.Verify => 360 - LowerAngle,
        _ => 0
    };

    /// <summary>The center of a station's node.</summary>
    public Point NodeOf(AgentLoopStation station) => ExplainerPath.OnCircle(Center, Radius, AngleOf(station));

    /// <summary>The clock angles of the three direction chevrons, halfway along each arc.</summary>
    public (double First, double Second, double Third) ChevronAngles => (LowerAngle / 2, 180, 360 - LowerAngle / 2);

    /// <summary>The pin under a lower station: from just under its node to the top of the lanes.</summary>
    public (Point From, Point To) PinOf(AgentLoopStation station)
    {
        var node = NodeOf(station);
        return (new Point(node.X, node.Y + 9), new Point(node.X, LaneTop));
    }

    /// <summary>
    /// The room a lower station's label has beside its node, to the edge of the column (Take action to the right,
    /// Verify to the left).
    /// </summary>
    public double LabelRoom(AgentLoopStation station)
    {
        var node = NodeOf(station);
        return station == AgentLoopStation.Verify ? Math.Max(0, node.X - LabelGap) : Math.Max(0, Width - node.X - LabelGap);
    }

    /// <summary>The lane rectangles (x relative to the column, y from its top): check lane, tool call lane; one merged lane when compact.</summary>
    public (Rect Checks, Rect Calls) Lanes
    {
        get
        {
            if (IsCompact)
            {
                var lane = new Rect(0, LaneTop, Width, RowHeight);
                return (lane, lane);
            }
            var half = Math.Max(0, (Width - LaneGap) / 2);
            return (new Rect(0, LaneTop, half, RowHeight), new Rect(half + LaneGap, LaneTop, half, RowHeight));
        }
    }

    /// <summary>
    /// Solves the ring for a column <paramref name="width"/> wide whose lower labels measure
    /// <paramref name="actLabelWidth"/> and <paramref name="verifyLabelWidth"/> (icon included). The radius is the
    /// largest up to 94 that keeps the wider lower label inside the column (with 12px to spare beside the icons);
    /// below a 67px radius at 120° (or a 270px column) the lower stations move to 140° / 220° and the radius is solved
    /// again.
    /// </summary>
    public static AgentLoopGeometry Solve(double width, double actLabelWidth, double verifyLabelWidth)
    {
        width = Math.Max(0, width);
        var compact = IsCompactColumn(width);
        var top = compact ? 30 : 34;
        var gap = compact ? 13 : 14;
        var label = Math.Max(Math.Max(actLabelWidth, verifyLabelWidth), 0);
        var radius = RadiusFor(width, label, 120, compact);
        var angle = 120.0;
        if (width < LowStationsBreakpoint || radius < LowRadius)
        {
            angle = 140;
            radius = RadiusFor(width, label, angle, compact);
        }
        radius = Math.Clamp(radius, MinRadius, MaxRadius);
        return new AgentLoopGeometry
        {
            Width = width,
            IsCompact = compact,
            Radius = radius,
            LowerAngle = angle,
            Top = top,
            LabelGap = gap,
            Center = new Point(width / 2, top + radius)
        };
    }

    /// <summary>The radius that fits a lower label <paramref name="label"/> px wide at clock angle <paramref name="degrees"/>.</summary>
    public static double RadiusFor(double width, double label, double degrees, bool compact)
    {
        var sin = Math.Sin(degrees * Math.PI / 180);
        if (compact)
        {
            return Math.Min(MaxRadius, Math.Floor((width / 2 - 13 - label) / sin));
        }
        return Math.Min(MaxRadius, Math.Round((width / 2 - 14 - label - 12) / sin, MidpointRounding.AwayFromZero));
    }

    /// <summary>True for a ring column narrower than 360px.</summary>
    public static bool IsCompactColumn(double ringWidth) => ringWidth < CompactBreakpoint;

    /// <summary>How the card arranges itself in a container <paramref name="width"/> wide (the card's content width).</summary>
    public static AgentLoopArrangement ArrangementFor(double width) => width >= WideBreakpoint
        ? AgentLoopArrangement.Wide
        : width >= TwoColumnBreakpoint ? AgentLoopArrangement.StackedTwoColumns : AgentLoopArrangement.StackedOneColumn;

    /// <summary>The ring column's width in a container <paramref name="width"/> wide.</summary>
    public static double RingColumnWidth(double width) => ArrangementFor(width) == AgentLoopArrangement.Wide
        ? Math.Max(0, width - ContextColumnWidth - ColumnGap)
        : Math.Max(0, width);

    /// <summary>True when the "Speed" words are shown beside the speed control (from a 400px container).</summary>
    public static bool ShowsSpeedLabel(double width) => width >= SpeedLabelBreakpoint;

    /// <summary>The card's padding at a card <paramref name="cardWidth"/> wide: 24, easing down to 16 on phones.</summary>
    public static double CardPadding(double cardWidth) => Math.Clamp(0.04 * (cardWidth + 48), 16, 24);

    /// <summary>The rows of context blocks for <paramref name="slots"/> loops (every loop's slot is reserved).</summary>
    public static int ContextRows(int slots, AgentLoopArrangement arrangement)
    {
        slots = Math.Max(1, slots);
        return arrangement == AgentLoopArrangement.StackedTwoColumns ? (slots + 1) / 2 : slots;
    }

    /// <summary>The height of the block area for <paramref name="rows"/> rows.</summary>
    public static double BlocksHeight(int rows) => rows * RowHeight + Math.Max(0, rows - 1) * BlockGap;

    /// <summary>The height of the context stack (heading, gap, blocks).</summary>
    public static double ContextHeight(int rows) => ContextHeadingHeight + ContextHeadingGap + BlocksHeight(rows);
}
