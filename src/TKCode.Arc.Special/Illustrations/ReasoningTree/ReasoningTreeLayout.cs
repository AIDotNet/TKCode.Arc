using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of the <see cref="ReasoningTree"/> stage as pure functions (Arc Reasoning tree, design reference). Two
/// layouts, chosen by the stage's own width:
/// <list type="bullet">
/// <item>Wide (620px and up): four columns (the question, then depths 1 to 3) under 18px headers. The question takes 19%
/// of the width, each depth 22.5%, with 4.5% gaps. Thoughts are 54 high on a 62 pitch; the question card is 84 high and
/// centred in the content area. The stage height fits the tallest column of any beam width, so changing the beam never
/// resizes the card.</item>
/// <item>Narrow: a vertical outline in depth-first order. The question row is 62 high; thought rows are 44 high on a 50
/// pitch, indented 16px per depth. The stage reserves the rows of the current beam before the search starts.</item>
/// </list>
/// </summary>
internal static class ReasoningTreeLayout
{
    /// <summary>Stage widths from this up use the wide layout.</summary>
    public const double WideBreakpoint = 620;

    /// <summary>Column headers: 18 high at the top of the wide stage.</summary>
    public const double HeaderHeight = 18;

    /// <summary>Where the wide content area starts (header 18 + 12).</summary>
    public const double ContentTop = 30;

    /// <summary>A wide thought card's height.</summary>
    public const double ThoughtHeight = 54;

    /// <summary>The wide question card's height.</summary>
    public const double RootHeight = 84;

    /// <summary>Distance between wide rows (54 + 8).</summary>
    public const double Pitch = 62;

    /// <summary>Column shares of the stage width.</summary>
    public const double QuestionShare = 0.19;

    public const double DepthShare = 0.225;

    public const double GapShare = 0.045;

    /// <summary>The narrow question row's height.</summary>
    public const double NarrowRootHeight = 62;

    /// <summary>A narrow thought row's height.</summary>
    public const double NarrowRowHeight = 44;

    /// <summary>Distance between narrow rows (44 + 6).</summary>
    public const double NarrowPitch = 50;

    /// <summary>The first narrow thought row's top (62 + 6).</summary>
    public const double NarrowFirstRow = 68;

    /// <summary>Indent per depth of the outline.</summary>
    public const double NarrowIndent = 16;

    /// <summary>The elbow edges' vertical runs sit this far into the parent row.</summary>
    public const double ElbowInset = 9;

    /// <summary>The elbow's corner radius.</summary>
    public const double ElbowRadius = 4.5;

    private const double Epsilon = 0.001;

    /// <summary>True when a stage of <paramref name="width"/> uses the wide layout.</summary>
    public static bool IsWide(double width) => width >= WideBreakpoint;

    /// <summary>The x and width of a column (0 = question, 1 to 3 = depths) on a wide stage.</summary>
    public static (double X, double Width) Column(int depth, double width)
    {
        if (depth <= 0)
        {
            return (0, QuestionShare * width);
        }
        var x = QuestionShare * width + depth * GapShare * width + (depth - 1) * DepthShare * width;
        return (x, DepthShare * width);
    }

    /// <summary>
    /// The wide stage height: room for the tallest column the search shows at any beam width (and at least the question
    /// card): <c>30 + rows · 62 − 8</c>.
    /// </summary>
    public static double WideHeight(ReasoningTreeModel model, IEnumerable<ReasoningTreeResult> results)
    {
        var rows = 0;
        foreach (var result in results)
        {
            for (var depth = 1; depth <= ReasoningTreeModel.MaxDepth; depth++)
            {
                var count = 0;
                foreach (var node in model.Nodes)
                {
                    if (node.Depth == depth && result.IsShown(node.Index))
                    {
                        count++;
                    }
                }
                rows = Math.Max(rows, count);
            }
        }
        return Math.Max(ContentTop + rows * Pitch - (Pitch - ThoughtHeight), ContentTop + RootHeight);
    }

