// Ported from vscode (src/vs/editor/common/model/textModelSearch.ts: TextModelSearch, createFindMatch and
// isMultilineRegexSource, commit b57d5c9e), MIT license.
//
// The text model is not ported yet, so the methods take a PieceTreeTextBuffer where upstream takes a TextModel.
//
// Difference from upstream: the line by line searches read each line as a view of the buffer (GetLineContentSpan)
// and not as a string of its own, and make one Searcher for the search and not one for every line.

using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Model;

/// <summary>Finds matches (multiline and line by line) in a text buffer, the way Monaco's find widget does.</summary>
internal static class TextModelSearch
{
    private const int LimitFindCount = 999;

    internal static List<FindMatch> FindMatches(
        PieceTreeTextBuffer model,
        SearchParams searchParams,
        TextRange searchRange,
        bool captureMatches,
        int limitResultCount)
    {
        var searchData = searchParams.ParseSearchRequest();
        if (searchData is null)
        {
            return [];
        }

        if (searchData.Regex.Multiline)
        {
            return _doFindMatchesMultiline(
                model,
                searchRange,
                new Searcher(searchData.WordSeparators, searchData.Regex.GetRegex(model.MightContainNonBasicASCII())),
                captureMatches,
                limitResultCount);
        }

        return _doFindMatchesLineByLine(model, searchRange, searchData, captureMatches, limitResultCount);
    }

