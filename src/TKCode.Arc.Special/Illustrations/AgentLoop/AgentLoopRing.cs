using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The agent loop's drawn layer, one control: the pins under the lower stations, the track, the direction chevrons, the
/// done line, the head's gradient trail, the three nodes and the head. The head and its trail follow the story clock
/// (they must pause, step and scale with the speed); the one-shot transitions (node states 240ms, pin draw 180ms, done
/// line 760ms, head fade 240ms) follow the explainer's wall clock, as measured: they finish while paused and keep their
/// pace at any speed. Nothing here allocates per frame: pens, brushes and the paths are cached, and the trail mutates one
/// gradient brush whose ends follow the tail and the head.
/// </summary>
internal sealed class AgentLoopRing : Control
{
    internal static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<AgentLoopRing, IBrush?>(nameof(AccentInk));

    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<AgentLoopRing, IBrush?>(nameof(SurfaceInk));

    internal static readonly StyledProperty<IBrush?> TrackInkProperty =
        AvaloniaProperty.Register<AgentLoopRing, IBrush?>(nameof(TrackInk));

    internal static readonly StyledProperty<IBrush?> ChevronInkProperty =
        AvaloniaProperty.Register<AgentLoopRing, IBrush?>(nameof(ChevronInk));

    internal static readonly StyledProperty<IBrush?> PinInkProperty =
        AvaloniaProperty.Register<AgentLoopRing, IBrush?>(nameof(PinInk));

    /// <summary>How far the trail's tail may reach behind the start (the path runs from −360° to 360°).</summary>
    private const double TurnStart = -360;

    private struct Pin
    {
        public bool Has;
        public ExplainerValue Draw;
        public ExplainerValue Presence;
        public ExplainerValue Lit;
    }

    private readonly ExplainerValue[] _active = new ExplainerValue[3];
    private readonly ExplainerValue[] _visited = new ExplainerValue[3];
    private readonly Pin[] _pins = new Pin[2];
    private readonly ExplainerPath _turns = new();
    private ExplainerValue _head;
    private ExplainerValue _done;
    private ExplainerValue _doneAlpha;
    private AgentLoopGeometry? _geometry;
    private AgentLoopTimeline? _timeline;
    private StreamGeometry? _chevrons;
    private double _story;
    private double _wall;
    private bool _travel = true;
    private bool _paletteDirty = true;
    private IBrush? _accent;
    private IBrush? _surface;
    private Pen? _trackPen;
    private Pen? _chevronPen;
    private Pen? _pinPen;
    private Pen? _livePinPen;
    private Pen? _accentPen;
    private Pen? _haloPen;
    private Pen? _headPen;
    private Pen? _donePen;
    private Pen? _trailPen;
    private LinearGradientBrush? _trailBrush;

    static AgentLoopRing()
    {
        AffectsRender<AgentLoopRing>(AccentInkProperty, SurfaceInkProperty, TrackInkProperty, ChevronInkProperty, PinInkProperty);
    }

