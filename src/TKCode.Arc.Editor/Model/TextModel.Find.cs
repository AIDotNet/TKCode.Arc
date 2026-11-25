// Ported from vscode (src/vs/editor/common/model/textModel.ts, commit b57d5c9e), MIT license.
// This part: find. Difference from upstream: findNextMatch does not run the TextModelSearch search whose result
// upstream discards (a leftover there).

namespace TKCode.Arc.Editor.Model;

internal sealed partial class TextModel
{
    private List<FindMatch> FindMatchesLineByLine(TextRange searchRange, SearchData searchData, bool captureMatches, int limitResultCount) =>
        _buffer.FindMatchesLineByLine(searchRange, searchData, captureMatches, limitResultCount);

    /// <summary>Finds the matches in the whole model (upstream's searchScope false or null).</summary>
    public List<FindMatch> FindMatches(
        string searchString,
        bool searchOnlyEditableRange,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        bool captureMatches,
        int limitResultCount = LimitFindCount)
    {
        _ = searchOnlyEditableRange;
        return FindMatches(searchString, (IReadOnlyList<TextRange>?)null, isRegex, matchCase, wordSeparators, captureMatches, limitResultCount);
    }

    /// <summary>Finds the matches in one range.</summary>
    public List<FindMatch> FindMatches(
        string searchString,
        TextRange searchScope,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        bool captureMatches,
        int limitResultCount = LimitFindCount) =>
        FindMatches(searchString, [searchScope], isRegex, matchCase, wordSeparators, captureMatches, limitResultCount);

    /// <summary>Finds the matches in some ranges (overlapping ranges are merged), or in the whole model for null.</summary>
    public List<FindMatch> FindMatches(
        string searchString,
        IReadOnlyList<TextRange>? searchScope,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        bool captureMatches,
        int limitResultCount = LimitFindCount)
    {
        AssertNotDisposed();

        List<TextRange> searchRanges;
        if (searchScope is { Count: > 0 })
        {
            searchRanges = searchScope.Select(ValidateRange).ToList();
        }
        else
        {
            searchRanges = [GetFullModelRange()];
        }

        // Upstream sorts stably: the index keeps equal starts in their order.
        searchRanges = searchRanges
            .Select((range, index) => (range, index))
            .OrderBy(static d => d.range.StartLineNumber)
            .ThenBy(static d => d.range.StartColumn)
            .ThenBy(static d => d.index)
            .Select(static d => d.range)
            .ToList();

        var uniqueSearchRanges = new List<TextRange>();
        var prev = searchRanges[0];
        for (var i = 1; i < searchRanges.Count; i++)
        {
            var curr = searchRanges[i];
            if (TextRange.AreIntersecting(prev, curr))
            {
                prev = prev.PlusRange(curr);
                continue;
            }

            uniqueSearchRanges.Add(prev);
            prev = curr;
        }

        uniqueSearchRanges.Add(prev);

        Func<TextRange, List<FindMatch>> matchMapper;
        if (!isRegex && searchString.IndexOf('\n') < 0)
        {
            // not regex, not multi line
            var searchParams = new SearchParams(searchString, isRegex, matchCase, wordSeparators);
            var searchData = searchParams.ParseSearchRequest();

            if (searchData is null)
            {
                return [];
            }

            matchMapper = searchRange => FindMatchesLineByLine(searchRange, searchData, captureMatches, limitResultCount);
        }
        else
        {
            matchMapper = searchRange => TextModelSearch.FindMatches(
                _buffer, new SearchParams(searchString, isRegex, matchCase, wordSeparators), searchRange, captureMatches, limitResultCount);
        }

        var result = new List<FindMatch>();
        foreach (var range in uniqueSearchRanges)
        {
            result.AddRange(matchMapper(range));
        }

        return result;
    }

    public FindMatch? FindNextMatch(
        string searchString,
        TextPosition rawSearchStart,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        bool captureMatches)
    {
        AssertNotDisposed();
        var searchStart = ValidatePosition(rawSearchStart);

        if (!isRegex && searchString.IndexOf('\n') < 0)
        {
            var searchParams = new SearchParams(searchString, isRegex, matchCase, wordSeparators);
            var searchData = searchParams.ParseSearchRequest();
            if (searchData is null)
            {
                return null;
            }

            var lineCount = GetLineCount();
            var searchRange = new TextRange(searchStart.LineNumber, searchStart.Column, lineCount, GetLineMaxColumn(lineCount));
            var ret = FindMatchesLineByLine(searchRange, searchData, captureMatches, 1);
            if (ret.Count > 0)
            {
                return ret[0];
            }

            searchRange = new TextRange(1, 1, searchStart.LineNumber, GetLineMaxColumn(searchStart.LineNumber));
            ret = FindMatchesLineByLine(searchRange, searchData, captureMatches, 1);

            return ret.Count > 0 ? ret[0] : null;
        }

        return TextModelSearch.FindNextMatch(
            _buffer, new SearchParams(searchString, isRegex, matchCase, wordSeparators), searchStart, captureMatches);
    }

    public FindMatch? FindPreviousMatch(
        string searchString,
        TextPosition rawSearchStart,
        bool isRegex,
        bool matchCase,
        string? wordSeparators,
        bool captureMatches)
    {
        AssertNotDisposed();
        var searchStart = ValidatePosition(rawSearchStart);
        return TextModelSearch.FindPreviousMatch(
            _buffer, new SearchParams(searchString, isRegex, matchCase, wordSeparators), searchStart, captureMatches);
    }
}
