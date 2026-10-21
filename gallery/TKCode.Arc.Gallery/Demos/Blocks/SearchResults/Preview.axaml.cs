using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsPreviewDemo : UserControl
{
    public SearchResultsPreviewDemo()
    {
        InitializeComponent();
        Search.Recent.AddRange(["launch", "rollback", "pricing copy", "Imani"]);
        Search.Suggestions = ["release notes", "budget model", "press kit", "support rota"];
        Search.ItemsSource = Workspace();
        // The control only shows how long a search took; the app measures it. Here the time is made up.
        Search.SearchChanged += (_, _) => Search.Elapsed = TimeSpan.FromSeconds(0.06 + Math.Abs(Search.Query.GetHashCode() % 17) / 100.0);
        Search.Elapsed = TimeSpan.FromSeconds(0.12);
        Search.DetailActionInvoked += OnDetailAction;
    }

    private void OnDetailAction(object? sender, SearchResultActionEventArgs e)
    {
        // "Open" would navigate in a real app; the demo just closes the detail.
        if (e.Action == SearchResultAction.Open)
        {
            Search.CloseDetail();
        }
    }

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 9, 0, 0, TimeSpan.Zero);

    private static List<SearchResultItem> Workspace() =>
    [
        Doc("launch-checklist", "Launch checklist for new releases", "Engineering / Handbook", "Engineering", Day(2026, 8, 30), "Nia Okonkwo", "4 min read",
            "Walk the checklist top to bottom before every launch: freeze the branch, rehearse the rollback and tell support what is changing.",
            "Two days before a launch the release owner freezes the branch, runs the full suite on staging and rehearses the rollback with the on-call engineer.\n\nOne day before, support and sales get the change list. Anything customers can notice needs a help article and a macro.\n\nOn launch day ship in waves of 5, 25 and 100 percent, and wait ten minutes between waves. The owner writes the summary the same afternoon."),
        Doc("rollback", "Rollback playbook", "Engineering / Runbooks", "Engineering", Day(2026, 6, 12), "Soren Lindgren", "3 min read",
            "Flip the flag first, then redeploy the last good build. Never roll back a launch and a migration at the same time.",
            "Switch the feature flag off. This is almost always enough and takes seconds.\n\nIf the flag is not enough, redeploy the last good build from the pipeline page and watch the error rate for ten minutes.\n\nNever roll back a launch and a database migration in the same step. Migrations are reverted by their own script."),
        Doc("pricing-copy", "Pricing page copy review", "Growth / Website", "Growth", Day(2026, 3, 4), "Kenji Watanabe", "6 min read",
            "Annual plans lead with the saving, monthly plans with flexibility. Keep the launch discount out of the headline.",
            "The pricing page leads with the plan names and the price per seat. Annual billing shows the saving next to the price, never in the headline.\n\nKeep launch offers in a banner above the table so the table stays correct when the offer ends."),
        Doc("brand-voice", "Brand voice and writing style", "Design / Guidelines", "Design", Day(2025, 11, 18), "Lucía Ferreyra", "5 min read",
            "Write short sentences in sentence case. Put verbs on buttons and never blame the reader in an error message.",
            "We sound like a calm colleague: plain words, short sentences, sentence case everywhere.\n\nButtons start with a verb. Errors say what happened and what to do next, and never blame the reader."),
        Doc("planning", "Quarterly planning notes", "Operations / Planning", "Operations", Day(2026, 1, 22), "Imani Okoro", "8 min read",
            "Themes for the quarter: a calmer inbox, faster exports and the first regional launch outside Europe.",
            "Three themes shape the quarter: a calmer inbox, faster exports and the first launch outside Europe.\n\nEach theme has one owner and one number that moves. We review the numbers every second Thursday."),
        Doc("support-macros", "Support macros for launch week", "Support / Playbooks", "Support", Day(2026, 7, 9), "Amara Diallo", "4 min read",
            "Ready-made replies for the questions every launch brings: billing dates, missing features and where to find the changelog.",
            "Launch week doubles the ticket volume. These macros answer the five questions we hear most.\n\nEdit them freely, but keep the first sentence: customers should know right away that a person read their message."),
        Doc("checkout-incident", "Incident review: checkout slowdown", "Engineering / Incidents", "Engineering", Day(2025, 12, 3), "Soren Lindgren", "7 min read",
            "A cache warm-up ran during the launch window and doubled the checkout latency for 38 minutes.",
            "At 14:05 the cache warm-up job started during the launch window and doubled checkout latency.\n\nThe alert fired after 11 minutes. We now block warm-ups while a launch is in progress."),

        File("launch-plan", "launch-plan-q4.xlsx", "Files / Operations", "Operations", Day(2026, 9, 2), "Imani Okoro", "48 KB", ArcIcons.File,
            "Dates, owners and dependencies for the October launch, one tab per workstream."),
        File("press-kit", "press-kit.zip", "Files / Growth", "Growth", Day(2026, 5, 14), "Kenji Watanabe", "36 MB", ArcIcons.FileArchive,
            "Logos, screenshots and the founder photo for the launch announcement."),
        File("budget", "campaign-budget.xlsx", "Files / Finance", "Finance", Day(2026, 2, 19), "Tobias Wren", "92 KB", ArcIcons.File,
            "Spend per channel for the launch campaign, with a tab for each region."),
        File("brief", "launch-campaign-brief.pdf", "Files / Design", "Design", Day(2025, 10, 27), "Lucía Ferreyra", "2.1 MB", ArcIcons.FileText,
            "Message house, audiences and the visual direction for the launch pages."),

        Person("imani", "Imani Okoro", "Support operations lead, Nairobi", "Support", "imani@tidewater.dev",
            "Runs the launch war room for support and keeps the escalation rota up to date. Ask about macros and staffing."),
        Person("soren", "Soren Lindgren", "Staff engineer, Gothenburg", "Engineering", "soren@tidewater.dev",
            "Owns the launch pipeline: feature flags, canary waves and the rollback switch. Happy to review any launch plan."),
        Person("kenji", "Kenji Watanabe", "Growth marketer, Osaka", "Growth", "kenji@tidewater.dev",
            "Plans launch campaigns and the waitlist emails for every new region, and tracks sign-ups per channel."),
        Person("lucia", "Lucía Ferreyra", "Brand designer, Buenos Aires", "Design", "lucia@tidewater.dev",
            "Designs launch pages, social cards and the product tour. Keeps the brand kit current."),
        Person("tobias", "Tobias Wren", "Finance partner, Dublin", "Finance", "tobias@tidewater.dev",
            "Signs off launch budgets and tracks spend against plan every Friday.")
    ];

    private static SearchResultItem Doc(string id, string title, string location, string team, DateTimeOffset date, string author, string length,
                                        string snippet, string body)
        => WithDetails(new SearchResultItem
        {
            Id = id,
            Title = title,
            Kind = "Docs",
            KindLabel = "Doc",
            Location = location,
            Snippet = snippet,
            Body = body,
            Date = date,
            Icon = ArcIcons.FileText,
            Url = $"https://tidewater.example/docs/{id}"
        }, team, author, length, date);

    private static SearchResultItem File(string id, string title, string location, string team, DateTimeOffset date, string author, string size,
                                         Geometry icon, string snippet)
        => WithDetails(new SearchResultItem
        {
            Id = id,
            Title = title,
            Kind = "Files",
            KindLabel = "File",
            Location = location,
            Snippet = snippet,
            Body = snippet,
            Date = date,
            Icon = icon,
            Url = $"https://tidewater.example/files/{id}"
        }, team, author, size, date, "Size");

    private static SearchResultItem WithDetails(SearchResultItem item, string team, string author, string length, DateTimeOffset date, string lengthLabel = "Length")
    {
        item.Attributes["team"] = team;
        item.Details.Add(new SearchResultDetail { Label = "Team", Value = team });
        item.Details.Add(new SearchResultDetail { Label = "Author", Value = author, AvatarName = author });
        item.Details.Add(new SearchResultDetail { Label = lengthLabel, Value = length });
        item.Details.Add(new SearchResultDetail { Label = "Last edited", Value = date.ToString("MMM d, yyyy"), Note = Ago(date) });
        return item;
    }

    private static string Ago(DateTimeOffset date)
    {
        var days = (int)(new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.Zero) - date).TotalDays;
        return days switch
        {
            < 14 => $"{days} days ago",
            < 60 => $"{days / 7} weeks ago",
            _ => $"{days / 30} months ago"
        };
    }

    // A person: an avatar (initials without a photo), a role line, a passage and the email as the caption.
    private static SearchResultItem Person(string id, string name, string role, string team, string email, string snippet)
    {
        var item = new SearchResultItem
        {
            Id = id,
            Title = name,
            Kind = "People",
            KindLabel = "Person",
            Subtitle = role,
            Snippet = snippet,
            Caption = email,
            AvatarName = name
        };
        item.Attributes["team"] = team;
        item.Body = snippet;
        item.Details.Add(new SearchResultDetail { Label = "Team", Value = team });
        item.Details.Add(new SearchResultDetail { Label = "Email", Value = email });
        return item;
    }
}
