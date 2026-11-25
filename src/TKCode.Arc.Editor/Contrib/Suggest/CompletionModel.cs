// Ported from vscode (src/vs/editor/contrib/suggest/browser/completionModel.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - LineContext (an interface literal in upstream) is the record CompletionLineContext; the suggest model's own
//    LineContext class is SuggestLineContext.
//  - The options are SuggestOptions (filterGraceful) and SnippetSuggestions.
//  - stats.pLabelLen is upstream's quickSelect(length - .85): the integer part makes it the longest label's length.
//  - Sorting is stable (List.Sort is not; the item index breaks ties last, as upstream's comparator does).

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Contrib.Suggest;

/// <summary>Statistics of the filtered items (upstream's ICompletionStats).</summary>
internal sealed record CompletionStats(int PLabelLen);

/// <summary>The text before the cursor and how many characters were typed since the request (upstream's completionModel LineContext).</summary>
internal sealed record CompletionLineContext(string LeadingLineContent, int CharacterCountDelta);

/// <summary>The sorted, filtered completions of one request (upstream's CompletionModel).</summary>
internal sealed class CompletionModel
{
    private enum Refilter
    {
        Nothing = 0,
        All = 1,
        Incr = 2,
    }

    private readonly IReadOnlyList<SuggestCompletionItem> _items;
    private readonly int _column;
    private readonly WordDistance _wordDistance;
    private readonly SuggestOptions _options;
    private readonly Comparison<SuggestCompletionItem> _snippetCompareFn = CompareCompletionItems;
    private readonly FuzzyScoreOptions _fuzzyScoreOptions;

    private CompletionLineContext _lineContext;
    private Refilter _refilterKind;
    private List<SuggestCompletionItem>? _filteredItems;
    private Dictionary<ICompletionItemProvider, List<SuggestCompletionItem>>? _itemsByProvider;
    private CompletionStats? _stats;

    public CompletionModel(
        IReadOnlyList<SuggestCompletionItem> items,
        int column,
        CompletionLineContext lineContext,
        WordDistance wordDistance,
        SuggestOptions options,
        SnippetSuggestions snippetSuggestions,
        FuzzyScoreOptions? fuzzyScoreOptions = null,
        string? clipboardText = null)
    {
        _items = items;
        _column = column;
        _wordDistance = wordDistance;
        _options = options;
        _refilterKind = Refilter.All;
        _lineContext = lineContext;
        _fuzzyScoreOptions = fuzzyScoreOptions ?? FuzzyScoreOptions.Default;
        ClipboardText = clipboardText;

        if (snippetSuggestions == SnippetSuggestions.Top)
        {
            _snippetCompareFn = CompareCompletionItemsSnippetsUp;
        }
        else if (snippetSuggestions == SnippetSuggestions.Bottom)
        {
            _snippetCompareFn = CompareCompletionItemsSnippetsDown;
        }
    }

    public string? ClipboardText { get; }

    public CompletionLineContext LineContext
    {
        get => _lineContext;
        set
        {
            if (_lineContext.LeadingLineContent != value.LeadingLineContent
                || _lineContext.CharacterCountDelta != value.CharacterCountDelta)
            {
                _refilterKind = _lineContext.CharacterCountDelta < value.CharacterCountDelta && _filteredItems is not null ? Refilter.Incr : Refilter.All;
                _lineContext = value;
            }
        }
    }

    /// <summary>The filtered items, best first.</summary>
    public IReadOnlyList<SuggestCompletionItem> Items
    {
        get
        {
            EnsureCachedState();
            return _filteredItems!;
        }
    }

    /// <summary>All valid items by provider (also those filtered out).</summary>
    public IReadOnlyDictionary<ICompletionItemProvider, IReadOnlyList<SuggestCompletionItem>> GetItemsByProvider()
    {
        EnsureCachedState();
        return _itemsByProvider!.ToDictionary(p => p.Key, p => (IReadOnlyList<SuggestCompletionItem>)p.Value);
    }

    /// <summary>The providers whose lists are incomplete (typing more asks them again).</summary>
    public HashSet<ICompletionItemProvider> GetIncompleteProvider()
    {
        EnsureCachedState();
        var result = new HashSet<ICompletionItemProvider>();
        foreach (var (provider, items) in _itemsByProvider!)
        {
            if (items.Count > 0 && items[0].Container.Incomplete)
            {
                result.Add(provider);
            }
        }
        return result;
    }

    public CompletionStats Stats
    {
        get
        {
            EnsureCachedState();
            return _stats!;
        }
    }

    private void EnsureCachedState()
    {
        if (_refilterKind != Refilter.Nothing)
        {
            CreateCachedState();
        }
    }

