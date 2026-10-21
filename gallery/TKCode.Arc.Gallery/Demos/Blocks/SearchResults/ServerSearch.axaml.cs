using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsServerSearchDemo : UserControl
{
    // What the "server" knows. A real app would call its search API instead.
    private sealed record Record(string Id, string Title, string Kind, string Location, string Snippet, string Meta);

    private static readonly Record[] s_index =
    [
        new("t-2091", "Export to CSV drops the last row", "Tickets", "#2091 · Billing", "When an export has more than 1,000 rows the last one is missing from the file.", "2h ago"),
        new("t-2088", "Can't export invoices from the mobile app", "Tickets", "#2088 · Mobile", "The export button stays disabled on iOS when the invoice has a discount.", "Yesterday"),
        new("t-2075", "Scheduled export runs twice", "Tickets", "#2075 · Platform", "Daily exports started twice after the move to the new scheduler.", "3d ago"),
        new("t-2063", "Password reset email never arrives", "Tickets", "#2063 · Accounts", "Customers on custom domains do not get the reset email; the bounce log shows a DNS error.", "5d ago"),
        new("a-export", "Export your data", "Articles", "Help center / Account", "Download every invoice, customer and payment as CSV or JSON. Large exports arrive by email.", "Updated 2w ago"),
        new("a-schedule", "Schedule a recurring export", "Articles", "Help center / Automation", "Pick a time and a format and the export is sent to your storage bucket every day.", "Updated 1mo ago"),
        new("a-reset", "Reset your password", "Articles", "Help center / Account", "Open the sign-in page, choose “Forgot password” and follow the link in the email.", "Updated 3mo ago"),
        new("m-export", "Export pipeline runbook", "Macros", "Support macros", "Steps to retry a failed export and the message to send to the customer.", "Used 41 times")
    ];

    private CancellationTokenSource? _pending;

    public SearchResultsServerSearchDemo()
    {
        InitializeComponent();
        Search.PropertyChanged += (_, e) =>
        {
            if (e.Property == SearchResults.QueryProperty)
            {
                _ = SearchAsync(Search.Query);
            }
        };
        Search.Query = "export";
    }

    private async Task SearchAsync(string query)
    {
        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        try
        {
            // Wait until typing pauses, then show skeleton rows while the "server" answers.
            await Task.Delay(200, pending.Token);
            Search.IsLoading = true;
            await Task.Delay(700, pending.Token);
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // The server decides what matches and in which order (here: any word, newest first).
            Search.ItemsSource = s_index
                .Where(r => words.Length == 0 || words.Any(w => r.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                                                                r.Snippet.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .Select(r => new SearchResultItem
                {
                    Id = r.Id,
                    Title = r.Title,
                    Kind = r.Kind,
                    Location = r.Location,
                    Snippet = r.Snippet,
                    Meta = r.Meta,
                    Icon = r.Kind switch
                    {
                        "Tickets" => ArcIcons.Inbox,
                        "Articles" => ArcIcons.FileText,
                        _ => ArcIcons.Zap
                    }
                })
                .ToList();
            Search.IsLoading = false;
        }
        catch (TaskCanceledException)
        {
            // A newer query took over.
        }
    }
}