    public AgentLoopRing()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        for (var i = 0; i < _pins.Length; i++)
        {
            _pins[i].Lit = new ExplainerValue(0.32);
        }
    }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    public IBrush? TrackInk { get => GetValue(TrackInkProperty); set => SetValue(TrackInkProperty, value); }

    public IBrush? ChevronInk { get => GetValue(ChevronInkProperty); set => SetValue(ChevronInkProperty, value); }

    public IBrush? PinInk { get => GetValue(PinInkProperty); set => SetValue(PinInkProperty, value); }

    /// <summary>On a single lane, the station whose pin shows (the latest chip's); null shows both plain pins.</summary>
    public AgentLoopStation? PinStation { get; set; }

    /// <summary>The geometry drawn (tests read it).</summary>
    internal AgentLoopGeometry? Geometry => _geometry;

    /// <summary>The head's clock angle and the trail's tail angle at the last update (tests read them).</summary>
    internal (double Head, double Tail, double Opacity) HeadPose
    {
        get
        {
            var (head, tail) = Angles();
            return (head, tail, Math.Clamp(_head.Value(_wall), 0, 1));
        }
    }

    /// <summary>The done line's drawn fraction and opacity (tests read them).</summary>
    internal (double Progress, double Opacity) DoneLine => (_done.Value(_wall), _doneAlpha.Value(_wall));

    /// <summary>A lower station's live pin: drawn fraction and opacity (tests read them).</summary>
    internal (bool Has, double Drawn, double Opacity) PinOf(AgentLoopStation station)
    {
        ref var pin = ref _pins[PinIndex(station)];
        return (pin.Has, pin.Draw.Value(_wall), pin.Presence.Value(_wall) * pin.Lit.Value(_wall));
    }

    /// <summary>A node's active and visited amounts (tests read them).</summary>
    internal (double Active, double Visited) NodeOf(AgentLoopStation station)
        => (_active[(int)station].Value(_wall), _visited[(int)station].Value(_wall));

    public void SetGeometry(AgentLoopGeometry geometry)
    {
        if (_geometry is { } old && old.Radius == geometry.Radius && old.LowerAngle == geometry.LowerAngle &&
            old.Center == geometry.Center && old.Width == geometry.Width && old.IsCompact == geometry.IsCompact)
        {
            return;
        }
        _geometry = geometry;
        _turns.Clear().ArcTo(geometry.Center, geometry.Radius, TurnStart, 720, 1.5);
        _chevrons = BuildChevrons(geometry);
        InvalidateVisual();
    }

    public void SetTimeline(AgentLoopTimeline timeline)
    {
        _timeline = timeline;
        InvalidateVisual();
    }

    /// <summary>Moves the nodes to the scene's station states (240ms, or at once).</summary>
    public void SetNodes(AgentLoopScene scene, double wall, bool animate)
    {
        for (var i = 0; i < 3; i++)
        {
            var state = scene[(AgentLoopStation)i];
            To(ref _active[i], state == AgentLoopNodeState.Active ? 1 : 0, wall, 0.24, animate);
            To(ref _visited[i], state == AgentLoopNodeState.Visited ? 1 : 0, wall, 0.24, animate);
        }
        var active = scene.ActiveStation;
        for (var i = 0; i < _pins.Length; i++)
        {
            var lit = active == (i == 0 ? AgentLoopStation.Act : AgentLoopStation.Verify) ? 1 : 0.32;
            To(ref _pins[i].Lit, lit, wall, 0.24, animate);
        }
        Touch(wall);
    }

    /// <summary>Shows or hides the head (240ms fade, or at once).</summary>
    public void SetHead(bool visible, double wall, bool animate)
    {
        To(ref _head, visible ? 1 : 0, wall, 0.24, animate);
        Touch(wall);
    }

    /// <summary>Draws the live pin under <paramref name="station"/> again (180ms, or at once) for a chip that dropped.</summary>
    public void DrawPin(AgentLoopStation station, double wall, bool animate)
    {
        ref var pin = ref _pins[PinIndex(station)];
        pin.Has = true;
        pin.Presence.Jump(1);
        if (animate)
        {
            pin.Draw.Jump(0);
            pin.Draw.Tween(1, wall, 0.18, ArcEasings.Standard);
        }
        else
        {
            pin.Draw.Jump(1);
        }
        Touch(wall);
    }

    /// <summary>Shows a live pin as already drawn (a new template or layout showing the same moment).</summary>
    public void SetPinShown(AgentLoopStation station, bool shown, double wall)
    {
        ref var pin = ref _pins[PinIndex(station)];
        pin.Has = shown;
        pin.Draw.Jump(shown ? 1 : 0);
        pin.Presence.Jump(shown ? 1 : 0);
        Touch(wall);
    }

    /// <summary>Fades the live pins out (100ms, or at once): a restart.</summary>
    public void ClearPins(double wall, bool animate)
    {
        for (var i = 0; i < _pins.Length; i++)
        {
            To(ref _pins[i].Presence, 0, wall, 0.1, animate);
        }
        Touch(wall);
    }

    /// <summary>Draws the done line in clockwise from the top (760ms) or fades it out (160ms).</summary>
    public void SetDone(bool done, double wall, bool animate)
    {
        if (done)
        {
            if (animate)
            {
                _done.Jump(0);
                _done.Tween(1, wall, 0.76, ArcEasings.InOut);
                _doneAlpha.Tween(1, wall, 0.16, ArcEasings.Standard);
            }
            else
            {
                _done.Jump(1);
                _doneAlpha.Jump(1);
            }
        }
        else
        {
            To(ref _doneAlpha, 0, wall, 0.16, animate);
        }
        Touch(wall);
    }

    /// <summary>
    /// Moves to story time <paramref name="story"/> and wall time <paramref name="wall"/>; redraws only when the head
    /// moved or a transition runs (a resting head costs no frames).
    /// </summary>
    public void Update(double story, double wall, bool travel)
    {
        var before = AnglesAt(_story, _travel);
        var after = AnglesAt(story, travel);
        var changed = before != after || IsAnimating(_wall) || IsAnimating(wall);
        _story = story;
        _wall = Math.Max(_wall, wall);
        _travel = travel;
        if (changed)
        {
            InvalidateVisual();
        }
    }

    private bool IsAnimating(double wall)
    {
        if (_head.IsMoving(wall) || _done.IsMoving(wall) || _doneAlpha.IsMoving(wall))
        {
            return true;
        }
        for (var i = 0; i < 3; i++)
        {
            if (_active[i].IsMoving(wall) || _visited[i].IsMoving(wall))
            {
                return true;
            }
        }
        for (var i = 0; i < _pins.Length; i++)
        {
            if (_pins[i].Draw.IsMoving(wall) || _pins[i].Presence.IsMoving(wall) || _pins[i].Lit.IsMoving(wall))
            {
                return true;
            }
        }
        return false;
    }

    private void Touch(double wall)
    {
        _wall = Math.Max(_wall, wall);
        InvalidateVisual();
    }

    private static void To(ref ExplainerValue value, double target, double wall, double seconds, bool animate)
    {
        if (!animate)
        {
            value.Jump(target);
            return;
        }
        if (value.Target == target)
        {
            return;
        }
        value.Tween(target, wall, seconds, ArcEasings.Standard);
    }

    private static int PinIndex(AgentLoopStation station) => station == AgentLoopStation.Verify ? 1 : 0;

    /// <summary>The head's and the tail's clock angles now (equal when the head rests or travel is off).</summary>
    private (double Head, double Tail) Angles() => AnglesAt(_story, _travel);

    private (double Head, double Tail) AnglesAt(double story, bool travel)
    {
        if (_geometry is not { } geometry || _timeline is not { } timeline)
        {
            return (0, 0);
        }
        var pose = timeline.HeadAt(story);
        var from = geometry.AngleOf(pose.From);
        if (!pose.IsTravelling)
        {
            return (from, from);
        }
        if (!travel)
        {
            // Reduced motion: the head waits at the station it leaves and jumps when the trip ends.
            return (from, from);
        }
        var to = geometry.AngleOf(pose.To);
        if (to <= from)
        {
            to += 360;
        }
        var sweep = to - from;
        var head = from + sweep * AgentLoopTimeline.EaseInOutCubic(pose.Progress);
        var tail = head - AgentLoopTimeline.TrailGap(sweep, pose.Progress);
        return (head, tail);
    }

    private double DistanceAt(double degrees) => (degrees - TurnStart) / 720 * _turns.Length;

    private static StreamGeometry BuildChevrons(AgentLoopGeometry geometry)
    {
        var chevrons = new StreamGeometry();
        using (var context = chevrons.Open())
        {
            var (a, b, c) = geometry.ChevronAngles;
            foreach (var angle in new[] { a, b, c })
            {
                var at = ExplainerPath.OnCircle(geometry.Center, geometry.Radius, angle);
                var radians = angle * Math.PI / 180;
                var cos = Math.Cos(radians);
                var sin = Math.Sin(radians);
                // A V 4.5 wide and 8 high pointing along the travel (clockwise tangent), arms ±4, tip at +2.25.
                Point Rotate(double x, double y) => new(at.X + x * cos - y * sin, at.Y + x * sin + y * cos);
                context.BeginFigure(Rotate(-2.25, -4), false);
                context.LineTo(Rotate(2.25, 0));
                context.LineTo(Rotate(-2.25, 4));
                context.EndFigure(false);
            }
        }
        return chevrons;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentInkProperty || change.Property == SurfaceInkProperty || change.Property == TrackInkProperty ||
            change.Property == ChevronInkProperty || change.Property == PinInkProperty)
        {
            _paletteDirty = true;
        }
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        var accent = (AccentInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0x1F, 0x7D, 0xCF);
        var surface = (SurfaceInk as ISolidColorBrush)?.Color ?? Colors.White;
        var track = (TrackInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0xC8, 0xC8, 0xC8);
        var chevron = (ChevronInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0x8A, 0x8A, 0x8A);
        var pin = (PinInk as ISolidColorBrush)?.Color ?? Color.FromRgb(0xE4, 0xE4, 0xE4);
        _accent = new ImmutableSolidColorBrush(accent);
        _surface = new ImmutableSolidColorBrush(surface);
        _trackPen = new Pen(new ImmutableSolidColorBrush(track), 1.5);
        _chevronPen = new Pen(new ImmutableSolidColorBrush(chevron), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _pinPen = new Pen(new ImmutableSolidColorBrush(pin), 1);
        _livePinPen = new Pen(_accent, 1);
        _accentPen = new Pen(_accent, 1.5);
        _haloPen = new Pen(_accent, 1.5);
        _headPen = new Pen(_surface, 2.5);
        _donePen = new Pen(_accent, 1.5, new DashStyle());
        _trailBrush = new LinearGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb((byte)Math.Round(0.7 * 255), accent.R, accent.G, accent.B), 1)
            }
        };
        _trailPen = new Pen(_trailBrush, 3, new DashStyle(), PenLineCap.Round);
    }

    public override void Render(DrawingContext context)
    {
        if (_geometry is not { } geometry)
        {
            return;
        }
        EnsurePalette();
        var wall = _wall;

        // Pins: a plain 1px line under each lower station (only the current chip's on a single lane), and a live accent
        // line drawn down over it for each chip that dropped.
        for (var i = 0; i < _pins.Length; i++)
        {
            var station = i == 0 ? AgentLoopStation.Act : AgentLoopStation.Verify;
            if (geometry.IsCompact && PinStation is { } only && only != station)
            {
                continue;
            }
            var (from, to) = geometry.PinOf(station);
            context.DrawLine(_pinPen!, from, to);
            ref var pin = ref _pins[i];
            if (!pin.Has || geometry.IsCompact && PinStation is null)
            {
                continue;
            }
            var drawn = Math.Clamp(pin.Draw.Value(wall), 0, 1);
            var alpha = Math.Clamp(pin.Presence.Value(wall), 0, 1) * Math.Clamp(pin.Lit.Value(wall), 0, 1);
            if (drawn <= 0.001 || alpha <= 0.001)
            {
                continue;
            }
            using (context.PushOpacity(alpha))
            {
                context.DrawLine(_livePinPen!, from, new Point(from.X, from.Y + (to.Y - from.Y) * drawn));
            }
        }

        context.DrawEllipse(null, _trackPen!, geometry.Center, geometry.Radius, geometry.Radius);
        if (_chevrons is not null)
        {
            context.DrawGeometry(null, _chevronPen!, _chevrons);
        }

        // Done line: the whole ring in the accent, drawn clockwise from the top.
        var doneAlpha = Math.Clamp(_doneAlpha.Value(wall), 0, 1);
        var done = Math.Clamp(_done.Value(wall), 0, 1);
        if (doneAlpha > 0.001 && done > 0.001)
        {
            if (done >= 0.9999)
            {
                using (context.PushOpacity(doneAlpha))
                {
                    context.DrawEllipse(null, _accentPen!, geometry.Center, geometry.Radius, geometry.Radius);
                }
            }
            else if (_turns.ShowPart(_donePen!, DistanceAt(0), DistanceAt(360 * done)))
            {
                using (context.PushOpacity(doneAlpha))
                {
                    context.DrawGeometry(null, _donePen!, _turns.Geometry);
                }
            }
        }

        var headAlpha = Math.Clamp(_head.Value(wall), 0, 1);
        var (headAngle, tailAngle) = Angles();
        var headPoint = ExplainerPath.OnCircle(geometry.Center, geometry.Radius, headAngle);

        // Trail: an arc along the ring from the tail to the head, its gradient running from clear at the tail to 70% at
        // the head (the brush's ends follow both every frame).
        if (headAlpha > 0.001 && headAngle - tailAngle > 0.2 && _trailBrush is not null)
        {
            var tailPoint = ExplainerPath.OnCircle(geometry.Center, geometry.Radius, tailAngle);
            _trailBrush.StartPoint = new RelativePoint(tailPoint, RelativeUnit.Absolute);
            _trailBrush.EndPoint = new RelativePoint(headPoint, RelativeUnit.Absolute);
            if (_turns.ShowPart(_trailPen!, DistanceAt(tailAngle), DistanceAt(headAngle)))
            {
                using (context.PushOpacity(headAlpha))
                {
                    context.DrawGeometry(null, _trailPen!, _turns.Geometry);
                }
            }
        }

        // Nodes: idle (surface fill, strong border), visited (accent outline), active (accent fill and a settling ring).
        for (var i = 0; i < 3; i++)
        {
            var station = (AgentLoopStation)i;
            var at = geometry.NodeOf(station);
            var active = Math.Clamp(_active[i].Value(wall), 0, 1);
            var visited = Math.Clamp(_visited[i].Value(wall), 0, 1);
            context.DrawEllipse(_surface, _trackPen!, at, 5, 5);
            if (visited > 0.001)
            {
                using (context.PushOpacity(visited))
                {
                    context.DrawEllipse(_surface, _accentPen!, at, 5, 5);
                }
            }
            if (active > 0.001)
            {
                using (context.PushOpacity(active))
                {
                    context.DrawEllipse(_accent, _accentPen!, at, 5, 5);
                }
                var ring = 10.5 * (0.6 + 0.4 * active);
                using (context.PushOpacity(0.3 * active))
                {
                    context.DrawEllipse(null, _haloPen!, at, ring, ring);
                }
            }
        }

        if (headAlpha > 0.001)
        {
            using (context.PushOpacity(headAlpha))
            {
                context.DrawEllipse(_accent, _headPen!, headPoint, 5, 5);
            }
        }
    }
}
