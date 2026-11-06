using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Raised after a resize of a <see cref="ResizablePanels"/> group settles.</summary>
public sealed class ResizablePanelsLayoutChangedEventArgs : RoutedEventArgs
{
    public ResizablePanelsLayoutChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<double> sizes) : base(routedEvent) => Sizes = sizes;

    /// <summary>Each pane's share of the group in percent (0 for hidden panes).</summary>
    public IReadOnlyList<double> Sizes { get; }
}

/// <summary>
/// Arc resizable panels: a split view whose panes, side by side or stacked (<see cref="Orientation"/>), trade space when
/// the dividers between them are dragged, the way a mail or code editor window works. Dividers follow the pointer 1:1, resist past a pane's limits (rubber
/// band up to 44px) and snap collapsible panes closed when dragged well past their minimum or flicked toward the edge;
/// on release the panes settle on the smooth spring keeping the pointer's velocity. Each divider is focusable: arrows
/// (Left / Right, or Up / Down when vertical) resize by 16px (Shift 64px) on the snappy spring, Home / End jump to the limits, Enter hides or restores a
/// collapsible neighbour, Escape during a drag puts the divider back, and a double-click resets the layout. The group
/// needs a definite height, whichever the orientation. <see cref="Sizes"/> (two-way, percent) persists the layout.
/// </summary>
[PseudoClasses(ResizingPseudoClass, VerticalPseudoClass)]
public class ResizablePanels : Control
{
    public const string ResizingPseudoClass = ":resizing";

    public const string VerticalPseudoClass = ":vertical";

    #region Properties

    /// <summary>Names the group for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ResizablePanels, string?>(nameof(Label));

