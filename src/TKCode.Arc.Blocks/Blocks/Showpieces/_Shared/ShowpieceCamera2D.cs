using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A 2D pan-and-zoom camera (Customer galaxy, Semantic zoom): pure state and math, advanced by explicit time steps.
/// <para>
/// World → screen: <c>screen = (world − Center)·Scale + stage center</c>. <see cref="Zoom"/> is the readout
/// <c>Scale / FitScale</c> (1 = everything fits inside the safe area given by <see cref="Insets"/>). The rendered view
/// chases its target on springs; the state that springs is the screen transform itself (scale and
/// <c>Center·Scale</c>), so a zoom about an anchor keeps the anchor under the pointer during the whole spring, not
/// just at rest.
/// </para>
/// <para>
/// Inputs: <see cref="ZoomBy"/> (wheel, buttons, keys; anchored), <see cref="BeginPan"/> / <see cref="PanBy"/> /
/// <see cref="EndPan"/> (1:1 drag, rubber band past the bounds, optional glide), <see cref="FlyTo"/> /
/// <see cref="FlyToRect"/> / <see cref="Fit"/> (spring or van Wijk flights), <see cref="JumpTo"/>. Any input interrupts a
/// flight or glide; springs keep their velocity when retargeted. At <see cref="ArcMotionLevel.Reduced"/> and
/// <see cref="ArcMotionLevel.Off"/> every change lands at once and releases do not glide.
/// </para>
/// </summary>
/// <remarks>
/// Code-driven motion: the camera chases targets that change every frame (wheel bursts, rubber band, glides that end
/// in a spring at a bound) and couples center and zoom through the anchor; neither a ControlTheme transition nor
/// independent <see cref="MotionValue{T}"/>s can express that, and explicit steps keep it unit-testable.
/// </remarks>
internal sealed class ShowpieceCamera2D : IShowpieceCamera
{
    private const double PanRestDelta = 0.01;
    private const double PanRestSpeed = 0.05;

    // Rendered and target state: scale k and P = Center·k (so screen = world·k − P + stage/2).
    private ShowpieceSpringChannel _k = new(1, 1e-6, 1e-5);
    private ShowpieceSpringChannel _px = new(0, PanRestDelta, PanRestSpeed);
    private ShowpieceSpringChannel _py = new(0, PanRestDelta, PanRestSpeed);
    private ShowpieceInertia _inertiaX;
    private ShowpieceInertia _inertiaY;

    private Size _viewport;
    private Thickness _insets;
    private Rect _content;
    private double? _fitOverride;
    private double _fitScale = 1;
    private bool _initialized;

    private bool _flying;
    private VanWijkPath _path;
    private double _flightElapsed;
    private double _flightDuration;
    private double _flightExtent;
    private double _flightVk, _flightVx, _flightVy;

    private bool _dragging;
    private double _rawPx, _rawPy;

    private double _rawLogZoom = double.NaN;
    private Point _lastAnchor;
    private double _sinceZoom = double.PositiveInfinity;
    private bool _boundsPending;

    public ShowpieceCamera2D()
    {
        _inertiaX = new ShowpieceInertia(ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.PanFrameFactor), 3);
        _inertiaY = _inertiaX;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public int Version { get; private set; }

    // Options --------------------------------------------------------------------------------------------------------

    /// <summary>Smallest readout zoom (default 1 = fit).</summary>
    public double MinZoom { get; set; } = 1;

    /// <summary>Largest readout zoom (Customer galaxy 9, Semantic zoom 53.04; default 10).</summary>
    public double MaxZoom { get; set; } = 10;

    /// <summary>Safe area kept free around the content at fit and around the pan bounds (Semantic zoom 24 / 64 / 24 / 60).</summary>
    public Thickness Insets
    {
        get => _insets;
        set
        {
            _insets = value;
            RecomputeFit();
        }
    }

