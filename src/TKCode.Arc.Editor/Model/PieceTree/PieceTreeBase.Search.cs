// Ported from vscode (src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeBase.ts:
// findMatchesInNode, findMatchesLineByLine and _findMatchesInLine, commit b57d5c9e), MIT license.
//
// The search methods were left out when the piece tree was ported; they live here as a partial of PieceTreeBase.
//
// Differences from upstream:
//  - the text that is searched is a view of the buffer or of the line, not a string of its own (see FindMatchesInNode);
//  - upstream's _findMatchesInLine is a copy of the one in textModelSearch.ts; here both callers use that one.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class PieceTreeBase
{
    // #region search

    private int FindMatchesInNode(
        TreeNode node,
        Searcher searcher,
        int startLineNumber,
        int startColumn,
        BufferCursor startCursor,
        BufferCursor endCursor,
        SearchData searchData,
        bool captureMatches,
        int limitResultCount,
        int resultLen,
        List<FindMatch> result)
    {
        var buffer = _buffers[node.Piece.BufferIndex];
        var startOffsetInBuffer = OffsetInBuffer(node.Piece.BufferIndex, node.Piece.Start);
        var start = OffsetInBuffer(node.Piece.BufferIndex, startCursor);
        var end = OffsetInBuffer(node.Piece.BufferIndex, endCursor);

        // What is searched is a view of the buffer. Upstream takes a substring for every node, which in JavaScript is
        // a slice of the parent string; a .NET substring is a copy, and copying the change buffer up to `end` for each
        // of its pieces is quadratic in the number of edits.
        int textStart;
        if (searcher.WordSeparators is not null)
        {
            textStart = start;
            searcher.Reset(0);
        }
        else
        {
            // Stop at `end`: the buffer can be much larger than this node (e.g. the change buffer of
            // an append-only model) and can contain text of other pieces after it.
            textStart = 0;
            searcher.Reset(start);
        }

        // Only a Match has the capture groups, and a Match needs a string: the text is copied only when groups were
        // asked for and the expression has some. The find box's plain text search, which is what reaches this
        // method, has none.
        var searchString = searcher.NeedsString(captureMatches) ? buffer.Substring(textStart, end) : null;
        var searchText = buffer.Slice(textStart, end);

        while (searcher.Next(searchText, searchString, captureMatches, out var matchIndex, out var matchLength, out var matches))
        {
            var matchOffset = matchIndex + textStart;
            if (matchOffset >= end)
            {
                return resultLen;
            }

            var ret = PositionInBuffer(node, matchOffset - startOffsetInBuffer);
            var lineFeedCnt = GetLineFeedCnt(node.Piece.BufferIndex, startCursor, ret);
            var retStartColumn = ret.Line == startCursor.Line
                ? ret.Column - startCursor.Column + startColumn
                : ret.Column + 1;
            var retEndColumn = retStartColumn + matchLength;
            result.Add(new FindMatch(
                new TextRange(
                    startLineNumber + lineFeedCnt,
                    retStartColumn,
                    startLineNumber + lineFeedCnt,
                    retEndColumn),
                matches));
            resultLen++;

            if (matchOffset + matchLength >= end)
            {
                return resultLen;
            }

            if (resultLen >= limitResultCount)
            {
                return resultLen;
            }
        }

        return resultLen;
    }

    internal List<FindMatch> FindMatchesLineByLine(
        TextRange searchRange,
        SearchData searchData,
        bool captureMatches,
        int limitResultCount,
        bool mightContainNonBasicAscii = true)
    {
        var result = new List<FindMatch>();
        var resultLen = 0;
        var searcher = new Searcher(searchData.WordSeparators, searchData.Regex.GetRegex(mightContainNonBasicAscii));

        var startPosition = NodeAt2(searchRange.StartLineNumber, searchRange.StartColumn);
        if (startPosition is null)
        {
            return [];
        }

        var endPosition = NodeAt2(searchRange.EndLineNumber, searchRange.EndColumn);
        if (endPosition is null)
        {
            return [];
        }

        var start = PositionInBuffer(startPosition.Value.Node, startPosition.Value.Remainder);
        var end = PositionInBuffer(endPosition.Value.Node, endPosition.Value.Remainder);

        if (startPosition.Value.Node == endPosition.Value.Node)
        {
            FindMatchesInNode(
                startPosition.Value.Node, searcher, searchRange.StartLineNumber, searchRange.StartColumn, start, end,
                searchData, captureMatches, limitResultCount, resultLen, result);
            return result;
        }

        var startLineNumber = searchRange.StartLineNumber;

        var currentNode = startPosition.Value.Node;
        while (currentNode != endPosition.Value.Node)
        {
            var lineBreakCnt = GetLineFeedCnt(currentNode.Piece.BufferIndex, start, currentNode.Piece.End);

            if (lineBreakCnt >= 1)
            {
                // last line break position
                var lineStarts = _buffers[currentNode.Piece.BufferIndex].LineStarts;
                var startOffsetInBuffer = OffsetInBuffer(currentNode.Piece.BufferIndex, currentNode.Piece.Start);
                var nextLineStartOffset = lineStarts[start.Line + lineBreakCnt];
                var column = startLineNumber == searchRange.StartLineNumber ? searchRange.StartColumn : 1;
                resultLen = FindMatchesInNode(
                    currentNode, searcher, startLineNumber, column, start,
                    PositionInBuffer(currentNode, nextLineStartOffset - startOffsetInBuffer),
                    searchData, captureMatches, limitResultCount, resultLen, result);

                if (resultLen >= limitResultCount)
                {
                    return result;
                }

                startLineNumber += lineBreakCnt;
            }

            var startColumn = startLineNumber == searchRange.StartLineNumber ? searchRange.StartColumn - 1 : 0;
            // search for the remaining content
            if (startLineNumber == searchRange.EndLineNumber)
            {
                var text = SearchHelpers.JsSubstring(
                    GetLineContentSpan(startLineNumber), startColumn, searchRange.EndColumn - 1);
                _ = TextModelSearch._findMatchesInLine(
                    searchData, searcher, text, searchRange.EndLineNumber, startColumn, resultLen, result,
                    captureMatches, limitResultCount);
                return result;
            }

            resultLen = TextModelSearch._findMatchesInLine(
                searchData, searcher, SearchHelpers.JsSubstr(GetLineContentSpan(startLineNumber), startColumn),
                startLineNumber, startColumn, resultLen, result, captureMatches, limitResultCount);

            if (resultLen >= limitResultCount)
            {
                return result;
            }

            startLineNumber++;
            var nextPosition = NodeAt2(startLineNumber, 1);
            if (nextPosition is null)
            {
                return result;
            }

            currentNode = nextPosition.Value.Node;
            start = PositionInBuffer(nextPosition.Value.Node, nextPosition.Value.Remainder);
        }

        if (startLineNumber == searchRange.EndLineNumber)
        {
            var endStartColumn = startLineNumber == searchRange.StartLineNumber ? searchRange.StartColumn - 1 : 0;
            var text = SearchHelpers.JsSubstring(
                GetLineContentSpan(startLineNumber), endStartColumn, searchRange.EndColumn - 1);
            _ = TextModelSearch._findMatchesInLine(
                searchData, searcher, text, searchRange.EndLineNumber, endStartColumn, resultLen, result,
                captureMatches, limitResultCount);
            return result;
        }

        var finalStartColumn = startLineNumber == searchRange.StartLineNumber ? searchRange.StartColumn : 1;
        FindMatchesInNode(
            endPosition.Value.Node, searcher, startLineNumber, finalStartColumn, start, end, searchData,
            captureMatches, limitResultCount, resultLen, result);
        return result;
    }

    // #endregion
}
