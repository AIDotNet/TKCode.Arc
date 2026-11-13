namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentTasks"/>: nine tasks of a small product team handed to four made-up agents.
/// With no <see cref="AgentTasks.Tasks"/> the board shows these and the running ones move on their own.
/// </summary>
public static class AgentTasksSamples
{
    /// <summary>The sample tasks, timed relative to <paramref name="now"/>.</summary>
    public static IReadOnlyList<AgentTaskItem> Tasks(DateTimeOffset now) =>
    [
        Task("t1", "Draft the March release notes", "Quill", AgentTaskStatus.Running, 0.62, "Writing the fixes section", now.AddMinutes(-2)),
        Task("t2", "Triage new bug reports from the weekend", "Scout", AgentTaskStatus.Running, 0.28, "Read 14 of 50 reports", now.AddMinutes(-1)),
        Task("t3", "Reconcile February card payouts", "Ledger", AgentTaskStatus.NeedsInput, 0.8, "Two payouts have no invoice. Which account?", now.AddMinutes(-18)),
        Task("t4", "Upgrade the image service to the new runtime", "Patchwork", AgentTaskStatus.Failed, 0.45, "Build failed: a test needs a newer fixture", now.AddMinutes(-40)),
        Task("t5", "Summarize churn interviews", "Quill", AgentTaskStatus.Queued, 0, null, now.AddMinutes(-55)),
        Task("t6", "Clean up stale feature flags", "Patchwork", AgentTaskStatus.Queued, 0, null, now.AddHours(-2)),
        Task("t7", "Translate the help center update", "Quill", AgentTaskStatus.Done, 1, "Published in 4 languages", now.AddHours(-3)),
        Task("t8", "Check uptime alerts for false alarms", "Scout", AgentTaskStatus.Done, 1, "Muted 3 noisy checks", now.AddHours(-5)),
        Task("t9", "Prepare the board deck numbers", "Ledger", AgentTaskStatus.Running, 0.12, "Pulling revenue by region", now.AddMinutes(-4))
    ];

    private static AgentTaskItem Task(string id, string title, string agent, AgentTaskStatus status, double progress, string? note,
        DateTimeOffset updated) => new()
    {
        Id = id,
        Title = title,
        Agent = agent,
        Status = status,
        Progress = progress,
        Note = note,
        Updated = updated
    };
}
