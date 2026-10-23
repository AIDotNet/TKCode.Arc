namespace TKCode.Arc;

/// <summary>
/// The number formats of <see cref="AgentStatus"/> (the defaults of <see cref="AgentStatusLabels"/>), as measured on the
/// reference: the timer is whole seconds as m:ss; a run's total is rounded to whole seconds ("13s", "2m 23s"); a finished
/// step has one decimal ("1.9s"); a running step counts whole seconds without minutes ("129s"). Pure functions.
/// </summary>
internal static class AgentStatusFormat
{
    /// <summary>The elapsed timer: whole seconds (floor) as m:ss, h:mm:ss from an hour on ("0:03", "2:20", "1:02:07").</summary>
    public static string Timer(TimeSpan elapsed) => AiDuration.Clock(WholeSecondsFloor(elapsed));

    /// <summary>A run's total, rounded to whole seconds: "13s", "2m 23s", from an hour on "1h 4m".</summary>
    public static string Total(TimeSpan duration)
    {
        var seconds = WholeSecondsRounded(duration);
        if (seconds < 60)
        {
            return AiDuration.Seconds(seconds);
        }
        return seconds < 3600 ? AiDuration.MinutesSeconds(seconds) : AiDuration.HoursMinutes(seconds);
    }

    /// <summary>A run's total in words for screen readers: "14 seconds", "2 minutes 23 seconds", "1 minute".</summary>
    public static string TotalSpoken(TimeSpan duration) => AiDuration.Spoken(WholeSecondsRounded(duration));

    /// <summary>
    /// A finished step's duration: tenths of a second rounded half up from the milliseconds ("1.9s", "1.5s" for 1499ms,
    /// "0.4s"); from a minute on "1m 12s".
    /// </summary>
    public static string StepDuration(TimeSpan duration)
    {
        var tenths = AiDuration.TenthsOfMilliseconds(AiDuration.WholeMilliseconds(duration));
        return tenths >= AiDuration.TenthsPerMinute ? Total(duration) : AiDuration.Tenths(tenths);
    }

    /// <summary>A running or waiting step's time: whole seconds (floor), no minutes ("0s", "12s", "129s").</summary>
    public static string LiveStepDuration(TimeSpan elapsed) => AiDuration.Seconds(WholeSecondsFloor(elapsed));

    /// <summary>Whole seconds, rounded down (never negative).</summary>
    public static long WholeSecondsFloor(TimeSpan span) => AiDuration.WholeSecondsFloor(span);

    /// <summary>Whole seconds, rounded half up (never negative).</summary>
    public static long WholeSecondsRounded(TimeSpan span) => AiDuration.WholeSecondsRounded(span);
}
