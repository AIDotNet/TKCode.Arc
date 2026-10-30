namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ToolCall"/> and <see cref="ToolCallGroup"/> show or say, in English by default. Replace the
/// whole object to localize or reword; sentences with numbers or names are format functions.
/// </summary>
public sealed class ToolCallLabels
{
    /// <summary>The English defaults.</summary>
    public static ToolCallLabels Default { get; } = new();

    /// <summary>The Input section's heading.</summary>
    public string Input { get; init; } = "Input";

    /// <summary>The Output section's heading.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The Output section's heading while the call failed (the section then shows the reason).</summary>
    public string Error { get; init; } = "Error";

    /// <summary>The Input copy button's name.</summary>
    public string CopyInput { get; init; } = "Copy input";

    /// <summary>The Output copy button's name.</summary>
    public string CopyOutput { get; init; } = "Copy output";

    /// <summary>The Error copy button's name.</summary>
    public string CopyError { get; init; } = "Copy error";

    /// <summary>What a copy button says after copying, after its name ("Copy input: Copied").</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>What a copy button says when the clipboard refused the text.</summary>
    public string CopyFailed { get; init; } = "Copy failed";

    /// <summary>The fold button of a JSON or text block with <c>n</c> hidden lines.</summary>
    public Func<int, string> ShowMoreLines { get; init; } = n => n == 1 ? "Show 1 more line" : $"Show {n} more lines";

    /// <summary>The fold button of a list or table with <c>n</c> hidden rows.</summary>
    public Func<int, string> ShowMoreRows { get; init; } = n => n == 1 ? "Show 1 more row" : $"Show {n} more rows";

    /// <summary>The fold button once everything shows.</summary>
    public string ShowLess { get; init; } = "Show less";

    /// <summary>The title of a row with no call (its empty state).</summary>
    public string EmptyTitle { get; init; } = "No tool call";

    /// <summary>The Retry button on a failed call.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The Allow button on a call waiting for approval.</summary>
    public string Allow { get; init; } = "Allow";

    /// <summary>The Deny button on a call waiting for approval.</summary>
    public string Deny { get; init; } = "Deny";

    /// <summary>An action button's name: the action and the call it acts on ("Allow: Post a status note to #finance-ops").</summary>
    public Func<string, string, string> ActionName { get; init; } = (action, title) => $"{action}: {title}";

    /// <summary>The meta of a denied call.</summary>
    public string DeniedMeta { get; init; } = "Denied";

    /// <summary>The Output section of a call waiting for approval.</summary>
    public string ApprovalPlaceholder { get; init; } = "This call runs once you allow it.";

    /// <summary>The Output section of a denied call.</summary>
    public string DeniedPlaceholder { get; init; } = "This call did not run.";

    /// <summary>The Output section of a call that is still running.</summary>
    public string RunningPlaceholder { get; init; } = "Waiting for the result.";

    /// <summary>The Output section of a successful call that returned nothing.</summary>
    public string EmptyOutput { get; init; } = "No output.";

    /// <summary>The derived summary of a list with <c>n</c> entries.</summary>
    public Func<int, string> Results { get; init; } = n => n == 1 ? "1 result" : $"{n} results";

    /// <summary>The derived summary of a table with <c>n</c> rows.</summary>
    public Func<int, string> Rows { get; init; } = n => n == 1 ? "1 row" : $"{n} rows";

    /// <summary>The derived summary of a JSON object with <c>n</c> keys.</summary>
    public Func<int, string> Fields { get; init; } = n => n == 1 ? "1 field" : $"{n} fields";

    /// <summary>The derived summary of a JSON array with <c>n</c> elements.</summary>
    public Func<int, string> Items { get; init; } = n => n == 1 ? "1 item" : $"{n} items";

    /// <summary>A duration as shown in the meta and the group header: "760ms" under a second, "1.2s" from one second,
    /// "1m 05s" from a minute.</summary>
    public Func<TimeSpan, string> Duration { get; init; } = ToolCallFormat.FormatDuration;

    /// <summary>The status words of a running call, read after its title.</summary>
    public string RunningStatus { get; init; } = "Running";

    /// <summary>The status words of a successful call, from its summary and duration (either may be null).</summary>
    public Func<string?, string?, string> DoneStatus { get; init; } = (summary, duration) =>
        string.Join(", ", new[] { "Done", summary, duration }.Where(s => !string.IsNullOrEmpty(s)));

    /// <summary>The status words of a failed call, from its reason (may be null).</summary>
    public Func<string?, string> FailedStatus { get; init; } = message =>
        string.IsNullOrEmpty(message) ? "Failed" : $"Failed: {message}";

    /// <summary>The status words of a call waiting for approval.</summary>
    public string ApprovalStatus { get; init; } = "Needs approval";

    /// <summary>The status words of a denied call.</summary>
    public string DeniedStatus { get; init; } = "Denied";

    /// <summary>A row's accessible name from the tool, the phrase and the status words ("Search: Searched the web Done,
    /// 3 results, 640ms").</summary>
    public Func<string, string, string, string> RowName { get; init; } = (tool, title, status) =>
        string.IsNullOrEmpty(tool) ? $"{title} {status}" : $"{tool}: {title} {status}";

    /// <summary>What a row says when its call starts running (tool, phrase).</summary>
    public Func<string, string, string> AnnounceRunning { get; init; } = (tool, title) => Named(tool, title);

    /// <summary>What a row says when its call succeeds (tool, phrase, summary or null).</summary>
    public Func<string, string, string?, string> AnnounceSuccess { get; init; } = (tool, title, summary) =>
        Named(tool, title) + ", " + (string.IsNullOrEmpty(summary) ? "done" : summary);

    /// <summary>What a row says when its call fails (tool, phrase, reason or null).</summary>
    public Func<string, string, string?, string> AnnounceError { get; init; } = (tool, title, message) =>
        string.IsNullOrEmpty(message) ? Named(tool, title) : $"{Named(tool, title)}. {message}";

    /// <summary>What a row says when its call starts waiting for approval (tool, phrase).</summary>
    public Func<string, string, string> AnnounceApproval { get; init; } = (tool, title) => Named(tool, title) + ", needs approval";

    /// <summary>What a row says when its call is denied (tool, phrase).</summary>
    public Func<string, string, string> AnnounceDenied { get; init; } = (tool, title) => Named(tool, title) + ", denied";

    /// <summary>Added to what a row says when a <see cref="ToolCallSimulation"/> takes Allow or Retry that nobody handles.</summary>
    public string SimulatedNote { get; init; } = "This is sample data, so the call is only simulated.";

    /// <summary>The group header while the agent works with <c>n</c> calls so far.</summary>
    public Func<int, string> Using { get; init; } = n => n == 1 ? "Using 1 tool" : $"Using {n} tools";

    /// <summary>The group header once the agent stopped, with <c>n</c> calls.</summary>
    public Func<int, string> Used { get; init; } = n => n == 1 ? "Used 1 tool" : $"Used {n} tools";

    /// <summary>The group header while a call waits for approval (this wins over the others).</summary>
    public string WaitingForApproval { get; init; } = "Waiting for approval";

    /// <summary>The group meta with <c>n</c> failed calls.</summary>
    public Func<int, string> Failed { get; init; } = n => $"{n} failed";

    private static string Named(string tool, string title) => string.IsNullOrEmpty(tool) ? title : $"{tool}: {title}";
}
