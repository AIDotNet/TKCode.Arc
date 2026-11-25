// Ported from vscode (src/vs/editor/contrib/semanticTokens/browser/viewportSemanticTokens.ts, commit b57d5c9e), MIT
// license.
//
// Differences: this is the editor-independent part of ViewportSemanticTokensContribution: the editor's contribution
// (SemanticTokensContribution in the controls) calls ScheduleTokenizeViewport / CancelAll on the editor events upstream
// listens to and supplies the visible ranges (upstream's getVisibleRangesPlusViewportAboveBelow). The debounce is the
// fixed minimum, 100 ms (upstream: ILanguageFeatureDebounceService, 100..500 ms).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;

namespace TKCode.Arc.Editor.Contrib.SemanticTokens;

/// <summary>Asks range semantic tokens providers for the visible lines while no full document tokens are there.</summary>
internal sealed class ViewportSemanticTokens : IDisposable
{
    public const int DebounceDelay = 100;

    private readonly Func<TextModel?> _getModel;
    private readonly Func<IReadOnlyList<TextRange>> _getVisibleRanges;
    private readonly Func<EditorSemanticHighlighting> _getSetting;
    private readonly SemanticTokensStylingService _stylingService;
    private readonly LanguageFeatureRegistry<IDocumentRangeSemanticTokensProvider> _provider;
    private readonly SemanticTokensScheduler _tokenizeViewport;
    private readonly List<CancellationTokenSource> _outstandingRequests = new();
    private readonly List<(IDocumentRangeSemanticTokensProvider Provider, Action Handler)> _rangeProvidersChangeListeners = new();

    public ViewportSemanticTokens(
        Func<TextModel?> getModel,
        Func<IReadOnlyList<TextRange>> getVisibleRanges,
        Func<EditorSemanticHighlighting> getSetting,
        SemanticTokensStylingService stylingService,
        LanguageFeatureRegistry<IDocumentRangeSemanticTokensProvider> provider,
        IdleScheduler scheduler)
    {
        _getModel = getModel;
        _getVisibleRanges = getVisibleRanges;
        _getSetting = getSetting;
        _stylingService = stylingService;
        _provider = provider;
        _tokenizeViewport = new SemanticTokensScheduler(scheduler, TokenizeViewportNow, DebounceDelay);

        BindRangeProvidersChangeListeners();
        _provider.DidChange += OnRegistryChanged;
        _stylingService.ThemeService.DidColorThemeChange += OnThemeChanged;
        ScheduleTokenizeViewport();
    }

    /// <summary>The requests that have not answered yet (for tests).</summary>
    internal int OutstandingRequestCount => _outstandingRequests.Count;

    public void ScheduleTokenizeViewport()
    {
        if (_getModel() is not null)
        {
            _tokenizeViewport.Schedule(DebounceDelay);
        }
    }

    /// <summary>The editor's model or its language changed: listen to the new providers, start over.</summary>
    public void HandleModelOrLanguageChanged()
    {
        BindRangeProvidersChangeListeners();
        CancelAll();
        ScheduleTokenizeViewport();
    }

    /// <summary>The content changed: the requests in flight are stale.</summary>
    public void HandleContentChanged()
    {
        CancelAll();
        ScheduleTokenizeViewport();
    }

    /// <summary>The semantic highlighting setting changed.</summary>
    public void HandleSettingChanged()
    {
        CancelAll();
        ScheduleTokenizeViewport();
    }

    private void OnThemeChanged()
    {
        CancelAll();
        ScheduleTokenizeViewport();
    }

    private void OnRegistryChanged(int _)
    {
        BindRangeProvidersChangeListeners();
        CancelAll();
        ScheduleTokenizeViewport();
    }

    private void BindRangeProvidersChangeListeners()
    {
        CleanupProviderListeners();
        if (_getModel() is { } model)
        {
            foreach (var p in _provider.All(model))
            {
                Action handler = () =>
                {
                    CancelAll();
                    ScheduleTokenizeViewport();
                };
                p.DidChange += handler;
                _rangeProvidersChangeListeners.Add((p, handler));
            }
        }
    }

    private void CleanupProviderListeners()
    {
        foreach (var (provider, handler) in _rangeProvidersChangeListeners)
        {
            provider.DidChange -= handler;
        }

        _rangeProvidersChangeListeners.Clear();
    }

    public void CancelAll()
    {
        foreach (var request in _outstandingRequests)
        {
            request.Cancel();
        }

        _outstandingRequests.Clear();
    }

    private void TokenizeViewportNow()
    {
        if (_getModel() is not { } model)
        {
            return;
        }

        if (model.Tokenization.HasCompleteSemanticTokens())
        {
            return;
        }

        if (!GetSemanticTokens.IsSemanticColoringEnabled(_getSetting(), _stylingService.ThemeService))
        {
            if (model.Tokenization.HasSomeSemanticTokens())
            {
                model.Tokenization.SetSemanticTokens(null, false);
            }

            return;
        }

        if (!GetSemanticTokens.HasDocumentRangeSemanticTokensProvider(_provider, model))
        {
            if (model.Tokenization.HasSomeSemanticTokens())
            {
                model.Tokenization.SetSemanticTokens(null, false);
            }

            return;
        }

        foreach (var range in _getVisibleRanges())
        {
            RequestRange(model, range);
        }
    }

    private void RequestRange(TextModel model, TextRange range)
    {
        var requestVersionId = model.GetVersionId();
        var cts = new CancellationTokenSource();
        _outstandingRequests.Add(cts);
        _ = Run();

        async Task Run()
        {
            try
            {
                var r = await GetSemanticTokens.GetDocumentRangeSemanticTokens(_provider, model, range, cts.Token).ConfigureAwait(true);
                if (cts.IsCancellationRequested || r?.Tokens is not { } result || model.IsDisposed() || model.GetVersionId() != requestVersionId)
                {
                    return;
                }

                var styling = _stylingService.GetStyling(r.Provider, model.Tokenization.LanguageIdCodec);
                model.Tokenization.SetPartialSemanticTokens(range, SemanticTokensConversion.ToMultilineTokens2(result, styling, model.GetLanguageId()));
            }
            catch (Exception err) when (err is not OperationCanceledException)
            {
                UnexpectedErrors.Report(err);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _outstandingRequests.Remove(cts);
            }
        }
    }

    public void Dispose()
    {
        CancelAll();
        CleanupProviderListeners();
        _provider.DidChange -= OnRegistryChanged;
        _stylingService.ThemeService.DidColorThemeChange -= OnThemeChanged;
        _tokenizeViewport.Dispose();
    }
}
