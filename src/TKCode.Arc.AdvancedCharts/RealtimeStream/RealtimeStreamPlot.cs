using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// The one drawing surface of a <see cref="RealtimeStream"/>. The line and its area are geometries in a coordinate system
/// fixed to the readings and built when a reading arrives or the axis moves; a scrolling frame only translates them and
/// lets the clip cut the line at the right edge, which is what interpolates the leading edge. Pens, brushes and laid-out
/// text are cached, so a scrolling frame allocates nothing of its own.
/// </summary>
internal sealed class RealtimeStreamPlot : Control, IChartsBAccessible, ICustomHitTest
{
    private const double TopInset = 8;
    private const double RightInset = 8;
    private const double NarrowWidth = 420;
    private const double DotRadius = 3.5;

    private static readonly double[] s_timeSteps =
        [0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 14400, 21600, 43200, 86400];

    private readonly ChartsBPalette _palette;
    private readonly ChartsBTooltip _tooltip;
    private readonly LiveLoop _loop;
    private readonly Dictionary<long, TextLayout> _timeLabels = new();
    private readonly List<long> _timeLabelScratch = new();
    private readonly List<(double Value, TextLayout Text)> _valueTicks = new();
    private RealtimeStream? _owner;
    private ChartsBAutomationPeer? _peer;

    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private long _geometryBase;
    private double _geometryScale;
    private bool _geometryDirty = true;
    private Size _geometrySize;

    private int _colorVersion = -1;
    private bool _stylesStale = true;
    private IPen? _linePen;
    private IPen? _gridPen;
    private IPen? _axisPen;
    private IPen? _ringPen;
    private IPen? _cursorPen;
    private IPen? _focusPen;
    private IPen? _chipPen;
    private IBrush? _lineBrush;
    private IBrush? _areaBrush;
    private IBrush? _markerBrush;
    private IBrush? _chipBrush;
    private IBrush? _surfaceBrush;
    private StreamGeometry? _diamond;
    private TextLayout? _empty;
    private double _timeStepSeconds = 5;
    private double _timeStepWidth = -1;
    private double _gutter;
    private bool _keyboard;
    private int _tipStamp = -1;
    private long _tipSequence = -2;
    private int _textStamp;

