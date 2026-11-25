// Ported from vscode (src/vs/editor/contrib/suggest/test/browser/completionModel.test.ts, commit b57d5c9e), MIT license.
// Differences: the default options are SuggestOptions.Default (upstream's EditorOptions.suggest.defaultValue and the
// test's defaultOptions differ only in options this port does not read); "score < score" compares FuzzyScore.Score.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class CompletionModelTests
{
    internal sealed class NullProvider : ICompletionItemProvider
    {
        public string DebugDisplayName => "test";

        public Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token) =>
            Task.FromResult<CompletionList?>(null);
    }

    internal static SuggestCompletionItem CreateSuggestItem(
        CompletionItemLabel label,
        int overwriteBefore,
        CompletionItemKind kind = CompletionItemKind.Property,
        bool incomplete = false,
        TextPosition? position = null,
        string? sortText = null,
        string? filterText = null)
    {
        var pos = position ?? new TextPosition(1, 1);
        var suggestion = new CompletionItem
        {
            Label = label,
            SortText = sortText,
            FilterText = filterText,
            Range = new TextRange(pos.LineNumber, pos.Column - overwriteBefore, pos.LineNumber, pos.Column),
            InsertText = label.Label,
            Kind = kind,
        };
        var container = new CompletionList([suggestion]) { Incomplete = incomplete };
        return new SuggestCompletionItem(pos, suggestion, container, new NullProvider());
    }

    private static CompletionModel Create(IReadOnlyList<SuggestCompletionItem> items, int column, string leading, int delta = 0, SnippetSuggestions snippets = SnippetSuggestions.Inline) =>
        new(items, column, new CompletionLineContext(leading, delta), WordDistance.None, SuggestOptions.Default, snippets);

    private static CompletionModel DefaultModel() => Create(
    [
        CreateSuggestItem("foo", 3),
        CreateSuggestItem("Foo", 3),
        CreateSuggestItem("foo", 2),
    ], 1, "foo");

    [Fact]
    public void Filtering_cached()
    {
        var model = DefaultModel();
        var itemsNow = model.Items;
        var itemsThen = model.Items;
        itemsThen.ShouldBeSameAs(itemsNow);

        // still the same context
        model.LineContext = new CompletionLineContext("foo", 0);
        itemsThen = model.Items;
        itemsThen.ShouldBeSameAs(itemsNow);

        // different context, refilter
        model.LineContext = new CompletionLineContext("foo1", 1);
        itemsThen = model.Items;
        itemsThen.ShouldNotBeSameAs(itemsNow);
    }

    [Fact]
    public void Complete_incomplete()
    {
        DefaultModel().GetIncompleteProvider().Count.ShouldBe(0);

        var incompleteModel = Create([CreateSuggestItem("foo", 3, incomplete: true), CreateSuggestItem("foo", 2)], 1, "foo");
        incompleteModel.GetIncompleteProvider().Count.ShouldBe(1);
    }

    [Fact]
    public void Fuzzy_matching_of_snippets_stopped_working_with_inline_snippet_suggestions_49895()
    {
        var p = new TextPosition(1, 2);
        var model = Create(
        [
            CreateSuggestItem("foobar1", 1, position: p),
            CreateSuggestItem("foobar2", 1, position: p),
            CreateSuggestItem("foobar3", 1, position: p),
            CreateSuggestItem("foobar4", 1, position: p),
            CreateSuggestItem("foobar5", 1, position: p),
            CreateSuggestItem("foofoo1", 1, incomplete: true, position: p),
        ], 2, "f");
        model.GetIncompleteProvider().Count.ShouldBe(1);
        model.Items.Count.ShouldBe(6);
    }

    [Fact]
    public void Proper_current_word_when_length_0_16380()
    {
        var model = Create(
        [
            CreateSuggestItem("    </div", 4),
            CreateSuggestItem("a", 0),
            CreateSuggestItem("p", 0),
            CreateSuggestItem("    </tag", 4),
            CreateSuggestItem("    XYZ", 4),
        ], 1, "   <");

        model.Items.Count.ShouldBe(4);
        model.Items.Select(i => i.Completion.Label.Label).ShouldBe(["    </div", "    </tag", "a", "p"]);
    }

    [Fact]
    public void Keep_snippet_sorting_with_prefix_top_25495()
    {
        var model = Create(
        [
            CreateSuggestItem("Snippet1", 1, CompletionItemKind.Snippet),
            CreateSuggestItem("tnippet2", 1, CompletionItemKind.Snippet),
            CreateSuggestItem("semver", 1, CompletionItemKind.Property),
        ], 1, "s", snippets: SnippetSuggestions.Top);

        model.Items.Count.ShouldBe(2);
        var a = model.Items[0];
        var b = model.Items[1];
        a.Completion.Label.Label.ShouldBe("Snippet1");
        b.Completion.Label.Label.ShouldBe("semver");
        a.Score.Score.ShouldBeLessThan(b.Score.Score); // snippet really promoted
    }

    [Fact]
    public void Keep_snippet_sorting_with_prefix_bottom_25495()
    {
        var model = Create(
        [
            CreateSuggestItem("snippet1", 1, CompletionItemKind.Snippet),
            CreateSuggestItem("tnippet2", 1, CompletionItemKind.Snippet),
            CreateSuggestItem("Semver", 1, CompletionItemKind.Property),
        ], 1, "s", snippets: SnippetSuggestions.Bottom);

        model.Items.Count.ShouldBe(2);
        var a = model.Items[0];
        var b = model.Items[1];
        a.Completion.Label.Label.ShouldBe("Semver");
        b.Completion.Label.Label.ShouldBe("snippet1");
        a.Score.Score.ShouldBeLessThan(b.Score.Score); // snippet really demoted
    }

    [Fact]
    public void Keep_snippet_sorting_with_prefix_inline_25495()
    {
        var model = Create(
        [
            CreateSuggestItem("snippet1", 1, CompletionItemKind.Snippet),
            CreateSuggestItem("tnippet2", 1, CompletionItemKind.Snippet),
            CreateSuggestItem("Semver", 1),
        ], 1, "s", snippets: SnippetSuggestions.Inline);

        model.Items.Count.ShouldBe(2);
        var a = model.Items[0];
        var b = model.Items[1];
        a.Completion.Label.Label.ShouldBe("snippet1");
        b.Completion.Label.Label.ShouldBe("Semver");
        a.Score.Score.ShouldBeGreaterThan(b.Score.Score);
    }

    [Fact]
    public void FilterText_seems_ignored_in_autocompletion_26874()
    {
        var item1 = CreateSuggestItem("Map - java.util", 1, filterText: "Map");
        var item2 = CreateSuggestItem("Map - java.util", 1);

        var model = Create([item1, item2], 1, "M");
        model.Items.Count.ShouldBe(2);

        model.LineContext = new CompletionLineContext("Map ", 3);
        model.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Vscode_1_12_no_longer_obeys_sortText_in_completion_items_26096()
    {
        var item1 = CreateSuggestItem("<- groups", 2, CompletionItemKind.Property, false, new TextPosition(1, 3), "00002", "  groups");
        var item2 = CreateSuggestItem("source", 0, CompletionItemKind.Property, false, new TextPosition(1, 3), "00001", "source");
        var items = new List<SuggestCompletionItem> { item1, item2 };
        items.Sort(SuggestProvider.GetSuggestionComparator(SnippetSortOrder.Inline));

        var model = Create(items, 3, "  ");
        model.Items.Count.ShouldBe(2);
        model.Items[0].Completion.Label.Label.ShouldBe("source");
        model.Items[1].Completion.Label.Label.ShouldBe("<- groups");
    }

    [Fact]
    public void Completion_item_sorting_broken_when_using_label_details_153026()
    {
        var itemZZZ = CreateSuggestItem(new CompletionItemLabel("ZZZ"), 0, CompletionItemKind.Operator);
        var itemAAA = CreateSuggestItem(new CompletionItemLabel("AAA"), 0, CompletionItemKind.Operator);
        var itemIII = CreateSuggestItem("III", 0, CompletionItemKind.Operator);

        var actual = new List<SuggestCompletionItem> { itemZZZ, itemAAA, itemIII };
        actual.Sort(SuggestProvider.GetSuggestionComparator(SnippetSortOrder.Inline));
        actual.ShouldBe([itemAAA, itemIII, itemZZZ]);
    }

    [Fact]
    public void Score_only_filtered_items_when_typing_more_score_all_when_typing_less()
    {
        var model = Create(
        [
            CreateSuggestItem("console", 0),
            CreateSuggestItem("co_new", 0),
            CreateSuggestItem("bar", 0),
            CreateSuggestItem("car", 0),
            CreateSuggestItem("foo", 0),
        ], 1, "");
        model.Items.Count.ShouldBe(5);

        // narrow down once
        model.LineContext = new CompletionLineContext("c", 1);
        model.Items.Count.ShouldBe(3);

        // query gets longer, narrow down the narrow-down'ed-set from before
        model.LineContext = new CompletionLineContext("cn", 2);
        model.Items.Count.ShouldBe(2);

        // query gets shorter, refilter everything
        model.LineContext = new CompletionLineContext("", 0);
        model.Items.Count.ShouldBe(5);
    }

    [Fact]
    public void Have_more_relaxed_suggest_matching_algorithm_15419()
    {
        var model = Create(
        [
            CreateSuggestItem("result", 0),
            CreateSuggestItem("replyToUser", 0),
            CreateSuggestItem("randomLolut", 0),
            CreateSuggestItem("car", 0),
            CreateSuggestItem("foo", 0),
        ], 1, "");

        model.LineContext = new CompletionLineContext("rlut", 4);
        model.Items.Count.ShouldBe(3);
        model.Items.Select(i => i.Completion.Label.Label).ShouldBe(["result", "replyToUser", "randomLolut"]);
    }

    [Fact]
    public void Emmet_suggestion_not_appearing_at_the_top_of_the_list_in_jsx_files_39518()
    {
        var model = Create(
        [
            CreateSuggestItem("from", 0),
            CreateSuggestItem("form", 0),
            CreateSuggestItem("form:get", 0),
            CreateSuggestItem("testForeignMeasure", 0),
            CreateSuggestItem("fooRoom", 0),
        ], 1, "");

        model.LineContext = new CompletionLineContext("form", 4);
        model.Items.Count.ShouldBe(5);
        model.Items.Take(3).Select(i => i.Completion.Label.Label).ShouldBe(["form", "form:get", "from"]);
    }
}