    /// <summary>When the pan bounds apply (default <see cref="ShowpieceBoundsMode.Immediate"/>).</summary>
    public ShowpieceBoundsMode BoundsMode { get; set; } = ShowpieceBoundsMode.Immediate;

    /// <summary>With <see cref="ShowpieceBoundsMode.AfterGesture"/>: how long zoom input rests before the view returns inside (1.1s).</summary>
    public TimeSpan BoundsSettleDelay { get; set; } = TimeSpan.FromSeconds(1.1);

    /// <summary>What zoom past the limits does (default <see cref="ShowpieceZoomOverflow.Clamp"/>).</summary>
    public ShowpieceZoomOverflow ZoomOverflow { get; set; } = ShowpieceZoomOverflow.Clamp;

    /// <summary>Rubber band dimension in ln(zoom) (Semantic zoom 0.2: −0.055 requested → −0.026 shown).</summary>
    public double ZoomRubberBandDimension { get; set; } = 0.2;

    /// <summary>How long zoom input rests before an overstretched zoom returns inside its limits (150ms).</summary>
    public TimeSpan ZoomSettleDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Rubber band factor past the pan bounds; the dimension is the stage width (x) and height (y).</summary>
    public double PanRubberBandFactor { get; set; } = RubberBand.DefaultFactor;

    /// <summary>Releases glide (Semantic zoom yes; Customer galaxy no). Default false.</summary>
    public bool HasInertia { get; set; }

    /// <summary>Glide time constant (default × 0.941 per frame, τ ≈ 274ms).</summary>
    public double InertiaTimeConstant
    {
        get => _inertiaX.TimeConstant;
        set
        {
            _inertiaX.TimeConstant = value;
            _inertiaY.TimeConstant = value;
        }
    }

    /// <summary>Glide stop speed in px/s (default 3).</summary>
    public double InertiaStopSpeed
    {
        get => _inertiaX.StopSpeed;
        set
        {
            _inertiaX.StopSpeed = value;
            _inertiaY.StopSpeed = value;
        }
    }

    /// <summary>Spring for wheel, button and key zoom (default <see cref="ShowpieceSprings.CameraFollow"/>, ω 25).</summary>
    public SpringSpec FollowSpring { get; set; } = ShowpieceSprings.CameraFollow;

    /// <summary>Spring for <see cref="ShowpieceFlightMode.Spring"/> flights (default <see cref="ShowpieceSprings.FlyFollow"/>).</summary>
    public SpringSpec FlySpring { get; set; } = ShowpieceSprings.FlyFollow;

    /// <summary>Spring back inside the bounds after a drag, a glide or a delayed settle (default ω 25).</summary>
    public SpringSpec SettleSpring { get; set; } = ShowpieceSprings.CameraFollow;

    /// <summary>Flight style of <see cref="FlyTo"/> and <see cref="Fit"/> (default <see cref="ShowpieceFlightMode.Spring"/>).</summary>
    public ShowpieceFlightMode FlightMode { get; set; } = ShowpieceFlightMode.Spring;

    /// <summary>van Wijk curvature ρ (default √2).</summary>
    public double Rho { get; set; } = VanWijkPath.DefaultRho;

    /// <summary>van Wijk flight duration from the path length (default <c>460 + 380·S</c> ms).</summary>
    public Func<double, TimeSpan>? FlightDuration { get; set; }

    /// <inheritdoc />
    public ArcMotionLevel MotionLevel { get; set; } = ArcMotionLevel.Full;

    // State ------------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public Size Viewport => _viewport;

    /// <summary>World bounds of the content (fit and pan bounds); empty means unbounded.</summary>
    public Rect ContentBounds => _content;

    /// <summary>Scale (px per world unit) at zoom 1.</summary>
    public double FitScale => _fitScale;

    /// <summary>Rendered scale (px per world unit).</summary>
    public double Scale => Math.Max(1e-9, _k.Value);

    /// <summary>Rendered world point at the stage center.</summary>
    public Point Center => new(_px.Value / Scale, _py.Value / Scale);

