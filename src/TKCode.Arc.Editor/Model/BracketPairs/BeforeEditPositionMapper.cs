// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/beforeEditPositionMapper.ts,
// commit b57d5c9e), MIT license.

using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>An edit by offsets: [start, end) is replaced by text of <see cref="NewLength"/>.</summary>
internal sealed class TextEditInfo(Length startOffset, Length endOffset, Length newLength)
{
    public static IReadOnlyList<TextEditInfo> FromModelContentChanges(IReadOnlyList<ModelContentChange> changes)
    {
        // Must be sorted in ascending order
        var edits = new TextEditInfo[changes.Count];
        for (var i = 0; i < changes.Count; i++)
        {
            var c = changes[i];
            edits[changes.Count - 1 - i] = new TextEditInfo(
                PositionToLength(new TextPosition(c.Range.StartLineNumber, c.Range.StartColumn)),
                PositionToLength(new TextPosition(c.Range.EndLineNumber, c.Range.EndColumn)),
                LengthOfString(c.Text));
        }

        return edits;
    }

    public Length StartOffset { get; } = startOffset;

    public Length EndOffset { get; } = endOffset;

    public Length NewLength { get; } = newLength;

    /// <inheritdoc />
    public override string ToString() => $"[{LengthToObj(StartOffset)}...{LengthToObj(EndOffset)}) -> {LengthToObj(NewLength)}";
}

/// <summary>Maps offsets in the text after some edits to the offsets they had before them.</summary>
internal sealed class BeforeEditPositionMapper
{
    private readonly TextEditInfoCache[] _edits;
    private int _nextEditIdx;
    private int _deltaOldToNewLineCount;
    private int _deltaOldToNewColumnCount;
    private int _deltaLineIdxInOld = -1;

    /// <param name="edits">Must be sorted by offset in ascending order.</param>
    public BeforeEditPositionMapper(IReadOnlyList<TextEditInfo> edits)
    {
        _edits = edits.Select(TextEditInfoCache.From).ToArray();
    }

    /// <param name="offset">Must be equal to or greater than the last offset this method has been called with.</param>
    public Length GetOffsetBeforeChange(Length offset)
    {
        AdjustNextEdit(offset);
        return TranslateCurToOld(offset);
    }

    /// <summary>The distance to the next change; null if there is no edit anymore.</summary>
    /// <param name="offset">Must be equal to or greater than the last offset this method has been called with.</param>
    public Length? GetDistanceToNextChange(Length offset)
    {
        AdjustNextEdit(offset);

        if (_nextEditIdx >= _edits.Length)
        {
            return null;
        }

        var nextChangeOffset = TranslateOldToCur(_edits[_nextEditIdx].OffsetObj);
        return LengthDiffNonNegative(offset, nextChangeOffset);
    }

    private Length TranslateOldToCur(TextLength oldOffsetObj) =>
        oldOffsetObj.LineCount == _deltaLineIdxInOld
            ? ToLength(oldOffsetObj.LineCount + _deltaOldToNewLineCount, oldOffsetObj.ColumnCount + _deltaOldToNewColumnCount)
            : ToLength(oldOffsetObj.LineCount + _deltaOldToNewLineCount, oldOffsetObj.ColumnCount);

    private Length TranslateCurToOld(Length newOffset)
    {
        var offsetObj = LengthToObj(newOffset);
        return offsetObj.LineCount - _deltaOldToNewLineCount == _deltaLineIdxInOld
            ? ToLength(offsetObj.LineCount - _deltaOldToNewLineCount, offsetObj.ColumnCount - _deltaOldToNewColumnCount)
            : ToLength(offsetObj.LineCount - _deltaOldToNewLineCount, offsetObj.ColumnCount);
    }

    private void AdjustNextEdit(Length offset)
    {
        while (_nextEditIdx < _edits.Length)
        {
            var nextEdit = _edits[_nextEditIdx];

            // After applying the edit, what is its end offset (considering all previous edits)?
            var nextEditEndOffsetInCur = TranslateOldToCur(nextEdit.EndOffsetAfterObj);

            if (LengthLessThanEqual(nextEditEndOffsetInCur, offset))
            {
                // We are after the edit, skip it
                _nextEditIdx++;

                var nextEditEndOffsetInCurObj = LengthToObj(nextEditEndOffsetInCur);

                // Before applying the edit, what is its end offset (considering all previous edits)?
                var nextEditEndOffsetBeforeInCurObj = LengthToObj(TranslateOldToCur(nextEdit.EndOffsetBeforeObj));

                var lineDelta = nextEditEndOffsetInCurObj.LineCount - nextEditEndOffsetBeforeInCurObj.LineCount;
                _deltaOldToNewLineCount += lineDelta;

                var previousColumnDelta = _deltaLineIdxInOld == nextEdit.EndOffsetBeforeObj.LineCount ? _deltaOldToNewColumnCount : 0;
                var columnDelta = nextEditEndOffsetInCurObj.ColumnCount - nextEditEndOffsetBeforeInCurObj.ColumnCount;
                _deltaOldToNewColumnCount = previousColumnDelta + columnDelta;
                _deltaLineIdxInOld = nextEdit.EndOffsetBeforeObj.LineCount;
            }
            else
            {
                // We are in or before the edit.
                break;
            }
        }
    }

    private sealed class TextEditInfoCache(Length startOffset, Length endOffset, Length textLength)
    {
        public static TextEditInfoCache From(TextEditInfo edit) => new(edit.StartOffset, edit.EndOffset, edit.NewLength);

        public TextLength EndOffsetBeforeObj { get; } = LengthToObj(endOffset);

        public TextLength EndOffsetAfterObj { get; } = LengthToObj(LengthAdd(startOffset, textLength));

        public TextLength OffsetObj { get; } = LengthToObj(startOffset);
    }
}
