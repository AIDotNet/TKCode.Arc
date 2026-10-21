namespace TKCode.Arc.Tests.Blocks;

/// <summary>The search engine behind <see cref="SearchResults"/>: terms, matching, ranking, highlights and snippet windows.</summary>
public class SearchResultsMatcherTests
{
    private static List<string> Terms(string query)
    {
        var terms = new List<string>();
        SearchResultsMatcher.Tokenize(query, terms);
        return terms;
    }

    private static SearchResultItem Item(string title, string? snippet = null, string? location = null, params string[] keywords)
        => new() { Id = title, Title = title, Snippet = snippet, Location = location, Keywords = keywords.Length == 0 ? null : keywords };

    [Fact]
    public void Queries_Split_On_White_Space_Into_Distinct_Terms()
    {
        Terms("  offline\tSync  offline ").ShouldBe(["offline", "Sync"]);
        Terms("Sync sync SYNC").ShouldBe(["Sync"]);
        Terms("").ShouldBeEmpty();
        Terms("   ").ShouldBeEmpty();
    }

    [Fact]
    public void Every_Term_Must_Match_Somewhere()
    {
        var terms = Terms("offline queue");
        SearchResultsMatcher.Matches("Offline sync design", "Changes wait in a local queue.", null, null, terms).ShouldBeTrue();
        SearchResultsMatcher.Matches("Offline sync design", "Changes wait locally.", null, null, terms).ShouldBeFalse();
        // Location and keywords count as well; matching ignores case.
        SearchResultsMatcher.Matches("Sync stalls", null, "FN-482 · Mobile", null, Terms("fn-482 STALLS")).ShouldBeTrue();
        SearchResultsMatcher.Matches("Sync stalls", null, null, ["socket", "sleep"], Terms("sleep sync")).ShouldBeTrue();
        SearchResultsMatcher.Matches("Anything", null, null, null, Terms("")).ShouldBeTrue();
    }

    [Fact]
    public void Tiers_Follow_The_First_Term_In_The_Title()
    {
        SearchResultsMatcher.Tier("Invoice template", "invoice").ShouldBe(SearchResultsMatcher.TierTitleStart);
        SearchResultsMatcher.Tier("Late invoices", "invoice").ShouldBe(SearchResultsMatcher.TierWordStart);
        SearchResultsMatcher.Tier("Pre-invoice checks", "invoice").ShouldBe(SearchResultsMatcher.TierWordStart);
        SearchResultsMatcher.Tier("Subinvoice tools", "invoice").ShouldBe(SearchResultsMatcher.TierTitleContains);
        // A later word start wins over an earlier match inside a word.
        SearchResultsMatcher.Tier("Reinvoice the invoice", "invoice").ShouldBe(SearchResultsMatcher.TierWordStart);
        SearchResultsMatcher.Tier("Billing", "invoice").ShouldBe(SearchResultsMatcher.TierElsewhere);
    }

    [Fact]
    public void Search_Ranks_By_Tier_And_Keeps_The_Source_Order_On_Ties()
    {
        List<SearchResultItem> items =
        [
            Item("Payroll", "Every invoice is paid"),
            Item("Late invoices"),
            Item("Invoice reminders"),
            Item("Subinvoice tools"),
            Item("Invoice template"),
            Item("Travel policy")
        ];
        var hits = new List<SearchResultsHit>();
        SearchResultsMatcher.Search(items, Terms("invoice"), filter: true, hits);
        hits.Select(h => h.Item.Title).ShouldBe(["Invoice reminders", "Invoice template", "Late invoices", "Subinvoice tools", "Payroll"]);
        hits.Select(h => h.Tier).ShouldBe([0, 0, 1, 2, 3]);

        // All terms must match (AND).
        SearchResultsMatcher.Search(items, Terms("invoice paid"), filter: true, hits);
        hits.Single().Item.Title.ShouldBe("Payroll");

        // Server-side results stay as given.
        SearchResultsMatcher.Search(items, Terms("invoice"), filter: false, hits);
        hits.Select(h => h.Item).ShouldBe(items);
    }

    [Fact]
    public void Highlights_Cover_Every_Occurrence_And_Merge_Overlaps()
    {
        var ranges = new List<SearchMatchRange>();
        SearchResultsMatcher.FindRanges("Release the released build", Terms("release"), ranges);
        ranges.ShouldBe([new SearchMatchRange(0, 7), new SearchMatchRange(12, 7)]);

        // "elease" and "rel" overlap "release": one mark.
        SearchResultsMatcher.FindRanges("Release notes", Terms("elease rel"), ranges);
        ranges.ShouldBe([new SearchMatchRange(0, 7)]);

        // Touching matches merge too.
        SearchResultsMatcher.FindRanges("offlinesync", Terms("offline sync"), ranges);
        ranges.ShouldBe([new SearchMatchRange(0, 11)]);

        SearchResultsMatcher.FindRanges("Nothing here", Terms("zebra"), ranges);
        ranges.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_Sorts_And_Joins_Ranges()
    {
        List<SearchMatchRange> ranges = [new(10, 3), new(0, 4), new(2, 4), new(12, 5), new(20, 1)];
        SearchResultsMatcher.Merge(ranges);
        ranges.ShouldBe([new SearchMatchRange(0, 6), new SearchMatchRange(10, 7), new SearchMatchRange(20, 1)]);
    }

    [Fact]
    public void Snippets_Are_Cut_Around_The_First_Match_With_Ellipses()
    {
        var text = "Our team keeps every document in one shared space so people can find decisions later. " +
                   "When a release goes out we write a short summary of what changed and why it matters for customers. " +
                   "Older summaries move to the archive after one year.";
        var ranges = new List<SearchMatchRange>();
        var window = SearchResultsMatcher.Window(text, Terms("release"), lead: 20, budget: 60, maxLength: 120, ranges);
        window.ShouldStartWith("…");
        window.ShouldEndWith("…");
        window.Length.ShouldBeLessThanOrEqualTo(122);
        var hit = window.IndexOf("release", StringComparison.Ordinal);
        hit.ShouldBeInRange(1, 24);
        // The window opens and closes on word boundaries.
        window[1].ShouldNotBe(' ');
        char.IsLetter(window[1]).ShouldBeTrue();
        text.ShouldContain(window.Trim('…'));
        ranges.ShouldBe([new SearchMatchRange(hit, 7)]);
    }

    [Fact]
    public void Snippets_Whose_Match_Is_Already_Visible_Start_At_The_Beginning()
    {
        var ranges = new List<SearchMatchRange>();
        const string text = "Pair with your buddy on a small release task.";
        SearchResultsMatcher.Window(text, Terms("release"), lead: 10, budget: 80, maxLength: 200, ranges).ShouldBe(text);
        ranges.Single().ShouldBe(new SearchMatchRange(text.IndexOf("release", StringComparison.Ordinal), 7));

        // Too long, but the match is in view: only the end is cut.
        var longText = "Release " + string.Join(' ', Enumerable.Repeat("word", 80));
        var window = SearchResultsMatcher.Window(longText, Terms("release"), lead: 10, budget: 80, maxLength: 60, ranges);
        window.ShouldStartWith("Release");
        window.ShouldEndWith("word…");

        // No match in the snippet (the title matched): the start is shown.
        SearchResultsMatcher.Window(longText, Terms("zebra"), lead: 10, budget: 80, maxLength: 60, ranges).ShouldStartWith("Release word");
        ranges.ShouldBeEmpty();
    }
}
