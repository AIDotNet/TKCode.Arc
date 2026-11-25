// Ported from vscode (src/vs/editor/contrib/semanticTokens/browser/documentSemanticTokens.ts, commit b57d5c9e), MIT
// license.
//
// Differences:
//  - DocumentSemanticTokensFeature watches the models it is given (AddModel; upstream: every model of the model service),
//    so an editor feeds it its own model;
//  - the setting is a property (upstream: the editor.semanticHighlighting configuration), the theme an
//    ISemanticTokensThemeService;
//  - the debounce is the fixed REQUEST_MIN_DELAY (upstream: ILanguageFeatureDebounceService, the average request time
//    clamped to 300..2000 ms);
//  - the timer is the model's IdleScheduler (Tokenization.Scheduler) unless another is given;
//  - an edit's start is compared as unsigned, as upstream's numbers from a Uint32Array are.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Services;

namespace TKCode.Arc.Editor.Contrib.SemanticTokens;

/// <summary>Keeps the semantic tokens of the watched models up to date (upstream's DocumentSemanticTokensFeature).</summary>
internal sealed class DocumentSemanticTokensFeature : IDisposable
{
    private readonly SemanticTokensStylingService _stylingService;
    private readonly ISemanticTokensThemeService _themeService;
    private readonly LanguageFeatureRegistry<IDocumentSemanticTokensProvider> _provider;
    private readonly IdleScheduler? _scheduler;
    private readonly List<TextModel> _models = new();
    private readonly Dictionary<TextModel, ModelSemanticColoring> _watchers = new();
    private readonly List<(IDocumentSemanticTokensProvider Provider, Action Handler)> _providerChangeListeners = new();
    private EditorSemanticHighlighting _setting;

    public DocumentSemanticTokensFeature(
        SemanticTokensStylingService stylingService,
        LanguageFeatureRegistry<IDocumentSemanticTokensProvider> provider,
        EditorSemanticHighlighting setting = EditorSemanticHighlighting.ConfiguredByTheme,
        IdleScheduler? scheduler = null)
    {
        _stylingService = stylingService;
        _themeService = stylingService.ThemeService;
        _provider = provider;
        _setting = setting;
        _scheduler = scheduler;

        _themeService.DidColorThemeChange += OnThemeChanged;
        BindProviderChangeListeners();
        _provider.DidChange += OnRegistryChanged;
    }

    /// <summary>Upstream's editor.semanticHighlighting.enabled.</summary>
    public EditorSemanticHighlighting Setting
    {
        get => _setting;
        set
        {
            if (_setting != value)
            {
                _setting = value;
                HandleSettingOrThemeChange(false);
            }
        }
    }

    /// <summary>The coloring of a model, when it is watched and semantic coloring is on (for tests).</summary>
    internal ModelSemanticColoring? GetWatcher(TextModel model) => _watchers.GetValueOrDefault(model);

    /// <summary>Starts watching a model (upstream's onModelAdded); dispose the result to stop (onModelRemoved).</summary>
    public IDisposable AddModel(TextModel model)
    {
        _models.Add(model);
        if (IsEnabled())
        {
            Register(model);
        }

        Registration? registration = null;
        Action onWillDispose = () => registration?.Dispose();
        model.WillDispose += onWillDispose;
        registration = new Registration(() =>
        {
            model.WillDispose -= onWillDispose;
            _models.Remove(model);
            if (_watchers.TryGetValue(model, out var curr))
            {
                Deregister(model, curr);
            }
        });
        return registration;
    }

    private bool IsEnabled() => GetSemanticTokens.IsSemanticColoringEnabled(_setting, _themeService);

    private void Register(TextModel model)
    {
        if (_watchers.TryGetValue(model, out var existing))
        {
            existing.Dispose();
        }

        _watchers[model] = new ModelSemanticColoring(model, _stylingService, _provider, _scheduler ?? model.Tokenization.Scheduler);
    }

    private void Deregister(TextModel model, ModelSemanticColoring modelSemanticColoring)
    {
        modelSemanticColoring.Dispose();
        _watchers.Remove(model);
    }

    private void OnThemeChanged() => HandleSettingOrThemeChange(true);

    private void HandleSettingOrThemeChange(bool themeChanged)
    {
        foreach (var model in _models.ToArray())
        {
            // Updating tokens can synchronously dispose another model in this snapshot.
            if (model.IsDisposed())
            {
                continue;
            }

            _watchers.TryGetValue(model, out var curr);
            if (IsEnabled())
            {
                if (curr is null)
                {
                    Register(model);
                }
                else if (themeChanged)
                {
                    try
                    {
                        curr.HandleThemeChange();
                    }
                    catch (Exception err)
                    {
                        UnexpectedErrors.Report(err);
                    }
                }
            }
            else if (curr is not null)
            {
                Deregister(model, curr);
            }
        }
    }

