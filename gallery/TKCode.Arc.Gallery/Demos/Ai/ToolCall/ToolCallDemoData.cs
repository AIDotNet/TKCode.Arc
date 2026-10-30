using System.Text.Json.Nodes;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The tool calls the demos show (invented: a finance team asks what holds up its March books close; the tools, tickets,
/// jobs, channel and people are made up). Each method returns new calls, since rows change them in place;
/// <see cref="Script"/> is the turn the preview's <see cref="ToolCallSimulation"/> plays (a simulation appends copies of
/// its calls, so the script can be shared). Demo content only.
/// </summary>
internal static class ToolCallDemoData
{
    /// <summary>The preview's simulated turn (shared: every run appends copies of these calls).</summary>
    public static IReadOnlyList<ToolCallSimulationStep> Script { get; } = NewScript();

    /// <summary>What the agent says before its first call.</summary>
    public const string Intro = "Let me look at what is holding up the close.";

    /// <summary>The answer streamed after the calls, in chunks; the last chunk depends on whether the note was posted.</summary>
    public static string[] Answer(bool denied) =>
    [
        "Two things hold up the March close: ", "the export bug #482 and ", "job 0112, which ", "has not finished on the replica. ",
        denied ? "I did not post to #finance-ops." : "I posted a note in #finance-ops."
    ];

    /// <summary>The tool names used by the demos.</summary>
    public const string TrackerTool = "Tracker";

    /// <inheritdoc cref="TrackerTool"/>
    public const string PlannerTool = "Planner";

    /// <inheritdoc cref="TrackerTool"/>
    public const string WarehouseTool = "Warehouse";

    /// <inheritdoc cref="TrackerTool"/>
    public const string ChatTool = "Chat";

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
            Duration = TimeSpan.FromMilliseconds(1180)
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
            Duration = TimeSpan.FromMilliseconds(760)
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
            Duration = TimeSpan.FromMilliseconds(420)
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
            Duration = TimeSpan.FromMilliseconds(610)
        };
        item.Input.Add(new ToolCallArgument("channel", "#finance-ops"));
        item.Input.Add(new ToolCallArgument("text",
            "The March close waits on #482 (export stops at 10,000 rows) and job 0112 on the reporting replica. The 31st still holds if both land by Thursday."));
        return item;
    }

    /// <summary>The four calls of the turn, all finished (1180 + 760 + 420 + 610ms = "3.0s").</summary>
    public static IReadOnlyList<ToolCallItem> SettledCalls() =>
    [
        TicketSearch(),
        ChecklistRead(),
        JobLogQuery(),
        StatusNote(ToolCallStatus.Success)
    ];

    /// <summary>
    /// The simulated turn the preview's <see cref="ToolCallSimulation"/> plays: the first call is appended 630ms after the
    /// start and each later one about 225ms after the one before settles (265ms after the retried query); the ticket
    /// search and the checklist read succeed, the job log query times out after 1.4s and waits for Retry (the retry runs
    /// 700ms), and the status note waits for Allow or Deny.
    /// </summary>
    public static IReadOnlyList<ToolCallSimulationStep> NewScript() =>
    [
        new(TicketSearch()) { Delay = TimeSpan.FromMilliseconds(630) },
        new(ChecklistRead()),
        new(JobLogQuery(), ToolCallSimulationOutcome.FailsUntilRetried) { RunFor = TimeSpan.FromMilliseconds(1400) },
        new(StatusNote(), ToolCallSimulationOutcome.NeedsApproval) { Delay = TimeSpan.FromMilliseconds(265) }
    ];

    /// <summary>The checklist the planner returns (35 lines once indented).</summary>
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
