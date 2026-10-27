using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The response-time chart (notes 3.4 and 4): a 216 high plot with the gridlines of nice ticks from zero (x 44 to W − 36,
/// y 12 to 186), the band between the p95 and p50 curves (Accent 7%), the p95 curve (2px Accent) and the p50 curve (1.5px
/// TextMuted), both monotone cubic, a dashed target line at the service's p95 target and the two series names at the right
/// end. Time labels sit under every twelfth sample from the sixth. Pointing (or ← → on the focused chart) snaps a crosshair
/// to the nearest half-hour sample with a dot on each curve. Switching service morphs the curves, the band, the target line and
/// the domain on Smooth springs; the tick labels cross-fade in place of the old ones.
/// </summary>
/// <remarks>
/// Why code-driven: the 2 × 48 values, the target and the domain are coordinates inside one geometry that move in
/// lockstep and keep their velocity when another service is chosen mid-flight.
/// </remarks>
internal sealed class UptimeLatencyChart : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly AnalyticsValueAxis _axis = new();
    private readonly AnalyticsSpringArray _p95;
    private readonly AnalyticsSpringArray _p50;
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel _target = new(0, 1e-6, 1e-4);
    private Point[] _upper = [];
    private Point[] _lower = [];
    private StreamGeometry? _upperLine;
    private StreamGeometry? _lowerLine;
    private StreamGeometry? _band;
    private Size _geometrySize;
    private bool _dirty = true;
    private bool _hasData;
    private LatencySample[] _samples = [];
    private int _cursor = -1;
    private bool _pointer;
    private string _timeFormat = "HH:mm";
    private Func<double, string> _tickFormat = v => AnalyticsFormat.Number(v);

    public UptimeLatencyChart()
    {
        Focusable = true;
        IsTabStop = true;
        ClipToBounds = false;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Cross);
        _p95 = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth);
        _p50 = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth);
        _p95.Changed += MarkDirty;
        _p50.Changed += MarkDirty;
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>Raised when the cursor moves: sample index (-1 none) and whether the keyboard moved it.</summary>
    public event Action<int, bool>? CursorMoved;

    /// <summary>The series name at the p95 curve's end.</summary>
    public string P95Name { get; set; } = "p95";

    /// <summary>The series name at the p50 curve's end.</summary>
    public string P50Name { get; set; } = "p50";

    /// <summary>The cursor's sample (-1 none).</summary>
    public int CursorIndex => _cursor;

    /// <summary>The axis (tests).</summary>
    internal AnalyticsValueAxis Axis => _axis;

    /// <summary>The p95 values as drawn now (tests).</summary>
    internal AnalyticsSpringArray P95Rows => _p95;

    internal AnalyticsSpringArray P50Rows => _p50;

    internal double TargetNow => _target.Value;

    internal double TargetGoal => _target.Target;

    internal IReadOnlyList<LatencySample> Samples => _samples;

    /// <summary>Sets how time labels are written (a .NET time pattern; default "HH:mm").</summary>
    public void SetTimeFormat(string format)
    {
        _timeFormat = string.IsNullOrEmpty(format) ? "HH:mm" : format;
        InvalidateVisual();
    }

    /// <summary>Sets how tick values are written (default grouped integer).</summary>
    public void SetTickFormat(Func<double, string>? format)
    {
        _tickFormat = format ?? (v => AnalyticsFormat.Number(v));
        InvalidateVisual();
    }

    /// <summary>
    /// Shows a service's samples and target. The first call (and <paramref name="animate"/> false) places everything at
    /// once; otherwise curves, band, target and domain spring to the new values.
    /// </summary>
    public void SetData(IReadOnlyList<LatencySample> samples, double target, double headroom, bool animate)
    {
        animate &= _hasData && this.IsAttachedToVisualTree();
        var level = ArcMotion.GetEffectiveLevel(this);
        _samples = samples.ToArray();
        var n = _samples.Length;
        var p95 = new double[n];
        var p50 = new double[n];
        var max = 0.0;
        for (var i = 0; i < n; i++)
        {
            p95[i] = _samples[i].P95;
            p50[i] = _samples[i].P50;
            max = Math.Max(max, p95[i]);
        }
        if (_upper.Length != n)
        {
            _upper = new Point[n];
            _lower = new Point[n];
        }
        if (animate && level == ArcMotionLevel.Full)
        {
            _p95.SetTargets(p95);
            _p50.SetTargets(p50);
            _target.SpringTo(target, AnalyticsSprings.Smooth);
        }
        else
        {
            _p95.Jump(p95);
            _p50.Jump(p50);
            _target.Jump(target);
        }
        _axis.Set(UptimeEngine.ChartTicks(target, max, headroom), level, animate);
        _hasData = true;
        if (_cursor >= n)
        {
            _cursor = n - 1;
        }
        MarkDirty();
        _loop.Start();
    }

    /// <summary>Hides the crosshair.</summary>
    public void ClearCursor()
    {
        _pointer = false;
        SetCursor(-1, false);
    }

    /// <summary>Puts the crosshair on a sample.</summary>
    public void Inspect(int index, bool byKeyboard) => SetCursor(index, byKeyboard);

    private void SetCursor(int index, bool byKeyboard)
    {
        index = _samples.Length == 0 ? -1 : Math.Clamp(index, -1, _samples.Length - 1);
        if (index == _cursor)
        {
            return;
        }
        // The crosshair and its dots jump (no animation).
        _cursor = index;
        InvalidateVisual();
        CursorMoved?.Invoke(index, byKeyboard);
    }

    private bool Advance(double dt)
    {
        var moving = _axis.Advance(dt);
        moving |= _target.Step(dt);
        MarkDirty();
        return moving || _p95.IsMoving || _p50.IsMoving;
    }

    private void MarkDirty()
    {
        _dirty = true;
        InvalidateVisual();
    }

    private void RefreshPalette()
    {
        _text.Clear();
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateVisual();
    }

    // Input --------------------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Type == PointerType.Touch && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pointer = true;
        SetCursor(UptimeEngine.NearestSample(e.GetPosition(this).X, _samples.Length, Bounds.Width), false);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointer)
        {
            _pointer = false;
            SetCursor(-1, false);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_pointer)
        {
            SetCursor(-1, false);
        }
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_cursor >= 0)
            {
                e.Handled = true;
                SetCursor(-1, true);
            }
            return;
        }
        // Focus does not inspect: → first shows the newest sample, ← the one before it. Page keys do nothing here.
        if (AnalyticsKeyboard.Step(e.Key, _cursor, _samples.Length, vertical: false) is { } next)
        {
            e.Handled = true;
            _pointer = false;
            SetCursor(next, true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            MarkDirty();
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, UptimeEngine.ChartHeight);

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    // Render -------------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (!_hasData || size.Width <= UptimeEngine.PlotLeft + UptimeEngine.PlotRightInset || _samples.Length == 0)
        {
            return;
        }
        var p = _palette;
        var w = size.Width;
        var n = _samples.Length;
        var scale = _axis.Scale(UptimeEngine.PlotBottom, UptimeEngine.PlotTop);
        if (_dirty || _geometrySize != size || _upperLine is null)
        {
            Build(scale, n, w);
            _geometrySize = size;
            _dirty = false;
        }
        var body = new Typeface(p.BodyFont);
        var right = w - UptimeEngine.PlotRightInset;

        // Gridlines and value labels (they cross-fade; kept ticks slide with the domain).
        for (var i = 0; i < _axis.TickCount; i++)
        {
            var tick = _axis[i];
            if (tick.Opacity <= 0.001)
            {
                continue;
            }
            var y = scale.Map(tick.Value);
            var line = Math.Round(y, MidpointRounding.AwayFromZero) + 0.5;
            context.DrawLine(p.Pen(p.BorderStrong, 1, tick.Opacity, cap: PenLineCap.Flat), new Point(UptimeEngine.PlotLeft, line), new Point(right, line));
            var label = _text.Get(_tickFormat(tick.Value), body, 12, true);
            label.Draw(context, _text.Brush(p.TextMuted, tick.Opacity), new Point(34 - label.Width, y - label.Height / 2 + 0.5));
        }

        // Time labels.
        var timeBrush = _text.Brush(p.TextMuted);
        foreach (var index in UptimeEngine.LabelIndices(n))
        {
            var label = _text.Get(_samples[index].Time.UtcDateTime.ToString(_timeFormat, AnalyticsFormat.DefaultCulture), body, 12, true);
            var x = UptimeEngine.ChartX(index, n, w);
            label.DrawAtBaseline(context, timeBrush, x - label.Width / 2, 208);
        }

        context.DrawGeometry(p.Brush(p.Accent, 0.07), null, _band!);
        var targetY = Math.Round(scale.Map(_target.Value), MidpointRounding.AwayFromZero) + 0.5;
        if (targetY >= UptimeEngine.PlotTop - 1 && targetY <= UptimeEngine.PlotBottom + 1)
        {
            context.DrawLine(p.Pen(p.TextMuted, 1, dash: 4, gap: 4, cap: PenLineCap.Flat), new Point(UptimeEngine.PlotLeft, targetY), new Point(right, targetY));
        }
        context.DrawGeometry(null, p.Pen(p.TextMuted, 1.5, join: PenLineJoin.Round), _lowerLine!);
        context.DrawGeometry(null, p.Pen(p.Accent, 2, join: PenLineJoin.Round), _upperLine!);

        // Series names at the right end; kept apart when the curves end close together.
        var y95 = _upper[n - 1].Y;
        var y50 = _lower[n - 1].Y;
        const double minGap = 14;
        if (Math.Abs(y95 - y50) < minGap)
        {
            var push = (minGap - Math.Abs(y95 - y50)) / 2;
            y95 -= push;
            y50 += push;
        }
        var secondary = _text.Brush(p.TextSecondary);
        var name95 = _text.Get(P95Name, body, 12);
        var name50 = _text.Get(P50Name, body, 12);
        name95.Draw(context, secondary, new Point(right + 8, y95 - name95.Height / 2 + 0.5));
        name50.Draw(context, secondary, new Point(right + 8, y50 - name50.Height / 2 + 0.5));

        if (_cursor >= 0 && _cursor < n)
        {
            var x = Math.Round(UptimeEngine.ChartX(_cursor, n, w), MidpointRounding.AwayFromZero) + 0.5;
            context.DrawLine(p.Pen(p.BorderStrong, 1, cap: PenLineCap.Flat), new Point(x, UptimeEngine.PlotTop), new Point(x, UptimeEngine.PlotBottom));
            var cx = UptimeEngine.ChartX(_cursor, n, w);
            var ring = p.Pen(p.Surface, 2);
            context.DrawEllipse(p.Brush(p.TextMuted), ring, new Point(cx, _lower[_cursor].Y), 3.5, 3.5);
            context.DrawEllipse(p.Brush(p.Accent), ring, new Point(cx, _upper[_cursor].Y), 4, 4);
        }
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, p, new Rect(size), 6);
        }
    }

    private void Build(AnalyticsLinearScale scale, int n, double w)
    {
        var a = _p95.Values;
        var b = _p50.Values;
        for (var i = 0; i < n; i++)
        {
            var x = UptimeEngine.ChartX(i, n, w);
            _upper[i] = new Point(x, scale.Map(i < a.Length ? a[i] : 0));
            _lower[i] = new Point(x, scale.Map(i < b.Length ? b[i] : 0));
        }
        _upperLine = AnalyticsGeometry.Line(_upper, AnalyticsCurve.Monotone);
        _lowerLine = AnalyticsGeometry.Line(_lower, AnalyticsCurve.Monotone);
        _band = AnalyticsGeometry.Band(_upper, _lower, AnalyticsCurve.Monotone);
    }

    private sealed class GroupPeer(UptimeLatencyChart owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}

