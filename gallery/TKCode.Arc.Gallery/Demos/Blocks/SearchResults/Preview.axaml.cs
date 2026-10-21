using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsPreviewDemo : UserControl
{
    public SearchResultsPreviewDemo()
    {
        InitializeComponent();
        Search.Suggestions = ["release", "sync", "onboarding", "design"];
        Search.ItemsSource =
        [
            Doc("release-checklist", "Release checklist", "Engineering / Releases", "Updated 2d ago",
                "Before every release, freeze the main branch, run the full test suite on staging and ask the on-call engineer to confirm the rollback plan."),
            Doc("offline-sync", "Offline sync design", "Engineering / Architecture", "Updated 5d ago",
                "Changes made offline wait in a local queue and are replayed in order when the device reconnects. Conflicts are resolved per field: the newest edit wins."),
            Doc("onboarding", "Onboarding guide for new engineers", "People ops / Onboarding", "Updated 3w ago",
                "Your first week: set up your laptop, get access to the repositories and pair with your buddy on a small release task."),
            Doc("voice", "Brand voice and writing style", "Design / Guidelines", "Updated 1mo ago",
                "Write short sentences in sentence case. Put verbs on buttons, avoid jargon and never blame the reader in an error message."),
            Doc("q3-planning", "Q3 planning notes", "Product / Planning", "Updated 6d ago",
                "Themes for the quarter: faster sync, a calmer notification center and the first public release of shared notebooks."),
            Doc("incident-sync", "Incident review: sync outage", "Engineering / Incidents", "Updated 2mo ago",
                "On 12 August the sync service rejected writes for 41 minutes after a certificate rotation. The next release added an alert for certificates that are about to expire."),
            Doc("a11y", "Accessibility checklist", "Design / Guidelines", "Updated 3mo ago",
                "Every screen needs a logical focus order, a visible focus ring, labels on icon buttons and a contrast of at least 4.5:1 for body text."),

            Issue("fn-482", "Sync stalls after the app wakes from sleep", "FN-482 · Mobile", "Open",
                "The release candidate keeps the old socket open after sleep, so edits stay in the queue until the app restarts."),
            Issue("fn-479", "Release notes are missing the offline fixes", "FN-479 · Docs", "In review",
                "The 4.2 release notes skip the three offline sync fixes that shipped in the last build."),
            Issue("fn-471", "Search ignores accented characters", "FN-471 · Web", "Open",
                "Typing “resume” does not find “résumé”. Fold accents before matching."),
            Issue("fn-466", "Dark mode: low contrast on tags", "FN-466 · Design", "Done",
                "Tag text uses the muted color on the muted surface in dark mode, which fails the contrast check."),
            Issue("fn-460", "Crash when sharing a notebook with 200+ pages", "FN-460 · Mobile", "Open",
                "Sharing a large notebook runs out of memory while it builds the preview. Stream the pages instead."),
            Issue("fn-455", "Add a release toggle for shared notebooks", "FN-455 · Platform", "Planned",
                "Ship shared notebooks behind a remote flag so we can release them to 5% of accounts first."),

            File("changelog", "release-4.2-changelog.md", "Files / Engineering", "12 KB", ArcIcons.FileCode2,
                "4.2: faster offline sync, a new share sheet and 23 bug fixes."),
            File("sync-pdf", "sync-architecture.pdf", "Files / Architecture", "2.4 MB", ArcIcons.FileText,
                "Diagrams of the sync queue, conflict resolution and the release pipeline."),
            File("roadmap", "Q3 roadmap.key", "Files / Product", "8.1 MB", ArcIcons.File, "18 slides for the planning review."),
            File("colors", "brand-colors.fig", "Files / Design", "640 KB", ArcIcons.FileImage, "Palette, contrast pairs and the accent scale."),
            File("onboarding-sheet", "onboarding-checklist.xlsx", "Files / People ops", "48 KB", ArcIcons.File, null),
            File("screens", "app-store-screenshots.zip", "Files / Marketing", "36 MB", ArcIcons.FileArchive,
                "Screenshots for the 4.2 release in six languages."),

            Person("maya", "Maya Lindqvist", "Release manager · Engineering", "Stockholm",
                "Owns the release calendar and the go or no-go call."),
            Person("tomas", "Tomás Ferreira", "Mobile engineer · Sync team", "Lisbon",
                "Works on offline sync and background refresh."),
            Person("priya", "Priya Raman", "Product designer · Design", "Bangalore",
                "Leads the notification center and the share sheet."),
            Person("jonah", "Jonah Whitfield", "Engineering manager · Platform", "Toronto",
                "Runs the platform team and the incident review rotation."),
            Person("aiko", "Aiko Tanaka", "Technical writer · Docs", "Osaka",
                "Writes the release notes and the help center."),
            Person("samuel", "Samuel Okafor", "Support lead · Customer success", "Lagos",
                "Collects feedback on sync and sharing from support tickets.")
        ];
    }

    private static SearchResultItem Doc(string id, string title, string location, string meta, string snippet)
        => new() { Id = id, Title = title, Kind = "Docs", Location = location, Meta = meta, Snippet = snippet, Icon = ArcIcons.FileText };

    private static SearchResultItem Issue(string id, string title, string location, string meta, string snippet)
        => new() { Id = id, Title = title, Kind = "Issues", Location = location, Meta = meta, Snippet = snippet, Icon = ArcIcons.CircleDot, Keywords = [id] };

    private static SearchResultItem File(string id, string title, string location, string meta, Geometry icon, string? snippet)
        => new() { Id = id, Title = title, Kind = "Files", Location = location, Meta = meta, Snippet = snippet, Icon = icon };

    // People show their initials instead of a glyph: Icon also takes a control.
    private static SearchResultItem Person(string id, string name, string role, string city, string snippet)
        => new()
        {
            Id = id,
            Title = name,
            Kind = "People",
            Location = role,
            Meta = city,
            Snippet = snippet,
            Icon = new Avatar { DisplayName = name, Size = AvatarSize.Small }
        };
}
