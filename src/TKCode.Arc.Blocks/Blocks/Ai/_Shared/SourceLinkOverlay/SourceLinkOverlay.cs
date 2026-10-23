using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc;

/// <summary>
/// One end of a <see cref="SourceLinkOverlay"/> link: the bounds of the cited text or of the field, in the overlay's own
/// coordinates, and whether that end can be seen in its pane (a span scrolled out of the canvas, a field scrolled out of
/// the form). The owner works out both with <c>TranslatePoint</c>.
/// </summary>
/// <param name="Bounds">The element's bounds in the overlay's coordinate space.</param>
/// <param name="IsVisible">Whether the element is inside its pane's viewport.</param>
internal readonly record struct SourceLinkAnchor(Rect Bounds, bool IsVisible);

/// <summary>
/// The curved link between a cited span of the document and the field it fills (Arc document extraction): one overlay over
/// both panes, never clipped and never hit, that draws an S-shaped cubic from 4px right of the span, at its vertical
/// middle, to 7px into the field, at its middle (<c>M x1 y1 C mx y1, mx y2, x2 y2</c> with mx halfway). The line is 1.5px
/// in the tone with round caps; over it runs a 1.75px gloss whose gradient (transparent, the tone mixed toward white,
/// transparent) peaks at one point that travels from start to end once; a dot of radius 3 in the tone sits on each end,
/// ringed 1.5px in <see cref="StartDotStroke"/> (white: it sits on the paper) and <see cref="EndDotStroke"/> (the surface).
/// <para>
/// While shown, the overlay asks both ends for their bounds on every frame (a <see cref="LiveLoop"/> that runs at every
/// motion level, because following is function, not decoration) and rebuilds the path only when one moved, so the link keeps
/// up with scrolling, zooming and resizing. It stops asking when hidden. When an end is out of view, or gone, the link
/// fades out in 120ms and comes back when it is visible again.
/// </para>
/// <para>
/// Full motion: a link appears once both ends are in view; it draws itself in 220ms (a dash as long as the path), its far
/// dot grows from 0.3 and fades in over 140ms after 170ms, and the gloss crosses it in 640ms after 135ms. Showing another
/// link fades the old one out in 120ms where it was and fades the new one in over 300ms while it draws; hiding fades out in
/// 120ms. Reduced motion and Off: the line appears, moves and disappears at once. Code-driven: the dash length is the
/// length of a path that depends on the two ends, and the gloss moves gradient stops, neither of which a ControlTheme
/// transition can express. Decorative: it is not in the automation tree.
/// </para>
/// </summary>
internal sealed class SourceLinkOverlay : Control
{
    /// <summary>The gap between the span's right edge and the start of the line.</summary>
    internal const double StartGap = 4;

    /// <summary>How far into the field the line ends.</summary>
    internal const double EndInset = 7;

    /// <summary>The line's stroke.</summary>
    internal const double LineThickness = 1.5;

    /// <summary>The gloss's stroke.</summary>
    internal const double GlossThickness = 1.75;

    /// <summary>The radius of an end dot.</summary>
    internal const double DotRadius = 3;

    /// <summary>The ring around an end dot.</summary>
    internal const double DotRingThickness = 1.5;

    /// <summary>The scale the far dot grows from.</summary>
    internal const double FarDotFrom = 0.3;

    /// <summary>Where the gloss's peak starts and ends, as a share of the line (the peak is 0.2 wide each side).</summary>
    internal const double GlossFrom = -0.2;

    /// <inheritdoc cref="GlossFrom"/>
    internal const double GlossTo = 1.2;

    /// <summary>The half width of the gloss's peak, as a share of the line.</summary>
    internal const double GlossSpread = 0.2;

