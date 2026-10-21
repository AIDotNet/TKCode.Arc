namespace TKCode.Arc.Gallery.Demos;

/// <summary>Sample updates for the Timeline demos. Times are relative to now, so the feeds always look current.</summary>
public static class TimelineDemoData
{
    private static readonly string[] s_people = ["Ana Ruiz", "Theo Walsh", "Mina Park", "Kai Moreno", "Lee Chen", "Ivy Brooks"];

    private static readonly (string Title, string? Meta)[] s_updates =
    [
        ("merged Checkout redesign into main", "PR #212"),
        ("opened Pricing page copy review", "PR #219"),
        ("commented on Onboarding flow", "Issue #88"),
        ("approved Billing migration", "PR #207"),
        ("pushed 3 commits to feature/search", "a41c9e2"),
        ("closed Flaky e2e test on Safari", "Issue #91")
    ];

    /// <summary>The current minute, the reference time of the feeds.</summary>
    public static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
    }

    /// <summary>A person and an update for the n-th post, cycling through the samples.</summary>
    public static (string Actor, string Title, string? Meta) Update(int n)
    {
        var (title, meta) = s_updates[n % s_updates.Length];
        return (s_people[n % s_people.Length], title, meta);
    }

    /// <summary>A long history of updates, one every <paramref name="minutesApart"/> minutes, with a system event now and then.</summary>
    public static List<TimelineEvent> History(int count, int minutesApart)
    {
        var now = Now();
        var list = new List<TimelineEvent>(count);
        for (var i = 0; i < count; i++)
        {
            var (actor, title, meta) = Update(i);
            var system = i % 7 == 3;
            list.Add(new TimelineEvent($"h-{i}", now.AddMinutes(-(i * minutesApart + 4)), system ? $"Nightly backup {(i % 2 == 0 ? "completed" : "finished")}" : title,
                system ? null : actor)
            {
                Meta = system ? "Storage" : meta,
                Icon = system ? ArcIcons.Check : null,
                Tone = system ? TimelineTone.Success : TimelineTone.Neutral,
                Detail = i % 5 == 0 ? $"Update {i + 1} of {count}. Opened from the audit log." : null
            });
        }
        return list;
    }
}
