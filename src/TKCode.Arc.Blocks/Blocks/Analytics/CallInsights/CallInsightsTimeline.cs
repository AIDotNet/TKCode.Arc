using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The call's timeline, drawn in one pass: the moments row (a mark per key moment), the sentiment curve above and below
/// a neutral line (green above, red below), a lane per speaker with a bar per utterance, search matches as ticks under
/// the lanes, the chosen topic's stretches tinted across every row, and the time axis. The playhead is a separate child
/// moved by a render transform, so playing does not redraw the curves.
/// </summary>
/// <remarks>
/// Pointer: press or drag to seek (a press on the moments row snaps to the nearest moment); hover reports the time,
/// speaker and sentiment under the pointer. Keyboard: ←/→ 5 s (Shift: 30 s), Home/End, PageUp/PageDown the previous and
/// next moment, Space plays or pauses. The curve draws in from the left the first time it is shown (Full motion only).
/// </remarks>
internal sealed class CallInsightsTimeline : Control
{
    private const double MomentsRow = 18;
    private const double Gutter = 92;
    private const double AxisRow = 20;
    private const double TrackGap = 8;

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly CallInsightsPlayhead _playhead = new();
    private readonly TranslateTransform _playheadShift = new();
    private CallInsightsModel? _model;
    private IReadOnlyList<CallInsightsUtterance> _matches = Array.Empty<CallInsightsUtterance>();
    private bool _isFiltered;
    private CallInsightsTopicSummary? _topic;
    private StreamGeometry? _curve;
    private StreamGeometry? _area;
    private Size _geometrySize;
    private MotionValue<double>? _reveal;
    private double _revealValue = 1;
    private bool _revealed;
    private double _hoverX = double.NaN;
    private bool _dragging;
    private TimeSpan _position;
    private int _currentIndex = -1;

    public CallInsightsTimeline()
    {
        Focusable = true;
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Hand);
        _playhead.RenderTransform = _playheadShift;
        _playhead.IsHitTestVisible = false;
        VisualChildren.Add(_playhead);
        LogicalChildren.Add(_playhead);
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>The sentiment track's height.</summary>
    public double SentimentHeight { get; set; } = 72;

    /// <summary>One speaker lane's height and the gap between lanes.</summary>
    public double LaneHeight { get; set; } = 10;

    public double LaneGap { get; set; } = 4;

    /// <summary>The labels the timeline reads out and draws.</summary>
    public CallInsightsLabels Labels { get; set; } = new();

    /// <summary>Called with the time the person moved the playhead to.</summary>
    public Action<TimeSpan>? SeekRequested { get; set; }

    /// <summary>Called when Space is pressed on the timeline.</summary>
    public Action? PlayToggleRequested { get; set; }

    /// <summary>Called with the time under the pointer while hovering (null when it leaves).</summary>
    public Action<TimeSpan?>? HoverChanged { get; set; }

    public TimeSpan Position => _position;

    public CallInsightsModel? Model => _model;

    internal double PlotLeft => Gutter;

    internal double PlotWidth => Math.Max(1, Bounds.Width - Gutter);

    internal double PlayheadX => _playheadShift.X;

    internal double RevealValue => _revealValue;

    public void SetModel(CallInsightsModel model)
    {
        _model = model;
        _curve = null;
        _area = null;
        _text.Clear();
        InvalidateMeasure();
        InvalidateVisual();
        UpdatePlayhead();
    }

    public void SetMatches(IReadOnlyList<CallInsightsUtterance> matches, bool isFiltered)
    {
        _matches = matches;
        _isFiltered = isFiltered;
        InvalidateVisual();
    }

    public void SetTopic(CallInsightsTopicSummary? topic)
    {
        _topic = topic;
        InvalidateVisual();
    }

    public void SetPosition(TimeSpan position)
    {
        _position = position;
        UpdatePlayhead();
        // Only the current utterance's bar changes with the position; the rest of the drawing stays.
        var current = _model?.UtteranceAt(position) ?? -1;
        if (current != _currentIndex)
        {
            _currentIndex = current;
            InvalidateVisual();
        }
        if (ControlAutomationPeer.FromElement(this) is CallInsightsTimelinePeer peer)
        {
            peer.RaiseValue();
        }
    }

    /// <summary>The time at a point's x, clamped to the call.</summary>
    public TimeSpan TimeAt(double x)
    {
        var duration = _model?.Duration ?? TimeSpan.Zero;
        var fraction = Math.Clamp((x - Gutter) / PlotWidth, 0, 1);
        return TimeSpan.FromSeconds(Math.Round(duration.TotalSeconds * fraction));
    }

    /// <summary>The x of a time.</summary>
    public double XOf(TimeSpan time)
    {
        var duration = _model?.Duration.TotalSeconds ?? 0;
        return duration <= 0 ? Gutter : Gutter + PlotWidth * Math.Clamp(time.TotalSeconds / duration, 0, 1);
    }

