namespace TKCode.Arc;

/// <summary>
/// The measured curves of Arc's Revenue globe (design reference): payment arcs, rings, dots and the timeline. Pure
/// functions so the tests can hold them to the measurements. Angles in radians unless the name says degrees, times in
/// seconds.
/// </summary>
internal static class RevenueGlobeMath
{
    /// <summary>Globe radius as a share of the smaller side of the globe area.</summary>
    public const double RadiusFactor = 0.41;

    /// <summary>The radius the dot sizes were measured at (663 × 437 globe area); sizes scale with R / this.</summary>
    public const double ReferenceRadius = 179.36;

    /// <summary>Points of a whole arc (56 segments).</summary>
    public const int ArcPoints = 57;

    /// <summary>Depth buckets of the land and sea dots.</summary>
    public const int Buckets = 8;

    /// <summary>Time an arc stays whole after its head lands.</summary>
    public const double ArcHold = 0.35;

    /// <summary>Time the tail takes to chase the head into HQ.</summary>
    public const double ArcTail = 0.435;

    /// <summary>Launch ring at the source city: lifetime and angular radius.</summary>
    public const double LaunchRingDuration = 0.8;

    public const double LaunchRingRadius = 1.3 * Math.PI / 180;

    /// <summary>Arrival ring at HQ: lifetime (live; shorter while replaying) and angular radius.</summary>
    public const double ArrivalRingDuration = 1.0;

    public const double ReplayArrivalRingDuration = 0.6;

    public const double ArrivalRingRadius = 3.1 * Math.PI / 180;

    /// <summary>Screen radius of the hover / selection ring and the hit radius beyond a city dot.</summary>
    public const double CityRingRadius = 7.5;

    public const double HitSlop = 11;

    /// <summary>Alpha of the arcs, heads and their back-side tiers (× ¼, ½, ¾, 1).</summary>
    public const double ArcAlpha = 0.92;

    /// <summary>Alpha of the still arcs drawn under reduced motion.</summary>
    public const double StillArcAlpha = 0.70;

    public const double StillArcWidth = 2;

    /// <summary>Land dot radius at the reference radius: 0.76 + 0.04 per bucket.</summary>
    public static double LandDotRadius(int bucket) => 0.76 + 0.04 * bucket;

    /// <summary>Land dot alpha: 0.04, 0.11, 0.19, 0.26, 0.34, 0.41, 0.49, 0.56.</summary>
    public static double LandDotAlpha(int bucket) => s_landAlpha[Math.Clamp(bucket, 0, Buckets - 1)];

    /// <summary>Sea square alpha: 0.01 … 0.21 (the squares are 1.28px at the reference radius).</summary>
    public static double OceanDotAlpha(int bucket) => s_oceanAlpha[Math.Clamp(bucket, 0, Buckets - 1)];

    public const double OceanSquareSize = 1.28;

    private static readonly double[] s_landAlpha = [0.04, 0.11, 0.19, 0.26, 0.34, 0.41, 0.49, 0.56];
    private static readonly double[] s_oceanAlpha = [0.01, 0.04, 0.07, 0.10, 0.12, 0.15, 0.18, 0.21];

    /// <summary>The depth bucket of a front-side dot (depth in (0, 1]): floor(8.4·z) clamped to 0…7.</summary>
    public static int Bucket(double depth) => Math.Clamp((int)(depth * 8.4), 0, Buckets - 1);

    /// <summary>City dot radius at the reference radius: 1.8 + 1.6·√(revenue / largest revenue).</summary>
    public static double CityDotRadius(double revenue, double maxRevenue)
        => 1.8 + 1.6 * Math.Sqrt(maxRevenue > 0 ? Math.Clamp(revenue / maxRevenue, 0, 1) : 0);

    /// <summary>City dot alpha by depth: 0.18 + 0.54·z.</summary>
    public static double CityDotAlpha(double depth) => 0.18 + 0.54 * Math.Clamp(depth, 0, 1);

    /// <summary>Alpha of the hover / selection ring by depth (0.70 – 0.97 on the visible side).</summary>
    public static double CityRingAlpha(double depth) => 0.25 + 0.72 * Math.Clamp(depth, 0, 1);

    /// <summary>HQ dot and label alpha by depth: 0.36 + 0.64·z.</summary>
    public static double HqAlpha(double depth) => 0.36 + 0.64 * Math.Clamp(depth, 0, 1);

    /// <summary>Arc lift at its middle, in globe radii: 0.04 + 0.077·θ (θ = angle between the cities).</summary>
    public static double ArcLift(double theta) => 0.04 + 0.077 * theta;

    /// <summary>Lift along an arc: <see cref="ArcLift"/> × sin(π·s).</summary>
    public static double ArcLiftAt(double theta, double s) => ArcLift(theta) * Math.Sin(Math.PI * s);