    /// <summary>Rendered readout zoom (<see cref="Scale"/> / <see cref="FitScale"/>).</summary>
    public double Zoom => Scale / _fitScale;

    /// <summary>Scale the view is heading to.</summary>
    public double TargetScale => Math.Max(1e-9, _k.Target);

    /// <summary>World center the view is heading to.</summary>
    public Point TargetCenter => new(_px.Target / TargetScale, _py.Target / TargetScale);

    /// <summary>Readout zoom the view is heading to.</summary>
    public double TargetZoom => TargetScale / _fitScale;

    /// <summary>A flight is running.</summary>
    public bool IsFlying => _flying;

    /// <summary>Scale velocity (per second; tests: retargets keep it).</summary>
    internal double ScaleVelocity => _k.Velocity;

    /// <summary>Velocity of <c>Center·Scale</c> in px/s (tests).</summary>
    internal Vector PanVelocity => new(_px.Velocity, _py.Velocity);

    /// <summary>A pan gesture is in progress.</summary>
    public bool IsPanning => _dragging;

    /// <summary>A release glide is running.</summary>
    public bool IsGliding => _inertiaX.IsActive || _inertiaY.IsActive;

    /// <summary>The world point shown at the safe-area center at fit.</summary>
    public Point FitCenter
    {
        get
        {
            var insets = Insets;
            var c = _content.Width > 0 || _content.Height > 0 ? _content.Center : default;
            return new Point(c.X - (insets.Left - insets.Right) / (2 * _fitScale), c.Y - (insets.Top - insets.Bottom) / (2 * _fitScale));
        }
    }

    /// <summary>The view is (heading to) zoom 1 at the fit center: Fit buttons disable themselves here.</summary>
    public bool IsAtFit
    {
        get
        {
            if (Math.Abs(TargetZoom - 1) > 1e-3)
            {
                return false;
            }
            var fit = FitCenter;
            var c = TargetCenter;
            return Math.Abs(fit.X - c.X) * TargetScale < 0.5 && Math.Abs(fit.Y - c.Y) * TargetScale < 0.5;
        }
    }

    /// <inheritdoc />
    public bool IsMoving => _flying || _k.IsMoving || _px.IsMoving || _py.IsMoving || IsGliding || _boundsPending || ZoomSettlePending;

    private bool ZoomSettlePending =>
        ZoomOverflow == ShowpieceZoomOverflow.RubberBand && !double.IsNaN(_rawLogZoom) &&
        (_rawLogZoom < LogMin - 1e-9 || _rawLogZoom > LogMax + 1e-9);

    private double LogMin => Math.Log(Math.Max(1e-9, MinZoom));

    private double LogMax => Math.Log(Math.Max(MinZoom, MaxZoom));

    private bool Animates => MotionLevel == ArcMotionLevel.Full;

    private Point StageCenter => new(_viewport.Width / 2, _viewport.Height / 2);

    /// <summary>The center of the safe area in screen px (the default zoom anchor).</summary>
    public Point SafeCenter
    {
        get
        {
            var i = Insets;
            return new Point(i.Left + (_viewport.Width - i.Left - i.Right) / 2, i.Top + (_viewport.Height - i.Top - i.Bottom) / 2);
        }
    }

    // Conversions ----------------------------------------------------------------------------------------------------

    /// <summary>World → screen with the rendered view.</summary>
    public Point WorldToScreen(Point world)
    {
        var k = Scale;
        return new Point(world.X * k - _px.Value + _viewport.Width / 2, world.Y * k - _py.Value + _viewport.Height / 2);
    }

    /// <summary>Screen → world with the rendered view.</summary>
    public Point ScreenToWorld(Point screen)
    {
        var k = Scale;
        return new Point((screen.X - _viewport.Width / 2 + _px.Value) / k, (screen.Y - _viewport.Height / 2 + _py.Value) / k);
    }