    private int LaneCount => Math.Max(1, _model?.Speakers.Count ?? 1);

    private double SentimentTop => MomentsRow;

    private double LanesTop => MomentsRow + SentimentHeight + TrackGap;

    private double LanesBottom => LanesTop + LaneCount * LaneHeight + (LaneCount - 1) * LaneGap;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = LanesBottom + 6 + AxisRow;
        _playhead.Measure(new Size(2, height));
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 640;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _playhead.Top = 0;
        _playhead.Bottom = LanesBottom + 4;
        _playhead.Arrange(new Rect(0, 0, 12, finalSize.Height));
        UpdatePlayhead();
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_revealed)
        {
            _revealed = true;
            if (ArcMotion.IsFull(this))
            {
                _reveal ??= new MotionValue<double>(this, 0, MotionVectors.Double);
                _reveal.Changed += v =>
                {
                    _revealValue = v;
                    InvalidateVisual();
                };
                _revealValue = 0;
                _ = _reveal.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(900), ArcEasings.InOut));
            }
        }
    }

    private void RefreshPalette()
    {
        _text.Clear();
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        _playhead.InvalidateVisual();
        InvalidateVisual();
    }

    internal AnalyticsPalette Palette
    {
        get
        {
            if (!_palette.IsReady)
            {
                _palette.Refresh(this);
            }
            return _palette;
        }
    }

    private void UpdatePlayhead()
    {
        _playheadShift.X = XOf(_position) - 6;
    }

    public override void Render(DrawingContext context)
    {
        var p = Palette;
        var size = Bounds.Size;
        // The whole area takes the pointer (a press anywhere seeks).
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (_model is not { } model)
        {
            return;
        }
        var left = Gutter;
        var width = PlotWidth;
        var typeface = new Typeface(p.BodyFont);

        // Gutter labels: the sentiment track and one per speaker.
        var sentimentLabel = _text.GetTrimmed(Labels.Sentiment, typeface, 12, Gutter - 12);
        sentimentLabel.Draw(context, p.Brush(p.TextMuted), new Point(0, SentimentTop + SentimentHeight / 2 - sentimentLabel.Height / 2));
        for (var i = 0; i < model.Speakers.Count; i++)
        {
            var y = LanesTop + i * (LaneHeight + LaneGap);
            var label = _text.GetTrimmed(model.Speakers[i].Name, typeface, 12, Gutter - 12);
            label.Draw(context, p.Brush(p.TextSecondary), new Point(0, y + LaneHeight / 2 - label.Height / 2));
        }

        // The track behind the curve and the lanes.
        var trackBrush = p.Brush(p.SurfaceMuted, 0.55);
        context.FillRectangle(trackBrush, new Rect(left, SentimentTop, width, SentimentHeight));
        for (var i = 0; i < model.Speakers.Count; i++)
        {
            var y = LanesTop + i * (LaneHeight + LaneGap);
            context.FillRectangle(trackBrush, new Rect(left, y, width, LaneHeight));
        }

        // The chosen topic's stretches, tinted across every row.
        if (_topic is { } topic)
        {
            var tint = p.Brush(p.Accent, 0.12);
            var edge = p.Brush(p.Accent);
            foreach (var stretch in topic.Stretches)
            {
                var x0 = XOf(stretch.Start);
                var x1 = Math.Max(x0 + 2, XOf(stretch.End));
                context.FillRectangle(tint, new Rect(x0, SentimentTop, x1 - x0, LanesBottom - SentimentTop));
                context.FillRectangle(edge, new Rect(x0, SentimentTop, x1 - x0, 2));
            }
        }

        using (context.PushClip(new Rect(left - 8, 0, (width + 16) * _revealValue, size.Height)))
        {
            DrawSentiment(context, p, left, width);
            DrawLanes(context, p, model);
        }
        DrawMoments(context, p, model);
        DrawAxis(context, p, typeface, model, left, width);

        if (!double.IsNaN(_hoverX) && !_dragging)
        {
            context.FillRectangle(p.Brush(p.BorderStrong), new Rect(Math.Round(_hoverX), SentimentTop, 1, LanesBottom - SentimentTop));
        }
        if (Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new Rect(left - 4, SentimentTop - 4, width + 8, LanesBottom - SentimentTop + 8), 6, 6);
        }
    }

    private void DrawSentiment(DrawingContext context, AnalyticsPalette p, double left, double width)
    {
        var top = SentimentTop;
        var mid = top + SentimentHeight / 2;
        if (_curve is null || _geometrySize != Bounds.Size)
        {
            BuildCurve(left, width, mid);
            _geometrySize = Bounds.Size;
        }
        context.FillRectangle(p.Brush(p.Border), new Rect(left, Math.Round(mid), width, 1));
        // Above the line in the success color, below in the danger color: the same geometry clipped twice.
        using (context.PushClip(new Rect(left, top, width, mid - top)))
        {
            context.DrawGeometry(p.Brush(p.Success, 0.16), null, _area!);
            context.DrawGeometry(null, p.Pen(p.Success, 2), _curve!);
        }
        using (context.PushClip(new Rect(left, mid, width, top + SentimentHeight - mid)))
        {
            context.DrawGeometry(p.Brush(p.Danger, 0.16), null, _area!);
            context.DrawGeometry(null, p.Pen(p.Danger, 2), _curve!);
        }
    }

    private void BuildCurve(double left, double width, double mid)
    {
        var samples = _model!.Sentiment;
        var amplitude = SentimentHeight / 2 - 4;
        var count = samples.Length;
        var points = new Point[count];
        for (var i = 0; i < count; i++)
        {
            points[i] = new Point(left + width * i / (count - 1), mid - samples[i] * amplitude);
        }
        _curve = new StreamGeometry();
        using (var g = _curve.Open())
        {
            Trace(g, points, close: false, mid);
        }
        _area = new StreamGeometry();
        using (var g = _area.Open())
        {
            Trace(g, points, close: true, mid);
        }
    }

    /// <summary>A smooth curve through the points (Catmull-Rom as cubic Béziers); closed down to the neutral line for the area.</summary>
    private static void Trace(StreamGeometryContext g, Point[] points, bool close, double mid)
    {
        if (points.Length == 0)
        {
            return;
        }
        if (close)
        {
            g.BeginFigure(new Point(points[0].X, mid), true);
            g.LineTo(points[0]);
        }
        else
        {
            g.BeginFigure(points[0], false);
        }
        for (var i = 0; i < points.Length - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(points.Length - 1, i + 2)];
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);
            g.CubicBezierTo(c1, c2, p2);
        }
        if (close)
        {
            g.LineTo(new Point(points[^1].X, mid));
        }
        g.EndFigure(close);
    }

    private void DrawLanes(DrawingContext context, AnalyticsPalette p, CallInsightsModel model)
    {
        var current = model.UtteranceAt(_position);
        for (var i = 0; i < model.Utterances.Length; i++)
        {
            var u = model.Utterances[i];
            var lane = model.IndexOfSpeaker(u.SpeakerKey);
            var y = LanesTop + lane * (LaneHeight + LaneGap);
            var x0 = XOf(u.Start);
            var x1 = Math.Max(x0 + 2, XOf(u.End) - 1);
            var dim = _isFiltered && !Contains(_matches, u);
            var color = p.Series(lane);
            var alpha = dim ? 0.25 : i == current ? 1 : 0.8;
            context.DrawRectangle(p.Brush(color, alpha), null, new Rect(x0, y, x1 - x0, LaneHeight), 2, 2);
        }
        if (_isFiltered)
        {
            var tick = p.Brush(p.Warning);
            foreach (var u in _matches)
            {
                var x = XOf(u.Start);
                context.FillRectangle(tick, new Rect(x - 1, LanesBottom + 2, 2, 4));
            }
        }
    }

    private static bool Contains(IReadOnlyList<CallInsightsUtterance> list, CallInsightsUtterance item)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
            {
                return true;
            }
        }
        return false;
    }

    private void DrawMoments(DrawingContext context, AnalyticsPalette p, CallInsightsModel model)
    {
        var y = MomentsRow / 2;
        foreach (var moment in model.Moments)
        {
            var x = XOf(moment.Time);
            var color = MomentColor(p, moment.Kind);
            context.DrawEllipse(p.Brush(p.Surface), p.Pen(color, 2), new Point(x, y), 4.5, 4.5);
            context.FillRectangle(p.Brush(color, 0.35), new Rect(x - 0.5, y + 5, 1, SentimentTop - y - 5 + SentimentHeight));
        }
    }

    internal static Color MomentColor(AnalyticsPalette p, CallInsightsMomentKind kind) => kind switch
    {
        CallInsightsMomentKind.Objection => p.Danger,
        CallInsightsMomentKind.Pricing => p.Warning,
        CallInsightsMomentKind.Competitor => p.Series4,
        CallInsightsMomentKind.Commitment => p.Success,
        _ => p.Accent,
    };

    private void DrawAxis(DrawingContext context, AnalyticsPalette p, Typeface typeface, CallInsightsModel model, double left, double width)
    {
        var duration = model.Duration.TotalSeconds;
        if (duration <= 0)
        {
            return;
        }
        // Ticks every 1, 2, 5, 10, 15 or 30 minutes, whichever keeps them 70px apart or more.
        double[] steps = [60, 120, 300, 600, 900, 1800, 3600];
        var step = steps[^1];
        foreach (var candidate in steps)
        {
            if (width * candidate / duration >= 70)
            {
                step = candidate;
                break;
            }
        }
        var y = LanesBottom + 8;
        var brush = p.Brush(p.TextMuted);
        for (var t = 0.0; t <= duration + 0.5; t += step)
        {
            var label = _text.Get(CallInsightsModel.FormatTime(TimeSpan.FromSeconds(t)), typeface, 11, tabular: true);
            var x = left + width * t / duration;
            var drawX = Math.Clamp(x - label.Width / 2, left, left + width - label.Width);
            label.Draw(context, brush, new Point(drawX, y));
        }
    }

    #region Input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _model is null)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (point.X < Gutter - 6)
        {
            return;
        }
        Focus(NavigationMethod.Pointer);
        if (point.Y < MomentsRow && NearestMoment(point.X) is { } moment)
        {
            SeekRequested?.Invoke(moment.Time);
            e.Handled = true;
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        SeekRequested?.Invoke(TimeAt(point.X));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_dragging)
        {
            SeekRequested?.Invoke(TimeAt(point.X));
            return;
        }
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var inside = point.X >= Gutter && point.X <= Bounds.Width && point.Y >= 0 && point.Y <= LanesBottom;
        SetHover(inside ? point.X : double.NaN);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(double.NaN);
    }

    private void SetHover(double x)
    {
        if (double.IsNaN(x) && double.IsNaN(_hoverX))
        {
            return;
        }
        _hoverX = x;
        HoverChanged?.Invoke(double.IsNaN(x) ? null : TimeAt(x));
        InvalidateVisual();
    }

    private CallInsightsMoment? NearestMoment(double x)
    {
        CallInsightsMoment? best = null;
        var bestDistance = 10.0;
        foreach (var moment in _model!.Moments)
        {
            var distance = Math.Abs(XOf(moment.Time) - x);
            if (distance <= bestDistance)
            {
                best = moment;
                bestDistance = distance;
            }
        }
        return best;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_model is not { } model || e.Handled)
        {
            return;
        }
        var step = TimeSpan.FromSeconds(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 30 : 5);
        TimeSpan? target = e.Key switch
        {
            Key.Left => _position - step,
            Key.Right => _position + step,
            Key.Home => TimeSpan.Zero,
            Key.End => model.Duration,
            Key.PageUp => model.MomentAfter(_position, forward: false)?.Time ?? TimeSpan.Zero,
            Key.PageDown => model.MomentAfter(_position, forward: true)?.Time ?? model.Duration,
            _ => null,
        };
        if (target is { } time)
        {
            SeekRequested?.Invoke(time < TimeSpan.Zero ? TimeSpan.Zero : time > model.Duration ? model.Duration : time);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            PlayToggleRequested?.Invoke();
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new CallInsightsTimelinePeer(this);
}