    /// <summary>The JavaScript source-level test for a regular expression that may span lines.</summary>
    internal static bool IsMultilineRegexSource(string searchString)
    {
        if (string.IsNullOrEmpty(searchString))
        {
            return false;
        }

        for (int i = 0, len = searchString.Length; i < len; i++)
        {
            var chCode = searchString[i];

            if (chCode == '\n')
            {
                return true;
            }

            if (chCode == '\\')
            {
                // move to next char
                i++;

                if (i >= len)
                {
                    // string ends with a \
                    break;
                }

                var nextChCode = searchString[i];
                if (nextChCode is 'n' or 'r' or 'W')
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static FindMatch CreateFindMatch(TextRange range, Match rawMatches, bool captureMatches)
    {
        return new FindMatch(range, captureMatches ? GetCaptures(rawMatches) : null);
    }

    /// <summary>The whole match and its groups, null for a group that did not take part (JavaScript's undefined).</summary>
    internal static string?[] GetCaptures(Match match)
    {
        var groups = match.Groups;
        var matches = new string?[groups.Count];
        for (var i = 0; i < groups.Count; i++)
        {
            matches[i] = groups[i].Success ? groups[i].Value : null;
        }

        return matches;
    }

    /// <summary>
    /// Multiline search always executes on the lines concatenated with \n.
    /// We must therefore compensate for the count of \n in case the model is CRLF.
    /// </summary>
    private static TextRange _getMultilineMatchRange(
        PieceTreeTextBuffer model,
        int deltaOffset,
        string text,
        LineFeedCounter? lfCounter,
        int matchIndex,
        string match0)
    {
        int startOffset;
        var lineFeedCountBeforeMatch = 0;
        if (lfCounter is not null)
        {
            lineFeedCountBeforeMatch = lfCounter.FindLineFeedCountBeforeOffset(matchIndex);
            startOffset = deltaOffset + matchIndex + lineFeedCountBeforeMatch; // add as many \r as there were \n
        }
        else
        {
            startOffset = deltaOffset + matchIndex;
        }

        int endOffset;
        if (lfCounter is not null)
        {
            var lineFeedCountBeforeEndOfMatch = lfCounter.FindLineFeedCountBeforeOffset(matchIndex + match0.Length);
            var lineFeedCountInMatch = lineFeedCountBeforeEndOfMatch - lineFeedCountBeforeMatch;
            endOffset = startOffset + match0.Length + lineFeedCountInMatch; // add as many \r as there were \n
        }
        else
        {
            endOffset = startOffset + match0.Length;
        }

        var startPosition = model.GetPositionAt(startOffset);
        var endPosition = model.GetPositionAt(endOffset);
        return new TextRange(startPosition.LineNumber, startPosition.Column, endPosition.LineNumber, endPosition.Column);
    }

    private static List<FindMatch> _doFindMatchesMultiline(
        PieceTreeTextBuffer model,
        TextRange searchRange,
        Searcher searcher,
        bool captureMatches,
        int limitResultCount)
    {
        var deltaOffset = model.GetOffsetAt(searchRange.StartLineNumber, searchRange.StartColumn);
        // We always execute multiline search over the lines joined with \n
        // This makes it that \n will match the EOL for both CRLF and LF models
        // We compensate for offset errors in _getMultilineMatchRange
        var text = model.GetValueInRange(searchRange, EndOfLinePreference.LF);
        var lfCounter = model.GetEOL() == "\r\n" ? new LineFeedCounter(text) : null;

        var result = new List<FindMatch>();
        var counter = 0;

        searcher.Reset(0);
        Match? m;
        while ((m = searcher.Next(text)) is not null)
        {
            result.Add(CreateFindMatch(
                _getMultilineMatchRange(model, deltaOffset, text, lfCounter, m.Index, m.Value),
                m,
                captureMatches));
            counter++;
            if (counter >= limitResultCount)
            {
                return result;
            }
        }

        return result;
    }

    private static List<FindMatch> _doFindMatchesLineByLine(
        PieceTreeTextBuffer model,
        TextRange searchRange,
        SearchData searchData,
        bool captureMatches,
        int limitResultCount)
    {
        var result = new List<FindMatch>();
        var resultLen = 0;
        // Upstream makes a searcher for every line; one that is reset for every line finds the same.
        var searcher = new Searcher(
            searchData.WordSeparators, searchData.Regex.GetRegex(model.MightContainNonBasicASCII()));

        // Early case for a search range that starts & stops on the same line number
        if (searchRange.StartLineNumber == searchRange.EndLineNumber)
        {
            var singleLineText = SearchHelpers.JsSubstring(
                model.GetLineContentSpan(searchRange.StartLineNumber),
                searchRange.StartColumn - 1,
                searchRange.EndColumn - 1);
            _ = _findMatchesInLine(
                searchData, searcher, singleLineText, searchRange.StartLineNumber, searchRange.StartColumn - 1,
                resultLen, result, captureMatches, limitResultCount);
            return result;
        }

        // Collect results from first line
        var text = SearchHelpers.JsSubstr(
            model.GetLineContentSpan(searchRange.StartLineNumber), searchRange.StartColumn - 1);
        resultLen = _findMatchesInLine(
            searchData, searcher, text, searchRange.StartLineNumber, searchRange.StartColumn - 1, resultLen, result,
            captureMatches, limitResultCount);

        // Collect results from middle lines
        for (var lineNumber = searchRange.StartLineNumber + 1;
            lineNumber < searchRange.EndLineNumber && resultLen < limitResultCount;
            lineNumber++)
        {
            resultLen = _findMatchesInLine(
                searchData, searcher, model.GetLineContentSpan(lineNumber), lineNumber, 0, resultLen, result,
                captureMatches, limitResultCount);
        }

        // Collect results from last line
        if (resultLen < limitResultCount)
        {
            var lastText = SearchHelpers.JsSubstring(
                model.GetLineContentSpan(searchRange.EndLineNumber), 0, searchRange.EndColumn - 1);
            _ = _findMatchesInLine(
                searchData, searcher, lastText, searchRange.EndLineNumber, 0, resultLen, result, captureMatches,
                limitResultCount);
        }

        return result;
    }

    internal static int _findMatchesInLine(
        SearchData searchData,
        Searcher searcher,
        ReadOnlySpan<char> text,
        int lineNumber,
        int deltaOffset,
        int resultLen,
        List<FindMatch> result,
        bool captureMatches,
        int limitResultCount)
    {
        var wordSeparators = searchData.WordSeparators;
        if (!captureMatches && searchData.SimpleSearch is not null)
        {
            var searchString = searchData.SimpleSearch.AsSpan();
            var searchStringLen = searchString.Length;
            var textLength = text.Length;

            var searchFrom = 0;
            while (true)
            {
                var found = text.Slice(searchFrom).IndexOf(searchString, StringComparison.Ordinal);
                if (found == -1)
                {
                    break;
                }

                var lastMatchIndex = searchFrom + found;
                searchFrom = lastMatchIndex + searchStringLen;
                if (wordSeparators is null
                    || SearchMatchValidation.IsValidMatch(wordSeparators, text, textLength, lastMatchIndex, searchStringLen))
                {
                    result.Add(new FindMatch(
                        new TextRange(
                            lineNumber,
                            lastMatchIndex + 1 + deltaOffset,
                            lineNumber,
                            lastMatchIndex + 1 + searchStringLen + deltaOffset),
                        null));
                    resultLen++;
                    if (resultLen >= limitResultCount)
                    {
                        return resultLen;
                    }
                }
            }

            return resultLen;
        }

        var input = searcher.NeedsString(captureMatches) ? new string(text) : null;
        // Reset regex to search from the beginning
        searcher.Reset(0);
        while (searcher.Next(text, input, captureMatches, out var matchIndex, out var matchLength, out var matches))
        {
            result.Add(new FindMatch(
                new TextRange(
                    lineNumber,
                    matchIndex + 1 + deltaOffset,
                    lineNumber,
                    matchIndex + 1 + matchLength + deltaOffset),
                matches));
            resultLen++;
            if (resultLen >= limitResultCount)
            {
                return resultLen;
            }
        }

        return resultLen;
    }

    internal static FindMatch? FindNextMatch(
        PieceTreeTextBuffer model,
        SearchParams searchParams,
        TextPosition searchStart,
        bool captureMatches)
    {
        var searchData = searchParams.ParseSearchRequest();
        if (searchData is null)
        {
            return null;
        }

        var searcher = new Searcher(searchData.WordSeparators, searchData.Regex.GetRegex(model.MightContainNonBasicASCII()));

        if (searchData.Regex.Multiline)
        {
            return _doFindNextMatchMultiline(model, searchStart, searcher, captureMatches);
        }

        return _doFindNextMatchLineByLine(model, searchStart, searcher, captureMatches);
    }

    private static FindMatch? _doFindNextMatchMultiline(
        PieceTreeTextBuffer model,
        TextPosition searchStart,
        Searcher searcher,
        bool captureMatches)
    {
        var searchTextStart = new TextPosition(searchStart.LineNumber, 1);
        var deltaOffset = model.GetOffsetAt(searchTextStart.LineNumber, searchTextStart.Column);
        var lineCount = model.GetLineCount();
        // We always execute multiline search over the lines joined with \n
        // This makes it that \n will match the EOL for both CRLF and LF models
        // We compensate for offset errors in _getMultilineMatchRange
        var text = model.GetValueInRange(
            new TextRange(
                searchTextStart.LineNumber,
                searchTextStart.Column,
                lineCount,
                model.GetLineMaxColumn(lineCount)),
            EndOfLinePreference.LF);
        var lfCounter = model.GetEOL() == "\r\n" ? new LineFeedCounter(text) : null;
        searcher.Reset(searchStart.Column - 1);
        var m = searcher.Next(text);
        if (m is not null)
        {
            return CreateFindMatch(
                _getMultilineMatchRange(model, deltaOffset, text, lfCounter, m.Index, m.Value),
                m,
                captureMatches);
        }

        if (searchStart.LineNumber != 1 || searchStart.Column != 1)
        {
            // Try again from the top
            return _doFindNextMatchMultiline(model, new TextPosition(1, 1), searcher, captureMatches);
        }

        return null;
    }

    private static FindMatch? _doFindNextMatchLineByLine(
        PieceTreeTextBuffer model,
        TextPosition searchStart,
        Searcher searcher,
        bool captureMatches)
    {
        var lineCount = model.GetLineCount();
        var startLineNumber = searchStart.LineNumber;

        // Look in first line
        var r = _findFirstMatchInLine(
            searcher, model.GetLineContentSpan(startLineNumber), startLineNumber, searchStart.Column, captureMatches);
        if (r is not null)
        {
            return r;
        }

        for (var i = 1; i <= lineCount; i++)
        {
            var lineIndex = (startLineNumber + i - 1) % lineCount;
            r = _findFirstMatchInLine(searcher, model.GetLineContentSpan(lineIndex + 1), lineIndex + 1, 1, captureMatches);
            if (r is not null)
            {
                return r;
            }
        }

        return null;
    }

    private static FindMatch? _findFirstMatchInLine(
        Searcher searcher,
        ReadOnlySpan<char> text,
        int lineNumber,
        int fromColumn,
        bool captureMatches)
    {
        var input = searcher.NeedsString(captureMatches) ? new string(text) : null;
        // Set regex to search from column
        searcher.Reset(fromColumn - 1);
        if (searcher.Next(text, input, captureMatches, out var matchIndex, out var matchLength, out var matches))
        {
            return new FindMatch(
                new TextRange(lineNumber, matchIndex + 1, lineNumber, matchIndex + 1 + matchLength),
                matches);
        }

        return null;
    }

    internal static FindMatch? FindPreviousMatch(
        PieceTreeTextBuffer model,
        SearchParams searchParams,
        TextPosition searchStart,
        bool captureMatches)
    {
        var searchData = searchParams.ParseSearchRequest();
        if (searchData is null)
        {
            return null;
        }

        var searcher = new Searcher(searchData.WordSeparators, searchData.Regex.GetRegex(model.MightContainNonBasicASCII()));

        if (searchData.Regex.Multiline)
        {
            return _doFindPreviousMatchMultiline(model, searchStart, searcher, captureMatches);
        }

        return _doFindPreviousMatchLineByLine(model, searchStart, searcher, captureMatches);
    }

    private static FindMatch? _doFindPreviousMatchMultiline(
        PieceTreeTextBuffer model,
        TextPosition searchStart,
        Searcher searcher,
        bool captureMatches)
    {
        var matches = _doFindMatchesMultiline(
            model,
            new TextRange(1, 1, searchStart.LineNumber, searchStart.Column),
            searcher,
            captureMatches,
            10 * LimitFindCount);
        if (matches.Count > 0)
        {
            return matches[matches.Count - 1];
        }

        var lineCount = model.GetLineCount();
        if (searchStart.LineNumber != lineCount || searchStart.Column != model.GetLineMaxColumn(lineCount))
        {
            // Try again with all content
            return _doFindPreviousMatchMultiline(
                model, new TextPosition(lineCount, model.GetLineMaxColumn(lineCount)), searcher, captureMatches);
        }

        return null;
    }

    private static FindMatch? _doFindPreviousMatchLineByLine(
        PieceTreeTextBuffer model,
        TextPosition searchStart,
        Searcher searcher,
        bool captureMatches)
    {
        var lineCount = model.GetLineCount();
        var startLineNumber = searchStart.LineNumber;

        // Look in first line
        var text = SearchHelpers.JsSubstring(model.GetLineContentSpan(startLineNumber), 0, searchStart.Column - 1);
        var r = _findLastMatchInLine(searcher, text, startLineNumber, captureMatches);
        if (r is not null)
        {
            return r;
        }

        for (var i = 1; i <= lineCount; i++)
        {
            var lineIndex = (lineCount + startLineNumber - i - 1) % lineCount;
            r = _findLastMatchInLine(searcher, model.GetLineContentSpan(lineIndex + 1), lineIndex + 1, captureMatches);
            if (r is not null)
            {
                return r;
            }
        }

        return null;
    }

    private static FindMatch? _findLastMatchInLine(
        Searcher searcher,
        ReadOnlySpan<char> text,
        int lineNumber,
        bool captureMatches)
    {
        FindMatch? bestResult = null;
        var input = searcher.NeedsString(captureMatches) ? new string(text) : null;
        searcher.Reset(0);
        while (searcher.Next(text, input, captureMatches, out var matchIndex, out var matchLength, out var matches))
        {
            bestResult = new FindMatch(
                new TextRange(lineNumber, matchIndex + 1, lineNumber, matchIndex + 1 + matchLength),
                matches);
        }

        return bestResult;
    }
}