    /// <summary>Flight time of an arc's head: 970 ms + 5.9 ms per degree.</summary>
    public static double FlightDuration(double theta) => 0.970 + 0.0059 * (theta * 180 / Math.PI);

    /// <summary>Arc weight from the amount: clamp(√(amount / 400), 0.09, 1); line width 1 + w, head radius 2 + 0.9·w.</summary>
    public static double ArcWeight(double amount) => Math.Clamp(Math.Sqrt(Math.Max(0, amount) / 400), 0.09, 1);

    /// <summary>Back-side tier of a lifted arc point by depth: 4 in front, then 3, 2, 1 farther behind the globe.</summary>
    public static int ArcTier(double depth) => depth >= 0 ? 4 : depth >= -0.1 ? 3 : depth >= -0.2 ? 2 : 1;

    /// <summary>easeOutExpo: 1 − 2^(−10t) (rings).</summary>
    public static double EaseOutExpo(double t) => t >= 1 ? 1 : t <= 0 ? 0 : 1 - Math.Pow(2, -10 * t);

    /// <summary>The fading of a ring: (1 − t)^1.5.</summary>
    public static double RingFade(double t) => t >= 1 ? 0 : Math.Pow(1 - Math.Max(0, t), 1.5);

    /// <summary>The arc head's progress curve, CSS cubic-bezier(.65, 0, .35, 1).</summary>
    public static double EaseInOut(double t) => CubicBezier(0.65, 0, 0.35, 1, t);

    /// <summary>A CSS cubic-bezier timing function at <paramref name="t"/>.</summary>
    public static double CubicBezier(double x1, double y1, double x2, double y2, double t)
    {
        if (t <= 0)
        {
            return 0;
        }
        if (t >= 1)
        {
            return 1;
        }
        // Newton on x(u) = t, falling back to bisection.
        var u = t;
        for (var i = 0; i < 8; i++)
        {
            var x = Bezier(x1, x2, u) - t;
            if (Math.Abs(x) < 1e-7)
            {
                return Bezier(y1, y2, u);
            }
            var d = BezierSlope(x1, x2, u);
            if (Math.Abs(d) < 1e-6)
            {
                break;
            }
            u -= x / d;
        }
        double lo = 0, hi = 1;
        u = t;
        for (var i = 0; i < 40; i++)
        {
            var x = Bezier(x1, x2, u);
            if (Math.Abs(x - t) < 1e-7)
            {
                break;
            }
            if (x < t)
            {
                lo = u;
            }
            else
            {
                hi = u;
            }
            u = (lo + hi) / 2;
        }
        return Bezier(y1, y2, u);
    }

    private static double Bezier(double p1, double p2, double u)
    {
        var v = 1 - u;
        return 3 * v * v * u * p1 + 3 * v * u * u * p2 + u * u * u;
    }

    private static double BezierSlope(double p1, double p2, double u)
    {
        var v = 1 - u;
        return 3 * v * v * p1 + 6 * v * u * (p2 - p1) + 3 * u * u * (1 - p2);
    }

    /// <summary>Angle between two unit vectors.</summary>
    public static double Angle(ShowpieceVec3 a, ShowpieceVec3 b)
        => Math.Atan2(a.Cross(b).Length, a.Dot(b));

    /// <summary>Spherical interpolation between unit vectors (linear when they nearly coincide; any s, also outside 0…1).</summary>
    public static ShowpieceVec3 Slerp(ShowpieceVec3 a, ShowpieceVec3 b, double theta, double s)
    {
        if (theta < 1e-6)
        {
            return (a * (1 - s) + b * s).Normalized();
        }
        var sin = Math.Sin(theta);
        var wa = Math.Sin((1 - s) * theta) / sin;
        var wb = Math.Sin(s * theta) / sin;
        return a * wa + b * wb;
    }

    /// <summary>Latitude and longitude in radians of a unit vector.</summary>
    public static (double Latitude, double Longitude) ToLatLon(ShowpieceVec3 u)
        => (Math.Asin(Math.Clamp(u.Z, -1, 1)), Math.Atan2(u.Y, u.X));