/// <summary>
/// The samples as a table for screen readers (Arc's visually hidden table): a zero-size control whose peer is a table named
/// by its caption with one row per sample.
/// </summary>
internal sealed class UptimeDataTable : Control
{
    private string _caption = string.Empty;
    private string[] _rows = [];

    static UptimeDataTable()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<UptimeDataTable>(false);
        FocusableProperty.OverrideDefaultValue<UptimeDataTable>(false);
    }

    /// <summary>The caption.</summary>
    public string Caption => _caption;

    /// <summary>The row texts.</summary>
    public IReadOnlyList<string> Rows => _rows;

    /// <summary>Replaces the table.</summary>
    public void Update(string caption, string[] rows)
    {
        _caption = caption;
        _rows = rows;
        if (ControlAutomationPeer.FromElement(this) is UptimeDataTablePeer peer)
        {
            peer.Rebuild();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override AutomationPeer OnCreateAutomationPeer() => new UptimeDataTablePeer(this);
}

internal sealed class UptimeDataTablePeer(UptimeDataTable owner) : ControlAutomationPeer(owner)
{
    private List<AutomationPeer>? _rows;

    internal void Rebuild()
    {
        _rows = null;
        InvalidateChildren();
        RaisePropertyChangedEvent(Avalonia.Automation.AutomationElementIdentifiers.NameProperty, null, owner.Caption);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    protected override string? GetNameCore() => owner.Caption;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        _rows ??= owner.Rows.Select(r => (AutomationPeer)new UptimeRowPeer(this, r)).ToList();
        return _rows;
    }
}

/// <summary>A table row that is not backed by a control.</summary>
internal sealed class UptimeRowPeer(AutomationPeer parent, string name) : AutomationPeer
{
    protected override void BringIntoViewCore()
    {
    }

    protected override string? GetAcceleratorKeyCore() => null;

    protected override string? GetAccessKeyCore() => null;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;

    protected override string? GetAutomationIdCore() => null;

    protected override Rect GetBoundingRectangleCore() => default;

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];

    protected override string GetClassNameCore() => "row";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string? GetNameCore() => name;

    protected override AutomationPeer? GetParentCore() => parent;

    protected override bool HasKeyboardFocusCore() => false;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() => true;

    protected override bool IsKeyboardFocusableCore() => false;

    protected override void SetFocusCore()
    {
    }

    protected override bool ShowContextMenuCore() => false;

    protected override bool TrySetParent(AutomationPeer? parent) => false;
}
