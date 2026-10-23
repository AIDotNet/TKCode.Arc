using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The dotted globe of a <see cref="RevenueGlobe"/> (template part): an orthographic sphere of land dots and sea squares
/// shaded in 8 depth buckets, city dots, payment arcs to HQ with launch and arrival rings, HQ with its label, and the
/// hover / selection rings. Drags spin it (1px = 1/R rad, surface under the pointer), releases glide (× 0.987 per
/// frame), pitch rubber-bands past ±65° and returns to the home tilt; idle it drifts back and forth around HQ.
/// </summary>
/// <remarks>
/// Code-driven motion: the camera (drift, glides, great-circle flights) and the arcs advance together each frame in the
/// stage's loop; every value is self-drawn scene state that no ControlTheme transition can reach.
/// </remarks>
internal sealed partial class RevenueGlobeCanvas : ShowpieceStage
{
    private const int MaxArcs = 64;
    private const int MaxRings = 40;

    private readonly ShowpieceOrbitCamera _camera;
    private RevenueGlobeScene _scene = RevenueGlobeScene.Empty;
    private RevenueGlobeLand _land = RevenueGlobeLand.Default;

    // Per-frame scratch (sized once per land; nothing allocated per frame except arc and ring geometries).
    private float[] _dotX = [];
    private float[] _dotY = [];
    private int[] _order = [];
    private byte[] _dotBucket = [];
    private readonly int[] _bucketStart = new int[RevenueGlobeMath.Buckets + 1];
    private readonly int[] _bucketFill = new int[RevenueGlobeMath.Buckets];
    private readonly Point[] _run = new Point[RevenueGlobeMath.ArcPoints + 2];
    private readonly Point[] _ringPoints = new Point[RingSegments + 1];

    private readonly ArcState[] _arcs = new ArcState[MaxArcs];
    private int _arcCount;
    private readonly RingState[] _rings = new RingState[MaxRings];
    private int _ringCount;
    private (int City, double Weight)[] _stillArcs = [];

    // Colors and caches (re-resolved on theme changes).
    private bool _colorsValid;
    private Color _foreground;
    private Color _border;
    private Color _accent;
    private Color _background;
    private Color _textSecondary;
    private readonly Dictionary<uint, IImmutableSolidColorBrush> _brushes = new();
    private readonly Dictionary<(uint Color, int Width), IPen> _pens = new();
    private readonly Dictionary<string, Geometry?> _labels = new();
    private Typeface _labelTypeface = new(FontFamily.Default);
    private IPen? _haloPen;
    private IPen? _focusPen;

    private int _hoverCity = -1;
    private int _listHoverCity = -1;
    private double _homeTilt = ShowpieceProjection.Radians(22);

    private const int RingSegments = 28;

