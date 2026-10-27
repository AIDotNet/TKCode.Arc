using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the plot draws: the bar names, which steps decrease, the running levels and the axis ticks.</summary>
internal sealed class MrrPlotContent
{
    /// <summary>Full category names, one per bar.</summary>
    public string[] Names { get; init; } = [];

    /// <summary>Short category names for narrow bands (null uses <see cref="Names"/>).</summary>
    public string[] CompactNames { get; init; } = [];

    /// <summary>Whether bar i is a decrease (its label hangs under the bar). Totals are false.</summary>
    public bool[] Decrease { get; init; } = [];

    /// <summary>The running levels, one more than there are steps (bars - 1).</summary>
    public double[] Levels { get; init; } = [];

    /// <summary>The axis ticks for these levels.</summary>
    public AnalyticsTickSet Ticks { get; init; } = new(0, 1, 1, [0, 1]);
}

/// <summary>How the plot spells numbers: ticks, totals and steps (the block picks them for the unit).</summary>
internal sealed class MrrPlotFormats
{
    /// <summary>A tick label from its value and the tick step.</summary>
    public Func<double, double, string> Tick { get; init; } = static (v, _) => v.ToString("0");

    /// <summary>A total bar's label from its level and whether the bands are narrow.</summary>
    public Func<double, bool, string> Total { get; init; } = static (v, _) => v.ToString("0");

    /// <summary>A step's label from its signed amount and whether the bands are narrow.</summary>
    public Func<double, bool, string> Step { get; init; } = static (v, _) => v.ToString("0");
}

/// <summary>
/// The bridge chart of <see cref="MrrWaterfall"/>: value axis, floating bars, connectors, break marks, value labels,
/// category labels, hover plate and selection marker, drawn by one control. It owns the pointer, the keyboard and the
/// motion; the block owns the data and the selection.
/// </summary>
/// <remarks>
/// Code-driven motion: the running levels ride one spring array and the domain rides the shared value axis, so every
/// bar end, connector, label position and label number is recomputed from the same moving values each frame. A ControlTheme
/// transition animates properties of elements, not coordinates inside one drawing. The 160ms fades and the marker slide
/// are tweens and a spring on the same loop.
/// </remarks>
internal sealed class MrrWaterfallPlot : Control
{
    private const double Dimmed = 0.28;
    private const double LabelDimmed = 0.32;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly LiveLoop _loop;
    private readonly AnalyticsSpringArray _levels;
    private readonly AnalyticsValueAxis _axis = new();

    private MrrPlotContent _content = new();
    private MrrPlotFormats _formats = new();
    private AnalyticsSpringChannel _marker = new(0, 0.01, 0.1);
    private AnalyticsTween[] _dim = [];
    private AnalyticsTween[] _current = [];
    private string[] _labelCache = [];
    private double[] _labelValue = [];
    private int _selected;
    private int _hover = -1;
    private int _paletteVersion = -1;
    private StreamGeometry? _breakGeometry;
    private double _breakWidth = -1;
    private bool _hasData;
    private bool _pointerIsMouse;
    private double _lastWidth = -1;

    static MrrWaterfallPlot()
    {
        FocusableProperty.OverrideDefaultValue<MrrWaterfallPlot>(true);
    }

    public MrrWaterfallPlot()
    {
        _levels = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth);
        _levels.Changed += InvalidateVisual;
        // Function, not decoration: the loop also finishes fades under reduced motion.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>Raised when a bar is chosen by pointer or keys.</summary>
    public event Action<int, bool>? SelectionRequested;

    /// <summary>Raised when the mouse enters a band (or leaves, -1).</summary>
    public event Action<int>? HoverRequested;

    /// <summary>Raised when the plot's width changes in a way that changes the chart (height follows the width).</summary>
    public event Action<bool>? CompactChanged;

    /// <summary>The number of bars.</summary>
    public int BarCount => _content.Names.Length;

    /// <summary>The selected bar.</summary>
    public int SelectedIndex => _selected;

    /// <summary>The bar under the mouse, or -1.</summary>
    public int HoverIndex => _hover;

    /// <summary>The bar the plate and dimming follow: the hovered one, else the selected one while the keyboard focus ring shows.</summary>
    public int ActiveIndex => _hover >= 0 ? _hover : AnalyticsFocus.IsVisible(this) ? _selected : -1;