    /// <summary>World → screen as a matrix (for <c>DrawingContext.PushTransform</c>).</summary>
    public Matrix WorldToScreenMatrix
    {
        get
        {
            var k = Scale;
            return new Matrix(k, 0, 0, 0, k, 0, _viewport.Width / 2 - _px.Value, _viewport.Height / 2 - _py.Value, 1);
        }
    }

    /// <summary>The world rectangle the rendered view shows.</summary>
    public Rect VisibleWorldRect
    {
        get
        {
            var a = ScreenToWorld(default);
            var b = ScreenToWorld(new Point(_viewport.Width, _viewport.Height));
            return new Rect(a, b);
        }
    }

    // Setup ------------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public void SetViewport(Size size)
    {
        if (size == _viewport)
        {
            return;
        }
        var zoom = TargetZoom;
        var center = TargetCenter;
        _viewport = size;
        RecomputeFit();
        if (!_initialized)
        {
            JumpState(_fitScale, FitCenter);
            return;
        }
        // Resizes are not animated (the view keeps its readout zoom and center).
        EndFlight(keepVelocity: false);
        JumpState(_fitScale * zoom, center);
    }

    /// <summary>
    /// Sets the content bounds (world units) and optionally the fit scale (otherwise the content fills the safe
    /// area). Keeps the current view; call <see cref="Fit"/> to frame the new content.
    /// </summary>
    public void SetContent(Rect worldBounds, double? fitScale = null)
    {
        _content = worldBounds;
        _fitOverride = fitScale is > 0 ? fitScale : null;
        RecomputeFit();
        if (!_initialized)
        {
            JumpState(_fitScale, FitCenter);
            return;
        }
        Notify();
    }

    private void RecomputeFit()
    {
        if (_fitOverride is { } fit)
        {
            _fitScale = fit;
            return;
        }
        var i = Insets;
        var w = _viewport.Width - i.Left - i.Right;
        var h = _viewport.Height - i.Top - i.Bottom;
        if (w <= 0 || h <= 0 || (_content.Width <= 0 && _content.Height <= 0))
        {
            _fitScale = _fitScale > 0 ? _fitScale : 1;
            return;
        }
        var sx = _content.Width > 0 ? w / _content.Width : double.PositiveInfinity;
        var sy = _content.Height > 0 ? h / _content.Height : double.PositiveInfinity;
        _fitScale = Math.Max(1e-9, Math.Min(sx, sy));
    }

