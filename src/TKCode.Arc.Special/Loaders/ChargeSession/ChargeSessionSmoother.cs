namespace TKCode.Arc;

/// <summary>
/// A first-order exponential follower (a low-pass toward a target with time constant τ), exact for any frame length:
/// <c>value += (target − value)·(1 − exp(−dt/τ))</c>. <see cref="ChargeSession"/> uses it for availability (τ 0.384 s up,
/// 0.237 s down) and the landing flash (τ 0.2 s toward zero). Pure and allocation-free; other loaders that fade a level
/// on a clock can use it as is.
/// </summary>
internal static class ChargeSessionSmoother
{
    /// <summary>Moves <paramref name="value"/> toward <paramref name="target"/> over <paramref name="dt"/> seconds.</summary>
    public static double Approach(double value, double target, double dt, double tau)
    {
        if (dt <= 0)
        {
            return value;
        }
        if (tau <= 0)
        {
            return target;
        }
        return value + (target - value) * (1 - Math.Exp(-dt / tau));
    }
}