    private void BindProviderChangeListeners()
    {
        ClearProviderChangeListeners();
        foreach (var p in _provider.AllNoModel())
        {
            var provider = p;
            Action handler = () =>
            {
                foreach (var watcher in _watchers.Values.ToArray())
                {
                    watcher.HandleProviderDidChange(provider);
                }
            };
            provider.DidChange += handler;
            _providerChangeListeners.Add((provider, handler));
        }
    }

    private void ClearProviderChangeListeners()
    {
        foreach (var (provider, handler) in _providerChangeListeners)
        {
            provider.DidChange -= handler;
        }

        _providerChangeListeners.Clear();
    }

    private void OnRegistryChanged(int _)
    {
        BindProviderChangeListeners();
        foreach (var watcher in _watchers.Values.ToArray())
        {
            watcher.HandleRegistryChange();
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers.Values.ToArray())
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        _models.Clear();
        _themeService.DidColorThemeChange -= OnThemeChanged;
        _provider.DidChange -= OnRegistryChanged;
        ClearProviderChangeListeners();
    }

    private sealed class Registration(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}

/// <summary>Fetches and applies the semantic tokens of one model (upstream's ModelSemanticColoring).</summary>
internal sealed class ModelSemanticColoring : IDisposable
{
    public const int RequestMinDelay = 300;
    public const int RequestMaxDelay = 2000;

    private readonly TextModel _model;
    private readonly SemanticTokensStylingService _stylingService;
    private readonly LanguageFeatureRegistry<IDocumentSemanticTokensProvider> _provider;
    private readonly SemanticTokensScheduler _fetchDocumentSemanticTokens;
    private bool _isDisposed;
    private SemanticTokensResponse? _currentDocumentResponse;
    private CancellationTokenSource? _currentDocumentRequestCancellationTokenSource;
    private HashSet<IDocumentSemanticTokensProvider> _relevantProviders = new(ReferenceEqualityComparer.Instance);
    private bool _providersChangedDuringRequest;

    public ModelSemanticColoring(
        TextModel model,
        SemanticTokensStylingService stylingService,
        LanguageFeatureRegistry<IDocumentSemanticTokensProvider> provider,
        IdleScheduler scheduler)
    {
        _model = model;
        _stylingService = stylingService;
        _provider = provider;
        _fetchDocumentSemanticTokens = new SemanticTokensScheduler(scheduler, FetchDocumentSemanticTokensNow, RequestMinDelay);
        UpdateRelevantProviders();

        _model.DidChangeContent += OnDidChangeContent;
        _model.DidChangeAttached += OnDidChangeAttached;
        _model.DidChangeLanguage += OnDidChangeLanguage;

        _fetchDocumentSemanticTokens.Schedule(0);
    }

    /// <summary>The debounce (upstream: the debounce information of the model; here the minimum delay).</summary>
    private static int DebounceDelay => RequestMinDelay;

    private void OnDidChangeContent(ModelContentChangedEvent e)
    {
        if (!_fetchDocumentSemanticTokens.IsScheduled())
        {
            _fetchDocumentSemanticTokens.Schedule(DebounceDelay);
        }
    }

    private void OnDidChangeAttached()
    {
        if (!_fetchDocumentSemanticTokens.IsScheduled())
        {
            _fetchDocumentSemanticTokens.Schedule(DebounceDelay);
        }
    }

    private void OnDidChangeLanguage(ModelLanguageChangedEvent e)
    {
        // clear any outstanding state
        if (_currentDocumentResponse is not null)
        {
            _currentDocumentResponse.Dispose();
            _currentDocumentResponse = null;
        }

        if (_currentDocumentRequestCancellationTokenSource is not null)
        {
            _currentDocumentRequestCancellationTokenSource.Cancel();
            _currentDocumentRequestCancellationTokenSource = null;
        }

        SetDocumentSemanticTokens(null, null, null, []);
        UpdateRelevantProviders();
        _fetchDocumentSemanticTokens.Schedule(0);
    }

    public void HandleThemeChange()
    {
        SetDocumentSemanticTokens(null, null, null, []);
        _fetchDocumentSemanticTokens.Schedule(DebounceDelay);
    }

    public void HandleRegistryChange()
    {
        UpdateRelevantProviders();
        _fetchDocumentSemanticTokens.Schedule(DebounceDelay);
    }

    public void HandleProviderDidChange(IDocumentSemanticTokensProvider provider)
    {
        if (!_relevantProviders.Contains(provider))
        {
            return;
        }

        if (_currentDocumentRequestCancellationTokenSource is not null)
        {
            // there is already a request running,
            _providersChangedDuringRequest = true;
            return;
        }

        _fetchDocumentSemanticTokens.Schedule(0);
    }

