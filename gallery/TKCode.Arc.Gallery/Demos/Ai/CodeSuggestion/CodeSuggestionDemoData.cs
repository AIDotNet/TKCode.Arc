namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented suggestions the code suggestion demos share: a search hook that fires a request on every keystroke with
/// two hunks that skip blank queries and debounce and cancel stale requests, and the simulated one of the Preview, a
/// billing helper that gains retries with backoff. Demo content only; the model is a real one from
/// <see cref="AiModelCatalog"/>.
/// </summary>
internal static class CodeSuggestionDemoData
{
    public const string FileName = "use-search.ts";

    public const string FilePath = "src/hooks";

    public const string Instruction = "Debounce the search";

    public const string Code =
        "import { useEffect, useState } from \"react\";\n" +
        "\n" +
        "export function useSearch(query: string) {\n" +
        "  const [results, setResults] = useState<string[]>([]);\n" +
        "  useEffect(() => {\n" +
        "    fetch(`/api/search?q=${query}`)\n" +
        "      .then((res) => res.json())\n" +
        "      .then(setResults);\n" +
        "  }, [query]);\n" +
        "  return results;\n" +
        "}";

    /// <summary>The two hunks, complete (a fresh copy each call).</summary>
    public static CodeSuggestionHunk[] Hunks() =>
    [
        new("guard", 4, 1,
            "  useEffect(() => {",
            "    if (!query.trim()) {",
            "      setResults([]);",
            "      return;",
            "    }") { Summary = "Skip blank queries" },
        new("debounce", 5, 3,
            "    const controller = new AbortController();",
            "    const timer = setTimeout(() => {",
            "      fetch(`/api/search?q=${encodeURIComponent(query)}`, { signal: controller.signal })",
            "        .then((res) => res.json())",
            "        .then(setResults)",
            "        .catch(() => {});",
            "    }, 250);",
            "    return () => {",
            "      clearTimeout(timer);",
            "      controller.abort();",
            "    };") { Summary = "Debounce and cancel stale requests" },
    ];

    /// <summary>
    /// The model credited in the demos: OpenAI's GPT-6.1 Sol (checked on <see cref="AiModelCatalog.CheckedOn"/>), with a
    /// neutral mark; no brand mark is drawn.
    /// </summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.Gpt61Sol.Name, ArcIcons.Sparkles);

    // The simulated suggestion of the Preview --------------------------------------------------------------------------

    public const string SimulatedFileName = "fetch-invoice.ts";

    public const string SimulatedFilePath = "src/billing";

    public const string SimulatedInstruction = "Add retry with backoff";

    public const string SimulatedCode =
        "import { db } from \"./db\";\n" +
        "import type { Invoice } from \"./types\";\n" +
        "\n" +
        "export async function fetchInvoice(id: string): Promise<Invoice> {\n" +
        "  const url = `${process.env.BILLING_API}/invoices/${id}`;\n" +
        "  const res = await fetch(url);\n" +
        "  if (!res.ok) {\n" +
        "    throw new Error(`Invoice ${id} failed: ${res.status}`);\n" +
        "  }\n" +
        "  const invoice = (await res.json()) as Invoice;\n" +
        "  await db.invoices.upsert(invoice);\n" +
        "  return invoice;\n" +
        "}";

    /// <summary>The three hunks the simulation streams (it copies them, so the list is shared).</summary>
    public static readonly IReadOnlyList<CodeSuggestionHunk> SimulatedHunks =
    [
        new("limits", 2, 0,
            "",
            "const MAX_ATTEMPTS = 4;",
            "const BASE_DELAY_MS = 250;") { Summary = "Add retry limits" },
        new("backoff", 5, 4,
            "  let res = await fetch(url);",
            "  for (let attempt = 1; attempt < MAX_ATTEMPTS; attempt++) {",
            "    if (res.ok || (res.status !== 429 && res.status < 500)) break;",
            "    await sleep(BASE_DELAY_MS * 2 ** (attempt - 1));",
            "    res = await fetch(url);",
            "  }",
            "  if (!res.ok) {",
            "    throw new Error(`Invoice ${id} failed after ${MAX_ATTEMPTS} tries: ${res.status}`);",
            "  }") { Summary = "Retry 429 and 5xx with backoff" },
        new("sleep", 13, 0,
            "",
            "function sleep(ms: number) {",
            "  return new Promise((resolve) => setTimeout(resolve, ms));",
            "}") { Summary = "Add a sleep helper" },
    ];

    /// <summary>Points the card at the simulated suggestion's file (the simulation itself holds the code and the hunks).</summary>
    public static void ShowSimulated(CodeSuggestion card)
    {
        card.FileName = SimulatedFileName;
        card.FilePath = SimulatedFilePath;
        card.Instruction = SimulatedInstruction;
        card.Model = Model;
    }

    /// <summary>Points the card at the demo file.</summary>
    public static void Show(CodeSuggestion card)
    {
        card.FileName = FileName;
        card.FilePath = FilePath;
        card.Instruction = Instruction;
        card.Model = Model;
        card.Hunks.Clear();
        card.Hunks.AddRange(Hunks());
        card.Code = Code;
    }
}
