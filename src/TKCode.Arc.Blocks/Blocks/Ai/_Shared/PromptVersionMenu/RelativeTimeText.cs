namespace TKCode.Arc;

/// <summary>
/// How long ago a version was written, as the version menu says it (measured from Arc: "3 days ago", "last week",
/// "2 weeks ago"): under 7 whole days <c>daysAgo(days)</c>, 7 to 13 days <c>lastWeek</c>, then <c>weeksAgo(weeks)</c> in
/// whole weeks. The words come from the caller (no English here); a missing one gives an empty string. A moment after
/// <c>now</c> counts as 0 days.
/// </summary>
internal static class RelativeTimeText
{
    /// <summary>Whole days from <paramref name="when"/> to <paramref name="now"/>, never negative.</summary>
    public static int DaysBetween(DateTimeOffset when, DateTimeOffset now) =>
        Math.Max(0, (int)Math.Floor((now - when).TotalDays));

    /// <summary>The words for <paramref name="when"/>, measured from <paramref name="now"/>.</summary>
    /// <param name="when">When the version was written.</param>
    /// <param name="now">The moment to measure from.</param>
    /// <param name="daysAgo">Words for fewer than 7 days (the argument is the whole days, 0 to 6).</param>
    /// <param name="lastWeek">Words for 7 to 13 days.</param>
    /// <param name="weeksAgo">Words for 14 days or more (the argument is the whole weeks, 2 and up).</param>
    public static string Format(DateTimeOffset when, DateTimeOffset now, Func<int, string>? daysAgo, string? lastWeek,
                                Func<int, string>? weeksAgo)
    {
        var days = DaysBetween(when, now);
        if (days < 7)
        {
            return daysAgo?.Invoke(days) ?? string.Empty;
        }
        if (days < 14)
        {
            return lastWeek ?? string.Empty;
        }
        return weeksAgo?.Invoke(days / 7) ?? string.Empty;
    }
}