    // Bounds -----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The range of world centers allowed at <paramref name="scale"/>: the content expanded by <see cref="Insets"/>
    /// must cover the stage; on an axis where it is smaller than the stage the center is fixed (content centered in
    /// the safe area). Unbounded when the content is empty.
    /// </summary>
    public Rect CenterRange(double scale)
    {
        if (_content.Width <= 0 && _content.Height <= 0)
        {
            return new Rect(double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        }
        var i = Insets;
        AxisRange(_content.Left, _content.Right, i.Left, i.Right, _viewport.Width, scale, out var minX, out var maxX);
        AxisRange(_content.Top, _content.Bottom, i.Top, i.Bottom, _viewport.Height, scale, out var minY, out var maxY);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AxisRange(double c0, double c1, double lo, double hi, double stage, double k, out double min, out double max)
    {
        k = Math.Max(1e-9, k);
        if ((c1 - c0) * k + lo + hi <= stage)
        {
            min = max = (c0 + c1) / 2 - (lo - hi) / (2 * k);
            return;
        }
        min = c0 + (stage / 2 - lo) / k;
        max = c1 - (stage / 2 - hi) / k;
    }

    /// <summary>A world center moved inside <see cref="CenterRange"/> at <paramref name="scale"/>.</summary>
    public Point ClampCenter(Point center, double scale)
    {
        var range = CenterRange(scale);
        if (double.IsInfinity(range.Width))
        {
            return center;
        }
        return new Point(Math.Clamp(center.X, range.Left, range.Right), Math.Clamp(center.Y, range.Top, range.Bottom));
    }

    private void PanRange(double k, out double minX, out double maxX, out double minY, out double maxY)
    {
        var range = CenterRange(k);
        if (double.IsInfinity(range.Width))
        {
            minX = minY = double.NegativeInfinity;
            maxX = maxY = double.PositiveInfinity;
            return;
        }
        minX = range.Left * k;
        maxX = range.Right * k;
        minY = range.Top * k;
        maxY = range.Bottom * k;
    }

    // Commands ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Zooms the target by <paramref name="factor"/> keeping the world point under <paramref name="anchor"/> (screen px,
    /// default <see cref="SafeCenter"/>) fixed. Limits clamp or rubber-band per <see cref="ZoomOverflow"/>; the pan
    /// bounds apply per <see cref="BoundsMode"/>. Springs on <see cref="FollowSpring"/>.
    /// </summary>
    public void ZoomBy(double factor, Point? anchor = null)
    {
        if (!(factor > 0) || double.IsInfinity(factor))
        {
            return;
        }
        _initialized = true;
        EndFlight(keepVelocity: true);
        StopGlide();
        var logZ = Math.Log(TargetScale / _fitScale);
        double newLog;
        if (ZoomOverflow == ShowpieceZoomOverflow.RubberBand)
        {
            if (double.IsNaN(_rawLogZoom))
            {
                _rawLogZoom = UnstretchLog(logZ);
            }
            _rawLogZoom += Math.Log(factor);
            newLog = RubberBand.Clamp(_rawLogZoom, LogMin, LogMax, ZoomRubberBandDimension);
        }
        else
        {
            newLog = Math.Clamp(logZ + Math.Log(factor), LogMin, LogMax);
        }
        var a = anchor ?? SafeCenter;
        _lastAnchor = a;
        _sinceZoom = 0;
        ZoomTargetTo(_fitScale * Math.Exp(newLog), a, FollowSpring);
    }

    /// <summary>Zooms the target to readout <paramref name="zoom"/> about <paramref name="anchor"/> (clamped to the limits).</summary>
    public void ZoomTo(double zoom, Point? anchor = null)
    {
        if (!(zoom > 0))
        {
            return;
        }
        ZoomBy(Math.Clamp(zoom, MinZoom, MaxZoom) / TargetZoom, anchor);
    }

    private double UnstretchLog(double logZ)
    {
        if (logZ < LogMin)
        {
            return LogMin + RubberBand.Invert(logZ - LogMin, ZoomRubberBandDimension);
        }
        if (logZ > LogMax)
        {
            return LogMax + RubberBand.Invert(logZ - LogMax, ZoomRubberBandDimension);
        }
        return logZ;
    }

    private void ZoomTargetTo(double k2, Point anchor, in SpringSpec spec)
    {
        var kT = TargetScale;
        var half = StageCenter;
        // The world point under the anchor in the target view stays under the anchor.
        var wx = (anchor.X - half.X + _px.Target) / kT;
        var wy = (anchor.Y - half.Y + _py.Target) / kT;
        var p2x = wx * k2 + half.X - anchor.X;
        var p2y = wy * k2 + half.Y - anchor.Y;
        if (BoundsMode == ShowpieceBoundsMode.Immediate)
        {
            PanRange(k2, out var minX, out var maxX, out var minY, out var maxY);
            p2x = Math.Clamp(p2x, minX, maxX);
            p2y = Math.Clamp(p2y, minY, maxY);
        }
        else
        {
            PanRange(k2, out var minX, out var maxX, out var minY, out var maxY);
            _boundsPending = p2x < minX - PanRestDelta || p2x > maxX + PanRestDelta || p2y < minY - PanRestDelta || p2y > maxY + PanRestDelta;
            if (_boundsPending && !Animates)
            {
                // Reduced motion: no delayed recentering, the bounds apply at once.
                p2x = Math.Clamp(p2x, minX, maxX);
                p2y = Math.Clamp(p2y, minY, maxY);
                _boundsPending = false;
            }
        }
        Retarget(k2, p2x, p2y, spec);
    }

    /// <summary>Starts a pan at the rendered view (stops flights, glides and springs; a stretched view is grabbed where it is).</summary>
    public void BeginPan()
    {
        _initialized = true;
        EndFlight(keepVelocity: false);
        StopGlide();
        _k.Stop();
        _px.Stop();
        _py.Stop();
        _rawLogZoom = double.NaN;
        _boundsPending = false;
        _dragging = true;
        PanRange(Scale, out var minX, out var maxX, out var minY, out var maxY);
        _rawPx = Unstretch(_px.Value, minX, maxX, _viewport.Width);
        _rawPy = Unstretch(_py.Value, minY, maxY, _viewport.Height);
    }

    private double Unstretch(double value, double min, double max, double dimension)
    {
        if (value < min)
        {
            return min + RubberBand.Invert(value - min, dimension, PanRubberBandFactor);
        }
        if (value > max)
        {
            return max + RubberBand.Invert(value - max, dimension, PanRubberBandFactor);
        }
        return value;
    }

    /// <summary>Moves the content by <paramref name="screenDelta"/> px (1:1), rubber-banding past the bounds.</summary>
    public void PanBy(Vector screenDelta)
    {
        if (!_dragging)
        {
            BeginPan();
        }
        _rawPx -= screenDelta.X;
        _rawPy -= screenDelta.Y;
        PanRange(Scale, out var minX, out var maxX, out var minY, out var maxY);
        _px.Jump(RubberBand.Clamp(_rawPx, minX, maxX, Math.Max(1, _viewport.Width), PanRubberBandFactor));
        _py.Jump(RubberBand.Clamp(_rawPy, minY, maxY, Math.Max(1, _viewport.Height), PanRubberBandFactor));
        Notify();
    }

    /// <summary>
    /// Ends a pan. An axis past its bound springs back (<see cref="SettleSpring"/>); otherwise, with
    /// <see cref="HasInertia"/> at full motion, the release <paramref name="screenVelocity"/> (px/s) glides on.
    /// </summary>
    public void EndPan(Vector screenVelocity = default)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        PanRange(Scale, out var minX, out var maxX, out var minY, out var maxY);
        ReleaseAxis(ref _px, ref _inertiaX, minX, maxX, -screenVelocity.X);
        ReleaseAxis(ref _py, ref _inertiaY, minY, maxY, -screenVelocity.Y);
        Notify();
    }

    private void ReleaseAxis(ref ShowpieceSpringChannel channel, ref ShowpieceInertia inertia, double min, double max, double velocity)
    {
        var clamped = Math.Clamp(channel.Value, min, max);
        if (Math.Abs(clamped - channel.Value) > PanRestDelta)
        {
            if (Animates)
            {
                channel.SpringTo(clamped, SettleSpring);
            }
            else
            {
                channel.Jump(clamped);
            }
            return;
        }
        if (HasInertia && Animates)
        {
            inertia.Start(velocity);
        }
    }

    /// <summary>
    /// Flies to <paramref name="center"/> (world) at readout <paramref name="zoom"/> (clamped to the limits and the pan
    /// bounds), by <paramref name="mode"/> or <see cref="FlightMode"/>.
    /// </summary>
    public void FlyTo(Point center, double zoom, ShowpieceFlightMode? mode = null)
    {
        _initialized = true;
        var k1 = _fitScale * Math.Clamp(zoom, MinZoom, MaxZoom);
        var c1 = ClampCenter(center, k1);
        EndFlight(keepVelocity: true);
        StopGlide();
        _dragging = false;
        _rawLogZoom = double.NaN;
        _boundsPending = false;
        if (!Animates)
        {
            JumpState(k1, c1);
            return;
        }
        if ((mode ?? FlightMode) == ShowpieceFlightMode.Spring)
        {
            Retarget(k1, c1.X * k1, c1.Y * k1, FlySpring);
            return;
        }
        var extent = Math.Max(1, Math.Max(_viewport.Width, _viewport.Height));
        var k0 = Scale;
        var path = VanWijkPath.Create(Center, extent / k0, c1, extent / k1, Rho);
        var duration = (FlightDuration ?? VanWijkPath.DurationFor)(path.Length).TotalSeconds;
        if (duration <= 0 || (path.Length < 1e-6 && (Center - c1).X == 0 && (Center - c1).Y == 0))
        {
            JumpState(k1, c1);
            return;
        }
        _path = path;
        _flightExtent = extent;
        _flightDuration = duration;
        _flightElapsed = 0;
        _flying = true;
        _k.Stop();
        _px.Stop();
        _py.Stop();
        Notify();
    }

    /// <summary>
    /// Flies so <paramref name="worldRect"/> fills the safe area (times <paramref name="fill"/>; Semantic zoom 0.97 for
    /// teams and projects, 0.96 for tasks), centered there and then held inside the pan bounds.
    /// </summary>
    public void FlyToRect(Rect worldRect, double fill = 1, ShowpieceFlightMode? mode = null)
    {
        var (center, zoom) = FrameRect(worldRect, fill);
        FlyTo(center, zoom, mode);
    }

    /// <summary>The (center, readout zoom) that frames <paramref name="worldRect"/> in the safe area.</summary>
    public (Point Center, double Zoom) FrameRect(Rect worldRect, double fill = 1)
    {
        var i = Insets;
        var w = Math.Max(1e-9, _viewport.Width - i.Left - i.Right);
        var h = Math.Max(1e-9, _viewport.Height - i.Top - i.Bottom);
        var sx = worldRect.Width > 0 ? w / worldRect.Width : double.PositiveInfinity;
        var sy = worldRect.Height > 0 ? h / worldRect.Height : double.PositiveInfinity;
        var k = Math.Min(sx, sy);
        if (double.IsInfinity(k))
        {
            k = TargetScale;
        }
        k *= fill;
        k = _fitScale * Math.Clamp(k / _fitScale, MinZoom, MaxZoom);
        var c = worldRect.Center;
        return (new Point(c.X - (i.Left - i.Right) / (2 * k), c.Y - (i.Top - i.Bottom) / (2 * k)), k / _fitScale);
    }

    /// <summary>Back to zoom 1 at the fit center (animated by <see cref="FlightMode"/> unless <paramref name="animate"/> is false).</summary>
    public void Fit(bool animate = true)
    {
        if (!animate)
        {
            JumpTo(FitCenter, 1);
            return;
        }
        FlyTo(FitCenter, 1);
    }

    /// <summary>Sets the view at once (clamped to the limits and bounds).</summary>
    public void JumpTo(Point center, double zoom)
    {
        _initialized = true;
        EndFlight(keepVelocity: false);
        StopGlide();
        _dragging = false;
        _rawLogZoom = double.NaN;
        _boundsPending = false;
        var k = _fitScale * Math.Clamp(zoom, MinZoom, MaxZoom);
        JumpState(k, ClampCenter(center, k));
    }

    /// <summary>Stops every motion where it is.</summary>
    public void Stop()
    {
        EndFlight(keepVelocity: false);
        StopGlide();
        _k.Stop();
        _px.Stop();
        _py.Stop();
        _rawLogZoom = double.NaN;
        _boundsPending = false;
        Notify();
    }

    private void StopGlide()
    {
        _inertiaX.Stop();
        _inertiaY.Stop();
    }

    private void JumpState(double k, Point center)
    {
        _k.Jump(k);
        _px.Jump(center.X * k);
        _py.Jump(center.Y * k);
        Notify();
    }

    private void Retarget(double k, double px, double py, in SpringSpec spec)
    {
        if (!Animates)
        {
            _k.Jump(k);
            _px.Jump(px);
            _py.Jump(py);
        }
        else
        {
            _k.RestDelta = Math.Max(1e-9, k * 1e-5);
            _k.RestSpeed = _k.RestDelta * 2;
            _k.SpringTo(k, spec);
            _px.SpringTo(px, spec);
            _py.SpringTo(py, spec);
        }
        Notify();
    }

    private void EndFlight(bool keepVelocity)
    {
        if (!_flying)
        {
            return;
        }
        _flying = false;
        if (keepVelocity)
        {
            // The next spring continues the flight's motion instead of starting from rest.
            _k.Velocity = _flightVk;
            _px.Velocity = _flightVx;
            _py.Velocity = _flightVy;
        }
    }

    // Frames -----------------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public bool Advance(double dt)
    {
        if (!(dt > 0))
        {
            return IsMoving;
        }
        double k0 = _k.Value, x0 = _px.Value, y0 = _py.Value;
        if (_flying)
        {
            StepFlight(dt);
        }
        StepGlide(ref _px, ref _inertiaX, dt, isX: true);
        StepGlide(ref _py, ref _inertiaY, dt, isX: false);
        _k.Step(dt);
        _px.Step(dt);
        _py.Step(dt);
        _sinceZoom += dt;
        if (ZoomSettlePending && _sinceZoom >= ZoomSettleDelay.TotalSeconds)
        {
            var clamped = Math.Clamp(_rawLogZoom, LogMin, LogMax);
            _rawLogZoom = double.NaN;
            ZoomTargetTo(_fitScale * Math.Exp(clamped), _lastAnchor, FollowSpring);
        }
        else if (!double.IsNaN(_rawLogZoom) && !ZoomSettlePending && _sinceZoom >= ZoomSettleDelay.TotalSeconds)
        {
            _rawLogZoom = double.NaN;
        }
        if (_boundsPending && !_dragging && _sinceZoom >= BoundsSettleDelay.TotalSeconds)
        {
            _boundsPending = false;
            PanRange(TargetScale, out var minX, out var maxX, out var minY, out var maxY);
            var tx = Math.Clamp(_px.Target, minX, maxX);
            var ty = Math.Clamp(_py.Target, minY, maxY);
            if (Math.Abs(tx - _px.Target) > PanRestDelta || Math.Abs(ty - _py.Target) > PanRestDelta)
            {
                if (Animates)
                {
                    _px.SpringTo(tx, SettleSpring);
                    _py.SpringTo(ty, SettleSpring);
                }
                else
                {
                    _px.Jump(tx);
                    _py.Jump(ty);
                }
            }
        }
        if (_k.Value != k0 || _px.Value != x0 || _py.Value != y0)
        {
            Notify();
        }
        return IsMoving;
    }

    private void StepFlight(double dt)
    {
        _flightElapsed += dt;
        var progress = Math.Min(1, _flightElapsed / _flightDuration);
        var (center, width) = _path.At(ArcEasings.InOut.Ease(progress));
        var k = _flightExtent / Math.Max(1e-12, width);
        var px = center.X * k;
        var py = center.Y * k;
        _flightVk = (k - _k.Value) / dt;
        _flightVx = (px - _px.Value) / dt;
        _flightVy = (py - _py.Value) / dt;
        _k.Jump(k);
        _px.Jump(px);
        _py.Jump(py);
        if (progress >= 1)
        {
            _flying = false;
        }
    }

    private void StepGlide(ref ShowpieceSpringChannel channel, ref ShowpieceInertia inertia, double dt, bool isX)
    {
        if (!inertia.IsActive)
        {
            return;
        }
        var position = channel.Value + inertia.Step(dt);
        PanRange(Scale, out var minX, out var maxX, out var minY, out var maxY);
        var min = isX ? minX : minY;
        var max = isX ? maxX : maxY;
        channel.Jump(position);
        if (position < min || position > max)
        {
            // The glide hit the bound: the axis stops gliding and springs back carrying its speed (a short overshoot).
            var velocity = inertia.Velocity;
            inertia.Stop();
            channel.SpringTo(Math.Clamp(position, min, max), SettleSpring, velocity);
        }
    }

    private void Notify()
    {
        Version++;
        Changed?.Invoke();
    }
}
