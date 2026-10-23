using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented models, sample prompts, scripted answers and a scripted runner for the model arena demos.</summary>
internal static class ModelArenaDemoData
{
    public const string SqlId = "sql";
    public const string DebounceId = "debounce";
    public const string NoteId = "note";

    public static ArenaPrompt[] Prompts() =>
    [
        new()
        {
            Id = SqlId, Label = "Dedupe orders in SQL",
            Text = "Write a SQL query that removes duplicate rows from an orders table and keeps only the newest row for each order number."
        },
        new()
        {
            Id = DebounceId, Label = "Debounce vs throttle",
            Text = "Explain the difference between debounce and throttle to a new frontend developer, with one small example of each."
        },
        new()
        {
            Id = NoteId, Label = "Move a 1:1",
            Text = "Draft a short, friendly note asking Jonas to move our Friday 1:1 to Monday morning because of a release."
        }
    ];

    /// <summary>Seven invented models; the first two are the demo's first pair.</summary>
    public static AiModel[] Models() =>
    [
        Model("lumen-pro", "Lumen 3 Pro", "Lumen Pro", "lumen", "#E8A317", 4m, 18m, "M12 2 21 7v10l-9 5-9-5V7Z"),
        Model("kite-5", "Kite 5", "Kite 5", "kitework", "#13A08A", 2.5m, 10m, "M12 2 22 12 12 22 2 12Z"),
        Model("lumen-swift", "Lumen 3 Swift", "Swift", "lumen", "#E8A317", 0.8m, 3.2m, "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z"),
        Model("basalt-ultra", "Basalt 2 Ultra", "Basalt", "basalt", "#2F6FEB", 3m, 12m, "M4 4h16v16H4Z"),
        Model("vireo-4", "Vireo 4", "Vireo 4", "vireo", "#7B4FD6", 1.5m, 6m, "M12 3 22 20H2Z"),
        Model("tern-flash", "Tern 1.2 Flash", "Tern Flash", "tern", "#2F6FEB", 0.2m, 0.6m, "M3 12 12 3l9 9-9 9Z"),
        Model("marlin-xl", "Marlin XL", "Marlin", "marlin", "#7B4FD6", 2m, 8m, "M3 8c3-3 6-3 9 0s6 3 9 0v8c-3 3-6 3-9 0s-6-3-9 0Z")
    ];

    /// <summary>Starting standings so the first open of the standings has rows.</summary>
    public static void SeedStandings(ModelArena arena)
    {
        arena.Standings["lumen-pro"] = new ArenaRecord(3, 1, 1);
        arena.Standings["kite-5"] = new ArenaRecord(2, 0, 2);
        arena.Standings["basalt-ultra"] = new ArenaRecord(1, 1, 1);
        arena.Standings["vireo-4"] = new ArenaRecord(1, 0, 2);
        arena.Standings["tern-flash"] = new ArenaRecord(0, 1, 2);
    }

    private static AiModel Model(string id, string name, string shortName, string provider, string color, decimal input, decimal output, string path) => new()
    {
        Id = id, Name = name, ShortName = shortName, ProviderId = provider, InputPrice = input, OutputPrice = output, IsSamplePrice = true,
        Mark = Geometry.Parse(path), MarkColor = new SolidColorBrush(Color.Parse(color))
    };

    /// <summary>Streams a scripted answer; it knows only the three sample prompts and fails on anything else.</summary>
    public sealed class Runner : IArenaRunner
    {
        /// <summary>When true the next stream breaks halfway, once; the demo's switch sets it.</summary>
        public bool BreakNext { get; set; }

