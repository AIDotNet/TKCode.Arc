using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The self-drawn chart of a <see cref="LineReplay"/> (Arc Line replay, design reference): the callout lane, the value
/// grid and axis labels, the dashed comparison, the area and the line up to the playhead, a stem and a marker for each
/// moment passed, the callout of the latest one with its connector, the playhead with the tip dot and value label, and
/// the clock on the time axis. A horizontal drag or a tap scrubs. The drawing is hidden from assistive technology: the
/// readout and the scrubber carry the same information as text.
/// </summary>
/// <remarks>
/// Motion is code-driven: playback writes the position every frame, and the markers, the callout and the axis labels
/// that give way to the clock animate per item in self-drawn space, which ControlTheme transitions cannot reach.
/// </remarks>
internal sealed class LineReplayStage : Control
{
    // Metrics (Arc Line replay, design reference).
    internal const double LaneHeight = 64;
    internal const double PlotLeft = 40;
    internal const double PlotRight = 16;
    internal const double PlotBottom = 32;
    private const double AxisLabelGap = 8;
    private const double LabelSize = 12;
    private const double CalloutGap = 12;
    private const double CalloutPadX = 10;
    private const double CalloutPadY = 6;
    private const double CalloutRadius = 12;
    private const double PillPadX = 8;
    private const double TipGap = 12;
    private const double MarkRadius = 4;
    private const double TipRadius = 4.5;
    private const double AreaAlpha = 0.09;
    private const double StemAlpha = 0.8;
    private const double BaselineAlpha = 0.8;
    private const double MarkStartScale = 0.4;
    private const double CalloutRise = 8;

    private readonly LineReplay _owner;
    private readonly LineChartsTextCache _text = new();
    private LineReplayData _data = LineReplayData.Empty;
    private MotionValue<double>[] _appear = [];
    private bool[] _shown = [];
    private double[] _ticks = [];
    private MotionValue<double>[] _tickOpacity = [];
    private readonly MotionValue<double> _calloutEnter;
    private readonly MotionValue<double> _calloutExit;
    private int _callout = -1;
    private int _previousCallout = -1;
    private int _current = -1;
    private double _position;
    private bool _narrow;
    private bool _scrubbing;

    private Point[] _points = [];
    private StreamGeometry? _baseline;
    private Size _geometrySize;

    private bool _colorsKnown;
    private IImmutableBrush _surface = Brushes.White;
    private IImmutableBrush _foreground = Brushes.Black;
    private IImmutableBrush _secondary = Brushes.Gray;
    private IImmutableBrush _muted = Brushes.Gray;
    private IImmutableBrush _series = Brushes.SteelBlue;
    private IImmutableBrush _warning = Brushes.Orange;
    private IImmutableBrush _area = Brushes.Transparent;
    private IPen _gridPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _axisPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _borderPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _strongPen = new ImmutablePen(Brushes.Gray, 1);
    private IPen _stemPen = new ImmutablePen(Brushes.Gray, 1);
    private IPen _linePen = new ImmutablePen(Brushes.SteelBlue, 2);
    private IPen _baselinePen = new ImmutablePen(Brushes.Gray, 1.25);
    private IPen _markPen = new ImmutablePen(Brushes.Black, 1.5);
    private IPen _incidentPen = new ImmutablePen(Brushes.Orange, 1.5);
    private IPen _tipPen = new ImmutablePen(Brushes.White, 2);

    public LineReplayStage(LineReplay owner)
    {
        _owner = owner;
        _calloutEnter = new MotionValue<double>(this, 1, MotionVectors.Double);
        _calloutExit = new MotionValue<double>(this, 0, MotionVectors.Double);
        _calloutEnter.Changed += _ => InvalidateVisual();
        _calloutExit.Changed += _ => InvalidateVisual();
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
    }

    // Theme and accent switches: colors are resolved again on the next render.
    private void OnThemeChanged()
    {
        _colorsKnown = false;
        InvalidateVisual();
    }

    /// <summary>The moment whose callout is shown (−1 for none; tests).</summary>
    internal int CalloutIndex => _callout;

    /// <summary>Whether a moment's marker is shown (tests).</summary>
    internal bool IsMarkerShown(int index) => index >= 0 && index < _shown.Length && _shown[index];

    /// <summary>The time axis ticks (tests).</summary>
    internal IReadOnlyList<double> Ticks => _ticks;