    /// <summary>True while a spring, the axis or a fade moves.</summary>
    public bool IsMoving => _loop.IsActive || _levels.IsMoving;

    /// <summary>The axis (tests).</summary>
    internal AnalyticsValueAxis Axis => _axis;

    /// <summary>The current, animated level i (tests).</summary>
    internal double LevelAt(int i) => _levels[i];

    /// <summary>The marker's current x (tests).</summary>
    internal double MarkerX => _marker.Value;

    /// <summary>A bar's current opacity (tests).</summary>
    internal double BarOpacity(int i) => i >= 0 && i < _dim.Length ? 1 - (1 - Dimmed) * _dim[i].Value : 1;

    /// <summary>Whether the bands are narrow enough for short names and symbol-less labels.</summary>
    public bool IsCompactBand => MrrWaterfallEngine.IsCompactBand(Band);

    private double Band => MrrWaterfallEngine.Band(Bounds.Width, Math.Max(1, BarCount));

    /// <summary>The current pixel box of a bar (tests and hit testing).</summary>
    internal MrrBarBox BoxOf(int index)
    {
        var h = Bounds.Height > 0 ? Bounds.Height : MrrWaterfallEngine.ChartHeight(Bounds.Width);
        var bottom = MrrWaterfallEngine.PlotBottom(h);
        var scale = _axis.Scale(bottom, MrrWaterfallEngine.PlotTop);
        var band = Band;
        return MrrWaterfallEngine.Bar(index, BarCount, _levels.Values, scale, bottom, MrrWaterfallEngine.PlotLeft + index * band, band);
    }

    #region Data

