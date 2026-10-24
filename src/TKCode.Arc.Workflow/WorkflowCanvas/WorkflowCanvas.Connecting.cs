using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    /// <summary>
    /// Raised when a person connects two ports, after the workflow's own checks (<see cref="WorkflowGraph.CanConnect"/>)
    /// and before the edge is added: set <see cref="WorkflowConnectionRequestedEventArgs.Cancel"/> to refuse it, with a
    /// <see cref="WorkflowConnectionRequestedEventArgs.Reason"/> to tell them why.
    /// </summary>
    public static readonly RoutedEvent<WorkflowConnectionRequestedEventArgs> ConnectionRequestedEvent =
        RoutedEvent.Register<WorkflowCanvas, WorkflowConnectionRequestedEventArgs>(nameof(ConnectionRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when a person drops a new connection on the empty canvas: show a list of steps there, add the chosen one
    /// and connect it yourself.
    /// </summary>
    public static readonly RoutedEvent<WorkflowConnectionDroppedEventArgs> ConnectionDroppedOnCanvasEvent =
        RoutedEvent.Register<WorkflowCanvas, WorkflowConnectionDroppedEventArgs>(nameof(ConnectionDroppedOnCanvas), RoutingStrategies.Bubble);

    /// <summary>A press this close to a port (screen px) grabs it; a dragged connection snaps to ports this close.</summary>
    private const double PortHitRadius = 9;
    private const double PortMagnetRadius = 24;

    private readonly List<WorkflowEdge> _selectedEdges = new();
    private WorkflowNode? _hoverNode;

    // A connection being made: from where, the edge picked up to reconnect, and where it would go now.
    private WorkflowNode? _connectFrom;
    private WorkflowPort? _connectFromPort;
    private WorkflowEdge? _pickedEdge;
    private WorkflowRemoveChange? _pickedChange;
    private WorkflowEdge? _pressEdge;
    private (WorkflowNode Node, WorkflowPort? Port)? _connectTarget;
    private Point _connectPointer;
    private readonly HashSet<WorkflowNode> _refusing = new();

    // The keyboard's connect mode.
    private bool _keyConnect;
    private List<WorkflowPort?> _keyPorts = [];
    private int _keyPortIndex = -1;
    private List<(WorkflowNode Node, WorkflowPort? Port)> _keyTargets = [];
    private int _keyTargetIndex;

    /// <inheritdoc cref="ConnectionRequestedEvent"/>
    public event EventHandler<WorkflowConnectionRequestedEventArgs>? ConnectionRequested
    {
        add => AddHandler(ConnectionRequestedEvent, value);
        remove => RemoveHandler(ConnectionRequestedEvent, value);
    }

    /// <inheritdoc cref="ConnectionDroppedOnCanvasEvent"/>
    public event EventHandler<WorkflowConnectionDroppedEventArgs>? ConnectionDroppedOnCanvas
    {
        add => AddHandler(ConnectionDroppedOnCanvasEvent, value);
        remove => RemoveHandler(ConnectionDroppedOnCanvasEvent, value);
    }

    /// <summary>The selected edges.</summary>
    public IReadOnlyList<WorkflowEdge> SelectedEdges => _selectedEdges;

    /// <summary>True while a connection is being made with the pointer or the keyboard (tests).</summary>
    internal bool IsConnecting => _connectFrom is not null;

    /// <summary>Undoes the last edit made on the canvas (<see cref="WorkflowGraph.History"/>); moved cards glide back.</summary>
    public void Undo() => ApplyHistory(redo: false);

    /// <summary>Redoes the last undone edit.</summary>
    public void Redo() => ApplyHistory(redo: true);

    private void ApplyHistory(bool redo)
    {
        if (Graph is not { } graph)
        {
            return;
        }
        var from = _cards.Keys.ToDictionary(n => n, DisplayPosition);
        WorkflowChange? change = null;
        ChangeGraph(() => change = redo ? graph.History.RedoChange() : graph.History.UndoChange());
        if (change is null)
        {
            return;
        }
        StartMove(from.Where(p => _cards.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value));
        OnMoveFrame();
        Announce(WorkflowText.Get(redo ? WorkflowLangResourceKind.AnnounceRedo : WorkflowLangResourceKind.AnnounceUndo));
        RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, redo ? WorkflowEditKind.Redo : WorkflowEditKind.Undo,
            change.Nodes, change.Edges));
    }

    // Ports ------------------------------------------------------------------------------------------------------------

    /// <summary>Where a port of <paramref name="node"/> sits (null is the implicit one): spread evenly along its card's side.</summary>
    internal Point PortPoint(WorkflowNode node, WorkflowPort? port, bool output)
    {
        var rect = DisplayRect(node);
        var list = output ? node.Outputs : node.Inputs;
        var index = port is null ? -1 : list.IndexOf(port);
        var share = index < 0 ? 0.5 : (index + 1.0) / (list.Count + 1);
        return LayoutDirection == WorkflowLayoutDirection.TopToBottom
            ? new Point(rect.X + rect.Width * share, output ? rect.Bottom : rect.Top)
            : new Point(output ? rect.Right : rect.X, rect.Y + rect.Height * share);
    }

    /// <summary>The ports a step shows: its named ones, or the implicit input (unless it has none) and output.</summary>
    internal static IEnumerable<(WorkflowPort? Port, bool Output)> PortsOf(WorkflowNode node)
    {
        if (node.Inputs.Count > 0)
        {
            foreach (var port in node.Inputs)
            {
                yield return (port, false);
            }
        }
        else if (node.HasDefaultInput)
        {
            yield return (null, false);
        }
        if (node.Outputs.Count > 0)
        {
            foreach (var port in node.Outputs)
            {
                yield return (port, true);
            }
        }
        else
        {
            yield return (null, true);
        }
    }

    /// <summary>The port within <see cref="PortHitRadius"/> screen px of <paramref name="world"/>, nearest first.</summary>
    private (WorkflowNode Node, WorkflowPort? Port, bool Output)? HitPort(Point world)
    {
        var radius = PortHitRadius / _view.Zoom;
        (WorkflowNode, WorkflowPort?, bool)? best = null;
        var bestDistance = radius;
        foreach (var node in _cards.Keys)
        {
            foreach (var (port, output) in PortsOf(node))
            {
                var d = Distance(PortPoint(node, port, output), world);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = (node, port, output);
                }
            }
        }
        return best;
    }

    /// <summary>The edge within 6 screen px of <paramref name="world"/>, nearest first.</summary>
    private WorkflowEdge? HitEdge(Point world)
    {
        if (_edgeLayer is null)
        {
            return null;
        }
        var best = 6 / _view.Zoom;
        WorkflowEdge? hit = null;
        foreach (var visual in _edgeLayer.Edges)
        {
            var d = visual.DistanceTo(world);
            if (d <= best)
            {
                best = d;
                hit = visual.Edge;
            }
        }
        return hit;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>The first input of <paramref name="target"/> that the connection may go to, or false.</summary>
    private bool FirstValidInput(WorkflowNode target, out WorkflowPort? port, out string? reason)
    {
        port = null;
        reason = null;
        if (Graph is not { } graph || _connectFrom is not { } from)
        {
            return false;
        }
        var inputs = target.Inputs.Count > 0 ? target.Inputs.Cast<WorkflowPort?>().ToList() : [null];
        foreach (var input in inputs)
        {
            if (graph.CanConnect(from, _connectFromPort, target, input, out var why))
            {
                port = input;
                return true;
            }
            reason ??= why;
        }
        return false;
    }

    /// <summary>Redraws the ports: of hovered, selected and focused cards when editable, of every card while connecting.</summary>
    private void UpdatePorts()
    {
        if (_portLayer is null)
        {
            return;
        }
        _portLayer.Marks.Clear();
        var vertical = LayoutDirection == WorkflowLayoutDirection.TopToBottom;
        foreach (var node in _cards.Keys)
        {
            var show = IsEditable && (IsConnecting || ReferenceEquals(node, _hoverNode) || _selection.Contains(node) ||
                                      _cards[node].IsFocused);
            foreach (var (port, output) in PortsOf(node))
            {
                var label = port?.Label;
                if (!show && string.IsNullOrEmpty(label))
                {
                    continue;
                }
                var highlight = _connectTarget is { } target && ReferenceEquals(target.Node, node) && target.Port == port && !output;
                _portLayer.Marks.Add(new WorkflowPortMark(PortPoint(node, port, output), output, highlight, label, show, vertical));
            }
        }
        _portLayer.Preview = null;
        if (_connectFrom is { } from)
        {
            var start = PortPoint(from, _connectFromPort, true);
            var end = _connectTarget is { } t ? PortPoint(t.Node, t.Port, false) : _connectPointer;
            var preview = new WorkflowEdgeVisual(new WorkflowEdge(from, from));
            preview.Place(start, end, LayoutDirection, EdgeStyle, _edgeLayer?.BezierMinHandle ?? 40, _edgeLayer?.StepCornerRadius ?? 8);
            _portLayer.Preview = preview.Wire;
            _portLayer.PreviewSnapped = _connectTarget is not null;
        }
        _portLayer.Extent = ContentBounds().Inflate(80);
        _portLayer.InvalidateVisual();
        _world?.InvalidateArrange();
    }

    // Connecting with the pointer ----------------------------------------------------------------------------------------

    /// <summary>A press on a port: an output starts a connection; an input with edges picks up the last one to move it.</summary>
    private bool TryBeginConnect(PointerPressedEventArgs e, Point world)
    {
        if (!IsEditable || Graph is not { } graph || HitPort(world) is not { } hit)
        {
            return false;
        }
        if (hit.Output)
        {
            _connectFrom = hit.Node;
            _connectFromPort = hit.Port;
        }
        else if (graph.Edges.LastOrDefault(edge => edge.Target == hit.Node && edge.TargetPort == hit.Port) is { } edge)
        {
            _pickedEdge = edge;
            _connectFrom = edge.Source;
            _connectFromPort = edge.SourcePort;
        }
        else
        {
            return false;
        }
        BeginPress(e, Gesture.ConnectPending, null);
        return true;
    }

    /// <summary>The press moved: the connection follows the pointer (a picked-up edge leaves its old input now).</summary>
    private void StartConnect()
    {
        _gesture = Gesture.Connect;
        if (_pickedEdge is { } edge && Graph is { } graph)
        {
            _pickedChange = WorkflowRemoveChange.Removing(graph, [], [edge]);
            ChangeGraph(() => graph.Edges.Remove(edge));
        }
        // Steps the connection cannot go to fade while it is made.
        _refusing.Clear();
        foreach (var (node, card) in _cards)
        {
            if (!ReferenceEquals(node, _connectFrom) && !FirstValidInput(node, out _, out _))
            {
                _refusing.Add(node);
                card.SetRefusing(true);
            }
        }
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <summary>Snaps the connection to the nearest port it can go to within reach, or to the card under the pointer.</summary>
    private void UpdateConnect()
    {
        _connectPointer = _view.ScreenToWorld(_lastPointer);
        _connectTarget = null;
        string? hint = null;
        var reach = PortMagnetRadius / _view.Zoom;
        foreach (var node in _cards.Keys)
        {
            if (ReferenceEquals(node, _connectFrom) || _refusing.Contains(node))
            {
                continue;
            }
            foreach (var (port, output) in PortsOf(node))
            {
                if (output || Graph?.CanConnect(_connectFrom!, _connectFromPort, node, port, out _) != true)
                {
                    continue;
                }
                var d = Distance(PortPoint(node, port, false), _connectPointer);
                if (d <= reach)
                {
                    reach = d;
                    _connectTarget = (node, port);
                }
            }
        }
        if (_connectTarget is null && _cards.Keys.FirstOrDefault(n => !ReferenceEquals(n, _connectFrom) && DisplayRect(n).Contains(_connectPointer)) is { } over)
        {
            if (FirstValidInput(over, out var input, out var reason))
            {
                _connectTarget = (over, input);
            }
            else
            {
                hint = reason;
            }
        }
        if (_overlay is not null)
        {
            _overlay.Hint = hint;
            _overlay.HintPoint = _lastPointer;
        }
        UpdatePorts();
    }

    /// <summary>The connection is let go: connect, reconnect, disconnect, or report a drop on the empty canvas.</summary>
    private void FinishConnect()
    {
        if (_connectFrom is not { } from || Graph is not { } graph)
        {
            EndConnect();
            return;
        }
        if (_connectTarget is { } target)
        {
            if (!TryConnect(from, _connectFromPort, target.Node, target.Port) && _pickedEdge is not null)
            {
                RestorePicked();
            }
        }
        else if (_pickedEdge is { } removed && _pickedChange is { } change)
        {
            graph.History.Record(change);
            Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceDisconnected, removed.Source.Title, removed.Target.Title));
            RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, WorkflowEditKind.Disconnect, [], [removed]));
        }
        else if (_cards.Keys.FirstOrDefault(n => DisplayRect(n).Contains(_connectPointer)) is { } refused)
        {
            // Let go on a step that cannot take the connection: nothing happens, and the person hears why.
            if (!ReferenceEquals(refused, from) && !FirstValidInput(refused, out _, out var reason))
            {
                Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceRefused, reason));
            }
        }
        else
        {
            RaiseEvent(new WorkflowConnectionDroppedEventArgs(ConnectionDroppedOnCanvasEvent, from, _connectFromPort, _connectPointer));
        }
        EndConnect();
    }

    /// <summary>Gives up the connection; a picked-up edge goes back where it was.</summary>
    private void CancelConnect()
    {
        if (_gesture == Gesture.Connect)
        {
            RestorePicked();
        }
        EndConnect();
    }

    /// <summary>Puts a picked-up edge back at its place in <see cref="WorkflowGraph.Edges"/>.</summary>
    private void RestorePicked()
    {
        if (_pickedChange is { } change && Graph is { } graph)
        {
            ChangeGraph(() => change.Undo(graph));
        }
    }

    private void EndConnect()
    {
        foreach (var node in _refusing)
        {
            _cards.GetValueOrDefault(node)?.SetRefusing(false);
        }
        _refusing.Clear();
        _connectFrom = null;
        _connectFromPort = null;
        _connectTarget = null;
        _pickedEdge = null;
        _pickedChange = null;
        _keyConnect = false;
        if (_overlay is not null)
        {
            _overlay.Hint = null;
        }
        UpdatePorts();
    }

    /// <summary>
    /// Connects after the workflow's checks and <see cref="ConnectionRequested"/>; records it, raises
    /// <see cref="GraphEdited"/> and says so. A refusal is announced with its reason. With an edge picked up from its
    /// input, this is a reconnect: one step in the history, and a <c>Disconnect</c> before the <c>Connect</c>.
    /// </summary>
    private bool TryConnect(WorkflowNode from, WorkflowPort? fromPort, WorkflowNode to, WorkflowPort? toPort)
    {
        if (Graph is not { } graph)
        {
            return false;
        }
        if (!graph.CanConnect(from, fromPort, to, toPort, out var reason))
        {
            Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceRefused, reason));
            return false;
        }
        var request = new WorkflowConnectionRequestedEventArgs(ConnectionRequestedEvent, from, fromPort, to, toPort);
        RaiseEvent(request);
        if (request.Cancel)
        {
            Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceRefused, request.Reason ?? string.Empty));
            return false;
        }
        WorkflowEdge edge = null!;
        ChangeGraph(() => edge = graph.Connect(from, fromPort, to, toPort));
        if (_pickedEdge is { } picked && _pickedChange is { } removed)
        {
            graph.History.Record(new WorkflowCompositeChange([removed, WorkflowRemoveChange.Adding(graph, edge)]));
            RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, WorkflowEditKind.Disconnect, [], [picked]));
        }
        else
        {
            graph.History.Record(WorkflowRemoveChange.Adding(graph, edge));
        }
        Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceConnected, from.Title, to.Title));
        RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, WorkflowEditKind.Connect, [from, to], [edge]));
        return true;
    }

    // Connecting with the keyboard ---------------------------------------------------------------------------------------

    /// <summary>C: choose an output (when there are several), then a step to connect it to.</summary>
    private void BeginKeyConnect(WorkflowNode node)
    {
        _keyConnect = true;
        _connectFrom = node;
        _keyPorts = node.Outputs.Count > 0 ? node.Outputs.Cast<WorkflowPort?>().ToList() : [null];
        if (_keyPorts.Count > 1)
        {
            _keyPortIndex = 0;
            _connectFromPort = _keyPorts[0];
            Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceConnectPort, _keyPorts[0]?.Label ?? _keyPorts[0]?.Id));
            UpdatePorts();
            return;
        }
        _connectFromPort = _keyPorts[0];
        ChooseKeyTarget();
    }

    private void ChooseKeyTarget()
    {
        _keyPortIndex = -1;
        _keyTargets = [];
        foreach (var node in ReadingOrder())
        {
            if (!ReferenceEquals(node, _connectFrom) && FirstValidInput(node, out var port, out _))
            {
                _keyTargets.Add((node, port));
            }
        }
        if (_keyTargets.Count == 0)
        {
            Announce(WorkflowText.Get(WorkflowLangResourceKind.AnnounceConnectNoTarget));
            EndConnect();
            return;
        }
        // Start at the first step after the source in reading order.
        var order = ReadingOrder();
        var after = order.IndexOf(_connectFrom!);
        _keyTargetIndex = Math.Max(0, _keyTargets.FindIndex(t => order.IndexOf(t.Node) > after));
        _connectTarget = _keyTargets[_keyTargetIndex];
        Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceConnectMode, _connectFrom!.Title, _connectTarget.Value.Node.Title));
        ScrollIntoView(_connectTarget.Value.Node);
        UpdatePorts();
    }

    /// <summary>The keys of the connect mode; it takes every key until it ends.</summary>
    private void HandleKeyConnect(KeyEventArgs e)
    {
        var next = e.Key is Key.Right or Key.Down;
        var previous = e.Key is Key.Left or Key.Up;
        if (_keyPortIndex >= 0)
        {
            if (next || previous)
            {
                _keyPortIndex = (_keyPortIndex + (next ? 1 : -1) + _keyPorts.Count) % _keyPorts.Count;
                _connectFromPort = _keyPorts[_keyPortIndex];
                Announce(WorkflowText.Format(WorkflowLangResourceKind.AnnounceConnectPort, _connectFromPort?.Label ?? _connectFromPort?.Id));
                UpdatePorts();
            }
            else if (e.Key == Key.Enter)
            {
                ChooseKeyTarget();
            }
            else if (e.Key == Key.Escape)
            {
                EndConnect();
                Announce(WorkflowText.Get(WorkflowLangResourceKind.AnnounceConnectCancelled));
            }
            return;
        }
        if (next || previous)
        {
            _keyTargetIndex = (_keyTargetIndex + (next ? 1 : -1) + _keyTargets.Count) % _keyTargets.Count;
            _connectTarget = _keyTargets[_keyTargetIndex];
            Announce(_connectTarget.Value.Node.Title);
            ScrollIntoView(_connectTarget.Value.Node);
            UpdatePorts();
        }
        else if (e.Key == Key.Enter && _connectTarget is { } target && _connectFrom is { } from)
        {
            TryConnect(from, _connectFromPort, target.Node, target.Port);
            EndConnect();
        }
        else if (e.Key == Key.Escape)
        {
            EndConnect();
            Announce(WorkflowText.Get(WorkflowLangResourceKind.AnnounceConnectCancelled));
        }
    }
}

