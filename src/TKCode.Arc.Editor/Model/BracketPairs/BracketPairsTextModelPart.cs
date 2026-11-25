// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsImpl.ts, commit b57d5c9e), MIT
// license. This part: the tree and the queries on it; the search without a tree (models over 5,000,000 characters) is
// in BracketPairsTextModelPart.Legacy.cs.
//
// Differences: the change event is an Emitter; maxDuration is a TimeSpan; a matched pair is a tuple; the language
// configuration service is the model's (the model forwards its change event here).

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>The bracket pairs of a model (upstream's IBracketPairsTextModelPart).</summary>
internal sealed partial class BracketPairsTextModelPart : IDisposable
{
    private readonly TextModel _textModel;
    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly Emitter<bool> _onDidChangeEmitter = new();
    private BracketPairsTree? _bracketPairsTree;
    private bool _bracketsRequested;

    public BracketPairsTextModelPart(TextModel textModel, LanguageConfigurationService languageConfigurationService)
    {
        _textModel = textModel;
        _languageConfigurationService = languageConfigurationService;
    }

    /// <summary>Is fired when bracket pairs change, either due to a text or a settings change.</summary>
    public event Action DidChange
    {
        add => _onDidChangeEmitter.Add(_ => value(), value);
        remove => _onDidChangeEmitter.Remove(value);
    }

    private bool CanBuildAST
    {
        get
        {
            const int maxSupportedDocumentLength = /* max lines */ 50_000 * /* average column count */ 100;
            return _textModel.GetValueLength() <= maxSupportedDocumentLength;
        }
    }

    public void Dispose()
    {
        ClearTree();
        _onDidChangeEmitter.Clear();
    }

    // ---- TextModel events

    public void HandleLanguageConfigurationServiceChange(LanguageConfigurationServiceChangeEvent e)
    {
        if (e.LanguageId is null || (_bracketPairsTree?.DidLanguageChange(e.LanguageId) ?? false))
        {
            ClearTree();
            UpdateBracketPairsTree();
        }
    }

    public void HandleDidChangeOptions()
    {
        ClearTree();
        UpdateBracketPairsTree();
    }

    public void HandleDidChangeLanguage()
    {
        ClearTree();
        UpdateBracketPairsTree();
    }

    public void HandleDidChangeContent(ModelContentChangedEvent change) => _bracketPairsTree?.HandleContentChanged(change);

    public void HandleDidChangeBackgroundTokenizationState() => _bracketPairsTree?.HandleDidChangeBackgroundTokenizationState();

    public void HandleDidChangeTokens(ModelTokensChangedEvent e) => _bracketPairsTree?.HandleDidChangeTokens(e);

    // ----

    private void ClearTree()
    {
        if (_bracketPairsTree is not null)
        {
            _bracketPairsTree.DidChange -= FireDidChange;
            _bracketPairsTree.Dispose();
            _bracketPairsTree = null;
        }
    }

    private void FireDidChange() => _onDidChangeEmitter.Fire(true);

    private void UpdateBracketPairsTree()
    {
        if (_bracketsRequested && CanBuildAST)
        {
            if (_bracketPairsTree is null)
            {
                _bracketPairsTree = new BracketPairsTree(_textModel, languageId => _languageConfigurationService.GetLanguageConfiguration(languageId));
                _bracketPairsTree.DidChange += FireDidChange;
                FireDidChange();
            }
        }
        else if (_bracketPairsTree is not null)
        {
            ClearTree();
            // Important: Don't call fire if there was no change!
            FireDidChange();
        }
    }

    /// <summary>Returns all bracket pairs that intersect the given range. The result is sorted by the start position.</summary>
    public CallbackIterable<BracketPairInfo> GetBracketPairsInRange(TextRange range)
    {
        _bracketsRequested = true;
        UpdateBracketPairsTree();
        return _bracketPairsTree?.GetBracketPairsInRange(range, false).Map(static p => (BracketPairInfo)p) ?? CallbackIterable<BracketPairInfo>.Empty;
    }

    public CallbackIterable<BracketPairWithMinIndentationInfo> GetBracketPairsInRangeWithMinIndentation(TextRange range)
    {
        _bracketsRequested = true;
        UpdateBracketPairsTree();
        return _bracketPairsTree?.GetBracketPairsInRange(range, true) ?? CallbackIterable<BracketPairWithMinIndentationInfo>.Empty;
    }

    public CallbackIterable<BracketInfo> GetBracketsInRange(TextRange range, bool onlyColorizedBrackets = false)
    {
        _bracketsRequested = true;
        UpdateBracketPairsTree();
        return _bracketPairsTree?.GetBracketsInRange(range, onlyColorizedBrackets) ?? CallbackIterable<BracketInfo>.Empty;
    }

    /// <summary>
    /// Returns whether an unmatched closing bracket for <paramref name="openingBracket"/> starts at or after
    /// <paramref name="rawPosition"/>. Returns false when token-aware bracket data is unavailable.
    /// </summary>
    public bool HasUnmatchedClosingBracketAfter(TextPosition rawPosition, string openingBracket)
    {
        var position = _textModel.ValidatePosition(rawPosition);
        var languageId = _textModel.GetLanguageIdAtPosition(position.LineNumber, position.Column);
        var openingBracketInfo = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew.GetOpeningBracketInfo(openingBracket);
        if (openingBracketInfo is null)
        {
            return false;
        }

        _bracketsRequested = true;
        UpdateBracketPairsTree();
        return _bracketPairsTree?.HasUnmatchedClosingBracketAfter(position, openingBracketInfo) ?? false;
    }

    /// <summary>Find the matching bracket of <paramref name="bracket"/> up, counting brackets; null when there is none.</summary>
    public TextRange? FindMatchingBracketUp(string bracket, TextPosition rawPosition, TimeSpan? maxDuration = null)
    {
        var position = _textModel.ValidatePosition(rawPosition);
        var languageId = _textModel.GetLanguageIdAtPosition(position.LineNumber, position.Column);

        if (CanBuildAST)
        {
            var closingBracketInfo = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew.GetClosingBracketInfo(bracket);

            if (closingBracketInfo is null)
            {
                return null;
            }

            var bracketPair = GetBracketPairsInRange(new TextRange(rawPosition.LineNumber, rawPosition.Column, rawPosition.LineNumber, rawPosition.Column))
                .FindLast(b => closingBracketInfo.Closes(b.OpeningBracketInfo));

            return bracketPair?.OpeningBracketRange;
        }

        // Fallback to old bracket matching code:
        var lowerBracket = bracket.ToLowerInvariant();

        var bracketsSupport = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
        if (bracketsSupport is null || !bracketsSupport.TextIsBracket.TryGetValue(lowerBracket, out var data))
        {
            return null;
        }

        return StripBracketSearchCanceled(FindMatchingBracketUpLegacy(data, position, CreateTimeBasedContinueBracketSearchPredicate(maxDuration)));
    }

    /// <summary>
    /// Given a position, if the position is on top or near a bracket, find the matching bracket of that bracket and
    /// return the ranges of both brackets.
    /// </summary>
    public (TextRange Open, TextRange Close)? MatchBracket(TextPosition position, TimeSpan? maxDuration = null)
    {
        if (CanBuildAST)
        {
            var bracketPair = GetBracketPairsInRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column))
                .Filter(item => item.ClosingBracketRange is { } closing
                    && (item.OpeningBracketRange.ContainsPosition(position) || closing.ContainsPosition(position)))
                .FindLastMaxBy((a, b) => TextRange.CompareRangesUsingStarts(
                    a.OpeningBracketRange.ContainsPosition(position) ? a.OpeningBracketRange : a.ClosingBracketRange!.Value,
                    b.OpeningBracketRange.ContainsPosition(position) ? b.OpeningBracketRange : b.ClosingBracketRange!.Value));
            if (bracketPair is not null)
            {
                return (bracketPair.OpeningBracketRange, bracketPair.ClosingBracketRange!.Value);
            }

            return null;
        }

        // Fallback to old bracket matching code:
        var continueSearchPredicate = CreateTimeBasedContinueBracketSearchPredicate(maxDuration);
        return MatchBracketLegacy(_textModel.ValidatePosition(position), continueSearchPredicate);
    }

    /// <summary>Find the first bracket in the model before <paramref name="rawPosition"/>; null if there are none.</summary>
    public FoundBracket? FindPrevBracket(TextPosition rawPosition)
    {
        var position = _textModel.ValidatePosition(rawPosition);

        if (CanBuildAST)
        {
            _bracketsRequested = true;
            UpdateBracketPairsTree();
            return _bracketPairsTree?.GetFirstBracketBefore(position);
        }

        return FindPrevBracketLegacy(position);
    }

    /// <summary>Find the first bracket in the model after <paramref name="rawPosition"/>; null if there are none.</summary>
    public FoundBracket? FindNextBracket(TextPosition rawPosition)
    {
        var position = _textModel.ValidatePosition(rawPosition);

        if (CanBuildAST)
        {
            _bracketsRequested = true;
            UpdateBracketPairsTree();
            return _bracketPairsTree?.GetFirstBracketAfter(position);
        }

        return FindNextBracketLegacy(position);
    }

    /// <summary>Find the enclosing brackets that contain <paramref name="rawPosition"/>.</summary>
    public (TextRange Open, TextRange Close)? FindEnclosingBrackets(TextPosition rawPosition, TimeSpan? maxDuration = null)
    {
        var position = _textModel.ValidatePosition(rawPosition);

        if (CanBuildAST)
        {
            var range = new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column);
            var bracketPair = GetBracketPairsInRange(range)
                .FindLast(item => item.ClosingBracketRange is not null && item.Range.StrictContainsRange(range));
            if (bracketPair is not null)
            {
                return (bracketPair.OpeningBracketRange, bracketPair.ClosingBracketRange!.Value);
            }

            return null;
        }

        return FindEnclosingBracketsLegacy(position, CreateTimeBasedContinueBracketSearchPredicate(maxDuration));
    }

    private FoundBracket? ToFoundBracket(LanguageBracketsConfiguration bracketConfig, TextRange r)
    {
        var text = _textModel.GetValueInRange(r).ToLowerInvariant();
        var bracketInfo = bracketConfig.GetBracketInfo(text);
        return bracketInfo is null ? null : new FoundBracket(r, bracketInfo);
    }

    private static Func<bool> CreateTimeBasedContinueBracketSearchPredicate(TimeSpan? maxDuration)
    {
        if (maxDuration is not { } max)
        {
            return static () => true;
        }

        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        return () => System.Diagnostics.Stopwatch.GetElapsedTime(start) <= max;
    }

    /// <summary>The legacy searches' "canceled" result (upstream's BracketSearchCanceled).</summary>
    private readonly record struct SearchResult<T>(T? Value, bool Canceled)
        where T : struct
    {
        public static SearchResult<T> Cancel => new(null, true);

        public static SearchResult<T> Of(T? value) => new(value, false);
    }

    private static T? StripBracketSearchCanceled<T>(SearchResult<T> result)
        where T : struct => result.Canceled ? null : result.Value;
}
