namespace TKCode.Arc;

/// <summary>
/// The charge curve of <see cref="ChargeSession"/> (Arc Charge session, design reference: fitted from the public preview's
/// 201-point plot, worst error 2e-4): the share of the peak power a pack takes at a state of charge. It rises from 62% at
/// empty to the full peak at 10% (a smoothstep), stays flat up to the taper point, then falls off as
/// <c>0.06 + 0.94·exp(−3.2·u^1.4)</c> with <c>u</c> running from 0 at the taper point to 1 at full.
/// </summary>
internal static class ChargeSessionCurve
{
    /// <summary>Steps of the time-to-limit integral (midpoint rule, as measured).</summary>
    public const int TimeSteps = 48;

    /// <summary>Simulated seconds per second on screen at speed 1 (one second shows forty seconds of charging).</summary>
    public const double TimeScale = 40;

    /// <summary>Hermite smoothstep of <paramref name="x"/> between <paramref name="edge0"/> and <paramref name="edge1"/>.</summary>
    public static double Smoothstep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// The default curve: power share (0..1) at state of charge <paramref name="soc"/> (0..1) for a taper starting at
    /// <paramref name="taperAt"/> (0..1).
    /// </summary>
    public static double Default(double soc, double taperAt)
    {
        if (soc < 0.10)
        {
            return 0.62 + 0.38 * Smoothstep(0, 0.10, soc);
        }
        if (soc < taperAt || taperAt >= 1)
        {
            return 1;
        }
        var u = Math.Clamp((soc - taperAt) / (1 - taperAt), 0, 1);
        return 0.06 + 0.94 * Math.Exp(-3.2 * Math.Pow(u, 1.4));
    }

    /// <summary>
    /// Minutes of charging from <paramref name="charge"/> to <paramref name="limit"/> (both in percent) at full availability:
    /// the midpoint sum of <c>capacity·Δs / (maxPower·f(s))</c> over <see cref="TimeSteps"/> steps; 0 when the limit is
    /// not above the charge.
    /// </summary>
    public static double MinutesToLimit(double charge, double limit, double capacity, double maxPower, Func<double, double> curve)
    {
        if (limit <= charge || capacity <= 0 || maxPower <= 0)
        {
            return 0;
        }
        var ds = (limit - charge) / 100 / TimeSteps;
        var hours = 0.0;
        for (var i = 0; i < TimeSteps; i++)
        {
            var s = charge / 100 + (i + 0.5) * ds;
            var share = Math.Max(curve(s), 1e-4);
            hours += capacity * ds / (maxPower * share);
        }
        return hours * 60;
    }

    /// <summary>
    /// How fast the charge climbs, in percent per second on screen: <c>P·speed·(40/3600)·100/capacity</c> for a power
    /// <paramref name="power"/> in kW.
    /// </summary>
    public static double PercentPerSecond(double power, double capacity, double speed) =>
        capacity <= 0 ? 0 : power * speed * (TimeScale / 3600) * 100 / capacity;
}
