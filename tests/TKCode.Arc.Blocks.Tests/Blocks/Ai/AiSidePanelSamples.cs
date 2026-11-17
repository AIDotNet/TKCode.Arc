using System.Runtime.CompilerServices;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for <see cref="AiSidePanel"/> (spec 10.1, 10.2): a design note about offline sync, three
/// suggestions, and a scripted responder that answers them (a paragraph change, a table change, a selection question,
/// one failure). Written for the Gallery as well as the tests.
/// </summary>
internal static class AiSidePanelSamples
{
    public const string Summary =
        "Offline sync is a planned change that would let the mobile app keep working when the connection drops, so that field staff do not lose the notes they write in basements, on trains or anywhere the signal fades out for a while.";

    public const string ShortSummary =
        "Offline sync lets the mobile app keep working without a connection, so field staff never lose notes written where the signal drops.";

    public static readonly string[] Suggestions = ["Summarize this note", "Shorten the summary", "Add a sync interval column"];

    /// <summary>The page: a summary, the scope, the storage limits and a table of tiers.</summary>
    public static AiPanelPage Page()
    {
        var page = new AiPanelPage { Title = "Offline sync", Meta = "Design note · Priya Raman · Updated Mar 3" };
        page.Blocks.Add(new AiPanelParagraph { Id = "summary", Label = "Summary", Text = Summary });
        page.Blocks.Add(new AiPanelHeading { Id = "scope-heading", Text = "Scope" });
        page.Blocks.Add(new AiPanelParagraph
        {
            Id = "scope",
            Label = "Scope",
            Text = "Notes, checklists and photos are cached on the device. Conflicts are resolved by keeping both versions and asking the author which one wins."
        });
        page.Blocks.Add(new AiPanelHeading { Id = "limits-heading", Text = "Storage limits" });
        page.Blocks.Add(new AiPanelParagraph
        {
            Id = "limits",
            Label = "Limits",
            Text = "Each device keeps up to 30 days of edits. Older items stay on the server and download on demand."
        });
        page.Blocks.Add(Table());
        return page;
    }

    /// <summary>The table of tiers; <paramref name="withSync"/> adds the sync interval column.</summary>
    public static AiPanelTable Table(bool withSync = false)
    {
        var table = new AiPanelTable { Id = "limits-table", Label = "Storage table" };
        table.Columns.Add(new AiPanelColumn { Header = "Tier", IsRowHeader = true });
        table.Columns.Add(new AiPanelColumn { Header = "Devices" });
        table.Columns.Add(new AiPanelColumn { Header = "Cache", Alignment = AiPanelColumnAlignment.Right });
        if (withSync)
        {
            table.Columns.Add(new AiPanelColumn { Header = "Sync", Alignment = AiPanelColumnAlignment.Right });
        }
        string[][] rows =
        [
            ["Basic", "1 device", "200 MB", "15 min"],
            ["Plus", "3 devices", "1 GB", "5 min"],
            ["Org", "Unlimited", "5 GB", "1 min"]
        ];
        foreach (var cells in rows)
        {
            var row = new AiPanelRow();
            row.Cells.AddRange(withSync ? cells : cells[..3]);
            table.Rows.Add(row);
        }
        return table;
    }

    /// <summary>
    /// The demo's scripted assistant: it reads the page it is given (so applied changes show in later answers), streams its
    /// text in small pieces, and fails the first free question it cannot answer once ("Connection timed out").
    /// </summary>
    public sealed class Responder : IAiPanelResponder
    {
        private bool _failedOnce;

        /// <summary>How long it "thinks" before the first text (the panel shows "Reading the page" at least 0.6s anyway).</summary>
        public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(200);

        public async ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }
            var page = request.Page;
            var prompt = request.Prompt.Trim();
            var summary = (page.Find("summary") as AiPanelParagraph)?.Text ?? string.Empty;
            var table = page.Find("limits-table") as AiPanelTable;
            var hasSync = table?.Columns.Any(c => c.Header == "Sync") == true;
            if (prompt.Equals("Shorten the summary", StringComparison.OrdinalIgnoreCase))
            {
                var words = CountWords(summary);
                if (words <= 25)
                {
                    return Reply($"The summary is already short at {words} words, so I would keep it.", cancellationToken);
                }
                var shorter = CountWords(ShortSummary);
                return Reply($"Here is a shorter summary. It keeps who it helps and why, and drops from {words} words to {shorter}.",
                             cancellationToken,
                             new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Label = "Summary", Text = ShortSummary }));
            }
            if (prompt.Equals("Add a sync interval column", StringComparison.OrdinalIgnoreCase))
            {
                if (hasSync)
                {
                    return Reply("The storage table already has a sync interval column.", cancellationToken);
                }
                return Reply("I added a column for how often each tier syncs in the background.", cancellationToken,
                             new AiPanelChangeProposal("limits-table", Table(withSync: true)));
            }
            if (prompt.Equals("Summarize this note", StringComparison.OrdinalIgnoreCase))
            {
                var second = hasSync
                    ? "Each device keeps 30 days of edits, and the storage table now lists how often each tier syncs, from every 15 minutes on Basic to every minute on Org."
                    : "Each device keeps 30 days of edits; the storage table gives the cache size of each tier, from 200 MB on Basic to 5 GB on Org.";
                return Reply("The note proposes offline sync for the mobile app so field staff keep working when the signal drops.\n\n" + second,
                             cancellationToken);
            }
            if (request.Selection is { } selection)
            {
                var quote = selection.Text.Length > 40 ? selection.Text[..40].TrimEnd() + "…" : selection.Text;
                return Reply($"“{quote}” is from {selection.Label}. I can shorten it, explain it or turn it into a list.", cancellationToken);
            }
            if (!_failedOnce)
            {
                _failedOnce = true;
                throw new InvalidOperationException("Connection timed out");
            }
            return Reply("I can summarize this note, shorten the summary or add a sync interval column. Select text on the page to ask about it.",
                         cancellationToken);
        }

        private static int CountWords(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        private static AiPanelReply Reply(string text, CancellationToken cancellationToken, AiPanelChangeProposal? change = null) =>
            new(Pieces(text, cancellationToken), change);

        /// <summary>The text in small pieces, like a network stream (the panel releases whole words on its own clock).</summary>
        private static async IAsyncEnumerable<string> Pieces(string text, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < text.Length; i += 12)
            {
                await Task.Delay(15, cancellationToken);
                yield return text.Substring(i, Math.Min(12, text.Length - i));
            }
        }
    }
}