    public RevenueGlobeCanvas()
    {
        _camera = new ShowpieceOrbitCamera
        {
            PitchMin = ShowpieceProjection.Radians(-65),
            PitchMax = ShowpieceProjection.Radians(65),
            PitchRubberBandDimension = ShowpieceProjection.Radians(25),
            PitchRubberBandFactor = 0.77,
            PitchReturnSpring = ShowpieceSprings.PitchHome,
            InertiaTimeConstant = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.GlobeFrameFactor),
            ZoomMin = 0.5,
            ZoomMax = 3,
            IsDriftEnabled = true
        };
        _camera.JumpTo(new ShowpieceOrbitPose { Yaw = 0, Pitch = _homeTilt, Zoom = 1 });
        _camera.PitchRest = _homeTilt;
        _camera.Changed += OnCameraViewChanged;
        Camera = _camera;
        WheelMode = ShowpieceWheelMode.PassThrough;
        RoleDescription = "interactive globe";
        ClipToBounds = true;
    }

    // Wiring to the block ---------------------------------------------------------------------------------------------------

    /// <summary>A city dot was clicked (index into the scene's cities).</summary>
    public event Action<int>? CityTapped;

    /// <summary>Empty space was clicked.</summary>
    public event Action? BackgroundTapped;

    /// <summary>The pointer hovers another city (−1 for none).</summary>
    public event Action<int>? HoverCityChanged;

    /// <summary>Escape was pressed on the globe.</summary>
    public event Action? EscapePressed;

    /// <summary>A drag started (the hover card hides).</summary>
    public event Action? DragBegan;

    /// <summary>The camera or the size changed: anything anchored to a city moves.</summary>
    public event Action? ViewChanged;

    /// <summary>An arc's head reached HQ (the payment index it carries; −1 when it does not count).</summary>
    public event Action<int>? ArcLanded;

    /// <summary>The effective motion level changed.</summary>
    public event Action<ArcMotionLevel>? MotionLevelChangedEvent;

    internal ShowpieceOrbitCamera OrbitCamera => _camera;

    public RevenueGlobeScene Scene
    {
        get => _scene;
        set
        {
            _scene = value ?? RevenueGlobeScene.Empty;
            _arcCount = 0;
            _ringCount = 0;
            _stillArcs = [];
            _hoverCity = -1;
            _listHoverCity = -1;
            InvalidateVisual();
        }
    }

    public RevenueGlobeLand Land
    {
        get => _land;
        set
        {
            _land = value ?? RevenueGlobeLand.Default;
            InvalidateVisual();
        }
    }

    /// <summary>The selected city (index, −1 for none): gets the ring.</summary>
    public int SelectedCity { get; set; } = -1;

    /// <summary>The selected region (index, −1 for none): other cities and their arcs fade.</summary>
    public int SelectedRegion { get; set; } = -1;

    /// <summary>Cities that get a ring and a name label while a region is selected (its top three).</summary>
    public int[] LabeledCities { get; set; } = [];

    /// <summary>Arrival rings last shorter while replaying.</summary>
    public bool IsReplaying { get; set; }

    /// <summary>The city hovered in the list of top cities (−1 for none): gets the ring like a hovered dot.</summary>
    public int ListHoverCity
    {
        get => _listHoverCity;
        set
        {
            if (_listHoverCity != value)
            {
                _listHoverCity = value;
                InvalidateVisual();
            }
        }
    }

    /// <summary>The city under the pointer (−1 for none).</summary>
    public int HoverCity => _hoverCity;

    /// <summary>Number of arcs alive (tests).</summary>
    public int ArcCount => _arcCount;

    /// <summary>Number of rings alive (tests).</summary>
    public int RingCount => _ringCount;

    /// <summary>Number of still arcs drawn under reduced motion (tests).</summary>
    public int StillArcCount => _stillArcs.Length;

    /// <summary>Radius of the globe at zoom 1 for the current size: 0.41 × the smaller side.</summary>
    public double BaseRadius => RevenueGlobeMath.RadiusFactor * Math.Min(Bounds.Width, Bounds.Height);

    /// <summary>The rendered radius (base radius × zoom).</summary>
    public double Radius => BaseRadius * _camera.Zoom;

    private Point StageCenter => new(Bounds.Width / 2, Bounds.Height / 2);

    public ShowpieceSphereView View => _camera.GetSphereView(StageCenter, BaseRadius);

    /// <summary>Configures the idle drift and home tilt (degrees).</summary>
    public void ConfigureCamera(double centerLongitude, double halfRange, double speed, double homeTilt)
    {
        _camera.DriftCenter = ShowpieceProjection.Radians(centerLongitude);
        _camera.DriftHalfRange = ShowpieceProjection.Radians(Math.Max(0, halfRange));
        _camera.DriftSpeed = ShowpieceProjection.Radians(Math.Max(0, speed));
        _homeTilt = ShowpieceProjection.Radians(homeTilt);
        if (_camera.PitchRest is not null)
        {
            _camera.PitchRest = _homeTilt;
        }
        RequestFrames();
    }

    /// <summary>Puts the camera at home at once: the middle of the drift range, the home tilt, zoom 1.</summary>
    public void PlaceHome()
    {
        _camera.JumpTo(new ShowpieceOrbitPose { Yaw = _camera.DriftCenter, Pitch = _homeTilt, Zoom = 1 });
        RequestFrames();
    }

    /// <summary>Whether the idle drift may run (off while a selection holds the view, or when the speed is 0).</summary>
    public void SetDriftEnabled(bool enabled)
    {
        _camera.IsDriftEnabled = enabled && _camera.DriftSpeed > 0;
        RequestFrames();
    }

    /// <summary>Flies along the great circle to <paramref name="target"/> at <paramref name="zoom"/> and holds the view there.</summary>
    public void FlyTo(ShowpieceVec3 target, double zoom)
    {
        var (latitude, longitude) = RevenueGlobeMath.ToLatLon(target);
        var yaw = _camera.Yaw;
        longitude = yaw + ShowpieceProjection.WrapAngle(longitude - yaw);
        _camera.IsDriftEnabled = false;
        _camera.PitchRest = null;
        _camera.FlyAlong(new RevenueGlobeFlightPath(yaw, _camera.Pitch, _camera.Zoom, longitude, latitude, zoom), ShowpieceSprings.GlobeFly);
        RequestFrames();
    }

    /// <summary>Lets go of a held view: zoom springs back to 1 (ω 8), pitch to the home tilt (ω 3), longitude stays.</summary>
    public void ReleaseView(bool drift)
    {
        _camera.AnimateTo(new ShowpieceOrbitPose { Zoom = 1 }, ShowpieceSprings.ZoomClear);
        _camera.PitchRest = _homeTilt;
        _camera.IsDriftEnabled = drift && _camera.DriftSpeed > 0;
        RequestFrames();
    }

    /// <summary>Turns the globe by the given degrees on a step spring.</summary>
    public void SpinBy(double longitudeDegrees, double latitudeDegrees)
    {
        var pose = new ShowpieceOrbitPose
        {
            Yaw = _camera.TargetYaw + ShowpieceProjection.Radians(longitudeDegrees),
            Pitch = Math.Clamp(_camera.TargetPitch + ShowpieceProjection.Radians(latitudeDegrees), _camera.PitchMin, _camera.PitchMax)
        };
        _camera.AnimateTo(pose, ShowpieceSprings.OrbitStep, shortestYaw: false);
        RequestFrames();
    }

    /// <summary>Projects a city; false when it is on the far side (the point is still computed).</summary>
    public bool TryProjectCity(int index, out Point screen, out double depth)
    {
        if ((uint)index >= (uint)_scene.CityPoints.Length || Bounds.Width <= 0)
        {
            screen = default;
            depth = -1;
            return false;
        }
        depth = View.Project(_scene.CityPoints[index], out screen);
        return depth > 0;
    }

    // Arcs and rings ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Launches an arc from <paramref name="city"/> to HQ for a payment (its ledger index, or −1 when it does not count),
    /// with a launch ring at the city when it faces the viewer.
    /// </summary>
    public bool SpawnArc(int city, double amount, int payment, double age = 0)
    {
        if (!_scene.HasArc(city))
        {
            return false;
        }
        if (_arcCount == MaxArcs)
        {
            // Too many in flight: the oldest gives way (its payment lands now).
            LandArc(ref _arcs[0]);
            Array.Copy(_arcs, 1, _arcs, 0, _arcCount - 1);
            _arcCount--;
        }
        _arcs[_arcCount++] = new ArcState
        {
            City = city,
            Payment = payment,
            Weight = RevenueGlobeMath.ArcWeight(amount),
            Duration = RevenueGlobeMath.FlightDuration(_scene.ArcTheta[city]),
            Age = Math.Max(0, age)
        };
        var (u, v) = _scene.CityTangents[city];
        AddRing(_scene.CityPoints[city], u, v, RevenueGlobeMath.LaunchRingDuration, RevenueGlobeMath.LaunchRingRadius, arrival: false);
        RequestFrames();
        return true;
    }

    /// <summary>Every arc in flight stops counting (the playhead jumped; the totals come from the ledger now).</summary>
    public void DetachArcs()
    {
        for (var i = 0; i < _arcCount; i++)
        {
            _arcs[i].Payment = -1;
        }
    }

    /// <summary>Removes every arc and ring.</summary>
    public void ClearArcs()
    {
        _arcCount = 0;
        _ringCount = 0;
        InvalidateVisual();
    }

    /// <summary>The still arcs of reduced motion (whole, no heads or rings).</summary>
    public void SetStillArcs((int City, double Weight)[] arcs)
    {
        _stillArcs = arcs;
        InvalidateVisual();
    }

    private void AddRing(ShowpieceVec3 center, ShowpieceVec3 u, ShowpieceVec3 v, double duration, double angle, bool arrival)
    {
        if (_ringCount == MaxRings)
        {
            Array.Copy(_rings, 1, _rings, 0, _ringCount - 1);
            _ringCount--;
        }
        _rings[_ringCount++] = new RingState { Center = center, U = u, V = v, Duration = duration, Angle = angle, Arrival = arrival };
    }

    private void LandArc(ref ArcState arc)
    {
        if (arc.Landed)
        {
            return;
        }
        arc.Landed = true;
        if (_scene.HasHq)
        {
            var (u, v) = _scene.HqTangents;
            AddRing(_scene.HqPoint, u, v, IsReplaying ? RevenueGlobeMath.ReplayArrivalRingDuration : RevenueGlobeMath.ArrivalRingDuration,
                RevenueGlobeMath.ArrivalRingRadius, arrival: true);
        }
        var payment = arc.Payment;
        arc.Payment = -1;
        ArcLanded?.Invoke(payment);
    }

    /// <summary>Steps the camera and the arcs by hand (tests and the snapshot harness), then redraws.</summary>
    internal bool Step(double dt)
    {
        var moving = _camera.Advance(dt);
        moving |= OnAdvance(dt);
        InvalidateVisual();
        return moving;
    }

    protected override bool OnAdvance(double dt)
    {
        var moving = false;
        var write = 0;
        for (var i = 0; i < _arcCount; i++)
        {
            ref var arc = ref _arcs[i];
            arc.Age += dt;
            if (!arc.Landed && arc.Age >= arc.Duration)
            {
                LandArc(ref arc);
            }
            if (!IsArcDone(arc))
            {
                _arcs[write++] = arc;
            }
        }
        _arcCount = write;
        write = 0;
        for (var i = 0; i < _ringCount; i++)
        {
            ref var ring = ref _rings[i];
            ring.Age += dt;
            if (ring.Age < ring.Duration)
            {
                _rings[write++] = ring;
            }
        }
        _ringCount = write;
        moving |= _arcCount > 0 || _ringCount > 0;
        return moving;
    }

    /// <summary>
    /// The visible part of an arc as fractional point indices (0 … 56) and the alpha multiplier of its fade-out.
    /// Flight: tail 0, head on cubic-bezier(.65,0,.35,1); hold 350 ms; then the tail chases the head in 435 ms (in-out)
    /// while the alpha falls to 0.4 + 0.6 × what is left.
    /// </summary>
    internal static void ArcSpan(double age, double duration, out double tail, out double head, out double fade, out bool flying)
    {
        const double last = RevenueGlobeMath.ArcPoints - 1;
        flying = age < duration;
        if (flying)
        {
            tail = 0;
            head = last * RevenueGlobeMath.EaseInOut(Math.Max(0, age) / duration);
            fade = 1;
            return;
        }
        head = last;
        var chase = (age - duration - RevenueGlobeMath.ArcHold) / RevenueGlobeMath.ArcTail;
        if (chase <= 0)
        {
            tail = 0;
            fade = 1;
            return;
        }
        var share = RevenueGlobeMath.EaseInOut(Math.Min(1, chase));
        tail = last * share;
        fade = 0.4 + 0.6 * (1 - share);
    }

    private static bool IsArcDone(in ArcState arc)
    {
        ArcSpan(arc.Age, arc.Duration, out var tail, out var head, out _, out var flying);
        return !flying && head - tail <= 2;
    }

    // Input -----------------------------------------------------------------------------------------------------------------

    private void UpdateDragScale()
    {
        var r = Math.Max(1, Radius);
        _camera.YawPerPixel = -1 / r;
        _camera.PitchPerPixel = 1 / r;
    }

    protected override object? HitTestItem(Point position) => CityAt(position) is var i and >= 0 ? _scene.Cities[i] : null;

    /// <summary>The nearest front-side city whose dot is within 11px + its radius of <paramref name="position"/> (−1 for none).</summary>
    public int CityAt(Point position)
    {
        if (Bounds.Width <= 0 || _scene.Cities.Length == 0)
        {
            return -1;
        }
        var view = View;
        var scale = view.Radius / RevenueGlobeMath.ReferenceRadius;
        var best = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _scene.CityPoints.Length; i++)
        {
            var depth = view.Project(_scene.CityPoints[i], out var p);
            if (depth <= 0)
            {
                continue;
            }
            var r = RevenueGlobeMath.CityDotRadius(_scene.CityRevenue[i], _scene.MaxCityRevenue) * scale;
            var dx = p.X - position.X;
            var dy = p.Y - position.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < RevenueGlobeMath.HitSlop + r && d < bestDistance)
            {
                best = i;
                bestDistance = d;
            }
        }
        return best;
    }

    protected override void OnHoverChanged(Point? position, object? item)
    {
        var index = item is RevenueCity city ? Array.IndexOf(_scene.Cities, city) : -1;
        if (index == _hoverCity)
        {
            return;
        }
        _hoverCity = index;
        InvalidateVisual();
        HoverCityChanged?.Invoke(index);
    }

    protected override void OnTap(Point position, object? item, KeyModifiers modifiers)
    {
        var index = item is RevenueCity city ? Array.IndexOf(_scene.Cities, city) : -1;
        if (index >= 0)
        {
            CityTapped?.Invoke(index);
        }
        else
        {
            BackgroundTapped?.Invoke();
        }
    }

    protected override void OnDragStarted(Point position)
    {
        UpdateDragScale();
        base.OnDragStarted(position);
        DragBegan?.Invoke();
    }

    protected override void OnDragMoved(Vector delta, Point position)
    {
        UpdateDragScale();
        base.OnDragMoved(delta, position);
    }

    protected override void OnDragCompleted(Vector velocity)
    {
        UpdateDragScale();
        base.OnDragCompleted(velocity);
    }

    protected override bool OnStageKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }
        // Arrow keys give the globe a push that glides out (← turns the surface left, so the center longitude grows).
        const double yawKick = 55 * Math.PI / 180;
        const double pitchKick = 45 * Math.PI / 180;
        switch (e.Key)
        {
            case Key.Left:
                _camera.Impulse(yawKick);
                RequestFrames();
                return true;
            case Key.Right:
                _camera.Impulse(-yawKick);
                RequestFrames();
                return true;
            case Key.Up:
                _camera.Impulse(0, -pitchKick);
                RequestFrames();
                return true;
            case Key.Down:
                _camera.Impulse(0, pitchKick);
                RequestFrames();
                return true;
            case Key.Escape:
                EscapePressed?.Invoke();
                return true;
        }
        return false;
    }

    // The globe does not zoom: no wheel (PassThrough), no + / − / 0 keys, no reset.
    protected override void OnZoom(double factor, Point? anchor, ShowpieceZoomSource source)
    {
    }

    protected override void OnResetView()
    {
    }

    protected override void OnMotionLevelChanged(ArcMotionLevel level) => MotionLevelChangedEvent?.Invoke(level);

    protected override void OnThemeChanged()
    {
        _colorsValid = false;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ViewChanged?.Invoke();
    }

    private void OnCameraViewChanged() => ViewChanged?.Invoke();

    private struct ArcState
    {
        public int City;
        public int Payment;
        public double Weight;
        public double Duration;
        public double Age;
        public bool Landed;
    }

    private struct RingState
    {
        public ShowpieceVec3 Center;
        public ShowpieceVec3 U;
        public ShowpieceVec3 V;
        public double Duration;
        public double Angle;
        public double Age;
        public bool Arrival;
    }
}