    /// <summary>
    /// The wide layout of one search: a rectangle per node (index as in the model). The question is centred in the
    /// content area. Each parent's shown children stack on the pitch, centred on the parent. A column whose stacks
    /// collide is pushed apart top-down (and kept below the content top), then pulled back bottom-up (and kept above the
    /// stage bottom); when a column moved, the parents one column to the left (and further left while they move) are
    /// centred again on their children unless that would bring them within a pitch of a neighbour. Thoughts this search
    /// never shows get their ideal place next to their parent and take no part in the collisions.
    /// </summary>
    public static Rect[] Wide(ReasoningTreeModel model, ReasoningTreeResult result, double width, double height)
    {
        var nodes = model.Nodes;
        var y = new double[nodes.Count];
        var rects = new Rect[nodes.Count];
        var root = Column(0, width);
        var contentCenter = (ContentTop + height) / 2;
        y[0] = contentCenter - RootHeight / 2;
        for (var depth = 1; depth <= ReasoningTreeModel.MaxDepth; depth++)
        {
            // Ideal places: each parent's children stacked around its centre.
            foreach (var parent in nodes)
            {
                if (parent.Depth != depth - 1 || parent.Children.Count == 0)
                {
                    continue;
                }
                var center = y[parent.Index] + HeightOf(parent) / 2;
                var shownChildren = parent.Children.Where(result.IsShown).ToList();
                for (var i = 0; i < shownChildren.Count; i++)
                {
                    y[shownChildren[i]] = center + (i - (shownChildren.Count - 1) / 2.0) * Pitch - ThoughtHeight / 2;
                }
                var hidden = parent.Children.Where(c => !result.IsShown(c)).ToList();
                for (var i = 0; i < hidden.Count; i++)
                {
                    y[hidden[i]] = center + (i - (hidden.Count - 1) / 2.0) * Pitch - ThoughtHeight / 2;
                }
            }
            var column = ShownColumn(model, result, y, depth);
            if (column.Count == 0)
            {
                continue;
            }
            var before = column.Select(i => y[i]).ToArray();
            var top = ContentTop;
            var bottom = height - ThoughtHeight;
            for (var i = 0; i < column.Count; i++)
            {
                var floor = i == 0 ? top : Math.Max(top, y[column[i - 1]] + Pitch);
                y[column[i]] = Math.Max(y[column[i]], floor);
            }
            for (var i = column.Count - 1; i >= 0; i--)
            {
                var ceiling = i == column.Count - 1 ? bottom : Math.Min(bottom, y[column[i + 1]] - Pitch);
                y[column[i]] = Math.Min(y[column[i]], ceiling);
            }
            var moved = false;
            for (var i = 0; i < column.Count; i++)
            {
                moved |= Math.Abs(before[i] - y[column[i]]) > Epsilon;
            }
            if (!moved)
            {
                continue;
            }
            for (var level = depth - 1; level >= 1; level--)
            {
                if (!Recenter(model, result, y, level, height))
                {
                    break;
                }
            }
        }
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var (x, w) = node.Depth == 0 ? root : Column(node.Depth, width);
            rects[i] = new Rect(x, y[i], w, HeightOf(node));
        }
        return rects;
    }

    /// <summary>The wide edge from a parent's right middle to a child's left middle: a horizontal S curve.</summary>
    public static void WideEdge(ExplainerPath path, Rect parent, Rect child)
    {
        var from = new Point(parent.Right, parent.Center.Y);
        var to = new Point(child.Left, child.Center.Y);
        var mid = (from.X + to.X) / 2;
        path.Clear().MoveTo(from).CubicTo(new Point(mid, from.Y), new Point(mid, to.Y), to, 24);
    }

    /// <summary>
    /// The wide best path: one line from the question's right edge through every node on the path (running under each
    /// card from its left to its right edge) to the answer's left edge.
    /// </summary>
    public static void WidePath(ExplainerPath path, IReadOnlyList<Rect> chain)
    {
        path.Clear();
        if (chain.Count < 2)
        {
            return;
        }
        path.MoveTo(new Point(chain[0].Right, chain[0].Center.Y));
        for (var i = 1; i < chain.Count; i++)
        {
            var from = new Point(chain[i - 1].Right, chain[i - 1].Center.Y);
            var to = new Point(chain[i].Left, chain[i].Center.Y);
            var mid = (from.X + to.X) / 2;
            path.CubicTo(new Point(mid, from.Y), new Point(mid, to.Y), to, 24);
            if (i < chain.Count - 1)
            {
                path.LineTo(new Point(chain[i].Right, chain[i].Center.Y));
            }
        }
    }

    /// <summary>The narrow stage height for one search: the question row plus a row per thought it shows.</summary>
    public static double NarrowHeight(ReasoningTreeResult result) =>
        NarrowRootHeight + NarrowPitch * result.ShownThoughtCount;

    /// <summary>The outline order of the thoughts a search shows: depth first, a parent before its subtree (question first).</summary>
    public static int[] OutlineOrder(ReasoningTreeModel model, ReasoningTreeResult result)
    {
        var order = new List<int>();
        Visit(model, result, 0, order);
        return order.ToArray();
    }

    /// <summary>
    /// The narrow rectangle of a node: the question across the top; a thought on row <paramref name="row"/> (0 = the
    /// first row under the question), indented by its depth.
    /// </summary>
    public static Rect NarrowRect(ReasoningTreeNodeData node, int row, double width)
    {
        if (node.Depth == 0)
        {
            return new Rect(0, 0, width, NarrowRootHeight);
        }
        var x = NarrowIndent * node.Depth;
        return new Rect(x, NarrowFirstRow + row * NarrowPitch, Math.Max(0, width - x), NarrowRowHeight);
    }

    /// <summary>
    /// The narrow best path: down the question's elbow to the first node on the path, under each card to its own elbow,
    /// and so on to the answer's left edge.
    /// </summary>
    public static void NarrowPath(ExplainerPath path, IReadOnlyList<Rect> chain)
    {
        path.Clear();
        if (chain.Count < 2)
        {
            return;
        }
        var r = ElbowRadius;
        path.MoveTo(new Point(chain[0].X + ElbowInset, chain[0].Bottom));
        for (var i = 1; i < chain.Count; i++)
        {
            var parent = chain[i - 1];
            var child = chain[i];
            var x = parent.X + ElbowInset;
            var cy = child.Center.Y;
            // From the previous card's middle (or the question's bottom) down its elbow column, under the card.
            path.LineTo(new Point(x, cy - r));
            path.ArcTo(new Point(x + r, cy - r), r, 270, -90, 15);
            path.LineTo(new Point(child.X, cy));
            if (i < chain.Count - 1)
            {
                path.LineTo(new Point(child.X + ElbowInset, cy));
            }
        }
    }

    private static double HeightOf(ReasoningTreeNodeData node) => node.Depth == 0 ? RootHeight : ThoughtHeight;

    private static List<int> ShownColumn(ReasoningTreeModel model, ReasoningTreeResult result, double[] y, int depth)
    {
        var column = new List<int>();
        foreach (var node in model.Nodes)
        {
            if (node.Depth == depth && result.IsShown(node.Index))
            {
                column.Add(node.Index);
            }
        }
        // Top to bottom; equal places keep document order.
        column.Sort((a, b) =>
        {
            var byY = y[a].CompareTo(y[b]);
            return byY != 0 ? byY : a.CompareTo(b);
        });
        return column;
    }

    private static bool Recenter(ReasoningTreeModel model, ReasoningTreeResult result, double[] y, int level, double height)
    {
        var column = ShownColumn(model, result, y, level);
        var moved = false;
        for (var i = 0; i < column.Count; i++)
        {
            var node = model.Nodes[column[i]];
            var children = node.Children.Where(result.IsShown).ToList();
            if (children.Count == 0)
            {
                continue;
            }
            var center = children.Average(c => y[c] + ThoughtHeight / 2);
            var target = center - ThoughtHeight / 2;
            if (Math.Abs(target - y[node.Index]) <= Epsilon)
            {
                continue;
            }
            var fits = target >= ContentTop - Epsilon && target <= height - ThoughtHeight + Epsilon;
            if (i > 0)
            {
                fits &= target >= y[column[i - 1]] + Pitch - Epsilon;
            }
            if (i < column.Count - 1)
            {
                fits &= target <= y[column[i + 1]] - Pitch + Epsilon;
            }
            if (fits)
            {
                y[node.Index] = target;
                moved = true;
            }
        }
        return moved;
    }

    private static void Visit(ReasoningTreeModel model, ReasoningTreeResult result, int index, List<int> order)
    {
        if (!result.IsShown(index))
        {
            return;
        }
        order.Add(index);
        foreach (var child in model.Nodes[index].Children)
        {
            Visit(model, result, child, order);
        }
    }
}
