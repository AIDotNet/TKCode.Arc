namespace TKCode.Arc.Gestures;

/// <summary>
/// Resistance past a bound (S10). <see cref="Apply"/> is the iOS curve Arc uses: it follows the finger at first and
/// approaches <c>dimension</c> asymptotically: <c>(1 − 1/(x·factor/d + 1))·d</c>.
/// </summary>
public static class RubberBand
{
    /// <summary>Arc's resistance constant.</summary>
    public const double DefaultFactor = 0.55;

    /// <summary>
    /// Maps an overflow distance (past the bound, any sign) to the displayed stretch. The result keeps the sign of
    /// <paramref name="overflow"/> and its magnitude stays below <paramref name="dimension"/>.
    /// </summary>
    public static double Apply(double overflow, double dimension, double factor = DefaultFactor)
    {
        if (dimension <= 0 || factor <= 0 || overflow == 0 || double.IsNaN(overflow))
        {
            return 0;
        }
        var x = Math.Abs(overflow);
        var stretch = (1 - 1 / (x * factor / dimension + 1)) * dimension;
        return Math.CopySign(stretch, overflow);
    }

    /// <summary>
    /// Inverse of <see cref="Apply"/>: the finger distance that produces <paramref name="stretch"/>. Use it to grab an
    /// overstretched surface mid-flight without a jump.
    /// </summary>
    public static double Invert(double stretch, double dimension, double factor = DefaultFactor)
    {
        if (dimension <= 0 || factor <= 0 || stretch == 0 || double.IsNaN(stretch))
        {
            return 0;
        }
        var s = Math.Min(Math.Abs(stretch), dimension * (1 - 1e-6));
        var x = (1 / (1 - s / dimension) - 1) * dimension / factor;
        return Math.CopySign(x, stretch);
    }

    /// <summary>
    /// Clamps <paramref name="value"/> to [<paramref name="min"/>, <paramref name="max"/>] with rubber-band resistance
    /// outside the range instead of a hard stop.
    /// </summary>
    public static double Clamp(double value, double min, double max, double dimension, double factor = DefaultFactor)
    {
        if (value < min)
        {
            return min + Apply(value - min, dimension, factor);
        }
        if (value > max)
        {
            return max + Apply(value - max, dimension, factor);
        }
        return value;
    }

    /// <summary>Square-root decay (Arc's drawer when pulled away from its edge): <c>sign·√|x|</c>.</summary>
    public static double Sqrt(double overflow) => overflow == 0 ? 0 : Math.CopySign(Math.Sqrt(Math.Abs(overflow)), overflow);
}
