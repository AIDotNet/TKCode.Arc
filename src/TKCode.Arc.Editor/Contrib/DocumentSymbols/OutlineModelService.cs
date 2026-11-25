// Ported from vscode (src/vs/editor/contrib/documentSymbols/browser/outlineModel.ts: IOutlineModelService,
// OutlineModelService; commit b57d5c9e), MIT license.
// Differences:
//  - one service per editor (upstream: a singleton), on that editor's language features; the cache keeps at most 15
//    models (upstream: an LRU of 15) and forgets a model when it is disposed (upstream: on onModelRemoved);
//  - the debounce information is a fixed minimum of 350 ms (upstream's languageFeatureDebounceService learns it from
//    how long the providers take, starting at that minimum).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.DocumentSymbols;

/// <summary>Builds and caches the outline of documents (upstream's OutlineModelService).</summary>
internal sealed class OutlineModelService
{
    private const int CacheSize = 15;

    /// <summary>The delay before the outline is computed again after an edit (upstream's debounce minimum).</summary>
    public const int MinDebounceMs = 350;

    private readonly LanguageFeatureRegistry<IDocumentSymbolProvider> _registry;
    private readonly List<(TextModel Model, CacheEntry Entry)> _cache = [];

    public OutlineModelService(LanguageFeatures languageFeatures)
    {
        _registry = languageFeatures.DocumentSymbolProvider;
    }

    /// <summary>
    /// The outline of <paramref name="textModel"/>: the cached one when the version and the providers are the same,
    /// else a new computation shared by every caller until it ends. Cancelling the last caller's token cancels the
    /// providers and drops the entry (upstream's getOrCreate).
    /// </summary>
    public async Task<OutlineModel> GetOrCreate(TextModel textModel, CancellationToken token)
    {
        var provider = _registry.Ordered(textModel);
        var data = Find(textModel);
        if (data is null || data.VersionId != textModel.GetVersionId() || !data.Provider.SequenceEqual(provider))
        {
            var source = new CancellationTokenSource();
            var entry = new CacheEntry(textModel.GetVersionId(), provider, source, OutlineModel.Create(_registry, textModel, source.Token));
            Set(textModel, entry);
            data = entry;
            _ = Observe(textModel, entry);
        }

        if (data.Model is { } model)
        {
            // resolved -> return data
            return model;
        }

        // increase usage counter
        data.PromiseCnt += 1;
        var current = data;
        using var listener = token.Register(() =>
        {
            // last -> cancel provider request, remove cached promise
            if (--current.PromiseCnt == 0)
            {
                current.Source.Cancel();
                Remove(textModel, current);
            }
        });

        return await current.Promise.ConfigureAwait(true);
    }

    /// <summary>The outlines that are computed (upstream's getCachedModels).</summary>
    public IEnumerable<OutlineModel> GetCachedModels() => _cache.Select(static c => c.Entry.Model).OfType<OutlineModel>();

    /// <summary>How long to wait after an edit before asking again (upstream's getDebounceValue).</summary>
    public int GetDebounceValue(TextModel textModel) => MinDebounceMs;

    private async Task Observe(TextModel textModel, CacheEntry entry)
    {
        try
        {
            entry.Model = await entry.Promise.ConfigureAwait(true);
        }
        catch (Exception)
        {
            Remove(textModel, entry);
        }
    }

    private CacheEntry? Find(TextModel textModel)
    {
        _cache.RemoveAll(static c => c.Model.IsDisposed());
        foreach (var (model, entry) in _cache)
        {
            if (ReferenceEquals(model, textModel))
            {
                return entry;
            }
        }

        return null;
    }

    private void Set(TextModel textModel, CacheEntry entry)
    {
        _cache.RemoveAll(c => ReferenceEquals(c.Model, textModel));
        _cache.Add((textModel, entry));
        if (_cache.Count > CacheSize)
        {
            _cache.RemoveAt(0);
        }
    }

    private void Remove(TextModel textModel, CacheEntry entry) =>
        _cache.RemoveAll(c => ReferenceEquals(c.Model, textModel) && ReferenceEquals(c.Entry, entry));

    private sealed class CacheEntry(int versionId, IReadOnlyList<IDocumentSymbolProvider> provider, CancellationTokenSource source, Task<OutlineModel> promise)
    {
        public int VersionId { get; } = versionId;

        public IReadOnlyList<IDocumentSymbolProvider> Provider { get; } = provider;

        public int PromiseCnt { get; set; }

        public CancellationTokenSource Source { get; } = source;

        public Task<OutlineModel> Promise { get; } = promise;

        public OutlineModel? Model { get; set; }
    }
}
