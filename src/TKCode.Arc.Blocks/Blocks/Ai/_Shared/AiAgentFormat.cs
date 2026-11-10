using System.Globalization;

namespace TKCode.Arc;

/// <summary>Shared English formatting of times, durations and costs for the agent blocks (the labels' defaults).</summary>
internal static class AiAgentFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>A run time: "0s", "48s", "1m 05s", "1h 02m".</summary>
    public static string Elapsed(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }
        var seconds = (long)time.TotalSeconds;
        if (seconds < 60)
        {
            return seconds.ToString(Invariant) + "s";
        }
        if (seconds < 3600)
        {
            return string.Create(Invariant, $"{seconds / 60}m {seconds % 60:00}s");
        }
        return string.Create(Invariant, $"{seconds / 3600}h {seconds / 60 % 60:00}m");
    }

    /// <summary>A short step time: "0.4s", "2.1s", "48s", "1m 05s".</summary>
    public static string Duration(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }
        return time.TotalSeconds < 10 ? string.Create(Invariant, $"{time.TotalSeconds:0.0}s") : Elapsed(time);
    }

    /// <summary>A cost in a currency: "$0.42", small amounts with more places ("$0.004"); zero is "$0.00".</summary>
    public static string Cost(decimal amount, string symbol)
    {
        var abs = Math.Abs(amount);
        var format = abs == 0 || abs >= 0.1m ? "0.00" : abs >= 0.01m ? "0.000" : "0.0000";
        return symbol + amount.ToString(format, Invariant);
    }

    /// <summary>
    /// A time relative to <paramref name="now"/>: "just now", "5m ago", "3h ago", "yesterday", "4d ago", then "Mar 3";
    /// a time in the future reads "in 5m", "in 3h", "in 2d".
    /// </summary>
    public static string Relative(DateTimeOffset time, DateTimeOffset now)
    {
        var delta = now - time;
        if (delta < TimeSpan.Zero)
        {
            var ahead = -delta;
            if (ahead.TotalMinutes < 1)
            {
                return "in a moment";
            }
            if (ahead.TotalHours < 1)
            {
                return string.Create(Invariant, $"in {(int)Math.Ceiling(ahead.TotalMinutes)}m");
            }
            if (ahead.TotalDays < 1)
            {
                return string.Create(Invariant, $"in {(int)Math.Round(ahead.TotalHours)}h");
            }
            return string.Create(Invariant, $"in {(int)Math.Round(ahead.TotalDays)}d");
        }
        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }
        if (delta.TotalHours < 1)
        {
            return string.Create(Invariant, $"{(int)delta.TotalMinutes}m ago");
        }
        if (delta.TotalDays < 1)
        {
            return string.Create(Invariant, $"{(int)delta.TotalHours}h ago");
        }
        if (delta.TotalDays < 2)
        {
            return "yesterday";
        }
        if (delta.TotalDays < 7)
        {
            return string.Create(Invariant, $"{(int)delta.TotalDays}d ago");
        }
        return time.ToString("MMM d", Invariant);
    }

    /// <summary>A whole count with thousands separators ("12,480").</summary>
    public static string Count(long value) => value.ToString("N0", Invariant);
}
