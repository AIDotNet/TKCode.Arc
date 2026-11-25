// Ported from vscode (src/vs/editor/contrib/suggest/test/browser/suggestMemory.test.ts, commit b57d5c9e), MIT license.
// Differences: completions are records, so "item.completion.preselect = true" replaces the item's completion.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.Suggest.CompletionModelTests;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class SuggestMemoryTests : IDisposable
{
    private TextPosition _pos = new(1, 1);
    private readonly TextModel _buffer = TestTextModel.Create("This is some text.\nthis.\nfoo: ,");
    private List<SuggestCompletionItem> _items = [CreateSuggestItem("foo", 0), CreateSuggestItem("bar", 0)];

    public void Dispose() => _buffer.Dispose();

    private sealed class TestMemory() : Memory(SuggestSelection.First)
    {
        public override void Memorize(TextModel model, TextPosition pos, SuggestCompletionItem item) => throw new NotImplementedException();
    }

    private static void Preselect(SuggestCompletionItem item, bool value) => item.Completion = item.Completion with { Preselect = value };

    [Fact]
    public void AbstractMemory_select()
    {
        var mem = new TestMemory();
        var item1 = CreateSuggestItem("fazz", 0);
        var item2 = CreateSuggestItem("bazz", 0);
        var item3 = CreateSuggestItem("bazz", 0);
        var item4 = CreateSuggestItem("bazz", 0);
        Preselect(item1, false);
        Preselect(item2, true);
        Preselect(item3, true);

        mem.Select(_buffer, _pos, [item1, item2, item3, item4]).ShouldBe(1);
    }

    [Fact]
    public void No_Prefix_LRU_Memory_honor_selection_boost()
    {
        var item1 = CreateSuggestItem("fazz", 0);
        var item2 = CreateSuggestItem("bazz", 0);
        var item3 = CreateSuggestItem("bazz", 0);
        var item4 = CreateSuggestItem("bazz", 0);
        Preselect(item1, false);
        Preselect(item2, true);
        Preselect(item3, true);
        var items = new[] { item1, item2, item3, item4 };

        new NoMemory().Select(_buffer, _pos, items).ShouldBe(1);
        new LRUMemory().Select(_buffer, _pos, items).ShouldBe(1);
        new PrefixMemory().Select(_buffer, _pos, items).ShouldBe(1);
    }

    [Fact]
    public void NoMemory()
    {
        var mem = new NoMemory();
        mem.Select(_buffer, _pos, _items).ShouldBe(0);
        mem.Select(_buffer, _pos, []).ShouldBe(0);

        mem.Memorize(_buffer, _pos, _items[0]);
        mem.Memorize(_buffer, _pos, null!);
    }

    [Fact]
    public void LRUMemory()
    {
        _pos = new TextPosition(2, 6);

        var mem = new LRUMemory();
        mem.Memorize(_buffer, _pos, _items[1]);

        mem.Select(_buffer, _pos, _items).ShouldBe(1);
        mem.Select(_buffer, new TextPosition(1, 3), _items).ShouldBe(0);

        mem.Memorize(_buffer, _pos, _items[0]);
        mem.Select(_buffer, _pos, _items).ShouldBe(0);

        mem.Select(_buffer, _pos, [CreateSuggestItem("new", 0), CreateSuggestItem("bar", 0)]).ShouldBe(1);
        mem.Select(_buffer, _pos, [CreateSuggestItem("new1", 0), CreateSuggestItem("new2", 0)]).ShouldBe(0);
    }

    [Fact]
    public void Editor_suggestSelection_recentlyUsed_should_be_a_little_more_sticky_78571()
    {
        var item1 = CreateSuggestItem("gamma", 0);
        var item2 = CreateSuggestItem("game", 0);
        _items = [item1, item2];

        var mem = new LRUMemory();
        _buffer.SetValue("    foo.");
        mem.Memorize(_buffer, new TextPosition(1, 1), item2);

        mem.Select(_buffer, new TextPosition(1, 2), _items).ShouldBe(0); // leading whitespace -> ignore recent items

        mem.Memorize(_buffer, new TextPosition(1, 9), item2);
        mem.Select(_buffer, new TextPosition(1, 9), _items).ShouldBe(1); // foo.

        _buffer.SetValue("    foo.g");
        mem.Select(_buffer, new TextPosition(1, 10), _items).ShouldBe(1); // foo.g, 'gamma' and 'game' have the same score

        item1.Score = new FuzzyScore(10, 0, [0]);
        mem.Select(_buffer, new TextPosition(1, 10), _items).ShouldBe(0); // foo.g, 'gamma' has higher score
    }

    [Fact]
    public void Intellisense_is_not_showing_top_options_first_43429()
    {
        // ensure we don't memorize for whitespace prefixes
        _pos = new TextPosition(2, 6);
        var mem = new LRUMemory();

        mem.Memorize(_buffer, _pos, _items[1]);
        mem.Select(_buffer, _pos, _items).ShouldBe(1);

        mem.Select(_buffer, new TextPosition(3, 5), _items).ShouldBe(0); // foo: |,
        mem.Select(_buffer, new TextPosition(3, 6), _items).ShouldBe(1); // foo: ,|
    }

    [Fact]
    public void PrefixMemory()
    {
        var mem = new PrefixMemory();
        _buffer.SetValue("constructor");
        var item0 = CreateSuggestItem("console", 0);
        var item1 = CreateSuggestItem("const", 0);
        var item2 = CreateSuggestItem("constructor", 0);
        var item3 = CreateSuggestItem("constant", 0);
        var items = new[] { item0, item1, item2, item3 };

        mem.Memorize(_buffer, new TextPosition(1, 2), item1); // c -> const
        mem.Memorize(_buffer, new TextPosition(1, 3), item0); // co -> console
        mem.Memorize(_buffer, new TextPosition(1, 4), item2); // con -> constructor

        mem.Select(_buffer, new TextPosition(1, 1), items).ShouldBe(0);
        mem.Select(_buffer, new TextPosition(1, 2), items).ShouldBe(1);
        mem.Select(_buffer, new TextPosition(1, 3), items).ShouldBe(0);
        mem.Select(_buffer, new TextPosition(1, 4), items).ShouldBe(2);
        mem.Select(_buffer, new TextPosition(1, 7), items).ShouldBe(2); // find substr
    }

    [Fact]
    public void Service_switches_strategy_with_the_mode()
    {
        var service = new SuggestMemoryService();
        _pos = new TextPosition(2, 6);
        service.Memorize(_buffer, _pos, _items[1], SuggestSelection.RecentlyUsed);
        service.Select(_buffer, _pos, _items, SuggestSelection.RecentlyUsed).ShouldBe(1);
        service.Select(_buffer, _pos, _items, SuggestSelection.First).ShouldBe(0);
    }

    [Fact]
    public void Lru_keeps_the_newest_entries_when_full()
    {
        var lru = new MemoryLru(3, 0.66);
        for (var i = 0; i < 4; i++)
        {
            lru.Set($"k{i}", new MemItem { Touch = i });
        }
        lru.Count.ShouldBe(2);
        lru.Peek("k3").ShouldNotBeNull();
        lru.Peek("k0").ShouldBeNull();
    }
}
