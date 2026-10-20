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
/// Arc resizable panels: a horizontal split view whose panes trade space when the dividers between them are dragged,
/// the way a mail or code editor window works. Dividers follow the pointer 1:1, resist past a pane's limits (rubber
/// band up to 44px) and snap collapsible panes closed when dragged well past their minimum or flicked toward the edge;
/// on release the panes settle on the smooth spring keeping the pointer's velocity. Each divider is focusable: arrows
/// resize by 16px (Shift 64px) on the snappy spring, Home / End jump to the limits, Enter hides or restores a
/// collapsible neighbour, Escape during a drag puts the divider back, and a double-click resets the layout. The group
/// needs a definite height. <see cref="Sizes"/> (two-way, percent) persists the layout.
/// </summary>
[PseudoClasses(ResizingPseudoClass)]
public class ResizablePanels : Control
{
    public const string ResizingPseudoClass = ":resizing";

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

    /// <summary>The panes, in order. The set of panes should stay the same for the life of the group.</summary>
    [Content]
    public AvaloniaList<ResizablePanel> Panels { get; } = new();

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="SizesProperty"/>
    public IReadOnlyList<double>? Sizes { get => GetValue(SizesProperty); set => SetValue(SizesProperty, value); }

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
    private double _room;
    private int _token;
    private bool _writingSizes;
    private bool _writingCollapsed;
    private DragState? _drag;

    private sealed class DragState
    {
        public required int Handle { get; init; }
        public required IPointer Pointer { get; init; }
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

    /// <summary>Current width of each pane in px (tests).</summary>
    internal IReadOnlyList<double> ActualWidths => _actual;

    internal IReadOnlyList<ResizablePanelsHandle> Handles => _handles;

    internal bool IsSettling => _sizes.Any(s => s.IsAnimating);

    #region Structure

    private void Rebuild()
    {
        foreach (var pane in _panes)
        {
            pane.ConfigChanged -= OnPaneConfigChanged;
            LogicalChildren.Remove(pane);
            VisualChildren.Remove(pane);
        }
        foreach (var handle in _handles)
        {
            VisualChildren.Remove(handle);
            LogicalChildren.Remove(handle);
        }
        _panes.Clear();
        _handles.Clear();
        foreach (var sizes in _sizes)
        {
            sizes.Dispose();
        }
        _panes.AddRange(Panels);
        var count = _panes.Count;
        _collapsed = new bool[count];
        _free = new bool[count];
        _restore = new double?[count];
        _actual = new double[count];
        _goals = new double[count];
        _sizes = new MotionValue<double>[count];
        var initial = InitialSizes();
        for (var i = 0; i < count; i++)
        {
            var pane = _panes[i];
            _collapsed[i] = pane.IsCollapsible && pane.IsCollapsed;
            _sizes[i] = new MotionValue<double>(this, initial[i], MotionVectors.Double);
            _sizes[i].Changed += _ => InvalidateMeasure();
            pane.AnchorEnd = i == 0 && count > 1;
            pane.ConfigChanged += OnPaneConfigChanged;
            LogicalChildren.Add(pane);
            VisualChildren.Add(pane);
        }
        _shares = ResizablePanelsMath.ToShares(initial);
        for (var i = 0; i < count - 1; i++)
        {
            var handle = new ResizablePanelsHandle(this, i);
            _handles.Add(handle);
            VisualChildren.Add(handle);
            LogicalChildren.Add(handle);
            handle.SetMode(ModeOf(i));
        }
        _stale = true;
        InvalidateMeasure();
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

    private double RoomFor(double width) => Math.Max(0, width - Math.Max(0, _panes.Count - 1));

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _panes.Count;
        if (count == 0)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        var room = RoomFor(width);
        if (Math.Abs(room - _room) > 0.5)
        {
            // Only a group resize (or the first layout) puts the sizes out of step with the screen.
            _stale = true;
        }
        _room = room;
        Span<double> grow = stackalloc double[count];
        Span<double> min = stackalloc double[count];
        Span<double> max = stackalloc double[count];
        Span<double> widths = stackalloc double[count];
        for (var i = 0; i < count; i++)
        {
            grow[i] = Math.Max(0, _sizes[i].Value);
            var loose = _free[i] || _collapsed[i];
            min[i] = loose ? 0 : MinOf(i);
            max[i] = loose ? double.PositiveInfinity : MaxOf(i);
        }
        ResizablePanelsMath.Distribute(room, grow, min, max, widths);
        var height = 0.0;
        for (var i = 0; i < count; i++)
        {
            _actual[i] = widths[i];
            var pane = _panes[i];
            pane.ContentMinWidth = MinOf(i);
            // Content fades as a pane is squeezed below its minimum, so a collapsing pane dissolves rather than crushes.
            pane.Opacity = Math.Clamp(widths[i] / Math.Max(1, MinOf(i)), 0, 1);
            pane.Measure(new Size(widths[i], availableSize.Height));
            height = Math.Max(height, pane.DesiredSize.Height);
        }
        foreach (var handle in _handles)
        {
            handle.Measure(new Size(handle.ReachWidth, availableSize.Height));
        }
        return new Size(width, double.IsInfinity(availableSize.Height) ? height : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        for (var i = 0; i < _panes.Count; i++)
        {
            var w = _actual[i];
            _panes[i].Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w;
            if (i < _handles.Count)
            {
                var handle = _handles[i];
                handle.Arrange(new Rect(x - handle.LineOffset, 0, handle.ReachWidth, finalSize.Height));
                x += 1;
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
        _writingSizes = true;
        try
        {
            SetCurrentValue(SizesProperty, _shares);
        }
        finally
        {
            _writingSizes = false;
        }
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
        if (_drag is not null || key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Enter))
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
    }

    #endregion
}
