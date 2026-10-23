using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The van Wijk–Nuij smooth zoom-and-pan path ("Smooth and efficient zooming and panning", 2003; the same family as
/// d3's <c>interpolateZoom</c>): a camera described by its center and visible width <c>w</c> (world units across the
/// viewport) travels so that a long trip first zooms out, pans, then zooms in, at a perceptually even pace. <see cref="S"/>
/// is the path length; Semantic zoom times its flights as <c>460 + 380·S</c> ms and eases the path parameter with
/// <c>cubic-bezier(.65, 0, .35, 1)</c> (<see cref="Motion.ArcEasings.InOut"/>).
/// </summary>
internal readonly struct VanWijkPath
{
    /// <summary>The curvature ρ = √2 (d3's default; fitted on Semantic zoom).</summary>
    public static readonly double DefaultRho = Math.Sqrt(2);

    /// <summary>Semantic zoom flight time: 460ms plus <see cref="DurationPerUnitMs"/> per unit of <see cref="S"/>.</summary>
    public const double DurationBaseMs = 460;

    /// <summary>Semantic zoom flight time per unit of path length.</summary>
    public const double DurationPerUnitMs = 380;

    private const double Epsilon2 = 1e-12;

    private readonly double _ux0, _uy0, _w0, _dx, _dy, _d1, _w1, _r0, _rho, _rho2;
    private readonly bool _zoomOnly;

    private VanWijkPath(Point from, double w0, Point to, double w1, double rho)
    {
        From = from;
        To = to;
        FromWidth = w0;
        ToWidth = w1;
        _ux0 = from.X;
        _uy0 = from.Y;
        _w0 = w0;
        _w1 = w1;
        _rho = rho;
        _rho2 = rho * rho;
        _dx = to.X - from.X;
        _dy = to.Y - from.Y;
        var d2 = _dx * _dx + _dy * _dy;
        if (d2 < Epsilon2)
        {
            _zoomOnly = true;
            _d1 = 0;
            _r0 = 0;
            S = Math.Log(w1 / w0) / rho;
        }
        else
        {
            _zoomOnly = false;
            var rho4 = _rho2 * _rho2;
            _d1 = Math.Sqrt(d2);
            var b0 = (w1 * w1 - w0 * w0 + rho4 * d2) / (2 * w0 * _rho2 * _d1);
            var b1 = (w1 * w1 - w0 * w0 - rho4 * d2) / (2 * w1 * _rho2 * _d1);
            _r0 = Math.Log(Math.Sqrt(b0 * b0 + 1) - b0);
            var r1 = Math.Log(Math.Sqrt(b1 * b1 + 1) - b1);
            S = (r1 - _r0) / rho;
        }
    }

    /// <summary>Creates the path between two views (centers in world units, widths > 0).</summary>
    public static VanWijkPath Create(Point from, double fromWidth, Point to, double toWidth, double? rho = null)
    {
        if (!(fromWidth > 0) || !(toWidth > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(fromWidth), "Widths must be positive.");
        }
        return new VanWijkPath(from, fromWidth, to, toWidth, rho ?? DefaultRho);
    }

    public Point From { get; }

    public Point To { get; }

    public double FromWidth { get; }

    public double ToWidth { get; }

    /// <summary>The path length (signed for a pure zoom: negative when zooming in). Use <see cref="Length"/> for timing.</summary>
    public double S { get; }

    /// <summary>|S|.</summary>
    public double Length => Math.Abs(S);

    /// <summary>The Semantic zoom flight duration: <c>460 + 380·|S|</c> ms.</summary>
    public TimeSpan Duration => DurationFor(Length);

    /// <summary><c>460 + 380·s</c> ms.</summary>
    public static TimeSpan DurationFor(double length) =>
        TimeSpan.FromMilliseconds(DurationBaseMs + DurationPerUnitMs * Math.Abs(length));

    /// <summary>The view at path parameter <paramref name="t"/> (0 = from, 1 = to; ease it before calling).</summary>
    public (Point Center, double Width) At(double t)
    {
        if (t <= 0)
        {
            return (From, FromWidth);
        }
        if (t >= 1)
        {
            return (To, ToWidth);
        }
        if (_zoomOnly)
        {
            return (new Point(_ux0 + t * _dx, _uy0 + t * _dy), _w0 * Math.Exp(_rho * t * S));
        }
        var s = t * S;
        var coshR0 = Math.Cosh(_r0);
        var u = _w0 / (_rho2 * _d1) * (coshR0 * Math.Tanh(_rho * s + _r0) - Math.Sinh(_r0));
        return (new Point(_ux0 + u * _dx, _uy0 + u * _dy), _w0 * coshR0 / Math.Cosh(_rho * s + _r0));
    }
}