    internal static readonly TimeSpan DrawDuration = TimeSpan.FromMilliseconds(220);
    internal static readonly TimeSpan FarDelay = TimeSpan.FromMilliseconds(170);
    internal static readonly TimeSpan FarDuration = TimeSpan.FromMilliseconds(140);
    internal static readonly TimeSpan GlossDelay = TimeSpan.FromMilliseconds(135);
    internal static readonly TimeSpan GlossDuration = TimeSpan.FromMilliseconds(640);
    internal static readonly TimeSpan SwitchInDuration = TimeSpan.FromMilliseconds(300);
    internal static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(120);

    /// <summary>The new link's fade when it replaces another (Arc: cubic-bezier(.25, .1, .35, 1)).</summary>
    private static readonly Easing s_switchIn = new SplineEasing(0.25, 0.1, 0.35, 1);

    /// <summary>The gloss's travel: a gentle in-out (sine).</summary>
    private static readonly Easing s_gloss = new SplineEasing(0.37, 0, 0.63, 1);

    /// <summary>The ring of the dot on the paper; white, because the paper is white in both themes.</summary>
    public static readonly StyledProperty<IBrush?> StartDotStrokeProperty =
        AvaloniaProperty.Register<SourceLinkOverlay, IBrush?>(nameof(StartDotStroke), Brushes.White);

    /// <summary>The ring of the dot on the form (the theme sets the surface color).</summary>
    public static readonly StyledProperty<IBrush?> EndDotStrokeProperty =
        AvaloniaProperty.Register<SourceLinkOverlay, IBrush?>(nameof(EndDotStroke));

    private readonly LiveLoop _loop;
    private SourceLink? _current;
    private SourceLink? _leaving;
    private Pen? _startRing;
    private Pen? _endRing;
    private int _trackCount;