    /// <summary>
    /// The lifted and ground points of an arc from <paramref name="from"/> to <paramref name="to"/> (x, y, z triples,
    /// <see cref="ArcPoints"/> each): ground on the great circle, lifted = ground × (1 + lift × sin(π·s)).
    /// </summary>
    public static void BuildArc(ShowpieceVec3 from, ShowpieceVec3 to, float[] lifted, float[] ground, out double theta)
    {
        theta = Angle(from, to);
        var lift = ArcLift(theta);
        for (var i = 0; i < ArcPoints; i++)
        {
            var s = i / (double)(ArcPoints - 1);
            var g = Slerp(from, to, theta, s);
            var h = 1 + lift * Math.Sin(Math.PI * s);
            ground[i * 3] = (float)g.X;
            ground[i * 3 + 1] = (float)g.Y;
            ground[i * 3 + 2] = (float)g.Z;
            lifted[i * 3] = (float)(g.X * h);
            lifted[i * 3 + 1] = (float)(g.Y * h);
            lifted[i * 3 + 2] = (float)(g.Z * h);
        }
    }

    /// <summary>
    /// Two unit vectors orthogonal to <paramref name="center"/> (and each other) for drawing small circles around it.
    /// </summary>
    public static (ShowpieceVec3 U, ShowpieceVec3 V) Tangents(ShowpieceVec3 center)
    {
        var helper = Math.Abs(center.Z) < 0.9 ? new ShowpieceVec3(0, 0, 1) : new ShowpieceVec3(1, 0, 0);
        var u = helper.Cross(center).Normalized();
        var v = center.Cross(u).Normalized();
        return (u, v);
    }

    /// <summary>
    /// The revenue-weighted center of points on the sphere (normalized sum); the plain center when every weight is
    /// zero; null when there are no points.
    /// </summary>
    public static ShowpieceVec3? Centroid(ReadOnlySpan<ShowpieceVec3> points, ReadOnlySpan<double> weights)
    {
        if (points.Length == 0)
        {
            return null;
        }
        var sum = ShowpieceVec3.Zero;
        var total = 0.0;
        for (var i = 0; i < points.Length; i++)
        {
            var w = i < weights.Length ? Math.Max(0, weights[i]) : 0;
            sum += points[i] * w;
            total += w;
        }
        if (total <= 0 || sum.Length < 1e-9)
        {
            sum = ShowpieceVec3.Zero;
            foreach (var p in points)
            {
                sum += p;
            }
        }
        return sum.Length < 1e-9 ? points[0] : sum.Normalized();
    }
}

/// <summary>
/// A great-circle camera flight on the globe (<see cref="ShowpieceOrbitCamera.FlyAlong"/>): the view direction slerps
/// from the current center to the target (no cross-track drift; a long flight bulges toward the pole, 22° → 54° →
/// target), longitude stays continuous, and the zoom dips mid-flight: lerp(z0, z1, s) − 0.052·θ·sin(π·s).
/// </summary>
internal sealed class RevenueGlobeFlightPath : IShowpieceOrbitPath
{
    /// <summary>Zoom dip per radian of flight.</summary>
    public const double ZoomDip = 0.052;

    private readonly ShowpieceVec3 _from;
    private readonly ShowpieceVec3 _to;
    private readonly double _fromZoom;
    private readonly double _toZoom;
    private double _lastYaw;

    public RevenueGlobeFlightPath(double fromLongitude, double fromLatitude, double fromZoom, double toLongitude, double toLatitude, double toZoom)
    {
        _from = Unit(fromLatitude, fromLongitude);
        _to = Unit(toLatitude, toLongitude);
        _fromZoom = fromZoom;
        _toZoom = toZoom;
        Theta = RevenueGlobeMath.Angle(_from, _to);
        _lastYaw = fromLongitude;
        FromLongitude = fromLongitude;
    }

    /// <summary>Angle of the flight.</summary>
    public double Theta { get; }

    /// <summary>The (unwrapped) longitude the flight starts at.</summary>
    public double FromLongitude { get; }

    private static ShowpieceVec3 Unit(double latitude, double longitude)
    {
        var c = Math.Cos(latitude);
        return new ShowpieceVec3(c * Math.Cos(longitude), c * Math.Sin(longitude), Math.Sin(latitude));
    }

    /// <summary>The zoom at <paramref name="s"/>.</summary>
    public double ZoomAt(double s) => _fromZoom + (_toZoom - _fromZoom) * s - ZoomDip * Theta * Math.Sin(Math.PI * Math.Clamp(s, 0, 1));

    public ShowpieceOrbitPose Evaluate(double progress)
    {
        var u = RevenueGlobeMath.Slerp(_from, _to, Theta, progress);
        var (latitude, longitude) = RevenueGlobeMath.ToLatLon(u);
        // Keep the yaw continuous with the previous step (a path near a pole swings the longitude quickly).
        var yaw = _lastYaw + ShowpieceProjection.WrapAngle(longitude - _lastYaw);
        if (Math.Cos(latitude) < 1e-6)
        {
            yaw = _lastYaw;
        }
        _lastYaw = yaw;
        return new ShowpieceOrbitPose { Yaw = yaw, Pitch = latitude, Zoom = ZoomAt(progress) };
    }
}
