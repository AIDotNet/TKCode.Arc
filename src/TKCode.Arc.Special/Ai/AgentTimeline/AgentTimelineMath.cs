using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The numbers of an <see cref="AgentTimeline"/>, as pure functions: time, token and cost formats (rounded the way the
/// reference rounds them, half away from zero), the speedup, and the axis rules: the tick step and the calm steps a live
/// axis grows in.
/// </summary>
internal static class AgentTimelineMath
{
    /// <summary>Nice tick steps in seconds, smallest first.</summary>
    private static readonly double[] s_steps =
    [
        0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200, 10800, 21600, 43200, 86400
    ];

    /// <summary>At most this many ticks (0 included) on the axis: 26.4 s shows 0 to 25 s in 5 s steps.</summary>
    public const int MaxTicks = 6;

    /// <summary>Ticks closer than this (px) take the next step up.</summary>
    public const double MinTickSpacing = 40;

    /// <summary>The live axis grows once the time passes this share of it.</summary>
    public const double LiveGrowAt = 0.9;

    /// <summary>The live axis grows to a nice length past this multiple of the time.</summary>
    public const double LiveGrowBy = 1.5;

    /// <summary>The shortest live axis in seconds.</summary>
    public const double LiveMinimumAxis = 5;

    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>Rounds to one decimal, half away from zero (0.05 → 0.1).</summary>
    public static double RoundTenths(double seconds)
        => Math.Round(Math.Max(0, seconds) * 10, MidpointRounding.AwayFromZero) / 10;

    /// <summary>
    /// A time with one decimal: "0.1s", "26.4s"; from a minute on "1m 05.3s" (whole minutes, then seconds with two
    /// digits and one decimal so the width stays put while it counts).
    /// </summary>
    public static string Seconds(double seconds)
    {
        var tenths = AiDuration.TenthsOfSeconds(seconds);
        return tenths < AiDuration.TenthsPerMinute ? AiDuration.Tenths(tenths) : AiDuration.MinutesTenths(tenths);
    }

    /// <summary>A tick label: "0s", "5s", "0.5s"; from a minute on "1m" or "1m 30s".</summary>
    public static string Tick(double seconds)
    {
        var t = Math.Round(seconds, 3);
        if (t < 60)
        {
            return t.ToString("0.#", s_invariant) + "s";
        }
        var minutes = (int)Math.Floor(t / 60);
        var rest = Math.Round(t - minutes * 60, 1);
        return rest <= 0
            ? minutes.ToString(s_invariant) + "m"
            : minutes.ToString(s_invariant) + "m " + rest.ToString("0.#", s_invariant) + "s";
    }

    /// <summary>Tokens with thousands separators: "1,040", "51,210".</summary>
    public static string Tokens(long tokens) => tokens.ToString("#,0", s_invariant);

    /// <summary>US dollars with two decimals, rounded half away from zero: 0.005248 → "$0.01".</summary>
    public static string Cost(double dollars)
        => "$" + Math.Round(Math.Max(0, dollars), 2, MidpointRounding.AwayFromZero).ToString("0.00", s_invariant);

    /// <summary>A speedup with one decimal: 1.508 → "1.5".</summary>
    public static string Speedup(double factor)
        => Math.Round(Math.Max(0, factor), 1, MidpointRounding.AwayFromZero).ToString("0.0", s_invariant);

    /// <summary>A playback speed: 0.5 → "0.5", 1 → "1", 2 → "2".</summary>
    public static string Speed(double speed) => speed.ToString("0.##", s_invariant);

    /// <summary>Rounds a token count the way it is shown (half up).</summary>
    public static long RoundTokens(double tokens) => (long)Math.Floor(tokens + 0.5);

    /// <summary>
    /// The tick step for an axis of <paramref name="axis"/> seconds drawn <paramref name="width"/> px wide: the smallest
    /// nice step (0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30 s, 1, 2, 5, 10, 15, 30 min, …) that gives at most six ticks from zero
    /// and keeps them at least 40 px apart. 26.4 s gives 5 s at any width from 300 px; 6.2 s gives 2 s; 60 s gives 15 s.
    /// </summary>
    public static double TickStep(double axis, double width = double.PositiveInfinity)
    {
        if (!(axis > 0) || !double.IsFinite(axis))
        {
            return 1;
        }
        foreach (var step in s_steps)
        {
            var ticks = (int)Math.Floor(axis / step + 1e-9) + 1;
            if (ticks > MaxTicks)
            {
                continue;
            }
            if (double.IsFinite(width) && width > 0 && step / axis * width < MinTickSpacing && ticks > 2)
            {
                continue;
            }
            return step;
        }
        return s_steps[^1];
    }

    /// <summary>The number of ticks from zero up to the axis length (the last one at or before the end).</summary>
    public static int TickCount(double axis, double step)
        => step > 0 && axis >= 0 ? (int)Math.Floor(axis / step + 1e-9) + 1 : 1;

    /// <summary><paramref name="seconds"/> rounded up to a whole tick step of itself (6.75 → 8, 10.8 → 12, 27 → 30).</summary>
    public static double NiceCeiling(double seconds)
    {
        if (!(seconds > 0))
        {
            return LiveMinimumAxis;
        }
        var step = TickStep(seconds);
        return Math.Ceiling(seconds / step - 1e-9) * step;
    }

    /// <summary>
    /// The axis length of a live run at <paramref name="extent"/> seconds (the later of now and the last thing that
    /// happened). It only grows, and only in calm steps: when the time passes 90% of the current axis, the axis jumps to a
    /// nice length past 1.5 times the time (5 → 8 → 12 → 20 → 30 → 50 s), so the scale changes every few seconds at most
    /// and each change is a clear step the plot springs to.
    /// </summary>
    public static double LiveAxis(double extent, double current)
    {
        extent = Math.Max(0, extent);
        if (current > 0 && extent <= current * LiveGrowAt)
        {
            return current;
        }
        return Math.Max(Math.Max(current, LiveMinimumAxis), NiceCeiling(Math.Max(extent, 0.01) * LiveGrowBy));
    }
}