    /// <summary>The opacity a tick label is moving to (tests).</summary>
    internal double TickTargetOpacity(int index) => _tickOpacity[index].Target;

    /// <summary>The plot's rectangle.</summary>
    internal Rect PlotRect => new(PlotLeft, LaneHeight, Math.Max(1, Bounds.Width - PlotLeft - PlotRight),
        Math.Max(1, Bounds.Height - LaneHeight - PlotBottom));

    public void SetData(LineReplayData data)
    {
        _data = data;
        foreach (var value in _appear)
        {
            value.Dispose();
        }
        _appear = new MotionValue<double>[data.Moments.Length];
        _shown = new bool[data.Moments.Length];
        for (var i = 0; i < _appear.Length; i++)
        {
            var value = new MotionValue<double>(this, 0, MotionVectors.Double);
            value.Changed += _ => InvalidateVisual();
            _appear[i] = value;
        }
        _callout = -1;
        _previousCallout = -1;
        _current = -1;
        _geometrySize = default;
        RebuildTicks();
        InvalidateVisual();
    }

    public void SetNarrow(bool narrow)
    {
        if (narrow == _narrow)
        {
            return;
        }
        _narrow = narrow;
        RebuildTicks();
        InvalidateVisual();
    }

    private void RebuildTicks()
    {
        foreach (var value in _tickOpacity)
        {
            value.Dispose();
        }
        var span = _data.End - _data.Start;
        // Hours on the clock, about seven across (three when narrow), leaving out both ends.
        double[] steps = [15, 30, 60, 120, 180, 360, 720, 1440];
        var most = _narrow ? 3 : 7;
        var step = steps.FirstOrDefault(s => span / s - 1 <= most, steps[^1]);
        var list = new List<double>();
        for (var t = Math.Floor(_data.Start / step) * step + step; t < _data.End - step * 0.25; t += step)
        {
            list.Add(t);
        }
        _ticks = list.ToArray();
        _tickOpacity = new MotionValue<double>[_ticks.Length];
        for (var i = 0; i < _ticks.Length; i++)
        {
            var value = new MotionValue<double>(this, 1, MotionVectors.Double);
            value.Changed += _ => InvalidateVisual();
            _tickOpacity[i] = value;
        }
    }

    /// <summary>
    /// Moves the playhead. Moments at or before it show their marker (settling on the smooth spring when
    /// <paramref name="animate"/>), the latest one shows its callout, and axis labels under the clock give way.
    /// </summary>
    public void Update(double position, bool animate)
    {
        _position = position;
        var current = -1;
        for (var i = 0; i < _data.Moments.Length; i++)
        {
            var reached = _data.Moments[i].Time <= position + 1e-6;
            if (reached)
            {
                current = i;
            }
            if (reached == _shown[i])
            {
                continue;
            }
            _shown[i] = reached;
            if (reached && animate)
            {
                _appear[i].Jump(0);
                _ = _appear[i].AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
            }
            else
            {
                _appear[i].Jump(reached ? 1 : 0);
            }
        }
        if (current != _current)
        {
            _current = current;
            SetCallout(current, animate);
        }
        UpdateTickFades(animate);
        InvalidateVisual();
    }