    /// <summary>
    /// Each pane's share in percent. Updated after every resize settles; set it (for example from saved settings)
    /// to restore a layout. Two-way.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> SizesProperty =
        AvaloniaProperty.Register<ResizablePanels, IReadOnlyList<double>?>(nameof(Sizes), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Horizontal (default) lays the panes out left to right with vertical dividers; Vertical stacks them top to bottom
    /// with horizontal dividers, sizes, limits and shares then being heights and the arrow keys Up / Down.
    /// </summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ResizablePanels, Orientation>(nameof(Orientation));

    /// <summary>
    /// Space in px between adjacent panes (default 1: the one pixel divider line). A wider gap shows no line at rest;
    /// the line and grip appear centred in it while the divider is hovered, focused or dragged, and the whole gap grabs.
    /// </summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ResizablePanels, double>(nameof(Gap), 1, coerce: (_, value) => double.IsFinite(value) ? Math.Max(0, value) : 1);

    /// <summary>Raised with each pane's share in percent after a resize settles.</summary>
    public static readonly RoutedEvent<ResizablePanelsLayoutChangedEventArgs> LayoutChangedEvent =
        RoutedEvent.Register<ResizablePanels, ResizablePanelsLayoutChangedEventArgs>(nameof(LayoutChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<IBrush?> HandleLineBrushProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(HandleLineBrush));
    internal static readonly StyledProperty<IBrush?> HandleLineHotBrushProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(HandleLineHotBrush));
    internal static readonly StyledProperty<IBrush?> GripBrushProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(GripBrush));
    internal static readonly StyledProperty<IBrush?> GripActiveBrushProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(GripActiveBrush));
    internal static readonly StyledProperty<IBrush?> RestoreFillProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(RestoreFill));
    internal static readonly StyledProperty<IBrush?> RestoreStrokeProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(RestoreStroke));
    internal static readonly StyledProperty<IBrush?> RestoreStrokeHotProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(RestoreStrokeHot));
    internal static readonly StyledProperty<IBrush?> RestoreGlyphProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(RestoreGlyph));
    internal static readonly StyledProperty<IBrush?> RestoreGlyphHotProperty =
        AvaloniaProperty.Register<ResizablePanels, IBrush?>(nameof(RestoreGlyphHot));
    internal static readonly StyledProperty<BoxShadows> RestoreShadowProperty =
        AvaloniaProperty.Register<ResizablePanels, BoxShadows>(nameof(RestoreShadow));

    /// <summary>
    /// The panes, in order. Panes can be added and removed at run time: once the group has been laid out, the panes
    /// that stay keep their current size, a removed pane's space goes to its neighbours in proportion to their sizes, a
    /// new pane gets a size from its <see cref="ResizablePanel.DefaultSize"/> relative to the average of the panes that
    /// stay, and <see cref="Sizes"/> is written once. Such changes jump, without animation.
    /// </summary>
    [Content]
    public AvaloniaList<ResizablePanel> Panels { get; } = new();

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="SizesProperty"/>
    public IReadOnlyList<double>? Sizes { get => GetValue(SizesProperty); set => SetValue(SizesProperty, value); }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Raised with each pane's share in percent after a resize settles.</summary>
    public event EventHandler<ResizablePanelsLayoutChangedEventArgs>? LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }

    internal IBrush? HandleLineBrush { get => GetValue(HandleLineBrushProperty); set => SetValue(HandleLineBrushProperty, value); }
    internal IBrush? HandleLineHotBrush { get => GetValue(HandleLineHotBrushProperty); set => SetValue(HandleLineHotBrushProperty, value); }
    internal IBrush? GripBrush { get => GetValue(GripBrushProperty); set => SetValue(GripBrushProperty, value); }
    internal IBrush? GripActiveBrush { get => GetValue(GripActiveBrushProperty); set => SetValue(GripActiveBrushProperty, value); }
    internal IBrush? RestoreFill { get => GetValue(RestoreFillProperty); set => SetValue(RestoreFillProperty, value); }
    internal IBrush? RestoreStroke { get => GetValue(RestoreStrokeProperty); set => SetValue(RestoreStrokeProperty, value); }
    internal IBrush? RestoreStrokeHot { get => GetValue(RestoreStrokeHotProperty); set => SetValue(RestoreStrokeHotProperty, value); }
    internal IBrush? RestoreGlyph { get => GetValue(RestoreGlyphProperty); set => SetValue(RestoreGlyphProperty, value); }
    internal IBrush? RestoreGlyphHot { get => GetValue(RestoreGlyphHotProperty); set => SetValue(RestoreGlyphHotProperty, value); }
    internal BoxShadows RestoreShadow { get => GetValue(RestoreShadowProperty); set => SetValue(RestoreShadowProperty, value); }

    #endregion

    private readonly List<ResizablePanel> _panes = new();
    private readonly List<ResizablePanelsHandle> _handles = new();
    private MotionValue<double>[] _sizes = [];
    private bool[] _collapsed = [];
    private bool[] _free = [];
    private double?[] _restore = [];
    private double[] _shares = [];
    private double[] _actual = [];
    private double[] _goals = [];
    private bool _hasGoals;
    private bool _stale = true;
    private bool _laidOut;
    private double _room;
    private double _length;
    private int _token;
    private bool _writingSizes;
    private bool _writingCollapsed;
    private DragState? _drag;

    private sealed class DragState
    {
        public required int Handle { get; init; }
        public required IPointer Pointer { get; init; }
        /// <summary>Pointer position along the group's axis (x, or y when vertical) where the drag started.</summary>
        public required double StartX { get; init; }
        public required double A0 { get; init; }
        public required double B0 { get; init; }
        public required double Pair { get; init; }
        public required double Lo { get; init; }
        public required double Hi { get; init; }
        public required ResizablePanelsMode StartMode { get; init; }
        public ResizablePanelsMode Mode { get; set; }
        public bool Moved { get; set; }
        public bool Catching { get; set; }
        public List<(double Time, double X)> Samples { get; } = new(17);
    }

    static ResizablePanels()
    {
        AffectsRender<ResizablePanels>(HandleLineBrushProperty, HandleLineHotBrushProperty, GripBrushProperty, GripActiveBrushProperty,
            RestoreFillProperty, RestoreStrokeProperty, RestoreStrokeHotProperty, RestoreGlyphProperty, RestoreGlyphHotProperty, RestoreShadowProperty);
    }

    public ResizablePanels()
    {
        ClipToBounds = true;
        Panels.CollectionChanged += (_, _) => Rebuild();
    }

    /// <summary>Current size of each pane along the axis in px (tests).</summary>
    internal IReadOnlyList<double> ActualSizes => _actual;

    internal ResizablePanelsAxis Axis => new(Orientation == Orientation.Vertical);

    internal IReadOnlyList<ResizablePanelsHandle> Handles => _handles;

    internal bool IsSettling => _sizes.Any(s => s.IsAnimating);

    #region Structure

    private void Rebuild()
    {
        var old = _panes.ToArray();
        var next = new List<ResizablePanel>(Panels);
        if (_drag is { } drag)
        {
            // The divider being dragged may be about to go; the drag ends where it is.
            _drag = null;
            SetResizing(drag.Handle, false);
            drag.Pointer.Capture(null);
        }
        _token++;
        var carried = Carry(old, next);
        var kept = new HashSet<ResizablePanel>(next);
        for (var i = 0; i < old.Length; i++)
        {
            var pane = old[i];
            if (kept.Contains(pane))
            {
                continue;
            }
            // A pane that leaves the group gets back the state the group put on it.
            pane.ConfigChanged -= OnPaneConfigChanged;
            pane.AnchorEnd = false;
            pane.ClearValue(OpacityProperty);
            ExpandInert.Apply(pane, false);
            _defaultHidden.Remove(pane);
            LogicalChildren.Remove(pane);
            VisualChildren.Remove(pane);
        }
        foreach (var sizes in _sizes)
        {
            sizes.Dispose();
        }
        var previous = new HashSet<ResizablePanel>(old);
        _panes.Clear();
        _panes.AddRange(next);
        var count = _panes.Count;
        _collapsed = new bool[count];
        _free = new bool[count];
        _restore = new double?[count];
        _actual = new double[count];
        _goals = new double[count];
        _hasGoals = false;
        _sizes = new MotionValue<double>[count];
        var initial = carried?.Sizes ?? InitialSizes();
        var axis = Axis;
        for (var i = 0; i < count; i++)
        {
            var pane = _panes[i];
            _collapsed[i] = carried?.Collapsed[i] ?? (pane.IsCollapsible && pane.IsCollapsed);
            _restore[i] = carried?.Restore[i];
            _sizes[i] = new MotionValue<double>(this, initial[i], MotionVectors.Double);
            _sizes[i].Changed += _ => InvalidateMeasure();
            pane.AnchorEnd = i == 0 && count > 1;
            pane.Axis = axis;
            if (!previous.Contains(pane))
            {
                pane.ConfigChanged += OnPaneConfigChanged;
                LogicalChildren.Add(pane);
                VisualChildren.Add(pane);
                if (carried is not null)
                {
                    ExpandInert.Apply(pane, _collapsed[i]);
                }
            }
            pane.InvalidateMeasure();
        }
        _shares = ResizablePanelsMath.ToShares(initial);
        // Dividers are kept by position (a focused one stays focused) and renumbered.
        while (_handles.Count > Math.Max(0, count - 1))
        {
            var handle = _handles[^1];
            _handles.RemoveAt(_handles.Count - 1);
            VisualChildren.Remove(handle);
            LogicalChildren.Remove(handle);
        }
        for (var i = 0; i < count - 1; i++)
        {
            if (i == _handles.Count)
            {
                var handle = new ResizablePanelsHandle(this, i);
                _handles.Add(handle);
                VisualChildren.Add(handle);
                LogicalChildren.Add(handle);
            }
            _handles[i].Index = i;
            _handles[i].SetMode(ModeOf(i));
            _handles[i].InvalidateMeasure();
        }
        _stale = true;
        if (carried is not null)
        {
            // Lay the new set out right away, so a further change before the next layout pass starts from real sizes.
            FitActual(_length);
            WriteSizes();
        }
        InvalidateMeasure();
    }

    private sealed record Carried(double[] Sizes, bool[] Collapsed, double?[] Restore);

    /// <summary>
    /// Sizes for a pane set that changed after layout: panes that stay keep their px size, a removed pane's space (and
    /// its divider's gap) goes to its nearest staying neighbours in proportion to their sizes, and a new pane asks for
    /// its DefaultSize relative to the staying panes' average. Null before the first layout or when no pane stays.
    /// </summary>
    private Carried? Carry(ResizablePanel[] old, List<ResizablePanel> next)
    {
        if (!_laidOut || old.Length == 0 || old.Length != _actual.Length)
        {
            return null;
        }
        var index = new Dictionary<ResizablePanel, int>(old.Length);
        for (var i = 0; i < old.Length; i++)
        {
            index.TryAdd(old[i], i);
        }
        var stays = new bool[old.Length];
        var any = false;
        foreach (var pane in next)
        {
            if (index.TryGetValue(pane, out var i))
            {
                stays[i] = true;
                any = true;
            }
        }
        if (!any)
        {
            return null;
        }
        // A settle in flight counts as finished: the panes start from where they were heading.
        var px = new double[old.Length];
        for (var i = 0; i < old.Length; i++)
        {
            px[i] = _collapsed[i] ? 0 : Math.Max(0, _hasGoals && _goals.Length == old.Length ? _goals[i] : _actual[i]);
        }
        var gained = new double[old.Length];
        var gap = Gap;
        var candidates = new List<int>(old.Length);
        for (var i = 0; i < old.Length; i++)
        {
            if (stays[i])
            {
                continue;
            }
            candidates.Clear();
            for (var j = i - 1; j >= 0; j--)
            {
                if (stays[j])
                {
                    candidates.Add(j);
                    break;
                }
            }
            for (var j = i + 1; j < old.Length; j++)
            {
                if (stays[j])
                {
                    candidates.Add(j);
                    break;
                }
            }
            candidates.RemoveAll(j => _collapsed[j]);
            if (candidates.Count == 0)
            {
                for (var j = 0; j < old.Length; j++)
                {
                    if (stays[j] && !_collapsed[j])
                    {
                        candidates.Add(j);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                continue;
            }
            var freed = px[i] + gap;
            var total = 0.0;
            foreach (var j in candidates)
            {
                total += px[j];
            }
            foreach (var j in candidates)
            {
                gained[j] += total > 0 ? freed * px[j] / total : freed / candidates.Count;
            }
        }
        var count = next.Count;
        var sizes = new double[count];
        var collapsed = new bool[count];
        var restore = new double?[count];
        var known = new bool[count];
        var openPx = 0.0;
        var openDefault = 0.0;
        var open = 0;
        for (var k = 0; k < count; k++)
        {
            if (!index.TryGetValue(next[k], out var i))
            {
                continue;
            }
            known[k] = true;
            collapsed[k] = _collapsed[i];
            restore[k] = _restore[i];
            sizes[k] = collapsed[k] ? 0 : px[i] + gained[i];
            if (!collapsed[k])
            {
                openPx += sizes[k];
                openDefault += Math.Max(0, next[k].DefaultSize);
                open++;
            }
        }
        var newDefaults = 0.0;
        for (var k = 0; k < count; k++)
        {
            if (!known[k])
            {
                newDefaults += Math.Max(0, next[k].DefaultSize);
            }
        }
        var room = Math.Max(0, _length - gap * Math.Max(0, count - 1));
        for (var k = 0; k < count; k++)
        {
            if (known[k])
            {
                continue;
            }
            var pane = next[k];
            collapsed[k] = pane.IsCollapsible && pane.IsCollapsed;
            var wanted = Math.Max(0, pane.DefaultSize);
            if (collapsed[k])
            {
                sizes[k] = 0;
            }
            else if (open > 0 && openDefault > 0)
            {
                sizes[k] = wanted / (openDefault / open) * (openPx / open);
            }
            else if (open > 0)
            {
                sizes[k] = openPx / open;
            }
            else
            {
                // Every staying pane is hidden: the new panes share the room by their defaults.
                sizes[k] = newDefaults > 0 ? room * wanted / newDefaults : room / count;
            }
        }
        return new Carried(sizes, collapsed, restore);
    }

    /// <summary>Distributes <paramref name="length"/> the way a layout pass would and records the result.</summary>
    private void FitActual(double length)
    {
        var count = _panes.Count;
        if (count == 0)
        {
            return;
        }
        _room = RoomFor(length);
        Span<double> grow = stackalloc double[count];
        Span<double> min = stackalloc double[count];
        Span<double> max = stackalloc double[count];
        Span<double> sizes = stackalloc double[count];
        for (var i = 0; i < count; i++)
        {
            grow[i] = Math.Max(0, _sizes[i].Value);
            var loose = _free[i] || _collapsed[i];
            min[i] = loose ? 0 : MinOf(i);
            max[i] = loose ? double.PositiveInfinity : MaxOf(i);
        }
        ResizablePanelsMath.Distribute(_room, grow, min, max, sizes);
        sizes.CopyTo(_actual);
    }

    private void WriteSizes()
    {
        _writingSizes = true;
        try
        {
            SetCurrentValue(SizesProperty, _shares);
        }
        finally
        {
            _writingSizes = false;
        }
    }

    private double[] InitialSizes()
    {
        var count = _panes.Count;
        var sizes = new double[count];
        var saved = Sizes;
        for (var i = 0; i < count; i++)
        {
            var pane = _panes[i];
            var hidden = pane.IsCollapsible && pane.IsCollapsed;
            sizes[i] = hidden ? 0 : saved is { } s && s.Count == count ? Math.Max(0, s[i]) : Math.Max(0, pane.DefaultSize);
        }
        return sizes;
    }

    private void OnPaneConfigChanged(ResizablePanel pane, AvaloniaProperty property)
    {
        if (property == ResizablePanel.IsCollapsedProperty && !_writingCollapsed)
        {
            var index = _panes.IndexOf(pane);
            if (index < 0 || !pane.IsCollapsible || pane.IsCollapsed == _collapsed[index])
            {
                return;
            }
            // Collapse or restore from code, through the divider next to the pane.
            var handle = index < _panes.Count - 1 ? index : index - 1;
            if (handle < 0)
            {
                return;
            }
            var side = handle == index ? ResizablePanelsMode.Before : ResizablePanelsMode.After;
            if (pane.IsCollapsed)
            {
                Collapse(handle, side);
            }
            else
            {
                Restore(handle, side);
            }
            return;
        }
        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == OrientationProperty)
        {
            if (_drag is { } drag)
            {
                _drag = null;
                SetResizing(drag.Handle, false);
                drag.Pointer.Capture(null);
                Commit();
            }
            PseudoClasses.Set(VerticalPseudoClass, Orientation == Orientation.Vertical);
            var axis = Axis;
            foreach (var pane in _panes)
            {
                pane.Axis = axis;
                pane.InvalidateMeasure();
            }
            foreach (var handle in _handles)
            {
                handle.RefreshCursor();
                handle.InvalidateMeasure();
                handle.InvalidateVisual();
            }
            _stale = true;
            InvalidateMeasure();
        }
        else if (change.Property == GapProperty)
        {
            foreach (var handle in _handles)
            {
                handle.InvalidateMeasure();
                handle.InvalidateVisual();
            }
            _stale = true;
            InvalidateMeasure();
        }
        else if (change.Property == SizesProperty && !_writingSizes && Sizes is { } sizes && sizes.Count == _panes.Count)
        {
            // Restoring a saved layout: the shares become flex factors, so they fit any width.
            _token++;
            for (var i = 0; i < _sizes.Length; i++)
            {
                _sizes[i].Jump(_collapsed[i] ? 0 : Math.Max(0, sizes[i]));
            }
            _shares = ResizablePanelsMath.ToShares(sizes);
            _stale = true;
            InvalidateMeasure();
        }
    }

    #endregion

    #region Layout

    private double MinTotal()
    {
        var total = 0.0;
        foreach (var pane in _panes)
        {
            total += Math.Max(0, pane.MinSize);
        }
        return total;
    }

    /// <summary>A group narrower than every minimum together scales the minimums down in proportion.</summary>
    private double MinOf(int index)
    {
        var min = Math.Max(0, _panes[index].MinSize);
        var total = MinTotal();
        return _room > 0 && _room < total ? min * _room / total : min;
    }

    private double MaxOf(int index) => _panes[index].MaxSize is var max and > 0 ? max : double.PositiveInfinity;

    private double RoomFor(double length) => Math.Max(0, length - Gap * Math.Max(0, _panes.Count - 1));

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _panes.Count;
        if (count == 0)
        {
            return default;
        }
        var axis = Axis;
        var available = axis.Main(availableSize);
        var crossAvailable = axis.Cross(availableSize);
        var length = double.IsInfinity(available) ? 0 : available;
        var room = RoomFor(length);
        if (Math.Abs(room - _room) > 0.5)
        {
            // Only a group resize (or the first layout) puts the sizes out of step with the screen.
            _stale = true;
        }
        _room = room;
        _length = length;
        _laidOut |= length > 0;
        Span<double> grow = stackalloc double[count];
        Span<double> min = stackalloc double[count];
        Span<double> max = stackalloc double[count];
        Span<double> sizes = stackalloc double[count];
        for (var i = 0; i < count; i++)
        {
            grow[i] = Math.Max(0, _sizes[i].Value);
            var loose = _free[i] || _collapsed[i];
            min[i] = loose ? 0 : MinOf(i);
            max[i] = loose ? double.PositiveInfinity : MaxOf(i);
        }
        ResizablePanelsMath.Distribute(room, grow, min, max, sizes);
        var cross = 0.0;
        for (var i = 0; i < count; i++)
        {
            _actual[i] = sizes[i];
            var pane = _panes[i];
            pane.ContentMin = MinOf(i);
            // Content fades as a pane is squeezed below its minimum, so a collapsing pane dissolves rather than crushes.
            pane.Opacity = Math.Clamp(sizes[i] / Math.Max(1, MinOf(i)), 0, 1);
            pane.Measure(axis.Size(sizes[i], crossAvailable));
            cross = Math.Max(cross, axis.Cross(pane.DesiredSize));
        }
        foreach (var handle in _handles)
        {
            handle.Measure(axis.Size(handle.ReachLength, crossAvailable));
        }
        return axis.Size(length, double.IsInfinity(crossAvailable) ? cross : crossAvailable);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var axis = Axis;
        var cross = axis.Cross(finalSize);
        var gap = Gap;
        var position = 0.0;
        for (var i = 0; i < _panes.Count; i++)
        {
            var size = _actual[i];
            _panes[i].Arrange(axis.Rect(position, 0, size, cross));
            position += size;
            if (i < _handles.Count)
            {
                var handle = _handles[i];
                handle.Arrange(axis.Rect(position - handle.LineOffset, 0, handle.ReachLength, cross));
                position += gap;
            }
        }
        return finalSize;
    }

    #endregion

    #region Interaction core

    private ResizablePanelsMode ModeOf(int handle) =>
        _collapsed[handle] ? ResizablePanelsMode.Before : _collapsed[handle + 1] ? ResizablePanelsMode.After : ResizablePanelsMode.Open;

    /// <summary>Starts an interaction: stops anything in flight and makes sure the sizes are the px widths on screen.</summary>
    private void Begin(params int[] involved)
    {
        _token++;
        foreach (var size in _sizes)
        {
            size.Stop();
        }
        if (_stale)
        {
            _stale = false;
            var differs = false;
            for (var i = 0; i < _sizes.Length; i++)
            {
                differs |= Math.Abs(_actual[i] - _sizes[i].Value) > 0.5;
            }
            if (differs)
            {
                for (var i = 0; i < _sizes.Length; i++)
                {
                    _sizes[i].Jump(_actual[i]);
                }
            }
        }
        // Freed panes drop their limits while the interaction owns them, so rubber banding and collapsing can pass them.
        foreach (var index in involved)
        {
            _free[index] = true;
        }
    }

    private void Commit()
    {
        Array.Clear(_free);
        var values = new double[_sizes.Length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = _sizes[i].Value;
        }
        _shares = ResizablePanelsMath.ToShares(values);
        _hasGoals = false;
        WriteSizes();
        InvalidateMeasure();
        RaiseEvent(new ResizablePanelsLayoutChangedEventArgs(LayoutChangedEvent, _shares));
    }

    /// <summary>Springs panes to new widths from wherever they are, keeping their velocity, then hands the limits back.</summary>
    private async void Glide(double?[] targets, MotionSpec spec, double[]? velocities = null)
    {
        var mine = _token;
        for (var i = 0; i < targets.Length; i++)
        {
            _goals[i] = targets[i] ?? _sizes[i].Value;
        }
        _hasGoals = true;
        // Assistive technology hears the destination right away rather than after the spring's tail settles.
        _shares = ResizablePanelsMath.ToShares(_goals);
        var full = IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var running = new List<Task<bool>>(2);
        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not { } target || Math.Abs(target - _sizes[i].Value) < 0.01)
            {
                continue;
            }
            if (!full)
            {
                _sizes[i].Jump(target);
                continue;
            }
            running.Add(_sizes[i].AnimateTo(target, spec, velocities?[i]));
        }
        if (running.Count > 0)
        {
            await Task.WhenAll(running);
        }
        if (_token == mine)
        {
            Commit();
        }
    }

    private (double Pair, double Lo, double Hi) Limits(int handle, double a, double b)
    {
        var pair = a + b;
        var lo = Math.Max(MinOf(handle), pair - MaxOf(handle + 1));
        var hi = Math.Min(MaxOf(handle), pair - MinOf(handle + 1));
        // A group too narrow for every minimum may already hold a pane below it; never yank it to the limit on grab.
        if (a > 0 && b > 0)
        {
            lo = Math.Min(lo, a);
            hi = Math.Max(hi, a);
        }
        if (lo > hi)
        {
            lo = hi = Math.Clamp((lo + hi) / 2, 0, pair);
        }
        return (pair, Math.Clamp(lo, 0, pair), Math.Clamp(hi, 0, pair));
    }

    private (ResizablePanelsMode Mode, double Size) Resolve(int handle, double raw, double pair, double lo, double hi)
    {
        if (_panes[handle].IsCollapsible && raw < ResizablePanelsMath.CollapseAt(MinOf(handle)))
        {
            return (ResizablePanelsMode.Before, 0);
        }
        if (_panes[handle + 1].IsCollapsible && pair - raw < ResizablePanelsMath.CollapseAt(MinOf(handle + 1)))
        {
            return (ResizablePanelsMode.After, pair);
        }
        return (ResizablePanelsMode.Open, ResizablePanelsMath.Band(raw, lo, hi));
    }

    private void SetCollapsedPair(int handle, ResizablePanelsMode mode)
    {
        _collapsed[handle] = mode == ResizablePanelsMode.Before;
        _collapsed[handle + 1] = mode == ResizablePanelsMode.After;
        _handles[handle].SetMode(mode);
        if (handle > 0)
        {
            _handles[handle - 1].SetMode(ModeOf(handle - 1));
        }
        if (handle + 1 < _handles.Count)
        {
            _handles[handle + 1].SetMode(ModeOf(handle + 1));
        }
        _writingCollapsed = true;
        try
        {
            _panes[handle].SetCurrentValue(ResizablePanel.IsCollapsedProperty, _collapsed[handle]);
            _panes[handle + 1].SetCurrentValue(ResizablePanel.IsCollapsedProperty, _collapsed[handle + 1]);
        }
        finally
        {
            _writingCollapsed = false;
        }
        ExpandInert.Apply(_panes[handle], _collapsed[handle]);
        ExpandInert.Apply(_panes[handle + 1], _collapsed[handle + 1]);
    }

    private void Remember(int handle, double a, double b, ResizablePanelsMode mode)
    {
        if (mode == ResizablePanelsMode.Before && a > 0)
        {
            _restore[handle] = a;
        }
        if (mode == ResizablePanelsMode.After && b > 0)
        {
            _restore[handle + 1] = b;
        }
    }

    private double?[] Pair(int handle, double a, double b)
    {
        var targets = new double?[_sizes.Length];
        targets[handle] = a;
        targets[handle + 1] = b;
        return targets;
    }

    private void Restore(int handle, ResizablePanelsMode side)
    {
        Begin(handle, handle + 1);
        var (pair, lo, hi) = Limits(handle, _sizes[handle].Value, _sizes[handle + 1].Value);
        var size = side == ResizablePanelsMode.Before
            ? Math.Clamp(_restore[handle] ?? MinOf(handle), lo, hi)
            : Math.Clamp(pair - (_restore[handle + 1] ?? MinOf(handle + 1)), lo, hi);
        SetCollapsedPair(handle, ResizablePanelsMode.Open);
        Glide(Pair(handle, size, pair - size), MotionSpec.Spring(ArcSpring.Smooth));
    }

    private void Collapse(int handle, ResizablePanelsMode side)
    {
        Begin(handle, handle + 1);
        var a = _sizes[handle].Value;
        var b = _sizes[handle + 1].Value;
        var pair = a + b;
        Remember(handle, a, b, side);
        SetCollapsedPair(handle, side);
        var before = side == ResizablePanelsMode.Before;
        Glide(Pair(handle, before ? 0 : pair, before ? pair : 0), MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>Resets to the default shares, fitted to the current width so every pane respects its limits.</summary>
    public void Reset()
    {
        if (_panes.Count == 0)
        {
            return;
        }
        var all = Enumerable.Range(0, _panes.Count).ToArray();
        Begin(all);
        var total = 0.0;
        foreach (var size in _sizes)
        {
            total += size.Value;
        }
        var count = _panes.Count;
        Span<double> wanted = stackalloc double[count];
        Span<double> min = stackalloc double[count];
        Span<double> max = stackalloc double[count];
        Span<double> fitted = stackalloc double[count];
        for (var i = 0; i < count; i++)
        {
            wanted[i] = DefaultHidden(i) ? 0 : Math.Max(0, _panes[i].DefaultSize);
            min[i] = wanted[i] == 0 ? 0 : MinOf(i);
            max[i] = wanted[i] == 0 ? 0 : MaxOf(i);
        }
        ResizablePanelsMath.Distribute(total, wanted, min, max, fitted);
        var targets = new double?[count];
        for (var i = 0; i < count; i++)
        {
            targets[i] = fitted[i];
            _collapsed[i] = DefaultHidden(i);
        }
        for (var h = 0; h < _handles.Count; h++)
        {
            _handles[h].SetMode(ModeOf(h));
        }
        _writingCollapsed = true;
        try
        {
            for (var i = 0; i < count; i++)
            {
                _panes[i].SetCurrentValue(ResizablePanel.IsCollapsedProperty, _collapsed[i]);
                ExpandInert.Apply(_panes[i], _collapsed[i]);
            }
        }
        finally
        {
            _writingCollapsed = false;
        }
        Glide(targets, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private readonly Dictionary<ResizablePanel, bool> _defaultHidden = new();

    /// <summary>Arc's defaultCollapsed: the collapsed state the pane started with.</summary>
    private bool DefaultHidden(int index)
    {
        var pane = _panes[index];
        if (!_defaultHidden.TryGetValue(pane, out var hidden))
        {
            hidden = pane.IsCollapsible && pane.IsCollapsed;
            _defaultHidden[pane] = hidden;
        }
        return hidden;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        for (var i = 0; i < _panes.Count; i++)
        {
            DefaultHidden(i);
            ExpandInert.Apply(_panes[i], _collapsed[i]);
        }
    }

    #endregion

    #region Pointer

    internal void BeginDrag(ResizablePanelsHandle handle, IPointer pointer, double x, ulong timestamp)
    {
        if (_drag is not null)
        {
            return;
        }
        var index = handle.Index;
        Begin(index, index + 1);
        var a = _sizes[index].Value;
        var b = _sizes[index + 1].Value;
        var (pair, lo, hi) = Limits(index, a, b);
        var mode = ModeOf(index);
        _drag = new DragState
        {
            Handle = index, Pointer = pointer, StartX = x, A0 = a, B0 = b, Pair = pair, Lo = lo, Hi = hi,
            StartMode = mode, Mode = mode
        };
        _drag.Samples.Add((timestamp, x));
    }

    internal void MoveDrag(IPointer pointer, double x, ulong timestamp)
    {
        var drag = _drag;
        if (drag is null || drag.Pointer != pointer)
        {
            return;
        }
        var dx = x - drag.StartX;
        if (!drag.Moved)
        {
            if (Math.Abs(dx) < 2)
            {
                return;
            }
            drag.Moved = true;
            SetResizing(drag.Handle, true);
        }
        var next = Resolve(drag.Handle, drag.A0 + dx, drag.Pair, drag.Lo, drag.Hi);
        if (next.Mode != drag.Mode)
        {
            drag.Mode = next.Mode;
            drag.Catching = true;
        }
        MoveBoundary(drag, next.Size);
        drag.Samples.Add((timestamp, x));
        if (drag.Samples.Count > 16)
        {
            drag.Samples.RemoveAt(0);
        }
    }

    /// <summary>Sets a pane pair to a new boundary: a change of collapse state springs, everything else follows the pointer.</summary>
    private void MoveBoundary(DragState drag, double size)
    {
        var a = _sizes[drag.Handle];
        var b = _sizes[drag.Handle + 1];
        if (drag.Catching && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full)
        {
            if (Math.Abs(a.Value - size) < 0.75)
            {
                drag.Catching = false;
                a.Stop();
                b.Stop();
            }
            else
            {
                if (Math.Abs(a.Target - size) > 0.01)
                {
                    _ = a.AnimateTo(size, MotionSpec.Spring(ArcSpring.Smooth));
                    _ = b.AnimateTo(drag.Pair - size, MotionSpec.Spring(ArcSpring.Smooth));
                }
                return;
            }
        }
        a.Jump(size);
        b.Jump(drag.Pair - size);
    }

    internal void EndDrag(IPointer pointer, double x, ulong timestamp, bool cancelled)
    {
        var drag = _drag;
        if (drag is null || drag.Pointer != pointer)
        {
            return;
        }
        _drag = null;
        SetResizing(drag.Handle, false);
        var handle = drag.Handle;
        if (!drag.Moved)
        {
            // A press on a divider that hides a pane brings it back.
            if (drag.StartMode != ResizablePanelsMode.Open && !cancelled)
            {
                Restore(handle, drag.StartMode);
            }
            else
            {
                Commit();
            }
            return;
        }
        var raw = drag.A0 + x - drag.StartX;
        var velocity = cancelled ? 0 : ResizablePanelsMath.VelocityOf(drag.Samples, timestamp);
        var flung = Resolve(handle, raw + ResizablePanelsMath.Project(velocity), drag.Pair, drag.Lo, drag.Hi);
        // A flick toward an edge collapses; a flick back out restores. Otherwise the divider stays where it was let go.
        var mode = cancelled ? drag.StartMode : flung.Mode;
        var size = mode == ResizablePanelsMode.Before ? 0
            : mode == ResizablePanelsMode.After ? drag.Pair
            : cancelled ? drag.A0 : Math.Clamp(raw, drag.Lo, drag.Hi);
        Remember(handle, drag.A0, drag.B0, mode);
        SetCollapsedPair(handle, mode);
        var velocities = new double[_sizes.Length];
        velocities[handle] = velocity;
        velocities[handle + 1] = -velocity;
        Glide(Pair(handle, size, drag.Pair - size), MotionSpec.Spring(ArcSpring.Smooth), velocities);
    }

    internal void CancelDrag(IPointer pointer)
    {
        if (_drag is { } drag && drag.Pointer == pointer)
        {
            EndDrag(pointer, drag.StartX, 0, cancelled: true);
        }
    }

    private void SetResizing(int handle, bool resizing)
    {
        PseudoClasses.Set(ResizingPseudoClass, resizing);
        _handles[handle].SetDragging(resizing);
        // Content ignores the pointer while a divider moves, so hover states never flicker underneath it.
        foreach (var pane in _panes)
        {
            if (!_collapsed[_panes.IndexOf(pane)])
            {
                pane.IsHitTestVisible = !resizing;
            }
        }
    }

    #endregion

    #region Keyboard

    internal bool HandleKey(int handle, Key key, bool shift)
    {
        if (key == Key.Escape && _drag is { } drag && drag.Handle == handle)
        {
            // Escape during a drag puts the divider back where it started.
            _drag = null;
            drag.Pointer.Capture(null);
            SetResizing(handle, false);
            SetCollapsedPair(handle, drag.StartMode);
            Glide(Pair(handle, drag.A0, drag.B0), MotionSpec.Spring(ArcSpring.Smooth));
            return true;
        }
        if (_drag is not null)
        {
            return false;
        }
        // The arrows along the axis resize; the other pair is left to the app. Below, Left means "toward the start"
        // (Up in a vertical group) and Right "toward the end" (Down).
        var vertical = Orientation == Orientation.Vertical;
        if (vertical)
        {
            if (key is Key.Left or Key.Right)
            {
                return false;
            }
            key = key switch
            {
                Key.Up => Key.Left,
                Key.Down => Key.Right,
                _ => key
            };
        }
        if (key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Enter))
        {
            return false;
        }
        var mode = ModeOf(handle);
        var canCollapseBefore = _panes[handle].IsCollapsible;
        var canCollapseAfter = _panes[handle + 1].IsCollapsible;
        if (key == Key.Enter)
        {
            if (mode != ResizablePanelsMode.Open)
            {
                Restore(handle, mode);
            }
            else if (canCollapseBefore)
            {
                Collapse(handle, ResizablePanelsMode.Before);
            }
            else if (canCollapseAfter)
            {
                Collapse(handle, ResizablePanelsMode.After);
            }
            return true;
        }
        // Repeated presses build on where the divider is heading, not on where the spring happens to be this frame.
        var heading = _hasGoals && (_sizes[handle].IsAnimating || _sizes[handle + 1].IsAnimating);
        Begin(handle, handle + 1);
        var pair = _sizes[handle].Value + _sizes[handle + 1].Value;
        var a = heading ? Math.Clamp(_goals[handle], 0, pair) : _sizes[handle].Value;
        var step = shift ? ResizablePanelsMath.BigStep : ResizablePanelsMath.Step;
        var (_, lo, hi) = Limits(handle, a, pair - a);
        // Arrows walk out of a collapsed pane at its minimum, and past a limit into collapse when the pane allows it.
        if (key == Key.Left && mode == ResizablePanelsMode.After)
        {
            RestoreTo(handle, hi, pair);
            return true;
        }
        if (key == Key.Right && mode == ResizablePanelsMode.Before)
        {
            RestoreTo(handle, lo, pair);
            return true;
        }
        if (key == Key.Left && a <= lo + 0.5 && canCollapseBefore && mode == ResizablePanelsMode.Open)
        {
            Collapse(handle, ResizablePanelsMode.Before);
            return true;
        }
        if (key == Key.Right && a >= hi - 0.5 && canCollapseAfter && mode == ResizablePanelsMode.Open)
        {
            Collapse(handle, ResizablePanelsMode.After);
            return true;
        }
        if (mode != ResizablePanelsMode.Open)
        {
            Commit();
            return true;
        }
        var size = key switch
        {
            Key.Home => lo,
            Key.End => hi,
            Key.Left => Math.Clamp(a - step, lo, hi),
            _ => Math.Clamp(a + step, lo, hi)
        };
        var arrow = key is Key.Left or Key.Right;
        Glide(Pair(handle, size, pair - size), MotionSpec.Spring(arrow ? ArcSpring.Snappy : ArcSpring.Smooth));
        return true;
    }

    private void RestoreTo(int handle, double size, double pair)
    {
        SetCollapsedPair(handle, ResizablePanelsMode.Open);
        Glide(Pair(handle, size, pair - size), MotionSpec.Spring(ArcSpring.Smooth));
    }

    #endregion

    #region Accessibility helpers

    internal string? PaneLabel(int handle) => _panes[handle].Label ?? _panes[handle].Id;

    internal double Share(int handle) => handle < _shares.Length ? _shares[handle] : 0;

    internal (double Min, double Max) ShareLimits(int handle)
    {
        var now = Math.Round(Share(handle));
        if (_room <= 0)
        {
            return (0, 100);
        }
        var a = Share(handle) / 100 * _room;
        var b = Share(handle + 1) / 100 * _room;
        var (_, lo, hi) = Limits(handle, a, b);
        return (Math.Min(Math.Round(lo / _room * 100), now), Math.Max(Math.Round(hi / _room * 100), now));
    }

    internal string ValueText(int handle)
    {
        var mode = ModeOf(handle);
        var hidden = mode == ResizablePanelsMode.Before ? _panes[handle].Label : mode == ResizablePanelsMode.After ? _panes[handle + 1].Label : null;
        return hidden is not null ? $"{hidden} hidden, press Enter to show" : $"{Math.Round(Share(handle))}%";
    }

    internal void SetShare(int handle, double percent)
    {
        if (_room <= 0 || ModeOf(handle) != ResizablePanelsMode.Open)
        {
            return;
        }
        Begin(handle, handle + 1);
        var pair = _sizes[handle].Value + _sizes[handle + 1].Value;
        var (_, lo, hi) = Limits(handle, _sizes[handle].Value, _sizes[handle + 1].Value);
        var size = Math.Clamp(percent / 100 * _room, lo, hi);
        Glide(Pair(handle, size, pair - size), MotionSpec.Spring(ArcSpring.Smooth));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupAutomationPeer(this);

    private sealed class GroupAutomationPeer : ControlAutomationPeer
    {
        public GroupAutomationPeer(ResizablePanels owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        // Avalonia's peers have no orientation property; the item type carries the axis ("horizontal" / "vertical").
        protected override string GetItemTypeCore() => ((ResizablePanels)Owner).Orientation == Orientation.Vertical ? "vertical" : "horizontal";
    }

    #endregion
}