    /// <summary>
    /// Shows new content. A change of the bar count, or <paramref name="animate"/> false, lands at once; otherwise the levels
    /// spring to their targets and the axis slides its domain and cross-fades its ticks.
    /// </summary>
    public void SetContent(MrrPlotContent content, MrrPlotFormats formats, bool animate)
    {
        var sameShape = _hasData && content.Names.Length == _content.Names.Length && content.Levels.Length == _content.Levels.Length;
        _content = content;
        _formats = formats;
        var count = content.Names.Length;
        if (_dim.Length != count)
        {
            _dim = new AnalyticsTween[count];
            _current = new AnalyticsTween[count];
            _labelCache = new string[count];
            _labelValue = new double[count];
            Array.Fill(_labelCache, string.Empty);
            for (var i = 0; i < count; i++)
            {
                _dim[i] = new AnalyticsTween(0);
                _current[i] = new AnalyticsTween(0);
            }
            _selected = Math.Clamp(_selected, 0, Math.Max(0, count - 1));
            if (count > 0)
            {
                _current[_selected].Jump(1);
            }
            _marker.Jump(MarkerTarget());
        }
        var span = content.Ticks.Max - content.Ticks.Min;
        _levels.RestDelta = Math.Max(1e-6, span / 236 / 100);
        _levels.RestSpeed = _levels.RestDelta * 10;
        var level = ArcMotion.GetEffectiveLevel(this);
        var animated = animate && sameShape && this.IsAttachedToVisualTree();
        if (animated)
        {
            _levels.SetTargets(content.Levels);
        }
        else
        {
            _levels.Jump(content.Levels);
        }
        _axis.Set(content.Ticks, level, animated);
        _hasData = true;
        Array.Fill(_labelCache, string.Empty);
        StartLoopIfNeeded();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Changes the number formats (the unit's text); the labels are recomputed on the next draw.</summary>
    public void SetFormats(MrrPlotFormats formats)
    {
        _formats = formats;
        Array.Fill(_labelCache, string.Empty);
        InvalidateVisual();
    }

    /// <summary>Selects a bar: the marker slides (spring), the category label turns strong (160ms).</summary>
    public void SetSelected(int index, bool animate)
    {
        index = Math.Clamp(index, 0, Math.Max(0, BarCount - 1));
        if (index == _selected && !animate)
        {
            return;
        }
        _selected = index;
        var full = animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        UpdateCurrent(full);
        if (full)
        {
            _marker.SpringTo(MarkerTarget(), AnalyticsSprings.MarkerSnap);
        }
        else
        {
            _marker.Jump(MarkerTarget());
        }
        UpdateDims();
        StartLoopIfNeeded();
        InvalidateVisual();
    }

    /// <summary>Sets the hovered bar (-1 for none): plate, dimming and the strong category label.</summary>
    public void SetHover(int index)
    {
        if (index == _hover)
        {
            return;
        }
        _hover = index;
        UpdateCurrent(ArcMotion.IsFull(this) && this.IsAttachedToVisualTree());
        UpdateDims();
        StartLoopIfNeeded();
        InvalidateVisual();
    }

    // The category label that is strong: the hovered bar's, else the selected one's.
    private void UpdateCurrent(bool animate)
    {
        var current = _hover >= 0 ? _hover : _selected;
        for (var i = 0; i < _current.Length; i++)
        {
            Fade(ref _current[i], i == current ? 1 : 0, animate);
        }
    }

    private double MarkerTarget() => MrrWaterfallEngine.BarLeft(MrrWaterfallEngine.PlotLeft + _selected * Band, Band);

    private static void Fade(ref AnalyticsTween tween, double target, bool animate)
    {
        if (animate)
        {
            tween.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
        }
        else
        {
            tween.Jump(target);
        }
    }

    private void UpdateDims()
    {
        var active = ActiveIndex;
        var full = ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        for (var i = 0; i < _dim.Length; i++)
        {
            Fade(ref _dim[i], active >= 0 && i != active ? 1 : 0, full);
        }
    }

    private void StartLoopIfNeeded()
    {
        if (_axis.IsMoving || _marker.IsMoving || AnyRunning())
        {
            _loop.Start();
        }
    }

    private bool AnyRunning()
    {
        for (var i = 0; i < _dim.Length; i++)
        {
            if (_dim[i].IsRunning || _current[i].IsRunning)
            {
                return true;
            }
        }
        return false;
    }

    private bool Advance(double dt)
    {
        var moving = _axis.Advance(dt);
        moving |= _marker.Step(dt);
        for (var i = 0; i < _dim.Length; i++)
        {
            moving |= _dim[i].Advance(dt);
            moving |= _current[i].Advance(dt);
        }
        InvalidateVisual();
        return moving;
    }

    /// <summary>Advances the plot's own motion by hand (tests).</summary>
    internal bool Step(double dt) => Advance(dt);

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    #endregion

    #region Layout and input

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 520 : availableSize.Width;
        return new Size(width, MrrWaterfallEngine.ChartHeight(width));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var compact = MrrWaterfallEngine.IsCompactBand(MrrWaterfallEngine.Band(finalSize.Width, Math.Max(1, BarCount)));
        CompactChanged?.Invoke(compact);
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Math.Abs(e.NewSize.Width - _lastWidth) > 0.01)
        {
            _lastWidth = e.NewSize.Width;
            _marker.Jump(MarkerTarget());
            Array.Fill(_labelCache, string.Empty);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerIsMouse = e.Pointer.Type == PointerType.Mouse;
        if (!_pointerIsMouse || BarCount == 0)
        {
            return;
        }
        var index = MrrWaterfallEngine.IndexAt(e.GetPosition(this).X, Bounds.Width, BarCount);
        if (index != _hover)
        {
            HoverRequested?.Invoke(index);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            HoverRequested?.Invoke(-1);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (BarCount == 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var index = MrrWaterfallEngine.IndexAt(e.GetPosition(this).X, Bounds.Width, BarCount);
        if (e.Pointer.Type == PointerType.Mouse && index != _hover)
        {
            HoverRequested?.Invoke(index);
        }
        SelectionRequested?.Invoke(index, false);
        e.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        FocusStateChanged();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        FocusStateChanged();
    }

    private void FocusStateChanged()
    {
        // The keyboard ring shows the plate and the dimming too (measured); the ring state arrives with the focus event.
        UpdateDims();
        StartLoopIfNeeded();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || BarCount == 0)
        {
            return;
        }
        if (MrrWaterfallEngine.Navigate(e.Key, _selected, BarCount) is { } next)
        {
            if (next != _selected)
            {
                SelectionRequested?.Invoke(next, true);
            }
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MrrWaterfallPlotPeer(this);

    #endregion

    #region Drawing

    private void EnsurePalette()
    {
        if (!_palette.IsReady || _paletteVersion != _palette.Version || _paletteVersion < 0)
        {
            if (this.IsAttachedToVisualTree())
            {
                _palette.Refresh(this);
                _paletteVersion = _palette.Version;
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        EnsurePalette();
        var count = BarCount;
        if (!_palette.IsReady || count == 0 || _levels.Count != _content.Levels.Length)
        {
            return;
        }
        var body = new Typeface(_palette.BodyFont);
        var medium = new Typeface(_palette.BodyFont, FontStyle.Normal, _palette.Medium);
        var plotBottom = MrrWaterfallEngine.PlotBottom(size.Height);
        var scale = _axis.Scale(plotBottom, MrrWaterfallEngine.PlotTop);
        var band = MrrWaterfallEngine.Band(size.Width, count);
        var compact = MrrWaterfallEngine.IsCompactBand(band);
        var levels = _levels.Values;
        var active = ActiveIndex;

        DrawGrid(context, size, body, scale, plotBottom);
        if (active >= 0 && active < count)
        {
            var left = MrrWaterfallEngine.PlotLeft + active * band;
            context.DrawRectangle(_palette.Brush(_palette.SurfaceMuted), null, new RoundedRect(new Rect(left + 3, 2, Math.Max(0, band - 6), size.Height - 4), 12));
        }

        DrawBars(context, count, levels, scale, plotBottom, band, size);
        DrawBaseline(context, size, plotBottom);
        DrawMarker(context, band, plotBottom);
        DrawLabels(context, count, levels, scale, plotBottom, band, compact, medium);
        DrawCategories(context, count, band, size, body, compact);

        if (AnalyticsFocus.IsVisible(this) && _selected < count)
        {
            AnalyticsFocus.Draw(context, _palette, new Rect(MrrWaterfallEngine.PlotLeft + _selected * band, 0, band, size.Height));
        }
    }

    private void DrawGrid(DrawingContext context, Size size, Typeface body, AnalyticsLinearScale scale, double plotBottom)
    {
        var top = MrrWaterfallEngine.PlotTop;
        for (var i = 0; i < _axis.TickCount; i++)
        {
            var tick = _axis[i];
            var y = scale.Map(tick.Value);
            if (y < top - 0.5 || y > plotBottom + 0.5 || tick.Opacity <= 0.003)
            {
                continue;
            }
            var line = AnalyticsPixels.Crisp(y);
            context.DrawLine(_palette.Pen(_palette.Border, 1, tick.Opacity, cap: PenLineCap.Flat),
                new Point(MrrWaterfallEngine.PlotLeft, line), new Point(size.Width, line));
            var label = _text.Get(_formats.Tick(tick.Value, _content.Ticks.Step), body, 11, true);
            label.DrawAtBaseline(context, _palette.Brush(_palette.TextMuted, tick.Opacity), 38 - label.Width, y + 0.32 * 11);
        }
    }

    private void DrawBaseline(DrawingContext context, Size size, double plotBottom)
    {
        var y = plotBottom - 0.5;
        context.DrawLine(_palette.Pen(_palette.BorderStrong, 1, cap: PenLineCap.Flat), new Point(MrrWaterfallEngine.PlotLeft, y), new Point(size.Width, y));
    }

    private void DrawBars(DrawingContext context, int count, ReadOnlySpan<double> levels, AnalyticsLinearScale scale, double plotBottom, double band, Size size)
    {
        var showBreak = _axis.DomainMin > 0;
        for (var i = 0; i < count; i++)
        {
            var box = MrrWaterfallEngine.Bar(i, count, levels, scale, plotBottom, MrrWaterfallEngine.PlotLeft + i * band, band);
            var total = i == 0 || i == count - 1;
            var color = total ? _palette.Accent : _content.Decrease[i] ? _palette.Danger : _palette.Success;
            var alpha = 1 - (1 - Dimmed) * _dim[i].Value;
            if (box.Height > 0)
            {
                var rect = new Rect(box.X, box.Top, box.Width, box.Height);
                var r = Math.Min(total ? 3 : 2, box.Height / 2);
                var rounded = total ? new RoundedRect(rect, r, r, 0, 0) : new RoundedRect(rect, r);
                context.DrawRectangle(_palette.Brush(color, alpha), null, rounded);
            }
            if (total && showBreak && box.Height > 16)
            {
                EnsureBreak(box.Width);
                using (context.PushTransform(Matrix.CreateTranslation(box.X, plotBottom - 14)))
                {
                    context.DrawGeometry(_palette.Brush(_palette.Surface), null, _breakGeometry!);
                }
            }
            if (i < count - 1)
            {
                var y = MrrWaterfallEngine.ConnectorY(i, levels, scale);
                var next = MrrWaterfallEngine.BarLeft(MrrWaterfallEngine.PlotLeft + (i + 1) * band, band);
                context.DrawLine(_palette.Pen(_palette.BorderStrong, 1, cap: PenLineCap.Flat), new Point(box.X + box.Width, y), new Point(next, y));
            }
        }
    }

    private void EnsureBreak(double width)
    {
        if (_breakGeometry is not null && Math.Abs(_breakWidth - width) < 0.01)
        {
            return;
        }
        var points = MrrWaterfallEngine.BreakMark(width);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), true);
            for (var i = 1; i < points.Length; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y));
            }
            ctx.EndFigure(true);
        }
        _breakGeometry = geometry;
        _breakWidth = width;
    }

    private void DrawMarker(DrawingContext context, double band, double plotBottom)
    {
        var (y, h) = MrrWaterfallEngine.Marker(plotBottom);
        var width = MrrWaterfallEngine.BarWidth(band);
        context.DrawRectangle(_palette.Brush(_palette.Accent), null, new RoundedRect(new Rect(_marker.Value, y, width, h), 1));
    }

    private void DrawLabels(DrawingContext context, int count, ReadOnlySpan<double> levels, AnalyticsLinearScale scale, double plotBottom,
        double band, bool compact, Typeface medium)
    {
        for (var i = 0; i < count; i++)
        {
            var box = MrrWaterfallEngine.Bar(i, count, levels, scale, plotBottom, MrrWaterfallEngine.PlotLeft + i * band, band);
            var text = LabelFor(i, levels, compact);
            var below = _content.Decrease[i];
            var anchor = MrrWaterfallEngine.LabelAnchor(box, below);
            var layout = _text.Get(text, medium, 12, true);
            var cx = Math.Round(MrrWaterfallEngine.PlotLeft + i * band + band / 2, MidpointRounding.AwayFromZero);
            var top = below ? anchor + (16 - layout.Height) / 2 : anchor - 16 + (16 - layout.Height) / 2;
            var alpha = 1 - (1 - LabelDimmed) * _dim[i].Value;
            layout.Draw(context, _palette.Brush(_palette.Foreground, alpha), new Point(cx - layout.Width / 2, top));
        }
    }

    /// <summary>The label of bar <paramref name="i"/> for the current (animated) levels; cached while the value does not change.</summary>
    private string LabelFor(int i, ReadOnlySpan<double> levels, bool compact)
    {
        var count = BarCount;
        var total = i == 0 || i == count - 1;
        double value;
        if (total)
        {
            value = i == 0 ? levels[0] : levels[^1];
        }
        else
        {
            var delta = Math.Abs(levels[i] - levels[i - 1]);
            value = _content.Decrease[i] ? -delta : delta;
        }
        var text = _labelCache[i];
        if (text.Length == 0 || Math.Abs(_labelValue[i] - value) > 1e-9)
        {
            text = total ? _formats.Total(value, compact) : _formats.Step(value, compact);
            _labelCache[i] = text;
            _labelValue[i] = value;
        }
        return text;
    }

    /// <summary>The label text of a bar now (tests).</summary>
    internal string CurrentLabel(int i) => i >= 0 && i < BarCount && _levels.Count == _content.Levels.Length ? LabelFor(i, _levels.Values, IsCompactBand) : string.Empty;

    private void DrawCategories(DrawingContext context, int count, double band, Size size, Typeface body, bool compact)
    {
        var fontSize = MrrWaterfallEngine.IsTinyBand(band) ? 10 : 12;
        for (var i = 0; i < count; i++)
        {
            var name = compact && i < _content.CompactNames.Length && _content.CompactNames[i].Length > 0 ? _content.CompactNames[i] : _content.Names[i];
            var layout = _text.Get(name, body, fontSize);
            var t = Math.Clamp(_current[i].Value, 0, 1);
            var color = t <= 0.001 ? _palette.TextMuted : t >= 0.999 ? _palette.Foreground : AnalyticsColors.Lerp(_palette.TextMuted, _palette.Foreground, t);
            var cx = MrrWaterfallEngine.PlotLeft + i * band + band / 2;
            layout.DrawAtBaseline(context, _palette.Brush(color), cx - layout.Width / 2, size.Height - 10);
        }
    }

    #endregion
}

/// <summary>The plot's peer: a focusable image group named by the block's summary of the bridge.</summary>
internal sealed class MrrWaterfallPlotPeer(MrrWaterfallPlot owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "chart";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
