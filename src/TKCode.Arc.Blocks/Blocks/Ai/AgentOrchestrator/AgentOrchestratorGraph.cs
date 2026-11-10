using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>One line of the graph: a lead edge (lead to a sub-agent) or a peer edge between two sub-agents, sampled once per layout.</summary>
internal sealed class AgentOrchestratorGraphEdge
{
    public AgentOrchestratorGraphEdge(int a, int b, bool isPeer)
    {
        A = a;
        B = b;
        IsPeer = isPeer;
    }

    /// <summary>The node the edge starts at (the lead for a lead edge).</summary>
    public int A { get; }

    /// <summary>The node the edge ends at.</summary>
    public int B { get; }

    /// <summary>True for a line between two sub-agents (drawn after their first message).</summary>
    public bool IsPeer { get; }

    public Point[] Points { get; private set; } = [];

    public double[] Lengths { get; private set; } = [];

    public double Total { get; private set; }

    public Geometry? Geometry { get; private set; }

    public void SetPoints(List<Point> points)
    {
        Points = points.ToArray();
        Lengths = new double[Points.Length];
        double total = 0;
        for (var i = 1; i < Points.Length; i++)
        {
            var d = Points[i] - Points[i - 1];
            total += Math.Sqrt(d.X * d.X + d.Y * d.Y);
            Lengths[i] = total;
        }
        Total = total;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (Points.Length > 0)
            {
                context.BeginFigure(Points[0], false);
                for (var i = 1; i < Points.Length; i++)
                {
                    context.LineTo(Points[i]);
                }
                context.EndFigure(false);
            }
        }
        Geometry = geometry;
    }

    /// <summary>The point at <paramref name="t"/> (0–1) of the way along the edge, by arc length.</summary>
    public Point At(double t)
    {
        var points = Points;
        if (points.Length == 0)
        {
            return default;
        }
        if (points.Length == 1 || Total <= 0)
        {
            return points[0];
        }
        var target = Math.Clamp(t, 0, 1) * Total;
        var lengths = Lengths;
        // Binary search for the segment that holds the target length.
        int lo = 1, hi = lengths.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (lengths[mid] < target)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        var segment = lengths[lo] - lengths[lo - 1];
        var f = segment <= 0 ? 0 : (target - lengths[lo - 1]) / segment;
        var a = points[lo - 1];
        var b = points[lo];
        return new Point(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
    }
}

