using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the journey chart asks its block for: words, numbers and reactions to input.</summary>
internal interface IJourneyFlowChartHost
{
    /// <summary>The node's name.</summary>
    string NodeHeader(int node);

    /// <summary>The stage's name (or short name).</summary>
    string StageHeader(int stage, bool shortForm);

    /// <summary>A count as shown on the chart.</summary>
    string FormatCount(double value);

    /// <summary>A node label's share (fraction 0–1) as shown on the chart ("31%").</summary>
    string FormatLabelShare(double share);

    /// <summary>The tooltip of a node.</summary>
    AnalyticsReadoutContent NodeTooltip(int node);

    /// <summary>The tooltip of a link.</summary>
    AnalyticsReadoutContent LinkTooltip(int link);

    /// <summary>The spoken name of a node's button.</summary>
    string NodeAutomationName(int node);

    /// <summary>A node was clicked or Enter / Space was pressed on it.</summary>
    void OnNodeInvoked(int node);

    /// <summary>The empty chart was clicked, or Escape was pressed on a node.</summary>
    void OnClearRequested();

    /// <summary>The previewed node or link (pointer or keyboard focus) changed.</summary>
    void OnTraceChanged(int node, int link);
}

/// <summary>
/// The self-drawn Sankey chart of <see cref="JourneyFlow"/> (Arc Journey flow, measured): guides and stage names,
/// ribbons, cohort traces, node bars with cohort overlays, two-line node labels with a Surface halo, the selection ring,
/// the focus ring and the tooltip; one invisible toggle button per node carries keyboard focus and the screen-reader
/// name (roving tab stop). Pointer input is hit-tested here: nodes (bar or label area) first, then ribbons (2px slop),
/// then the empty chart.
/// </summary>
/// <remarks>
/// Why code-driven: the counts are interpolated on a spring and the whole layout (scale, node heights, link ends, ribbon
/// paths) is rebuilt from them every frame, the cohort thickness follows a second spring that keeps its velocity when the
/// traced node changes, and the fades are drawn into the same render (a ControlTheme transition animates properties, not
/// one geometry).
/// </remarks>
internal sealed class JourneyFlowChart : Control
{
    /// <summary>Counts morph on k 70, c 16.4 (measured: ζ ≈ 0.98, no overshoot, about 0.78s to 99%).</summary>
    internal static readonly SpringSpec CountSpring = SpringSpec.FromPhysical(70, 16.4);

    /// <summary>Label counts follow the same spring a beat later (60–70ms).</summary>
    internal static readonly TimeSpan LabelDelay = TimeSpan.FromMilliseconds(65);

    /// <summary>Smaller label type (block 479 wide or less): names 11, counts and stage names 10.</summary>
    public static readonly StyledProperty<bool> IsTightProperty =
        AvaloniaProperty.Register<JourneyFlowChart, bool>(nameof(IsTight));

    private const double DimRibbonStep = 0.035;
    private const double DimRibbonGoal = 0.07;
    private const double TraceAlpha = 0.32;
    private const double DimNodeOpacity = 0.3;
    private const double OffLabelOpacity = 0.35;

    private readonly JourneyFlowLayout _layout = new();
    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new();
    private readonly AnalyticsReadout _readout = new(AnalyticsReadoutStyle.Flow) { CrossfadesContent = true, ExitDuration = TimeSpan.Zero };
    private readonly LiveLoop _loop;
    private readonly List<JourneyFlowNodeButton> _buttons = new();

    private JourneyFlowModel _model = JourneyFlowModel.Empty;
    private AnalyticsSpringChannel[] _values = [];
    private double[] _current = [];
    private double[] _targets = [];
    private AnalyticsSpringChannel[] _labels = [];
    private double[] _labelTargets = [];
    private double _labelDelay;
    private bool _labelsPending;
    private AnalyticsSpringChannel[] _fractions = [];
    private double[] _cohort = [];
    private AnalyticsTween _dim = new(0);
    private AnalyticsTween[] _labelAlpha = [];
    private AnalyticsTween[] _ringAlpha = [];
    private StreamGeometry?[] _ribbons = [];
    private StreamGeometry?[] _traces = [];
    private string?[] _countText = [];
    private double[] _countShown = [];
    private bool _geometryDirty = true;
    private bool _freshStart;
    private Size _size;
    private int _hoverNode = -1;
    private int _hoverLink = -1;
    private int _focusNode = -1;
    private int _selected = -1;
    private int _roving;
    private Point _pointer;
    private int _previewNode = -1;
    private int _previewLink = -1;

