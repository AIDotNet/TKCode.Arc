// Ported from vscode (src/vs/editor/contrib/suggest/test/browser/suggest.test.ts, commit b57d5c9e), MIT license.
// Differences: providers are classes; the 'only from' provider returns an item with a range (the record requires one).

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class SuggestTests : IDisposable
{
    private readonly TextModel _model;
    private readonly IDisposable _registration;
    private readonly LanguageFeatureRegistry<ICompletionItemProvider> _registry = new();

    private static readonly LanguageFilter s_selector = new() { Pattern = "bar/path", Scheme = "foo" };

    private sealed class Provider(Func<TextPosition, CompletionList?> provide) : ICompletionItemProvider
    {
        public string DebugDisplayName => "test";

        public IReadOnlyList<string>? TriggerCharacters => [];

        public Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token) =>
            Task.FromResult(provide(position));
    }

    private static CompletionItem Item(string label, CompletionItemKind kind, TextPosition pos) =>
        new() { Label = label, Kind = kind, InsertText = label, Range = new TextRange(pos) };

    public SuggestTests()
    {
        _model = TestTextModel.Create("FOO\nbar\\BAR\nfoo", uri: new Uri("foo:bar/path"));
        _registration = _registry.Register(s_selector, new Provider(pos => new CompletionList(
        [
            Item("aaa", CompletionItemKind.Snippet, pos),
            Item("zzz", CompletionItemKind.Snippet, pos),
            Item("fff", CompletionItemKind.Property, pos),
        ])));
    }

    public void Dispose()
    {
        _registration.Dispose();
        _model.Dispose();
    }

    [Fact]
    public async Task Sort_snippet_inline()
    {
        var (items, _, _, disposable) = await SuggestProvider.ProvideSuggestionItems(_registry, _model, new TextPosition(1, 1), new CompletionOptions { SnippetSortOrder = SnippetSortOrder.Inline });
        items.Select(i => i.Completion.Label.Label).ShouldBe(["aaa", "fff", "zzz"]);
        disposable.Dispose();
    }

    [Fact]
    public async Task Sort_snippet_top()
    {
        var (items, _, _, disposable) = await SuggestProvider.ProvideSuggestionItems(_registry, _model, new TextPosition(1, 1), new CompletionOptions { SnippetSortOrder = SnippetSortOrder.Top });
        items.Select(i => i.Completion.Label.Label).ShouldBe(["aaa", "zzz", "fff"]);
        disposable.Dispose();
    }

    [Fact]
    public async Task Sort_snippet_bottom()
    {
        var (items, _, _, disposable) = await SuggestProvider.ProvideSuggestionItems(_registry, _model, new TextPosition(1, 1), new CompletionOptions { SnippetSortOrder = SnippetSortOrder.Bottom });
        items.Select(i => i.Completion.Label.Label).ShouldBe(["fff", "aaa", "zzz"]);
        disposable.Dispose();
    }

    [Fact]
    public async Task Sort_snippet_none()
    {
        var (items, _, _, disposable) = await SuggestProvider.ProvideSuggestionItems(_registry, _model, new TextPosition(1, 1),
            new CompletionOptions { KindFilter = new HashSet<CompletionItemKind> { CompletionItemKind.Snippet } });
        items.Select(i => i.Completion.Label.Label).ShouldBe(["fff"]);
        disposable.Dispose();
    }

    [Fact]
    public async Task Only_from()
    {
        var foo = new Provider(pos => new CompletionList([Item("jjj", CompletionItemKind.Property, pos)]));
        var registration = _registry.Register(s_selector, foo);

        var (items, _, _, disposable) = await SuggestProvider.ProvideSuggestionItems(_registry, _model, new TextPosition(1, 1),
            new CompletionOptions { ProviderFilter = new HashSet<ICompletionItemProvider> { foo } });
        registration.Dispose();

        items.Count.ShouldBe(1);
        items[0].Provider.ShouldBeSameAs(foo);
        disposable.Dispose();
    }

    [Fact]
    public async Task Ctrl_space_completions_stopped_working_with_the_latest_Insiders_97650()
    {
        var foo = new Provider(_ => new CompletionList(
        [
            new CompletionItem { Label = "one", Kind = CompletionItemKind.Class, InsertText = "one", Range = new CompletionItemRanges(new TextRange(0, 0, 0, 0), new TextRange(0, 0, 0, 10)) },
            new CompletionItem { Label = "two", Kind = CompletionItemKind.Class, InsertText = "two", Range = new CompletionItemRanges(new TextRange(0, 0, 0, 0), new TextRange(0, 1, 0, 10)) },
        ]));

        var registration = _registry.Register(s_selector, foo);
        var (items, _, _, disposable) = await SuggestProvider.ProvideSuggestionItems(_registry, _model, new TextPosition(0, 0),
            new CompletionOptions { ProviderFilter = new HashSet<ICompletionItemProvider> { foo } });
        registration.Dispose();

        items.Count.ShouldBe(2);
        items[0].Completion.Label.Label.ShouldBe("one");
        items[0].IsInvalid.ShouldBeFalse();
        items[1].Completion.Label.Label.ShouldBe("two");
        items[1].IsInvalid.ShouldBeTrue();
        disposable.Dispose();
    }
}