    private void SetCallout(int index, bool animate)
    {
        if (index == _callout)
        {
            return;
        }
        _previousCallout = animate ? _callout : -1;
        _callout = index;
        if (animate && _previousCallout >= 0)
        {
            // The previous callout fades out faster than the new one enters.
            _calloutExit.Jump(1);
            _ = _calloutExit.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Exit));
        }
        else
        {
            _calloutExit.Jump(0);
        }
        if (animate && index >= 0)
        {
            _calloutEnter.Jump(0);
            _ = _calloutEnter.AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
        }
        else
        {
            _calloutEnter.Jump(1);
        }
    }

    private void UpdateTickFades(bool animate)
    {
        if (_ticks.Length == 0 || Bounds.Width <= 0)
        {
            return;
        }
        EnsureColors();
        var plot = PlotRect;
        var headX = XOf(plot, _position);
        var clock = _text.Get(_owner.FormatTimeText(_position), LabelSize, FontWeight.Medium);
        var clockHalf = clock.Width / 2 + PillPadX + 1;
        for (var i = 0; i < _ticks.Length; i++)
        {
            var label = _text.Get(_owner.FormatTickText(_ticks[i]), LabelSize);
            var x = XOf(plot, _ticks[i]);
            var clear = Math.Abs(x - headX) > clockHalf + label.Width / 2 + 6;
            var target = clear ? 1.0 : 0.0;
            var value = _tickOpacity[i];
            if (value.Target == target)
            {
                continue;
            }
            if (animate)
            {
                _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                value.Jump(target);
            }
        }
    }

    private double XOf(Rect plot, double time)
    {
        var span = _data.End - _data.Start;
        return plot.X + (span > 0 ? (time - _data.Start) / span : 1) * plot.Width;
    }

    private double YOf(Rect plot, double value) => plot.Bottom - (_data.AxisTop > 0 ? value / _data.AxisTop : 0) * plot.Height;

    private double TimeAt(double x)
    {
        var plot = PlotRect;
        var t = Math.Clamp((x - plot.X) / plot.Width, 0, 1);
        return _data.Start + t * (_data.End - _data.Start);
    }

    // Input ------------------------------------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || _data.Count == 0)
        {
            return;
        }
        _scrubbing = true;
        e.Pointer.Capture(this);
        _owner.ScrubTo(TimeAt(point.Position.X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_scrubbing)
        {
            _owner.ScrubTo(TimeAt(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_scrubbing)
        {
            _scrubbing = false;
            e.Pointer.Capture(null);
            _owner.EndScrub();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_scrubbing)
        {
            _scrubbing = false;
            _owner.EndScrub();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    // Rendering ------------------------------------------------------------------------------------------------------------

    private void EnsureColors()
    {
        if (_colorsKnown)
        {
            return;
        }
        var series = ArcTokens.GetColor(this, ArcDesignTokenKind.Series1);
        var muted = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        var warning = ArcTokens.GetColor(this, ArcDesignTokenKind.Warning);
        var borderStrong = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
        _surface = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Surface));
        _foreground = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground));
        _secondary = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary));
        _muted = new ImmutableSolidColorBrush(muted);
        _series = new ImmutableSolidColorBrush(series);
        _warning = new ImmutableSolidColorBrush(warning);
        _area = new ImmutableSolidColorBrush(series, AreaAlpha);
        _gridPen = new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.BorderSubtle)), 1);
        _axisPen = new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Border)), 1);
        _borderPen = _axisPen;
        _strongPen = new ImmutablePen(new ImmutableSolidColorBrush(borderStrong), 1);
        // Avalonia dash lengths are multiples of the thickness: 2px on and 3px off at 1px; 3 / 3 at 1.25px.
        _stemPen = new ImmutablePen(new ImmutableSolidColorBrush(borderStrong, StemAlpha), 1, new ImmutableDashStyle([2, 3], 0));
        _linePen = new ImmutablePen(_series, 2, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round);
        _baselinePen = new ImmutablePen(new ImmutableSolidColorBrush(muted, BaselineAlpha), 1.25,
            new ImmutableDashStyle([3 / 1.25, 3 / 1.25], 0), lineJoin: PenLineJoin.Round);
        _markPen = new ImmutablePen(_foreground, 1.5);
        _incidentPen = new ImmutablePen(_warning, 1.5);
        _tipPen = new ImmutablePen(_surface, 2);
        _text.Family = TextElement.GetFontFamily(this);
        _colorsKnown = true;
    }

    private void EnsureGeometry(Rect plot)
    {
        var size = Bounds.Size;
        if (size == _geometrySize && _points.Length == _data.Count)
        {
            return;
        }
        _geometrySize = size;
        _points = new Point[_data.Count];
        for (var i = 0; i < _data.Count; i++)
        {
            _points[i] = new Point(XOf(plot, _data.Times[i]), YOf(plot, _data.Values[i]));
        }
        _baseline = null;
        if (!_data.HasBaseline)
        {
            return;
        }
        _baseline = new StreamGeometry();
        using var g = _baseline.Open();
        var open = false;
        for (var i = 0; i < _data.Count; i++)
        {
            var b = _data.Baselines[i];
            if (!double.IsFinite(b))
            {
                if (open)
                {
                    g.EndFigure(false);
                    open = false;
                }
                continue;
            }
            var p = new Point(XOf(plot, _data.Times[i]), YOf(plot, b));
            if (!open)
            {
                g.BeginFigure(p, false);
                open = true;
            }
            else
            {
                g.LineTo(p);
            }
        }
        if (open)
        {
            g.EndFigure(false);
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        EnsureColors();
        // The whole stage scrubs, so it is hit-testable everywhere.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var plot = PlotRect;
        DrawGrid(context, plot);
        if (_data.Count == 0)
        {
            return;
        }
        EnsureGeometry(plot);
        if (_baseline is not null)
        {
            context.DrawGeometry(null, _baselinePen, _baseline);
        }
        var headX = XOf(plot, _position);
        var headValue = _data.ValueAt(_position);
        var head = new Point(headX, YOf(plot, headValue));
        DrawLine(context, plot, head);
        DrawStems(context, plot);
        DrawCalloutConnector(context, plot);
        context.DrawLine(_strongPen, new Point(Math.Round(headX) + 0.5, plot.Y), new Point(Math.Round(headX) + 0.5, plot.Bottom));
        DrawMarks(context, plot);
        context.DrawEllipse(_series, _tipPen, head, TipRadius, TipRadius);
        DrawCallouts(context, plot);
        DrawTipLabel(context, size, head, headValue);
        DrawClock(context, size, plot, headX);
    }

    private void DrawGrid(DrawingContext context, Rect plot)
    {
        var step = _data.AxisStep;
        if (step > 0 && _data.AxisTop > 0)
        {
            for (var v = 0.0; v <= _data.AxisTop + step * 0.01; v += step)
            {
                var y = Math.Round(YOf(plot, v)) + 0.5;
                context.DrawLine(v == 0 ? _axisPen : _gridPen, new Point(plot.X, y), new Point(plot.Right, y));
                var label = _text.Get(LineChartsFormat.CompactCount(v), LabelSize);
                label.DrawCentered(context, _muted, plot.X - AxisLabelGap - label.Width, y);
            }
        }
        for (var i = 0; i < _ticks.Length; i++)
        {
            var opacity = _tickOpacity[i].Value;
            if (opacity <= 0.01)
            {
                continue;
            }
            var label = _text.Get(_owner.FormatTickText(_ticks[i]), LabelSize);
            var x = XOf(plot, _ticks[i]);
            using (context.PushOpacity(opacity))
            {
                label.Draw(context, _muted, new Point(x - label.Width / 2, plot.Bottom + AxisLabelGap));
            }
        }
    }

    private void DrawLine(DrawingContext context, Rect plot, Point head)
    {
        var last = _data.IndexAtOrBefore(_position);
        if (last < 0 && _points.Length > 0)
        {
            return;
        }
        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var l = line.Open())
        using (var a = area.Open())
        {
            var first = _points[0];
            l.BeginFigure(first, false);
            a.BeginFigure(new Point(first.X, plot.Bottom), true);
            a.LineTo(first);
            for (var i = 1; i <= last && i < _points.Length; i++)
            {
                l.LineTo(_points[i]);
                a.LineTo(_points[i]);
            }
            if (head.X > _points[Math.Min(last, _points.Length - 1)].X)
            {
                l.LineTo(head);
                a.LineTo(head);
            }
            a.LineTo(new Point(head.X, plot.Bottom));
            a.EndFigure(true);
            l.EndFigure(false);
        }
        context.DrawGeometry(_area, null, area);
        context.DrawGeometry(null, _linePen, line);
    }

    private void DrawStems(DrawingContext context, Rect plot)
    {
        for (var i = 0; i < _data.Moments.Length; i++)
        {
            var appear = _appear[i].Value;
            if (appear <= 0.01)
            {
                continue;
            }
            var m = _data.Moments[i];
            var x = Math.Round(XOf(plot, m.Time)) + 0.5;
            var y = YOf(plot, m.Value);
            using (context.PushOpacity(Math.Clamp(appear, 0, 1)))
            {
                context.DrawLine(_stemPen, new Point(x, y + MarkRadius), new Point(x, plot.Bottom));
            }
        }
    }

    private void DrawMarks(DrawingContext context, Rect plot)
    {
        for (var i = 0; i < _data.Moments.Length; i++)
        {
            var appear = _appear[i].Value;
            if (appear <= 0.01)
            {
                continue;
            }
            var m = _data.Moments[i];
            var center = new Point(XOf(plot, m.Time), YOf(plot, m.Value));
            var scale = MarkStartScale + (1 - MarkStartScale) * appear;
            var pen = m.IsIncident ? _incidentPen : _markPen;
            // The latest moment passed is filled with its stroke color.
            var fill = i == _current ? (m.IsIncident ? _warning : _foreground) : _surface;
            using (context.PushOpacity(Math.Clamp(appear, 0, 1)))
            {
                context.DrawEllipse(fill, pen, center, MarkRadius * scale, MarkRadius * scale);
            }
        }
    }

    private (Rect Card, LineChartsTextCache.Entry Title, LineChartsTextCache.Entry Time) CalloutLayout(Rect plot, int index)
    {
        var m = _data.Moments[index];
        var title = _text.Get(m.Moment.Title ?? "", LabelSize, FontWeight.Medium);
        var time = _text.Get(_owner.FormatTimeText(m.Time), LabelSize);
        var width = Math.Ceiling(Math.Max(title.Width, time.Width) + CalloutPadX * 2 + 2);
        var height = Math.Ceiling(title.Height + time.Height + CalloutPadY * 2 + 2);
        var x = Math.Clamp(XOf(plot, m.Time) - width / 2, 0, Math.Max(0, Bounds.Width - width));
        var bottom = plot.Y - CalloutGap;
        return (new Rect(x, bottom - height, width, height), title, time);
    }

    private void DrawCalloutConnector(DrawingContext context, Rect plot)
    {
        if (_callout < 0 || _callout >= _data.Moments.Length)
        {
            return;
        }
        var m = _data.Moments[_callout];
        var (card, _, _) = CalloutLayout(plot, _callout);
        var x = Math.Round(XOf(plot, m.Time)) + 0.5;
        var y = YOf(plot, m.Value) - MarkRadius;
        using (context.PushOpacity(Math.Clamp(_calloutEnter.Value, 0, 1)))
        {
            context.DrawLine(_strongPen, new Point(x, card.Bottom), new Point(x, y));
        }
    }

    private void DrawCallouts(DrawingContext context, Rect plot)
    {
        if (_previousCallout >= 0 && _previousCallout < _data.Moments.Length && _calloutExit.Value > 0.01)
        {
            DrawCallout(context, plot, _previousCallout, _calloutExit.Value, 0);
        }
        if (_callout >= 0 && _callout < _data.Moments.Length)
        {
            var enter = _calloutEnter.Value;
            DrawCallout(context, plot, _callout, Math.Clamp(enter, 0, 1), (1 - enter) * CalloutRise);
        }
    }

    private void DrawCallout(DrawingContext context, Rect plot, int index, double opacity, double offsetY)
    {
        var (card, title, time) = CalloutLayout(plot, index);
        card = card.Translate(new Vector(0, offsetY));
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(_surface, _borderPen, new RoundedRect(card.Deflate(0.5), CalloutRadius));
            var x = card.X + 1 + CalloutPadX;
            var y = card.Y + 1 + CalloutPadY;
            title.Draw(context, _foreground, new Point(x, y));
            time.Draw(context, _secondary, new Point(x, y + title.Height));
        }
    }

    private void DrawTipLabel(DrawingContext context, Size size, Point head, double value)
    {
        if (!double.IsFinite(value))
        {
            return;
        }
        var text = _text.Get(_owner.FormatValueText(value), LabelSize, FontWeight.Medium);
        var width = Math.Ceiling(text.Width + PillPadX * 2 + 2);
        var height = Math.Ceiling(text.Height + 4 + 2);
        // To the right of the tip, or to its left when it would leave the stage.
        var x = head.X + TipGap + width <= size.Width ? head.X + TipGap : head.X - TipGap - width;
        var rect = new Rect(x, head.Y - height / 2, width, height);
        context.DrawRectangle(_surface, _borderPen, new RoundedRect(rect.Deflate(0.5), height / 2));
        text.DrawCentered(context, _foreground, rect.X + 1 + PillPadX, head.Y);
    }

    private void DrawClock(DrawingContext context, Size size, Rect plot, double headX)
    {
        var text = _text.Get(_owner.FormatTimeText(_position), LabelSize, FontWeight.Medium);
        var width = Math.Ceiling(text.Width + PillPadX * 2 + 2);
        var height = Math.Ceiling(text.Height + 2 + 2);
        var x = Math.Clamp(headX - width / 2, 0, Math.Max(0, size.Width - width));
        var centerY = plot.Bottom + 16;
        var rect = new Rect(x, centerY - height / 2, width, height);
        context.DrawRectangle(_surface, _strongPen, new RoundedRect(rect.Deflate(0.5), height / 2));
        text.DrawCentered(context, _foreground, rect.X + 1 + PillPadX, centerY);
    }
}