    public JourneyFlowChart()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        Focusable = false;
        ClipToBounds = false;
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
    }

    /// <inheritdoc cref="IsTightProperty"/>
    public bool IsTight { get => GetValue(IsTightProperty); set => SetValue(IsTightProperty, value); }

    /// <summary>The block that supplies words and takes input.</summary>
    public IJourneyFlowChartHost? Host { get; set; }

    /// <summary>The structure shown.</summary>
    internal JourneyFlowModel Model => _model;

    /// <summary>The current layout (follows the morph).</summary>
    internal JourneyFlowLayout CurrentLayout => _layout;

    /// <summary>Counts on screen now, per link.</summary>
    internal ReadOnlySpan<double> CurrentValues => _current;

    /// <summary>Counts the morph heads to, per link.</summary>
    internal ReadOnlySpan<double> TargetValues => _targets;

    /// <summary>The count a node's label shows now.</summary>
    internal double LabelValue(int node) => _labels[node].Value;

    /// <summary>The share of each link drawn as the traced cohort now (0–1).</summary>
    internal double TraceFraction(int link) => _fractions[link].Value;

    /// <summary>Fade of the rest of the chart while tracing (0 rest, 1 traced).</summary>
    internal double DimProgress => _dim.Value;

    /// <summary>Opacity of a node's label now.</summary>
    internal double LabelOpacity(int node) => _labelAlpha[node].Value;

    /// <summary>The node traced now (−1 when a link or nothing is).</summary>
    internal int TraceNode { get; private set; } = -1;

    /// <summary>The link traced now (−1 when a node or nothing is).</summary>
    internal int TraceLink { get; private set; } = -1;

    /// <summary>The tooltip.</summary>
    internal AnalyticsReadout Readout => _readout;

    /// <summary>The node buttons, in node order.</summary>
    internal IReadOnlyList<JourneyFlowNodeButton> Buttons => _buttons;

    /// <summary>True while the loop runs.</summary>
    internal bool IsLooping => _loop.IsActive;

    /// <summary>The selected node (−1 for none).</summary>
    public int SelectedNode
    {
        get => _selected;
        set
        {
            value = value >= 0 && value < _model.NodeCount ? value : -1;
            if (_selected == value)
            {
                return;
            }
            _selected = value;
            for (var i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].IsChecked = i == value;
                StartTween(ref _ringAlpha[i], i == value ? 1 : 0);
            }
            UpdateTrace();
            Kick();
        }
    }

    /// <summary>
    /// Shows a structure and its counts. A new structure resets everything at once; the same structure morphs to the
    /// new counts when <paramref name="animate"/> (and motion is full).
    /// </summary>
    public void SetData(JourneyFlowModel model, ReadOnlySpan<double> values, bool animate)
    {
        if (!ReferenceEquals(model, _model))
        {
            _model = model;
            Reset(values);
            return;
        }
        SetValues(values, animate);
    }

    /// <summary>Moves to new counts for the same structure (morph when animated, otherwise at once).</summary>
    public void SetValues(ReadOnlySpan<double> values, bool animate)
    {
        var count = _model.LinkCount;
        var full = animate && ShouldAnimate();
        for (var l = 0; l < count; l++)
        {
            var v = l < values.Length && double.IsFinite(values[l]) && values[l] > 0 ? values[l] : 0;
            _targets[l] = v;
            if (full)
            {
                _values[l].SpringTo(v, CountSpring);
            }
            else
            {
                _values[l].Jump(v);
                _current[l] = v;
            }
        }
        JourneyFlowEngine.NodeTotals(_model, _targets, _labelTargets);
        if (full)
        {
            _labelDelay = LabelDelay.TotalSeconds;
            _labelsPending = true;
        }
        else
        {
            _labelsPending = false;
            for (var n = 0; n < _labels.Length; n++)
            {
                _labels[n].Jump(_labelTargets[n]);
            }
        }
        Relayout();
        RetargetCohort(full);
        RefreshTooltip();
        RefreshButtonNames();
        Kick();
    }

    /// <summary>Re-reads names (header, automation names, tooltip) after a text property changed.</summary>
    public void RefreshTexts()
    {
        RefreshButtonNames();
        RefreshTooltip();
        InvalidateVisual();
    }

    /// <summary>Focuses a node's button from the keyboard (the focus ring shows and the node is previewed).</summary>
    public void FocusNode(int node)
    {
        if (node >= 0 && node < _buttons.Count)
        {
            _buttons[node].Focus(NavigationMethod.Directional);
        }
    }

    /// <summary>Clears the pointer preview (the pointer left, or the block is hiding).</summary>
    public void ClearHover()
    {
        if (_hoverNode < 0 && _hoverLink < 0)
        {
            return;
        }
        _hoverNode = -1;
        _hoverLink = -1;
        Cursor = null;
        UpdateTrace();
        Kick();
    }

    /// <summary>The node or link at <paramref name="point"/> (chart coordinates): nodes first, then ribbons, the last drawn on top.</summary>
    internal (int Node, int Link) HitTest(Point point)
    {
        var lastStage = _model.StageCount - 1;
        for (var n = _model.NodeCount - 1; n >= 0; n--)
        {
            var rect = _layout.Nodes[n];
            if (JourneyFlowEngine.NodeHitRect(rect).Contains(point) ||
                JourneyFlowEngine.LabelHitRect(rect, _model.NodeStage[n] == lastStage && lastStage > 0).Contains(point))
            {
                return (n, -1);
            }
        }
        for (var l = _model.LinkCount - 1; l >= 0; l--)
        {
            var thickness = _layout.LinkThickness[l];
            if (thickness <= 0)
            {
                continue;
            }
            var x0 = _layout.LinkSourceX(_model, l);
            var x1 = _layout.LinkTargetX(_model, l);
            var s = _layout.LinkSourceY[l];
            var t = _layout.LinkTargetY[l];
            if (JourneyFlowEngine.RibbonContains(x0, s, s + thickness, x1, t, t + thickness, point))
            {
                return (-1, l);
            }
        }
        return (-1, -1);
    }

    // Data ----------------------------------------------------------------------------------------------------------

    private void Reset(ReadOnlySpan<double> values)
    {
        var links = _model.LinkCount;
        var nodes = _model.NodeCount;
        _values = new AnalyticsSpringChannel[links];
        _current = new double[links];
        _targets = new double[links];
        _fractions = new AnalyticsSpringChannel[links];
        _cohort = new double[links];
        _ribbons = new StreamGeometry?[links];
        _traces = new StreamGeometry?[links];
        _labels = new AnalyticsSpringChannel[nodes];
        _labelTargets = new double[nodes];
        _labelAlpha = new AnalyticsTween[nodes];
        _ringAlpha = new AnalyticsTween[nodes];
        _countText = new string?[nodes];
        _countShown = new double[nodes];
        for (var l = 0; l < links; l++)
        {
            var v = l < values.Length && double.IsFinite(values[l]) && values[l] > 0 ? values[l] : 0;
            _values[l] = new AnalyticsSpringChannel(v, 0.05, 0.5);
            _current[l] = v;
            _targets[l] = v;
            _fractions[l] = new AnalyticsSpringChannel(0, 0.0005, 0.005);
        }
        JourneyFlowEngine.NodeTotals(_model, _targets, _labelTargets);
        for (var n = 0; n < nodes; n++)
        {
            _labels[n] = new AnalyticsSpringChannel(_labelTargets[n], 0.05, 0.5);
            _labelAlpha[n] = new AnalyticsTween(1);
            _ringAlpha[n] = new AnalyticsTween(0);
            _countShown[n] = double.NaN;
        }
        _labelsPending = false;
        _dim.Jump(0);
        _hoverNode = -1;
        _hoverLink = -1;
        _focusNode = -1;
        _selected = -1;
        TraceNode = -1;
        TraceLink = -1;
        _previewNode = -1;
        _previewLink = -1;
        _readout.Clear();
        RebuildButtons();
        Relayout();
        InvalidateVisual();
    }

    private void RebuildButtons()
    {
        foreach (var button in _buttons)
        {
            button.Invoked -= OnButtonInvoked;
            button.GotFocus -= OnButtonGotFocus;
            button.LostFocus -= OnButtonLostFocus;
            button.RemoveHandler(KeyDownEvent, OnNodeKeyDown);
            VisualChildren.Remove(button);
            LogicalChildren.Remove(button);
        }
        _buttons.Clear();
        _roving = 0;
        for (var n = 0; n < _model.NodeCount; n++)
        {
            var button = new JourneyFlowNodeButton { Index = n, IsTabStop = n == 0 };
            button.Invoked += OnButtonInvoked;
            button.GotFocus += OnButtonGotFocus;
            button.LostFocus += OnButtonLostFocus;
            // handledEventsToo: the focus navigation of the window must not swallow the arrows first.
            button.AddHandler(KeyDownEvent, OnNodeKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
            _buttons.Add(button);
            VisualChildren.Add(button);
            LogicalChildren.Add(button);
        }
        RefreshButtonNames();
        InvalidateMeasure();
    }

    private void RefreshButtonNames()
    {
        if (Host is not { } host)
        {
            return;
        }
        for (var n = 0; n < _buttons.Count; n++)
        {
            AutomationProperties.SetName(_buttons[n], host.NodeAutomationName(n));
        }
    }

    private void Relayout()
    {
        if (_size.Width <= 0)
        {
            return;
        }
        JourneyFlowEngine.Layout(_model, _current, _size.Width, _size.Height, _layout);
        _geometryDirty = true;
        InvalidateArrange();
        InvalidateVisual();
    }

    // Tracing -------------------------------------------------------------------------------------------------------

    private void UpdateTrace()
    {
        int node = -1, link = -1, previewNode = -1, previewLink = -1;
        if (_hoverLink >= 0)
        {
            link = previewLink = _hoverLink;
        }
        else if (_hoverNode >= 0)
        {
            node = previewNode = _hoverNode;
        }
        else if (_focusNode >= 0)
        {
            node = previewNode = _focusNode;
        }
        else if (_selected >= 0)
        {
            node = _selected;
        }
        if (node != TraceNode || link != TraceLink)
        {
            TraceNode = node;
            TraceLink = link;
            RetargetCohort(ShouldAnimate());
        }
        if (previewNode != _previewNode || previewLink != _previewLink)
        {
            _previewNode = previewNode;
            _previewLink = previewLink;
            Host?.OnTraceChanged(previewNode, previewLink);
        }
        RefreshTooltip();
        InvalidateVisual();
    }

    private void RetargetCohort(bool animate)
    {
        var tracing = TraceNode >= 0 || TraceLink >= 0;
        if (TraceLink >= 0)
        {
            JourneyFlowEngine.LinkCohort(_model, _targets, TraceLink, _cohort);
        }
        else if (TraceNode >= 0)
        {
            JourneyFlowEngine.NodeCohort(_model, _targets, TraceNode, _cohort);
        }
        else
        {
            Array.Clear(_cohort);
        }
        for (var l = 0; l < _fractions.Length; l++)
        {
            var fraction = _targets[l] > 0 ? Math.Clamp(_cohort[l] / _targets[l], 0, 1) : 0;
            if (animate)
            {
                _fractions[l].SpringTo(fraction, AnalyticsSprings.Quick);
            }
            else
            {
                _fractions[l].Jump(fraction);
            }
        }
        StartTween(ref _dim, tracing ? 1 : 0);
        for (var n = 0; n < _labelAlpha.Length; n++)
        {
            var on = !tracing || JourneyFlowEngine.NodeCohortTotal(_model, _cohort, n) > 1e-9;
            StartTween(ref _labelAlpha[n], on ? 1 : OffLabelOpacity);
        }
        _geometryDirty = true;
        Kick();
    }

    private void RefreshTooltip()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (Host is not { } host || _size.Width <= 0 || (_previewNode < 0 && _previewLink < 0))
        {
            if (_readout.IsShown)
            {
                _readout.Hide(level);
                Kick();
            }
            return;
        }
        if (!_palette.IsReady)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return;
            }
            _palette.Refresh(this);
        }
        var content = _previewLink >= 0 ? host.LinkTooltip(_previewLink) : host.NodeTooltip(_previewNode);
        _readout.SetContent(content, level);
        var size = _readout.Measure(_text, _palette);
        Point position;
        if (_hoverNode >= 0 || _hoverLink >= 0)
        {
            position = AnalyticsReadout.NearPointer(_pointer, size, new Rect(_size));
        }
        else
        {
            var rect = _layout.Nodes[_previewNode];
            var x = rect.Right + 14 + size.Width <= _size.Width ? rect.Right + 14 : rect.X - 14 - size.Width;
            position = new Point(x, rect.Y - 8);
        }
        _readout.Show(position, level);
        Kick();
    }

    // Input ---------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _pointer = e.GetPosition(this);
        var (node, link) = HitTest(_pointer);
        Cursor = node >= 0 ? new Cursor(StandardCursorType.Hand) : null;
        if (node != _hoverNode || link != _hoverLink)
        {
            _hoverNode = node;
            _hoverLink = link;
            UpdateTrace();
        }
        else if (node >= 0 || link >= 0)
        {
            // The tooltip follows the pointer directly (no spring), as measured.
            RefreshTooltipPosition();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var (node, link) = HitTest(e.GetPosition(this));
        if (node >= 0)
        {
            _buttons[node].Focus(NavigationMethod.Pointer);
            Host?.OnNodeInvoked(node);
            e.Handled = true;
        }
        else if (link < 0)
        {
            Host?.OnClearRequested();
            e.Handled = true;
        }
    }

    private void RefreshTooltipPosition()
    {
        if (!_readout.IsShown)
        {
            return;
        }
        var size = _readout.Measure(_text, _palette);
        _readout.Show(AnalyticsReadout.NearPointer(_pointer, size, new Rect(_size)), ArcMotion.GetEffectiveLevel(this));
        InvalidateVisual();
    }

    private void OnButtonInvoked(object? sender, EventArgs e)
    {
        if (sender is JourneyFlowNodeButton button)
        {
            Host?.OnNodeInvoked(button.Index);
        }
    }

    private void OnButtonGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not JourneyFlowNodeButton button)
        {
            return;
        }
        _roving = button.Index;
        for (var i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].IsTabStop = i == _roving;
        }
        _focusNode = e.NavigationMethod == NavigationMethod.Pointer ? -1 : button.Index;
        UpdateTrace();
    }

    private void OnButtonLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is JourneyFlowNodeButton button && _focusNode == button.Index)
        {
            _focusNode = -1;
            UpdateTrace();
        }
        InvalidateVisual();
    }

    private void OnNodeKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not JourneyFlowNodeButton button || _model.NodeCount == 0)
        {
            return;
        }
        var node = button.Index;
        var next = -1;
        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            {
                var stage = _model.StageNodes[_model.NodeStage[node]];
                var rank = _model.NodeRank[node] + (e.Key == Key.Down ? 1 : -1);
                next = rank >= 0 && rank < stage.Length ? stage[rank] : node;
                break;
            }
            case Key.Left:
            case Key.Right:
                next = JourneyFlowEngine.Nearest(_model, _layout, node, e.Key == Key.Right ? 1 : -1);
                if (next < 0)
                {
                    next = node;
                }
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = _model.NodeCount - 1;
                break;
            case Key.Escape:
                Host?.OnClearRequested();
                e.Handled = true;
                return;
            default:
                return;
        }
        e.Handled = true;
        if (next != node)
        {
            _buttons[next].Focus(NavigationMethod.Directional);
        }
    }

    // Motion --------------------------------------------------------------------------------------------------------

    private bool ShouldAnimate() => this.IsAttachedToVisualTree() && ArcMotion.IsFull(this);

    private void StartTween(ref AnalyticsTween tween, double target)
    {
        if (ShouldAnimate())
        {
            tween.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
        }
        else
        {
            tween.Jump(target);
        }
    }

    private void Kick()
    {
        InvalidateVisual();
        if (this.IsAttachedToVisualTree())
        {
            // A new motion starts with one ordinary frame (see Advance).
            _freshStart = true;
            _loop.Start();
        }
    }

    /// <summary>One frame: counts, label counts, cohort, fades, tooltip. Returns true while anything moves.</summary>
    internal bool Advance(double dt)
    {
        if (_freshStart)
        {
            // The scheduler measures the first frame after an idle spell from its last frame (up to 0.25s): a motion that
            // just started would skip its first quarter second. Start it with one ordinary frame instead.
            _freshStart = false;
            dt = Math.Min(dt, 1.0 / 60.0);
        }
        var full = ArcMotion.IsFull(this);
        var moving = false;
        var countsMoved = false;
        for (var l = 0; l < _values.Length; l++)
        {
            ref var channel = ref _values[l];
            if (!channel.IsMoving)
            {
                continue;
            }
            if (!full)
            {
                channel.Jump(channel.Target);
            }
            else
            {
                moving |= channel.Step(dt);
            }
            _current[l] = channel.Value;
            countsMoved = true;
        }
        if (_labelsPending)
        {
            _labelDelay -= dt;
            if (_labelDelay <= 0 || !full)
            {
                _labelsPending = false;
                for (var n = 0; n < _labels.Length; n++)
                {
                    if (full)
                    {
                        _labels[n].SpringTo(_labelTargets[n], CountSpring);
                    }
                    else
                    {
                        _labels[n].Jump(_labelTargets[n]);
                    }
                }
            }
            moving |= _labelsPending;
        }
        for (var n = 0; n < _labels.Length; n++)
        {
            ref var channel = ref _labels[n];
            if (!channel.IsMoving)
            {
                continue;
            }
            if (!full)
            {
                channel.Jump(channel.Target);
            }
            else
            {
                moving |= channel.Step(dt);
            }
        }
        for (var l = 0; l < _fractions.Length; l++)
        {
            ref var channel = ref _fractions[l];
            if (!channel.IsMoving)
            {
                continue;
            }
            if (!full)
            {
                channel.Jump(channel.Target);
            }
            else
            {
                moving |= channel.Step(dt);
            }
            _geometryDirty = true;
        }
        moving |= _dim.Advance(dt);
        for (var n = 0; n < _labelAlpha.Length; n++)
        {
            moving |= _labelAlpha[n].Advance(dt);
            moving |= _ringAlpha[n].Advance(dt);
        }
        moving |= _readout.Advance(dt);
        if (countsMoved)
        {
            Relayout();
        }
        InvalidateVisual();
        return moving;
    }

    // Layout --------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        var height = JourneyFlowEngine.ChartHeight(width);
        foreach (var button in _buttons)
        {
            button.Measure(new Size(JourneyFlowEngine.NodeWidth + 12, height));
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = new Size(finalSize.Width, JourneyFlowEngine.ChartHeight(finalSize.Width));
        if (size != _size)
        {
            // A width change lays the chart out again at the new size; it never animates.
            _size = size;
            Relayout();
            RefreshTooltip();
        }
        for (var n = 0; n < _buttons.Count && n < _layout.Nodes.Length; n++)
        {
            _buttons[n].Arrange(JourneyFlowEngine.NodeHitRect(_layout.Nodes[n]));
        }
        return size;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        OnThemeChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hoverNode = -1;
        _hoverLink = -1;
        _readout.Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsTightProperty)
        {
            InvalidateVisual();
        }
    }

    private void OnThemeChanged()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        _palette.Refresh(this);
        _text.Clear();
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartPeer(this);

    private sealed class ChartPeer(JourneyFlowChart owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;
    }

    // Rendering -----------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (_size.Width <= 0)
        {
            return;
        }
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var p = _palette;
        var bounds = new Rect(_size);
        // Hit area of the whole chart (clicking empty space clears the selection).
        context.FillRectangle(Brushes.Transparent, bounds);
        EnsureGeometry();
        var dim = Math.Clamp(_dim.Value, 0, 1);
        var model = _model;
        var layout = _layout;
        var density = JourneyFlowEngine.Density(_size.Width);
        var tight = IsTight;

        // Guides: one per stage through the node centres and the bottom rule.
        var guide = p.Pen(p.Border, 1, cap: PenLineCap.Flat);
        var ruleY = layout.BaselineY;
        for (var s = 0; s < model.StageCount; s++)
        {
            var x = layout.ColumnX[s] + 4.5;
            context.DrawLine(guide, new Point(x, layout.Top), new Point(x, ruleY));
        }
        context.DrawLine(guide, new Point(0, ruleY), new Point(_size.Width, ruleY));

        var body = new Typeface(p.BodyFont);
        var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        if (Host is { } host)
        {
            var stageSize = tight ? 10 : 11;
            var stageBrush = p.Brush(p.TextMuted);
            for (var s = 0; s < model.StageCount; s++)
            {
                var name = host.StageHeader(s, density == JourneyLabelDensity.Compact);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                var text = _text.Get(name, body, stageSize);
                var last = s == model.StageCount - 1 && s > 0;
                var x = last ? _size.Width - text.WidthIncludingTrailingWhitespace : layout.ColumnX[s];
                text.DrawAtBaseline(context, stageBrush, x, layout.StageNameBaseline);
            }
        }

        // Ribbons, then the cohort traces on top of them.
        for (var l = 0; l < model.LinkCount; l++)
        {
            if (_ribbons[l] is not { } geometry)
            {
                continue;
            }
            var kind = model.NodeKind[model.LinkTarget[l]];
            var color = kind == JourneyNodeKind.Goal ? p.Success : p.Accent;
            var rest = kind switch
            {
                JourneyNodeKind.Goal => 0.16,
                JourneyNodeKind.Exit => 0.04,
                _ => 0.10
            };
            var dimmed = kind == JourneyNodeKind.Goal ? DimRibbonGoal : DimRibbonStep;
            context.DrawGeometry(p.Brush(color, rest + (dimmed - rest) * dim), null, geometry);
        }
        var traceBrush = p.Brush(p.Accent, TraceAlpha);
        for (var l = 0; l < model.LinkCount; l++)
        {
            if (_traces[l] is { } trace)
            {
                context.DrawGeometry(traceBrush, null, trace);
            }
        }

        // Node bars (faded while tracing), then the cohort's part of them in solid Accent.
        using (context.PushOpacity(1 - (1 - DimNodeOpacity) * dim))
        {
            for (var n = 0; n < model.NodeCount; n++)
            {
                var rect = layout.Nodes[n];
                if (rect.Height <= 0)
                {
                    continue;
                }
                var color = model.NodeKind[n] switch
                {
                    JourneyNodeKind.Goal => p.Success,
                    JourneyNodeKind.Exit => p.TextMuted,
                    _ => p.AccentStrong
                };
                context.DrawRectangle(p.Brush(color), null, new RoundedRect(rect, JourneyFlowEngine.NodeRadius));
            }
        }
        var overlay = p.Brush(p.Accent);
        for (var l = 0; l < model.LinkCount; l++)
        {
            var h = _fractions[l].Value * layout.LinkThickness[l];
            if (h <= 0.01)
            {
                continue;
            }
            var source = layout.Nodes[model.LinkSource[l]];
            var target = layout.Nodes[model.LinkTarget[l]];
            context.DrawRectangle(overlay, null, new RoundedRect(new Rect(source.X, layout.LinkSourceY[l], JourneyFlowEngine.NodeWidth, h), JourneyFlowEngine.NodeRadius));
            context.DrawRectangle(overlay, null, new RoundedRect(new Rect(target.X, layout.LinkTargetY[l], JourneyFlowEngine.NodeWidth, h), JourneyFlowEngine.NodeRadius));
        }

        if (Host is { } labelHost)
        {
            DrawLabels(context, labelHost, body, medium, density, tight);
        }

        // Selection: a 1px BorderStrong ring inside the node's hit box; focus: 2px FocusRing 2px outside it.
        for (var n = 0; n < model.NodeCount; n++)
        {
            var alpha = _ringAlpha[n].Value;
            if (alpha <= 0.001)
            {
                continue;
            }
            var hit = JourneyFlowEngine.NodeHitRect(layout.Nodes[n]);
            context.DrawRectangle(null, p.Pen(p.BorderStrong, 1, alpha, cap: PenLineCap.Flat), new RoundedRect(hit.Deflate(0.5), 5.5));
        }
        for (var n = 0; n < _buttons.Count; n++)
        {
            if (AnalyticsFocus.IsVisible(_buttons[n]))
            {
                AnalyticsFocus.Draw(context, p, JourneyFlowEngine.NodeHitRect(layout.Nodes[n]), 6);
            }
        }

        _readout.Draw(context, _text, p);
    }

    private void DrawLabels(DrawingContext context, IJourneyFlowChartHost host, Typeface body, Typeface medium, JourneyLabelDensity density, bool tight)
    {
        var p = _palette;
        var model = _model;
        var layout = _layout;
        var nameSize = tight ? 11 : 12;
        var countSize = tight ? 10 : 11;
        var nameBrush = p.Brush(p.Foreground);
        var countBrush = p.Brush(p.TextSecondary);
        var halo = p.Brush(p.Surface);
        double visitors = 0;
        if (model.StageCount > 0)
        {
            foreach (var n in model.StageNodes[0])
            {
                visitors += _labels[n].Value;
            }
        }
        var lastStage = model.StageCount - 1;
        for (var n = 0; n < model.NodeCount; n++)
        {
            var stage = model.NodeStage[n];
            var rect = layout.Nodes[n];
            var rightAligned = stage == lastStage && lastStage > 0;
            var anchor = rightAligned ? _size.Width : rect.X;
            var showName = density != JourneyLabelDensity.Compact || model.NodeRank[n] > 0;
            var showShare = density == JourneyLabelDensity.Full && stage > 0;
            var count = CountText(host, n, showShare, visitors);
            using (context.PushOpacity(Math.Clamp(_labelAlpha[n].Value, 0, 1)))
            {
                if (showName)
                {
                    var name = host.NodeHeader(n);
                    if (!string.IsNullOrEmpty(name))
                    {
                        var text = _text.Get(name, medium, nameSize);
                        DrawHaloed(context, text, rightAligned ? anchor - text.WidthIncludingTrailingWhitespace : anchor, rect.Y + JourneyFlowEngine.NameBaseline, nameBrush, halo);
                    }
                }
                var countText = _text.Get(count, body, countSize, tabular: true);
                DrawHaloed(context, countText, rightAligned ? anchor - countText.WidthIncludingTrailingWhitespace : anchor, rect.Y + JourneyFlowEngine.CountBaseline, countBrush, halo);
            }
        }
    }

    private string CountText(IJourneyFlowChartHost host, int node, bool showShare, double visitors)
    {
        var value = Math.Round(_labels[node].Value);
        var key = showShare ? value + 0.25 : value;
        if (_countText[node] is { } cached && _countShown[node].Equals(key))
        {
            return cached;
        }
        var text = host.FormatCount(value);
        if (showShare)
        {
            text += " · " + host.FormatLabelShare(visitors > 0 ? value / visitors : 0);
        }
        _countText[node] = text;
        _countShown[node] = key;
        return text;
    }

    /// <summary>
    /// Text with a 4px Surface halo (Arc: a 4px round-joined Surface stroke painted under the fill), so labels stay
    /// readable over ribbons. Drawn as eight 2px-offset copies of the cached layout: no geometry per frame.
    /// </summary>
    private static void DrawHaloed(DrawingContext context, AnalyticsText text, double x, double baseline, IBrush fill, IBrush halo)
    {
        const double r = 2;
        const double d = 1.41;
        text.DrawAtBaseline(context, halo, x - r, baseline);
        text.DrawAtBaseline(context, halo, x + r, baseline);
        text.DrawAtBaseline(context, halo, x, baseline - r);
        text.DrawAtBaseline(context, halo, x, baseline + r);
        text.DrawAtBaseline(context, halo, x - d, baseline - d);
        text.DrawAtBaseline(context, halo, x + d, baseline - d);
        text.DrawAtBaseline(context, halo, x - d, baseline + d);
        text.DrawAtBaseline(context, halo, x + d, baseline + d);
        text.DrawAtBaseline(context, fill, x, baseline);
    }

    private void EnsureGeometry()
    {
        if (!_geometryDirty)
        {
            return;
        }
        _geometryDirty = false;
        for (var l = 0; l < _model.LinkCount; l++)
        {
            var thickness = _layout.LinkThickness[l];
            var x0 = _layout.LinkSourceX(_model, l);
            var x1 = _layout.LinkTargetX(_model, l);
            var s = _layout.LinkSourceY[l];
            var t = _layout.LinkTargetY[l];
            _ribbons[l] = thickness > 0 ? AnalyticsGeometry.Ribbon(x0, s, s + thickness, x1, t, t + thickness) : null;
            var traced = _fractions[l].Value * thickness;
            _traces[l] = traced > 0.01 ? AnalyticsGeometry.Ribbon(x0, s, s + traced, x1, t, t + traced) : null;
        }
    }
}

/// <summary>
/// The invisible toggle button of one journey node: keyboard focus (one tab stop for all nodes, moved with the arrow
/// keys), the pressed state for screen readers (selected), and the spoken name. Pressing it asks the chart to toggle the
/// selection instead of toggling itself. It takes no pointer input: the chart hit-tests the drawing.
/// </summary>
internal sealed class JourneyFlowNodeButton : ToggleButton
{
    public JourneyFlowNodeButton()
    {
        IsHitTestVisible = false;
        FocusAdorner = null;
        Background = Brushes.Transparent;
    }

    /// <summary>The node index.</summary>
    public int Index { get; init; }

    /// <summary>Raised on click, Enter or Space.</summary>
    public event EventHandler? Invoked;

    protected override Type StyleKeyOverride => typeof(JourneyFlowNodeButton);

    protected override void Toggle() => Invoked?.Invoke(this, EventArgs.Empty);
}
