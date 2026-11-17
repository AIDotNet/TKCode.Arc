using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the arena's tests and snapshots (spec 10: seven made-up models of six made-up providers, three
/// sample prompts, the preset standings of 10.5). The marks are plain filled shapes on a 24 grid.
/// </summary>
internal static class ModelArenaSamples
{
    public static AiModel Model(string id, string name, string? shortName, string provider, string mark, string? color,
        decimal input, decimal output) => new()
    {
        Id = id,
        Name = name,
        ShortName = shortName,
        ProviderId = provider,
        Mark = Geometry.Parse(mark),
        MarkColor = color is null ? null : new ImmutableSolidColorBrush(Color.Parse(color)),
        InputPrice = input,
        OutputPrice = output
    };

    /// <summary>The pool of spec 10.1, in its order (the first round pairs the first two).</summary>
    public static List<AiModel> Pool() =>
    [
        Model("lumen-pro", "Lumen 3 Pro", "Lumen Pro", "lumen-labs", "M12 7A5 5 0 1 0 12 17A5 5 0 1 0 12 7Z M11 1H13V5H11Z M11 19H13V23H11Z M1 11H5V13H1Z M19 11H23V13H19Z", "#D9822B", 4.00m, 18.00m),
        Model("kite-5", "Kite 5", null, "kitework", "M12 2L20.66 7V17L12 22L3.34 17V7Z", null, 2.50m, 10.00m),
        Model("lumen-swift", "Lumen 3 Swift", "Swift", "lumen-labs", "M12 8A4 4 0 1 0 12 16A4 4 0 1 0 12 8Z", "#D9822B", 0.80m, 3.20m),
        Model("basalt-ultra", "Basalt 2 Ultra", "Basalt", "basalt-ai", "M12 2L22 12L12 22L2 12Z", "#3D6FD6", 3.00m, 12.00m),
        Model("vireo-4", "Vireo 4", null, "vireo", "M12 3L22 21H2Z", "#7B4BC4", 1.50m, 6.00m),
        Model("tern-flash", "Tern 1.2 Flash", "Tern Flash", "tern-research", "M4 20L20 4V12L12 20Z", null, 0.20m, 0.60m),
        Model("marlin-xl", "Marlin XL", "Marlin", "marlin-systems", "M2 10H22V14H2Z M2 16H22V20H2Z", "#1F9A8A", 2.00m, 8.00m)
    ];

    /// <summary>The sample prompts of spec 10.2.</summary>
    public static List<ArenaPrompt> Prompts() =>
    [
        new() { Id = "sql", Label = "Dedupe orders in SQL", Text = "Write a SQL query that removes duplicate rows from an orders table and keeps only the newest row for each order number." },
        new() { Id = "debounce", Label = "Debounce vs throttle", Text = "Explain the difference between debounce and throttle to a new frontend developer, with one small example of each." },
        new() { Id = "move", Label = "Move a 1:1", Text = "Draft a short, friendly note asking Jonas to move our Friday 1:1 to Monday morning because of a release." }
    ];

    /// <summary>The preset standings of spec 10.5 (wins / ties / losses).</summary>
    public static void PresetStandings(ModelArena arena)
    {
        arena.Standings["lumen-pro"] = new ArenaRecord(3, 1, 1);
        arena.Standings["kite-5"] = new ArenaRecord(2, 0, 2);
        arena.Standings["basalt-ultra"] = new ArenaRecord(1, 1, 1);
        arena.Standings["vireo-4"] = new ArenaRecord(1, 0, 2);
        arena.Standings["tern-flash"] = new ArenaRecord(0, 1, 2);
    }

    /// <summary>An arena with the sample pool and prompts (no runner).</summary>
    public static ModelArena Arena(Action<ModelArena>? configure = null)
    {
        var arena = new ModelArena();
        foreach (var model in Pool())
        {
            arena.Contestants.Add(model);
        }
        foreach (var prompt in Prompts())
        {
            arena.Prompts.Add(prompt);
        }
        configure?.Invoke(arena);
        return arena;
    }

    /// <summary>A Markdown answer of <paramref name="words"/> words.</summary>
    public static string Words(int words, string word = "word")
    {
        var parts = new string[words];
        for (var i = 0; i < words; i++)
        {
            parts[i] = word + (i + 1);
        }
        return string.Join(' ', parts);
    }
}
