// Tests of the code lens model and cache (vscode's src/vs/editor/contrib/codelens/browser/codelens.ts and
// codeLensCache.ts, commit b57d5c9e, MIT license). Upstream has no unit tests of code lenses; these are written for
// this port: the sort by line, provider rank and column, disposal, cancellation, and the cache's copies, line count
// check, LRU trimming and JSON round trip.

using Shouldly;
using TKCode.Arc.Editor.Contrib.CodeLens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;
using CodeLens = TKCode.Arc.Editor.Languages.CodeLens;

namespace TKCode.Arc.Editor.Tests;

public sealed class CodeLensTests : IDisposable
{
    private const string LangId = "lensLang";
    private readonly TextModel _model = TestTextModel.Create("a\nb\nc\nd", LangId, uri: new Uri("file:///lens.txt"));
    private readonly LanguageFeatureRegistry<ICodeLensProvider> _registry = new();
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _model.Dispose();
    }

    private sealed class Provider(params CodeLens[] lenses) : ICodeLensProvider
    {
        public int Disposed { get; private set; }

        public Task<CodeLensList?> ProvideCodeLenses(TextModel model, CancellationToken token) =>
            Task.FromResult<CodeLensList?>(new CodeLensList(lenses) { Dispose = () => Disposed++ });
    }

    private static CodeLens Lens(int line, int column, string title) =>
        new(new TextRange(line, column, line, column + 1)) { Command = new LanguageCommand("cmd." + title, title) };

    [Fact]
    public async Task Lenses_are_sorted_by_line_provider_rank_and_column()
    {
        var older = new Provider(Lens(3, 1, "older-3"), Lens(1, 5, "older-1"));
        var newer = new Provider(Lens(1, 9, "newer-1b"), Lens(1, 2, "newer-1a"), Lens(2, 1, "newer-2"));
        _disposables.Add(_registry.Register(LangId, older));
        _disposables.Add(_registry.Register(LangId, newer));

        using var model = await CodeLensService.GetCodeLensModel(_registry, _model, CancellationToken.None);
        model.Lenses.Select(l => l.Symbol.Command!.Title).ShouldBe(["newer-1a", "newer-1b", "older-1", "newer-2", "older-3"]);
        model.Lenses[0].Provider.ShouldBeSameAs(newer);

        model.Dispose();
        model.IsDisposed.ShouldBeTrue();
        (older.Disposed, newer.Disposed).ShouldBe((1, 1));
    }

    [Fact]
    public async Task A_cancelled_request_gives_an_empty_model_and_disposes_the_lists()
    {
        var provider = new Provider(Lens(1, 1, "x"));
        _disposables.Add(_registry.Register(LangId, provider));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var model = await CodeLensService.GetCodeLensModel(_registry, _model, cts.Token);
        model.Lenses.ShouldBeEmpty();
        provider.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task The_cache_keeps_titles_without_commands_for_the_same_line_count()
    {
        _disposables.Add(_registry.Register(LangId, new Provider(Lens(2, 1, "2 references"), new CodeLens(new TextRange(4, 1, 4, 1)))));
        using var lenses = await CodeLensService.GetCodeLensModel(_registry, _model, CancellationToken.None);
        var cache = new CodeLensCache();
        cache.Put(_model, lenses);

        var cached = cache.Get(_model).ShouldNotBeNull();
        cached.Lenses.Count.ShouldBe(2);
        cached.Lenses[0].Symbol.Command.ShouldBe(new LanguageCommand(string.Empty, "2 references"));
        cached.Lenses[1].Symbol.Command.ShouldBeNull();

        _model.SetValue("a\nb\nc\nd\ne");
        cache.Get(_model).ShouldBeNull();
        cache.Count.ShouldBe(1);
        cache.Delete(_model);
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void The_cache_trims_to_the_15_most_recently_used_and_round_trips_through_json()
    {
        var cache = new CodeLensCache();
        var models = Enumerable.Range(0, 21).Select(i => TestTextModel.Create("x\ny", uri: new Uri($"file:///m{i}.txt"))).ToList();
        try
        {
            var data = new CodeLensModel();
            data.Add(new CodeLensList([Lens(2, 3, "t"), Lens(2, 7, "u")]), new Provider());
            for (var i = 0; i < 20; i++)
            {
                cache.Put(models[i], data);
            }

            cache.Get(models[0]).ShouldNotBeNull(); // touch: now the most recent
            cache.Put(models[20], data);
            cache.Count.ShouldBe(15);
            cache.Get(models[0]).ShouldNotBeNull();
            cache.Get(models[1]).ShouldBeNull();

            var json = cache.Serialize();
            json.ShouldContain("\"file:///m20.txt\":{\"lineCount\":2,\"lines\":[2]}");
            var restored = new CodeLensCache();
            restored.Deserialize(json);
            restored.Count.ShouldBe(15);
            restored.Get(models[20]).ShouldNotBeNull().Lenses.Single().Symbol.Range.ShouldBe(new TextRange(2, 1, 2, 11));

            restored.Deserialize("{not json");
            restored.Count.ShouldBe(15);
        }
        finally
        {
            models.ForEach(m => m.Dispose());
        }
    }
}
