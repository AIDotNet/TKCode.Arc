using System.Text.Json.Nodes;

namespace TKCode.Arc;

/// <summary>
/// The built-in sample data (invented: a finance team asks what holds up its March books close). A <see cref="ToolCall"/>
/// without an item shows <see cref="WebSearch"/>, a <see cref="ToolCallGroup"/> without calls shows
/// <see cref="SettledCalls"/>, and <see cref="ToolCallSimulation"/> plays <see cref="Script"/>. Every call returned is new
/// and marked as sample data: Allow and Retry on it are simulated when nothing handles them.
/// </summary>
public static class ToolCallSamples
{
    /// <summary>The tool names used by the samples.</summary>
    public const string TrackerTool = "Tracker";

    /// <inheritdoc cref="TrackerTool"/>
    public const string PlannerTool = "Planner";

    /// <inheritdoc cref="TrackerTool"/>
    public const string WarehouseTool = "Warehouse";

    /// <inheritdoc cref="TrackerTool"/>
    public const string ChatTool = "Chat";

    /// <inheritdoc cref="TrackerTool"/>
    public const string WebTool = "Web";

    /// <summary>A finished web search with three results (what a lone <see cref="ToolCall"/> shows by default).</summary>
    public static ToolCallItem WebSearch()
    {
        var item = new ToolCallItem("web-search", WebTool, "Searched the web for spring easing curves", ToolCallStatus.Success, ArcIcons.Globe)
        {
            RunningTitle = "Searching the web for spring easing curves",
            Output = new ToolCallListOutput(
            [
                new ToolCallListEntry("1", "Critically damped springs for interface motion", "guide"),
                new ToolCallListEntry("2", "Choosing a visual duration instead of stiffness", "article"),
                new ToolCallListEntry("3", "Why springs keep their velocity when retargeted", "notes")
            ]),
            Duration = TimeSpan.FromMilliseconds(640),
            IsSample = true
        };
        item.Input.Add(new ToolCallArgument("query", "spring easing curves interface motion"));
        item.Input.Add(new ToolCallArgument("limit", "3"));
        return item;
    }

    /// <summary>Call 1: a ticket search with four results (1180ms).</summary>
    public static ToolCallItem TicketSearch(ToolCallStatus status = ToolCallStatus.Success)
    {
        var item = new ToolCallItem("tickets", TrackerTool, "Searched tickets in finance/close", status, ArcIcons.Inbox)
        {
            RunningTitle = "Searching tickets in finance/close",
            Output = new ToolCallListOutput(
            [
                new ToolCallListEntry("#482", "Invoice export stops at 10,000 rows", "export"),
                new ToolCallListEntry("#491", "CSV dates use the wrong time zone", "format"),
                new ToolCallListEntry("#503", "Retry sends the same file twice", "delivery"),
                new ToolCallListEntry("#507", "Export button stays grey in one browser", "web")
            ]),
            Summary = "4 results",
            Duration = TimeSpan.FromMilliseconds(1180),
            IsSample = true
        };
        item.Input.Add(new ToolCallArgument("project", "finance/close"));
        item.Input.Add(new ToolCallArgument("query", "status:open tag:close-blocker month:march"));
        item.Input.Add(new ToolCallArgument("sort", "updated"));
        return item;
    }

    /// <summary>Call 2: a checklist read whose JSON runs 35 lines (760ms).</summary>
    public static ToolCallItem ChecklistRead(ToolCallStatus status = ToolCallStatus.Success)
    {
        var item = new ToolCallItem("checklist", PlannerTool, "Read the March close checklist", status, ArcIcons.ListChecks)
        {
            RunningTitle = "Reading the March close checklist",
            Output = new ToolCallJsonOutput(ChecklistJson()),
            Summary = "2 blocked",
            Duration = TimeSpan.FromMilliseconds(760),
            IsSample = true
        };
        item.Input.Add(new ToolCallArgument("board", "March close"));
        item.Input.Add(new ToolCallArgument("team", "FIN"));
        item.Input.Add(new ToolCallArgument("include", "tasks, owners"));
        return item;
    }

