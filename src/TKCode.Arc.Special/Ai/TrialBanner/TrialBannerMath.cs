namespace TKCode.Arc;

/// <summary>
/// The pure date and state rules of <see cref="TrialBanner"/>: days left from the end date and the local today, the
/// status, the ring's fill, the snooze options and when each snooze ends. Every "now" comes from a <see cref="TimeProvider"/>.
/// </summary>
internal static class TrialBannerMath
{
    /// <summary>The local calendar day of <paramref name="time"/> in the provider's time zone.</summary>
    public static DateOnly LocalDate(DateTimeOffset time, TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, clock.LocalTimeZone).DateTime);

    /// <summary>Today in the provider's time zone.</summary>
    public static DateOnly Today(TimeProvider clock) => LocalDate(clock.GetUtcNow(), clock);

    /// <summary>Whole calendar days from today to the end date (negative once it has passed).</summary>
    public static int DaysUntil(DateTimeOffset endsAt, TimeProvider clock) =>
        LocalDate(endsAt, clock).DayNumber - Today(clock).DayNumber;

    /// <summary>The start of a local day as an instant in the provider's time zone.</summary>
    public static DateTimeOffset StartOf(DateOnly day, TimeProvider clock)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, clock.LocalTimeZone.GetUtcOffset(local));
    }

    /// <summary>
    /// The resolved trial: days left, the end date and whether it has ended. Explicit days win (the clock then only dates
    /// the end, when no end date is given); otherwise they come from the end date and today; with neither, no days (the
    /// banner shows that there is no trial).
    /// </summary>
    public static (int Days, DateOnly EndDate, bool Ended) Resolve(int? daysLeft, DateTimeOffset? endsAt, TimeProvider clock)
    {
        if (daysLeft is { } days)
        {
            var end = endsAt is { } given ? LocalDate(given, clock) : Today(clock).AddDays(Math.Max(0, days));
            return (days, end, days <= 0);
        }
        if (endsAt is { } at)
        {
            var left = DaysUntil(at, clock);
            // An end later today is still running ("ends today"); one that has passed has ended.
            var ended = left < 0 || (left == 0 && at <= clock.GetUtcNow());
            return (Math.Max(0, left), LocalDate(at, clock), ended);
        }
        return (0, Today(clock), false);
    }

    /// <summary>The status from the resolved days.</summary>
    public static TrialStatus StatusOf(int days, bool ended, int warnAt, bool upgraded)
    {
        if (upgraded)
        {
            return TrialStatus.Upgraded;
        }
        if (ended)
        {
            return TrialStatus.Expired;
        }
        return days <= warnAt ? TrialStatus.Warning : TrialStatus.Active;
    }

    /// <summary>The ring's fill: the days left of the trial's length, empty once ended, full once upgraded.</summary>
    public static double RingValue(TrialStatus status, int days, int trialDays) => status switch
    {
        TrialStatus.Upgraded => 1,
        TrialStatus.Expired => 0,
        _ => trialDays <= 0 ? 0 : Math.Clamp(days / (double)trialDays, 0, 1)
    };

    /// <summary>The snooze rows offered for a status: tomorrow always; three days while that is before the end; the warning while it is at least two days off.</summary>
    public static IReadOnlyList<TrialSnoozeOption> SnoozeOptions(TrialStatus status, int days, int warnAt)
    {
        var options = new List<TrialSnoozeOption>(3);
        if (status == TrialStatus.Upgraded)
        {
            return options;
        }
        options.Add(TrialSnoozeOption.Tomorrow);
        if (status == TrialStatus.Expired || days > 3)
        {
            options.Add(TrialSnoozeOption.ThreeDays);
        }
        var untilWarning = days - warnAt;
        if (status == TrialStatus.Active && untilWarning >= 2 && untilWarning != 3)
        {
            options.Add(TrialSnoozeOption.UntilWarning);
        }
        return options;
    }

    /// <summary>The first day of the warning, counted from today.</summary>
    public static DateOnly WarningStarts(int days, int warnAt, TimeProvider clock) =>
        Today(clock).AddDays(Math.Max(1, days - warnAt));

    /// <summary>When a snooze ends: the start of that local day.</summary>
    public static DateTimeOffset SnoozeUntil(TrialSnoozeOption option, int days, int warnAt, TimeProvider clock)
    {
        var today = Today(clock);
        var day = option switch
        {
            TrialSnoozeOption.ThreeDays => today.AddDays(3),
            TrialSnoozeOption.UntilWarning => WarningStarts(days, warnAt, clock),
            _ => today.AddDays(1)
        };
        return StartOf(day, clock);
    }

    /// <summary>The end day in the warning copy: today, tomorrow or its weekday.</summary>
    public static string EndDay(int days, DateOnly endDate, TrialBannerLabels labels) => days switch
    {
        <= 0 => labels.EndsToday,
        1 => labels.EndsTomorrow,
        _ => labels.Weekday(endDate)
    };
}