    private void UpdateRelevantProviders() =>
        _relevantProviders = new HashSet<IDocumentSemanticTokensProvider>(_provider.All(_model), ReferenceEqualityComparer.Instance);

    public void Dispose()
    {
        if (_currentDocumentResponse is not null)
        {
            _currentDocumentResponse.Dispose();
            _currentDocumentResponse = null;
        }

        if (_currentDocumentRequestCancellationTokenSource is not null)
        {
            _currentDocumentRequestCancellationTokenSource.Cancel();
            _currentDocumentRequestCancellationTokenSource = null;
        }

        SetDocumentSemanticTokens(null, null, null, []);
        _isDisposed = true;

        _model.DidChangeContent -= OnDidChangeContent;
        _model.DidChangeAttached -= OnDidChangeAttached;
        _model.DidChangeLanguage -= OnDidChangeLanguage;
        _fetchDocumentSemanticTokens.Dispose();
    }

    private void FetchDocumentSemanticTokensNow()
    {
        if (_currentDocumentRequestCancellationTokenSource is not null)
        {
            // there is already a request running, let it finish...
            return;
        }

        if (!GetSemanticTokens.HasDocumentSemanticTokensProvider(_provider, _model))
        {
            // there is no provider
            if (_currentDocumentResponse is not null)
            {
                // there are semantic tokens set
                _model.Tokenization.SetSemanticTokens(null, false);
            }

            return;
        }

        if (!_model.IsAttachedToEditor())
        {
            // this document is not visible, there is no need to fetch semantic tokens for it
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        var lastProvider = _currentDocumentResponse?.Provider;
        var lastResultId = _currentDocumentResponse?.ResultId;
        _currentDocumentRequestCancellationTokenSource = cancellationTokenSource;
        _providersChangedDuringRequest = false;

        var pendingChanges = new List<ModelContentChangedEvent>();
        Action<ModelContentChangedEvent> contentChangeListener = pendingChanges.Add;
        _model.DidChangeContent += contentChangeListener;

        _ = RunRequestSafe();

        async Task RunRequestSafe()
        {
            try
            {
                await RunRequest().ConfigureAwait(true);
            }
            catch (Exception err)
            {
                UnexpectedErrors.Report(err);
            }
        }

        async Task RunRequest()
        {
            DocumentSemanticTokensResult? res;
            try
            {
                res = await GetSemanticTokens.GetDocumentSemanticTokens(_provider, _model, lastProvider, lastResultId, cancellationTokenSource.Token).ConfigureAwait(true);
            }
            catch (Exception err)
            {
                var isExpectedError = err is OperationCanceledException || err.Message.Contains("busy", StringComparison.Ordinal);
                if (!isExpectedError)
                {
                    UnexpectedErrors.Report(err);
                }

                // Semantic tokens eats up all errors and considers errors to mean that the result is temporarily not
                // available. The API does not have a special error kind to express this...
                if (ReferenceEquals(_currentDocumentRequestCancellationTokenSource, cancellationTokenSource))
                {
                    _currentDocumentRequestCancellationTokenSource = null;
                }

                _model.DidChangeContent -= contentChangeListener;

                if (!_isDisposed && (pendingChanges.Count > 0 || _providersChangedDuringRequest))
                {
                    // More changes occurred while the request was running
                    if (!_fetchDocumentSemanticTokens.IsScheduled())
                    {
                        _fetchDocumentSemanticTokens.Schedule(DebounceDelay);
                    }
                }

                return;
            }

            if (ReferenceEquals(_currentDocumentRequestCancellationTokenSource, cancellationTokenSource))
            {
                _currentDocumentRequestCancellationTokenSource = null;
            }

            _model.DidChangeContent -= contentChangeListener;

            if (res is null)
            {
                SetDocumentSemanticTokens(null, null, null, pendingChanges);
            }
            else
            {
                var styling = _stylingService.GetStyling(res.Provider, _model.Tokenization.LanguageIdCodec);
                SetDocumentSemanticTokens(res.Provider, res.Tokens, styling, pendingChanges);
            }
        }
    }

    private static void Copy(uint[] src, int srcOffset, uint[] dest, int destOffset, int length)
    {
        // protect against overflows
        length = Math.Min(length, Math.Min(dest.Length - destOffset, src.Length - srcOffset));
        for (var i = 0; i < length; i++)
        {
            dest[destOffset + i] = src[srcOffset + i];
        }
    }

    private void SetDocumentSemanticTokens(
        IDocumentSemanticTokensProvider? provider,
        SemanticTokensOrEdits? tokens,
        SemanticTokensProviderStyling? styling,
        List<ModelContentChangedEvent> pendingChanges)
    {
        var currentResponse = _currentDocumentResponse;
        void RescheduleIfNeeded()
        {
            if ((pendingChanges.Count > 0 || _providersChangedDuringRequest) && !_fetchDocumentSemanticTokens.IsScheduled())
            {
                _fetchDocumentSemanticTokens.Schedule(DebounceDelay);
            }
        }

        if (_currentDocumentResponse is not null)
        {
            _currentDocumentResponse.Dispose();
            _currentDocumentResponse = null;
        }

        if (_isDisposed)
        {
            // disposed!
            if (provider is not null && tokens is { } disposedTokens)
            {
                provider.ReleaseDocumentSemanticTokens(disposedTokens.Tokens?.ResultId ?? disposedTokens.Edits?.ResultId);
            }

            return;
        }

        if (provider is null || styling is null)
        {
            _model.Tokenization.SetSemanticTokens(null, false);
            return;
        }

        if (tokens is not { } tokensValue)
        {
            _model.Tokenization.SetSemanticTokens(null, true);
            RescheduleIfNeeded();
            return;
        }

        Languages.SemanticTokens? full = tokensValue.Tokens;
        if (full is null && tokensValue.Edits is { } edits)
        {
            var resultId = edits.ResultId;
            void RejectInvalidEdits(bool requestFullRefresh)
            {
                provider.ReleaseDocumentSemanticTokens(resultId);
                _model.Tokenization.SetSemanticTokens(null, true);
                if (requestFullRefresh)
                {
                    _fetchDocumentSemanticTokens.Schedule(0);
                }
            }

            if (currentResponse is null)
            {
                // not possible!
                RejectInvalidEdits(false);
                return;
            }

            if (edits.Edits.Count == 0)
            {
                // nothing to do!
                full = new Languages.SemanticTokens(currentResponse.Data) { ResultId = edits.ResultId };
            }
            else
            {
                long deltaLength = 0;
                foreach (var edit in edits.Edits)
                {
                    deltaLength += (edit.Data?.Length ?? 0) - (long)(uint)edit.DeleteCount;
                }

                var srcData = currentResponse.Data;
                var destDataLength = srcData.Length + deltaLength;
                if (destDataLength < 0)
                {
                    styling.WarnInvalidEditDeleteCount(currentResponse.ResultId, resultId, srcData.Length, deltaLength);
                    RejectInvalidEdits(true);
                    return;
                }

                var destData = new uint[destDataLength];
                var srcLastStart = srcData.Length;
                var destLastStart = destData.Length;
                for (var i = edits.Edits.Count - 1; i >= 0; i--)
                {
                    var edit = edits.Edits[i];
                    var start = (uint)edit.Start;
                    if (start > srcData.Length)
                    {
                        styling.WarnInvalidEditStart(currentResponse.ResultId, resultId, i, start, srcData.Length);
                        RejectInvalidEdits(true);
                        return;
                    }

                    var copyCount = srcLastStart - ((int)start + edit.DeleteCount);
                    if (copyCount > 0)
                    {
                        Copy(srcData, srcLastStart - copyCount, destData, destLastStart - copyCount, copyCount);
                        destLastStart -= copyCount;
                    }

                    if (edit.Data is { } data)
                    {
                        Copy(data, 0, destData, destLastStart - data.Length, data.Length);
                        destLastStart -= data.Length;
                    }

                    srcLastStart = (int)start;
                }

                if (srcLastStart > 0)
                {
                    Copy(srcData, 0, destData, 0, srcLastStart);
                }

                full = new Languages.SemanticTokens(destData) { ResultId = edits.ResultId };
            }
        }

        if (full is not null)
        {
            _currentDocumentResponse = new SemanticTokensResponse(provider, full.ResultId, full.Data);

            var result = SemanticTokensConversion.ToMultilineTokens2(full, styling, _model.GetLanguageId());

            // Adjust incoming semantic tokens
            if (pendingChanges.Count > 0)
            {
                // More changes occurred while the request was running. We need to:
                // 1. Adjust incoming semantic tokens
                // 2. Request them again
                foreach (var change in pendingChanges)
                {
                    foreach (var area in result)
                    {
                        foreach (var singleChange in change.Changes)
                        {
                            area.ApplyEdit(singleChange.Range, singleChange.Text);
                        }
                    }
                }
            }

            _model.Tokenization.SetSemanticTokens(result, true);
        }
        else
        {
            _model.Tokenization.SetSemanticTokens(null, true);
        }

        RescheduleIfNeeded();
    }

    private sealed class SemanticTokensResponse(IDocumentSemanticTokensProvider provider, string? resultId, uint[] data)
    {
        public IDocumentSemanticTokensProvider Provider { get; } = provider;

        public string? ResultId { get; } = resultId;

        public uint[] Data { get; } = data;

        public void Dispose() => Provider.ReleaseDocumentSemanticTokens(ResultId);
    }
}
