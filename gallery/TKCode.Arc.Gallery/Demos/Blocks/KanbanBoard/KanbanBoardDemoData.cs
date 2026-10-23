using Avalonia.Collections;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The Kanban board demos' data (all invented): one release of a field-inspection app, "Field app 4.2", with four
/// columns (Building limited to 3 and Testing to 2, both full), four people, four labels and twelve cards whose order is
/// the order inside every column. New cards continue the numbering from FLD-245.
/// </summary>
public static class KanbanBoardDemoData
{
    public const string Title = "Field app 4.2";

    /// <summary>The year of the demo's due dates (shown as "Mar 14").</summary>
    private const int Year = 2026;

    public static AvaloniaList<KanbanColumn> CreateColumns(bool limits = true) =>
    [
        new KanbanColumn("queued", "Queued"),
        new KanbanColumn("building", "Building", limits ? 3 : null),
        new KanbanColumn("testing", "Testing", limits ? 2 : null),
        new KanbanColumn("shipped", "Shipped")
    ];

    public static AvaloniaList<ProjectPerson> CreatePeople() =>
    [
        new ProjectPerson("Amara Okafor", "amara"),
        new ProjectPerson("Jonas Lindqvist", "jonas"),
        new ProjectPerson("Mei Takahashi", "mei"),
        new ProjectPerson("Rafael Ortiz", "rafael")
    ];

    public static AvaloniaList<KanbanLabel> CreateLabels() =>
    [
        new KanbanLabel("sync", "Sync", KanbanLabelTone.Accent),
        new KanbanLabel("maps", "Maps", KanbanLabelTone.Warning),
        new KanbanLabel("interface", "Interface", KanbanLabelTone.Violet),
        new KanbanLabel("server", "Server", KanbanLabelTone.Blue)
    ];

    public static AvaloniaList<KanbanCard> CreateCards()
    {
        static KanbanCard Card(string key, string column, string label, string title, string owner, int points,
                               (int Month, int Day)? due = null, string? notes = null, params (string Text, bool Done)[] steps)
        {
            var card = new KanbanCard(key, title, column)
            {
                LabelKey = label,
                AssigneeKey = owner,
                Points = points,
                Due = due is { } d ? new DateOnly(Year, d.Month, d.Day) : null,
                Description = notes
            };
            foreach (var (text, done) in steps)
            {
                card.Checklist.Add(new KanbanChecklistItem(text, done));
            }
            return card;
        }

        return
        [
            Card("FLD-231", "queued", "sync", "Resume photo uploads after the app closes mid-transfer", "rafael", 5, (3, 14),
                "Large photo sets should pick up where they stopped instead of starting over.",
                ("Persist upload offsets", false), ("Resume on next launch", false), ("Show progress in the queue", false)),
            Card("FLD-236", "queued", "maps", "Cache tiles for the next inspection route", "mei", 3, (3, 18)),
            Card("FLD-240", "queued", "sync", "Conflict banner for edited reports", "jonas", 2),
            Card("FLD-244", "queued", "interface", "Large-type mode for the checklist screen", "amara", 1),
            Card("FLD-218", "building", "interface", "Signature pad on the handover form", "amara", 3, (3, 7),
                "Capture the client's signature at handover and attach it to the visit report.",
                ("Pad component", true), ("Clear and redo", true), ("Dark theme pass", false), ("Export as PNG", false)),
            Card("FLD-222", "building", "server", "Retry queue for failed report posts", "rafael", 5, (3, 6), null,
                ("Back-off schedule", true), ("Queue badge", false)),
            Card("FLD-226", "building", "sync", "Merge duplicate site visits", "jonas", 3, (3, 11)),
            Card("FLD-209", "testing", "sync", "Offline drafts survive an app update", "mei", 3, (3, 5), null,
                ("Draft schema version", true), ("Migration test", true), ("Upgrade from 4.1", false)),
            Card("FLD-213", "testing", "interface", "Site map pins use the client's colour", "amara", 2),
            Card("FLD-201", "shipped", "server", "Move photo storage to the regional bucket", "rafael", 8),
            Card("FLD-204", "shipped", "interface", "Settings screen grouping and labels", "amara", 3),
            Card("FLD-207", "shipped", "sync", "Background sync on metered networks", "jonas", 5)
        ];
    }

    /// <summary>Fills <paramref name="board"/> with the demo's columns, people, labels and cards.</summary>
    public static void Fill(KanbanBoard board, bool limits = true)
    {
        board.Title ??= Title;
        board.Columns = CreateColumns(limits);
        board.People = CreatePeople();
        board.Labels = CreateLabels();
        board.Cards = CreateCards();
    }

    /// <summary>A longer Queued column (for the over-limit and auto-scroll demos).</summary>
    public static IEnumerable<KanbanCard> CreateBacklog()
    {
        string[] titles =
        [
            "Field names in the export mapping",
            "Empty state for the visits list",
            "Proofread the visit summary email",
            "Wording for a failed photo upload",
            "Retire the old route planner screen",
            "Keyboard shortcuts for the report editor"
        ];
        string[] owners = ["amara", "jonas", "mei", "rafael"];
        for (var i = 0; i < titles.Length; i++)
        {
            yield return new KanbanCard($"FLD-25{i}", titles[i], "queued")
            {
                AssigneeKey = owners[i % owners.Length],
                LabelKey = i % 2 == 0 ? "interface" : "sync",
                Points = 1 + i % 3
            };
        }
    }
}
