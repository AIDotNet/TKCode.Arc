namespace TKCode.Arc;

/// <summary>
/// Exponential glide after a release (and first-order smoothing), in two parameterizations: a per-frame factor at 60fps
/// as measured in the browser (velocity × factor every 16.7ms) or a time constant τ (velocity × e^(−t/τ)). Both describe
/// the same curve: <c>τ = −(1/60) / ln(factor)</c>. Steps integrate exactly, so the glide does not depend on the frame
/// rate. Bounds are handled by the cameras: an axis that reaches its bound stops gliding and springs back with the
/// remaining velocity (see <see cref="ShowpieceCamera2D"/>).
/// </summary>
internal struct ShowpieceInertia
{
    /// <summary>Semantic zoom pan: × 0.941 per frame (τ ≈ 274ms; a 290px/s release glides about 80px in 1.3s).</summary>
    public const double PanFrameFactor = 0.941;

    /// <summary>Activity terrain orbit: × 0.948 per frame (τ ≈ 0.31s; 96.6°/s glides about 30°).</summary>
    public const double OrbitFrameFactor = 0.948;

    /// <summary>Revenue globe spin: × 0.987 per frame (τ ≈ 1.3s).</summary>
    public const double GlobeFrameFactor = 0.987;

    /// <summary>Semantic zoom hover outline: × 0.83 per frame (τ ≈ 90ms).</summary>
    public const double HoverFrameFactor = 0.83;

    /// <summary>Semantic zoom dimming and level-of-detail smoothing: × 0.885 per frame (τ ≈ 140ms).</summary>
    public const double DimFrameFactor = 0.885;

    /// <summary>The frame interval the factors were measured at (60fps).</summary>
    public const double FrameSeconds = 1.0 / 60.0;

    /// <summary>Time constant (s) of a per-frame factor measured at 60fps.</summary>
    public static double TimeConstantFromFrameFactor(double factor)
    {
        if (!(factor > 0 && factor < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "The factor must be in (0, 1).");
        }
        return -FrameSeconds / Math.Log(factor);
    }

    /// <summary>The per-frame factor at 60fps of a time constant (s).</summary>
    public static double FrameFactorFromTimeConstant(double timeConstant)
    {
        if (!(timeConstant > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(timeConstant));
        }
        return Math.Exp(-FrameSeconds / timeConstant);
    }

    /// <summary>Total distance a release at <paramref name="velocity"/> glides before it stops: <c>v·τ</c>.</summary>
    public static double GlideDistance(double velocity, double timeConstant) => velocity * timeConstant;

    /// <summary>
    /// First-order approach (the canvases' hover growth, dimming, fades): moves <paramref name="current"/> toward
    /// <paramref name="target"/> by <c>1 − e^(−dt/τ)</c> of the gap. Snaps within <paramref name="snap"/>.
    /// </summary>
    public static double Approach(double current, double target, double timeConstant, double dt, double snap = 1e-3)
    {
        if (timeConstant <= 0 || dt <= 0)
        {
            return dt > 0 ? target : current;
        }
        var next = target + (current - target) * Math.Exp(-dt / timeConstant);
        return Math.Abs(next - target) <= snap ? target : next;
    }

    public ShowpieceInertia(double timeConstant, double stopSpeed)
    {
        TimeConstant = timeConstant;
        StopSpeed = stopSpeed;
        Velocity = 0;
    }

    /// <summary>Current velocity (units per second); 0 when not gliding.</summary>
    public double Velocity;

    /// <summary>τ in seconds.</summary>
    public double TimeConstant;

    /// <summary>Speed (units per second) under which the glide stops.</summary>
    public double StopSpeed;

    /// <summary>A glide is running.</summary>
    public readonly bool IsActive => Velocity != 0;

    /// <summary>Starts a glide (a release velocity under <see cref="StopSpeed"/> does not glide).</summary>
    public void Start(double velocity) => Velocity = Math.Abs(velocity) > StopSpeed && TimeConstant > 0 ? velocity : 0;

    /// <summary>Stops the glide.</summary>
    public void Stop() => Velocity = 0;

    /// <summary>
    /// Advances by <paramref name="dt"/> seconds and returns the distance travelled in that time (the exact integral
    /// <c>v·τ·(1 − e^(−dt/τ))</c>); the velocity decays by <c>e^(−dt/τ)</c> and drops to 0 under <see cref="StopSpeed"/>.
    /// </summary>
    public double Step(double dt)
    {
        if (Velocity == 0 || dt <= 0)
        {
            return 0;
        }
        var decay = Math.Exp(-dt / TimeConstant);
        var distance = Velocity * TimeConstant * (1 - decay);
        Velocity *= decay;
        if (Math.Abs(Velocity) <= StopSpeed)
        {
            Velocity = 0;
        }
        return distance;
    }
}
