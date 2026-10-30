using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app article with a summarizer that returns whole results, paced by TldrStreams.FromResult.</summary>
public partial class TldrToggleYourSummarizerDemo : UserControl
{
    private static readonly TldrArticle s_article = new()
    {
        Title = "Release notes: offline mode for the field app",
        SourceName = "Contoso Labs help center",
        AuthorName = "Tomas Berg",
        Date = "Oct 28, 2026",
        Paragraphs =
        [
            new("what", "Offline mode keeps the field app working when the signal drops. Forms, photos and signatures are saved on " +
                        "the device and sent as soon as a connection comes back, in the order they were made."),
            new("setup", "Admins turn it on per team under Settings, Devices. Each device then downloads the forms and the last " +
                         "thirty days of jobs, which takes about a minute on a typical connection."),
            new("conflicts", "When two people edit the same job while offline, the later change wins field by field, and the " +
                             "earlier value is kept in the job's history so nothing is lost."),
            new("limits", "Large attachments over 50 MB wait for Wi-Fi, and reports that need live data stay unavailable until " +
                          "the device is back online."),
        ],
    };

    private static readonly string[] s_summaries =
    [
        "Offline mode saves forms, photos and signatures on the device and sends them when the signal returns.",
        "Offline mode keeps the field app working without signal: work is saved on the device and sent in order once a " +
        "connection returns. Admins enable it per team, and conflicting edits resolve field by field with history kept.",
    ];

    private static readonly TldrKeyPoint[] s_points =
    [
        new("what", "Work is saved on the device and sent in order when the signal returns."),
        new("setup", "Admins turn it on per team; devices download forms and 30 days of jobs."),
        new("conflicts", "Conflicting edits resolve field by field and keep the earlier value in history."),
        new("limits", "Attachments over 50 MB wait for Wi-Fi; live reports need a connection."),
    ];

    public TldrToggleYourSummarizerDemo()
    {
        InitializeComponent();
        Card.Article = s_article;
        Card.Model = TldrToggleDemoData.Model;
        Card.Summarizer = Summarize;
        Card.Completed += (_, e) => Log.Text = $"Completed: {e.Mode}, length {e.Length}, {e.Words} words.";
    }

    private static IAsyncEnumerable<TldrStreamEvent> Summarize(TldrRequest request)
    {
        // A provider that returns whole results: the card still streams them, paced by FromResult.
        if (request.Mode == TldrMode.KeyPoints)
        {
            return TldrStreams.FromResult(s_points.Take(request.PointCount).ToArray(), request.CancellationToken);
        }
        return TldrStreams.FromResult(s_summaries[request.Length <= 2 ? 0 : 1], request.CancellationToken);
    }
}
