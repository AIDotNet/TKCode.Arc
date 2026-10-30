using Avalonia.Collections;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Sessions for the ContextWindow demos. The models are real, with the context window from <see cref="AiModelCatalog"/>;
/// the output reserve is what this sample app asks for in one reply (<see cref="ReplyReserve"/>, at most the model's
/// published output limit). The items, turns, files and token counts are invented. Each property returns new lists, so
/// every card gets its own.
/// </summary>
internal static class ContextWindowDemoData
{
    /// <summary>The longest reply the sample app requests, held back as the output reserve.</summary>
    public const long ReplyReserve = 32_000;

    /// <summary>
    /// The coding session's models: Claude Sonnet 5.5 (1M window), Grok 4.7 (500K) and Mistral Medium 3.5 (256K); the
    /// session overfills the two smaller windows, so switching to them compacts.
    /// </summary>
    public static AvaloniaList<ContextWindowModel> SessionModels =>
        [ModelOf(AiModelCatalog.ClaudeSonnet55), ModelOf(AiModelCatalog.Grok47), ModelOf(AiModelCatalog.MistralMedium35)];

    /// <summary>A session of 436K tokens: a 3.8K system prompt, 30.2K of tools, 98K of files and 24 turns (304K).</summary>
    public static AvaloniaList<ContextWindowItem> SessionItems
    {
        get
        {
            var items = new AvaloniaList<ContextWindowItem>
            {
                new(ContextWindowCategory.System, "System prompt", 3_800, "system"),
                new(ContextWindowCategory.Tools, "search_docs", 12_400, "tool-search-docs"),
                new(ContextWindowCategory.Tools, "run_tests", 9_100, "tool-run-tests"),
                new(ContextWindowCategory.Tools, "query_db", 8_700, "tool-query-db"),
                new(ContextWindowCategory.Files, "AGENTS.md", 6_200, "file-agents"),
                new(ContextWindowCategory.Files, "memory/project-notes.md", 21_800, "file-notes"),
                new(ContextWindowCategory.Files, "schema.sql", 20_500, "file-schema"),
                new(ContextWindowCategory.Files, "src/ingest/queue.go", 49_500, "file-queue")
            };
            for (var i = 0; i < s_messages.Length; i++)
            {
                var (label, tokens) = s_messages[i];
                items.Add(new ContextWindowItem(ContextWindowCategory.Messages, label, tokens, $"message-{i + 1}"));
            }
            return items;
        }
    }

    /// <summary>Five turns for Send message: 30.6K, 27.4K, 35.2K, 24.8K and 18.6K.</summary>
    public static AvaloniaList<ContextWindowTurn> SessionTurns =>
    [
        new("Add a dry-run flag to the import job", 30_600),
        new("Write tests for the dedupe step", 27_400),
        new("Why does the nightly sync time out?", 35_200),
        new("Rename the queue metrics to snake_case", 24_800),
        new("Draft release notes for 2.4", 18_600)
    ];

    /// <summary>Three files for Attach file: 62K, 21K and 18.5K.</summary>
    public static AvaloniaList<ContextWindowTurn> SessionFiles =>
    [
        new("spec/ingest-api.yaml", 62_000),
        new("docs/oncall-playbook.md", 21_000),
        new("src/ingest/dedupe.go", 18_500)
    ];

    /// <summary>The documentation session's models: Mistral Medium 3.5 and Mistral Small 4 (256K windows).</summary>
    public static IEnumerable<ContextWindowModel> Models() =>
        [ModelOf(AiModelCatalog.MistralMedium35), ModelOf(AiModelCatalog.MistralSmall4)];

    /// <summary>A catalog model with its context window and the sample app's reply reserve.</summary>
    public static ContextWindowModel ModelOf(AiModelInfo model) =>
        new(model.ApiId, model.Name, model.ContextWindow ?? 0, Math.Min(ReplyReserve, model.MaxOutputTokens ?? ReplyReserve));

    /// <summary>A documentation session; <paramref name="scale"/> multiplies every token count.</summary>
    public static IEnumerable<ContextWindowItem> Items(int scale = 1) =>
        DocsItems().Select(i => new ContextWindowItem(i.Category, i.Label, i.Tokens * scale, i.Id));

    private static IEnumerable<ContextWindowItem> DocsItems() =>
    [
        new ContextWindowItem(ContextWindowCategory.System, "Style guide and instructions", 2_400, "system"),
        new ContextWindowItem(ContextWindowCategory.Tools, "search_handbook", 3_100, "tool-search"),
        new ContextWindowItem(ContextWindowCategory.Tools, "open_page", 1_900, "tool-open"),
        new ContextWindowItem(ContextWindowCategory.Files, "handbook/billing.md", 14_600, "file-billing"),
        new ContextWindowItem(ContextWindowCategory.Files, "handbook/refunds.md", 9_800, "file-refunds"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Outline the refunds page", 8_200, "m1"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Add the partial refund rules", 11_400, "m2"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Check the wording against the style guide", 6_900, "m3"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Draft the FAQ section", 12_700, "m4"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Link the billing page", 5_300, "m5"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Shorten the introduction", 7_600, "m6")
    ];

    /// <summary>Four turns for Send message in the documentation session.</summary>
    public static IEnumerable<ContextWindowTurn> Turns() =>
    [
        new ContextWindowTurn("Write the cancellation section", 14_800),
        new ContextWindowTurn("Turn the table into steps", 9_600),
        new ContextWindowTurn("Find every mention of store credit", 17_200),
        new ContextWindowTurn("Proofread the whole page", 21_500)
    ];

    /// <summary>Two files for Attach file in the documentation session.</summary>
    public static IEnumerable<ContextWindowTurn> Files() =>
    [
        new ContextWindowTurn("handbook/invoices.md", 18_300),
        new ContextWindowTurn("exports/tickets-march.csv", 26_400)
    ];

    /// <summary>Replies of different sizes for the demo that adds its own items.</summary>
    public static readonly (string Label, long Tokens)[] Replies =
    [
        ("Summarize the open tickets", 9_400),
        ("Group the tickets by cause", 13_800),
        ("Draft a reply for the top issue", 7_200),
        ("Compare with last month", 16_500)
    ];

    // 22 older turns (279K, between 6K and 25K) and the newest two (11.6K and 13.4K).
    private static readonly (string Label, long Tokens)[] s_messages =
    [
        ("Plan the importer rewrite", 9_400),
        ("Fix the flaky retry test", 14_200),
        ("Map the CSV columns to the schema", 7_800),
        ("Explain the backoff settings", 18_600),
        ("Add logging around the batch writer", 11_300),
        ("Split the parser into two stages", 6_400),
        ("Review the queue consumer", 22_100),
        ("Handle empty rows in the import", 13_700),
        ("Bump the client library", 8_900),
        ("Trace the duplicate events", 16_200),
        ("Write a migration for the status column", 10_500),
        ("Profile the nightly job", 24_300),
        ("Tidy the config loader", 7_100),
        ("Add a health check endpoint", 12_800),
        ("Investigate the memory spike", 15_600),
        ("Cache the schema lookups", 9_700),
        ("Document the import flags", 19_400),
        ("Rename the staging tables", 6_900),
        ("Check the timezone handling", 11_800),
        ("Speed up the dedupe query", 13_100),
        ("Add retries to the uploader", 8_200),
        ("Summarize the open questions", 11_000),
        ("Draft the rollout checklist", 11_600),
        ("Review the failing import run", 13_400)
    ];
}
