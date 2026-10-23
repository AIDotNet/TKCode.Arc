
namespace TKCode.Arc;

/// <summary>What a group header shows: the label (and whether it shimmers) and the quiet meta on the right.</summary>
internal readonly record struct ToolCallGroupHeader(string Label, bool Shimmers, string? Meta, bool MetaIsFailure);

/// <summary>
/// The arithmetic and wording of the tool call parts, as pure functions (tested against the measured preview): duration
/// format, row meta, status words, accessible names, announcements, the group header's phrasing and its total.
/// </summary>
internal static class ToolCallFormat
{
    /// <summary>
    /// "760ms" under a second (whole milliseconds), "1.2s" from one second (one decimal, ".0" kept: 2970ms reads "3.0s"),
    /// "1m 05s" from a minute. No space before the unit; the views draw it with tabular digits.
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        var ms = AiDuration.WholeMilliseconds(duration);
        if (ms < 1000)
        {
            return AiDuration.Milliseconds(ms);
        }
        var tenths = AiDuration.TenthsOfMilliseconds(ms);
        if (tenths < AiDuration.TenthsPerMinute)
        {
            return AiDuration.Tenths(tenths);
        }
        return AiDuration.MinutesSeconds(AiDuration.WholeSecondsRounded(TimeSpan.FromMilliseconds(ms)), padSeconds: true);
    }

    /// <summary>Whether a reason line shows under the title (a failed or waiting call with a message).</summary>
    public static bool ShowsNote(ToolCallStatus status, string? message) =>
        status is ToolCallStatus.Error or ToolCallStatus.Approval && !string.IsNullOrEmpty(message);

    /// <summary>Whether the row offers buttons (Retry, or Deny and Allow).</summary>
    public static bool ShowsActions(ToolCallStatus status) => status is ToolCallStatus.Error or ToolCallStatus.Approval;

    /// <summary>
    /// The meta pieces of a row: (summary, duration) for a successful call, ("Denied", null) for a denied one, nothing for
    /// the others (their side shows buttons or nothing). <paramref name="compact"/> keeps only the duration (or the summary
    /// when there is no duration); <paramref name="tight"/> shows nothing.
    /// </summary>
    public static (string? Summary, string? Duration) Meta(ToolCallItem item, ToolCallLabels labels, bool compact, bool tight)
    {
        if (tight)
        {
            return (null, null);
        }
        switch (item.Status)
        {
            case ToolCallStatus.Success:
            {
                var summary = item.SummaryText(labels);
                var duration = item.Duration is { } d ? labels.Duration(d) : null;
                if (compact && duration is not null)
                {
                    return (null, duration);
                }
                return (summary, duration);
            }
            case ToolCallStatus.Denied:
                return (labels.DeniedMeta, null);
            default:
                return (null, null);
        }
    }

    /// <summary>The meta as one string ("4 results · 1.2s"), or null when empty.</summary>
    public static string? MetaText(ToolCallItem item, ToolCallLabels labels, bool compact = false, bool tight = false)
    {
        var (summary, duration) = Meta(item, labels, compact, tight);
        if (summary is null)
        {
            return duration;
        }
        return duration is null ? summary : summary + " · " + duration;
    }

    /// <summary>The status words read after a row's title.</summary>
    public static string StatusWords(ToolCallItem item, ToolCallLabels labels) => item.Status switch
    {
        ToolCallStatus.Running => labels.RunningStatus,
        ToolCallStatus.Success => labels.DoneStatus(item.SummaryText(labels), item.Duration is { } d ? labels.Duration(d) : null),
        ToolCallStatus.Error => labels.FailedStatus(item.Message),
        ToolCallStatus.Approval => labels.ApprovalStatus,
        _ => labels.DeniedStatus
    };

    /// <summary>A row's accessible name: "Tool: phrase status words".</summary>
    public static string RowName(ToolCallItem item, ToolCallLabels labels) =>
        labels.RowName(item.ToolName, item.TitleFor(item.Status), StatusWords(item, labels));

    /// <summary>What a row says when its call reaches its current status.</summary>
    public static string Announcement(ToolCallItem item, ToolCallLabels labels)
    {
        var title = item.TitleFor(item.Status);
        return item.Status switch
        {
            ToolCallStatus.Running => labels.AnnounceRunning(item.ToolName, title),
            ToolCallStatus.Success => labels.AnnounceSuccess(item.ToolName, title, item.SummaryText(labels)),
            ToolCallStatus.Error => labels.AnnounceError(item.ToolName, title, item.Message),
            ToolCallStatus.Approval => labels.AnnounceApproval(item.ToolName, title),
            _ => labels.AnnounceDenied(item.ToolName, title)
        };
    }

    /// <summary>The sum of the successful calls' durations (failed, waiting, denied and running calls do not count).</summary>
    public static TimeSpan TotalDuration(IEnumerable<ToolCallItem> items)
    {
        var total = TimeSpan.Zero;
        foreach (var item in items)
        {
            if (item.Status == ToolCallStatus.Success && item.Duration is { } duration)
            {
                total += duration;
            }
        }
        return total;
    }

    /// <summary>The number of failed calls.</summary>
    public static int FailedCount(IEnumerable<ToolCallItem> items) => items.Count(i => i.Status == ToolCallStatus.Error);

    /// <summary>Whether the agent counts as working: <paramref name="isActive"/> when set, otherwise any call running.</summary>
    public static bool IsActive(IEnumerable<ToolCallItem> items, bool? isActive) =>
        isActive ?? items.Any(i => i.Status == ToolCallStatus.Running);

    /// <summary>
    /// The group header. Label: "Waiting for approval" while any call waits (it wins), "Using N tools" (shimmering) while
    /// active, "Used N tools" otherwise; N counts every call. Meta: "K failed" while any call failed, the total of the
    /// successful durations once nothing runs, waits or is active, and nothing otherwise.
    /// </summary>
    public static ToolCallGroupHeader Header(IReadOnlyCollection<ToolCallItem> items, bool? isActive, ToolCallLabels labels)
    {
        var active = IsActive(items, isActive);
        var waiting = items.Any(i => i.Status == ToolCallStatus.Approval);
        var running = items.Any(i => i.Status == ToolCallStatus.Running);
        string label;
        var shimmers = false;
        if (waiting)
        {
            label = labels.WaitingForApproval;
        }
        else if (active)
        {
            label = labels.Using(items.Count);
            shimmers = true;
        }
        else
        {
            label = labels.Used(items.Count);
        }
        var failed = FailedCount(items);
        if (failed > 0)
        {
            return new ToolCallGroupHeader(label, shimmers, labels.Failed(failed), true);
        }
        string? meta = null;
        if (!active && !running && !waiting)
        {
            var total = TotalDuration(items);
            if (total > TimeSpan.Zero)
            {
                meta = labels.Duration(total);
            }
        }
        return new ToolCallGroupHeader(label, shimmers, meta, false);
    }

    /// <summary>Whether every call has settled (none running, waiting or failed) and the agent stopped: the moment a
    /// group may fold itself.</summary>
    public static bool IsSettled(IReadOnlyCollection<ToolCallItem> items, bool? isActive) =>
        items.Count > 0 && !IsActive(items, isActive) &&
        items.All(i => i.Status is ToolCallStatus.Success or ToolCallStatus.Denied);

    /// <summary>Whether the call needs a person (a failure to retry, an approval).</summary>
    public static bool NeedsDecision(ToolCallStatus status) => status is ToolCallStatus.Error or ToolCallStatus.Approval;

    /// <summary>Whether a group with these calls starts open: while any call runs, waits or failed.</summary>
    public static bool OpensByDefault(IEnumerable<ToolCallItem> items, bool? isActive) =>
        items.Any(i => i.Status is ToolCallStatus.Running or ToolCallStatus.Approval or ToolCallStatus.Error) || isActive == true;
}
