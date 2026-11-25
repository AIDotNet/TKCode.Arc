// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsImpl.ts, commit b57d5c9e), MIT
// license. This part: the bracket search without a tree, which upstream keeps for models too large to parse
// (over 5,000,000 characters): _establishBracketSearchOffsets, _matchBracket, _matchFoundBracket,
// _findMatchingBracketUp, _findMatchingBracketDown and the fallbacks of findPrevBracket, findNextBracket and
// findEnclosingBrackets.

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Model.BracketPairs;

internal sealed partial class BracketPairsTextModelPart
{
    private (int SearchStartOffset, int SearchEndOffset) EstablishBracketSearchOffsets(TextPosition position, LineTokens lineTokens, RichEditBrackets modeBrackets, int tokenIndex)
    {
        var tokenCount = lineTokens.GetCount();
        var currentLanguageId = lineTokens.GetLanguageId(tokenIndex);

        // limit search to not go before `maxBracketLength`
        var searchStartOffset = Math.Max(0, position.Column - 1 - modeBrackets.MaxBracketLength);
        for (var i = tokenIndex - 1; i >= 0; i--)
        {
            var tokenEndOffset = lineTokens.GetEndOffset(i);
            if (tokenEndOffset <= searchStartOffset)
            {
                break;
            }

            if (Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(i)) || lineTokens.GetLanguageId(i) != currentLanguageId)
            {
                searchStartOffset = tokenEndOffset;
                break;
            }
        }