/// <summary>
/// The live graph of an <see cref="AgentOrchestrator"/>: the lead's node, one node per sub-agent, a line from the lead to
/// each of them and a dashed line between two sub-agents once they have talked. Every message sends a dot along its line
/// (down for a task, back up for a result, across for a question); the dots are drawn here, driven by one
/// <see cref="LiveLoop"/>, from a fixed pool with no per-frame allocation. When the sub-agents fit side by side at
/// <see cref="NodeMinWidth"/> they fan out under the lead; otherwise they stack under it as a tree with a trunk line on
/// the left. The nodes are real focusable controls: the arrow keys move between them, Home and End go to the first and
/// last, Enter and Space select.
/// </summary>
internal sealed class AgentOrchestratorGraph : Control
{
    public static readonly StyledProperty<double> NodeMaxWidthProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(NodeMaxWidth), 208);

    public static readonly StyledProperty<double> NodeMinWidthProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(NodeMinWidth), 156);

    public static readonly StyledProperty<double> LeadWidthProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(LeadWidth), 248);

    public static readonly StyledProperty<double> NodeHeightProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(NodeHeight), 112);

    public static readonly StyledProperty<double> TreeNodeHeightProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(TreeNodeHeight), 76);

    public static readonly StyledProperty<double> NodeGapProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(NodeGap), 12);

    public static readonly StyledProperty<double> RankGapProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(RankGap), 72);

    public static readonly StyledProperty<double> TreeIndentProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(TreeIndent), 28);

    public static readonly StyledProperty<double> EdgeWidthProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(EdgeWidth), 1);

    public static readonly StyledProperty<double> ActiveEdgeWidthProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(ActiveEdgeWidth), 1.5);

    public static readonly StyledProperty<double> PacketRadiusProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(PacketRadius), 4);

    public static readonly StyledProperty<double> PacketHaloRadiusProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, double>(nameof(PacketHaloRadius), 9);

    public static readonly StyledProperty<TimeSpan> PacketDurationProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, TimeSpan>(nameof(PacketDuration), TimeSpan.FromMilliseconds(900));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> ActiveBrushProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, IBrush?>(nameof(ActiveBrush));

    public static readonly StyledProperty<IBrush?> DoneBrushProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, IBrush?>(nameof(DoneBrush));

    public static readonly StyledProperty<IBrush?> FailedBrushProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, IBrush?>(nameof(FailedBrush));

    public static readonly StyledProperty<IBrush?> NoteBrushProperty =
        AvaloniaProperty.Register<AgentOrchestratorGraph, IBrush?>(nameof(NoteBrush));

    public static readonly DirectProperty<AgentOrchestratorGraph, bool> IsTreeProperty =
        AvaloniaProperty.RegisterDirect<AgentOrchestratorGraph, bool>(nameof(IsTree), o => o.IsTree);

    public double NodeMaxWidth { get => GetValue(NodeMaxWidthProperty); set => SetValue(NodeMaxWidthProperty, value); }
    public double NodeMinWidth { get => GetValue(NodeMinWidthProperty); set => SetValue(NodeMinWidthProperty, value); }
    public double LeadWidth { get => GetValue(LeadWidthProperty); set => SetValue(LeadWidthProperty, value); }
    public double NodeHeight { get => GetValue(NodeHeightProperty); set => SetValue(NodeHeightProperty, value); }
    public double TreeNodeHeight { get => GetValue(TreeNodeHeightProperty); set => SetValue(TreeNodeHeightProperty, value); }
    public double NodeGap { get => GetValue(NodeGapProperty); set => SetValue(NodeGapProperty, value); }
    public double RankGap { get => GetValue(RankGapProperty); set => SetValue(RankGapProperty, value); }
    public double TreeIndent { get => GetValue(TreeIndentProperty); set => SetValue(TreeIndentProperty, value); }
    public double EdgeWidth { get => GetValue(EdgeWidthProperty); set => SetValue(EdgeWidthProperty, value); }
    public double ActiveEdgeWidth { get => GetValue(ActiveEdgeWidthProperty); set => SetValue(ActiveEdgeWidthProperty, value); }
    public double PacketRadius { get => GetValue(PacketRadiusProperty); set => SetValue(PacketRadiusProperty, value); }
    public double PacketHaloRadius { get => GetValue(PacketHaloRadiusProperty); set => SetValue(PacketHaloRadiusProperty, value); }
    public TimeSpan PacketDuration { get => GetValue(PacketDurationProperty); set => SetValue(PacketDurationProperty, value); }
    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }
    public IBrush? ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public IBrush? DoneBrush { get => GetValue(DoneBrushProperty); set => SetValue(DoneBrushProperty, value); }
    public IBrush? FailedBrush { get => GetValue(FailedBrushProperty); set => SetValue(FailedBrushProperty, value); }
    public IBrush? NoteBrush { get => GetValue(NoteBrushProperty); set => SetValue(NoteBrushProperty, value); }

    /// <summary>True while the sub-agents stack under the lead (they do not fit side by side).</summary>
    public bool IsTree { get => _isTree; private set => SetAndRaise(IsTreeProperty, ref _isTree, value); }

    /// <summary>Raised with a node's index when it is clicked or chosen with Enter or Space.</summary>
    public event Action<int>? NodeInvoked;

    private const int PacketCapacity = 24;
    private const double CornerRadius = 10;

    private struct Packet
    {
        public int Edge;
        public bool Reverse;
        public double Progress;
        public OrchestratorMessageKind Kind;
    }

    private readonly List<AgentOrchestratorNode> _nodes = new();
    private readonly List<AgentOrchestratorGraphEdge> _edges = new();
    private readonly List<(int A, int B)> _peerPairs = new();
    private readonly Rect[] _empty = [];
    private Rect[] _rects = [];
    private readonly Packet[] _packets = new Packet[PacketCapacity];
    private int _packetCount;
    private LiveLoop? _loop;
    private bool _isTree;
    private Size _laidOut;
    private bool _edgesDirty = true;
    private readonly List<Point> _scratch = new(64);

    // Pens and brushes are rebuilt when a brush or a width changes, never per frame.
    private IPen? _restPen;
    private IPen? _activePen;
    private IPen? _donePen;
    private IPen? _failedPen;
    private IPen? _peerPen;
    private IBrush? _activeFill;
    private IBrush? _doneFill;
    private IBrush? _failedFill;
    private IBrush? _noteFill;
    private IBrush? _activeHalo;
    private IBrush? _doneHalo;
    private IBrush? _failedHalo;
    private IBrush? _noteHalo;

    static AgentOrchestratorGraph()
    {
        AffectsMeasure<AgentOrchestratorGraph>(NodeMaxWidthProperty, NodeMinWidthProperty, LeadWidthProperty, NodeHeightProperty,
            TreeNodeHeightProperty, NodeGapProperty, RankGapProperty, TreeIndentProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<AgentOrchestratorGraph>(AccessibilityView.Control);
    }

    public AgentOrchestratorGraph()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        ClipToBounds = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GraphPeer(this);

    private sealed class GraphPeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }

    /// <summary>The nodes, the lead first.</summary>
    public IReadOnlyList<AgentOrchestratorNode> Nodes => _nodes;

    /// <summary>The number of dots in flight (tests).</summary>
    public int PacketCount => _packetCount;

    /// <summary>The edges, lead edges first in node order, then peer edges (tests).</summary>
    public IReadOnlyList<AgentOrchestratorGraphEdge> Edges => _edges;

    /// <summary>The laid-out rectangle of node <paramref name="index"/>.</summary>
    public Rect NodeRect(int index) => index >= 0 && index < _rects.Length ? _rects[index] : default;

    /// <summary>Replaces the nodes: <paramref name="agents"/> with the lead first.</summary>
    public void SetAgents(IReadOnlyList<OrchestratorAgent> agents, AgentOrchestratorLabels labels)
    {
        foreach (var node in _nodes)
        {
            node.Invoked -= OnNodeInvoked;
            node.Agent = null;
            VisualChildren.Remove(node);
            LogicalChildren.Remove(node);
        }
        _nodes.Clear();
        _peerPairs.Clear();
        _packetCount = 0;
        for (var i = 0; i < agents.Count; i++)
        {
            var node = new AgentOrchestratorNode { Labels = labels, IsLead = i == 0, Index = i };
            node.Agent = agents[i];
            node.Invoked += OnNodeInvoked;
            _nodes.Add(node);
            VisualChildren.Add(node);
            LogicalChildren.Add(node);
        }
        _rects = _empty;
        _edgesDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Gives every node new labels.</summary>
    public void SetLabels(AgentOrchestratorLabels labels)
    {
        foreach (var node in _nodes)
        {
            node.Labels = labels;
        }
    }

    /// <summary>Marks node <paramref name="index"/> as selected (−1 for none).</summary>
    public void Select(int index)
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].IsSelected = i == index;
        }
    }

    /// <summary>Forgets the peer lines and the dots of the last run.</summary>
    public void ClearRun()
    {
        _peerPairs.Clear();
        _packetCount = 0;
        _edgesDirty = true;
        _loop?.Stop();
        BuildEdges();
        InvalidateVisual();
    }

    /// <summary>Sends a dot from node <paramref name="from"/> to node <paramref name="to"/> (full motion only); adds a peer line if needed.</summary>
    public void Launch(int from, int to, OrchestratorMessageKind kind)
    {
        if (from == to || from < 0 || to < 0 || from >= _nodes.Count || to >= _nodes.Count)
        {
            return;
        }
        var pair = (Math.Min(from, to), Math.Max(from, to));
        if (from != 0 && to != 0 && !_peerPairs.Contains(pair))
        {
            // Two sub-agents talk for the first time: their line appears now and stays for the run.
            _peerPairs.Add(pair);
            _edgesDirty = true;
            InvalidateVisual();
        }
        var edge = EdgeIndex(from, to, out var reverse);
        if (edge < 0 || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_packetCount == PacketCapacity)
        {
            // A burst beyond the pool drops the oldest dot.
            Array.Copy(_packets, 1, _packets, 0, PacketCapacity - 1);
            _packetCount--;
        }
        _packets[_packetCount++] = new Packet { Edge = edge, Reverse = reverse, Progress = 0, Kind = kind };
        _loop ??= new LiveLoop(this, Step);
        _loop.Start();
        InvalidateVisual();
    }

    /// <summary>The edge between two nodes and whether a dot from <paramref name="from"/> runs against its direction; −1 when none is laid out.</summary>
    private int EdgeIndex(int from, int to, out bool reverse)
    {
        reverse = false;
        return BuildEdges() ? FindEdge(from, to, out reverse) : -1;
    }

    private int FindEdge(int from, int to, out bool reverse)
    {
        reverse = false;
        for (var i = 0; i < _edges.Count; i++)
        {
            var edge = _edges[i];
            if (edge.A == from && edge.B == to)
            {
                return i;
            }
            if (edge.A == to && edge.B == from)
            {
                reverse = true;
                return i;
            }
        }
        return -1;
    }

    private bool Step(double dt)
    {
        var duration = Math.Max(0.05, PacketDuration.TotalSeconds);
        var write = 0;
        for (var i = 0; i < _packetCount; i++)
        {
            var packet = _packets[i];
            packet.Progress += dt / duration;
            if (packet.Progress < 1)
            {
                _packets[write++] = packet;
            }
        }
        _packetCount = write;
        InvalidateVisual();
        return _packetCount > 0;
    }

    private void OnNodeInvoked(AgentOrchestratorNode node) => NodeInvoked?.Invoke(node.Index);

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _nodes.Count == 0)
        {
            return;
        }
        var current = -1;
        for (var i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].IsFocused)
            {
                current = i;
                break;
            }
        }
        if (current < 0)
        {
            return;
        }
        var target = Navigate(current, e.Key);
        if (target is { } next && next != current)
        {
            _nodes[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (target is not null)
        {
            e.Handled = true;
        }
    }

    /// <summary>The node the key moves to from <paramref name="current"/>; null when the key is not a move.</summary>
    internal int? Navigate(int current, Key key)
    {
        var last = _nodes.Count - 1;
        switch (key)
        {
            case Key.Home:
                return 0;
            case Key.End:
                return last;
        }
        if (IsTree)
        {
            return key switch
            {
                Key.Up or Key.Left => Math.Max(0, current - 1),
                Key.Down or Key.Right => Math.Min(last, current + 1),
                _ => null,
            };
        }
        // Fan: Up goes to the lead, Down from the lead to the selected sub-agent (or the first), Left and Right along the row.
        return key switch
        {
            Key.Up => 0,
            Key.Down when current == 0 => last >= 1 ? Math.Max(1, SelectedIndex()) : 0,
            Key.Down => current,
            Key.Left => current <= 1 ? current : current - 1,
            Key.Right when current == 0 => last >= 1 ? 1 : 0,
            Key.Right => Math.Min(last, current + 1),
            _ => null,
        };
    }

    private int SelectedIndex()
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].IsSelected)
            {
                return i;
            }
        }
        return 1;
    }

    #endregion

    #region Layout

    private int SubCount => Math.Max(0, _nodes.Count - 1);

    private bool FitsFan(double width)
    {
        var m = SubCount;
        return m == 0 || m * NodeMinWidth + (m - 1) * NodeGap <= width + 0.5;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var m = SubCount;
        var ideal = Math.Max(LeadWidth, m * NodeMaxWidth + Math.Max(0, m - 1) * NodeGap);
        var width = double.IsInfinity(availableSize.Width) ? ideal : availableSize.Width;
        var tree = !FitsFan(width);
        IsTree = tree;
        ComputeRects(width, tree);
        for (var i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].IsTree = tree && i > 0;
            _nodes[i].Measure(_rects[i].Size);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? ideal : width, TotalHeight(tree));
    }

    private double TotalHeight(bool tree)
    {
        var m = SubCount;
        if (_nodes.Count == 0)
        {
            return 0;
        }
        if (m == 0)
        {
            return NodeHeight;
        }
        return tree
            ? NodeHeight + NodeGap * 2 + m * TreeNodeHeight + (m - 1) * NodeGap
            : NodeHeight * 2 + RankGap;
    }

    private void ComputeRects(double width, bool tree)
    {
        var count = _nodes.Count;
        if (_rects.Length != count)
        {
            _rects = new Rect[count];
        }
        if (count == 0)
        {
            return;
        }
        var m = SubCount;
        if (tree)
        {
            _rects[0] = new Rect(0, 0, width, NodeHeight);
            var indent = TreeIndent;
            var y = NodeHeight + NodeGap * 2;
            for (var i = 1; i < count; i++)
            {
                _rects[i] = new Rect(indent, y, Math.Max(0, width - indent), TreeNodeHeight);
                y += TreeNodeHeight + NodeGap;
            }
            return;
        }
        var leadWidth = Math.Min(LeadWidth, width);
        _rects[0] = new Rect((width - leadWidth) / 2, 0, leadWidth, NodeHeight);
        if (m == 0)
        {
            return;
        }
        var nodeWidth = Math.Min(NodeMaxWidth, (width - (m - 1) * NodeGap) / m);
        var total = m * nodeWidth + (m - 1) * NodeGap;
        var left = (width - total) / 2;
        var top = NodeHeight + RankGap;
        for (var i = 1; i < count; i++)
        {
            _rects[i] = new Rect(left + (i - 1) * (nodeWidth + NodeGap), top, nodeWidth, NodeHeight);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tree = !FitsFan(finalSize.Width);
        IsTree = tree;
        ComputeRects(finalSize.Width, tree);
        for (var i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].IsTree = tree && i > 0;
            _nodes[i].Arrange(_rects[i]);
        }
        if (_laidOut != finalSize)
        {
            _laidOut = finalSize;
            _edgesDirty = true;
        }
        BuildEdges();
        return finalSize;
    }

    /// <summary>Samples every line for the current rectangles (only when the layout or the peer lines changed). False before the first layout.</summary>
    private bool BuildEdges()
    {
        if (!_edgesDirty)
        {
            return true;
        }
        if (_rects.Length != _nodes.Count || _nodes.Count == 0)
        {
            return false;
        }
        _edgesDirty = false;
        // Dots keep their edge index: lead edges come first and keep their order; peer edges keep theirs.
        _edges.Clear();
        var lead = _rects[0];
        var m = SubCount;
        for (var i = 1; i < _nodes.Count; i++)
        {
            var edge = new AgentOrchestratorGraphEdge(0, i, isPeer: false);
            _scratch.Clear();
            var sub = _rects[i];
            if (IsTree)
            {
                var trunk = TreeIndent / 2;
                var mid = sub.Y + sub.Height / 2;
                Elbow(new Point(trunk, lead.Bottom), new Point(trunk, mid), new Point(sub.X, mid));
            }
            else
            {
                var spread = m > 1 ? Math.Min(24, lead.Width * 0.6 / (m - 1)) : 0;
                var start = new Point(lead.Center.X + (i - 1 - (m - 1) / 2.0) * spread, lead.Bottom);
                var end = new Point(sub.Center.X, sub.Y);
                var bend = (end.Y - start.Y) * 0.5;
                Cubic(start, new Point(start.X, start.Y + bend), new Point(end.X, end.Y - bend), end, 28);
            }
            edge.SetPoints(_scratch);
            _edges.Add(edge);
        }
        foreach (var (a, b) in _peerPairs)
        {
            if (a >= _rects.Length || b >= _rects.Length)
            {
                continue;
            }
            var edge = new AgentOrchestratorGraphEdge(a, b, isPeer: true);
            _scratch.Clear();
            var ra = _rects[a];
            var rb = _rects[b];
            if (IsTree)
            {
                // Along the trunk side, a little right of the trunk.
                var x = TreeIndent / 2 + 6;
                var ya = ra.Y + ra.Height * 0.3;
                var yb = rb.Y + rb.Height * 0.3;
                _scratch.Add(new Point(ra.X, ya));
                Elbow(new Point(x, ya), new Point(x, yb), new Point(rb.X, yb), skipFirst: false);
            }
            else
            {
                var start = new Point(ra.Center.X, ra.Y);
                var end = new Point(rb.Center.X, rb.Y);
                var lift = Math.Min(RankGap * 0.6, 20 + Math.Abs(end.X - start.X) * 0.12);
                Cubic(start, new Point(start.X, start.Y - lift), new Point(end.X, end.Y - lift), end, 28);
            }
            edge.SetPoints(_scratch);
            _edges.Add(edge);
        }
        return true;
    }

    private void Cubic(Point p0, Point p1, Point p2, Point p3, int steps)
    {
        for (var s = 0; s <= steps; s++)
        {
            var t = s / (double)steps;
            var u = 1 - t;
            var x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
            var y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
            _scratch.Add(new Point(x, y));
        }
    }

    /// <summary>A line from <paramref name="a"/> through the corner <paramref name="corner"/> to <paramref name="b"/>, with a rounded corner.</summary>
    private void Elbow(Point a, Point corner, Point b, bool skipFirst = false)
    {
        var inLength = Distance(a, corner);
        var outLength = Distance(corner, b);
        var r = Math.Min(CornerRadius, Math.Min(inLength, outLength) / 2);
        if (!skipFirst)
        {
            _scratch.Add(a);
        }
        if (r <= 0.5)
        {
            _scratch.Add(corner);
            _scratch.Add(b);
            return;
        }
        var before = corner + (a - corner) * (r / inLength);
        var after = corner + (b - corner) * (r / outLength);
        _scratch.Add(before);
        // A quadratic curve through the corner stands in for the arc.
        for (var s = 1; s < 6; s++)
        {
            var t = s / 6.0;
            var u = 1 - t;
            _scratch.Add(new Point(u * u * before.X + 2 * u * t * corner.X + t * t * after.X,
                u * u * before.Y + 2 * u * t * corner.Y + t * t * after.Y));
        }
        _scratch.Add(after);
        _scratch.Add(b);
    }

    private static double Distance(Point a, Point b)
    {
        var d = a - b;
        return Math.Sqrt(d.X * d.X + d.Y * d.Y);
    }

    #endregion

    #region Rendering

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == EdgeBrushProperty || p == ActiveBrushProperty || p == DoneBrushProperty || p == FailedBrushProperty ||
            p == NoteBrushProperty || p == EdgeWidthProperty || p == ActiveEdgeWidthProperty)
        {
            _restPen = null;
            InvalidateVisual();
        }
    }

    private void EnsurePaint()
    {
        if (_restPen is not null)
        {
            return;
        }
        var edge = ColorOf(EdgeBrush, Colors.Gray);
        var active = ColorOf(ActiveBrush, Colors.DodgerBlue);
        var done = ColorOf(DoneBrush, Colors.SeaGreen);
        var failed = ColorOf(FailedBrush, Colors.IndianRed);
        var note = ColorOf(NoteBrush, Colors.Gray);
        var dash = new ImmutableDashStyle([3, 3], 0);
        _restPen = new ImmutablePen(new ImmutableSolidColorBrush(edge), EdgeWidth, dash, PenLineCap.Round);
        _peerPen = new ImmutablePen(new ImmutableSolidColorBrush(edge), EdgeWidth, new ImmutableDashStyle([1, 3], 0), PenLineCap.Round);
        _activePen = new ImmutablePen(new ImmutableSolidColorBrush(active), ActiveEdgeWidth, null, PenLineCap.Round);
        _donePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb((byte)(done.A * 0.7), done.R, done.G, done.B)), EdgeWidth, null, PenLineCap.Round);
        _failedPen = new ImmutablePen(new ImmutableSolidColorBrush(failed), EdgeWidth, dash, PenLineCap.Round);
        _activeFill = new ImmutableSolidColorBrush(active);
        _doneFill = new ImmutableSolidColorBrush(done);
        _failedFill = new ImmutableSolidColorBrush(failed);
        _noteFill = new ImmutableSolidColorBrush(note);
        _activeHalo = new ImmutableSolidColorBrush(active, 0.22);
        _doneHalo = new ImmutableSolidColorBrush(done, 0.22);
        _failedHalo = new ImmutableSolidColorBrush(failed, 0.22);
        _noteHalo = new ImmutableSolidColorBrush(note, 0.18);
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => (brush as ISolidColorBrush)?.Color ?? fallback;

    /// <summary>Repaints the lines (an agent's status changed).</summary>
    public void Refresh() => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        if (_edges.Count == 0 && _packetCount == 0)
        {
            return;
        }
        EnsurePaint();
        foreach (var edge in _edges)
        {
            if (edge.Geometry is not { } geometry)
            {
                continue;
            }
            var pen = edge.IsPeer ? _peerPen : PenFor(edge.B);
            context.DrawGeometry(null, pen, geometry);
        }
        var radius = PacketRadius;
        var halo = PacketHaloRadius;
        for (var i = 0; i < _packetCount; i++)
        {
            var packet = _packets[i];
            if (packet.Edge >= _edges.Count)
            {
                continue;
            }
            var t = ArcEasings.InOut.Ease(Math.Clamp(packet.Progress, 0, 1));
            var point = _edges[packet.Edge].At(packet.Reverse ? 1 - t : t);
            var (fill, glow) = packet.Kind switch
            {
                OrchestratorMessageKind.Delegate => (_activeFill, _activeHalo),
                OrchestratorMessageKind.Result => (_doneFill, _doneHalo),
                OrchestratorMessageKind.Error => (_failedFill, _failedHalo),
                _ => (_noteFill, _noteHalo),
            };
            // Fade in over the first tenth and out over the last.
            var fade = Math.Min(1, Math.Min(packet.Progress, 1 - packet.Progress) * 10);
            using (context.PushOpacity(fade))
            {
                context.DrawEllipse(glow, null, point, halo, halo);
                context.DrawEllipse(fill, null, point, radius, radius);
            }
        }
    }

    private IPen? PenFor(int node)
    {
        var status = node < _nodes.Count ? _nodes[node].Agent?.Status ?? OrchestratorAgentStatus.Idle : OrchestratorAgentStatus.Idle;
        return status switch
        {
            OrchestratorAgentStatus.Thinking or OrchestratorAgentStatus.Working or OrchestratorAgentStatus.Waiting => _activePen,
            OrchestratorAgentStatus.Done => _donePen,
            OrchestratorAgentStatus.Failed => _failedPen,
            _ => _restPen,
        };
    }

    #endregion
}
