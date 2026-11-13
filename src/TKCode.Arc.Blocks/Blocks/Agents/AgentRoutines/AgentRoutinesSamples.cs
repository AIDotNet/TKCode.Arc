namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentRoutines"/>: four routines of made-up agents with a few weeks of runs. With no
/// <see cref="AgentRoutines.Routines"/> the list shows these and Run now plays a short stand-in run.
/// </summary>
public static class AgentRoutinesSamples
{
    /// <summary>How long the stand-in run of Run now takes (1.6 seconds).</summary>
    public static readonly TimeSpan SimulatedRunTime = TimeSpan.FromMilliseconds(1600);

    /// <summary>The sample routines, with runs ending before <paramref name="now"/>.</summary>
    public static IReadOnlyList<AgentRoutine> Routines(DateTimeOffset now) =>
    [
        new AgentRoutine
        {
            Id = "r1", Name = "Morning inbox digest", Agent = "Quill", Schedule = "0 8 * * 1-5",
            Instructions = "Read the shared support inbox since yesterday's digest. Group messages by topic, flag anything " +
                           "urgent or angry, and post a five-line summary to the team channel.",
            Runs = History(now, "0 8 * * 1-5", "SFSSSSSSSS", "Posted a digest of {0} messages")
        },
        new AgentRoutine
        {
            Id = "r2", Name = "Nightly dependency check", Agent = "Patchwork", Schedule = "30 2 * * *",
            Instructions = "Check every service for outdated or vulnerable packages. Open one pull request per service with " +
                           "safe minor updates; list major updates in the weekly report instead.",
            Runs = History(now, "30 2 * * *", "SSKSSSFSSS", "Opened {0} pull requests")
        },
        new AgentRoutine
        {
            Id = "r3", Name = "Weekly spend report", Agent = "Ledger", Schedule = "0 9 * * 1",
            Instructions = "Compare last week's cloud and software spend with the budget. Explain any line that grew by more " +
                           "than 10% and email the report to the finance lead.",
            Runs = History(now, "0 9 * * 1", "SSSSS", "Sent the report, {0} items over budget")
        },
        new AgentRoutine
        {
            Id = "r4", Name = "Stale ticket sweep", Agent = "Scout", Schedule = "0 */4 * * *", IsEnabled = false,
            Instructions = "Find tickets with no reply for five days. Nudge the owner once; close tickets that were already " +
                           "nudged and leave a note saying why.",
            Runs = History(now, "0 */4 * * *", "SKSFS", "Nudged {0} tickets")
        }
    ];

    /// <summary>Past runs of <paramref name="cron"/> before <paramref name="now"/>, newest first, from a pattern of S / F / K.</summary>
    private static IReadOnlyList<AgentRoutineRun> History(DateTimeOffset now, string cron, string pattern, string summary)
    {
        // Walk the schedule forward from far enough back, then keep the last runs before now.
        var times = new List<DateTimeOffset>();
        var cursor = now.AddDays(-pattern.Length * 8);
        while (AgentRoutineSchedule.Next(cron, cursor) is { } next && next < now)
        {
            times.Add(next);
            cursor = next;
        }
        var runs = new List<AgentRoutineRun>();
        for (var i = 0; i < pattern.Length && i < times.Count; i++)
        {
            var started = times[times.Count - 1 - i];
            var seconds = 20 + (i * 37 + pattern.Length * 11) % 90;
            runs.Add(pattern[i] switch
            {
                'F' => new AgentRoutineRun(started, AgentRoutineOutcome.Failed, TimeSpan.FromSeconds(seconds / 3), "Stopped: a service did not answer in time"),
                'K' => new AgentRoutineRun(started, AgentRoutineOutcome.Skipped, TimeSpan.FromSeconds(2), "Nothing new to do"),
                _ => new AgentRoutineRun(started, AgentRoutineOutcome.Succeeded, TimeSpan.FromSeconds(seconds), string.Format(System.Globalization.CultureInfo.InvariantCulture, summary, 3 + (i * 7) % 19))
            });
        }
        return runs;
    }
}
