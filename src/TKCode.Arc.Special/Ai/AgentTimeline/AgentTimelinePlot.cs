using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The timeline's plot, one self-drawn control: grid lines, the elbow connectors, every span (tint, solid or hatched
/// fill, the square leading edge and shine while active, the corner, color and icon change on completion, hover and
/// dimming), the port dots, the playhead, the axis with its ticks and the scrubber knob. Nothing is a control per span:
/// a frame only redraws (no layout), and nothing is allocated while drawing (brushes and pens come from the palette,
/// the hatch is one cached strip, tick texts are cached text layouts; only the elbows are rebuilt, and only when the
/// scale changes).
/// <para>
/// Spans still take keyboard focus: two transparent focus proxies (<see cref="AgentTimelineSpanProxy"/>, buttons to
/// assistive technology, named like the span, toggled when pinned) take turns sitting on the roving span, so every
/// arrow key is a real focus change a screen reader announces; the plot draws the focus ring. The axis band is an
/// <see cref="AgentTimelineScrubber"/> (a slider to assistive technology) whose knob the plot draws.
/// </para>
/// Times: the story clock positions everything (spans grow with it, connectors and dots mount at their moment);
/// one-shot transitions (completion 160 ms, connector reveal 360 ms, port pop 160 ms, dimming and hover 160 ms, the
/// playhead fade 240 ms, the knob growing on focus 160 ms) and the 1.5 s shine run on the clock's wall time, so they keep
/// their pace at any speed and finish while paused, as measured.
/// </summary>
internal sealed class AgentTimelinePlot : Control
{
    private const double CompleteDuration = 0.16;
    private const double RevealDuration = 0.36;
    private const double PortDuration = 0.16;
    private const double ResultPortDelay = 0.26;
    private const double DimDuration = 0.16;
    private const double PlayheadDuration = 0.24;
    private const double DimSpanOpacity = 0.28;
    private const double DimLinkOpacity = 0.16;
    private const double PlayheadOpacity = 0.5;

    private struct SpanState
    {
        public byte Phase;
        public double ActiveAt;
        public double DoneAt;
        public ExplainerValue Hover;
        public ExplainerValue Dim;
    }

    private struct LinkState
    {
        public bool Mounted;
        public double MountedAt;
        public ExplainerValue Dim;
        public StreamGeometry? Elbow;
    }

    private readonly AgentTimelineSpanProxy[] _proxies = new AgentTimelineSpanProxy[2];
    private readonly AgentTimelineScrubber _scrubber;
    private readonly Dictionary<string, TextLayout> _ticks = new(StringComparer.Ordinal);
    private AgentTimelineModel _model = AgentTimelineModel.Empty;
    private AgentTimelinePalette? _palette;
    private AgentTimelineRowPalette[] _rows = [];
    private SpanState[] _spans = [];
    private LinkState[] _links = [];
    private bool[] _spanOn = [];
    private bool[] _linkOn = [];
    private StreamGeometry? _hatch;
    private double _hatchWidth;
    private double _elbowScale = double.NaN;
    private double _plotWidth = 530;
    private double _axis = 1;
    private double _duration;
    private double _now;
    private double _wall;
    private double _openEnd;
    private bool _live;
    private bool _full = true;
    private bool _knobVisible = true;
    private int _pinned = -1;
    private int _hovered = -1;
    private int _roving = -1;
    private int _activeProxy;
    private ExplainerValue _playhead;
    private ExplainerValue _knob = new(1);
    private Typeface _typeface;
    private Color _tickColor;

    public AgentTimelinePlot()
    {
        ClipToBounds = false;
        Focusable = false;
        for (var i = 0; i < _proxies.Length; i++)
        {
            var proxy = new AgentTimelineSpanProxy(this);
            proxy.GotFocus += OnProxyFocusChanged;
            proxy.LostFocus += OnProxyFocusChanged;
            _proxies[i] = proxy;
            VisualChildren.Add(proxy);
            LogicalChildren.Add(proxy);
        }
        _scrubber = new AgentTimelineScrubber(this);
        _scrubber.GotFocus += OnKnobFocusChanged;
        _scrubber.LostFocus += OnKnobFocusChanged;
        VisualChildren.Add(_scrubber);
        LogicalChildren.Add(_scrubber);
        UpdateProxies();
    }

    /// <summary>The timeline this plot belongs to (set by the timeline when it applies its template).</summary>
    public AgentTimeline? Host { get; set; }

    public AgentTimelineScrubber Scrubber => _scrubber;

    public AgentTimelineModel Model => _model;

    /// <summary>The seconds per pixel factor in use (px per second).</summary>
    public double Scale => _plotWidth / Math.Max(_axis, 1e-6);

    public double PlotWidth => _plotWidth;

    public double Axis => _axis;

    /// <summary>The span the keyboard is on, or −1.</summary>
    public int Roving => _roving;

    /// <summary>The span under the pointer, or −1.</summary>
    public int Hovered => _hovered;

    /// <summary>The pinned span, or −1.</summary>
    public int Pinned => _pinned;

    /// <summary>True while a span proxy has keyboard focus (any method).</summary>
    public bool HasSpanFocus => _proxies[0].IsFocused || _proxies[1].IsFocused;

    /// <summary>True while a span proxy has focus that came from the keyboard.</summary>
    public bool HasVisibleSpanFocus => IsFocusVisible(_proxies[0]) || IsFocusVisible(_proxies[1]);

