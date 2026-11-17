namespace TKCode.Arc.Tests.Blocks;

/// <summary>The matching behind <see cref="SearchResults"/>: terms, tiers, highlights and snippet windows.</summary>
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
    public void Every_Term_Must_Match_Somewhere_In_The_Searched_Text()
    {
        var terms = Terms("offline queue");
        SearchResultsMatcher.MatchesItem(Item("Offline sync design", "Changes wait in a local queue."), terms).ShouldBeTrue();
        SearchResultsMatcher.MatchesItem(Item("Offline sync design", "Changes wait locally."), terms).ShouldBeFalse();
        // Location, keywords, subtitle, caption and body count as well; matching ignores case.
        SearchResultsMatcher.MatchesItem(Item("Sync stalls", null, "FN-482 · Mobile"), Terms("fn-482 STALLS")).ShouldBeTrue();
        SearchResultsMatcher.MatchesItem(Item("Sync stalls", null, null, "socket", "sleep"), Terms("sleep sync")).ShouldBeTrue();
        SearchResultsMatcher.MatchesItem(new SearchResultItem { Title = "Imani", Subtitle = "Support lead, Nairobi" }, Terms("nairobi")).ShouldBeTrue();
        SearchResultsMatcher.MatchesItem(new SearchResultItem { Title = "Imani", Caption = "imani@tidewater.dev" }, Terms("tidewater")).ShouldBeTrue();
        SearchResultsMatcher.MatchesItem(new SearchResultItem { Title = "Runbook", Body = "Flip the flag first." }, Terms("flag")).ShouldBeTrue();
        SearchResultsMatcher.MatchesItem(Item("Anything"), Terms("")).ShouldBeTrue();
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

/// <summary>The search itself: query, filters, counts and sorting, without any control.</summary>
public class SearchResultsEngineTests
{
    private static SearchResultItem Item(string id, string title, string kind, string? team = null, int? day = null, string? snippet = null)
    {
        var item = new SearchResultItem { Id = id, Title = title, Kind = kind, Snippet = snippet };
        if (team is not null)
        {
            item.Attributes["team"] = team;
        }
        if (day is { } d)
        {
            item.Date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(d);
        }
        return item;
    }

    private static List<SearchResultItem> Items() =>
    [
        Item("a", "Launch checklist", "Docs", "Engineering", 10),
        Item("b", "Rollback playbook", "Docs", "Engineering", 200),
        Item("c", "Pricing page copy", "Docs", "Growth", 120),
        Item("d", "Launch plan.xlsx", "Files", "Operations", 60),
        Item("e", "press-kit.zip", "Files", "Growth", 250),
        Item("f", "Imani Okoro", "People", "Operations", snippet: "Runs the launch war room."),
        Item("g", "Soren Lindgren", "People", "Engineering", snippet: "Owns the launch pipeline."),
        Item("h", "Kenji Watanabe", "People", "Growth")
    ];

    private static (SearchResultsEngine Engine, List<SearchResultItem> Items, SearchValueFilter Type, SearchValueFilter Team, SearchDateFilter Date, List<string> Terms) Setup()
    {
        var items = Items();
        var type = new SearchValueFilter { Key = "kind", Title = "Type" };
        var team = new SearchValueFilter { Key = "team", Title = "Team" };
        var date = new SearchDateFilter { Key = "edited", Title = "Edited", AppliesTo = "Docs, Files" };
        var engine = new SearchResultsEngine();
        engine.Compile(items, [type, team, date]);
        return (engine, items, type, team, date, new List<string>());
    }

    private static void Run(SearchResultsEngine engine, List<SearchResultItem> items, List<string> terms, string query,
                            SearchSortOrder sort = SearchSortOrder.BestMatch, bool filtering = true)
    {
        engine.ReadSelections();
        engine.RunQuery(items, query, terms, filtering);
        engine.RunFilters(filtering, count: filtering, sort, terms.Count > 0);
    }

    private static int[] Counts(SearchValueFilter filter, SearchResultsEngine engine)
        => engine.Plans.Single(p => ReferenceEquals(p.Source, filter)).Counts;

    [Fact]
    public void Value_Filters_List_The_Values_They_Find_In_The_Order_They_Appear()
    {
        var (_, _, type, team, _, _) = Setup();
        type.Values.Select(v => v.Value).ShouldBe(["Docs", "Files", "People"]);
        team.Values.Select(v => v.Value).ShouldBe(["Engineering", "Growth", "Operations"]);

        // Values you list yourself keep their order and labels; unknown item values are not added.
        var fixedTeam = new SearchValueFilter { Key = "team", Values = { new SearchFilterValue("Operations", "Ops"), new SearchFilterValue("Design") } };
        new SearchResultsEngine().Compile(Items(), [fixedTeam]);
        fixedTeam.Values.Select(v => v.DisplayLabel).ShouldBe(["Ops", "Design"]);
    }

    [Fact]
    public void Counts_Follow_The_Query_And_Ignore_The_Filters_Own_Ticks()
    {
        var (engine, items, type, team, _, terms) = Setup();
        Run(engine, items, terms, string.Empty);
        Counts(type, engine).ShouldBe([3, 2, 3]);
        Counts(team, engine).ShouldBe([3, 3, 2]);

        // "launch" narrows everything: two docs/files plus two people.
        Run(engine, items, terms, "launch");
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["a", "d", "f", "g"]);
        Counts(type, engine).ShouldBe([1, 1, 2]);
        Counts(team, engine).ShouldBe([2, 0, 2]);

        // Ticking People: the Type counts still show every kind (own ticks are ignored) while Team counts only people.
        type.Values[2].IsSelected = true;
        Run(engine, items, terms, "launch");
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["f", "g"]);
        Counts(type, engine).ShouldBe([1, 1, 2]);
        Counts(team, engine).ShouldBe([1, 0, 1]);

        // Another value of the same filter ORs.
        type.Values[0].IsSelected = true;
        Run(engine, items, terms, "launch");
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["a", "f", "g"]);

        // A value of another filter ANDs.
        team.Values[0].IsSelected = true;
        Run(engine, items, terms, "launch");
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["a", "g"]);
        Counts(type, engine).ShouldBe([1, 0, 1]);
        Counts(team, engine).ShouldBe([2, 0, 1]);
    }

    [Fact]
    public void Date_Ranges_Only_Apply_To_Their_Kinds()
    {
        var (engine, items, _, _, date, terms) = Setup();
        var plan = engine.Plans.Single(p => p.IsDate);
        // The span is the oldest and newest date among the kinds the range applies to.
        (plan.MaxDay - plan.MinDay).ShouldBe(240);
        plan.HasRange.ShouldBeTrue();

        date.Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(100);
        Run(engine, items, terms, string.Empty);
        plan.DateActive.ShouldBeTrue();
        // Docs and files before day 100 are out; people have no date but pass because the range is not about them.
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["b", "c", "e", "f", "g", "h"]);

        date.Start = null;
        date.End = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(100);
        Run(engine, items, terms, string.Empty);
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["a", "d", "f", "g", "h"]);

        // A range at its extremes is not active.
        date.Clear();
        Run(engine, items, terms, string.Empty);
        plan.DateActive.ShouldBeFalse();
        engine.Hits.Count.ShouldBe(8);
    }

    [Fact]
    public void Sort_Orders_Follow_Rank_Date_And_Title()
    {
        var (engine, items, _, _, _, terms) = Setup();
        Run(engine, items, terms, "launch");
        // Best match: titles that start with the word first, then the rest in source order.
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["a", "d", "f", "g"]);

        Run(engine, items, terms, string.Empty, SearchSortOrder.Newest);
        // Newest first; undated items last in source order.
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["e", "b", "c", "d", "a", "f", "g", "h"]);

        Run(engine, items, terms, string.Empty, SearchSortOrder.Title);
        engine.Hits.Select(h => h.Item.Title).ShouldBe(
            ["Imani Okoro", "Kenji Watanabe", "Launch checklist", "Launch plan.xlsx", "press-kit.zip", "Pricing page copy", "Rollback playbook", "Soren Lindgren"],
            ignoreOrder: false);
    }

    [Fact]
    public void A_Query_That_Only_Grows_Searches_The_Previous_Matches()
    {
        var (engine, items, _, _, _, terms) = Setup();
        Run(engine, items, terms, "l");
        engine.LastCandidateCount.ShouldBe(items.Count);
        var matches = engine.QueryHits.Count;
        Run(engine, items, terms, "la");
        engine.LastCandidateCount.ShouldBe(matches);
        Run(engine, items, terms, "launch p");
        engine.LastCandidateCount.ShouldBeLessThanOrEqualTo(matches);
        engine.Hits.Select(h => h.Item.Id).ShouldBe(["d", "g"]);

        // A shorter or different query starts over; so does a changed item list.
        Run(engine, items, terms, "launch");
        engine.LastCandidateCount.ShouldBe(items.Count);
        Run(engine, items, terms, "launch p");
        engine.Compile(items, []);
        Run(engine, items, terms, "launch pi");
        engine.LastCandidateCount.ShouldBe(items.Count);
    }

    [Fact]
    public void Server_Results_Are_Kept_As_Given_And_Counts_Are_Left_Alone()
    {
        var (engine, items, type, _, _, terms) = Setup();
        type.Values[0].IsSelected = true;
        type.Values[0].Count = 99;
        Run(engine, items, terms, "launch", SearchSortOrder.Title, filtering: false);
        engine.Hits.Select(h => h.Item.Id).ShouldBe(items.Select(i => i.Id));
        Counts(type, engine).ShouldBe([0, 0, 0]);
        type.Values[0].Count.ShouldBe(99);
    }
}
