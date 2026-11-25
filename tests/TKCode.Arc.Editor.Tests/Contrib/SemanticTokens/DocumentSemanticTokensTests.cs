// Ported from vscode (src/vs/editor/contrib/semanticTokens/test/browser/documentSemanticTokens.test.ts,
// getSemanticTokens.test.ts and viewportSemanticTokens.test.ts, commit b57d5c9e), MIT license: all 11 tests of
// documentSemanticTokens.test.ts, the 1 of getSemanticTokens.test.ts and the 1 of viewportSemanticTokens.test.ts.
//
// Differences: there is no model service; the feature watches the models a test adds (AddModel). runWithFakedTimers is
// InlineFakeTimers (virtual time); Barrier is a TaskCompletionSource whose continuations run inline. "theme changes do
// not register models disposed by token listeners" checks that no watcher is created for the disposed model (upstream
// spies on onDidChangeContent registrations). The model id is its URI.

using Shouldly;
using TKCode.Arc.Editor.Contrib.SemanticTokens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;
using Xunit;
using static TKCode.Arc.Editor.Tests.SemanticTokensProviderStylingTests;

namespace TKCode.Arc.Editor.Tests;

public sealed class DocumentSemanticTokensTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly InlineFakeTimers _timers = new();
    private readonly LanguageFeatures _languageFeatures = new();
    private readonly StubThemeService _themeService;
    private readonly SemanticTokensStylingService _stylingService;
    private readonly DocumentSemanticTokensFeature _feature;

    public DocumentSemanticTokensTests()
    {
        _themeService = new StubThemeService(new StubTheme((_, _) => new TokenStyle(1), semanticHighlighting: true));
        _stylingService = new SemanticTokensStylingService(_themeService);
        _feature = new DocumentSemanticTokensFeature(_stylingService, _languageFeatures.DocumentSemanticTokensProvider, EditorSemanticHighlighting.Enabled, _timers.Scheduler);
    }

    public void Dispose()
    {
        _feature.Dispose();
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }

        _stylingService.Dispose();
    }

    internal sealed class Provider : IDocumentSemanticTokensProvider
    {
        public Func<TextModel, string?, Task<SemanticTokensOrEdits?>> Provide { get; init; } = (_, _) => Task.FromResult<SemanticTokensOrEdits?>(null);

        public Action<string?> Release { get; init; } = _ => { };

        public string[] TokenTypes { get; init; } = ["class"];

        public event Action? DidChange;

        public void FireDidChange() => DidChange?.Invoke();

        public SemanticTokensLegend GetLegend() => new(TokenTypes, []);

        public Task<SemanticTokensOrEdits?> ProvideDocumentSemanticTokens(TextModel model, string? lastResultId, CancellationToken token) => Provide(model, lastResultId);

        public void ReleaseDocumentSemanticTokens(string? resultId) => Release(resultId);
    }

    private static Task<SemanticTokensOrEdits?> Result(SemanticTokensOrEdits? value) => Task.FromResult(value);

    private static SemanticTokensOrEdits Full(string? resultId, params uint[] data) => new SemanticTokens(data) { ResultId = resultId };

    private TextModel CreateModel(string text, string language = "testMode", bool watch = true)
    {
        var model = new TextModel(text, language, TextModel.DefaultCreationOptions, TestTextModel.CreateUndoRedoService(), null, null, null, _timers.Scheduler);
        _disposables.Add(model);
        if (watch)
        {
            _disposables.Add(_feature.AddModel(model));
        }

        return model;
    }

    private void Register(string language, IDocumentSemanticTokensProvider provider) =>
        _disposables.Add(_languageFeatures.DocumentSemanticTokensProvider.Register(language, provider));

    private static string Id(TextModel model) => model.Uri.ToString();

    [Fact]
    public void Theme_listeners_do_not_grow_with_semantic_coloring_models()
    {
        var initialCount = _themeService.ListenerCount;
        var models = Enumerable.Range(0, 20).Select(_ => CreateModel("Hello world", "plaintext")).ToList();
        var afterAddingModels = _themeService.ListenerCount;

        _feature.Setting = EditorSemanticHighlighting.ConfiguredByTheme;
        _themeService.SetTheme(new StubTheme((_, _) => null, semanticHighlighting: false));
        var afterDisabling = _themeService.ListenerCount;
        _themeService.SetTheme(new StubTheme((_, _) => null, semanticHighlighting: true));
        var afterEnabling = _themeService.ListenerCount;

        foreach (var model in models)
        {
            model.Dispose();
        }

        new[] { afterAddingModels, afterDisabling, afterEnabling, _themeService.ListenerCount }
            .ShouldBe([initialCount, initialCount, initialCount, initialCount]);
    }

    [Fact]
    public void Theme_changes_clear_semantic_tokens_and_refetch_with_the_new_styling()
    {
        static StubTheme CreateTheme(int foreground) => new((_, _) => new TokenStyle(foreground), semanticHighlighting: true);
        _themeService.SetTheme(CreateTheme(1));

        var lastResultIds = new List<string?>();
        var releasedResultIds = new List<string?>();
        Register("testMode", new Provider
        {
            Provide = (_, lastResultId) =>
            {
                lastResultIds.Add(lastResultId);
                return Result(Full(lastResultIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, 0, 5, 0, 0));
            },
            Release = releasedResultIds.Add,
        });
        var model = CreateModel("Hello world");
        model.OnBeforeAttached();
        _timers.Advance(1000);
        var initialForeground = model.Tokenization.GetLineTokens(1).GetForeground(0);

        _themeService.SetTheme(CreateTheme(2));
        var clearedImmediately = !model.Tokenization.HasCompleteSemanticTokens();
        _timers.Advance(1000);

        ((int)initialForeground).ShouldBe(1);
        clearedImmediately.ShouldBeTrue();
        ((int)model.Tokenization.GetLineTokens(1).GetForeground(0)).ShouldBe(2);
        lastResultIds.ShouldBe([null, null]);
        releasedResultIds.ShouldBe(["1"]);
    }

    [Fact]
    public void Theme_changes_do_not_register_models_disposed_by_token_listeners()
    {
        var firstModel = CreateModel("First model", "plaintext");
        var secondModel = CreateModel("Second model", "plaintext");
        firstModel.DidChangeTokens += _ => secondModel.Dispose();

        _themeService.SetTheme(new StubTheme((_, _) => null, semanticHighlighting: true));

        secondModel.IsDisposed().ShouldBeTrue();
        _feature.GetWatcher(secondModel).ShouldBeNull();
    }

    [Fact]
    public void Theme_changes_continue_refreshing_models_after_a_provider_release_error()
    {
        var requests = new List<string>();
        var reportedErrors = new List<Exception>();
        var releaseError = new InvalidOperationException("Failed to release semantic tokens");
        string? throwForResultId = null;
        Register("testMode", new Provider
        {
            Provide = (model, _) =>
            {
                requests.Add(Id(model));
                return Result(Full(Id(model), 0, 0, 5, 0, 0));
            },
            Release = resultId =>
            {
                if (resultId == throwForResultId)
                {
                    throwForResultId = null;
                    throw releaseError;
                }
            },
        });
        var firstModel = CreateModel("First model");
        var secondModel = CreateModel("Second model");
        firstModel.OnBeforeAttached();
        secondModel.OnBeforeAttached();
        _timers.Advance(1000);

        Action<Exception> handler = reportedErrors.Add;
        UnexpectedErrors.Occurred += handler;
        try
        {
            throwForResultId = Id(firstModel);
            _themeService.SetTheme(new StubTheme((_, _) => new TokenStyle(1), semanticHighlighting: true));
            _timers.Advance(1000);

            requests.ShouldBe([Id(firstModel), Id(secondModel), Id(secondModel)]);
            reportedErrors.ShouldBe([releaseError]);
        }
        finally
        {
            UnexpectedErrors.Occurred -= handler;
        }
    }

    [Fact]
    public void Enabling_semantic_coloring_through_a_theme_immediately_fetches_only_attached_models()
    {
        _feature.Setting = EditorSemanticHighlighting.ConfiguredByTheme;
        _themeService.SetTheme(new StubTheme((_, _) => new TokenStyle(1), semanticHighlighting: false));

        var requestedModels = new List<TextModel>();
        Register("testMode", new Provider
        {
            Provide = (model, _) =>
            {
                requestedModels.Add(model);
                return Result(Full(null, 0, 0, 5, 0, 0));
            },
        });
        var visibleModel = CreateModel("Hello world");
        CreateModel("Hidden model");
        visibleModel.OnBeforeAttached();

        _themeService.SetTheme(new StubTheme((_, _) => new TokenStyle(1), semanticHighlighting: true));
        _timers.Advance(1);
        var immediatelyRequested = requestedModels.Count;
        _timers.Advance(1000);

        immediatelyRequested.ShouldBe(1);
        requestedModels.ShouldBe([visibleModel]);
    }

    [Fact]
    public void DocumentSemanticTokens_should_be_fetched_when_the_result_is_empty_if_there_are_pending_changes()
    {
        var inFirstCall = false;
        var delayFirstResult = new TaskCompletionSource<SemanticTokensOrEdits?>();
        var callCount = 0;
        Register("testMode", new Provider
        {
            Provide = (_, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    inFirstCall = true;
                    return delayFirstResult.Task;
                }

                if (callCount == 2)
                {
                    return Result(null);
                }

                throw new InvalidOperationException("Unexpected call");
            },
        });

        var textModel = CreateModel("Hello world");
        // pretend the text model is attached to an editor (so that semantic tokens are computed)
        textModel.OnBeforeAttached();

        // wait for the provider to be called
        _timers.Advance(0);
        inFirstCall.ShouldBeTrue();

        // the provider is now in the provide call: change the text buffer while the provider is running
        textModel.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "x")]);

        // let the provider finish its first result
        _timers.Advance(1000);
        delayFirstResult.SetResult(null);

        // we need to check that the provider is called again, even if it returns null
        _timers.Advance(1000);

        callCount.ShouldBe(2);
    }

    [Fact]
    public void Issue_149412_bad_semantic_token_data_does_not_hang()
    {
        SemanticTokensOrEdits? lastResult = null;
        Register("testMode", new Provider
        {
            Provide = (_, lastResultId) =>
            {
                lastResult = lastResultId is null
                    // this is the first call
                    ? Full("1", 4294967293, 0, 7, 16, 0, 1, 4, 3, 11, 1)
                    // this is the second call
                    : new SemanticTokensEdits([new SemanticTokensEdit(unchecked((int)4294967276u), 0) { Data = [2, 0, 3, 11, 0] }]) { ResultId = "2" };
                return Result(lastResult);
            },
        });

        var errors = new List<Exception>();
        Action<Exception> onError = errors.Add;
        UnexpectedErrors.Occurred += onError;
        using var unsubscribe = new Unsubscribe(() => UnexpectedErrors.Occurred -= onError);
        var textModel = CreateModel("");
        var tokenChanges = 0;
        var resultIdsAtTokenChanges = new List<string?>();
        textModel.DidChangeTokens += _ =>
        {
            tokenChanges++;
            resultIdsAtTokenChanges.Add(lastResult?.Tokens?.ResultId ?? lastResult?.Edits?.ResultId);
        };
        textModel.OnBeforeAttached();

        // wait for the semantic tokens to be fetched
        _timers.Advance(1000);
        tokenChanges.ShouldBe(1);
        lastResult!.Value.Tokens!.ResultId.ShouldBe("1");

        // edit the text
        textModel.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "foo")]);

        // wait for the semantic tokens to be fetched again
        _timers.Advance(1000);
        // (the invalid edit is rejected with a token change, then the full tokens are asked for again)
        errors.ShouldBeEmpty();
        resultIdsAtTokenChanges[1].ShouldBe("2");
    }

    private sealed class Unsubscribe(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    [Fact]
    public void Issue_322571_semantic_token_edits_cannot_delete_more_data_than_the_previous_result_contains()
    {
        var lastResultIds = new List<string?>();
        var releasedResultIds = new List<string?>();
        Register("testMode", new Provider
        {
            Provide = (_, lastResultId) =>
            {
                lastResultIds.Add(lastResultId);
                return lastResultIds.Count switch
                {
                    1 => Result(Full("full", 0, 0, 1, 0, 0)),
                    2 => Result(new SemanticTokensEdits([new SemanticTokensEdit(0, 6)]) { ResultId = "invalid-edit" }),
                    3 => Result(Full("recovered-full", 0, 0, 1, 0, 0)),
                    _ => throw new InvalidOperationException("Unexpected call"),
                };
            },
            Release = releasedResultIds.Add,
        });

        var textModel = CreateModel("a");
        textModel.OnBeforeAttached();
        _timers.Advance(1000);
        textModel.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "b")]);
        _timers.Advance(1000);

        lastResultIds.ShouldBe([null, "full", null]);
        releasedResultIds.ShouldBe(["full", "invalid-edit"]);
    }

    [Fact]
    public void Invalid_semantic_token_edits_without_a_previous_result_are_not_retried()
    {
        var requestCount = 0;
        var releasedResultIds = new List<string?>();
        Register("testMode", new Provider
        {
            Provide = (_, _) =>
            {
                requestCount++;
                return Result(new SemanticTokensEdits([new SemanticTokensEdit(0, 1)]) { ResultId = "invalid-edit" });
            },
            Release = releasedResultIds.Add,
        });

        var textModel = CreateModel("a");
        textModel.OnBeforeAttached();
        _timers.Advance(1000);
        _timers.Advance(1000);

        requestCount.ShouldBe(1);
        releasedResultIds.ShouldBe(["invalid-edit"]);
    }

    [Fact]
    public void Issue_161573_onDidChangeSemanticTokens_consistently_triggers_provideDocumentSemanticTokens()
    {
        var requestCount = 0;
        var firstResult = new TaskCompletionSource<SemanticTokensOrEdits?>();
        Provider? provider = null;
        provider = new Provider
        {
            Provide = (_, _) =>
            {
                requestCount++;
                return requestCount == 1 ? firstResult.Task : Result(null);
            },
        };
        Register("testMode", provider);

        var textModel = CreateModel("");
        // pretend the text model is attached to an editor (so that semantic tokens are computed)
        textModel.OnBeforeAttached();

        _timers.Advance(1000);
        // send a change event while the first request runs
        provider.FireDidChange();
        _timers.Advance(1000);
        firstResult.SetResult(null);
        _timers.Advance(3000);

        requestCount.ShouldBe(2);
    }

    [Fact]
    public async Task DocumentSemanticTokens_should_pick_the_token_provider_with_actual_items()
    {
        var callCount = 0;
        var first = new Provider
        {
            TokenTypes = ["class1"],
            Provide = (_, lastResultId) =>
            {
                callCount++;
                // For a secondary request return a different value
                return lastResultId is not null
                    ? Result(Full(null, 2, 1, 1, 1, 1, 0, 2, 1, 1, 1))
                    : Result(Full("1", 0, 1, 1, 1, 1, 0, 2, 1, 1, 1));
            },
        };
        Register("testMode2", first);
        Register("testMode2", new Provider
        {
            TokenTypes = ["class2"],
            Provide = (_, _) =>
            {
                callCount++;
                return Result(null);
            },
        });

        var textModel = CreateModel("Hello world 2", "testMode2", watch: false);
        var result = await GetSemanticTokens.GetDocumentSemanticTokens(_languageFeatures.DocumentSemanticTokensProvider, textModel, null, null, CancellationToken.None);
        result.ShouldNotBeNull();
        GetSemanticTokens.IsSemanticTokens(result.Tokens).ShouldBeTrue();
        result.Tokens!.Value.Tokens!.ResultId.ShouldNotBeNull();
        result.Tokens.Value.Tokens.Data.ShouldBe([0u, 1, 1, 1, 1, 0, 2, 1, 1, 1]);
        callCount.ShouldBe(2);
        result.Provider.GetLegend().TokenTypes.ShouldBe(["class1"]);

        // Make a second request. Make sure we get the secondary value
        result = await GetSemanticTokens.GetDocumentSemanticTokens(_languageFeatures.DocumentSemanticTokensProvider, textModel, result.Provider, result.Tokens.Value.Tokens.ResultId, CancellationToken.None);
        result.ShouldNotBeNull();
        GetSemanticTokens.IsSemanticTokens(result.Tokens).ShouldBeTrue();
        result.Tokens!.Value.Tokens!.ResultId.ShouldBeNull();
        result.Tokens.Value.Tokens.Data.ShouldBe([2u, 1, 1, 1, 1, 0, 2, 1, 1, 1]);
        callCount.ShouldBe(4);
        result.Provider.GetLegend().TokenTypes.ShouldBe(["class1"]);
    }

    // getSemanticTokens.test.ts
    [Fact]
    public async Task Issue_136540_semantic_highlighting_flickers()
    {
        var registry = new LanguageFeatureRegistry<IDocumentSemanticTokensProvider>();
        using var registration = registry.Register("testLang", new Provider { Provide = (_, _) => throw new OperationCanceledException() });
        using var textModel = TestTextModel.Create("example", "testLang");

        await Should.ThrowAsync<OperationCanceledException>(() =>
            GetSemanticTokens.GetDocumentSemanticTokens(registry, textModel, null, null, CancellationToken.None));
    }

    // viewportSemanticTokens.test.ts
    [Fact]
    public void DocumentRangeSemanticTokens_provider_onDidChange_event_should_trigger_refresh()
    {
        var calls = 0;
        var provider = new RangeProvider(() =>
        {
            calls++;
            return new SemanticTokens([0, 1, 1, 0, 0]);
        });
        _disposables.Add(_languageFeatures.DocumentRangeSemanticTokensProvider.Register("testMode", provider));
        var model = CreateModel("hello world", watch: false);
        model.OnBeforeAttached();
        using var viewport = new ViewportSemanticTokens(
            () => model,
            () => [new TextRange(1, 1, 1, 12)],
            () => EditorSemanticHighlighting.Enabled,
            _stylingService,
            _languageFeatures.DocumentRangeSemanticTokensProvider,
            _timers.Scheduler);

        _timers.Advance(1000);
        calls.ShouldBe(1);
        model.Tokenization.HasSomeSemanticTokens().ShouldBeTrue();

        provider.FireDidChange();
        _timers.Advance(1000);
        calls.ShouldBe(2);
    }

    private sealed class RangeProvider(Func<SemanticTokens?> provide) : IDocumentRangeSemanticTokensProvider
    {
        public event Action? DidChange;

        public void FireDidChange() => DidChange?.Invoke();

        public SemanticTokensLegend GetLegend() => new(["class"], []);

        public Task<SemanticTokens?> ProvideDocumentRangeSemanticTokens(TextModel model, TextRange range, CancellationToken token) => Task.FromResult(provide());
    }
}