        public async IAsyncEnumerable<ArenaStreamEvent> RunAsync(AiModel model, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var promptId = IdOf(prompt);
            var firstWait = 450 + Math.Abs(model.Id.GetHashCode()) % 1300;
            await Task.Delay(firstWait, cancellationToken);
            if (promptId is null)
            {
                throw new ArenaRunException(
                    "This demo only knows the three sample prompts. Connect a runner to stream real models.", "No demo answer", isRetryable: false);
            }

            var text = Answer(promptId, model);
            var breakAt = BreakNext ? text.Length * 4 / 10 : -1;
            BreakNext = false;
            var chunkChars = 24 + Math.Abs(model.Id.GetHashCode() / 7) % 12;
            for (var i = 0; i < text.Length; i += chunkChars)
            {
                if (breakAt >= 0 && i >= breakAt)
                {
                    throw new ArenaRunException("The stream ended before this answer was complete.", "Connection dropped");
                }

                await Task.Delay(40, cancellationToken);
                yield return text.Substring(i, Math.Min(chunkChars, text.Length - i));
            }
        }

        private static string? IdOf(string prompt)
        {
            foreach (var sample in Prompts())
            {
                if (string.Equals(sample.Text, prompt, StringComparison.Ordinal))
                {
                    return sample.Id;
                }
            }

            return null;
        }

        // Two voices per prompt, chosen by the model: the answers differ in length so one finishes first.
        private static string Answer(string promptId, AiModel model)
        {
            var first = Math.Abs(model.Id.GetHashCode()) % 2 == 0;
            return (promptId, first) switch
            {
                (SqlId, true) => SqlA,
                (SqlId, false) => SqlB,
                (DebounceId, true) => DebounceA,
                (DebounceId, false) => DebounceB,
                (NoteId, true) => NoteA,
                _ => NoteB
            };
        }

        private const string SqlA =
            "Number the rows of each order and delete everything but the newest one:\n\n```sql\nWITH ranked AS (\n  SELECT id,\n         ROW_NUMBER() OVER (\n           PARTITION BY order_number\n           ORDER BY updated_at DESC, id DESC\n         ) AS rn\n  FROM orders\n)\nDELETE FROM orders\nWHERE id IN (SELECT id FROM ranked WHERE rn > 1);\n```\n\nBefore you run it:\n\n1. Replace `DELETE` with a `SELECT` first and look at what would go.\n2. Wrap the statement in a transaction so a surprise can be rolled back.\n3. An index on `(order_number, updated_at)` keeps it fast on a big table.\n";

        private const string SqlB =
            "A self-join does the same job without window functions:\n\n```sql\nDELETE FROM orders o\nUSING orders newer\nWHERE o.order_number = newer.order_number\n  AND (o.updated_at, o.id) < (newer.updated_at, newer.id);\n```\n\nIt keeps the row with the latest `updated_at` and breaks ties with `id`. Try it on a copy of the table first.\n";

        private const string DebounceA =
            "## Debounce\n\nDebounce waits until the calls stop. Each new call restarts the timer, so the function runs once, after a quiet period. A search box is the classic case: wait until the person stops typing, then fetch.\n\n## Throttle\n\nThrottle lets a call through at most once per interval, however often it is triggered. A scroll listener that updates a progress bar can run every 100 ms instead of on every pixel.\n\nPick debounce when only the final value matters, and throttle when you want steady updates while something keeps happening.\n";

        private const string DebounceB =
            "- **Debounce**: run after the calls stop, once.\n- **Throttle**: run during the calls, at most once per interval.\n\n```ts\nfunction debounce<T extends unknown[]>(fn: (...a: T) => void, ms: number) {\n  let timer: ReturnType<typeof setTimeout>;\n  return (...args: T) => {\n    clearTimeout(timer);\n    timer = setTimeout(() => fn(...args), ms);\n  };\n}\n```\n\nThrottle is the same idea with a timestamp check instead of a restarted timer.\n";

        private const string NoteA =
            "Hi Jonas,\n\nWould it be all right to move our Friday 1:1 to Monday morning? We are shipping a release on Friday and I would like to keep the afternoon clear for it.\n\nAny time before 11 works for me. Thanks for understanding!\n";

        private const string NoteB =
            "**Short**\n\nHi Jonas, can we move Friday's 1:1 to Monday morning? Release day. Thanks!\n\n**Warmer**\n\nHi Jonas, the release is landing on Friday, so I would love to shift our 1:1 to Monday morning. Is that okay with you? I am happy to take whichever slot suits you best.\n";
    }
}
