using System.Diagnostics;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsServerSearchDemo : UserControl
{
    // What the "server" knows. A real app would call its search API instead.
    private sealed record Record(string Id, string Title, string Kind, string Location, string Snippet, DateTimeOffset Date);

    private static DateTimeOffset Day(int month, int day) => new(2026, month, day, 9, 0, 0, TimeSpan.Zero);

    private static readonly Record[] s_index =
    [
        new("t-2091", "Export to CSV drops the last row", "Tickets", "#2091 · Billing", "When an export has more than 1,000 rows the last one is missing from the file.", Day(9, 18)),
        new("t-2088", "Can't export invoices from the mobile app", "Tickets", "#2088 · Mobile", "The export button stays disabled on iOS when the invoice has a discount.", Day(9, 17)),
        new("t-2075", "Scheduled export runs twice", "Tickets", "#2075 · Platform", "Daily exports started twice after the move to the new scheduler.", Day(9, 12)),
        new("t-2063", "Password reset email never arrives", "Tickets", "#2063 · Accounts", "Customers on custom domains do not get the reset email; the bounce log shows a DNS error.", Day(9, 8)),
        new("a-export", "Export your data", "Articles", "Help center / Account", "Download every invoice, customer and payment as CSV or JSON. Large exports arrive by email.", Day(9, 1)),
        new("a-schedule", "Schedule a recurring export", "Articles", "Help center / Automation", "Pick a time and a format and the export is sent to your storage bucket every day.", Day(8, 20)),
        new("a-reset", "Reset your password", "Articles", "Help center / Account", "Open the sign-in page, choose “Forgot password” and follow the link in the email.", Day(6, 4)),
        new("m-export", "Export pipeline runbook", "Macros", "Support macros", "Steps to retry a failed export and the message to send to the customer.", Day(7, 30))
    ];

    private CancellationTokenSource? _pending;

    public SearchResultsServerSearchDemo()
    {
        InitializeComponent();
        // Any change of the query, a filter or the sort order is a new request.
        Search.SearchChanged += (_, _) => _ = SearchAsync();
        Search.Query = "export";
    }

    private async Task SearchAsync()
    {
        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        try
        {
            // Wait until typing pauses, then show skeleton rows while the "server" answers.
            await Task.Delay(200, pending.Token);
            Search.IsLoading = true;
            var clock = Stopwatch.StartNew();
            await Task.Delay(600, pending.Token);

            var words = Search.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = s_index
                .Where(r => words.All(w => r.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                                           r.Snippet.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // The server also counts every type for the query, whatever is ticked (the counts of the filter's own values).
            var type = (SearchValueFilter)Search.Filters[0];
            foreach (var value in type.Values)
            {
                value.Count = matches.Count(r => r.Kind == value.Value);
            }

            var kinds = type.SelectedValues.Select(v => v.Value).ToList();
            var results = matches.Where(r => kinds.Count == 0 || kinds.Contains(r.Kind));
            // The server decides the order: the control shows the items as given.
            results = Search.SortOrder switch
            {
                SearchSortOrder.Newest => results.OrderByDescending(r => r.Date),
                SearchSortOrder.Title => results.OrderBy(r => r.Title),
                _ => results
            };
            Search.ItemsSource = results
                .Select(r => new SearchResultItem
                {
                    Id = r.Id,
                    Title = r.Title,
                    Kind = r.Kind,
                    KindLabel = r.Kind.TrimEnd('s'),
                    Location = r.Location,
                    Snippet = r.Snippet,
                    Date = r.Date,
                    Icon = r.Kind switch
                    {
                        "Tickets" => ArcIcons.Inbox,
                        "Articles" => ArcIcons.FileText,
                        _ => ArcIcons.Zap
                    }
                })
                .ToList();
            Search.Elapsed = clock.Elapsed;
            Search.IsLoading = false;
        }
        catch (TaskCanceledException)
        {
            // A newer request took over.
        }
    }
}