/// <summary>The playhead: a 2px accent line with a round knob on top, drawn once and moved by its render transform.</summary>
internal sealed class CallInsightsPlayhead : Control
{
    public double Top { get; set; }

    public double Bottom { get; set; } = 100;

    public override void Render(DrawingContext context)
    {
        if (Parent is not CallInsightsTimeline timeline)
        {
            return;
        }
        var p = timeline.Palette;
        var accent = p.Brush(p.Accent);
        context.FillRectangle(accent, new Rect(5, Top + 6, 2, Math.Max(0, Bottom - Top - 6)));
        context.DrawEllipse(accent, p.Pen(p.Surface, 2), new Point(6, Top + 6), 5, 5);
    }
}

/// <summary>Reports the timeline as a slider over the call's seconds, named by the labels and described by the readout.</summary>
internal sealed class CallInsightsTimelinePeer(CallInsightsTimeline owner) : ControlAutomationPeer(owner), IRangeValueProvider
{
    private CallInsightsTimeline Timeline => (CallInsightsTimeline)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner) is { Length: > 0 } name ? name : Timeline.Labels.Timeline;

    protected override string? GetHelpTextCore() => CallInsightsModel.FormatTime(Timeline.Position)
                                                    + " / " + CallInsightsModel.FormatTime(Timeline.Model?.Duration ?? TimeSpan.Zero);

    protected override bool IsContentElementCore() => true;

    public bool IsReadOnly => false;

    public double Minimum => 0;

    public double Maximum => Timeline.Model?.Duration.TotalSeconds ?? 0;

    public double Value => Timeline.Position.TotalSeconds;

    public double SmallChange => 5;

    public double LargeChange => 30;

    public void SetValue(double value) => Timeline.SeekRequested?.Invoke(TimeSpan.FromSeconds(Math.Clamp(value, Minimum, Maximum)));

    private double _last;

    public void RaiseValue()
    {
        var value = Value;
        RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, _last, value);
        _last = value;
    }
}
