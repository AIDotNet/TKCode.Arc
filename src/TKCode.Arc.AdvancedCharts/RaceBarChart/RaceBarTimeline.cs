using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The timeline of a <see cref="RaceBarChart"/>: a slider measured in periods, self drawn so the thumb can sit between
/// two periods while the race moves. The whole band (36 px) takes the pointer, so a finger can scrub. Keys bubble to the
/// chart, which owns the keyboard model.
/// </summary>
internal sealed class RaceBarTimeline : Control
{
    private const double PadX = 9;
    private const double TrackHeight = 4;
    private const double ThumbRadius = 8;
    private const double DotRadius = 1.5;
    private const double MinDotSpacing = 14;

    private RaceBarChart? _chart;
    private bool _dragging;
    private double _reported;
    private string? _reportedText;
    private int _paletteVersion = -1;
    private Color _thumbColor = Colors.White;
    private BoxShadows _thumbShadow;

    public RaceBarTimeline()
    {
        Focusable = true;
        IsTabStop = true;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal void Attach(RaceBarChart chart)
    {
        _chart = chart;
        Report();
    }

    /// <summary>The value text reported to assistive technology.</summary>
    internal string? ValueText => _reportedText;

    /// <summary>The position reported to assistive technology.</summary>
    internal double ReportedValue => _reported;

    /// <summary>Publishes the current period to assistive technology (not during playback: the chart calls this per period).</summary>
    public void Report()
    {
        if (_chart is null)
        {
            return;
        }
        var text = _chart.TimelineValueText;
        if (text == _reportedText)
        {
            return;
        }
        var old = _reported;
        _reportedText = text;
        _reported = _chart.FrameIndex;
        AutomationProperties.SetName(this, _chart.TimelineName);
        AutomationProperties.SetHelpText(this, text);
        if (old != _reported && !_chart.IsPlaying && ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, old, _reported);
        }
    }

    private Rect TrackRect => new(PadX, (Bounds.Height - TrackHeight) / 2, Math.Max(1, Bounds.Width - PadX * 2), TrackHeight);

    private double Fraction(double x)
    {
        var track = TrackRect;
        return Math.Clamp((x - track.X) / track.Width, 0, 1);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (_chart is null || !IsEffectivelyEnabled || (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed))
        {
            return;
        }
        _dragging = true;
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        _chart.ScrubTo(Fraction(point.Position.X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            _chart?.ScrubTo(Fraction(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            _chart?.EndScrub();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            _chart?.EndScrub();
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
        _chart?.SetReading(-1);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width, 0);

    public override void Render(DrawingContext context)
    {
        if (_chart is null)
        {
            return;
        }
        var p = _chart.Palette;
        if (_paletteVersion != p.Version)
        {
            _paletteVersion = p.Version;
            _thumbColor = ArcTokens.GetColor(this, ArcDesignTokenKind.ControlThumb);
            _thumbShadow = ArcTokens.Get<BoxShadows>(this, ArcDesignTokenKind.ControlThumbShadow);
        }
        var size = Bounds.Size;
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, p.Pen(p.FocusRing, 2), new RoundedRect(new Rect(size).Deflate(1), size.Height / 2));
        }
        var engine = _chart.Engine;
        var track = TrackRect;
        var opacity = IsEffectivelyEnabled ? 1 : 0.5;
        using (context.PushOpacity(opacity))
        {
            var trackColor = ChartsBMath.MixIn(p.BorderStrong, 0.35, p.Border);
            context.DrawRectangle(p.Brush(trackColor), null, new RoundedRect(track, TrackHeight / 2));
            var last = Math.Max(1, engine.FrameCount - 1);
            var fraction = engine.FrameCount > 1 ? Math.Clamp(engine.Thumb / last, 0, 1) : 0;
            var x = track.X + fraction * track.Width;
            var fill = p.Series(0);
            if (x > track.X)
            {
                context.DrawRectangle(p.Brush(fill), null, new RoundedRect(new Rect(track.X, track.Y, x - track.X, track.Height), TrackHeight / 2));
            }
            // A dot per period while they do not crowd each other: the track reads as discrete steps.
            if (engine.FrameCount > 2 && track.Width / last >= MinDotSpacing)
            {
                var passed = p.Brush(p.Surface);
                var ahead = p.Brush(p.TextMuted);
                for (var f = 1; f < last; f++)
                {
                    var dot = track.X + track.Width * f / last;
                    context.DrawEllipse(dot <= x ? passed : ahead, null, new Point(dot, size.Height / 2), DotRadius, DotRadius);
                }
            }
            var thumb = new Rect(x - ThumbRadius, size.Height / 2 - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);
            context.DrawRectangle(p.Brush(_thumbColor), null, new RoundedRect(thumb, ThumbRadius), _thumbShadow);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelinePeer(this);

    private sealed class TimelinePeer : ControlAutomationPeer, IRangeValueProvider
    {
        public TimelinePeer(RaceBarTimeline owner) : base(owner)
        {
        }

        private RaceBarTimeline Timeline => (RaceBarTimeline)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string? GetNameCore() => Timeline._chart?.TimelineName;

        protected override string? GetHelpTextCore() => Timeline._reportedText;

        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => Math.Max(0, (Timeline._chart?.Engine.FrameCount ?? 1) - 1);

        public double Value => Timeline._reported;

        public double SmallChange => 1;

        public double LargeChange => 1;

        public void SetValue(double value) => Timeline._chart?.SeekTo((int)Math.Round(value));
    }
}
