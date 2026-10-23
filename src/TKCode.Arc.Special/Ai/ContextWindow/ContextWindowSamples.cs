using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The invented session a <see cref="ContextWindow"/> shows when it is given no data: three sample models (1M, 512K and
/// 400K windows), a coding session of 436K tokens (a system prompt, three tools, four files and 24 turns), five turns
/// for Send message and three files for Attach file. Sending from the start warns at the ninth turn (69%) and compacts
/// on its own at the thirteenth (80%). Each call returns new objects, so a page can start from the sample and change it.
/// The names, files and numbers are made up.
/// </summary>
public static class ContextWindowSamples
{
    private static readonly IBrush s_atlasMark = new ImmutableSolidColorBrush(Color.Parse("#E58F00"));

    /// <summary>Three sample models: Atlas 1 (1M window, 64K reserve), Cirrus M (512K, 32K) and Kestrel S (400K, 32K).</summary>
    public static IReadOnlyList<ContextWindowModel> CreateModels() =>
    [
        new("atlas-1", "Atlas 1", 1_000_000, 64_000, ArcIcons.Sparkles, s_atlasMark),
        new("cirrus-m", "Cirrus M", 512_000, 32_000, ArcIcons.Cloud),
        new("kestrel-s", "Kestrel S", 400_000, 32_000, ArcIcons.Zap)
    ];

    /// <summary>A session of 436K tokens: a 3.8K system prompt, 30.2K of tools, 98K of files and 24 turns (304K).</summary>
    public static IReadOnlyList<ContextWindowItem> CreateItems()
    {
        var items = new List<ContextWindowItem>
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

    /// <summary>Five turns for Send message: 30.6K, 27.4K, 35.2K, 24.8K and 18.6K.</summary>
    public static IReadOnlyList<ContextWindowTurn> CreateTurns() =>
    [
        new("Add a dry-run flag to the import job", 30_600),
        new("Write tests for the dedupe step", 27_400),
        new("Why does the nightly sync time out?", 35_200),
        new("Rename the queue metrics to snake_case", 24_800),
        new("Draft release notes for 2.4", 18_600)
    ];

    /// <summary>Three files for Attach file: 62K, 21K and 18.5K.</summary>
    public static IReadOnlyList<ContextWindowTurn> CreateFiles() =>
    [
        new("spec/ingest-api.yaml", 62_000),
        new("docs/oncall-playbook.md", 21_000),
        new("src/ingest/dedupe.go", 18_500)
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