    public RealtimeStreamPlot()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        FocusAdorner = null;
        ClipToBounds = false;
        _palette = new ChartsBPalette(this, InvalidateVisual);
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, InvalidateVisual) { MinWidth = 120 };
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += OnRunningChanged;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    internal RealtimeStream? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            ResetText();
            _geometryDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    bool ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>How many times the line was built (tests: scrolling must not build it).</summary>
    internal int GeometryBuilds { get; private set; }

    /// <summary>Whether frames are being delivered (tests).</summary>
    internal bool IsLoopRunning => _loop.IsRunning;

    /// <summary>The seconds between time labels at the current width.</summary>
    internal double TimeStepSeconds => _timeStepSeconds;

    /// <summary>Picks the shortest round step whose labels keep their distance at this width.</summary>
    internal static double ChooseTimeStep(double windowSeconds, double plotWidth, double labelWidth, double gap)
    {
        var perSecond = plotWidth / Math.Max(0.001, windowSeconds);
        foreach (var step in s_timeSteps)
        {
            if (step * perSecond >= labelWidth + gap)
            {
                return step;
            }
        }
        return s_timeSteps[^1];
    }

    // ---- loop ----------------------------------------------------------------------------------------------------

    /// <summary>Asks for frames; the loop itself waits until the plot is attached, in view and at full motion.</summary>
    internal void Wake()
    {
        _loop.Start();
        InvalidateVisual();
    }

    private bool Step(double dt) => _owner is { } owner && owner.AdvanceFrame(dt);

    private void OnRunningChanged(bool running)
    {
        if (running)
        {
            _owner?.OnLoopStarted();
        }
        else
        {
            _owner?.OnLoopGated();
        }
        InvalidateVisual();
    }

    // ---- invalidation --------------------------------------------------------------------------------------------

    internal void MarkGeometryDirty()
    {
        _geometryDirty = true;
        InvalidateVisual();
    }

    internal void OnAxisTargetChanged()
    {
        RebuildValueTicks();
        _geometryDirty = true;
        InvalidateVisual();
    }

    internal void OnScaleChanged()
    {
        _timeStepWidth = -1;
        _geometryDirty = true;
        InvalidateVisual();
    }

    internal void OnTextChanged()
    {
        ResetText();
        SyncReading();
        InvalidateVisual();
    }

    private void ResetText()
    {
        _stylesStale = true;
        _textStamp++;
        _timeStepWidth = -1;
        _gutter = 0;
        foreach (var layout in _timeLabels.Values)
        {
            layout.Dispose();
        }
        _timeLabels.Clear();
        foreach (var tick in _valueTicks)
        {
            tick.Text.Dispose();
        }
        _valueTicks.Clear();
        _empty?.Dispose();
        _empty = null;
        if (_owner is { } owner)
        {
            foreach (var marker in owner.Markers)
            {
                marker.Layout?.Dispose();
                marker.Layout = null;
            }
        }
        _geometryDirty = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            _timeStepWidth = -1;
            var oldWidth = change.GetOldValue<Rect>().Width;
            if (Math.Abs(oldWidth - Bounds.Width) > 0.5)
            {
                _gutter = 0;
                RebuildValueTicks();
            }
            _geometryDirty = true;
        }
    }

    // ---- geometry helpers ----------------------------------------------------------------------------------------

    private bool Narrow => Bounds.Width > 0 && Bounds.Width < NarrowWidth;

    private Rect PlotRect
    {
        get
        {
            var owner = _owner;
            if (owner is null)
            {
                return default;
            }
            var left = Math.Max(_gutter, Narrow ? owner.NarrowGutter : owner.Gutter);
            return new Rect(left, TopInset, Math.Max(1, Bounds.Width - left - RightInset), Math.Max(1, owner.PlotHeight - TopInset));
        }
    }

    private double XOf(long ticks, in Rect rect, RealtimeStream owner)
        => rect.Right - (double)(owner.HeadTicks - ticks) / owner.WindowTicks * rect.Width;

    private static double YOf(double value, in Rect rect, RealtimeStream owner)
    {
        var span = owner.AxisHigh - owner.AxisLow;
        return rect.Bottom - (span == 0 ? 0 : (value - owner.AxisLow) / span) * rect.Height;
    }

    /// <summary>The moment under <paramref name="x"/>.</summary>
    internal long TicksAtX(double x)
    {
        if (_owner is not { } owner)
        {
            return 0;
        }
        var rect = PlotRect;
        return owner.HeadTicks - (long)((rect.Right - x) / rect.Width * owner.WindowTicks);
    }

    // ---- pointer -------------------------------------------------------------------------------------------------

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner is not { } owner)
        {
            return;
        }
        var touch = e.Pointer.Type == PointerType.Touch;
        if (touch && e.Pointer.Captured != this)
        {
            return;
        }
        owner.PointerAt(e.GetPosition(this).X, touch);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner is not { } owner)
        {
            return;
        }
        var touch = e.Pointer.Type == PointerType.Touch;
        if (touch)
        {
            e.Pointer.Capture(this);
        }
        owner.PointerAt(e.GetPosition(this).X, touch);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            e.Pointer.Capture(null);
            _owner?.PointerGone(true);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _owner?.PointerGone(true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _owner?.PointerGone(false);
        }
    }

    // ---- keyboard ------------------------------------------------------------------------------------------------

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        if (_keyboard)
        {
            _owner?.KeyboardEntered();
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboard = false;
        _owner?.KeyboardLeft();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _owner is { } owner && owner.HandleKey(e.Key))
        {
            e.Handled = true;
        }
    }

    // ---- accessibility -------------------------------------------------------------------------------------------

    protected override AutomationPeer OnCreateAutomationPeer() => _peer = new ChartsBAutomationPeer(this, this);

    string IChartsBAccessible.ChartRoleDescription => "realtime line chart";

    string IChartsBAccessible.ChartName
    {
        get
        {
            var label = _owner?.Label;
            var name = string.IsNullOrEmpty(label) ? "Realtime stream" : label;
            return $"{name}. Left and right arrows read the points in view, space pauses or resumes, escape returns to live.";
        }
    }

    string IChartsBAccessible.ChartHelp => _owner is { } owner ? owner.Spoken ?? owner.Summary : string.Empty;

    internal void Announce() => _peer?.Announce();

    // ---- reading -------------------------------------------------------------------------------------------------

    /// <summary>Brings the tooltip and the marks in line with the reading.</summary>
    internal void SyncReading()
    {
        InvalidateVisual();
        if (_owner is not { } owner)
        {
            return;
        }
        var index = owner.CursorIndex;
        if (index < 0 || Bounds.Width <= 0)
        {
            _tooltip.Hide();
            _tipSequence = -2;
            return;
        }
        var sequence = owner.Buffer.Dropped + index;
        var p = _palette;
        var xs = p.TypeXS;
        var buffer = owner.Buffer;
        if (sequence != _tipSequence || _tipStamp != _textStamp)
        {
            _tipSequence = sequence;
            _tipStamp = _textStamp;
            var rows = new List<ChartsBTipRow>(3)
            {
                new() { Left = ChartsBText.Make(owner.FormatTime(buffer.TicksAt(index)), p, xs, p.TextSecondary), MinHeight = xs * 1.5 },
                new()
                {
                    MarginTop = 2, MinHeight = p.TypeSM * 1.4,
                    Left = ChartsBText.Make(string.IsNullOrEmpty(owner.Label) ? "Value" : owner.Label, p, xs, p.TextMuted),
                    Right = ChartsBText.Make(owner.Format(buffer.ValueAt(index)), p, p.TypeSM, p.Foreground, FontWeight.Medium)
                }
            };
            if (buffer.TagAt(index) is { } tag)
            {
                rows.Add(new ChartsBTipRow
                {
                    MarginTop = 4, MinHeight = xs * 1.5, Swatch = p.Danger, Shape = ChartsBSwatchShape.Round,
                    Left = ChartsBText.Make(tag, p, xs, p.Foreground)
                });
            }
            _tooltip.SetRows(rows);
        }
        var rect = PlotRect;
        var x = XOf(buffer.TicksAt(index), rect, owner);
        var y = YOf(buffer.ValueAt(index), rect, owner);
        var size = _tooltip.Size;
        const double gap = 12;
        var left = x + gap;
        if (left + size.Width > Bounds.Width)
        {
            left = x - gap - size.Width;
        }
        var top = y - size.Height - gap;
        if (top < 0)
        {
            top = y + gap;
        }
        _tooltip.Show(new Point(
            ChartsBMath.Clamp(left, 0, Math.Max(0, Bounds.Width - size.Width)),
            ChartsBMath.Clamp(top, 0, Math.Max(0, Bounds.Height - size.Height))));
    }

    // ---- layout and drawing --------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        var owner = _owner;
        return new Size(width, (owner?.PlotHeight ?? 200) + (owner?.TimeAxisHeight ?? 26));
    }

    private void EnsureStyles()
    {
        var p = _palette;
        if (_owner is not { } owner || (!_stylesStale && _colorVersion == p.Version))
        {
            return;
        }
        if (_colorVersion != p.Version && !_stylesStale)
        {
            // Text is laid out in its color, and the font and sizes may have changed with the theme.
            ResetText();
        }
        _stylesStale = false;
        _colorVersion = p.Version;
        var series = p.Series(0);
        _lineBrush = p.Brush(series);
        _linePen = new ImmutablePen((IImmutableBrush)_lineBrush, owner.LineThickness, null, PenLineCap.Round, PenLineJoin.Round);
        _areaBrush = p.Brush(ChartsBMath.Alpha(series, 0.12));
        _gridPen = p.Pen(p.BorderSubtle, 1);
        _axisPen = p.Pen(p.Border, 1);
        _ringPen = p.Pen(p.Surface, 2);
        _cursorPen = p.Pen(ChartsBMath.Alpha(p.Foreground, 0.35), 1);
        _focusPen = p.Pen(p.FocusRing, 2);
        _chipPen = p.Pen(p.Border, 1);
        _markerBrush = p.Brush(p.Danger);
        _chipBrush = p.Brush(p.SurfaceRaised);
        _surfaceBrush = p.Brush(p.Surface);
        var radius = owner.MarkerRadius;
        var diamond = new StreamGeometry();
        using (var ctx = diamond.Open())
        {
            ctx.BeginFigure(new Point(0, -radius), true);
            ctx.LineTo(new Point(radius, 0));
            ctx.LineTo(new Point(0, radius));
            ctx.LineTo(new Point(-radius, 0));
            ctx.EndFigure(true);
        }
        _diamond = diamond;
        if (_valueTicks.Count == 0)
        {
            RebuildValueTicks();
        }
    }

    private TextLayout Layout(string text, double size, Color color, double maxWidth = double.PositiveInfinity)
    {
        var p = _palette;
        return new TextLayout(text, p.Body(), size, p.Brush(color), TextAlignment.Left, TextWrapping.NoWrap,
            double.IsInfinity(maxWidth) ? null : TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, maxWidth);
    }

    /// <summary>The value labels for the axis target. They change when the target does, not while the axis eases toward it.</summary>
    private void RebuildValueTicks()
    {
        foreach (var tick in _valueTicks)
        {
            tick.Text.Dispose();
        }
        _valueTicks.Clear();
        if (_owner is not { } owner || owner.IsEmpty)
        {
            return;
        }
        var low = owner.TargetLow;
        var high = owner.TargetHigh;
        var step = ChartsBMath.NiceStep(high - low, Narrow ? 3 : 4);
        var p = _palette;
        var widest = 0.0;
        var first = Math.Ceiling(low / step - 1e-9);
        for (var i = 0; i < 12; i++)
        {
            var value = (first + i) * step;
            if (value > high + step * 1e-6)
            {
                break;
            }
            var rounded = Math.Round(value * 1e6) / 1e6;
            var layout = Layout(owner.Format(rounded == 0 ? 0 : rounded), p.TypeXS, p.TextMuted);
            _valueTicks.Add((rounded, layout));
            widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
        }
        // The column only widens until the next resize, so the plot does not breathe as the labels change.
        var needed = Math.Min(Math.Ceiling(widest) + 8, Math.Max(32, Bounds.Width * 0.4));
        if (needed > _gutter)
        {
            _gutter = needed;
            _geometryDirty = true;
            _timeStepWidth = -1;
        }
    }

    /// <summary>The line and area, in pixels from the first reading they contain. A scrolling frame only moves them.</summary>
    private void EnsureGeometry(in Rect rect, RealtimeStream owner)
    {
        var scale = rect.Width / owner.WindowTicks;
        if (!_geometryDirty && _geometryScale == scale && _geometrySize == Bounds.Size && _line is not null)
        {
            return;
        }
        GeometryBuilds++;
        _geometryDirty = false;
        _geometryScale = scale;
        _geometrySize = Bounds.Size;
        var buffer = owner.Buffer;
        var first = Math.Max(0, buffer.LowerBound(owner.HeadTicks - owner.WindowTicks) - 1);
        var count = buffer.Count;
        _geometryBase = buffer.TicksAt(first);
        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var lineCtx = line.Open())
        using (var areaCtx = area.Open())
        {
            var start = new Point(0, YOf(buffer.ValueAt(first), rect, owner));
            lineCtx.BeginFigure(start, false);
            areaCtx.BeginFigure(new Point(0, rect.Bottom), true);
            areaCtx.LineTo(start);
            var lastX = 0.0;
            for (var i = first + 1; i < count; i++)
            {
                var point = new Point((buffer.TicksAt(i) - _geometryBase) * scale, YOf(buffer.ValueAt(i), rect, owner));
                lineCtx.LineTo(point);
                areaCtx.LineTo(point);
                lastX = point.X;
            }
            areaCtx.LineTo(new Point(lastX, rect.Bottom));
            lineCtx.EndFigure(false);
            areaCtx.EndFigure(true);
        }
        _line = line;
        _area = area;
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0 || _owner is not { } owner)
        {
            return;
        }
        EnsureStyles();
        var p = _palette;
        var rect = PlotRect;
        // The whole box takes the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var baseline = Math.Round(rect.Bottom) + 0.5;
        if (owner.IsEmpty)
        {
            _empty ??= Layout(owner.EmptyLabel, p.TypeSM, p.TextMuted);
            context.DrawLine(_axisPen!, new Point(rect.Left, baseline), new Point(rect.Right, baseline));
            _empty.Draw(context, new Point(rect.Left + (rect.Width - _empty.WidthIncludingTrailingWhitespace) / 2, rect.Top + (rect.Height - _empty.Height) / 2));
            DrawFocus(context);
            return;
        }
        if (_valueTicks.Count == 0)
        {
            RebuildValueTicks();
            rect = PlotRect;
        }
        foreach (var (value, text) in _valueTicks)
        {
            var y = YOf(value, rect, owner);
            if (y < rect.Top - 1 || y > rect.Bottom + 1)
            {
                continue;
            }
            var snapped = Math.Round(y) + 0.5;
            context.DrawLine(_gridPen!, new Point(rect.Left, snapped), new Point(rect.Right, snapped));
            text.Draw(context, new Point(rect.Left - 8 - text.WidthIncludingTrailingWhitespace, y - text.Height / 2));
        }
        context.DrawLine(_axisPen!, new Point(rect.Left, baseline), new Point(rect.Right, baseline));
        DrawTimeLabels(context, rect, owner, baseline);

        EnsureGeometry(rect, owner);
        // The clip's right side is the live edge: the segment into the next reading is cut there, so the line grows
        // smoothly between readings without touching the geometry.
        var dx = rect.Right - (owner.HeadTicks - _geometryBase) * _geometryScale;
        using (context.PushClip(new Rect(rect.Left, rect.Top - 2, rect.Width, rect.Height + 4)))
        using (context.PushTransform(Matrix.CreateTranslation(dx, 0)))
        {
            context.DrawGeometry(_areaBrush, null, _area!);
            context.DrawGeometry(null, _linePen, _line!);
        }

        var leadY = YOf(owner.LeadingValue, rect, owner);
        var cursor = owner.CursorIndex;
        DrawMarkers(context, rect, owner);
        if (cursor >= 0)
        {
            var x = XOf(owner.Buffer.TicksAt(cursor), rect, owner);
            var y = YOf(owner.Buffer.ValueAt(cursor), rect, owner);
            context.DrawLine(_cursorPen!, new Point(x, rect.Top), new Point(x, rect.Bottom));
            context.DrawEllipse(_lineBrush, _ringPen, new Point(x, y), DotRadius + 1, DotRadius + 1);
        }
        else
        {
            context.DrawEllipse(_lineBrush, _ringPen, new Point(rect.Right, leadY), DotRadius, DotRadius);
        }
        DrawFocus(context);
        _tooltip.Draw(context, p);
    }

    private void DrawFocus(DrawingContext context)
    {
        if (_keyboard)
        {
            context.DrawRectangle(null, _focusPen, new RoundedRect(new Rect(Bounds.Size).Inflate(3), 10));
        }
    }

    private void DrawTimeLabels(DrawingContext context, in Rect rect, RealtimeStream owner, double baseline)
    {
        var buffer = owner.Buffer;
        var window = owner.WindowTicks;
        if (Math.Abs(_timeStepWidth - rect.Width) > 0.5)
        {
            _timeStepWidth = rect.Width;
            using var sample = Layout(owner.FormatTime(buffer.LastTicks), _palette.TypeXS, _palette.TextMuted);
            _timeStepSeconds = ChooseTimeStep((double)window / TimeSpan.TicksPerSecond, rect.Width, sample.WidthIncludingTrailingWhitespace, owner.TimeLabelGap);
        }
        var step = (long)(_timeStepSeconds * TimeSpan.TicksPerSecond);
        var head = owner.HeadTicks;
        var from = head - window;
        var y = baseline + 6;
        var fadeRoom = 24.0;
        // Round moments counted from the zero of the clock, so a label keeps its time as it scrolls.
        var offset = TimeZoneOffsetTicks(owner);
        for (var t = (from + offset) / step * step - offset + step; t <= head; t += step)
        {
            var x = XOf(t, rect, owner);
            var layout = TimeLabel(t, owner);
            var half = layout.WidthIncludingTrailingWhitespace / 2;
            var room = Math.Min(x - half, Bounds.Width - (x + half));
            var edge = Math.Min(x - rect.Left, Bounds.Width - x);
            if (room < 0 || edge < 0)
            {
                continue;
            }
            context.DrawLine(_axisPen!, new Point(Math.Round(x) + 0.5, baseline), new Point(Math.Round(x) + 0.5, baseline + 3));
            var fade = Math.Min(1, edge / fadeRoom);
            if (fade < 1)
            {
                using (context.PushOpacity(fade))
                {
                    layout.Draw(context, new Point(x - half, y));
                }
            }
            else
            {
                layout.Draw(context, new Point(x - half, y));
            }
        }
        EvictTimeLabels(from);
    }

    private static long TimeZoneOffsetTicks(RealtimeStream owner) => owner.OffsetTicks;

    private TextLayout TimeLabel(long ticks, RealtimeStream owner)
    {
        if (!_timeLabels.TryGetValue(ticks, out var layout))
        {
            layout = Layout(owner.FormatTime(ticks), _palette.TypeXS, _palette.TextMuted);
            _timeLabels[ticks] = layout;
        }
        return layout;
    }

    private void EvictTimeLabels(long before)
    {
        if (_timeLabels.Count < 48)
        {
            return;
        }
        _timeLabelScratch.Clear();
        foreach (var ticks in _timeLabels.Keys)
        {
            if (ticks < before)
            {
                _timeLabelScratch.Add(ticks);
            }
        }
        foreach (var ticks in _timeLabelScratch)
        {
            _timeLabels.Remove(ticks, out var layout);
            layout?.Dispose();
        }
    }

    /// <summary>Anomaly markers in view, newest first: a label that would overlap a newer one is left to the tooltip.</summary>
    private void DrawMarkers(DrawingContext context, in Rect rect, RealtimeStream owner)
    {
        var buffer = owner.Buffer;
        var markers = owner.Markers;
        var labelLeft = double.PositiveInfinity;
        for (var i = markers.Count - 1; i >= 0; i--)
        {
            var marker = markers[i];
            var index = marker.Sequence - buffer.Dropped;
            if (index < 0 || index >= buffer.Count)
            {
                continue;
            }
            var ticks = buffer.TicksAt((int)index);
            if (ticks > owner.HeadTicks || marker.Progress <= 0)
            {
                continue;
            }
            if (ticks < owner.HeadTicks - owner.WindowTicks)
            {
                break;
            }
            var value = buffer.ValueAt((int)index);
            var x = XOf(ticks, rect, owner);
            var y = YOf(value, rect, owner);
            var scale = PopScale(marker.Progress);
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
            {
                context.DrawGeometry(_markerBrush, _ringPen, _diamond!);
            }
            var layout = marker.Layout ??= Layout(buffer.TagAt((int)index) ?? string.Empty, _palette.TypeXS, _palette.Foreground, 120);
            var textWidth = layout.WidthIncludingTrailingWhitespace;
            var chipWidth = textWidth + 12;
            var chipHeight = layout.Height + 4;
            var radius = owner.MarkerRadius;
            var chipLeft = ChartsBMath.Clamp(x - chipWidth / 2, rect.Left, Math.Max(rect.Left, rect.Right - chipWidth));
            var chipTop = y - radius - 4 - chipHeight;
            if (chipTop < rect.Top)
            {
                // No room above a spike: beside the marker, because below it the chip would cover the line.
                chipTop = Math.Max(rect.Top, y - chipHeight / 2);
                chipLeft = x + radius + 4 + chipWidth <= rect.Right ? x + radius + 4 : Math.Max(rect.Left, x - radius - 4 - chipWidth);
            }
            if (chipLeft + chipWidth + 4 > labelLeft || chipWidth > rect.Width)
            {
                continue;
            }
            labelLeft = chipLeft;
            var chip = new Rect(chipLeft, chipTop, chipWidth, chipHeight);
            var center = chip.Center;
            using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X, center.Y)))
            {
                context.DrawRectangle(_chipBrush, _chipPen, new RoundedRect(chip.Deflate(0.5), chipHeight / 2));
                layout.Draw(context, new Point(chipLeft + 6, chipTop + 2));
            }
        }
    }

    /// <summary>Grows to full size past it once and settles: a single overshoot.</summary>
    internal static double PopScale(double t)
    {
        t = ChartsBMath.Clamp(t, 0, 1);
        const double back = 1.70158;
        var u = t - 1;
        return 1 + (back + 1) * u * u * u + back * u * u;
    }
}