    /// <summary>Call 3: a query of the export job log. As <see cref="ToolCallStatus.Error"/> it timed out; once retried it
    /// returns two rows (420ms).</summary>
    public static ToolCallItem JobLogQuery(ToolCallStatus status = ToolCallStatus.Success)
    {
        var item = new ToolCallItem("job-log", WarehouseTool, "Read the export job log", status, ArcIcons.Database)
        {
            RunningTitle = "Reading the export job log",
            ErrorTitle = "Could not read the export job log",
            Message = "Connection to reporting_replica timed out after 5s",
            Output = new ToolCallTableOutput(
                ["job", "step", "finished_at"],
                [
                    ["0112", "build_invoice_rows", null],
                    ["0113", "write_csv_batches", null]
                ]),
            Summary = "2 rows",
            Duration = TimeSpan.FromMilliseconds(420),
            IsSample = true
        };
        item.Input.Add(new ToolCallArgument("database", "reporting_replica"));
        item.Input.Add(new ToolCallArgument("sql", "select job, step, finished_at from export_runs where finished_at is null order by job"));
        return item;
    }

    /// <summary>Call 4: posting a note to a channel, which needs approval first; once allowed it sends one message
    /// (610ms).</summary>
    public static ToolCallItem StatusNote(ToolCallStatus status = ToolCallStatus.Approval)
    {
        var item = new ToolCallItem("status-note", ChatTool, "Posted to #finance-ops", status, ArcIcons.MessageCircle)
        {
            RunningTitle = "Posting to #finance-ops",
            ApprovalTitle = "Post a status note to #finance-ops",
            Message = "Sends as Ledger bot to 36 people in #finance-ops",
            Output = new ToolCallTextOutput("Sent to #finance-ops at 09:15 as Ledger bot."),
            Summary = "1 message",
            Duration = TimeSpan.FromMilliseconds(610),
            IsSample = true
        };
        item.Input.Add(new ToolCallArgument("channel", "#finance-ops"));
        item.Input.Add(new ToolCallArgument("text",
            "The March close waits on #482 (export stops at 10,000 rows) and job 0112 on the reporting replica. The 31st still holds if both land by Thursday."));
        return item;
    }

    /// <summary>The four calls of the sample turn, all finished (1180 + 760 + 420 + 610ms = "3.0s"): what a group shows
    /// when it is given no calls.</summary>
    public static IReadOnlyList<ToolCallItem> SettledCalls() =>
    [
        TicketSearch(),
        ChecklistRead(),
        JobLogQuery(),
        StatusNote(ToolCallStatus.Success)
    ];

    /// <summary>
    /// The simulated turn <see cref="ToolCallSimulation"/> plays by default: the first call is appended 630ms after the
    /// start and each later one about 225ms after the one before settles (265ms after the retried query); the ticket
    /// search and the checklist read succeed, the job log query times out after 1.4s and waits for Retry (the retry runs
    /// 700ms), and the status note waits for Allow or Deny.
    /// </summary>
    public static IReadOnlyList<ToolCallSimulationStep> Script() =>
    [
        new(TicketSearch()) { Delay = TimeSpan.FromMilliseconds(630) },
        new(ChecklistRead()),
        new(JobLogQuery(), ToolCallSimulationOutcome.FailsUntilRetried) { RunFor = TimeSpan.FromMilliseconds(1400) },
        new(StatusNote(), ToolCallSimulationOutcome.NeedsApproval) { Delay = TimeSpan.FromMilliseconds(265) }
    ];

    /// <summary>The checklist the sample planner returns (35 lines once indented).</summary>
    private static JsonNode ChecklistJson() => new JsonObject
    {
        ["board"] = "March close",
        ["owner"] = "Iris Novak",
        ["due"] = "2026-03-31",
        ["progress"] = 0.72,
        ["health"] = "at risk",
        ["milestones"] = new JsonArray(
            new JsonObject { ["name"] = "Ledger lock", ["date"] = "2026-03-27", ["done"] = true },
            new JsonObject { ["name"] = "Board pack", ["date"] = "2026-03-31", ["done"] = false }),
        ["blocked"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "FIN-77",
                ["title"] = "Export the March invoices",
                ["assignee"] = "Tomas Reyes",
                ["blockedBy"] = "finance/close#482"
            },
            new JsonObject
            {
                ["id"] = "FIN-81",
                ["title"] = "Reconcile the replica totals",
                ["assignee"] = "Ana Ibarra",
                ["blockedBy"] = null
            }),
        ["openTasks"] = 9,
        ["doneTasks"] = 31
    };
}
