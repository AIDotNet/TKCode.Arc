// Ported from vscode (src/vs/editor/contrib/wordHighlighter/test/browser/wordHighlighter.test.ts: the suite
// 'DocumentHighlightProviders', commit b57d5c9e), MIT license.
// Differences: providers are IDocumentHighlightProvider objects; errors go to the callback instead of the unexpected
// error handler; the suite 'WordHighlighterContribution' is not ported (it tests the static decoration ids upstream
// shares between editors, which there are none of here; the control's word highlighter has its own tests).

using Shouldly;
using TKCode.Arc.Editor.Contrib.WordHighlighter;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class DocumentHighlightProvidersTests : IDisposable
{
    private static readonly TextPosition s_position = new(1, 2);
    private static readonly DocumentHighlight s_first = new(new TextRange(1, 1, 1, 5)) { Kind = DocumentHighlightKind.Read };
    private static readonly DocumentHighlight s_second = new(new TextRange(1, 6, 1, 10)) { Kind = DocumentHighlightKind.Write };

    private readonly LanguageFeatureRegistry<IDocumentHighlightProvider> _registry = new();
    private readonly TextModel _model = TestTextModel.Create("word word word\nword");
    private readonly List<IDisposable> _store = [];

    public void Dispose()
    {
        foreach (var d in _store)
        {
            d.Dispose();
        }

        _model.Dispose();
    }

    private sealed class Provider(Func<Task<IReadOnlyList<DocumentHighlight>?>> provide) : IDocumentHighlightProvider
    {
        public Task<IReadOnlyList<DocumentHighlight>?> ProvideDocumentHighlights(TextModel model, TextPosition position, CancellationToken token) => provide();
    }

    private void Register(string language, Func<Task<IReadOnlyList<DocumentHighlight>?>> provide) =>
        _store.Add(_registry.Register(language, new Provider(provide)));

    private static Task<IReadOnlyList<DocumentHighlight>?> Of(params DocumentHighlight[] highlights) => Task.FromResult<IReadOnlyList<DocumentHighlight>?>(highlights);

    private IReadOnlyList<DocumentHighlight>? Get(Dictionary<Uri, IReadOnlyList<DocumentHighlight>> result) =>
        result.TryGetValue(_model.Uri, out var highlights) ? highlights : null;

    [Fact]
    public async Task Keeps_The_First_Answer_When_Merging_Is_Disabled()
    {
        var calls = new List<string>();
        Register("*", () => { calls.Add("lower"); return Of(s_second); });
        Register("plaintext", () => { calls.Add("higher"); return Of(s_first); });

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, false);

        calls.ShouldBe(["higher"]);
        Get(result).ShouldBe([s_first]);
    }

    [Fact]
    public async Task An_Empty_Answer_Still_Stops_The_Default_Provider_Search()
    {
        var calls = new List<string>();
        Register("*", () => { calls.Add("lower"); return Of(s_second); });
        Register("plaintext", () => { calls.Add("higher"); return Of(); });

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, false);

        calls.ShouldBe(["higher"]);
        Get(result).ShouldBe([]);
    }

    [Fact]
    public async Task Queries_Different_Priorities_Concurrently_And_Deduplicates_Ranges_In_Provider_Order()
    {
        var calls = new List<string>();
        var higher = new TaskCompletionSource<IReadOnlyList<DocumentHighlight>?>();
        var lower = new TaskCompletionSource<IReadOnlyList<DocumentHighlight>?>();
        var text = new DocumentHighlight(new TextRange(1, 11, 1, 15)) { Kind = DocumentHighlightKind.Text };
        var unspecified = new DocumentHighlight(new TextRange(2, 1, 2, 5));
        var overlapping = new DocumentHighlight(new TextRange(1, 2, 1, 5)) { Kind = DocumentHighlightKind.Write };
        DocumentHighlight[] higherHighlights = [s_first, s_first, text, unspecified];

        Register("*", () => { calls.Add("lower"); return lower.Task; });
        Register("plaintext", () => { calls.Add("higher"); return higher.Task; });

        var pending = WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);
        var callsBeforeCompletion = calls.ToList();
        lower.SetResult(
        [
            new DocumentHighlight(s_first.Range) { Kind = DocumentHighlightKind.Write },
            s_second,
            new DocumentHighlight(text.Range) { Kind = DocumentHighlightKind.Read },
            new DocumentHighlight(unspecified.Range) { Kind = DocumentHighlightKind.Write },
            overlapping,
        ]);
        higher.SetResult(higherHighlights);
        var result = await pending;

        callsBeforeCompletion.ShouldBe(["higher", "lower"]);
        Get(result).ShouldBe([s_first, text, unspecified, s_second, overlapping]);
    }

    public static TheoryData<string> Answers => new() { "null", "empty" };

    [Theory]
    [InlineData("null")]
    [InlineData("an empty array")]
    public async Task Merges_Other_Providers_When_A_Higher_Priority_Provider_Returns(string name)
    {
        Register("*", () => Of(s_second));
        Register("plaintext", () => name == "null" ? Task.FromResult<IReadOnlyList<DocumentHighlight>?>(null) : Of());

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);

        Get(result).ShouldBe([s_second]);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("an empty array")]
    public async Task Does_Not_Use_Textual_Fallback_When_A_Matching_Provider_Returns(string name)
    {
        _store.Add(_registry.Register("*", new TextualDocumentHighlightProvider()));
        Register("plaintext", () => name == "null" ? Task.FromResult<IReadOnlyList<DocumentHighlight>?>(null) : Of());

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);

        if (name == "null")
        {
            Get(result).ShouldBeNull();
        }
        else
        {
            Get(result).ShouldBe([]);
        }
    }

    [Fact]
    public async Task Reports_Provider_Failures_Without_Discarding_Successful_Results()
    {
        var errors = new List<Exception>();
        var synchronousError = new InvalidOperationException("synchronous failure");
        var asynchronousError = new InvalidOperationException("asynchronous failure");
        Register("plaintext", () => Of(s_first));
        Register("plaintext", () => throw new OperationCanceledException());
        Register("plaintext", () => Task.FromException<IReadOnlyList<DocumentHighlight>?>(asynchronousError));
        Register("plaintext", () => throw synchronousError);

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true, errors.Add);

        errors.ShouldBe([synchronousError, asynchronousError]);
        Get(result).ShouldBe([s_first]);
    }

    [Fact]
    public async Task Uses_Textual_Fallback_Only_When_No_Other_Provider_Matches()
    {
        var nonMatchingCalls = 0;
        Register("javascript", () => { nonMatchingCalls++; return Of(s_first); });
        _store.Add(_registry.Register("*", new TextualDocumentHighlightProvider()));

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);

        nonMatchingCalls.ShouldBe(0);
        Get(result).ShouldBe(
        [
            new DocumentHighlight(new TextRange(1, 1, 1, 5)) { Kind = DocumentHighlightKind.Text },
            new DocumentHighlight(new TextRange(1, 6, 1, 10)) { Kind = DocumentHighlightKind.Text },
            new DocumentHighlight(new TextRange(1, 11, 1, 15)) { Kind = DocumentHighlightKind.Text },
            new DocumentHighlight(new TextRange(2, 1, 2, 5)) { Kind = DocumentHighlightKind.Text },
        ]);
    }

    [Fact]
    public async Task Keeps_Wildcard_Extension_Providers_But_Excludes_The_Built_In_Textual_Provider()
    {
        Register("*", () => Of(s_first));
        _store.Add(_registry.Register("*", new TextualDocumentHighlightProvider()));

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);

        Get(result).ShouldBe([s_first]);
    }

    [Fact]
    public async Task Preserves_Textual_Fallback_After_A_Declined_Answer_When_Merging_Is_Disabled()
    {
        _store.Add(_registry.Register("*", new TextualDocumentHighlightProvider()));
        Register("plaintext", () => Task.FromResult<IReadOnlyList<DocumentHighlight>?>(null));

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, false);

        Get(result)!.Select(static h => h.Range).ShouldBe(
        [
            new TextRange(1, 1, 1, 5), new TextRange(1, 6, 1, 10), new TextRange(1, 11, 1, 15), new TextRange(2, 1, 2, 5),
        ]);
    }

    [Fact]
    public async Task Preserves_Exclusive_Document_Selectors()
    {
        var calls = new List<string>();
        Register("*", () => { calls.Add("wildcard"); return Of(s_second); });
        _store.Add(_registry.Register(new LanguageFilter { Language = "plaintext", Exclusive = true }, new Provider(() => { calls.Add("exclusive"); return Of(s_first); })));

        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);

        calls.ShouldBe(["exclusive"]);
        Get(result).ShouldBe([s_first]);
    }

    [Fact]
    public async Task Returns_An_Empty_Map_Without_Providers()
    {
        var result = await WordHighlighterCore.GetOccurrencesAtPosition(_registry, _model, s_position, CancellationToken.None, true);

        result.Count.ShouldBe(0);
    }

    // ---- own tests of the textual provider

    [Fact]
    public async Task Textual_Provider_Finds_Whole_Word_Case_Sensitive_Occurrences()
    {
        using var model = TestTextModel.Create("foo Foo foobar foo");
        var provider = new TextualDocumentHighlightProvider();

        var highlights = await provider.ProvideDocumentHighlights(model, new TextPosition(1, 2), CancellationToken.None);

        highlights!.Select(static h => h.Range).ShouldBe([new TextRange(1, 1, 1, 4), new TextRange(1, 16, 1, 19)]);
        highlights!.ShouldAllBe(static h => h.Kind == DocumentHighlightKind.Text);
    }

    [Fact]
    public async Task Textual_Provider_Returns_Nothing_Off_A_Word()
    {
        using var model = TestTextModel.Create("foo   bar");
        var provider = new TextualDocumentHighlightProvider();

        var highlights = await provider.ProvideDocumentHighlights(model, new TextPosition(1, 6), CancellationToken.None);

        highlights.ShouldNotBeNull().ShouldBeEmpty();
    }
}
