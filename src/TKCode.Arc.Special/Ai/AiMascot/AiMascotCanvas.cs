using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The mascot drawn in one control: the ground (a soft contact shadow and the written line), the companion dot drawn
/// twice (behind and in front of the body), the pebble body with its quiet form shade, the pill eyes, the happy squint,
/// the worried brows and the done sparkle. Geometry is static and placed with transforms; brushes and pens are cached
/// and rebuilt only when a color changes, so a frame allocates nothing.
/// <para>
/// Why code-driven motion: every state is a continuous function of a clock (an orbit, a writing cycle, breathing, blinks
/// and glances) and the drawn frame is a weighted sum of the states' frames, which a ControlTheme transition cannot
/// express. The state weights are <see cref="MotionValue{T}"/>s on a critically damped spring with a 420ms visual
/// duration, so a status change blends from the frame on screen and a change in the middle of a blend keeps its
/// velocity; under reduced motion the springs jump (the state switches in place). The clock is a Core
/// <see cref="LiveLoop"/>, which pauses off screen, while hidden and below full motion and keeps its phase. The eyes
/// ride their own critically damped spring, stepped on the same clock, because their target moves every frame.
/// </para>
/// </summary>
internal sealed class AiMascotCanvas : Control
{
    public static readonly StyledProperty<IBrush?> AccentInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> AccentForegroundInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(AccentForegroundInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> BackgroundInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(BackgroundInk));
    public static readonly StyledProperty<IBrush?> SurfaceMutedInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(SurfaceMutedInk));
    public static readonly StyledProperty<IBrush?> BorderInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(BorderInk));
    public static readonly StyledProperty<IBrush?> ShadeInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(ShadeInk));
    public static readonly StyledProperty<IBrush?> GroundInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(GroundInk));
    public static readonly StyledProperty<IBrush?> LineInkProperty = AvaloniaProperty.Register<AiMascotCanvas, IBrush?>(nameof(LineInk));
    public static readonly StyledProperty<double> CompactBelowProperty = AvaloniaProperty.Register<AiMascotCanvas, double>(nameof(CompactBelow), 56);
    public static readonly StyledProperty<TimeSpan> BlendDurationProperty = AvaloniaProperty.Register<AiMascotCanvas, TimeSpan>(nameof(BlendDuration), TimeSpan.FromMilliseconds(420));

    private const int StateCount = 6;
    private const double BlinkSeconds = 0.16;

    private static readonly Geometry s_body = StreamGeometry.Parse("M50,30 C69,30 81,42 81,58 C81,74 68,82 50,82 C32,82 19,74 19,58 C19,42 31,30 50,30 Z");
    private static readonly Geometry s_squint = StreamGeometry.Parse("M-5,2 Q0,-5.5 5,2");
    private static readonly Geometry s_sparkle = StreamGeometry.Parse(
        "M0,-1 C0.1,-0.22 0.22,-0.1 1,0 C0.22,0.1 0.1,0.22 0,1 C-0.1,0.22 -0.22,0.1 -1,0 C-0.22,-0.1 -0.1,-0.22 0,-1 Z");
    private static int s_seed = 0x2F6B1D;

    private readonly LiveLoop _loop;
    private readonly MotionValue<double>[] _weights = new MotionValue<double>[StateCount];
    private readonly double[] _entered = new double[StateCount];
    private AiMascot? _owner;
    private AiMascotStatus _status;
    private bool _statusSet;

    // The clock, the ease out of the still pose and the eye spring.
    private double _t;
    private double _liveMix;
    private double _gazeX, _gazeY, _gazeVX, _gazeVY;
    private bool _gazeReady;

    // Idle glances and blinks, scheduled on the clock from a per-mascot random sequence.
    private uint _rng;
    private double _glanceX, _glanceY, _nextGlance;
    private double _blinkA = double.NegativeInfinity, _blinkB = double.NegativeInfinity, _nextBlink;

    // The pointer the eyes follow (in this control's coordinates) and the window that reports it in Page mode.
    private Point? _pointer;
    private TopLevel? _pageHost;

    // Colors and the pens built from them.
    private IImmutableSolidColorBrush? _bodyBrush, _eyeBrush, _dotBrush, _shadeBrush, _groundBrush;
    private Pen? _borderPen, _browPen, _squintPen, _linePen;
    private bool _palette;
    private bool _penCompact;
    private double _borderScale;

    static AiMascotCanvas()
    {
        AffectsRender<AiMascotCanvas>(AccentInkProperty, AccentForegroundInkProperty, ForegroundInkProperty, BackgroundInkProperty,
            SurfaceMutedInkProperty, BorderInkProperty, ShadeInkProperty, GroundInkProperty, LineInkProperty, CompactBelowProperty);
        FocusableProperty.OverrideDefaultValue<AiMascotCanvas>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<AiMascotCanvas>(AutomationControlType.Image);
    }

    public AiMascotCanvas()
    {
        _rng = (uint)Interlocked.Add(ref s_seed, 0x9E3779B) | 1u;
        for (var i = 0; i < StateCount; i++)
        {
            var value = new MotionValue<double>(this, 0, MotionVectors.Double);
            value.Changed += _ => InvalidateVisual();
            _weights[i] = value;
        }
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += _ => InvalidateVisual();
        // The motion level can also come from the theme's tokens: redraw so the still or live pose shows.
        ResourcesChanged += (_, _) => InvalidateVisual();
        _nextBlink = 1.2 + Next() * 2;
        _nextGlance = 1.0 + Next();
    }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    public IBrush? AccentForegroundInk { get => GetValue(AccentForegroundInkProperty); set => SetValue(AccentForegroundInkProperty, value); }
    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }
    public IBrush? SurfaceMutedInk { get => GetValue(SurfaceMutedInkProperty); set => SetValue(SurfaceMutedInkProperty, value); }
    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    public IBrush? ShadeInk { get => GetValue(ShadeInkProperty); set => SetValue(ShadeInkProperty, value); }
    public IBrush? GroundInk { get => GetValue(GroundInkProperty); set => SetValue(GroundInkProperty, value); }
    public IBrush? LineInk { get => GetValue(LineInkProperty); set => SetValue(LineInkProperty, value); }
    public double CompactBelow { get => GetValue(CompactBelowProperty); set => SetValue(CompactBelowProperty, value); }
    public TimeSpan BlendDuration { get => GetValue(BlendDurationProperty); set => SetValue(BlendDurationProperty, value); }

    /// <summary>The loop's clock in seconds (it only runs at full motion while the mascot is shown and not paused).</summary>
    internal double Now => _t;

    /// <summary>The ease out of the still pose (0 on the first paint and under reduced motion, 1 once live).</summary>
    internal double LiveMix => _liveMix;

    /// <summary>Whether frames are being delivered.</summary>
    internal bool IsLooping => _loop.IsRunning;

    /// <summary>Whether the loop is asked for (it may still be paused by a gate).</summary>
    internal bool WantsLoop => _loop.IsActive;

    /// <summary>The current weight of each state (0..1, sums to about 1).</summary>
    internal double WeightOf(AiMascotStatus status) => _weights[(int)status].Value;

    /// <summary>The eye spring's position (−1..1 each way).</summary>
    internal Point Gaze => new(_gazeX, _gazeY);

    /// <summary>Whether the drawing uses the compact frame.</summary>
    internal bool IsCompact => FrameFor(SizeOf()).Compact;

    /// <summary>The colors in use: body, eyes and dot.</summary>
    internal (Color Body, Color Eye, Color Dot) Colors
    {
        get
        {
            EnsurePalette();
            return (_bodyBrush!.Color, _eyeBrush!.Color, _dotBrush!.Color);
        }
    }

    /// <summary>The frame that would be drawn now.</summary>
    internal AiMascotPose CurrentPose => Compose(FrameFor(SizeOf()), out _);

    internal void Attach(AiMascot? owner)
    {
        _owner = owner;
        _palette = false;
        UpdatePointerHost();
        UpdateLoop();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Blends to <paramref name="status"/> from the frame on screen (jumps on the first call and below full motion).</summary>
    internal void ShowStatus(AiMascotStatus status)
    {
        if (_statusSet && status == _status)
        {
            return;
        }
        var first = !_statusSet;
        _statusSet = true;
        _status = status;
        _entered[(int)status] = _t;
        var spec = MotionSpec.Spring(SpringSpec.FromVisual(Math.Max(0.01, BlendDuration.TotalSeconds), 0));
        for (var i = 0; i < StateCount; i++)
        {
            var target = i == (int)status ? 1.0 : 0.0;
            if (first)
            {
                _weights[i].Jump(target);
            }
            else
            {
                _ = _weights[i].AnimateTo(target, spec);
            }
        }
        InvalidateVisual();
    }

    /// <summary>Re-reads the owner's colors.</summary>
    internal void InvalidatePalette()
    {
        _palette = false;
        InvalidateVisual();
    }

    /// <summary>Starts or stops the loop after a change to the owner (paused, tour, motion).</summary>
    internal void UpdateLoop()
    {
        if (_owner is null || _owner.IsPaused)
        {
            _loop.Stop();
            return;
        }
        // The self-playing tour keeps time at every motion level (it only advances the states then); otherwise the loop
        // is decoration and runs at full motion only.
        _loop.MinimumLevel = _owner.Status is null ? ArcMotionLevel.Off : ArcMotionLevel.Full;
        _loop.Start();
    }

    /// <summary>Re-hooks the window's pointer events when the follow mode changes.</summary>
    internal void UpdatePointerHost()
    {
        var want = _owner?.FollowPointer == AiMascotPointerFollow.Page && IsAttachedToVisualTreeSafe() ? TopLevel.GetTopLevel(this) : null;
        if (want == _pageHost)
        {
            return;
        }
        _pageHost?.RemoveHandler(PointerMovedEvent, OnPagePointerMoved);
        _pageHost = want;
        _pageHost?.AddHandler(PointerMovedEvent, OnPagePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (_owner?.FollowPointer == AiMascotPointerFollow.Never)
        {
            _pointer = null;
        }
    }

    private bool IsAttachedToVisualTreeSafe() => TopLevel.GetTopLevel(this) is not null;

    /// <summary>Advances the loop by <paramref name="dt"/> seconds (the frame callback; tests call it too).</summary>
    internal bool Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.1);
        _owner?.AdvanceTour(dt);
        if (!ArcMotion.IsFull(this))
        {
            // The tour keeps time below full motion, but the drawing holds the still pose of each state.
            _liveMix = 0;
            _gazeReady = false;
            return true;
        }
        _t += dt;
        var blend = Math.Max(0.01, BlendDuration.TotalSeconds);
        _liveMix = Math.Min(1, _liveMix + dt / blend);
        Schedule();
        var target = Compose(FrameFor(SizeOf()), out var stiffness);
        if (!_gazeReady)
        {
            _gazeX = target.GazeX;
            _gazeY = target.GazeY;
            _gazeVX = 0;
            _gazeVY = 0;
            _gazeReady = true;
        }
        else
        {
            StepSpring(ref _gazeX, ref _gazeVX, target.GazeX, stiffness, dt);
            StepSpring(ref _gazeY, ref _gazeVY, target.GazeY, stiffness, dt);
        }
        InvalidateVisual();
        return true;
    }

    // A critically damped spring, integrated in small sub-steps so a long frame stays stable.
    private static void StepSpring(ref double x, ref double v, double target, double omega, double dt)
    {
        var steps = (int)Math.Ceiling(dt / (1.0 / 240));
        var h = steps > 0 ? dt / steps : 0;
        for (var i = 0; i < steps; i++)
        {
            var a = omega * omega * (target - x) - 2 * omega * v;
            v += a * h;
            x += v * h;
        }
    }

    // Idle glances (a held target every 1.2 to 3.4s, every other one back to center) and blinks every 2.4 to 5.2s,
    // sometimes twice. Both live on the loop clock, so they pause with it.
    private void Schedule()
    {
        if (_t >= _nextGlance)
        {
            var centered = Math.Abs(_glanceX) > 0.01 || Math.Abs(_glanceY) > 0.01;
            if (centered)
            {
                _glanceX = 0;
                _glanceY = 0;
            }
            else
            {
                var angle = Next() * AiMascotPose.Tau;
                var reach = 0.55 + Next() * 0.4;
                _glanceX = Math.Cos(angle) * reach;
                _glanceY = Math.Sin(angle) * reach * 0.7;
            }
            _nextGlance = _t + 1.2 + Next() * 2.2;
        }
        if (_t >= _nextBlink)
        {
            _blinkA = _t;
            _blinkB = Next() < 0.28 ? _t + 0.26 : double.NegativeInfinity;
            _nextBlink = _t + 2.4 + Next() * 2.8;
        }
    }

    /// <summary>How open the eyes are at loop time <paramref name="t"/> for blinks starting at <paramref name="a"/> and <paramref name="b"/>.</summary>
    internal static double BlinkOpen(double t, double a, double b)
    {
        var closure = Math.Max(Closure(t - a), Closure(t - b));
        return 1 - 0.94 * closure;

        static double Closure(double dt) => dt < 0 || dt > BlinkSeconds ? 0 : Math.Sin(Math.PI * dt / BlinkSeconds);
    }

    private double Next()
    {
        _rng ^= _rng << 13;
        _rng ^= _rng >> 17;
        _rng ^= _rng << 5;
        return (_rng & 0xFFFFFF) / (double)0x1000000;
    }

    private double SizeOf() => Math.Max(1, _owner?.Size ?? 96);

    private AiMascotFrame FrameFor(double size) => size < CompactBelow ? AiMascotFrame.Small : AiMascotFrame.Regular;

    /// <summary>The weighted sum of the states' frames; the gaze includes the pointer while it is followed.</summary>
    private AiMascotPose Compose(in AiMascotFrame frame, out double stiffness)
    {
        var full = ArcMotion.IsFull(this);
        var mix = full ? ArcEasings.InOut.Ease(_liveMix) : 0;
        var total = 0.0;
        for (var i = 0; i < StateCount; i++)
        {
            total += Math.Max(0, _weights[i].Value);
        }
        // Before the first status (nothing weighted yet) the current status is drawn alone.
        var unweighted = total < 1e-6;
        var pose = new AiMascotPose();
        var follows = 0.0;
        for (var i = 0; i < StateCount; i++)
        {
            var w = unweighted ? (i == (int)_status ? 1 : 0) : Math.Max(0, _weights[i].Value) / total;
            if (w < 1e-4)
            {
                continue;
            }
            var status = (AiMascotStatus)i;
            var since = _t - _entered[i];
            var still = AiMascotPose.Of(status, _t, since, false, _glanceX, _glanceY, frame);
            var state = mix > 0 ? AiMascotPose.Lerp(still, AiMascotPose.Of(status, _t, since, true, _glanceX, _glanceY, frame), mix) : still;
            pose.Add(state, w);
            if (status is not (AiMascotStatus.Thinking or AiMascotStatus.Writing))
            {
                follows += w;
            }
        }
        stiffness = pose.GazeStiffness;
        if (full && _pointer is { } pointer && _owner is { FollowPointer: not AiMascotPointerFollow.Never } && follows > 0)
        {
            // Thinking and writing keep their own gaze; the other states look at the pointer.
            var size = SizeOf();
            var center = Origin(size) + new Vector(size / 2, size * 0.55);
            var d = pointer - center;
            var reach = Math.Sqrt(d.X * d.X + d.Y * d.Y + size * size * 0.16);
            pose.GazeX += (d.X / reach - pose.GazeX) * follows;
            pose.GazeY += (d.Y / reach - pose.GazeY) * follows;
            stiffness = Math.Max(stiffness, 26);
        }
        return pose;
    }

    private Point Origin(double size) => new((Bounds.Width - size) / 2, (Bounds.Height - size) / 2);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = SizeOf();
        return new Size(size, size);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == AccentInkProperty || p == AccentForegroundInkProperty || p == ForegroundInkProperty || p == BackgroundInkProperty
            || p == SurfaceMutedInkProperty || p == BorderInkProperty || p == ShadeInkProperty || p == GroundInkProperty
            || p == LineInkProperty)
        {
            _palette = false;
        }
        else if (p == ArcMotion.LevelProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePointerHost();
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pageHost?.RemoveHandler(PointerMovedEvent, OnPagePointerMoved);
        _pageHost = null;
        _pointer = null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_owner?.FollowPointer == AiMascotPointerFollow.Hover && e.Pointer.Type != PointerType.Touch)
        {
            _pointer = e.GetPosition(this);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_owner?.FollowPointer == AiMascotPointerFollow.Hover)
        {
            _pointer = null;
        }
    }

    private void OnPagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
        {
            _pointer = e.GetPosition(this);
        }
    }

    /// <summary>Points the eyes at <paramref name="point"/> (control coordinates) as a pointer would; null lets go (tests).</summary>
    internal void SetPointer(Point? point) => _pointer = point;

    private void EnsurePalette()
    {
        if (_palette)
        {
            return;
        }
        _palette = true;
        var accent = ColorOf(AccentInk, Color.Parse("#5B5BD6"));
        var accentForeground = ColorOf(AccentForegroundInk, Avalonia.Media.Colors.White);
        var foreground = ColorOf(ForegroundInk, Color.Parse("#0B0B0B"));
        var background = ColorOf(BackgroundInk, Avalonia.Media.Colors.White);
        var muted = ColorOf(SurfaceMutedInk, Color.Parse("#F8F8F8"));
        var border = ColorOf(BorderInk, Color.Parse("#E9E9E9"));
        var tone = _owner?.Tone ?? AiMascotTone.Accent;
        Color body, eye, dot;
        var bordered = false;
        if (_owner?.BodyColor is { } custom)
        {
            body = custom;
            eye = Oklch.MixOklab(custom, Avalonia.Media.Colors.Black, 0.72);
            dot = custom;
        }
        else
        {
            switch (tone)
            {
                case AiMascotTone.Ink:
                    body = foreground;
                    eye = background;
                    dot = foreground;
                    break;
                case AiMascotTone.Soft:
                    body = muted;
                    eye = foreground;
                    dot = foreground;
                    bordered = true;
                    break;
                default:
                    body = accent;
                    eye = accentForeground;
                    dot = accent;
                    break;
            }
        }
        if (_owner?.EyeColor is { } eyeColor)
        {
            eye = eyeColor;
        }
        if (_owner?.DotColor is { } dotColor)
        {
            dot = dotColor;
        }
        _bodyBrush = new ImmutableSolidColorBrush(body);
        _eyeBrush = new ImmutableSolidColorBrush(eye);
        _dotBrush = new ImmutableSolidColorBrush(dot);
        _shadeBrush = new ImmutableSolidColorBrush(ColorOf(ShadeInk, Color.FromArgb(36, 0, 0, 0)));
        _groundBrush = new ImmutableSolidColorBrush(ColorOf(GroundInk, Color.FromArgb(26, 0, 0, 0)));
        _borderPen = bordered ? new Pen(new ImmutableSolidColorBrush(border), 1) : null;
        _borderScale = 1;
        _browPen = null;
    }

    private void EnsurePens(in AiMascotFrame frame, double scale)
    {
        if (_borderPen is not null && Math.Abs(_borderScale - scale) > 1e-6)
        {
            // The soft tone's border stays one device pixel wide at every size.
            _borderPen = new Pen(_borderPen.Brush, 1 / scale);
            _borderScale = scale;
        }
        if (_browPen is not null && _penCompact == frame.Compact)
        {
            return;
        }
        _penCompact = frame.Compact;
        _browPen = new Pen(_eyeBrush, 2.4 * frame.StrokeScale, lineCap: PenLineCap.Round);
        _squintPen = new Pen(_eyeBrush, 3 * frame.StrokeScale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _linePen = new Pen(new ImmutableSolidColorBrush(ColorOf(LineInk, Color.FromRgb(196, 196, 196))), 2, lineCap: PenLineCap.Round);
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // The whole square takes the pointer, so the eyes can follow it anywhere over the mascot.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_owner is null)
        {
            return;
        }
        EnsurePalette();
        var size = SizeOf();
        var frame = FrameFor(size);
        var scale = size / frame.Crop.Width;
        EnsurePens(frame, scale);
        var full = ArcMotion.IsFull(this);
        if (!full)
        {
            _liveMix = 0;
            _gazeReady = false;
        }
        var pose = Compose(frame, out _);
        var gazeX = full && _gazeReady ? _gazeX : pose.GazeX;
        var gazeY = full && _gazeReady ? _gazeY : pose.GazeY;
        var blink = full ? BlinkOpen(_t, _blinkA, _blinkB) : 1;
        var origin = Origin(size);
        var root = Matrix.CreateTranslation(-frame.Crop.X, -frame.Crop.Y) * Matrix.CreateScale(scale, scale)
                   * Matrix.CreateTranslation(origin.X, origin.Y);
        var companion = _owner.ShowCompanion;
        var ground = _owner.ShowGround && !frame.Compact;
        var dotR = pose.DotR * frame.DotScale;
        var dotCenter = new Point(pose.DotX, pose.DotY);
        using (context.PushTransform(root))
        {
            if (ground)
            {
                var lift = Math.Max(-2, -pose.BodyY);
                var rx = 21 * (1 - lift * 0.02) * (pose.ScaleX);
                using (context.PushOpacity(Math.Clamp(1 - lift * 0.05, 0.4, 1)))
                {
                    context.DrawEllipse(_groundBrush, null, new Point(AiMascotPose.AnchorX + pose.BodyX * 0.6, 88), rx, 2.4);
                }
                if (pose.LineOpacity > 0.01 && pose.LineTo - pose.LineFrom > 0.2)
                {
                    using (context.PushOpacity(Math.Min(1, pose.LineOpacity)))
                    {
                        context.DrawLine(_linePen!, new Point(pose.LineFrom, AiMascotPose.LineY), new Point(pose.LineTo, AiMascotPose.LineY));
                    }
                }
            }
            var dotOpacity = Math.Clamp(pose.DotOpacity, 0, 1);
            var back = Math.Clamp(pose.DotBack, 0, 1);
            if (companion && dotR > 0.05 && dotOpacity * back > 0.01)
            {
                using (context.PushOpacity(dotOpacity * back))
                {
                    context.DrawEllipse(_dotBrush, null, dotCenter, dotR, dotR);
                }
            }
            DrawBody(context, frame, pose, gazeX, gazeY, blink);
            if (companion && dotR > 0.05 && dotOpacity * (1 - back) > 0.01)
            {
                using (context.PushOpacity(dotOpacity * (1 - back)))
                {
                    context.DrawEllipse(_dotBrush, null, dotCenter, dotR, dotR);
                    var ink = Math.Clamp(pose.DotInk, 0, 1);
                    if (ink > 0.01)
                    {
                        using (context.PushOpacity(ink))
                        {
                            context.DrawEllipse(_eyeBrush, null, dotCenter, dotR, dotR);
                        }
                    }
                }
            }
            if (companion && pose.Sparkle > 0.01)
            {
                var s = 7.5 * frame.SparkleScale * pose.Sparkle;
                var m = Matrix.CreateScale(s, s) * Matrix.CreateRotation(pose.SparkleTurn * Math.PI / 180)
                        * Matrix.CreateTranslation(AiMascotPose.SparkleX, AiMascotPose.SparkleY);
                using (context.PushOpacity(Math.Clamp(pose.Sparkle, 0, 1)))
                using (context.PushTransform(m))
                {
                    context.DrawGeometry(_dotBrush, null, s_sparkle);
                }
            }
        }
    }

    private void DrawBody(DrawingContext context, in AiMascotFrame frame, in AiMascotPose pose, double gazeX, double gazeY, double blink)
    {
        var body = Matrix.CreateTranslation(-AiMascotPose.AnchorX, -AiMascotPose.AnchorY)
                   * Matrix.CreateScale(pose.ScaleX, pose.ScaleY)
                   * Matrix.CreateRotation(pose.Tilt * Math.PI / 180)
                   * Matrix.CreateTranslation(AiMascotPose.AnchorX + pose.BodyX, AiMascotPose.AnchorY + pose.BodyY);
        using (context.PushTransform(body))
        {
            context.DrawGeometry(_bodyBrush, null, s_body);
            // The quiet form shade: the body darkened, then lit again by a large ellipse up and to the left, which leaves
            // a soft crescent along the lower right.
            context.DrawGeometry(_shadeBrush, null, s_body);
            using (context.PushGeometryClip(s_body))
            {
                context.DrawEllipse(_bodyBrush, null, new Point(46.5, 52.5), 31, 26.5);
            }
            if (_borderPen is not null)
            {
                context.DrawGeometry(null, _borderPen, s_body);
            }

            var spread = AiMascotPose.EyeSpread * (frame.Compact ? 1.06 : 1);
            var shiftX = gazeX * AiMascotPose.GazeRangeX;
            var shiftY = gazeY * AiMascotPose.GazeRangeY;
            var eyeW = AiMascotPose.EyeWidth * frame.EyeScale * pose.EyeWide;
            var squint = Math.Clamp(pose.Squint, 0, 1);
            // Done passes the eyes through a closed line into the happy squint.
            var closeK = AiMascotPose.SmoothStep(0, 0.55, squint);
            var openH = AiMascotPose.EyeHeight * frame.EyeScale * pose.EyeOpen * blink;
            var lineH = eyeW * 0.34;
            var eyeH = Math.Max(lineH, openH + (lineH - openH) * closeK);
            var pillOpacity = 1 - AiMascotPose.SmoothStep(0.5, 0.75, squint);
            var arcOpacity = AiMascotPose.SmoothStep(0.5, 0.9, squint);
            for (var side = -1; side <= 1; side += 2)
            {
                var cx = AiMascotPose.AnchorX + side * spread + shiftX;
                var cy = AiMascotPose.EyeY + shiftY;
                if (pillOpacity > 0.01)
                {
                    using (context.PushOpacity(pillOpacity))
                    {
                        var rect = new Rect(cx - eyeW / 2, cy - eyeH / 2, eyeW, eyeH);
                        var radius = Math.Min(eyeW, eyeH) / 2;
                        context.DrawRectangle(_eyeBrush, null, new RoundedRect(rect, radius));
                    }
                }
                if (arcOpacity > 0.01)
                {
                    var s = frame.EyeScale * (0.85 + 0.15 * arcOpacity);
                    using (context.PushOpacity(arcOpacity))
                    using (context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(cx, cy)))
                    {
                        context.DrawGeometry(null, _squintPen, s_squint);
                    }
                }
                var brows = Math.Clamp(pose.Brows, 0, 1);
                if (brows > 0.01)
                {
                    // Worried brows: the inner ends raised, lifting into place as they appear.
                    var top = cy - AiMascotPose.EyeHeight * frame.EyeScale / 2 - 4.2 * frame.EyeScale + (1 - brows) * 2;
                    var inner = new Point(cx - side * 3.6 * frame.EyeScale, top - 1.6 * frame.EyeScale);
                    var outer = new Point(cx + side * 4.4 * frame.EyeScale, top + 1.8 * frame.EyeScale);
                    using (context.PushOpacity(brows))
                    {
                        context.DrawLine(_browPen!, inner, outer);
                    }
                }
            }
        }
    }
}
