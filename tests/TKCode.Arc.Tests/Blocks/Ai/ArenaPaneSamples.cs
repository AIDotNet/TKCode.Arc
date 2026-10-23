using Avalonia.Layout;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the arena pane and its narrow tabs (tests and snapshots): states the way the arena fills them,
/// the arena's English labels, and answers of our own (spec section 10: the SQL and debounce prompts).
/// </summary>
internal static class ArenaPaneSamples
{
    public static readonly ModelArenaLabels Labels = new();

    /// <summary>A long answer: a lead paragraph, a small heading, a list and a code block that keeps going.</summary>
    public const string LongAnswer =
        "Keep the newest row for each order number and delete the rest. A window function ranks the copies, so the query " +
        "reads the table once.\n\n" +
        "#### The query\n\n" +
        "```sql\n" +
        "with ranked as (\n" +
        "  select id,\n" +
        "         row_number() over (partition by order_number order by updated_at desc) as copy\n" +
        "  from orders\n" +
        ")\n" +
        "delete from orders\n" +
        "using ranked\n" +
        "where orders.id = ranked.id\n" +
        "  and ranked.copy > 1;\n" +
        "```\n\n" +
        "A few things worth checking first:\n\n" +
        "- **Preview it.** Run the `select` part alone and count the rows with `copy > 1`.\n" +
        "- **Index it.** An index on `(order_number, updated_at desc)` keeps the sort cheap.\n" +
        "- **Wrap it.** Do the delete in a transaction so a mistake rolls back.\n";

    /// <summary>A short answer that fits the pane.</summary>
    public const string ShortAnswer =
        "Debounce waits until the input goes quiet; throttle lets one call through per window.\n\n" +
        "- **Search box:** debounce, so one request goes out when typing stops.\n" +
        "- **Scroll bar:** throttle, so progress still moves while the page scrolls.";

    public static ArenaPaneState State(ArenaSlot slot = ArenaSlot.A, AiModel? model = null,
        ArenaPaneStatus status = ArenaPaneStatus.Idle, ArenaMetrics? metrics = null) =>
        new(slot)
        {
            Model = model ?? (slot == ArenaSlot.A ? ArenaStatusSamples.Lumen() : ArenaStatusSamples.Kite()),
            Status = status,
            Metrics = metrics ?? ArenaMetrics.Empty
        };

    /// <summary>Metrics of a finished answer: 1.83s to the first token, 152 tok/s, $0.0041.</summary>
    public static ArenaMetrics Finished(double first = 1.83, double speed = 152, decimal cost = 0.0041m) =>
        new(TimeSpan.FromSeconds(first), speed, TimeSpan.FromSeconds(first + 2), 120, 400, cost, false);

    public static ArenaPane Pane(ArenaPaneState state, bool revealed = false, double width = 392) => new()
    {
        State = state,
        Labels = Labels,
        IsRevealed = revealed,
        Width = width,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };

    public static ArenaPaneTabs Tabs(ArenaPaneState a, ArenaPaneState b, bool revealed = false) => new()
    {
        PaneA = a,
        PaneB = b,
        Labels = Labels,
        IsRevealed = revealed,
        TabsName = Labels.Answers
    };
}
