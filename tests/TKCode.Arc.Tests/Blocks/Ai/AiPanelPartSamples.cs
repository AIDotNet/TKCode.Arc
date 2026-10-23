namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Invented sample data for the side panel parts (spec 10.1): a design note about offline sync.</summary>
internal static class AiPanelPartSamples
{
    public const string Summary =
        "Offline sync is a planned change that would let the mobile app keep working when the connection drops, so that field staff do not lose the notes they write in basements, on trains or anywhere the signal fades out for a while.";

    public const string ShortSummary =
        "Offline sync lets the mobile app keep working without a connection, so field staff never lose notes written where the signal drops.";

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
}