    /// <summary>The focused span proxy (tests).</summary>
    internal AgentTimelineSpanProxy ActiveProxy => _proxies[_activeProxy];

    /// <summary>The playhead's opacity now (tests).</summary>
    internal double PlayheadAlpha => Math.Clamp(_playhead.Value(_wall), 0, 1);

    /// <summary>A span's drawn opacity (1, or 0.28 when off the pinned path) (tests).</summary>
    internal double SpanOpacity(int span) => span >= 0 && span < _spans.Length ? _spans[span].Dim.Value(_wall) : 1;

    /// <summary>A connector's drawn opacity (tests).</summary>
    internal double LinkOpacity(int link) => link >= 0 && link < _links.Length ? _links[link].Dim.Value(_wall) : 1;

    /// <summary>How far a connector has drawn in, 0 to 1 (tests).</summary>
    internal double LinkReveal(int link) => link >= 0 && link < _links.Length && _links[link].Mounted ? Reveal(_links[link]) : 0;

    /// <summary>A span's state: 0 not started, 1 active, 2 done (tests).</summary>
    internal int SpanPhase(int span) => span >= 0 && span < _spans.Length ? _spans[span].Phase : 0;

    /// <summary>The knob's scale (1, 1.3 while focused or dragged) (tests).</summary>
    internal double KnobScale => _knob.Value(_wall);

    // ---- Data from the timeline ------------------------------------------------------------------------------------

