using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Shows a <see cref="WorkflowGraph"/> on a dotted canvas (visual rules from Arc's free hero-section block, HeroRelay):
/// one card per step, edges between them, and a run played out step by step. Set the steps' states as your run goes;
/// when a step finishes, the data draws along its edges in the accent before the next step shows as running, and the
/// edges of a branch the run did not take turn dashed. Steps without a position are arranged in ranks; by default the
/// whole workflow shows at actual size and shrinks like a screenshot when it does not fit. Pan with a drag or the wheel,
/// zoom with Ctrl / ⌘ + wheel, a pinch or the keyboard, and move between steps with the arrow keys.
/// </summary>
[TemplatePart(GridPart, typeof(WorkflowGridLayer))]
[TemplatePart(WorldPart, typeof(WorkflowWorldPanel))]
[TemplatePart(EdgesPart, typeof(WorkflowEdgeLayer))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[TemplatePart(OverlayPart, typeof(WorkflowOverlayLayer))]
[TemplatePart(PortsPart, typeof(WorkflowPortLayer))]
[PseudoClasses(NarrowPseudoClass, PanningPseudoClass)]
public partial class WorkflowCanvas : TemplatedControl
{
    internal const string GridPart = "PART_Grid";
    internal const string WorldPart = "PART_World";
    internal const string EdgesPart = "PART_Edges";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string OverlayPart = "PART_Overlay";
    internal const string PortsPart = "PART_Ports";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string PanningPseudoClass = ":panning";

    /// <summary>The workflow to show.</summary>
    public static readonly StyledProperty<WorkflowGraph?> GraphProperty =
        AvaloniaProperty.Register<WorkflowCanvas, WorkflowGraph?>(nameof(Graph));

    /// <summary>
    /// How the workflow fits the canvas (default <see cref="WorkflowAutoFit.ShrinkToFit"/>: actual size, centered,
    /// shrinking when it does not fit). Panning, zooming or setting <see cref="Zoom"/> or <see cref="Offset"/> stops
    /// fitting until <see cref="FitToContent"/>. In a container with unlimited height the canvas is as tall as the fitted
    /// workflow.
    /// </summary>
    public static readonly StyledProperty<WorkflowAutoFit> AutoFitProperty =
        AvaloniaProperty.Register<WorkflowCanvas, WorkflowAutoFit>(nameof(AutoFit), WorkflowAutoFit.ShrinkToFit);

    /// <summary>Screen pixels per canvas unit; kept within <see cref="MinZoom"/> and <see cref="MaxZoom"/>.</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<WorkflowCanvas, double>(nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceZoom);

    /// <summary>The canvas point shown at the top-left corner of the view.</summary>
    public static readonly StyledProperty<Point> OffsetProperty =
        AvaloniaProperty.Register<WorkflowCanvas, Point>(nameof(Offset), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The smallest zoom (default 0.25).</summary>
    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<WorkflowCanvas, double>(nameof(MinZoom), 0.25);

    /// <summary>The largest zoom (default 2).</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<WorkflowCanvas, double>(nameof(MaxZoom), 2);

    /// <summary>
    /// Whether people can pan and zoom (default true): dragging pans, the wheel works per <see cref="WheelMode"/>, a
    /// pinch zooms, and the + / − keys zoom. Turn it off for a workflow shown inside a scrolling page that should only
    /// fit (Arc's hero). <see cref="ZoomTo"/>, <see cref="FitToContent"/> and <see cref="ScrollIntoView"/> work either way.
    /// </summary>
    public static readonly StyledProperty<bool> IsPanZoomEnabledProperty =
        AvaloniaProperty.Register<WorkflowCanvas, bool>(nameof(IsPanZoomEnabled), true);

    /// <summary>
    /// What the wheel does (default <see cref="Gestures.WheelMode.Pan"/>: scrolling pans, Ctrl / ⌘ + wheel zooms;
    /// <see cref="Gestures.WheelMode.ModifierZoom"/> leaves plain scrolling to the page).
    /// </summary>
    public static readonly StyledProperty<WheelMode> WheelModeProperty =
        AvaloniaProperty.Register<WorkflowCanvas, WheelMode>(nameof(WheelMode), WheelMode.Pan);

    /// <summary>Which way the workflow runs: down (default, as in Arc) or across. Edges and arranging follow it.</summary>
    public static readonly StyledProperty<WorkflowLayoutDirection> LayoutDirectionProperty =
        AvaloniaProperty.Register<WorkflowCanvas, WorkflowLayoutDirection>(nameof(LayoutDirection));

    /// <summary>How edges are drawn: curves (default, as in Arc), right angles with rounded corners, or straight lines.</summary>
    public static readonly StyledProperty<WorkflowEdgeStyle> EdgeStyleProperty =
        AvaloniaProperty.Register<WorkflowCanvas, WorkflowEdgeStyle>(nameof(EdgeStyle));

    /// <summary>
    /// Below this width (default 520, as in Arc; 0 never) the canvas is narrow: cards shrink to 344 × 52 and 108 × 72,
    /// and a workflow the canvas arranged itself is arranged again with tighter spacing, so it stays readable on a phone
    /// instead of shrinking like a screenshot. Steps placed by your code keep their places.
    /// </summary>
    public static readonly StyledProperty<double> NarrowBelowWidthProperty =
        AvaloniaProperty.Register<WorkflowCanvas, double>(nameof(NarrowBelowWidth), 520);

    /// <summary>Whether the dot grid shows (default true).</summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<WorkflowCanvas, bool>(nameof(ShowGrid), true);

    /// <summary>
    /// Whether a step that starts while the data is still drawing toward it keeps showing as waiting until the data
    /// arrives (default true), so a run reads step, edge, step as in Arc even when your run sets the next step running
    /// at once. Only what is shown waits; the steps' states are untouched. Full motion only.
    /// </summary>
    public static readonly StyledProperty<bool> QueueRunVisualsProperty =
        AvaloniaProperty.Register<WorkflowCanvas, bool>(nameof(QueueRunVisuals), true);

    /// <summary>Raised when a step is opened: double-clicked, or Enter pressed on its card. Open its settings here.</summary>
    public static readonly RoutedEvent<WorkflowNodeEventArgs> NodeActivatedEvent =
        RoutedEvent.Register<WorkflowCanvas, WorkflowNodeEventArgs>(nameof(NodeActivated), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowCanvas, IBrush?>(nameof(AccentBrush));

    internal static readonly StyledProperty<double> CanvasAccentShareProperty =
        AvaloniaProperty.Register<WorkflowCanvas, double>(nameof(CanvasAccentShare), 0.02);

    internal static readonly StyledProperty<IBrush?> CanvasBrushProperty =
        AvaloniaProperty.Register<WorkflowCanvas, IBrush?>(nameof(CanvasBrush));

    internal static readonly StyledProperty<double> ZoomStepProperty =
        AvaloniaProperty.Register<WorkflowCanvas, double>(nameof(ZoomStep), 1.25);

    private readonly Dictionary<WorkflowNode, WorkflowNodeCard> _cards = new();
    private readonly Dictionary<WorkflowEdge, WorkflowEdgeVisual> _edges = new();
    private readonly Dictionary<WorkflowNode, List<WorkflowEdgeVisual>> _incoming = new();
    private WorkflowGridLayer? _grid;
    private WorkflowWorldPanel? _world;
    private WorkflowEdgeLayer? _edgeLayer;
    private TextBlock? _announcer;
    private WorkflowPortLayer? _portLayer;
    private WorkflowGraph? _observed;
    private bool _updatingRun;
    private bool _runActive;
    private string? _defaultName;

    static WorkflowCanvas()
    {
        AffectsMeasure<WorkflowCanvas>(AutoFitProperty, LayoutDirectionProperty, NarrowBelowWidthProperty);
        AffectsArrange<WorkflowCanvas>(MinZoomProperty, MaxZoomProperty, EdgeStyleProperty);
    }

    /// <summary>Creates an empty canvas; set <see cref="Graph"/>.</summary>
    public WorkflowCanvas()
    {
        ArcTokens.Bind(this, AccentBrushProperty, ArcDesignTokenKind.Accent);
        InitializeInput();
        NodeTemplates.CollectionChanged += (_, _) => UpdateTemplates();
    }

    /// <summary>
    /// Templates for the cards' content, tried in order: the first that matches a step (a
    /// <see cref="WorkflowNodeTemplate"/> matches its <see cref="WorkflowNode.Kind"/>) replaces the card's icon, title,
    /// subtitle and state; the frame, the run's colors, the selection, the focus ring and the ports stay. Steps no
    /// template matches keep the default card. Controls in a card (text boxes, switches) keep their own pointer and keys.
    /// </summary>
    public DataTemplates NodeTemplates { get; } = new();

    /// <summary>The first of <see cref="NodeTemplates"/> that matches <paramref name="node"/>, or null.</summary>
    private IDataTemplate? TemplateFor(WorkflowNode node)
    {
        foreach (var template in NodeTemplates)
        {
            if (template.Match(node))
            {
                return template;
            }
        }
        return null;
    }

    private void UpdateTemplates()
    {
        foreach (var (node, card) in _cards)
        {
            card.ContentTemplate = TemplateFor(node);
        }
    }

    /// <inheritdoc cref="GraphProperty"/>
    public WorkflowGraph? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <inheritdoc cref="AutoFitProperty"/>
    public WorkflowAutoFit AutoFit
    {
        get => GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <inheritdoc cref="OffsetProperty"/>
    public Point Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <inheritdoc cref="MinZoomProperty"/>
    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    /// <inheritdoc cref="MaxZoomProperty"/>
    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    /// <inheritdoc cref="IsPanZoomEnabledProperty"/>
    public bool IsPanZoomEnabled
    {
        get => GetValue(IsPanZoomEnabledProperty);
        set => SetValue(IsPanZoomEnabledProperty, value);
    }

    /// <inheritdoc cref="WheelModeProperty"/>
    public WheelMode WheelMode
    {
        get => GetValue(WheelModeProperty);
        set => SetValue(WheelModeProperty, value);
    }

    /// <inheritdoc cref="LayoutDirectionProperty"/>
    public WorkflowLayoutDirection LayoutDirection
    {
        get => GetValue(LayoutDirectionProperty);
        set => SetValue(LayoutDirectionProperty, value);
    }

    /// <inheritdoc cref="EdgeStyleProperty"/>
    public WorkflowEdgeStyle EdgeStyle
    {
        get => GetValue(EdgeStyleProperty);
        set => SetValue(EdgeStyleProperty, value);
    }

    /// <inheritdoc cref="NarrowBelowWidthProperty"/>
    public double NarrowBelowWidth
    {
        get => GetValue(NarrowBelowWidthProperty);
        set => SetValue(NarrowBelowWidthProperty, value);
    }

    /// <inheritdoc cref="ShowGridProperty"/>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <inheritdoc cref="QueueRunVisualsProperty"/>
    public bool QueueRunVisuals
    {
        get => GetValue(QueueRunVisualsProperty);
        set => SetValue(QueueRunVisualsProperty, value);
    }

    /// <inheritdoc cref="NodeActivatedEvent"/>
    public event EventHandler<WorkflowNodeEventArgs>? NodeActivated
    {
        add => AddHandler(NodeActivatedEvent, value);
        remove => RemoveHandler(NodeActivatedEvent, value);
    }

    internal IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    internal double CanvasAccentShare
    {
        get => GetValue(CanvasAccentShareProperty);
        set => SetValue(CanvasAccentShareProperty, value);
    }

    internal IBrush? CanvasBrush
    {
        get => GetValue(CanvasBrushProperty);
        set => SetValue(CanvasBrushProperty, value);
    }

    internal double ZoomStep
    {
        get => GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    /// <summary>The card of <paramref name="node"/>, or null when the step is not on this canvas.</summary>
    public WorkflowNodeCard? CardFor(WorkflowNode node) => _cards.GetValueOrDefault(node);

    /// <summary>Focuses a step's card as the arrow keys do (tests).</summary>
    internal bool FocusNodeForTest(WorkflowNode node) => FocusNode(node);

    /// <summary>The drawn edges (tests).</summary>
    internal WorkflowEdgeVisual? EdgeVisualFor(WorkflowEdge edge) => _edges.GetValueOrDefault(edge);

    /// <summary>The last announcement for screen readers (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Text;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // A new template has new layers: drop the cards and edge visuals of the old one.
        _world?.Children.Clear();
        _cards.Clear();
        foreach (var visual in _edges.Values)
        {
            visual.Progress?.Dispose();
        }
        _edges.Clear();
        _grid = e.NameScope.Find<WorkflowGridLayer>(GridPart);
        _world = e.NameScope.Find<WorkflowWorldPanel>(WorldPart);
        _edgeLayer = e.NameScope.Find<WorkflowEdgeLayer>(EdgesPart);
        _announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        _overlay = e.NameScope.Find<WorkflowOverlayLayer>(OverlayPart);
        _portLayer = e.NameScope.Find<WorkflowPortLayer>(PortsPart);
        if (_portLayer is not null)
        {
            // Above the cards (dragged cards rise to 1).
            _portLayer.ZIndex = 2;
        }
        if (_world is not null)
        {
            _world.RenderTransformOrigin = RelativePoint.TopLeft;
            _world.Locate = DisplayPosition;
            _world.ShouldRealize = ShouldRealize;
            // A card that grows or shrinks (content from a template) moves its edges and changes the fit.
            _world.SizesChanged = InvalidateMeasure;
            // One Tab stop for all cards; the arrow keys move between them.
            KeyboardNavigation.SetTabNavigation(_world, KeyboardNavigationMode.Once);
        }
        if (_grid is not null)
        {
            _grid.IsVisible = ShowGrid;
        }
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Graph);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A workflow can outlive the canvas that shows it: stop listening so it does not keep the canvas alive.
        Observe(null);
        EndPan();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // The focused card shows its ports on an editable canvas.
        UpdatePorts();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GraphProperty || change.Property == IsEditableProperty || change.Property == MinZoomProperty ||
            change.Property == MaxZoomProperty)
        {
            RefreshCommands();
        }
        if (change.Property == GraphProperty)
        {
            if (VisualRoot is not null)
            {
                Observe(Graph);
            }
            _manualView = false;
            _autoPlaced.Clear();
            Rebuild();
        }
        else if (change.Property == ZoomProperty || change.Property == OffsetProperty)
        {
            if (!_settingView)
            {
                _manualView = true;
                StopViewAnimation();
                ApplyView(new PanZoomTransform(Offset, Zoom));
            }
        }
        else if (change.Property == MinZoomProperty || change.Property == MaxZoomProperty)
        {
            CoerceValue(ZoomProperty);
        }
        else if (change.Property == ShowGridProperty)
        {
            if (_grid is not null)
            {
                _grid.IsVisible = ShowGrid;
            }
        }
        else if (change.Property == QueueRunVisualsProperty)
        {
            UpdateRun();
        }
        else if (change.Property == LayoutDirectionProperty)
        {
            // Arranged steps follow the new direction; steps your code placed keep their places.
            if (Graph is { } graph && AllAutoPlaced(graph))
            {
                ArrangeNodes(animate: false);
            }
        }
        else if (change.Property == IsPanZoomEnabledProperty && !IsPanZoomEnabled)
        {
            EndPan();
        }
        else if (change.Property == IsEditableProperty)
        {
            if (!IsEditable)
            {
                CancelMove();
                CancelConnect();
                EndPan();
            }
            UpdateName();
            UpdatePorts();
        }
        else if (change.Property == BackgroundProperty || change.Property == AccentBrushProperty ||
                 change.Property == CanvasAccentShareProperty)
        {
            UpdateCanvasBrush();
        }
    }

    private static double CoerceZoom(AvaloniaObject target, double value)
    {
        var canvas = (WorkflowCanvas)target;
        if (!double.IsFinite(value) || value <= 0)
        {
            return 1;
        }
        return Math.Clamp(value, canvas.MinZoom, Math.Max(canvas.MinZoom, canvas.MaxZoom));
    }

    // Graph ------------------------------------------------------------------------------------------------------------

    // The steps and edges this canvas listens to. Kept apart from the workflow's lists: a step taken out of the workflow stays
    // in its undo history, so the canvas must stop listening to it by what it subscribed to, not by what the workflow holds.
    private readonly HashSet<WorkflowNode> _watchedNodes = new();
    private readonly HashSet<WorkflowEdge> _watchedEdges = new();

    private void Observe(WorkflowGraph? graph)
    {
        if (ReferenceEquals(_observed, graph))
        {
            return;
        }
        if (_observed is { } old)
        {
            old.Nodes.CollectionChanged -= OnNodesChanged;
            old.Edges.CollectionChanged -= OnEdgesChanged;
            old.History.Changed -= OnHistoryChanged;
        }
        _observed = graph;
        if (graph is not null)
        {
            graph.Nodes.CollectionChanged += OnNodesChanged;
            graph.Edges.CollectionChanged += OnEdgesChanged;
            graph.History.Changed += OnHistoryChanged;
        }
        SyncWatched();
    }

    /// <summary>Listens to exactly the steps and edges of the observed workflow, and to nothing that left it.</summary>
    private void SyncWatched()
    {
        var nodes = _observed?.Nodes.ToHashSet() ?? [];
        foreach (var node in _watchedNodes.Where(n => !nodes.Contains(n)).ToList())
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            _watchedNodes.Remove(node);
        }
        foreach (var node in nodes)
        {
            if (_watchedNodes.Add(node))
            {
                node.PropertyChanged += OnNodePropertyChanged;
            }
        }
        var edges = _observed?.Edges.ToHashSet() ?? [];
        foreach (var edge in _watchedEdges.Where(e => !edges.Contains(e)).ToList())
        {
            edge.PropertyChanged -= OnEdgePropertyChanged;
            _watchedEdges.Remove(edge);
        }
        foreach (var edge in edges)
        {
            if (_watchedEdges.Add(edge))
            {
                edge.PropertyChanged += OnEdgePropertyChanged;
            }
        }
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncWatched();
        Rebuild();
    }

    private void OnEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnNodesChanged(sender, e);
        // A new connection changes the ranks of a workflow the canvas arranged (not while a person edits it).
        if (!_changingGraph && Graph is { } graph && graph.Nodes.Count > 0 && AllAutoPlaced(graph))
        {
            ArrangeNodes();
        }
    }

    private void OnNodePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WorkflowNode.StateProperty)
        {
            if (sender is WorkflowNode { State: WorkflowNodeState.Failed } node)
            {
                Announce(string.IsNullOrEmpty(node.Error)
                    ? WorkflowText.Format(Localization.WorkflowLangResourceKind.AnnounceFailed, node.Title)
                    : WorkflowText.Format(Localization.WorkflowLangResourceKind.AnnounceFailedReason, node.Title, node.Error));
            }
            UpdateRun();
        }
        else if (e.Property == WorkflowNode.PositionProperty)
        {
            if (!_placing && sender is WorkflowNode moved)
            {
                // Your code placed the step: the canvas no longer arranges it.
                _autoPlaced.Remove(moved);
                InvalidateMeasure();
                _world?.InvalidateArrange();
            }
        }
        else if (e.Property == WorkflowNode.SizeProperty || e.Property == WorkflowNode.AppearanceProperty)
        {
            InvalidateMeasure();
        }
        else if (e.Property == WorkflowNode.KindProperty && sender is WorkflowNode node && _cards.TryGetValue(node, out var card))
        {
            card.ContentTemplate = TemplateFor(node);
        }
    }

    private void OnEdgePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WorkflowEdge.StateProperty)
        {
            UpdateRun();
        }
    }

    /// <summary>Matches the cards and edge visuals to the workflow (keeps the existing ones, so a run keeps its drawing).</summary>
    private void Rebuild()
    {
        if (_world is null || _edgeLayer is null)
        {
            return;
        }
        var graph = Graph;
        var nodes = graph?.Nodes.ToHashSet() ?? [];
        foreach (var (node, card) in _cards.ToList())
        {
            if (!nodes.Contains(node))
            {
                _world.Children.Remove(card);
                _cards.Remove(node);
                _autoPlaced.Remove(node);
            }
        }
        if (graph is not null)
        {
            foreach (var node in graph.Nodes)
            {
                if (!_cards.ContainsKey(node))
                {
                    var card = new WorkflowNodeCard { Node = node, Focusable = true, ContentTemplate = TemplateFor(node) };
                    card.SetNarrow(_narrow);
                    card.SetSelected(_selection.Contains(node));
                    _cards[node] = card;
                    _world.Children.Add(card);
                }
            }
        }

        var edges = graph?.Edges.ToHashSet() ?? [];
        foreach (var (edge, visual) in _edges.ToList())
        {
            if (!edges.Contains(edge))
            {
                visual.Progress?.Dispose();
                _edges.Remove(edge);
            }
        }
        _edgeLayer.Edges.Clear();
        _incoming.Clear();
        if (graph is not null)
        {
            foreach (var edge in graph.Edges)
            {
                if (!_edges.TryGetValue(edge, out var visual))
                {
                    visual = new WorkflowEdgeVisual(edge);
                    var layer = _edgeLayer;
                    visual.Progress = new MotionValue<double>(layer, 0, MotionVectors.Double);
                    visual.Progress.Changed += value =>
                    {
                        layer.InvalidateVisual();
                        // The frame the data arrives, the step it reached may show its real state.
                        if (value >= 1 && visual.Lit)
                        {
                            UpdateRun();
                        }
                    };
                    _edges[edge] = visual;
                }
                _edgeLayer.Edges.Add(visual);
                if (!_incoming.TryGetValue(edge.Target, out var list))
                {
                    _incoming[edge.Target] = list = [];
                }
                list.Add(visual);
            }
        }
        PruneSelection();
        _runActive = graph?.Nodes.Any(n => n.State is WorkflowNodeState.Pending or WorkflowNodeState.Running) ?? false;
        UpdateRun();
        UpdateName();
        InvalidateMeasure();
        _world.InvalidateMeasure();
    }

    // Run ----------------------------------------------------------------------------------------------------------------

    /// <summary>Derives every edge's look from its steps, starts the data drawing where it newly went, and holds back steps it has not reached yet.</summary>
    private void UpdateRun()
    {
        SceneChanged?.Invoke();
        // A drawing that completes while this runs (a jump to the end) is picked up by the card pass below.
        if (_edgeLayer is null || _updatingRun)
        {
            return;
        }
        _updatingRun = true;
        try
        {
            UpdateRunCore(_edgeLayer);
        }
        finally
        {
            _updatingRun = false;
        }
        AnnounceRunEnd();
    }

    private void UpdateRunCore(WorkflowEdgeLayer edgeLayer)
    {
        var animate = IsLoaded && ArcMotion.IsFull(this);
        foreach (var visual in edgeLayer.Edges)
        {
            var (lit, skipped) = Derive(visual.Edge);
            visual.Skipped = skipped;
            if (lit == visual.Lit)
            {
                continue;
            }
            visual.Lit = lit;
            if (!lit)
            {
                visual.Progress?.Jump(0);
            }
            else if (animate && visual.Progress is { } progress)
            {
                progress.Jump(0);
                var duration = edgeLayer.FlowDuration;
                _ = progress.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.InOut));
            }
            else
            {
                visual.Progress?.Jump(1);
            }
        }
        var queue = QueueRunVisuals && animate;
        foreach (var (node, card) in _cards)
        {
            WorkflowNodeState? held = null;
            if (queue && node.State is WorkflowNodeState.Running or WorkflowNodeState.Done or WorkflowNodeState.Failed &&
                _incoming.TryGetValue(node, out var incoming) &&
                incoming.Exists(v => v.Lit && v.Progress is { } p && p.Value < 1))
            {
                held = WorkflowNodeState.Pending;
            }
            card.SetHostState(held);
        }
        edgeLayer.InvalidateVisual();
    }

    /// <summary>Lit (the data went along it) and skipped, from the edge's own state or its two steps.</summary>
    internal static (bool Lit, bool Skipped) Derive(WorkflowEdge edge)
    {
        switch (edge.State)
        {
            case WorkflowEdgeState.Idle:
                return (false, false);
            case WorkflowEdgeState.Active:
                return (true, false);
            case WorkflowEdgeState.Skipped:
                return (false, true);
        }
        var source = edge.Source.State;
        var target = edge.Target.State;
        if (source == WorkflowNodeState.Skipped || target == WorkflowNodeState.Skipped)
        {
            return (false, true);
        }
        var lit = source == WorkflowNodeState.Done &&
                  target is WorkflowNodeState.Running or WorkflowNodeState.Done or WorkflowNodeState.Failed;
        return (lit, false);
    }

    /// <summary>When the last waiting or running step settles, tells screen readers how the run ended.</summary>
    private void AnnounceRunEnd()
    {
        if (Graph is not { } graph)
        {
            return;
        }
        var active = graph.Nodes.Any(n => n.State is WorkflowNodeState.Pending or WorkflowNodeState.Running);
        if (_runActive && !active && graph.Nodes.Any(n => n.State != WorkflowNodeState.Idle))
        {
            Announce(WorkflowText.Format(Localization.WorkflowLangResourceKind.AnnounceFinished,
                graph.Nodes.Count(n => n.State == WorkflowNodeState.Done),
                graph.Nodes.Count(n => n.State == WorkflowNodeState.Failed),
                graph.Nodes.Count(n => n.State == WorkflowNodeState.Skipped)));
        }
        _runActive = active;
    }

    /// <summary>Says <paramref name="text"/> through the hidden live region (polite).</summary>
    internal void Announce(string text)
    {
        if (_announcer is null)
        {
            return;
        }
        _announcer.Text = text;
        AutomationProperties.SetName(_announcer, text);
    }

    // Appearance ---------------------------------------------------------------------------------------------------------

    private void UpdateCanvasBrush()
    {
        // Arc: the page background with 2% of the accent (color-mix in oklab).
        if (Background is ISolidColorBrush background && AccentBrush is ISolidColorBrush accent)
        {
            CanvasBrush = new ImmutableSolidColorBrush(Oklch.MixOklab(background.Color, accent.Color, CanvasAccentShare));
        }
        else
        {
            CanvasBrush = Background;
        }
    }

    /// <summary>"Workflow, 6 steps" unless the app named the canvas, and the keys in the help text.</summary>
    private void UpdateName()
    {
        AutomationProperties.SetHelpText(this, WorkflowText.Get(IsEditable
            ? Localization.WorkflowLangResourceKind.CanvasHelpEditable
            : Localization.WorkflowLangResourceKind.CanvasHelp));
        var current = AutomationProperties.GetName(this);
        if (current is not null && current != _defaultName)
        {
            return;
        }
        _defaultName = WorkflowText.CanvasName(Graph?.Nodes.Count ?? 0);
        AutomationProperties.SetName(this, _defaultName);
    }

    /// <summary>The control that hosts the view (the world's parent), for pointer positions and the view's size.</summary>
    private Visual? ViewportHost => _world?.GetVisualParent();
}

/// <summary>A step of a <see cref="WorkflowCanvas"/>, for <see cref="WorkflowCanvas.NodeActivated"/>.</summary>
public sealed class WorkflowNodeEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="node"/>.</summary>
    public WorkflowNodeEventArgs(RoutedEvent routedEvent, WorkflowNode node)
        : base(routedEvent)
    {
        Node = node;
    }

    /// <summary>The step.</summary>
    public WorkflowNode Node { get; }
}
