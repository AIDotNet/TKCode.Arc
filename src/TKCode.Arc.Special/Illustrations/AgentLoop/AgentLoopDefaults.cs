using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The sample run an <see cref="AgentLoop"/> plays when it is given no iterations: an invented triage of a nightly
/// export that is missing rows, in three loops with one, two and two tool calls. The first check fails, the second loop's
/// first call fails and is retried, and the last check passes. The model, the tools and every result are made up.
/// </summary>
public static class AgentLoopDefaults
{
    /// <summary>The sample run's task (the caption while it runs).</summary>
    public const string Task = "Find out why the nightly export is missing rows, then tell the data team.";

    /// <summary>The sample run's summary (the caption once it is done).</summary>
    public const string Summary =
        "The export job timed out on the largest table. The batch size is fixed, ticket DATA-204 is open and the team knows.";

    /// <summary>The sample model's invented name.</summary>
    public const string ModelName = "Nimbus Pro 3";

    /// <summary>A new copy of the sample model (an invented name with a sparkle mark).</summary>
    public static ExplainerModel CreateModel() => new() { Name = ModelName, Mark = ArcIcons.Sparkles };

    /// <summary>A new copy of the sample run's three loops.</summary>
    public static IReadOnlyList<AgentLoopIteration> CreateIterations()
    {
        var ticket = AgentLoopIcons.Ticket;
        var database = ArcIcons.Database;
        var chat = ArcIcons.MessageCircle;
        return
        [
            new AgentLoopIteration(
                new AgentLoopContext("Request from Lena", "Nightly export short on rows", 1400, AgentLoopIcons.MessageSquareText),
                [new AgentLoopCall("Tracker", "Open ticket #418", "Rows missing since Mar 3", mark: ticket)],
                new AgentLoopCheck(false, "Cause not found yet")),
            new AgentLoopIteration(
                new AgentLoopContext("Ticket #418", "9 comments, opened Mar 3", 5800, ticket),
                [
                    new AgentLoopCall("Warehouse", "Count exported rows", "Query timed out after 30s", AgentLoopCallStatus.Error, database),
                    new AgentLoopCall("Warehouse", "Check last 7 days", "41,920 rows, one table short", mark: database)
                ],
                new AgentLoopCheck(true, "Largest table times out")),
            new AgentLoopIteration(
                new AgentLoopContext("Query output", "41,920 exported rows", 8600, database),
                [
                    new AgentLoopCall("Tracker", "Update status", "DATA-204 now in progress", mark: ticket),
                    new AgentLoopCall("Chat", "Post update", "Posted in #data-team", mark: chat)
                ],
                new AgentLoopCheck(true, "Ticket and team updated"))
        ];
    }

    /// <summary>The mark drawn for a context block without one.</summary>
    internal static Geometry ContextMark => AgentLoopIcons.MessageSquareText;

    /// <summary>The mark drawn for a tool call without one.</summary>
    internal static Geometry CallMark => ArcIcons.Wrench;
}

/// <summary>
/// Lucide glyphs the agent loop needs that Core <see cref="ArcIcons"/> and the shared explainer icons do not ship
/// (lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c>, ISC), parsed once through
/// <see cref="ArcIcons.Parse"/>.
/// </summary>
internal static class AgentLoopIcons
{
    // Lucide message-square-text, ISC.
    private const string MessageSquareTextData =
        "M22 17a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 21.286V5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2z M7 11h10 M7 15h6 M7 7h8";

    // Lucide ticket, ISC.
    private const string TicketData =
        "M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z M13 5v2 M13 17v2 M13 11v2";

    // Lucide workflow, ISC.
    private const string WorkflowData =
        "M5 3h4a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-4a2 2 0 0 1 -2 -2v-4a2 2 0 0 1 2 -2z M7 11v4a2 2 0 0 0 2 2h4 " +
        "M15 13h4a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-4a2 2 0 0 1 -2 -2v-4a2 2 0 0 1 2 -2z";

    // Lucide circle-stop, ISC.
    private const string CircleStopData =
        "M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M10 9h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z";

    /// <summary>A speech bubble with lines: a message.</summary>
    public static Geometry MessageSquareText => ArcIcons.Parse(MessageSquareTextData);

    /// <summary>A ticket: an issue in a tracker.</summary>
    public static Geometry Ticket => ArcIcons.Parse(TicketData);

    /// <summary>Two linked boxes: a pipeline or workflow.</summary>
    public static Geometry Workflow => ArcIcons.Parse(WorkflowData);

    /// <summary>A stop square in a circle: a run that stopped.</summary>
    public static Geometry CircleStop => ArcIcons.Parse(CircleStopData);
}
