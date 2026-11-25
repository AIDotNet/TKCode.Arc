// Ported from vscode (src/vs/editor/contrib/suggest/browser/suggest.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - CompletionItem (the suggest model's item) is SuggestCompletionItem: the name CompletionItem is the provider's item
//    (languages.CompletionItem, TKCode.Arc.Editor.Languages.CompletionItem).
//  - Provider items are immutable records: provideSuggestionItems does not fill in a missing range or sortText (the
//    record requires a range; the sort text used is SortText ?? the label), and resolving replaces Completion with the
//    resolved item instead of Object.assign.
//  - The snippet suggest support (getSnippetSuggestSupport / setSnippetSuggestSupport) is a module-level singleton
//    upstream; here it is CompletionOptions.SnippetSuggestSupport, passed per request.
//  - provideSuggestionItems throws OperationCanceledException when cancelled (upstream: CancellationError); provider
//    errors go to UnexpectedErrors. StopWatch durations are measured with Stopwatch.
//  - Context (SuggestContext) keys are in SuggestContext below; the `_executeCompletionItemProvider` command,
//    showSimpleSuggestions and the status bar menu id are not ported.

using System.Diagnostics;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>The context keys of the suggest feature (upstream's suggest.ts Context).</summary>
internal static class SuggestContext
{
    public static readonly RawContextKey<bool> HasFocusedSuggestion = new("suggestWidgetHasFocusedSuggestion", false, "Whether any suggestion is focused");
    public static readonly RawContextKey<bool> DetailsVisible = new("suggestWidgetDetailsVisible", false, "Whether suggestion details are visible");
    public static readonly RawContextKey<bool> DetailsFocused = new("suggestWidgetDetailsFocused", false, "Whether the details pane of the suggest widget has focus");
    public static readonly RawContextKey<bool> MultipleSuggestions = new("suggestWidgetMultipleSuggestions", false, "Whether there are multiple suggestions to pick from");
    public static readonly RawContextKey<bool> MakesTextEdit = new("suggestionMakesTextEdit", true, "Whether inserting the current suggestion yields in a change or has everything already been typed");
    public static readonly RawContextKey<bool> AcceptSuggestionsOnEnter = new("acceptSuggestionOnEnter", true, "Whether suggestions are inserted when pressing Enter");
    public static readonly RawContextKey<bool> HasInsertAndReplaceRange = new("suggestionHasInsertAndReplaceRange", false, "Whether the current suggestion has insert and replace behaviour");
    public static readonly RawContextKey<string> InsertMode = new("suggestionInsertMode", "insert", "Whether the default behaviour is to insert or replace");
    public static readonly RawContextKey<bool> CanResolve = new("suggestionCanResolve", false, "Whether the current suggestion supports to resolve further details");
}

/// <summary>An item of the suggest model: a provider's completion with its ranges, score and resolve state (upstream's suggest.ts CompletionItem).</summary>
internal sealed class SuggestCompletionItem
{
    private Task? _resolveCache;
    private double? _resolveDuration;

    public SuggestCompletionItem(TextPosition position, CompletionItem completion, CompletionList container, ICompletionItemProvider provider)
    {
        Position = position;
        Completion = completion;
        Container = container;
        Provider = provider;

        TextLabel = completion.Label.Label;

        // ensure lower-variants (perf)
        LabelLow = TextLabel.ToLowerInvariant();

        // validate label
        IsInvalid = string.IsNullOrEmpty(TextLabel);

        SortTextLow = completion.SortText?.ToLowerInvariant();
        FilterTextLow = completion.FilterText?.ToLowerInvariant();
        ExtensionId = completion.ExtensionId;

        // normalize ranges
        var insert = completion.Range.Insert;
        var replace = completion.Range.Replace;
        EditStart = insert.Start;
        EditInsertEnd = insert.End;
        EditReplaceEnd = replace.End;

        // validate ranges
        IsInvalid = IsInvalid
            || insert.StartLineNumber != insert.EndLineNumber || replace.StartLineNumber != replace.EndLineNumber
            || insert.StartLineNumber != position.LineNumber || replace.StartLineNumber != position.LineNumber
            || insert.StartColumn != replace.StartColumn;

        // create the suggestion resolver
        if (!provider.SupportsResolveCompletionItem)
        {
            _resolveCache = Task.CompletedTask;
            _resolveDuration = 0;
        }
    }

    /// <summary>Where completion was requested.</summary>
    public TextPosition Position { get; }

    /// <summary>The provider's item; replaced by the resolved item once resolved.</summary>
    public CompletionItem Completion { get; set; }

    public CompletionList Container { get; }

    public ICompletionItemProvider Provider { get; }

    public TextPosition EditStart { get; }

    public TextPosition EditInsertEnd { get; }

    public TextPosition EditReplaceEnd { get; }

    public string TextLabel { get; }

    public string LabelLow { get; }

    /// <summary>The lower-cased sort text, or null.</summary>
    public string? SortTextLow { get; }

    public string? FilterTextLow { get; }

    /// <summary>Whether the item is unusable (empty label, ranges that span lines or do not start on the request line).</summary>
    public bool IsInvalid { get; }

    public string? ExtensionId { get; }

    // sorting, filtering

    public FuzzyScore Score { get; set; } = FuzzyScore.Default;

    public int Distance { get; set; }

    public int Idx { get; set; }

    /// <summary>The word the item was scored against.</summary>
    public string? Word { get; set; }

    // resolving

    public bool IsResolved => _resolveDuration is not null;

    /// <summary>Milliseconds resolving took, or -1.</summary>
    public double ResolveDuration => _resolveDuration ?? -1;

    /// <summary>Resolves the item once (more details, documentation, additional edits).</summary>
    public Task Resolve(CancellationToken token)
    {
        if (_resolveCache is null)
        {
            var sw = Stopwatch.StartNew();
            _resolveCache = ResolveCore(sw, token);
        }
        return _resolveCache;
    }

    private async Task ResolveCore(Stopwatch sw, CancellationToken token)
    {
        try
        {
            var value = await Provider.ResolveCompletionItem(Completion, token);
            if (token.IsCancellationRequested)
            {
                // reset the cache, a later request resolves again
                _resolveCache = null;
                _resolveDuration = null;
                return;
            }
            if (value is not null)
            {
                Completion = value;
            }
            _resolveDuration = sw.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            _resolveCache = null;
            _resolveDuration = null;
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }
}

/// <summary>Where snippets sort (upstream's SnippetSortOrder).</summary>
internal enum SnippetSortOrder
{
    Top,
    Inline,
    Bottom,
}

/// <summary>What <see cref="SuggestProvider.ProvideSuggestionItems"/> asks for (upstream's CompletionOptions).</summary>
internal sealed record CompletionOptions
{
    public static readonly CompletionOptions Default = new();

    public SnippetSortOrder SnippetSortOrder { get; init; } = SnippetSortOrder.Bottom;

    /// <summary>Kinds left out.</summary>
    public IReadOnlySet<CompletionItemKind> KindFilter { get; init; } = new HashSet<CompletionItemKind>();

    /// <summary>When not empty, only these providers are asked.</summary>
    public IReadOnlySet<ICompletionItemProvider> ProviderFilter { get; init; } = new HashSet<ICompletionItemProvider>();

    /// <summary>Items of a previous request to reuse instead of asking their provider again.</summary>
    public IReadOnlyDictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>> ProviderItemsToReuse { get; init; } =
        new Dictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>>();

    public bool ShowDeprecated { get; init; } = true;

    /// <summary>The snippet provider asked in parallel to the others (upstream's module-level snippet suggest support).</summary>
    public ICompletionItemProvider? SnippetSuggestSupport { get; init; }
}

/// <summary>How long one provider took (upstream's CompletionDurationEntry).</summary>
internal sealed record CompletionDurationEntry(string ProviderName, double ElapsedProvider, double ElapsedOverall);

/// <summary>How long a request took (upstream's CompletionDurations).</summary>
internal sealed record CompletionDurations(IReadOnlyList<CompletionDurationEntry> Entries, double Elapsed);

/// <summary>The items of one request (upstream's CompletionItemModel).</summary>
internal sealed record CompletionItemModel(List<SuggestCompletionItem> Items, bool NeedsClipboard, CompletionDurations Durations, IDisposable Disposable);

/// <summary>Picks the item to select when the list shows (upstream's ISuggestItemPreselector).</summary>
internal interface ISuggestItemPreselector
{
    /// <summary>The preselector with the highest priority is asked first.</summary>
    int Priority { get; }

    /// <summary>The index to select, or -1 to ask the preselectors with a lower priority.</summary>
    int Select(TextModel model, TextPosition pos, IReadOnlyList<SuggestCompletionItem> items);
}

/// <summary>Asks the completion providers (upstream's provideSuggestionItems and the comparators of suggest.ts).</summary>
internal static class SuggestProvider
{
    public static async Task<CompletionItemModel> ProvideSuggestionItems(
        LanguageFeatureRegistry<ICompletionItemProvider> registry,
        TextModel model,
        TextPosition position,
        CompletionOptions? options = null,
        CompletionContext? context = null,
        CancellationToken token = default)
    {
        options ??= CompletionOptions.Default;
        context ??= new CompletionContext(CompletionTriggerKind.Invoke);
        var sw = Stopwatch.StartNew();

        var result = new List<SuggestCompletionItem>();
        var disposables = new List<Action>();
        var durations = new List<CompletionDurationEntry>();
        var needsClipboard = false;
        var gate = new object();

        bool OnCompletionList(ICompletionItemProvider provider, CompletionList? container, Stopwatch providerWatch)
        {
            var didAddResult = false;
            if (container is null)
            {
                return didAddResult;
            }

            lock (gate)
            {
                foreach (var suggestion in container.Suggestions)
                {
                    if (options.KindFilter.Contains(suggestion.Kind))
                    {
                        continue;
                    }

                    // skip if not showing deprecated suggestions
                    if (!options.ShowDeprecated && suggestion.Tags is { } tags && tags.Contains(CompletionItemTag.Deprecated))
                    {
                        continue;
                    }

                    if (!needsClipboard && (suggestion.InsertTextRules & CompletionItemInsertTextRule.InsertAsSnippet) != 0)
                    {
                        needsClipboard = GuessNeedsClipboard(suggestion.InsertText);
                    }

                    result.Add(new SuggestCompletionItem(position, suggestion, container, provider));
                    didAddResult = true;
                }

                if (container.Dispose is { } dispose)
                {
                    disposables.Add(dispose);
                }

                durations.Add(new CompletionDurationEntry(provider.DebugDisplayName, container.Duration?.TotalMilliseconds ?? -1, providerWatch.Elapsed.TotalMilliseconds));
            }
            return didAddResult;
        }

        // ask for snippets in parallel to asking "real" providers. Only do something if configured to do so - no
        // snippet filter, no special-providers-only request
        async Task SnippetCompletions()
        {
            var snippetSupport = options.SnippetSuggestSupport;
            if (snippetSupport is null || options.KindFilter.Contains(CompletionItemKind.Snippet))
            {
                return;
            }

            // we have items from a previous session that we can reuse
            if (options.ProviderItemsToReuse.TryGetValue(snippetSupport, out var reuseItems))
            {
                lock (gate)
                {
                    result.AddRange(reuseItems);
                }
                return;
            }

            if (options.ProviderFilter.Count > 0 && !options.ProviderFilter.Contains(snippetSupport))
            {
                return;
            }

            var watch = Stopwatch.StartNew();
            var list = await snippetSupport.ProvideCompletionItems(model, position, context, token);
            OnCompletionList(snippetSupport, list, watch);
        }

        var snippetCompletions = SnippetCompletions();

        // add suggestions from contributed providers - providers are ordered in groups of equal score and once a group
        // produces a result the process stops
        foreach (var providerGroup in registry.OrderedGroups(model))
        {
            // for each support in the group ask for suggestions
            var didAddResult = false;
            var tasks = new List<Task>();
            foreach (var provider in providerGroup)
            {
                tasks.Add(AskProvider(provider));
            }

            await Task.WhenAll(tasks);

            if (didAddResult || token.IsCancellationRequested)
            {
                break;
            }

            async Task AskProvider(ICompletionItemProvider provider)
            {
                // we have items from a previous session that we can reuse
                if (options.ProviderItemsToReuse.TryGetValue(provider, out var items))
                {
                    lock (gate)
                    {
                        result.AddRange(items);
                        didAddResult = didAddResult || items.Count > 0;
                    }
                    return;
                }

                // check if this provider is filtered out
                if (options.ProviderFilter.Count > 0 && !options.ProviderFilter.Contains(provider))
                {
                    return;
                }

                try
                {
                    var watch = Stopwatch.StartNew();
                    var list = await provider.ProvideCompletionItems(model, position, context, token);
                    var added = OnCompletionList(provider, list, watch);
                    lock (gate)
                    {
                        didAddResult = added || didAddResult;
                    }
                }
                catch (OperationCanceledException)
                {
                    // the request was cancelled
                }
                catch (Exception e)
                {
                    UnexpectedErrors.Report(e);
                }
            }
        }

        try
        {
            await snippetCompletions;
        }
        catch (OperationCanceledException)
        {
            // the request was cancelled
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }

        var disposable = new ActionDisposable(disposables);
        if (token.IsCancellationRequested)
        {
            disposable.Dispose();
            throw new OperationCanceledException(token);
        }

        // a stable sort, like Array.prototype.sort
        var comparator = GetSuggestionComparator(options.SnippetSortOrder);
        var sorted = result.Select((item, index) => (item, index)).ToList();
        sorted.Sort((a, b) =>
        {
            var c = comparator(a.item, b.item);
            return c != 0 ? c : a.index.CompareTo(b.index);
        });

        return new CompletionItemModel(
            sorted.Select(x => x.item).ToList(),
            needsClipboard,
            new CompletionDurations(durations, sw.Elapsed.TotalMilliseconds),
            disposable);
    }

    /// <summary>Whether a snippet uses the clipboard variable (upstream's SnippetParser.guessNeedsClipboard).</summary>
    public static bool GuessNeedsClipboard(string template) =>
        template.Contains("$CLIPBOARD", StringComparison.Ordinal) || template.Contains("${CLIPBOARD", StringComparison.Ordinal);

    private static int DefaultComparator(SuggestCompletionItem a, SuggestCompletionItem b)
    {
        // check with 'sortText'
        var aSort = a.SortTextLow ?? a.LabelLow;
        var bSort = b.SortTextLow ?? b.LabelLow;
        var cmp = string.CompareOrdinal(aSort, bSort);
        if (cmp != 0)
        {
            return cmp < 0 ? -1 : 1;
        }

        // check with 'label'
        cmp = string.CompareOrdinal(a.TextLabel, b.TextLabel);
        if (cmp != 0)
        {
            return cmp < 0 ? -1 : 1;
        }

        // check with 'type'
        return a.Completion.Kind - b.Completion.Kind;
    }

    private static int SnippetUpComparator(SuggestCompletionItem a, SuggestCompletionItem b)
    {
        if (a.Completion.Kind != b.Completion.Kind)
        {
            if (a.Completion.Kind == CompletionItemKind.Snippet)
            {
                return -1;
            }
            if (b.Completion.Kind == CompletionItemKind.Snippet)
            {
                return 1;
            }
        }
        return DefaultComparator(a, b);
    }

    private static int SnippetDownComparator(SuggestCompletionItem a, SuggestCompletionItem b)
    {
        if (a.Completion.Kind != b.Completion.Kind)
        {
            if (a.Completion.Kind == CompletionItemKind.Snippet)
            {
                return 1;
            }
            if (b.Completion.Kind == CompletionItemKind.Snippet)
            {
                return -1;
            }
        }
        return DefaultComparator(a, b);
    }

    /// <summary>The comparator of a snippet sort order (upstream's getSuggestionComparator).</summary>
    public static Comparison<SuggestCompletionItem> GetSuggestionComparator(SnippetSortOrder snippetConfig) => snippetConfig switch
    {
        SnippetSortOrder.Top => SnippetUpComparator,
        SnippetSortOrder.Bottom => SnippetDownComparator,
        _ => DefaultComparator,
    };

    private sealed class ActionDisposable(List<Action> actions) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            foreach (var action in actions)
            {
                action();
            }
        }
    }
}
