using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class TableMultilineCellsDemo : UserControl
{
    private sealed record Job(string Name, string Error, int Attempts);

    private static readonly Job[] s_jobs =
    [
        new("nightly-export", "Timed out after 30 s.", 3),
        new("invoice-sync", "HTTP 502 from billing-api.\nRetried with backoff; the upstream recovered at 02:14.", 2),
        new("thumbnail-render", "Out of memory while decoding /media/uploads/2026/11/campaign-hero-final-v7.psd (412 MB). Raise the worker memory limit or convert the file before upload.", 1),
        new("search-reindex", "Shard 4 rejected the bulk request.\nmapping conflict: field \"tags\" is keyword, got text\nSee the reindex log for the 18 affected documents.", 4),
        new("cache-warmup", "Skipped: the cache is already warm.", 1)
    ];

    public TableMultilineCellsDemo()
    {
        InitializeComponent();
        Table.ItemsSource = s_jobs;
        Table.Columns.Add(DataTableColumn.Create<Job>("Job", j => j.Name));
        Table.Columns.Add(new DataTableColumn("Error", j => ((Job)j).Error)
        {
            Width = new GridLength(1, GridUnitType.Star),
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3
        });
        Table.Columns.Add(DataTableColumn.Create<Job>("Attempts", j => j.Attempts));
    }
}
