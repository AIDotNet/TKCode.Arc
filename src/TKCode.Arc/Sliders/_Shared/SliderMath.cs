using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The arithmetic both sliders share (Arc's slider.tsx / elastic-slider.tsx helpers). Thumb positions are kept in
/// per-mille of the track (0..1000), so springs settle well below a pixel at any width and resizing needs no work.
/// </summary>
internal static class SliderMath
{
    /// <summary>Position units across the whole track.</summary>
    public const double Span = 1000;

    /// <summary>
    /// iOS style resistance: travel past a limit gives less and less, and never more than <paramref name="limit"/> pixels.
    /// <c>sign(d) · (1 − 1 / (|d| · 0.55 / limit + 1)) · limit</c>.
    /// </summary>
    public static double Rubber(double distance, double limit)
    {
        if (limit <= 0 || distance == 0)
        {
            return 0;
        }
        return Math.Sign(distance) * (1 - 1 / (Math.Abs(distance) * 0.55 / limit + 1)) * limit;
    }

    /// <summary>Where a release is heading from a scroll-like deceleration (rate 0.99), in the velocity's units.</summary>
    public static double Project(double velocity) => velocity * 0.099;

    /// <summary>Number of decimals written in <paramref name="value"/> (0.25 → 2).</summary>
    public static int DecimalsOf(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponent = text.IndexOfAny(['E', 'e']);
        if (exponent >= 0)
        {
            return Math.Max(0, -int.Parse(text[(exponent + 1)..], CultureInfo.InvariantCulture));
        }
        var dot = text.IndexOf('.');
        return dot < 0 ? 0 : text.Length - dot - 1;
    }

    /// <summary>Rounds to the step grid that starts at <paramref name="min"/>.</summary>
    public static double Snap(double value, double min, double step, int decimals)
    {
        // JavaScript Math.round: halves round up.
        var snapped = min + Math.Floor((value - min) / step + 0.5) * step;
        return Math.Round(snapped, Math.Clamp(decimals, 0, 15));
    }

    /// <summary>Arc's default large step: a tenth of the range on the step grid, never less than one step.</summary>
    public static double DefaultLargeStep(double min, double max, double step, int decimals)
        => Math.Max(step, Snap(min + (max - min) / 10, min, step, decimals) - min);

    /// <summary>Arc's default text: en-US grouping with the step's (or minimum's) decimals.</summary>
    public static string Format(double value, int decimals)
        => value.ToString("N" + Math.Clamp(decimals, 0, 15), CultureInfo.GetCultureInfo("en-US"));
}

/// <summary>Release velocity from the last few pointer samples (six, like Arc), without allocating per sample.</summary>
internal sealed class SliderVelocityTracker
{
    private const int Capacity = 6;
    private readonly ulong[] _times = new ulong[Capacity];
    private readonly double[] _positions = new double[Capacity];
    private int _count;
    private int _next;

    public void Reset()
    {
        _count = 0;
        _next = 0;
    }

    public void Add(ulong time, double position)
    {
        _times[_next] = time;
        _positions[_next] = position;
        _next = (_next + 1) % Capacity;
        _count = Math.Min(Capacity, _count + 1);
    }

    /// <summary>
    /// Units per second over the kept samples; a pointer that paused before letting go (more than 60ms since the last
    /// sample) or samples closer than 8ms carry none.
    /// </summary>
    public double Velocity(ulong now)
    {
        if (_count < 2)
        {
            return 0;
        }
        var first = (_next - _count + Capacity) % Capacity;
        var last = (_next - 1 + Capacity) % Capacity;
        var elapsed = (_times[last] - _times[first]) / 1000.0;
        if (elapsed <= 0.008 || (now > _times[last] && now - _times[last] >= 60))
        {
            return 0;
        }
        return (_positions[last] - _positions[first]) / elapsed;
    }
}