    static SourceLinkOverlay()
    {
        AffectsRender<SourceLinkOverlay>(StartDotStrokeProperty, EndDotStrokeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SourceLinkOverlay>(false);
        FocusableProperty.OverrideDefaultValue<SourceLinkOverlay>(false);
        ClipToBoundsProperty.OverrideDefaultValue<SourceLinkOverlay>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<SourceLinkOverlay>(AccessibilityView.Raw);
    }

    public SourceLinkOverlay()
    {
        // Following the ends is function: it runs at every motion level and wherever the overlay is (it covers the body).
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
    }

    /// <inheritdoc cref="StartDotStrokeProperty"/>
    public IBrush? StartDotStroke { get => GetValue(StartDotStrokeProperty); set => SetValue(StartDotStrokeProperty, value); }

    /// <inheritdoc cref="EndDotStrokeProperty"/>
    public IBrush? EndDotStroke { get => GetValue(EndDotStrokeProperty); set => SetValue(EndDotStrokeProperty, value); }

    /// <summary>Whether a link is shown (it may still be waiting for both ends to come into view).</summary>
    public bool IsShown => _current is not null;

    /// <summary>Whether the overlay is asking for the ends' bounds every frame (only while a link is shown).</summary>
    internal bool IsFollowing => _loop.IsActive;

    /// <summary>How many times the ends were asked for their bounds (tests: no work while hidden).</summary>
    internal int TrackCount => _trackCount;

    /// <summary>The shown link (tests).</summary>
    internal SourceLink? Current => _current;

    /// <summary>The link fading out after a switch or a hide (tests).</summary>
    internal SourceLink? Leaving => _leaving;

    /// <summary>
    /// Shows the link between two ends, replacing the one shown. The delegates are asked every frame while the link is shown
    /// and return null when that end does not exist; showing the same two delegates with the same tone again keeps the link.
    /// </summary>
    /// <param name="start">The cited span's bounds in this overlay's coordinates.</param>
    /// <param name="end">The field's (or row's) bounds in this overlay's coordinates.</param>
    /// <param name="tone">The group's color.</param>
    public void Show(Func<SourceLinkAnchor?> start, Func<SourceLinkAnchor?> end, Color tone)
    {
        if (_current is { } shown && ReferenceEquals(shown.Start, start) && ReferenceEquals(shown.End, end) && shown.Tone == tone)
        {
            return;
        }
        var full = ArcMotion.IsFull(this);
        // A link that is on screen, or still fading out, is being replaced: the new one fades in.
        var replacing = _current is { HasAppeared: true } || _leaving is not null;
        Retire(full);
        var link = new SourceLink(this, start, end, tone, GlossColor(tone)) { FadesIn = replacing && full };
        _current = link;
        Track(link);
        _loop.Start();
        InvalidateVisual();
    }

    /// <summary>Hides the link: it fades out in 120ms at full motion, at once otherwise.</summary>
    public void Hide()
    {
        if (_current is null)
        {
            return;
        }
        Retire(ArcMotion.IsFull(this));
        _loop.Stop();
        InvalidateVisual();
    }

    /// <summary>Moves the shown link out: it fades where it is (full motion) or goes at once; a link that never appeared just goes.</summary>
    private void Retire(bool fade)
    {
        var link = _current;
        _current = null;
        if (link is null)
        {
            return;
        }
        DropLeaving();
        if (!fade || !link.HasAppeared || link.Alpha <= 0.001)
        {
            link.Dispose();
            return;
        }
        _leaving = link;
        _ = FadeOut(link);
    }

    private async Task FadeOut(SourceLink link)
    {
        await link.Opacity.AnimateTo(0, MotionSpec.Tween(FadeDuration, ArcEasings.Standard));
        if (ReferenceEquals(_leaving, link))
        {
            DropLeaving();
            InvalidateVisual();
        }
    }

    private void DropLeaving()
    {
        _leaving?.Dispose();
        _leaving = null;
    }

    private bool Step(double dt)
    {
        if (_current is not { } link)
        {
            return false;
        }
        if (Track(link))
        {
            InvalidateVisual();
        }
        return true;
    }

    /// <summary>Asks both ends where they are; rebuilds the path when one moved and starts or fades the link. Returns whether anything changed.</summary>
    private bool Track(SourceLink link)
    {
        _trackCount++;
        var start = link.Start();
        var end = link.End();
        var changed = false;
        if (start is { } s && end is { } e)
        {
            changed = link.MoveTo(s.Bounds, e.Bounds);
        }
        var visible = start is { IsVisible: true } && end is { IsVisible: true } && link.HasGeometry;
        if (!link.HasAppeared)
        {
            if (visible)
            {
                link.Appear(ArcMotion.IsFull(this));
                changed = true;
            }
            return changed;
        }
        if (visible != link.IsInView)
        {
            link.IsInView = visible;
            var target = visible ? 1.0 : 0.0;
            if (ArcMotion.IsFull(this))
            {
                _ = link.InView.AnimateTo(target, MotionSpec.Tween(FadeDuration, ArcEasings.Standard));
            }
            else
            {
                link.InView.Jump(target);
            }
            changed = true;
        }
        return changed;
    }

    /// <summary>The gloss's color: the tone mixed toward white until it is about as light as Arc's (L 0.91 light, 0.94 dark).</summary>
    private Color GlossColor(Color tone)
    {
        var target = ActualThemeVariant == ThemeVariant.Dark ? 0.94 : 0.91;
        var (l, _, _) = Oklch.FromColor(tone);
        var t = l >= target ? 0 : (target - l) / (1 - l);
        return Oklch.MixOklab(tone, Colors.White, t);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartDotStrokeProperty)
        {
            _startRing = StartDotStroke is { } brush ? new Pen(brush, DotRingThickness) : null;
        }
        else if (change.Property == EndDotStrokeProperty)
        {
            _endRing = EndDotStroke is { } brush ? new Pen(brush, DotRingThickness) : null;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A link fading out has nowhere to go; a shown link stays and picks up again when the overlay is back.
        DropLeaving();
    }

    /// <summary>Called by a link's motion values on every step.</summary>
    internal void OnLinkChanged() => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        _startRing ??= StartDotStroke is { } start ? new Pen(start, DotRingThickness) : null;
        _endRing ??= EndDotStroke is { } end ? new Pen(end, DotRingThickness) : null;
        if (_leaving is { } leaving)
        {
            leaving.Paint(context, _startRing, _endRing);
        }
        if (_current is { } current)
        {
            current.Paint(context, _startRing, _endRing);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OverlayPeer(this);

    /// <summary>Decorative: neither content nor control.</summary>
    private sealed class OverlayPeer(SourceLinkOverlay owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }

    /// <summary>The control points of the link from <paramref name="from"/> to <paramref name="to"/>: horizontal tangents halfway across.</summary>
    internal static (Point C1, Point C2) ControlPoints(Point from, Point to)
    {
        var mx = (from.X + to.X) / 2;
        return (new Point(mx, from.Y), new Point(mx, to.Y));
    }

    /// <summary>Where a link starts on a span and ends on a field.</summary>
    internal static (Point From, Point To) Endpoints(Rect span, Rect field) =>
        (new Point(span.Right + StartGap, span.Y + span.Height / 2), new Point(field.X + EndInset, field.Y + field.Height / 2));

    /// <summary>The length of a cubic Bézier, summed over short chords (exact to well under a pixel for these gentle curves).</summary>
    internal static double CubicLength(Point p0, Point p1, Point p2, Point p3, int segments = 64)
    {
        var length = 0.0;
        var previous = p0;
        for (var i = 1; i <= segments; i++)
        {
            var t = (double)i / segments;
            var u = 1 - t;
            var a = u * u * u;
            var b = 3 * u * u * t;
            var c = 3 * u * t * t;
            var d = t * t * t;
            var point = new Point(a * p0.X + b * p1.X + c * p2.X + d * p3.X, a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y);
            length += Point.Distance(previous, point);
            previous = point;
        }
        return length;
    }

    /// <summary>
    /// One link: its ends, its path and the pens that draw it, and the values that animate it. Every pen, brush and dash style
    /// belongs to the link and is changed in place, so a frame of drawing, gloss or following allocates nothing but a new path
    /// when an end moved.
    /// </summary>
    internal sealed class SourceLink : IDisposable
    {
        private readonly SourceLinkOverlay _owner;
        private readonly IBrush _fill;
        private readonly DashStyle _lineDash = new(new double[] { 0, 0 }, 0);
        private readonly DashStyle _glossDash = new(new double[] { 0, 0 }, 0);
        private readonly Pen _line;
        private readonly Pen _lineDrawing;
        private readonly Pen _gloss;
        private readonly Pen _glossDrawing;
        private readonly LinearGradientBrush _glossBrush;
        private readonly GradientStop _glossLead;
        private readonly GradientStop _glossPeak;
        private readonly GradientStop _glossTail;
        private Rect _startBounds;
        private Rect _endBounds;

        public SourceLink(SourceLinkOverlay owner, Func<SourceLinkAnchor?> start, Func<SourceLinkAnchor?> end, Color tone, Color gloss)
        {
            _owner = owner;
            Start = start;
            End = end;
            Tone = tone;
            GlossColorValue = gloss;
            var toneBrush = new SolidColorBrush(tone);
            _fill = toneBrush;
            _line = new Pen(toneBrush, LineThickness, lineCap: PenLineCap.Round);
            _lineDrawing = new Pen(toneBrush, LineThickness, _lineDash, PenLineCap.Round);
            var clear = Color.FromArgb(0, gloss.R, gloss.G, gloss.B);
            _glossLead = new GradientStop(clear, 0);
            _glossPeak = new GradientStop(gloss, 0);
            _glossTail = new GradientStop(clear, 0);
            _glossBrush = new LinearGradientBrush
            {
                GradientStops = { _glossLead, _glossPeak, _glossTail }
            };
            _gloss = new Pen(_glossBrush, GlossThickness, lineCap: PenLineCap.Round);
            _glossDrawing = new Pen(_glossBrush, GlossThickness, _glossDash, PenLineCap.Round);
            Draw = Value(1);
            Far = Value(1);
            Gloss = Value(GlossTo);
            Opacity = Value(1);
            InView = Value(1);
            Draw.Changed += _ => UpdateDash();
            Gloss.Changed += _ => UpdateGloss();
        }

        private MotionValue<double> Value(double initial)
        {
            var value = new MotionValue<double>(_owner, initial, MotionVectors.Double);
            value.Changed += _ => _owner.OnLinkChanged();
            return value;
        }

        public Func<SourceLinkAnchor?> Start { get; }

        public Func<SourceLinkAnchor?> End { get; }

        public Color Tone { get; }

        /// <summary>The color at the gloss's peak.</summary>
        public Color GlossColorValue { get; }

        /// <summary>How much of the line is drawn (0 to 1).</summary>
        public MotionValue<double> Draw { get; }

        /// <summary>The far dot's entrance (0 to 1: scale from 0.3 and opacity).</summary>
        public MotionValue<double> Far { get; }

        /// <summary>Where the gloss's peak is along the line (−0.2 to 1.2).</summary>
        public MotionValue<double> Gloss { get; }

        /// <summary>The whole link's opacity (fade in on a switch, fade out on a switch or a hide).</summary>
        public MotionValue<double> Opacity { get; }

        /// <summary>The link's opacity for its ends being in view.</summary>
        public MotionValue<double> InView { get; }

        /// <summary>Whether the link has started to show (both ends were in view once).</summary>
        public bool HasAppeared { get; private set; }

        /// <summary>Whether a new link fades in (it replaced another one at full motion).</summary>
        public bool FadesIn { get; init; }

        /// <summary>Whether both ends were in view at the last frame.</summary>
        public bool IsInView { get; set; } = true;

        /// <summary>Whether the path is known (both ends reported bounds once).</summary>
        public bool HasGeometry => Path is not null;

        /// <summary>The start of the line.</summary>
        public Point From { get; private set; }

        /// <summary>The end of the line.</summary>
        public Point To { get; private set; }

        /// <summary>The path.</summary>
        public StreamGeometry? Path { get; private set; }

        /// <summary>The path's length.</summary>
        public double Length { get; private set; }

        /// <summary>How opaque the link is drawn now.</summary>
        public double Alpha => Math.Clamp(Opacity.Value, 0, 1) * Math.Clamp(InView.Value, 0, 1);

        /// <summary>The ends' bounds changed: a new path, length, dash and gradient line. Returns whether anything moved.</summary>
        public bool MoveTo(Rect start, Rect end)
        {
            if (Path is not null && start == _startBounds && end == _endBounds)
            {
                return false;
            }
            _startBounds = start;
            _endBounds = end;
            var (from, to) = Endpoints(start, end);
            if (Path is not null && from == From && to == To)
            {
                return false;
            }
            From = from;
            To = to;
            var (c1, c2) = ControlPoints(from, to);
            var path = new StreamGeometry();
            using (var context = path.Open())
            {
                context.BeginFigure(from, false);
                context.CubicBezierTo(c1, c2, to);
                context.EndFigure(false);
            }
            Path = path;
            Length = CubicLength(from, c1, c2, to);
            _glossBrush.StartPoint = new RelativePoint(from, RelativeUnit.Absolute);
            _glossBrush.EndPoint = new RelativePoint(to, RelativeUnit.Absolute);
            UpdateDash();
            return true;
        }

        /// <summary>Both ends are in view for the first time: the link starts to show.</summary>
        public void Appear(bool full)
        {
            HasAppeared = true;
            IsInView = true;
            InView.Jump(1);
            if (!full)
            {
                Draw.Jump(1);
                Far.Jump(1);
                Gloss.Jump(GlossTo);
                Opacity.Jump(1);
                return;
            }
            Draw.Jump(0);
            Far.Jump(0);
            Gloss.Jump(GlossFrom);
            _ = Draw.AnimateTo(1, MotionSpec.Tween(DrawDuration, ArcEasings.Standard));
            _ = Far.AnimateTo(1, MotionSpec.Tween(FarDuration, ArcEasings.Standard, FarDelay));
            _ = Gloss.AnimateTo(GlossTo, MotionSpec.Tween(GlossDuration, s_gloss, GlossDelay));
            if (FadesIn)
            {
                Opacity.Jump(0);
                _ = Opacity.AnimateTo(1, MotionSpec.Tween(SwitchInDuration, s_switchIn));
            }
            else
            {
                Opacity.Jump(1);
            }
        }

        /// <summary>
        /// The drawn part as a dash: one dash as long as the path (plus a pixel, so rounding never leaves a gap) shifted back by
        /// the part not yet drawn. Dash lengths are in units of the pen's thickness.
        /// </summary>
        private void UpdateDash()
        {
            var length = Length + 1;
            var hidden = length * (1 - Math.Clamp(Draw.Value, 0, 1));
            SetDash(_lineDash, length / LineThickness, hidden / LineThickness);
            SetDash(_glossDash, length / GlossThickness, hidden / GlossThickness);
        }

        private static void SetDash(DashStyle dash, double length, double offset)
        {
            // A new list only when the path's length changed (the path itself is new then); the offset moves every frame.
            if (dash.Dashes is not { Count: 2 } dashes || dashes[0] != length)
            {
                dash.Dashes = new AvaloniaList<double> { length, length };
            }
            dash.Offset = offset;
        }

        /// <summary>The gloss's three stops around the peak, kept inside the line.</summary>
        private void UpdateGloss()
        {
            var peak = Gloss.Value;
            _glossLead.Offset = Math.Clamp(peak - GlossSpread, 0, 1);
            _glossPeak.Offset = Math.Clamp(peak, 0, 1);
            _glossTail.Offset = Math.Clamp(peak + GlossSpread, 0, 1);
        }

        /// <summary>The gloss's stops (tests).</summary>
        public (double Lead, double Peak, double Tail) GlossStops => (_glossLead.Offset, _glossPeak.Offset, _glossTail.Offset);

        /// <summary>The line's dash: length and offset in pixels (tests).</summary>
        public (double Length, double Offset) LineDash => (_lineDash.Dashes![0] * LineThickness, _lineDash.Offset * LineThickness);

        /// <summary>Draws the link: the line and its gloss as far as drawn, the start dot, then the far dot as it grows in.</summary>
        public void Paint(DrawingContext context, Pen? startRing, Pen? endRing)
        {
            var path = Path;
            if (path is null || !HasAppeared)
            {
                return;
            }
            var alpha = Alpha;
            if (alpha <= 0.001)
            {
                return;
            }
            using (context.PushOpacity(alpha))
            {
                var drawn = Draw.Value;
                if (drawn > 0.0001)
                {
                    var done = drawn >= 1;
                    context.DrawGeometry(null, done ? _line : _lineDrawing, path);
                    var gloss = Gloss.Value;
                    if (gloss > GlossFrom && gloss < GlossTo && Length > 0.5)
                    {
                        context.DrawGeometry(null, done ? _gloss : _glossDrawing, path);
                    }
                }
                context.DrawEllipse(_fill, startRing, From, DotRadius, DotRadius);
                var far = Math.Clamp(Far.Value, 0, 1);
                if (far > 0.001)
                {
                    var scale = FarDotFrom + (1 - FarDotFrom) * far;
                    var to = To;
                    using (context.PushTransform(Matrix.CreateTranslation(-to.X, -to.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(to.X, to.Y)))
                    using (context.PushOpacity(far))
                    {
                        context.DrawEllipse(_fill, endRing, to, DotRadius, DotRadius);
                    }
                }
            }
        }

        public void Dispose()
        {
            Draw.Dispose();
            Far.Dispose();
            Gloss.Dispose();
            Opacity.Dispose();
            InView.Dispose();
        }
    }
}
