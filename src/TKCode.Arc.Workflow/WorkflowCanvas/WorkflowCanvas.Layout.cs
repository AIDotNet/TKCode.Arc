using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    /// <summary>Steps whose position the canvas chose; your code placing a step takes it out.</summary>
    private readonly HashSet<WorkflowNode> _autoPlaced = new();

    /// <summary>Where moving cards start from while arranging animates.</summary>
    private readonly Dictionary<WorkflowNode, Point> _moveFrom = new();

    private MotionValue<double>? _move;
    private double _moveSpan = 1;
    private bool _moveOnSpring;

    /// <summary>Below this speed (px/s, half a pixel a frame) a card within half a pixel of its place has arrived.</summary>
    private const double SettledSpeed = 30;

    private bool _placing;
    private bool _narrow;

    /// <summary>The steps were arranged before the canvas was in a window, with cards that had no size yet.</summary>
    private bool _relayoutPending;

    /// <summary>Whether the canvas is in its narrow layout (tests).</summary>
    internal bool IsNarrow => _narrow;

    /// <summary>
    /// Arranges every step in ranks along <see cref="LayoutDirection"/> (Arc's spacing: 52 between ranks, 72 below a
    /// fan-out, 20 between neighbours; narrow 26, 36 and 10) and writes the places to the steps'
    /// <see cref="WorkflowNode.Position"/>. With <paramref name="animate"/> the cards glide to their new places (full
    /// motion), the edges following.
    /// </summary>
    public void ArrangeNodes(bool animate = true)
    {
        if (Graph is { } graph)
        {
            Place(graph.Nodes.ToList(), default, animate);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var relayout = UpdateNarrow(availableSize.Width) || _relayoutPending;
        _relayoutPending = false;
        base.MeasureOverride(availableSize);
        if (relayout && Graph is { } graph && graph.Nodes.Count > 0 && AllAutoPlaced(graph))
        {
            // Narrow and wide workflows the canvas arranged are arranged again for their width (Arc's two layouts), and
            // an arrangement made before the cards had their sizes is made again now that they have them.
            Place(graph.Nodes.ToList(), default, animate: false);
        }
        PlaceUnplaced();
        var content = ContentBounds();
        var padding = Padding;
        var width = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : content.Width * (AutoFit == WorkflowAutoFit.None ? Zoom : 1) + padding.Left + padding.Right;
        double height;
        if (double.IsFinite(availableSize.Height))
        {
            height = availableSize.Height;
        }
        else
        {
            // Unlimited height: as tall as the workflow at the zoom the width allows (Arc: the stage is the scaled graph's height).
            var zoom = AutoFit == WorkflowAutoFit.None || _manualView
                ? Zoom
                : FitView(AutoFit, new Size(width, double.PositiveInfinity)).Zoom;
            height = content.Height * zoom + padding.Top + padding.Bottom;
        }
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateEdges();
        var arranged = base.ArrangeOverride(finalSize);
        if (IsViewAnimating)
        {
            // A fit or zoom is on its way; it ends where the next arrange would put the view.
        }
        else if (!_manualView && AutoFit != WorkflowAutoFit.None)
        {
            ApplyView(FitView(AutoFit, ViewportSize));
        }
        else
        {
            ApplyView(_view);
        }
        return arranged;
    }

    /// <summary>Switches the narrow layout on or off for a canvas <paramref name="width"/> wide; true when it changed.</summary>
    private bool UpdateNarrow(double width)
    {
        var narrow = double.IsFinite(width) && width < NarrowBelowWidth;
        if (narrow == _narrow)
        {
            return false;
        }
        _narrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        foreach (var card in _cards.Values)
        {
            card.SetNarrow(narrow);
        }
        return true;
    }

    /// <summary>True when the canvas chose every step's place (or still has to).</summary>
    private bool AllAutoPlaced(WorkflowGraph graph)
        => graph.Nodes.All(n => _autoPlaced.Contains(n) || !IsPlaced(n));

    private static bool IsPlaced(WorkflowNode node) => double.IsFinite(node.Position.X) && double.IsFinite(node.Position.Y);

    /// <summary>
    /// Gives the steps without a position one: a workflow the canvas arranged is arranged again as a whole (animated when
    /// it is on screen); otherwise the new steps go in ranks after the placed ones.
    /// </summary>
    private void PlaceUnplaced()
    {
        var graph = Graph;
        if (graph is null)
        {
            return;
        }
        var unplaced = graph.Nodes.Where(n => !IsPlaced(n)).ToList();
        if (unplaced.Count == 0)
        {
            return;
        }
        if (AllAutoPlaced(graph))
        {
            Place(graph.Nodes.ToList(), default, animate: unplaced.Count < graph.Nodes.Count);
            return;
        }
        // Next to the placed steps: below them (top to bottom) or to their right, one rank gap away.
        var placed = BoundsOf(graph.Nodes.Except(unplaced), DisplayPosition);
        var gap = _narrow ? _world?.NarrowRankGap ?? 26 : _world?.RankGap ?? 52;
        var origin = LayoutDirection == WorkflowLayoutDirection.TopToBottom
            ? new Point(placed.X, placed.Bottom + gap)
            : new Point(placed.Right + gap, placed.Y);
        Place(unplaced, origin, animate: false);
    }

    /// <summary>
    /// Arranges <paramref name="nodes"/> (and the edges among them) with their top-left at <paramref name="origin"/>,
    /// gliding the cards there from where they show when <paramref name="animate"/>.
    /// </summary>
    private void Place(List<WorkflowNode> nodes, Point origin, bool animate)
    {
        if (nodes.Count == 0)
        {
            return;
        }
        var index = new Dictionary<WorkflowNode, int>();
        var sizes = new Size[nodes.Count];
        var from = new Dictionary<WorkflowNode, Point>();
        for (var i = 0; i < nodes.Count; i++)
        {
            index[nodes[i]] = i;
            if (_cards.TryGetValue(nodes[i], out var card))
            {
                if (!Virtualizing && card.DesiredSize == default)
                {
                    card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                }
                sizes[i] = SizeOf(card);
            }
            if (IsPlaced(nodes[i]))
            {
                from[nodes[i]] = DisplayPosition(nodes[i]);
            }
        }
        if (TopLevel.GetTopLevel(this) is null)
        {
            // Cards out of a window have no template, so no size: arrange again at the first measure in one.
            _relayoutPending = true;
        }
        var edges = new List<LayeredGraphEdge>();
        foreach (var edge in Graph?.Edges ?? [])
        {
            if (index.TryGetValue(edge.Source, out var s) && index.TryGetValue(edge.Target, out var t))
            {
                edges.Add(new LayeredGraphEdge(s, t));
            }
        }
        var world = _world;
        var options = new LayeredGraphLayoutOptions
        {
            Direction = LayoutDirection == WorkflowLayoutDirection.TopToBottom
                ? LayeredGraphDirection.TopToBottom
                : LayeredGraphDirection.LeftToRight,
            RankGap = _narrow ? world?.NarrowRankGap ?? 26 : world?.RankGap ?? 52,
            FanOutRankGap = _narrow ? world?.NarrowFanOutRankGap ?? 36 : world?.FanOutRankGap ?? 72,
            NodeGap = _narrow ? world?.NarrowNodeGap ?? 10 : world?.NodeGap ?? 20
        };
        var layout = LayeredGraphLayout.Arrange(sizes, edges, options);
        _placing = true;
        try
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i].Position = layout.Positions[i] + origin;
                _autoPlaced.Add(nodes[i]);
            }
        }
        finally
        {
            _placing = false;
        }
        if (animate)
        {
            StartMove(from);
            // A fitted view travels with the cards instead of jumping to where they end.
            if (!_manualView && AutoFit != WorkflowAutoFit.None && IsArrangeValid)
            {
                SetView(FitView(AutoFit, ViewportSize), animate: true);
            }
        }
        _world?.InvalidateArrange();
        InvalidateMeasure();
    }

    /// <summary>
    /// Glides the cards from <paramref name="from"/> to their steps' positions, on <paramref name="motion"/>: by default
    /// <see cref="Glide"/>; a card let go off the grid settles onto it on the snappy spring.
    /// </summary>
    /// <remarks>
    /// Code-driven motion: the cards are placed by the world panel from the steps' positions, and the edges between them
    /// are re-shaped every frame; no control property carries the position, so a ControlTheme cannot animate it.
    /// </remarks>
    private void StartMove(Dictionary<WorkflowNode, Point> from, MotionSpec? motion = null)
    {
        _moveFrom.Clear();
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        foreach (var (node, start) in from)
        {
            if (start != WorkflowWorldPanel.PositionOf(node))
            {
                _moveFrom[node] = start;
            }
        }
        if (_moveFrom.Count == 0)
        {
            return;
        }
        if (_move is null)
        {
            _move = new MotionValue<double>(this, 0, MotionVectors.Double);
            _move.Changed += OnMoveChanged;
        }
        // Over the screen pixels the farthest card travels, so where the move ends is judged in pixels.
        _moveSpan = Math.Max(1, _moveFrom.Max(p =>
        {
            var d = WorkflowWorldPanel.PositionOf(p.Key) - p.Value;
            return Math.Sqrt(d.X * d.X + d.Y * d.Y) * _view.Zoom;
        }));
        _moveOnSpring = motion is MotionSpec.SpringMotion;
        _move.Jump(0);
        _ = FinishMove(_move.AnimateTo(_moveSpan, motion ?? Glide));
    }

    private async Task FinishMove(Task<bool> animation)
    {
        if (await animation)
        {
            _moveFrom.Clear();
            OnMoveFrame();
        }
    }

    /// <summary>
    /// A frame of a move. On a spring, once the farthest card is within half a pixel of its place and slower than
    /// <see cref="SettledSpeed"/>, the move ends: the rest of the spring would only tick cards by a pixel (see
    /// <see cref="Glide"/>).
    /// </summary>
    private void OnMoveChanged(double travelled)
    {
        if (_moveOnSpring && _move is { IsAnimating: true } move && Math.Abs(_moveSpan - travelled) < 0.5 &&
            Math.Abs(move.Velocity) < SettledSpeed)
        {
            // Jumping raises this again, no longer animating.
            move.Jump(_moveSpan);
            return;
        }
        OnMoveFrame();
    }

    private void OnMoveFrame()
    {
        UpdateEdges();
        _world?.InvalidateArrange();
    }

    /// <summary>Where a step's card shows now: its position, or on its way there while arranging animates.</summary>
    internal Point DisplayPosition(WorkflowNode node)
    {
        if (_dragStart.TryGetValue(node, out var start))
        {
            return start + _dragOffset;
        }
        var to = WorkflowWorldPanel.PositionOf(node);
        if (_move is { } move && _moveFrom.TryGetValue(node, out var from))
        {
            var t = move.Value / _moveSpan;
            return new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
        }
        return to;
    }

    /// <summary>A step's card rectangle as it shows now, in canvas units.</summary>
    internal Rect DisplayRect(WorkflowNode node)
        => new(DisplayPosition(node), _cards.TryGetValue(node, out var card) ? SizeOf(card) : default);

    /// <summary>
    /// Workflows with more steps than this realize only the cards near the view (see <see cref="Virtualizing"/>): at 500
    /// steps creating every card's template took most of the cold load.
    /// </summary>
    private const int VirtualizeAbove = 300;

    /// <summary>True for a large workflow, whose cards are realized (measured) only near the view.</summary>
    private bool Virtualizing => Graph is { } graph && graph.Nodes.Count > VirtualizeAbove;

    /// <summary>A card's size: measured, or while it is not realized the size its step would have.</summary>
    private Size SizeOf(WorkflowNodeCard card)
    {
        if (card.IsRealized || !Virtualizing || card.Node is not { } node)
        {
            return card.DesiredSize;
        }
        if (node.Size is { } own)
        {
            return own;
        }
        var compact = node.Appearance == WorkflowNodeAppearance.Compact;
        return _narrow ? compact ? new Size(108, 72) : new Size(344, 52) : compact ? new Size(200, 84) : new Size(300, 60);
    }

    // The part of the canvas whose cards are realized: the view and a screen around it. It grows when the view leaves it.
    private Rect _realizedArea;

    /// <summary>Realizes the cards that come near the view; called when the view changes.</summary>
    private void UpdateRealized()
    {
        if (!Virtualizing || _world is null || ViewportSize is not { Width: > 0, Height: > 0 } size)
        {
            return;
        }
        var visible = _view.VisibleWorld(size);
        if (!_realizedArea.Contains(visible.TopLeft) || !_realizedArea.Contains(visible.BottomRight))
        {
            _realizedArea = visible.Inflate(new Thickness(visible.Width / 2, visible.Height / 2));
            _world.InvalidateMeasure();
        }
    }

    /// <summary>Whether a card that is not realized yet is near enough to the view to be.</summary>
    private bool ShouldRealize(WorkflowNodeCard card)
    {
        if (!Virtualizing || card.Node is not { } node || card.IsFocused || _selection.Contains(node))
        {
            return true;
        }
        if (_realizedArea == default)
        {
            _realizedArea = _view.VisibleWorld(ViewportSize).Inflate(ViewportSize.Width / 2 / Math.Max(_view.Zoom, 1e-6));
        }
        return _realizedArea.Intersects(new Rect(DisplayPosition(node), SizeOf(card)));
    }

    /// <summary>Realizes <paramref name="node"/>'s card now (to focus or measure it).</summary>
    private void Realize(WorkflowNode node)
    {
        if (_cards.TryGetValue(node, out var card) && !card.IsRealized && _world is not null)
        {
            _world.ForceRealize.Add(card);
            _world.InvalidateMeasure();
            _world.UpdateLayout();
        }
    }

    /// <summary>The union of the cards' rectangles in canvas units, at their positions (where arranging ends).</summary>
    internal Rect ContentBounds() => BoundsOf(_cards.Keys, WorkflowWorldPanel.PositionOf);

    private Rect BoundsOf(IEnumerable<WorkflowNode> nodes, Func<WorkflowNode, Point> position)
    {
        Rect? bounds = null;
        foreach (var node in nodes)
        {
            var size = _cards.TryGetValue(node, out var card) ? SizeOf(card) : default;
            var rect = new Rect(position(node), size);
            bounds = bounds is { } b ? b.Union(rect) : rect;
        }
        return bounds ?? default;
    }

    /// <summary>Shapes every edge between its cards as they show now and sizes the edge layer to cover them.</summary>
    private void UpdateEdges()
    {
        SceneChanged?.Invoke();
        if (_edgeLayer is null)
        {
            return;
        }
        Rect? extent = null;
        var minHandle = _edgeLayer.BezierMinHandle;
        var corner = _edgeLayer.StepCornerRadius;
        foreach (var visual in _edgeLayer.Edges)
        {
            if (!_cards.ContainsKey(visual.Edge.Source) || !_cards.ContainsKey(visual.Edge.Target))
            {
                continue;
            }
            visual.Place(PortPoint(visual.Edge.Source, visual.Edge.SourcePort, true),
                PortPoint(visual.Edge.Target, visual.Edge.TargetPort, false), LayoutDirection, EdgeStyle, minHandle, corner);
            extent = extent is { } e ? e.Union(visual.Bounds) : visual.Bounds;
        }
        // Room for the strokes' width and a selected edge's halo.
        _edgeLayer.Extent = (extent ?? default).Inflate(6);
        _edgeLayer.InvalidateVisual();
        UpdatePorts();
        _world?.InvalidateArrange();
    }
}