/// <summary>A connection a person is making, for <see cref="WorkflowCanvas.ConnectionRequested"/>.</summary>
public sealed class WorkflowConnectionRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WorkflowConnectionRequestedEventArgs(RoutedEvent routedEvent, WorkflowNode source, WorkflowPort? sourcePort,
                                                WorkflowNode target, WorkflowPort? targetPort)
        : base(routedEvent)
    {
        SourceNode = source;
        SourcePort = sourcePort;
        TargetNode = target;
        TargetPort = targetPort;
    }

    /// <summary>The step the data would leave.</summary>
    public WorkflowNode SourceNode { get; }

    /// <summary>The output it would leave through; null is the implicit one.</summary>
    public WorkflowPort? SourcePort { get; }

    /// <summary>The step the data would reach.</summary>
    public WorkflowNode TargetNode { get; }

    /// <summary>The input it would come in through; null is the implicit one.</summary>
    public WorkflowPort? TargetPort { get; }

    /// <summary>Set to true to refuse the connection.</summary>
    public bool Cancel { get; set; }

    /// <summary>Why it was refused, said to the person (screen readers hear it).</summary>
    public string? Reason { get; set; }
}

/// <summary>A connection dropped on the empty canvas, for <see cref="WorkflowCanvas.ConnectionDroppedOnCanvas"/>.</summary>
public sealed class WorkflowConnectionDroppedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WorkflowConnectionDroppedEventArgs(RoutedEvent routedEvent, WorkflowNode source, WorkflowPort? sourcePort, Point position)
        : base(routedEvent)
    {
        SourceNode = source;
        SourcePort = sourcePort;
        Position = position;
    }

    /// <summary>The step the connection came from.</summary>
    public WorkflowNode SourceNode { get; }

    /// <summary>The output it came from; null is the implicit one.</summary>
    public WorkflowPort? SourcePort { get; }

    /// <summary>Where it was dropped, in canvas units (a good place for a new step's <see cref="WorkflowNode.Position"/>).</summary>
    public Point Position { get; }
}