    public void SetModel(AgentTimelineModel model)
    {
        _model = model;
        _spans = new SpanState[model.Spans.Length];
        _links = new LinkState[model.Handoffs.Length];
        _spanOn = new bool[model.Spans.Length];
        _linkOn = new bool[model.Handoffs.Length];
        for (var i = 0; i < _spans.Length; i++)
        {
            _spans[i].Dim = new ExplainerValue(1);
            _spans[i].DoneAt = double.NegativeInfinity;
        }
        for (var i = 0; i < _links.Length; i++)
        {
            _links[i].Dim = new ExplainerValue(1);
            _links[i].MountedAt = double.NegativeInfinity;
        }
        _elbowScale = double.NaN;
        _pinned = -1;
        _hovered = -1;
        if (_roving >= model.Spans.Length)
        {
            _roving = -1;
        }
        RebuildRows();
        SyncStates(animate: false);
        UpdateProxies();
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void SetPalette(AgentTimelinePalette palette)
    {
        _palette = palette;
        _ticks.Clear();
        RebuildRows();
        InvalidateVisual();
    }

    private void RebuildRows()
    {
        if (_palette is not { } palette)
        {
            _rows = [];
            return;
        }
        _rows = new AgentTimelineRowPalette[_model.Rows.Length];
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i] = palette.Row(i, _model.Rows[i].Agent);
        }
    }

    /// <summary>The palette row of an agent (labels and the card use the same colors).</summary>
    public AgentTimelineRowPalette? RowPalette(int row) => row >= 0 && row < _rows.Length ? _rows[row] : null;

    public void SetPlotWidth(double width)
    {
        if (Math.Abs(width - _plotWidth) < 0.01)
        {
            return;
        }
        _plotWidth = width;
        _elbowScale = double.NaN;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Sets the axis length (seconds the plot width shows) and the run's length (where scrubbing stops).</summary>
    public void SetAxis(double axis, double duration)
    {
        axis = Math.Max(axis, 1e-3);
        if (Math.Abs(axis - _axis) > 1e-9 || Math.Abs(duration - _duration) > 1e-9)
        {
            _axis = axis;
            _duration = duration;
            _elbowScale = double.NaN;
            InvalidateVisual();
        }
    }

    public double Duration => _duration;

    public bool IsLive => _live;

    /// <summary>
    /// Moves the view to story time <paramref name="now"/> and wall time <paramref name="wall"/>. Spans mount, become active
    /// and complete, and connectors mount, by the story time; with <paramref name="animate"/> (forward playback at full
    /// motion) those changes play their transitions, otherwise (a seek, reduced motion) they apply at once.
    /// </summary>
    public void Advance(double now, double wall, double openEnd, bool live, bool full, bool animate)
    {
        var moved = Math.Abs(now - _now) > 1e-12 || Math.Abs(openEnd - _openEnd) > 1e-12 || live != _live || full != _full;
        _now = now;
        _wall = Math.Max(_wall, wall);
        _openEnd = openEnd;
        _live = live;
        _full = full;
        var changed = SyncStates(animate && full);
        if (moved || changed || IsAnimating())
        {
            InvalidateVisual();
        }
        if (changed)
        {
            UpdateProxies();
        }
    }

    private bool SyncStates(bool animate)
    {
        var changed = false;
        var spans = _model.Spans;
        for (var i = 0; i < spans.Length; i++)
        {
            var span = spans[i];
            var end = span.EndAt(_openEnd);
            byte phase = _now + 1e-9 < span.Start ? (byte)0 : _now < end - 1e-9 || (span.IsOpen && _live) ? (byte)1 : (byte)2;
            ref var state = ref _spans[i];
            if (phase == state.Phase)
            {
                continue;
            }
            changed = true;
            if (phase == 1)
            {
                state.ActiveAt = _wall;
            }
            else if (phase == 2)
            {
                state.DoneAt = animate && state.Phase == 1 ? _wall : double.NegativeInfinity;
            }
            state.Phase = phase;
        }
        var links = _model.Handoffs;
        for (var i = 0; i < links.Length; i++)
        {
            var mounted = _now + 1e-9 >= links[i].At;
            ref var state = ref _links[i];
            if (mounted == state.Mounted)
            {
                continue;
            }
            changed = true;
            state.Mounted = mounted;
            state.MountedAt = mounted && animate ? _wall : double.NegativeInfinity;
        }
        return changed;
    }

    /// <summary>True while a transition runs at the current wall time (the clock keeps its frames coming).</summary>
    public bool IsAnimating()
    {
        var wall = _wall;
        if (_playhead.IsMoving(wall) || _knob.IsMoving(wall))
        {
            return true;
        }
        for (var i = 0; i < _spans.Length; i++)
        {
            ref var state = ref _spans[i];
            if (wall - state.DoneAt < CompleteDuration || state.Hover.IsMoving(wall) || state.Dim.IsMoving(wall))
            {
                return true;
            }
        }
        for (var i = 0; i < _links.Length; i++)
        {
            ref var state = ref _links[i];
            if (wall - state.MountedAt < ResultPortDelay + PortDuration || state.Dim.IsMoving(wall))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>True while an active thinking or tool span shows its looping shine (full motion only).</summary>
    public bool HasShine
    {
        get
        {
            if (!_full)
            {
                return false;
            }
            for (var i = 0; i < _spans.Length; i++)
            {
                if (_spans[i].Phase == 1 && _model.Spans[i].Kind != AgentTimelineSpanKind.Waiting)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Fades the playhead in (0.5) or out (240 ms, or at once).</summary>
    public void SetPlayheadVisible(bool visible, bool animate)
    {
        var target = visible ? PlayheadOpacity : 0;
        if (!animate)
        {
            _playhead.Jump(target);
        }
        else if (Math.Abs(_playhead.Target - target) > 1e-9)
        {
            _playhead.Tween(target, _wall, PlayheadDuration, ArcEasings.Standard);
        }
        InvalidateVisual();
    }

    /// <summary>Shows or hides the knob (hidden in live mode).</summary>
    public void SetKnobVisible(bool visible)
    {
        if (_knobVisible != visible)
        {
            _knobVisible = visible;
            _scrubber.IsVisible = visible;
            InvalidateVisual();
        }
    }

    /// <summary>Pins a span (−1 unpins): spans off its handoff path dim to 28%, connectors and dots to 16% (160 ms).</summary>
    public void SetPinned(int span, bool animate)
    {
        _pinned = span >= 0 && span < _spans.Length ? span : -1;
        if (_pinned >= 0)
        {
            _model.PathOf(_pinned, _spanOn, _linkOn);
        }
        for (var i = 0; i < _spans.Length; i++)
        {
            Fade(ref _spans[i].Dim, _pinned < 0 || _spanOn[i] ? 1 : DimSpanOpacity, animate);
        }
        for (var i = 0; i < _links.Length; i++)
        {
            Fade(ref _links[i].Dim, _pinned < 0 || _linkOn[i] ? 1 : DimLinkOpacity, animate);
        }
        UpdateProxies();
        InvalidateVisual();
    }

    /// <summary>True when the span is on the pinned span's handoff path (or nothing is pinned) (tests).</summary>
    internal bool IsOnPath(int span) => _pinned < 0 || (span >= 0 && span < _spanOn.Length && _spanOn[span]);

    /// <summary>True when the connector is on the pinned path (or nothing is pinned) (tests).</summary>
    internal bool IsLinkOnPath(int link) => _pinned < 0 || (link >= 0 && link < _linkOn.Length && _linkOn[link]);

    private void Fade(ref ExplainerValue value, double target, bool animate)
    {
        if (!animate)
        {
            value.Jump(target);
        }
        else if (Math.Abs(value.Target - target) > 1e-9)
        {
            value.Tween(target, _wall, DimDuration, ArcEasings.Standard);
        }
    }

    /// <summary>Marks the span under the pointer (thinking and tool spans tint on hover, 160 ms).</summary>
    public void SetHovered(int span, bool animate)
    {
        if (span == _hovered)
        {
            return;
        }
        if (_hovered >= 0 && _hovered < _spans.Length)
        {
            Fade(ref _spans[_hovered].Hover, 0, animate);
        }
        _hovered = span;
        if (span >= 0 && span < _spans.Length)
        {
            Fade(ref _spans[span].Hover, 1, animate);
        }
        Cursor = span >= 0 ? s_hand : null;
        InvalidateVisual();
    }

    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    /// <summary>
    /// Moves the roving span to <paramref name="span"/>. With <paramref name="focus"/> the other proxy takes the span and
    /// focus moves to it (by <paramref name="method"/>), so assistive technology hears the new span.
    /// </summary>
    public void SetRoving(int span, bool focus, NavigationMethod method = NavigationMethod.Directional)
    {
        if (span < 0 || span >= _spans.Length)
        {
            _roving = -1;
            UpdateProxies();
            return;
        }
        var hadFocus = HasSpanFocus;
        var moved = span != _roving;
        _roving = span;
        if (focus && (moved || !hadFocus))
        {
            if (hadFocus && moved)
            {
                _activeProxy = 1 - _activeProxy;
            }
            UpdateProxies();
            InvalidateArrange();
            UpdateLayout();
            _proxies[_activeProxy].Focus(method);
        }
        else
        {
            UpdateProxies();
            InvalidateArrange();
        }
        InvalidateVisual();
    }

    /// <summary>Refreshes the proxies' names, pressed states and tab stops (after data, pin or label changes).</summary>
    public void UpdateProxies()
    {
        var roving = _roving >= 0 && _roving < _model.Spans.Length && _model.IsMounted(_roving, _now) ? _roving : -1;
        for (var i = 0; i < _proxies.Length; i++)
        {
            var proxy = _proxies[i];
            var active = i == _activeProxy;
            var span = active ? roving : -1;
            proxy.Span = span;
            proxy.IsVisible = active ? roving >= 0 || _model.FirstMounted(_now) >= 0 : proxy.IsFocused;
            proxy.IsTabStop = active;
            proxy.IsPressed = span >= 0 && span == _pinned;
            var index = span >= 0 ? span : _model.FirstMounted(_now);
            if (index >= 0)
            {
                var info = _model.Spans[index];
                var agent = _model.Rows[info.Row].Agent;
                AutomationProperties.SetName(proxy, Host?.Labels.DescribeSpan(agent, info.Span) ?? info.Span.Label);
            }
            else
            {
                AutomationProperties.SetName(proxy, Host?.Labels.Spans);
            }
        }
    }

    /// <summary>Called by a proxy that got focus without a span (Tab into the plot): it lands on the remembered or first span.</summary>
    internal int EnsureRoving()
    {
        if (_roving < 0 || _roving >= _spans.Length || !_model.IsMounted(_roving, _now))
        {
            _roving = _model.FirstMounted(_now);
            UpdateProxies();
            InvalidateArrange();
        }
        return _roving;
    }

    private static bool IsFocusVisible(Control control) => control.IsFocused && control.Classes.Contains(":focus-visible");

    private void OnProxyFocusChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is AgentTimelineSpanProxy { IsFocused: true })
        {
            EnsureRoving();
        }
        Host?.OnSpanFocusChanged();
        InvalidateVisual();
    }

    private void OnKnobFocusChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => UpdateKnob();

    /// <summary>Grows the knob to 1.3 while it has focus or is dragged (160 ms), back to 1 otherwise.</summary>
    internal void UpdateKnob()
    {
        var target = _scrubber.IsFocused || _scrubber.IsDragging ? AgentTimelineLayout.KnobFocusScale : 1;
        if (!_full)
        {
            _knob.Jump(target);
        }
        else if (Math.Abs(_knob.Target - target) > 1e-9)
        {
            _knob.Tween(target, _wall, 0.16, ArcEasings.Standard);
        }
        Host?.RequestFrames(0.2);
        InvalidateVisual();
    }

    // ---- Hit testing and keys ---------------------------------------------------------------------------------------

    /// <summary>The mounted span under <paramref name="point"/> (plot coordinates), or −1. Tiny spans hit at least 6 px wide.</summary>
    public int SpanAt(Point point)
    {
        var row = (int)Math.Floor((point.Y - AgentTimelineLayout.TopPadding) / AgentTimelineLayout.RowHeight);
        if (row < 0 || row >= _model.Rows.Length)
        {
            return -1;
        }
        var top = AgentTimelineLayout.BarY(row) - 2;
        if (point.Y < top || point.Y > top + AgentTimelineLayout.BarHeight + 4)
        {
            return -1;
        }
        var info = _model.Rows[row];
        var scale = Scale;
        var best = -1;
        for (var i = info.First; i < info.First + info.Count; i++)
        {
            var span = _model.Spans[i];
            if (!_model.IsMounted(i, _now))
            {
                break;
            }
            var rect = VisibleRect(i, scale);
            var left = rect.X;
            var right = rect.Right;
            if (rect.Width < 6)
            {
                var middle = (left + right) / 2;
                left = middle - 3;
                right = middle + 3;
            }
            if (point.X >= left && point.X <= right)
            {
                best = i;
                if (span.Start * scale <= point.X)
                {
                    // Later spans win where hit areas of tiny spans overlap.
                    continue;
                }
            }
        }
        return best;
    }

    /// <summary>The drawn box of a span now (start to now or its end), in plot coordinates.</summary>
    public Rect VisibleRect(int span, double scale)
    {
        var info = _model.Spans[span];
        var end = Math.Max(info.Start, Math.Min(info.EndAt(_openEnd), _now));
        return AgentTimelineLayout.SpanRect(info.Start, end, scale, info.Row);
    }

    /// <summary>The whole box of a span (start to its end), in plot coordinates: where the card anchors.</summary>
    public Rect FullRect(int span)
    {
        var info = _model.Spans[span];
        return AgentTimelineLayout.SpanRect(info.Start, Math.Max(info.Start, info.EndAt(_openEnd)), Scale, info.Row);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        Host?.OnPlotHover(SpanAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Host?.OnPlotHover(-1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var span = SpanAt(e.GetPosition(this));
        if (span >= 0)
        {
            Host?.OnSpanPressed(span);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not AgentTimelineSpanProxy || Host is null)
        {
            return;
        }
        AgentTimelineKeyMove? move = e.Key switch
        {
            Key.Left => AgentTimelineKeyMove.Previous,
            Key.Right => AgentTimelineKeyMove.Next,
            Key.Up => AgentTimelineKeyMove.Up,
            Key.Down => AgentTimelineKeyMove.Down,
            Key.Home => AgentTimelineKeyMove.First,
            Key.End => AgentTimelineKeyMove.Last,
            _ => null
        };
        if (move is { } m)
        {
            Host.MoveRoving(m);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            Host.TogglePin(EnsureRoving());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _pinned >= 0)
        {
            Host.Unpin();
            e.Handled = true;
        }
    }

    // ---- Layout --------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var proxy in _proxies)
        {
            proxy.Measure(Size.Infinity);
        }
        _scrubber.Measure(Size.Infinity);
        return new Size(_plotWidth, AgentTimelineLayout.PlotHeight(_model.Rows.Length));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < _proxies.Length; i++)
        {
            var span = _proxies[i].Span >= 0 ? _proxies[i].Span : _model.FirstMounted(_now);
            var rect = span >= 0 ? FullRect(span) : new Rect(0, 0, 1, 1);
            _proxies[i].Arrange(new Rect(rect.X, rect.Y, Math.Max(1, rect.Width), rect.Height));
        }
        var rows = AgentTimelineLayout.RowsHeight(_model.Rows.Length);
        _scrubber.Arrange(new Rect(0, rows, finalSize.Width, AgentTimelineLayout.AxisHeight));
        return finalSize;
    }

    // ---- Drawing -------------------------------------------------------------------------------------------------------

    private StreamGeometry Hatch(double width)
    {
        // "/" lines one 5 px period apart (7.07 px along the bottom), anchored at the bottom-right corner like the
        // reference's repeating gradient: the hatch travels with the leading edge of a growing waiting span.
        if (_hatch is not null && _hatchWidth >= width)
        {
            return _hatch;
        }
        _hatchWidth = Math.Max(width, 64);
        var pitch = 5 * Math.Sqrt(2);
        var first = 0.5 * Math.Sqrt(2);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var h = AgentTimelineLayout.BarHeight;
            for (var x = -first; x > -_hatchWidth - h; x -= pitch)
            {
                context.BeginFigure(new Point(x - 1, 1), false);
                context.LineTo(new Point(x + h + 1, -h - 1));
                context.EndFigure(false);
            }
        }
        _hatch = geometry;
        return geometry;
    }

    private void EnsureElbows(double scale)
    {
        if (!double.IsNaN(_elbowScale) && Math.Abs(_elbowScale - scale) < 1e-9)
        {
            return;
        }
        _elbowScale = scale;
        for (var i = 0; i < _links.Length; i++)
        {
            var link = _model.Handoffs[i];
            var x = link.At * scale;
            var top = AgentTimelineLayout.BarBottom(link.UpperRow);
            var middle = AgentTimelineLayout.BarCenter(link.LowerRow);
            double end = double.NaN;
            if (link.LowerSpan >= 0)
            {
                var lower = _model.Spans[link.LowerSpan];
                end = link.IsDispatch ? lower.Start * scale + AgentTimelineLayout.SpanInset : lower.End * scale - AgentTimelineLayout.SpanInset;
                if (!double.IsFinite(end))
                {
                    end = double.NaN;
                }
            }
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                AgentTimelineLayout.Elbow(context, link.IsDispatch, x, top, middle, end);
            }
            _links[i].Elbow = geometry;
        }
    }

    private double Reveal(in LinkState link)
    {
        if (double.IsNegativeInfinity(link.MountedAt))
        {
            return 1;
        }
        return ArcEasings.Standard.Ease(Math.Clamp((_wall - link.MountedAt) / RevealDuration, 0, 1));
    }

    private double PortPop(in LinkState link, bool dispatch)
    {
        if (double.IsNegativeInfinity(link.MountedAt))
        {
            return 1;
        }
        var delay = dispatch ? 0 : ResultPortDelay;
        return ArcEasings.Enter.Ease(Math.Clamp((_wall - link.MountedAt - delay) / PortDuration, 0, 1));
    }

    private double Completion(in SpanState state)
    {
        if (state.Phase != 2)
        {
            return 0;
        }
        if (double.IsNegativeInfinity(state.DoneAt))
        {
            return 1;
        }
        return ArcEasings.Standard.Ease(Math.Clamp((_wall - state.DoneAt) / CompleteDuration, 0, 1));
    }

    private double IconAppear(in SpanState state)
    {
        if (double.IsNegativeInfinity(state.DoneAt))
        {
            return 1;
        }
        return ArcEasings.Enter.Ease(Math.Clamp((_wall - state.DoneAt) / CompleteDuration, 0, 1));
    }

    private void EnsureText()
    {
        var family = TextElement.GetFontFamily(this);
        var typeface = new Typeface(family);
        var color = _palette?.Muted ?? Colors.Gray;
        if (typeface != _typeface || color != _tickColor)
        {
            foreach (var layout in _ticks.Values)
            {
                layout.Dispose();
            }
            _ticks.Clear();
            _typeface = typeface;
            _tickColor = color;
        }
    }

    private TextLayout TickText(string text)
    {
        if (!_ticks.TryGetValue(text, out var layout))
        {
            if (_ticks.Count > 64)
            {
                foreach (var old in _ticks.Values)
                {
                    old.Dispose();
                }
                _ticks.Clear();
            }
            layout = new TextLayout(text, _typeface, AgentTimelineLayout.TickFontSize, _palette?.MutedBrush ?? Brushes.Gray,
                TextAlignment.Left, TextWrapping.NoWrap, null, null, FlowDirection.LeftToRight, double.PositiveInfinity,
                double.PositiveInfinity, 12, 0, 1, s_tabular);
            _ticks[text] = layout;
        }
        return layout;
    }

    private string[] _tickLabels = [];
    private double _tickLabelStep;

    /// <summary>The tick texts for a step, formatted once per step (not per frame).</summary>
    private string[] TickLabels(double step, int count)
    {
        if (_tickLabels.Length != count || Math.Abs(_tickLabelStep - step) > 1e-12)
        {
            _tickLabelStep = step;
            _tickLabels = new string[count];
            for (var k = 0; k < count; k++)
            {
                _tickLabels[k] = AgentTimelineMath.Tick(k * step);
            }
        }
        return _tickLabels;
    }

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    public override void Render(DrawingContext context)
    {
        if (_palette is not { } palette)
        {
            return;
        }
        EnsureText();
        var scale = Scale;
        var width = Bounds.Width;
        var rowCount = _model.Rows.Length;
        var rowsHeight = AgentTimelineLayout.RowsHeight(rowCount);

        // Pointer input over the rows (the axis band is the scrubber's).
        context.FillRectangle(Brushes.Transparent, new Rect(0, 0, width, rowsHeight));

        // Grid lines at the ticks.
        var step = AgentTimelineMath.TickStep(_axis, _plotWidth);
        var ticks = AgentTimelineMath.TickCount(_axis, step);
        for (var k = 0; k < ticks; k++)
        {
            var x = Math.Round(k * step * scale) + 0.5;
            context.DrawLine(palette.GridPen, new Point(x, 0), new Point(x, rowsHeight));
        }

        EnsureElbows(scale);
        DrawConnectors(context, scale);
        DrawSpans(context, palette, scale);
        DrawPorts(context, palette, scale);

        // Keyboard focus ring on the roving span: 2 px, 2 px outside its box, following its corners.
        if (_roving >= 0 && _roving < _spans.Length && HasVisibleSpanFocus && _model.IsMounted(_roving, _now))
        {
            var rect = VisibleRect(_roving, scale);
            var radius = AgentTimelineLayout.SpanRadius + 3;
            context.DrawRectangle(null, palette.FocusPen, new RoundedRect(rect.Inflate(3), radius));
        }

        // Playhead.
        var playhead = Math.Clamp(_playhead.Value(_wall), 0, 1);
        if (playhead > 0.001)
        {
            var x = _now * scale;
            using (context.PushOpacity(playhead))
            {
                context.DrawLine(palette.PlayheadPen, new Point(x, 0), new Point(x, rowsHeight));
            }
        }

        // Axis line and tick labels.
        context.DrawLine(palette.AxisPen, new Point(0, rowsHeight + 0.5), new Point(width, rowsHeight + 0.5));
        var labels = TickLabels(step, ticks);
        for (var k = 0; k < ticks; k++)
        {
            var layout = TickText(labels[k]);
            var x = k * step * scale - layout.WidthIncludingTrailingWhitespace / 2;
            layout.Draw(context, new Point(x, rowsHeight + AgentTimelineLayout.TickLabelTop));
        }

        // Knob: a 9 px dot with a 2 px surface ring on the axis line, 1.3x while focused or dragged.
        if (_knobVisible)
        {
            var center = new Point(Math.Min(_now, _duration) * scale, rowsHeight + 0.5);
            var r = AgentTimelineLayout.KnobRadius * Math.Max(0.5, _knob.Value(_wall));
            context.DrawEllipse(palette.SurfaceBrush, null, center, r + AgentTimelineLayout.KnobRing, r + AgentTimelineLayout.KnobRing);
            context.DrawEllipse(palette.ForegroundBrush, null, center, r, r);
            if (IsFocusVisible(_scrubber))
            {
                var ring = r + 3;
                context.DrawEllipse(null, palette.FocusPen, center, ring, ring);
            }
        }
    }

    private void DrawConnectors(DrawingContext context, double scale)
    {
        for (var i = 0; i < _links.Length; i++)
        {
            ref var state = ref _links[i];
            if (!state.Mounted || state.Elbow is not { } elbow)
            {
                continue;
            }
            var link = _model.Handoffs[i];
            var palette = RowPalette(link.LowerRow);
            if (palette is null)
            {
                continue;
            }
            var alpha = Math.Clamp(state.Dim.Value(_wall), 0, 1);
            var reveal = _full ? Reveal(state) : 1;
            if (alpha <= 0.001 || reveal <= 0.001)
            {
                continue;
            }
            var top = AgentTimelineLayout.BarBottom(link.UpperRow) - 1;
            var bottom = AgentTimelineLayout.BarCenter(link.LowerRow) + 1.5;
            var height = (bottom - top) * reveal;
            var x = link.At * scale;
            var clip = link.IsDispatch
                ? new Rect(-10000, top, 20000 + x, height)
                : new Rect(-10000, bottom - height, 20000 + x, height);
            using (context.PushClip(clip))
            using (context.PushOpacity(alpha))
            {
                context.DrawGeometry(null, palette.Connector, elbow);
            }
        }
    }

    private void DrawPorts(DrawingContext context, AgentTimelinePalette palette, double scale)
    {
        for (var i = 0; i < _links.Length; i++)
        {
            ref var state = ref _links[i];
            if (!state.Mounted)
            {
                continue;
            }
            var link = _model.Handoffs[i];
            var row = RowPalette(link.LowerRow);
            if (row is null)
            {
                continue;
            }
            var pop = _full ? PortPop(state, link.IsDispatch) : 1;
            var alpha = Math.Clamp(state.Dim.Value(_wall), 0, 1) * pop;
            if (alpha <= 0.001)
            {
                continue;
            }
            var size = 0.4 + 0.6 * pop;
            var center = new Point(link.At * scale, AgentTimelineLayout.BarBottom(link.UpperRow));
            var r = AgentTimelineLayout.PortRadius * size;
            var ring = (AgentTimelineLayout.PortRadius + AgentTimelineLayout.PortRing) * size;
            using (context.PushOpacity(alpha))
            {
                context.DrawEllipse(palette.SurfaceBrush, null, center, ring, ring);
                context.DrawEllipse(row.Solid, null, center, r, r);
            }
        }
    }

    private void DrawSpans(DrawingContext context, AgentTimelinePalette palette, double scale)
    {
        var spans = _model.Spans;
        for (var i = 0; i < spans.Length; i++)
        {
            ref var state = ref _spans[i];
            if (state.Phase == 0)
            {
                continue;
            }
            var span = spans[i];
            var row = RowPalette(span.Row);
            if (row is null)
            {
                continue;
            }
            var rect = VisibleRect(i, scale);
            if (rect.Width <= 0.01)
            {
                continue;
            }
            var done = Completion(state);
            var right = Math.Min(AgentTimelineLayout.ActiveEdgeRadius + (AgentTimelineLayout.SpanRadius - AgentTimelineLayout.ActiveEdgeRadius) * done,
                rect.Width / 2);
            var left = Math.Min(AgentTimelineLayout.SpanRadius, rect.Width / 2);
            var radiusLeft = new Vector(left, Math.Min(left, rect.Height / 2));
            var radiusRight = new Vector(right, Math.Min(right, rect.Height / 2));
            var box = new RoundedRect(rect, radiusLeft, radiusRight, radiusRight, radiusLeft);
            var dim = Math.Clamp(state.Dim.Value(_wall), 0, 1);
            using (context.PushOpacity(dim))
            {
                var hover = Math.Clamp(state.Hover.Value(_wall), 0, 1);
                switch (span.Kind)
                {
                    case AgentTimelineSpanKind.Thinking:
                        context.DrawRectangle(row.Thinking, null, box);
                        if (hover > 0.001)
                        {
                            using (context.PushOpacity(hover))
                            {
                                context.DrawRectangle(row.ThinkingHover, null, box);
                            }
                        }
                        break;
                    case AgentTimelineSpanKind.Tool:
                        var failed = span.IsFailed ? done : 0;
                        if (failed < 0.999)
                        {
                            context.DrawRectangle(row.Solid, null, box);
                            var toolHover = hover * (1 - failed);
                            if (toolHover > 0.001)
                            {
                                using (context.PushOpacity(toolHover))
                                {
                                    context.DrawRectangle(row.ToolHover, null, box);
                                }
                            }
                        }
                        if (failed > 0.001)
                        {
                            using (context.PushOpacity(failed))
                            {
                                context.DrawRectangle(palette.FailedFill, null, box);
                                context.DrawRectangle(null, palette.FailedInset, Deflate(box, rect));
                            }
                        }
                        break;
                    default:
                        context.DrawRectangle(row.WaitingFill, null, box);
                        using (context.PushClip(box))
                        using (context.PushTransform(Matrix.CreateTranslation(rect.Right, rect.Bottom)))
                        {
                            context.DrawGeometry(null, row.Hatch, Hatch(_plotWidth + 64));
                        }
                        context.DrawRectangle(null, row.WaitingInset, Deflate(box, rect));
                        break;
                }

                // The square leading edge while active, fading as the span completes.
                var edge = state.Phase == 1 ? 1 : 1 - done;
                if (edge > 0.001 && rect.Width > 0)
                {
                    var brush = span.Kind == AgentTimelineSpanKind.Tool ? row.ToolEdge : row.Solid;
                    var strip = new Rect(rect.Right - Math.Min(AgentTimelineLayout.LeadingEdgeWidth, rect.Width), rect.Y,
                        Math.Min(AgentTimelineLayout.LeadingEdgeWidth, rect.Width), rect.Height);
                    using (context.PushClip(box))
                    using (context.PushOpacity(edge))
                    {
                        context.FillRectangle(brush, strip);
                    }
                }

                // The shine: a 28 px band sweeping across an active thinking or tool span every 1.5 s.
                if (state.Phase == 1 && _full && span.Kind != AgentTimelineSpanKind.Waiting)
                {
                    var offset = AgentTimelineLayout.ShineOffset(_wall - state.ActiveAt, rect.Width);
                    if (offset < rect.Width)
                    {
                        using (context.PushClip(box))
                        {
                            context.FillRectangle(palette.Shine, new Rect(rect.X + offset, rect.Y, AgentTimelineLayout.ShineWidth, rect.Height));
                        }
                    }
                }

                // A failed call shows a cross, a retry its arrow, once they end (160 ms).
                if (state.Phase == 2 && span.Kind == AgentTimelineSpanKind.Tool && (span.IsFailed || span.IsRetry))
                {
                    var appear = _full ? IconAppear(state) : 1;
                    var glyph = span.IsFailed ? ArcIcons.CircleX : ArcIcons.RotateCw;
                    var pen = span.IsFailed ? palette.FailedIcon : palette.RetryIcon;
                    var size = AgentTimelineLayout.IconSize * (0.5 + 0.5 * appear);
                    var center = rect.Center;
                    var matrix = Matrix.CreateScale(size / 24, size / 24) * Matrix.CreateTranslation(center.X - size / 2, center.Y - size / 2);
                    using (context.PushClip(box))
                    using (context.PushOpacity(appear))
                    using (context.PushTransform(matrix))
                    {
                        context.DrawGeometry(null, pen, glyph);
                    }
                }
            }
        }
    }

    /// <summary>The box half a pixel in, so a 1 px pen draws an inset outline.</summary>
    private static RoundedRect Deflate(RoundedRect box, Rect rect)
    {
        var inner = rect.Deflate(0.5);
        return new RoundedRect(inner,
            new Vector(Math.Max(0, box.RadiiTopLeft.X - 0.5), Math.Max(0, box.RadiiTopLeft.Y - 0.5)),
            new Vector(Math.Max(0, box.RadiiTopRight.X - 0.5), Math.Max(0, box.RadiiTopRight.Y - 0.5)),
            new Vector(Math.Max(0, box.RadiiBottomRight.X - 0.5), Math.Max(0, box.RadiiBottomRight.Y - 0.5)),
            new Vector(Math.Max(0, box.RadiiBottomLeft.X - 0.5), Math.Max(0, box.RadiiBottomLeft.Y - 0.5)));
    }
}

/// <summary>
/// A transparent focus stand-in for the roving span of an <see cref="AgentTimelinePlot"/>: a button to assistive
/// technology, named like the span ("Log reader, Tool call, Mail logs, failed: …"), toggled when the span is pinned.
/// Pointer input passes through it to the plot.
/// </summary>
internal sealed class AgentTimelineSpanProxy : Control
{
    private readonly AgentTimelinePlot _plot;

    public AgentTimelineSpanProxy(AgentTimelinePlot plot)
    {
        _plot = plot;
        Focusable = true;
        IsHitTestVisible = false;
        FocusAdorner = null;
    }

    /// <summary>The span it stands for, or −1.</summary>
    public int Span { get; set; } = -1;

    /// <summary>True when its span is pinned.</summary>
    public bool IsPressed { get; set; }

    internal AgentTimelinePlot Plot => _plot;

    protected override AutomationPeer OnCreateAutomationPeer() => new SpanProxyPeer(this);

    protected override Size MeasureOverride(Size availableSize) => default;

    private sealed class SpanProxyPeer : ControlAutomationPeer, IToggleProvider, IInvokeProvider
    {
        public SpanProxyPeer(AgentTimelineSpanProxy owner)
            : base(owner)
        {
        }

        private AgentTimelineSpanProxy Proxy => (AgentTimelineSpanProxy)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public ToggleState ToggleState => Proxy.IsPressed ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void Invoke()
        {
            var plot = Proxy.Plot;
            plot.Host?.TogglePin(Proxy.Span >= 0 ? Proxy.Span : plot.EnsureRoving());
        }
    }
}

/// <summary>
/// The axis band of an <see cref="AgentTimelinePlot"/> as a slider: press or drag anywhere on it to move the replay there
/// (it pauses), or use the keys (arrows ±0.5 s, Shift or Page Up / Page Down ±5 s, Home / End). The plot draws its knob.
/// </summary>
internal sealed class AgentTimelineScrubber : Control
{
    private static readonly Cursor s_resize = new(StandardCursorType.SizeWestEast);
    private readonly AgentTimelinePlot _plot;
    private bool _dragging;

    public AgentTimelineScrubber(AgentTimelinePlot plot)
    {
        _plot = plot;
        Focusable = true;
        IsTabStop = true;
        FocusAdorner = null;
        Cursor = s_resize;
    }

    /// <summary>True while the pointer drags the knob.</summary>
    public bool IsDragging => _dragging;

    internal AgentTimelinePlot Plot => _plot;

    /// <summary>The run time under <paramref name="x"/> (plot coordinates), within the run.</summary>
    public double TimeAt(double x) => Math.Clamp(x / Math.Max(_plot.Scale, 1e-9), 0, _plot.Duration);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _plot.Host is not { } host || _plot.IsLive)
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        host.ScrubTo(TimeAt(e.GetPosition(this).X), AgentTimelinePlaybackAction.Seek, announce: false);
        _plot.UpdateKnob();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging && _plot.Host is { } host)
        {
            host.ScrubTo(TimeAt(e.GetPosition(this).X), AgentTimelinePlaybackAction.Seek, announce: false);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            EndDrag();
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        _dragging = false;
        _plot.UpdateKnob();
        _plot.Host?.AnnouncePosition();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _plot.Host is not { } host || _plot.IsLive)
        {
            return;
        }
        var now = host.Now;
        var big = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        double? target = e.Key switch
        {
            Key.Left or Key.Down => now - (big ? 5 : 0.5),
            Key.Right or Key.Up => now + (big ? 5 : 0.5),
            Key.PageDown => now - 5,
            Key.PageUp => now + 5,
            Key.Home => 0,
            Key.End => _plot.Duration,
            _ => null
        };
        if (target is { } t)
        {
            host.ScrubTo(t, AgentTimelinePlaybackAction.Seek, announce: true);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ScrubberPeer(this);

    private sealed class ScrubberPeer : ControlAutomationPeer, IRangeValueProvider
    {
        public ScrubberPeer(AgentTimelineScrubber owner)
            : base(owner)
        {
        }

        private AgentTimelineScrubber Scrubber => (AgentTimelineScrubber)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public bool IsReadOnly => Scrubber.Plot.IsLive;

        public double Minimum => 0;

        public double Maximum => Scrubber.Plot.Duration;

        public double Value => Scrubber.Plot.Host?.Now ?? 0;

        public double LargeChange => 5;

        public double SmallChange => 0.5;

        public void SetValue(double value) => Scrubber.Plot.Host?.ScrubTo(value, AgentTimelinePlaybackAction.Seek, announce: true);
    }
}