        // limit search to not go after `maxBracketLength`
        var searchEndOffset = Math.Min(lineTokens.GetLineContent().Length, position.Column - 1 + modeBrackets.MaxBracketLength);
        for (var i = tokenIndex + 1; i < tokenCount; i++)
        {
            var tokenStartOffset = lineTokens.GetStartOffset(i);
            if (tokenStartOffset >= searchEndOffset)
            {
                break;
            }

            if (Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(i)) || lineTokens.GetLanguageId(i) != currentLanguageId)
            {
                searchEndOffset = tokenStartOffset;
                break;
            }
        }

        return (searchStartOffset, searchEndOffset);
    }

    private (TextRange Open, TextRange Close)? MatchBracketLegacy(TextPosition position, Func<bool> continueSearchPredicate)
    {
        var lineNumber = position.LineNumber;
        var lineTokens = _textModel.Tokenization.GetLineTokens(lineNumber);
        var lineText = _textModel.GetLineContent(lineNumber);

        var tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
        if (tokenIndex < 0)
        {
            return null;
        }

        var currentModeBrackets = _languageConfigurationService.GetLanguageConfiguration(lineTokens.GetLanguageId(tokenIndex)).Brackets;

        // check that the token is not to be ignored
        if (currentModeBrackets is not null && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(tokenIndex)))
        {
            var (searchStartOffset, searchEndOffset) = EstablishBracketSearchOffsets(position, lineTokens, currentModeBrackets, tokenIndex);

            // it might be the case that [currentTokenStart -> currentTokenEnd] contains multiple brackets
            // `bestResult` will contain the most right-side result
            (TextRange, TextRange)? bestResult = null;
            while (true)
            {
                var foundBracket = BracketsUtils.FindNextBracketInRange(currentModeBrackets.ForwardRegex, lineNumber, lineText, searchStartOffset, searchEndOffset);
                if (foundBracket is not { } found)
                {
                    // there are no more brackets in this text
                    break;
                }

                // check that we didn't hit a bracket too far away from position
                if (found.StartColumn <= position.Column && position.Column <= found.EndColumn)
                {
                    var foundBracketText = lineText[(found.StartColumn - 1)..(found.EndColumn - 1)].ToLowerInvariant();
                    var r = MatchFoundBracket(
                        found,
                        currentModeBrackets.TextIsBracket.GetValueOrDefault(foundBracketText),
                        currentModeBrackets.TextIsOpenBracket.GetValueOrDefault(foundBracketText),
                        continueSearchPredicate);
                    if (r.Canceled)
                    {
                        return null;
                    }

                    if (r.Value is { } value)
                    {
                        bestResult = value;
                    }
                }

                searchStartOffset = found.EndColumn - 1;
            }

            if (bestResult is not null)
            {
                return bestResult;
            }
        }

        // If position is in between two tokens, try also looking in the previous token
        if (tokenIndex > 0 && lineTokens.GetStartOffset(tokenIndex) == position.Column - 1)
        {
            var prevTokenIndex = tokenIndex - 1;
            var prevModeBrackets = _languageConfigurationService.GetLanguageConfiguration(lineTokens.GetLanguageId(prevTokenIndex)).Brackets;

            // check that previous token is not to be ignored
            if (prevModeBrackets is not null && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(prevTokenIndex)))
            {
                var (searchStartOffset, searchEndOffset) = EstablishBracketSearchOffsets(position, lineTokens, prevModeBrackets, prevTokenIndex);

                var foundBracket = BracketsUtils.FindPrevBracketInRange(prevModeBrackets.ReversedRegex, lineNumber, lineText, searchStartOffset, searchEndOffset);

                // check that we didn't hit a bracket too far away from position
                if (foundBracket is { } found && found.StartColumn <= position.Column && position.Column <= found.EndColumn)
                {
                    var foundBracketText = lineText[(found.StartColumn - 1)..(found.EndColumn - 1)].ToLowerInvariant();
                    var r = MatchFoundBracket(
                        found,
                        prevModeBrackets.TextIsBracket.GetValueOrDefault(foundBracketText),
                        prevModeBrackets.TextIsOpenBracket.GetValueOrDefault(foundBracketText),
                        continueSearchPredicate);
                    if (r.Canceled)
                    {
                        return null;
                    }

                    if (r.Value is { } value)
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private SearchResult<(TextRange, TextRange)> MatchFoundBracket(TextRange foundBracket, RichEditBracket? data, bool isOpen, Func<bool> continueSearchPredicate)
    {
        if (data is null)
        {
            return SearchResult<(TextRange, TextRange)>.Of(null);
        }

        var matched = isOpen
            ? FindMatchingBracketDownLegacy(data, new TextPosition(foundBracket.EndLineNumber, foundBracket.EndColumn), continueSearchPredicate)
            : FindMatchingBracketUpLegacy(data, new TextPosition(foundBracket.StartLineNumber, foundBracket.StartColumn), continueSearchPredicate);

        if (matched.Canceled)
        {
            return SearchResult<(TextRange, TextRange)>.Cancel;
        }

        return matched.Value is { } range
            ? SearchResult<(TextRange, TextRange)>.Of((foundBracket, range))
            : SearchResult<(TextRange, TextRange)>.Of(null);
    }

    private SearchResult<TextRange> FindMatchingBracketUpLegacy(RichEditBracket bracket, TextPosition position, Func<bool> continueSearchPredicate)
    {
        var languageId = bracket.LanguageId;
        var reversedBracketRegex = bracket.ReversedRegex;
        var count = -1;

        var totalCallCount = 0;
        SearchResult<TextRange> SearchPrevMatchingBracketInRange(int lineNumber, string lineText, int searchStartOffset, int searchEndOffset)
        {
            while (true)
            {
                if ((++totalCallCount) % 100 == 0 && !continueSearchPredicate())
                {
                    return SearchResult<TextRange>.Cancel;
                }

                if (BracketsUtils.FindPrevBracketInRange(reversedBracketRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is not { } r)
                {
                    break;
                }

                var hitText = lineText[(r.StartColumn - 1)..(r.EndColumn - 1)].ToLowerInvariant();
                if (bracket.IsOpen(hitText))
                {
                    count++;
                }
                else if (bracket.IsClose(hitText))
                {
                    count--;
                }

                if (count == 0)
                {
                    return SearchResult<TextRange>.Of(r);
                }

                searchEndOffset = r.StartColumn - 1;
            }

            return SearchResult<TextRange>.Of(null);
        }

        for (var lineNumber = position.LineNumber; lineNumber >= 1; lineNumber--)
        {
            var lineTokens = _textModel.Tokenization.GetLineTokens(lineNumber);
            var tokenCount = lineTokens.GetCount();
            var lineText = _textModel.GetLineContent(lineNumber);

            var tokenIndex = tokenCount - 1;
            var searchStartOffset = lineText.Length;
            var searchEndOffset = lineText.Length;
            if (lineNumber == position.LineNumber)
            {
                tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
                searchStartOffset = position.Column - 1;
                searchEndOffset = position.Column - 1;
            }

            var prevSearchInToken = true;
            for (; tokenIndex >= 0; tokenIndex--)
            {
                var searchInToken = lineTokens.GetLanguageId(tokenIndex) == languageId && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(tokenIndex));

                if (searchInToken)
                {
                    // this token should be searched
                    if (prevSearchInToken)
                    {
                        // the previous token should be searched, simply extend searchStartOffset
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                    }
                    else
                    {
                        // the previous token should not be searched
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                }
                else if (prevSearchInToken && searchStartOffset != searchEndOffset)
                {
                    // this token should not be searched
                    var r = SearchPrevMatchingBracketInRange(lineNumber, lineText, searchStartOffset, searchEndOffset);
                    if (r.Canceled || r.Value is not null)
                    {
                        return r;
                    }
                }

                prevSearchInToken = searchInToken;
            }

            if (prevSearchInToken && searchStartOffset != searchEndOffset)
            {
                var r = SearchPrevMatchingBracketInRange(lineNumber, lineText, searchStartOffset, searchEndOffset);
                if (r.Canceled || r.Value is not null)
                {
                    return r;
                }
            }
        }

        return SearchResult<TextRange>.Of(null);
    }

    private SearchResult<TextRange> FindMatchingBracketDownLegacy(RichEditBracket bracket, TextPosition position, Func<bool> continueSearchPredicate)
    {
        var languageId = bracket.LanguageId;
        var bracketRegex = bracket.ForwardRegex;
        var count = 1;

        var totalCallCount = 0;
        SearchResult<TextRange> SearchNextMatchingBracketInRange(int lineNumber, string lineText, int searchStartOffset, int searchEndOffset)
        {
            while (true)
            {
                if ((++totalCallCount) % 100 == 0 && !continueSearchPredicate())
                {
                    return SearchResult<TextRange>.Cancel;
                }

                if (BracketsUtils.FindNextBracketInRange(bracketRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is not { } r)
                {
                    break;
                }

                var hitText = lineText[(r.StartColumn - 1)..(r.EndColumn - 1)].ToLowerInvariant();
                if (bracket.IsOpen(hitText))
                {
                    count++;
                }
                else if (bracket.IsClose(hitText))
                {
                    count--;
                }

                if (count == 0)
                {
                    return SearchResult<TextRange>.Of(r);
                }

                searchStartOffset = r.EndColumn - 1;
            }

            return SearchResult<TextRange>.Of(null);
        }

        var lineCount = _textModel.GetLineCount();
        for (var lineNumber = position.LineNumber; lineNumber <= lineCount; lineNumber++)
        {
            var lineTokens = _textModel.Tokenization.GetLineTokens(lineNumber);
            var tokenCount = lineTokens.GetCount();
            var lineText = _textModel.GetLineContent(lineNumber);

            var tokenIndex = 0;
            var searchStartOffset = 0;
            var searchEndOffset = 0;
            if (lineNumber == position.LineNumber)
            {
                tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
                searchStartOffset = position.Column - 1;
                searchEndOffset = position.Column - 1;
            }

            var prevSearchInToken = true;
            for (; tokenIndex < tokenCount; tokenIndex++)
            {
                var searchInToken = lineTokens.GetLanguageId(tokenIndex) == languageId && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(tokenIndex));

                if (searchInToken)
                {
                    // this token should be searched
                    if (prevSearchInToken)
                    {
                        // the previous token should be searched, simply extend searchEndOffset
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                    else
                    {
                        // the previous token should not be searched
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                }
                else if (prevSearchInToken && searchStartOffset != searchEndOffset)
                {
                    // this token should not be searched
                    var r = SearchNextMatchingBracketInRange(lineNumber, lineText, searchStartOffset, searchEndOffset);
                    if (r.Canceled || r.Value is not null)
                    {
                        return r;
                    }
                }

                prevSearchInToken = searchInToken;
            }

            if (prevSearchInToken && searchStartOffset != searchEndOffset)
            {
                var r = SearchNextMatchingBracketInRange(lineNumber, lineText, searchStartOffset, searchEndOffset);
                if (r.Canceled || r.Value is not null)
                {
                    return r;
                }
            }
        }

        return SearchResult<TextRange>.Of(null);
    }

    private FoundBracket? FindPrevBracketLegacy(TextPosition position)
    {
        string? languageId = null;
        RichEditBrackets? modeBrackets = null;
        LanguageBracketsConfiguration? bracketConfig = null;
        for (var lineNumber = position.LineNumber; lineNumber >= 1; lineNumber--)
        {
            var lineTokens = _textModel.Tokenization.GetLineTokens(lineNumber);
            var tokenCount = lineTokens.GetCount();
            var lineText = _textModel.GetLineContent(lineNumber);

            var tokenIndex = tokenCount - 1;
            var searchStartOffset = lineText.Length;
            var searchEndOffset = lineText.Length;
            if (lineNumber == position.LineNumber)
            {
                tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
                searchStartOffset = position.Column - 1;
                searchEndOffset = position.Column - 1;
                var tokenLanguageId = lineTokens.GetLanguageId(tokenIndex);
                if (languageId != tokenLanguageId)
                {
                    languageId = tokenLanguageId;
                    modeBrackets = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
                    bracketConfig = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew;
                }
            }

            var prevSearchInToken = true;
            for (; tokenIndex >= 0; tokenIndex--)
            {
                var tokenLanguageId = lineTokens.GetLanguageId(tokenIndex);

                if (languageId != tokenLanguageId)
                {
                    // language id change!
                    if (modeBrackets is not null && bracketConfig is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
                    {
                        if (BracketsUtils.FindPrevBracketInRange(modeBrackets.ReversedRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is { } r)
                        {
                            return ToFoundBracket(bracketConfig, r);
                        }

                        prevSearchInToken = false;
                    }

                    languageId = tokenLanguageId;
                    modeBrackets = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
                    bracketConfig = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew;
                }

                var searchInToken = modeBrackets is not null && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(tokenIndex));

                if (searchInToken)
                {
                    // this token should be searched
                    if (prevSearchInToken)
                    {
                        // the previous token should be searched, simply extend searchStartOffset
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                    }
                    else
                    {
                        // the previous token should not be searched
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                }
                else if (bracketConfig is not null && modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
                {
                    // this token should not be searched
                    if (BracketsUtils.FindPrevBracketInRange(modeBrackets.ReversedRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is { } r)
                    {
                        return ToFoundBracket(bracketConfig, r);
                    }
                }

                prevSearchInToken = searchInToken;
            }

            if (bracketConfig is not null && modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
            {
                if (BracketsUtils.FindPrevBracketInRange(modeBrackets.ReversedRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is { } r)
                {
                    return ToFoundBracket(bracketConfig, r);
                }
            }
        }

        return null;
    }

    private FoundBracket? FindNextBracketLegacy(TextPosition position)
    {
        var lineCount = _textModel.GetLineCount();

        string? languageId = null;
        RichEditBrackets? modeBrackets = null;
        LanguageBracketsConfiguration? bracketConfig = null;
        for (var lineNumber = position.LineNumber; lineNumber <= lineCount; lineNumber++)
        {
            var lineTokens = _textModel.Tokenization.GetLineTokens(lineNumber);
            var tokenCount = lineTokens.GetCount();
            var lineText = _textModel.GetLineContent(lineNumber);

            var tokenIndex = 0;
            var searchStartOffset = 0;
            var searchEndOffset = 0;
            if (lineNumber == position.LineNumber)
            {
                tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
                searchStartOffset = position.Column - 1;
                searchEndOffset = position.Column - 1;
                var tokenLanguageId = lineTokens.GetLanguageId(tokenIndex);
                if (languageId != tokenLanguageId)
                {
                    languageId = tokenLanguageId;
                    modeBrackets = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
                    bracketConfig = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew;
                }
            }

            var prevSearchInToken = true;
            for (; tokenIndex < tokenCount; tokenIndex++)
            {
                var tokenLanguageId = lineTokens.GetLanguageId(tokenIndex);

                if (languageId != tokenLanguageId)
                {
                    // language id change!
                    if (bracketConfig is not null && modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
                    {
                        if (BracketsUtils.FindNextBracketInRange(modeBrackets.ForwardRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is { } r)
                        {
                            return ToFoundBracket(bracketConfig, r);
                        }

                        prevSearchInToken = false;
                    }

                    languageId = tokenLanguageId;
                    modeBrackets = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
                    bracketConfig = _languageConfigurationService.GetLanguageConfiguration(languageId).BracketsNew;
                }

                var searchInToken = modeBrackets is not null && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(tokenIndex));
                if (searchInToken)
                {
                    // this token should be searched
                    if (prevSearchInToken)
                    {
                        // the previous token should be searched, simply extend searchEndOffset
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                    else
                    {
                        // the previous token should not be searched
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                }
                else if (bracketConfig is not null && modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
                {
                    // this token should not be searched
                    if (BracketsUtils.FindNextBracketInRange(modeBrackets.ForwardRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is { } r)
                    {
                        return ToFoundBracket(bracketConfig, r);
                    }
                }

                prevSearchInToken = searchInToken;
            }

            if (bracketConfig is not null && modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
            {
                if (BracketsUtils.FindNextBracketInRange(modeBrackets.ForwardRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is { } r)
                {
                    return ToFoundBracket(bracketConfig, r);
                }
            }
        }

        return null;
    }

    private (TextRange Open, TextRange Close)? FindEnclosingBracketsLegacy(TextPosition position, Func<bool> continueSearchPredicate)
    {
        var lineCount = _textModel.GetLineCount();
        var savedCounts = new Dictionary<string, int[]>(StringComparer.Ordinal);

        int[] counts = [];
        void ResetCounts(string languageId, RichEditBrackets? modeBrackets)
        {
            if (!savedCounts.TryGetValue(languageId, out var tmp))
            {
                tmp = new int[modeBrackets?.Brackets.Count ?? 0];
                savedCounts[languageId] = tmp;
            }

            counts = tmp;
        }

        var totalCallCount = 0;
        SearchResult<(TextRange, TextRange)> SearchInRange(RichEditBrackets modeBrackets, int lineNumber, string lineText, int searchStartOffset, int searchEndOffset)
        {
            while (true)
            {
                if ((++totalCallCount) % 100 == 0 && !continueSearchPredicate())
                {
                    return SearchResult<(TextRange, TextRange)>.Cancel;
                }

                if (BracketsUtils.FindNextBracketInRange(modeBrackets.ForwardRegex, lineNumber, lineText, searchStartOffset, searchEndOffset) is not { } r)
                {
                    break;
                }

                var hitText = lineText[(r.StartColumn - 1)..(r.EndColumn - 1)].ToLowerInvariant();
                if (modeBrackets.TextIsBracket.TryGetValue(hitText, out var bracket))
                {
                    if (bracket.IsOpen(hitText))
                    {
                        counts[bracket.Index]++;
                    }
                    else if (bracket.IsClose(hitText))
                    {
                        counts[bracket.Index]--;
                    }

                    if (counts[bracket.Index] == -1)
                    {
                        return MatchFoundBracket(r, bracket, false, continueSearchPredicate);
                    }
                }

                searchStartOffset = r.EndColumn - 1;
            }

            return SearchResult<(TextRange, TextRange)>.Of(null);
        }

        string? languageId = null;
        RichEditBrackets? modeBrackets = null;
        for (var lineNumber = position.LineNumber; lineNumber <= lineCount; lineNumber++)
        {
            var lineTokens = _textModel.Tokenization.GetLineTokens(lineNumber);
            var tokenCount = lineTokens.GetCount();
            var lineText = _textModel.GetLineContent(lineNumber);

            var tokenIndex = 0;
            var searchStartOffset = 0;
            var searchEndOffset = 0;
            if (lineNumber == position.LineNumber)
            {
                tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
                searchStartOffset = position.Column - 1;
                searchEndOffset = position.Column - 1;
                var tokenLanguageId = lineTokens.GetLanguageId(tokenIndex);
                if (languageId != tokenLanguageId)
                {
                    languageId = tokenLanguageId;
                    modeBrackets = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
                    ResetCounts(languageId, modeBrackets);
                }
            }

            var prevSearchInToken = true;
            for (; tokenIndex < tokenCount; tokenIndex++)
            {
                var tokenLanguageId = lineTokens.GetLanguageId(tokenIndex);

                if (languageId != tokenLanguageId)
                {
                    // language id change!
                    if (modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
                    {
                        var r = SearchInRange(modeBrackets, lineNumber, lineText, searchStartOffset, searchEndOffset);
                        if (r.Canceled || r.Value is not null)
                        {
                            return StripBracketSearchCanceled(r);
                        }

                        prevSearchInToken = false;
                    }

                    languageId = tokenLanguageId;
                    modeBrackets = _languageConfigurationService.GetLanguageConfiguration(languageId).Brackets;
                    ResetCounts(languageId, modeBrackets);
                }

                var searchInToken = modeBrackets is not null && !Supports.IgnoreBracketsInToken(lineTokens.GetStandardTokenType(tokenIndex));
                if (searchInToken)
                {
                    // this token should be searched
                    if (prevSearchInToken)
                    {
                        // the previous token should be searched, simply extend searchEndOffset
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                    else
                    {
                        // the previous token should not be searched
                        searchStartOffset = lineTokens.GetStartOffset(tokenIndex);
                        searchEndOffset = lineTokens.GetEndOffset(tokenIndex);
                    }
                }
                else if (modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
                {
                    // this token should not be searched
                    var r = SearchInRange(modeBrackets, lineNumber, lineText, searchStartOffset, searchEndOffset);
                    if (r.Canceled || r.Value is not null)
                    {
                        return StripBracketSearchCanceled(r);
                    }
                }

                prevSearchInToken = searchInToken;
            }

            if (modeBrackets is not null && prevSearchInToken && searchStartOffset != searchEndOffset)
            {
                var r = SearchInRange(modeBrackets, lineNumber, lineText, searchStartOffset, searchEndOffset);
                if (r.Canceled || r.Value is not null)
                {
                    return StripBracketSearchCanceled(r);
                }
            }
        }

        return null;
    }
}
