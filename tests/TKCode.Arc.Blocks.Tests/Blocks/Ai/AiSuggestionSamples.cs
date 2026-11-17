using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Invented sample commands and context for the "/" and "@" lists (ai-composer spec, section 10).</summary>
internal static class AiSuggestionSamples
{
    // Lucide glyphs that ArcIcons does not carry (book-open, list-todo, layout-template).
    private static readonly Geometry s_bookOpen = ArcIcons.Parse(
        "M12 5v16 M20.001 19A2 2 0 0022 17V5a2 2 0 00-1.999-2L16 3.002A5 5 0 0012 5a5 5 0 00-4-2H4a2 2 0 00-2 2v12a2 2 0 001.999 2H8a5 5 0 014 2 5 5 0 014-2z");

    private static readonly Geometry s_listTodo = ArcIcons.Parse(
        "M13 5h8 M13 12h8 M13 19h8 M3 17l2 2 4-4 M4 4h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z");

    private static readonly Geometry s_layoutTemplate = ArcIcons.Parse(
        "M4 3h16a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-16a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M4 14h7a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-7a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M17 14h3a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-3a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z");

    public static readonly string[] Groups = ["Files", "Docs"];

    public static AiCommand[] Commands() =>
    [
        new AiCommand { Id = "recap", Name = "recap", Description = "Boil a doc or file down to its main points", Icon = ArcIcons.FileText },
        new AiCommand { Id = "rewrite", Name = "rewrite", Description = "Rewrite in a clearer or friendlier tone", Icon = ArcIcons.PenLine },
        new AiCommand { Id = "outline", Name = "outline", Description = "Turn loose notes into a structured outline", Icon = ArcIcons.Lightbulb },
        new AiCommand { Id = "debug", Name = "debug", Description = "Track down and fix a problem in the code", Icon = ArcIcons.Wrench }
    ];

    public static AiContextItem[] Mentions() =>
    [
        new AiContextItem { Id = "f-export", Label = "export-worker.ts", Hint = "services/export", Group = "Files", Icon = ArcIcons.FileCode },
        new AiContextItem { Id = "f-usage", Label = "usage-oct.csv", Hint = "analytics/monthly", Group = "Files", Icon = ArcIcons.FileSpreadsheet },
        new AiContextItem { Id = "f-style", Label = "style-guide.pdf", Hint = "Brand, 3.1 MB", Group = "Files", Icon = ArcIcons.FileText },
        new AiContextItem { Id = "d-sprint", Label = "Sprint 15 review", Hint = "Wiki, edited Mar 3", Group = "Docs", Icon = s_bookOpen },
        new AiContextItem { Id = "d-timeouts", Label = "Export timeouts", Hint = "Tracker, OPS-318", Group = "Docs", Icon = s_listTodo },
        new AiContextItem { Id = "d-onboarding", Label = "Onboarding flow v2", Hint = "Design board, edited Mar 1", Group = "Docs", Icon = s_layoutTemplate }
    ];

    public static IReadOnlyList<AiSuggestionRow> CommandRows(string query = "") => AiSuggestionFilter.Commands(Commands(), query);

    public static IReadOnlyList<AiSuggestionRow> MentionRows(string query = "") => AiSuggestionFilter.Mentions(Mentions(), Groups, query);

    /// <summary><paramref name="count"/> invented one-group mention rows ("note-01.md" …) for scrolling tests.</summary>
    public static IReadOnlyList<AiSuggestionRow> ManyRows(int count, string? group = "Files") =>
        Enumerable.Range(1, count)
                  .Select(i => new AiSuggestionRow($"n{i:00}", $"note-{i:00}.md", $"notes/week-{i:00}", ArcIcons.FileText, group))
                  .ToArray();
}
