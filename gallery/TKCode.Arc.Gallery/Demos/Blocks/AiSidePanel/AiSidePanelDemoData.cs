namespace TKCode.Arc.Gallery.Demos;

/// <summary>An invented design note and a scripted responder for the AI side panel demos.</summary>
internal static class AiSidePanelDemoData
{
    public const string SummaryText =
        "Offline sync is a planned change that would let the mobile app keep working when the connection drops, so that field staff do not lose the notes they write in basements, on trains or anywhere the signal fades out for a while.";

    public static string[] Suggestions { get; } = ["Summarize this note", "Shorten the summary", "Add a sync interval column"];

    public static AiPanelPage Page()
    {
        var page = new AiPanelPage { Title = "Offline sync", Meta = "Design note · Priya Raman · Updated Mar 3" };
        page.Blocks.Add(new AiPanelParagraph { Id = "summary", Label = "Summary", Text = SummaryText });
        page.Blocks.Add(new AiPanelHeading { Id = "scope-heading", Text = "Scope" });
        page.Blocks.Add(new AiPanelParagraph
        {
            Id = "scope", Label = "Scope",
            Text = "Notes, checklists and photos are cached on the device. Conflicts are resolved by keeping both versions and asking the author which one wins."
        });
        page.Blocks.Add(new AiPanelHeading { Id = "limits-heading", Text = "Storage limits" });
        page.Blocks.Add(new AiPanelParagraph
        {
            Id = "limits", Label = "Limits",
            Text = "Each device keeps up to 30 days of edits. Older items stay on the server and download on demand."
        });
        var table = new AiPanelTable { Id = "limits-table", Label = "Storage table" };
        table.Columns.Add(new AiPanelColumn { Header = "Tier", IsRowHeader = true });
        table.Columns.Add(new AiPanelColumn { Header = "Devices" });
        table.Columns.Add(new AiPanelColumn { Header = "Cache", Alignment = AiPanelColumnAlignment.Right });
        table.Rows.Add(Row("Basic", "1 device", "200 MB"));
        table.Rows.Add(Row("Plus", "3 devices", "1 GB"));
        table.Rows.Add(Row("Org", "Unlimited", "5 GB"));
        page.Blocks.Add(table);
        return page;
    }

    private static AiPanelRow Row(params string[] cells)
    {
        var row = new AiPanelRow();
        row.Cells.AddRange(cells);
        return row;
    }

    /// <summary>
    /// Answers a few prompts from a script: a shorter summary, a new table column, a summary of the note, a question about a
    /// selection; the first question it cannot answer fails once, so Retry can be tried.
    /// </summary>
    public sealed class ScriptedResponder : IAiPanelResponder
    {
        private bool _failedOnce;

        public async ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(600, cancellationToken);
            var prompt = request.Prompt.Trim();

            if (prompt.Contains("shorten", StringComparison.OrdinalIgnoreCase) || prompt.Contains("shorter", StringComparison.OrdinalIgnoreCase))
            {
                var summary = request.Page.Find("summary") as AiPanelParagraph;
                if (summary is not null && summary.Text.Split(' ').Length <= 25)
                {
                    return AiPanelReply.FromText("The summary is already short at 21 words, so I would keep it.");
                }

                return AiPanelReply.FromText(
                    "Here is a shorter summary. It keeps who it helps and why, and drops from 45 words to 21.",
                    new AiPanelChangeProposal("summary", new AiPanelParagraph
                    {
                        Id = "summary", Label = "Summary",
                        Text = "Offline sync lets the mobile app keep working without a connection, so field staff never lose notes written where the signal drops."
                    }));
            }

            if (prompt.Contains("column", StringComparison.OrdinalIgnoreCase) || prompt.Contains("interval", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Page.Find("limits-table") is not AiPanelTable current || current.Columns.Any(c => c.Header == "Sync"))
                {
                    return AiPanelReply.FromText("The table already has a sync column.");
                }

                var table = (AiPanelTable)current.Clone();
                table.Columns.Add(new AiPanelColumn { Header = "Sync", Alignment = AiPanelColumnAlignment.Right });
                var minutes = new[] { "15 min", "5 min", "1 min" };
                for (var i = 0; i < table.Rows.Count; i++)
                {
                    table.Rows[i].Cells.Add(minutes[Math.Min(i, minutes.Length - 1)]);
                }

                return AiPanelReply.FromText("I added a column for how often each tier syncs in the background.", new AiPanelChangeProposal("limits-table", table));
            }

            if (prompt.Contains("summar", StringComparison.OrdinalIgnoreCase))
            {
                var hasSync = request.Page.Find("limits-table") is AiPanelTable t && t.Columns.Any(c => c.Header == "Sync");
                return AiPanelReply.FromText("This note proposes caching notes, checklists and photos on the device so the app works offline."
                    + $"\n\nConflicts keep both versions, and each device holds 30 days of edits{(hasSync ? ", synced every 1 to 15 minutes depending on the tier" : string.Empty)}.");
            }

            if (request.Selection is { } selection)
            {
                var quote = selection.Text.Length > 40 ? selection.Text[..40] + "…" : selection.Text;
                return AiPanelReply.FromText($"“{quote}” is from {selection.Label}. I can shorten it, explain it or turn it into a list.");
            }

            if (!_failedOnce)
            {
                _failedOnce = true;
                throw new InvalidOperationException("Connection timed out");
            }

            return AiPanelReply.FromText("I can summarize the note, shorten its summary or add a column to the storage table.");
        }
    }
}
