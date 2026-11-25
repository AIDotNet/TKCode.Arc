// Ported from vscode (src/vs/editor/common/languageFeatureRegistry.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the model is the TextModel (upstream's ITextModel); shouldSynchronizeModel(model) is !model.IsTooLargeForSyncing();
//  - onDidChange is the .NET event DidChange (delivered through the ported Emitter, so nested registrations keep order);
//  - registeredLanguageIds returns a new set each time, as upstream does;
//  - the registry is not thread-safe: register and query on the thread that owns the editor, as upstream does.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Finds the notebook a document belongs to (upstream's NotebookInfoResolver).</summary>
internal delegate NotebookInfo? NotebookInfoResolver(Uri uri);

/// <summary>
/// The providers of one language feature (hover, completion, ...), each with the documents it applies to. Queries order
/// them by how well their selector matches a model (<see cref="LanguageSelector.Score"/>), then the non-builtin ones
/// first, then the most recently registered first; an exclusive selector that matches shadows every other provider.
/// </summary>
/// <typeparam name="T">The provider type.</typeparam>
internal sealed class LanguageFeatureRegistry<T>
    where T : class
{
    private readonly NotebookInfoResolver? _notebookInfoResolver;
    private readonly List<Entry> _entries = new();
    private readonly Emitter<int> _onDidChange = new();
    private int _clock;
    private MatchCandidate? _lastCandidate;

    public LanguageFeatureRegistry(NotebookInfoResolver? notebookInfoResolver = null)
    {
        _notebookInfoResolver = notebookInfoResolver;
    }

    /// <summary>Raised with the number of providers after one was registered or removed.</summary>
    public event Action<int>? DidChange
    {
        add => _onDidChange.Add(value!);
        remove => _onDidChange.Remove(value);
    }

    /// <summary>Registers <paramref name="provider"/> for the documents <paramref name="selector"/> selects; dispose to remove it.</summary>
    public IDisposable Register(LanguageSelector selector, T provider)
    {
        var entry = new Entry(selector, provider, _clock++);
        _entries.Add(entry);
        _lastCandidate = null;
        _onDidChange.Fire(_entries.Count);

        return RegistrationDisposable.ToDisposable(() =>
        {
            var idx = _entries.IndexOf(entry);
            if (idx >= 0)
            {
                _entries.RemoveAt(idx);
                _lastCandidate = null;
                _onDidChange.Fire(_entries.Count);
            }
        });
    }

    /// <summary>Whether some provider applies to <paramref name="model"/>.</summary>
    public bool Has(TextModel model) => All(model).Count > 0;

    /// <summary>Checks potential provider availability before a text model has been resolved.</summary>
    public bool HasForResource(Uri uri, string languageId)
    {
        var notebook = _notebookInfoResolver?.Invoke(uri);
        return _entries.Any(entry => LanguageSelector.Score(entry.Selector, uri, languageId, true, notebook?.Uri, notebook?.Type) > 0);
    }

    /// <summary>The providers that apply to <paramref name="model"/>, best first.</summary>
    public IReadOnlyList<T> All(TextModel? model)
    {
        if (model is null)
        {
            return [];
        }

        UpdateScores(model, false);
        var result = new List<T>();

        // from registry
        foreach (var entry in _entries)
        {
            if (entry.Score > 0)
            {
                result.Add(entry.Provider);
            }
        }

        return result;
    }

    /// <summary>Every provider, whatever its selector.</summary>
    public IReadOnlyList<T> AllNoModel() => [.. _entries.Select(entry => entry.Provider)];

    /// <summary>The language ids the selectors name.</summary>
    public IReadOnlySet<string> RegisteredLanguageIds
    {
        get
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                LanguageSelector.SelectLanguageIds(entry.Selector, result);
            }

            return result;
        }
    }

    /// <summary>
    /// The providers that apply to <paramref name="model"/>, best first. <paramref name="recursive"/> (a provider asking
    /// the registry from inside its own call) leaves out the exclusive ones.
    /// </summary>
    public IReadOnlyList<T> Ordered(TextModel model, bool recursive = false)
    {
        var result = new List<T>();
        OrderedForEach(model, recursive, entry => result.Add(entry.Provider));
        return result;
    }

    /// <summary>The providers that apply to <paramref name="model"/>, grouped by equal score, best group first.</summary>
    public IReadOnlyList<IReadOnlyList<T>> OrderedGroups(TextModel model)
    {
        var result = new List<IReadOnlyList<T>>();
        List<T>? lastBucket = null;
        var lastBucketScore = 0;

        OrderedForEach(model, false, entry =>
        {
            if (lastBucket is not null && lastBucketScore == entry.Score)
            {
                lastBucket.Add(entry.Provider);
            }
            else
            {
                lastBucketScore = entry.Score;
                lastBucket = [entry.Provider];
                result.Add(lastBucket);
            }
        });

        return result;
    }

    private void OrderedForEach(TextModel model, bool recursive, Action<Entry> callback)
    {
        UpdateScores(model, recursive);

        foreach (var entry in _entries)
        {
            if (entry.Score > 0)
            {
                callback(entry);
            }
        }
    }

    private void UpdateScores(TextModel model, bool recursive)
    {
        var notebookInfo = _notebookInfoResolver?.Invoke(model.Uri);

        // use the uri (scheme, pattern) of the notebook info iff we have one
        // otherwise it's the model's/document's uri
        var candidate = notebookInfo is not null
            ? new MatchCandidate(model.Uri, model.GetLanguageId(), notebookInfo.Uri, notebookInfo.Type, recursive, !model.IsTooLargeForSyncing())
            : new MatchCandidate(model.Uri, model.GetLanguageId(), null, null, recursive, !model.IsTooLargeForSyncing());

        if (_lastCandidate is not null && _lastCandidate.Equals(candidate))
        {
            // nothing has changed
            return;
        }

        _lastCandidate = candidate;

        foreach (var entry in _entries)
        {
            entry.Score = LanguageSelector.Score(entry.Selector, candidate.Uri, candidate.LanguageId, candidate.IsSynchronized, candidate.NotebookUri, candidate.NotebookType);

            if (LanguageSelector.IsExclusive(entry.Selector) && entry.Score > 0)
            {
                if (recursive)
                {
                    entry.Score = 0;
                }
                else
                {
                    // support for one exclusive selector that overwrites any other selector
                    foreach (var other in _entries)
                    {
                        other.Score = 0;
                    }

                    entry.Score = 1000;
                    break;
                }
            }
        }

        // needs sorting (stable, as Array.prototype.sort is)
        var sorted = _entries.OrderBy(static e => e, EntryComparer.Instance).ToList();
        _entries.Clear();
        _entries.AddRange(sorted);
    }

    private sealed class Entry(LanguageSelector selector, T provider, int time)
    {
        public LanguageSelector Selector { get; } = selector;

        public T Provider { get; } = provider;

        public int Score { get; set; } = -1;

        public int Time { get; } = time;
    }

    private sealed record MatchCandidate(Uri Uri, string LanguageId, Uri? NotebookUri, string? NotebookType, bool Recursive, bool IsSynchronized)
    {
        public bool Equals(MatchCandidate? other) =>
            other is not null
            && NotebookType == other.NotebookType
            && LanguageId == other.LanguageId
            && Uri.ToString() == other.Uri.ToString()
            && NotebookUri?.ToString() == other.NotebookUri?.ToString()
            && Recursive == other.Recursive
            && IsSynchronized == other.IsSynchronized;

        public override int GetHashCode() => HashCode.Combine(LanguageId, Uri.ToString(), Recursive);
    }

    // upstream's _compareByScoreAndTime
    private sealed class EntryComparer : IComparer<Entry>
    {
        public static readonly EntryComparer Instance = new();

        public int Compare(Entry? a, Entry? b)
        {
            if (a!.Score < b!.Score)
            {
                return 1;
            }

            if (a.Score > b.Score)
            {
                return -1;
            }

            // De-prioritize built-in providers
            var aBuiltin = LanguageSelector.IsBuiltinSelector(a.Selector);
            var bBuiltin = LanguageSelector.IsBuiltinSelector(b.Selector);
            if (aBuiltin && !bBuiltin)
            {
                return 1;
            }

            if (!aBuiltin && bBuiltin)
            {
                return -1;
            }

            if (a.Time < b.Time)
            {
                return 1;
            }

            return a.Time > b.Time ? -1 : 0;
        }
    }
}
