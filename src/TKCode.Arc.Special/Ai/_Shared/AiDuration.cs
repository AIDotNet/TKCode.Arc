using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The building blocks of the Special › AI duration formats: whole-second rounding, minutes and seconds (with the seconds
/// optionally zero padded), tenths of a second, milliseconds, the m:ss timer and spoken plurals. Each component keeps its
/// own measured format (<c>AgentStatusFormat</c>, <c>AgentPlanFormat.Duration</c>, <c>ToolCallFormat.FormatDuration</c>,
/// <c>AgentTimelineMath.Seconds</c>) as a thin wrapper that picks the pieces. All rounding is half away from zero (as
/// JavaScript's <c>Math.Round</c> for positive values), never banker's rounding; all text is invariant. Pure functions.
/// </summary>
internal static class AiDuration
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>Tenths of a second in one minute.</summary>
    public const long TenthsPerMinute = 600;

    /// <summary>Whole seconds, rounded down (never negative).</summary>
    public static long WholeSecondsFloor(TimeSpan span) => Math.Max(0, (long)Math.Floor(span.TotalMilliseconds / 1000));

    /// <summary>Whole seconds, rounded half up (never negative).</summary>
    public static long WholeSecondsRounded(TimeSpan span) =>
        Math.Max(0, (long)Math.Floor(span.TotalMilliseconds / 1000 + 0.5));

    /// <summary>Whole milliseconds, rounded half away from zero (never negative).</summary>
    public static long WholeMilliseconds(TimeSpan span) =>
        Math.Max(0, (long)Math.Round(span.TotalMilliseconds, MidpointRounding.AwayFromZero));

    /// <summary>Tenths of a second in <paramref name="seconds"/>, rounded half away from zero (never negative).</summary>
    public static long TenthsOfSeconds(double seconds) =>
        Math.Max(0, (long)Math.Round(seconds * 10, MidpointRounding.AwayFromZero));

    /// <summary>Tenths of a second in <paramref name="milliseconds"/>, rounded half up.</summary>
    public static long TenthsOfMilliseconds(long milliseconds) => (Math.Max(0, milliseconds) + 50) / 100;

    /// <summary>"23s".</summary>
    public static string Seconds(long seconds) => string.Create(s_invariant, $"{seconds}s");

    /// <summary>"2m 23s", or "1m 07s" with <paramref name="padSeconds"/>.</summary>
    public static string MinutesSeconds(long totalSeconds, bool padSeconds = false)
    {
        var m = totalSeconds / 60;
        var s = totalSeconds % 60;
        return padSeconds
            ? string.Create(s_invariant, $"{m}m {s:00}s")
            : string.Create(s_invariant, $"{m}m {s}s");
    }

    /// <summary>"1h 4m" (the seconds are dropped).</summary>
    public static string HoursMinutes(long totalSeconds) =>
        string.Create(s_invariant, $"{totalSeconds / 3600}h {totalSeconds / 60 % 60}m");

    /// <summary>The timer: "0:03", "2:20", from an hour on "1:02:07".</summary>
    public static string Clock(long totalSeconds)
    {
        var h = totalSeconds / 3600;
        var m = totalSeconds / 60 % 60;
        var s = totalSeconds % 60;
        return h > 0
            ? string.Create(s_invariant, $"{h}:{m:00}:{s:00}")
            : string.Create(s_invariant, $"{totalSeconds / 60}:{s:00}");
    }

    /// <summary>Tenths of a second with one decimal (".0" kept): 19 reads "1.9s", 30 reads "3.0s".</summary>
    public static string Tenths(long tenths) => string.Create(s_invariant, $"{tenths / 10}.{tenths % 10}s");

    /// <summary>
    /// Tenths of a second from a minute on, as "1m 05.3s": whole minutes, then the seconds with two digits and one decimal
    /// so the width stays put while it counts.
    /// </summary>
    public static string MinutesTenths(long tenths)
    {
        var rest = tenths % TenthsPerMinute;
        return string.Create(s_invariant, $"{tenths / TenthsPerMinute}m {rest / 10:00}.{rest % 10}s");
    }

    /// <summary>Whole milliseconds: "760ms".</summary>
    public static string Milliseconds(long milliseconds) => string.Create(s_invariant, $"{milliseconds}ms");

    /// <summary>Whole seconds in words for screen readers: "14 seconds", "2 minutes 23 seconds", "1 minute".</summary>
    public static string Spoken(long totalSeconds)
    {
        var h = totalSeconds / 3600;
        var m = totalSeconds / 60 % 60;
        var s = totalSeconds % 60;
        var parts = new List<string>(3);
        if (h > 0)
        {
            parts.Add(Plural(h, "hour"));
        }
        if (m > 0)
        {
            parts.Add(Plural(m, "minute"));
        }
        if (s > 0 || parts.Count == 0)
        {
            parts.Add(Plural(s, "second"));
        }
        return string.Join(" ", parts);
    }

    /// <summary>"1 minute", "2 minutes".</summary>
    public static string Plural(long n, string unit) =>
        string.Create(s_invariant, $"{n} {unit}{(n == 1 ? string.Empty : "s")}");
}