    private void CreateCachedState()
    {
        _itemsByProvider = new Dictionary<ICompletionItemProvider, List<SuggestCompletionItem>>();

        var maxLabelLength = 0;
        var hasLabel = false;

        var leadingLineContent = _lineContext.LeadingLineContent;
        var characterCountDelta = _lineContext.CharacterCountDelta;
        var word = string.Empty;
        var wordLow = string.Empty;

        // incrementally filter less
        IReadOnlyList<SuggestCompletionItem> source = _refilterKind == Refilter.All ? _items : _filteredItems!;
        var target = new List<SuggestCompletionItem>();

        // picks a score function based on the number of items that we have to score/filter and based on the
        // user-configuration
        FuzzyScorer scoreFn = !_options.FilterGraceful || source.Count > 2000
            ? Filters.FuzzyScore
            : Filters.FuzzyScoreGracefulAggressive;

        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];

            if (item.IsInvalid)
            {
                continue; // SKIP invalid items
            }

            // keep all items by their provider
            if (_itemsByProvider.TryGetValue(item.Provider, out var arr))
            {
                arr.Add(item);
            }
            else
            {
                _itemsByProvider[item.Provider] = [item];
            }

            // 'word' is that remainder of the current line that we filter and score against. In theory each
            // suggestion uses a different word, but in practice not - that's why we cache
            var overwriteBefore = item.Position.Column - item.EditStart.Column;
            var wordLen = overwriteBefore + characterCountDelta - (item.Position.Column - _column);
            if (word.Length != wordLen)
            {
                word = wordLen <= 0 ? string.Empty : JsSliceFromEnd(leadingLineContent, wordLen);
                wordLow = word.ToLowerInvariant();
            }

            // remember the word against which this item was scored
            item.Word = word;

            if (wordLen == 0)
            {
                // when there is nothing to score against, don't even try to do. Use a const rank and rely on the
                // fallback-sort using the initial sort order. use a score of `-100` because that is out of the bound
                // of values `fuzzyScore` will return
                item.Score = FuzzyScore.Default;
            }
            else
            {
                // skip word characters that are whitespace until we have hit the replace range (overwriteBefore)
                var wordPos = 0;
                while (wordPos < overwriteBefore && wordPos < word.Length)
                {
                    var ch = word[wordPos];
                    if (ch is ' ' or '\t')
                    {
                        wordPos += 1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (wordPos >= wordLen)
                {
                    // the wordPos at which scoring starts is the whole word and therefore the same rules as not having
                    // a word apply
                    item.Score = FuzzyScore.Default;
                }
                else if (item.Completion.FilterText is { } filterText)
                {
                    // when there is a `filterText` it must match the `word`. if it matches we check with the label to
                    // compute highlights and if that doesn't yield a result we have no highlights, despite having the
                    // match
                    var match = scoreFn(word, wordLow, wordPos, filterText, item.FilterTextLow!, 0, _fuzzyScoreOptions);
                    if (match is null)
                    {
                        continue; // NO match
                    }
                    if (string.Compare(filterText, item.TextLabel, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // filterText and label are actually the same -> use good highlights
                        item.Score = match;
                    }
                    else
                    {
                        // re-run the scorer on the label in the hope of a result BUT use the rank of the filterText-match
                        item.Score = Filters.AnyScore(word, wordLow, wordPos, item.TextLabel, item.LabelLow, 0) with { Score = match.Score };
                    }
                }
                else
                {
                    // by default match `word` against the `label`
                    var match = scoreFn(word, wordLow, wordPos, item.TextLabel, item.LabelLow, 0, _fuzzyScoreOptions);
                    if (match is null)
                    {
                        continue; // NO match
                    }
                    item.Score = match;
                }
            }

            item.Idx = i;
            item.Distance = _wordDistance.Distance(item.Position, item.Completion);
            target.Add(item);

            // update stats
            hasLabel = true;
            maxLabelLength = Math.Max(maxLabelLength, item.TextLabel.Length);
        }

        target.Sort(_snippetCompareFn);
        _filteredItems = target;
        _refilterKind = Refilter.Nothing;
        _stats = new CompletionStats(hasLabel ? maxLabelLength : 0);
    }

    // JavaScript's leadingLineContent.slice(-wordLen)
    private static string JsSliceFromEnd(string text, int length) =>
        length >= text.Length ? text : text[^length..];

    private static int CompareCompletionItems(SuggestCompletionItem a, SuggestCompletionItem b)
    {
        if (a.Score.Score > b.Score.Score)
        {
            return -1;
        }
        if (a.Score.Score < b.Score.Score)
        {
            return 1;
        }
        if (a.Distance < b.Distance)
        {
            return -1;
        }
        if (a.Distance > b.Distance)
        {
            return 1;
        }
        return a.Idx.CompareTo(b.Idx);
    }

    private static int CompareCompletionItemsSnippetsDown(SuggestCompletionItem a, SuggestCompletionItem b)
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
        return CompareCompletionItems(a, b);
    }

    private static int CompareCompletionItemsSnippetsUp(SuggestCompletionItem a, SuggestCompletionItem b)
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
        return CompareCompletionItems(a, b);
    }
}
