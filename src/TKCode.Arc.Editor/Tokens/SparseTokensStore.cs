// Ported from vscode (src/vs/editor/common/tokens/sparseTokensStore.ts, commit b57d5c9e), MIT license.
// Differences from upstream: set does not take the text model, because reportIfInvalid (which only logs) is left out
// (SparseMultilineTokens.cs); the pieces are a List.

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Tokens;

/// <summary>Represents sparse tokens in a text model (the semantic tokens, laid over the syntax tokens).</summary>
internal sealed class SparseTokensStore
{
    private readonly ILanguageIdCodec _languageIdCodec;
    private List<SparseMultilineTokens> _pieces;
    private bool _isComplete;

    public SparseTokensStore(ILanguageIdCodec languageIdCodec)
    {
        _pieces = new List<SparseMultilineTokens>();
        _isComplete = false;
        _languageIdCodec = languageIdCodec;
    }

    public void Flush()
    {
        _pieces = new List<SparseMultilineTokens>();
        _isComplete = false;
    }

    public bool IsEmpty() => _pieces.Count == 0;

    public void Set(IReadOnlyList<SparseMultilineTokens>? pieces, bool isComplete)
    {
        _pieces = pieces is null ? new List<SparseMultilineTokens>() : new List<SparseMultilineTokens>(pieces);
        _isComplete = isComplete;
    }

    public TextRange SetPartial(TextRange range0, IReadOnlyList<SparseMultilineTokens> pieces)
    {
        var range = range0;
        if (pieces.Count > 0)
        {
            var firstRange = pieces[0].GetRange();
            var lastRange = pieces[^1].GetRange();
            if (firstRange is null || lastRange is null)
            {
                return range0;
            }

            range = range0.PlusRange(firstRange.Value).PlusRange(lastRange.Value);
        }

        int? insertPosition = null;
        for (int i = 0, len = _pieces.Count; i < len; i++)
        {
            var piece = _pieces[i];
            if (piece.EndLineNumber < range.StartLineNumber)
            {
                // this piece is before the range
                continue;
            }

            if (piece.StartLineNumber > range.EndLineNumber)
            {
                // this piece is after the range, so mark the spot before this piece
                // as a good insertion position and stop looping
                insertPosition ??= i;
                break;
            }

            // this piece might intersect with the range
            piece.RemoveTokens(range);

            if (piece.IsEmpty())
            {
                // remove the piece if it became empty
                _pieces.RemoveAt(i);
                i--;
                len--;
                continue;
            }

            if (piece.EndLineNumber < range.StartLineNumber)
            {
                // after removal, this piece is before the range
                continue;
            }

            if (piece.StartLineNumber > range.EndLineNumber)
            {
                // after removal, this piece is after the range
                insertPosition ??= i;
                continue;
            }

            // after removal, this piece contains the range
            var (a, b) = piece.Split(range);
            if (a.IsEmpty())
            {
                // this piece is actually after the range
                insertPosition ??= i;
                continue;
            }

            if (b.IsEmpty())
            {
                // this piece is actually before the range
                continue;
            }

            _pieces.RemoveAt(i);
            _pieces.InsertRange(i, [a, b]);
            i++;
            len++;

            insertPosition ??= i;
        }

        insertPosition ??= _pieces.Count;

        if (pieces.Count > 0)
        {
            _pieces.InsertRange(insertPosition.Value, pieces);
        }

        return range;
    }

    public bool IsComplete() => _isComplete;

    /// <summary>The syntax tokens of a line with the semantic tokens of the line laid over them.</summary>
    public LineTokens AddSparseTokens(int lineNumber, LineTokens aTokens)
    {
        if (aTokens.GetTextLength() == 0)
        {
            // Don't do anything for empty lines
            return aTokens;
        }

        var pieces = _pieces;

        if (pieces.Count == 0)
        {
            return aTokens;
        }

        var pieceIndex = FindFirstPieceWithLine(pieces, lineNumber);
        var maybeBTokens = pieces[pieceIndex].GetLineTokens(lineNumber);

        if (maybeBTokens is not { } bTokens)
        {
            return aTokens;
        }

        var aLen = aTokens.GetCount();
        var bLen = bTokens.GetCount();

        var aIndex = 0;
        var result = new List<uint>();
        var lastEndOffset = 0;

        void EmitToken(int endOffset, uint metadata)
        {
            if (endOffset <= lastEndOffset)
            {
                return;
            }

            lastEndOffset = endOffset;
            result.Add((uint)endOffset);
            result.Add(metadata);
        }

        for (var bIndex = 0; bIndex < bLen; bIndex++)
        {
            // bTokens is not validated yet, but aTokens is. We want to make sure that the LineTokens we return
            // are valid, so we clamp the ranges to ensure that.
            var bStartCharacter = Math.Min(bTokens.GetStartCharacter(bIndex), aTokens.GetTextLength());
            var bEndCharacter = Math.Min(bTokens.GetEndCharacter(bIndex), aTokens.GetTextLength());
            var bMetadata = bTokens.GetMetadata(bIndex);

            var bMask =
                ((bMetadata & MetadataConsts.SemanticUseItalic) != 0 ? MetadataConsts.ItalicMask : 0)
                | ((bMetadata & MetadataConsts.SemanticUseBold) != 0 ? MetadataConsts.BoldMask : 0)
                | ((bMetadata & MetadataConsts.SemanticUseUnderline) != 0 ? MetadataConsts.UnderlineMask : 0)
                | ((bMetadata & MetadataConsts.SemanticUseStrikethrough) != 0 ? MetadataConsts.StrikethroughMask : 0)
                | ((bMetadata & MetadataConsts.SemanticUseForeground) != 0 ? MetadataConsts.ForegroundMask : 0)
                | ((bMetadata & MetadataConsts.SemanticUseBackground) != 0 ? MetadataConsts.BackgroundMask : 0);
            var aMask = ~bMask;

            // push any token from `a` that is before `b`
            while (aIndex < aLen && aTokens.GetEndOffset(aIndex) <= bStartCharacter)
            {
                EmitToken(aTokens.GetEndOffset(aIndex), aTokens.GetMetadata(aIndex));
                aIndex++;
            }

            // push the token from `a` if it intersects the token from `b`
            if (aIndex < aLen && aTokens.GetStartOffset(aIndex) < bStartCharacter)
            {
                EmitToken(bStartCharacter, aTokens.GetMetadata(aIndex));
            }

            // skip any tokens from `a` that are contained inside `b`
            while (aIndex < aLen && aTokens.GetEndOffset(aIndex) < bEndCharacter)
            {
                EmitToken(aTokens.GetEndOffset(aIndex), (aTokens.GetMetadata(aIndex) & aMask) | (bMetadata & bMask));
                aIndex++;
            }

            if (aIndex < aLen)
            {
                EmitToken(bEndCharacter, (aTokens.GetMetadata(aIndex) & aMask) | (bMetadata & bMask));
                if (aTokens.GetEndOffset(aIndex) == bEndCharacter)
                {
                    // `a` ends exactly at the same spot as `b`!
                    aIndex++;
                }
            }
            else
            {
                var aMergeIndex = Math.Min(Math.Max(0, aIndex - 1), aLen - 1);

                // push the token from `b`
                EmitToken(bEndCharacter, (aTokens.GetMetadata(aMergeIndex) & aMask) | (bMetadata & bMask));
            }
        }

        // push the remaining tokens from `a`
        while (aIndex < aLen)
        {
            EmitToken(aTokens.GetEndOffset(aIndex), aTokens.GetMetadata(aIndex));
            aIndex++;
        }

        return new LineTokens(result.ToArray(), aTokens.GetLineContent(), _languageIdCodec);
    }

    private static int FindFirstPieceWithLine(List<SparseMultilineTokens> pieces, int lineNumber)
    {
        var low = 0;
        var high = pieces.Count - 1;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);

            if (pieces[mid].EndLineNumber < lineNumber)
            {
                low = mid + 1;
            }
            else if (pieces[mid].StartLineNumber > lineNumber)
            {
                high = mid - 1;
            }
            else
            {
                while (mid > low && pieces[mid - 1].StartLineNumber <= lineNumber && lineNumber <= pieces[mid - 1].EndLineNumber)
                {
                    mid--;
                }

                return mid;
            }
        }

        return low;
    }

    public void AcceptEdit(TextRange range, int eolCount, int firstLineLength, int lastLineLength, int firstCharCode)
    {
        for (var i = 0; i < _pieces.Count; i++)
        {
            var piece = _pieces[i];
            piece.AcceptEdit(range, eolCount, firstLineLength, lastLineLength, firstCharCode);

            if (piece.IsEmpty())
            {
                // Remove empty pieces
                _pieces.RemoveAt(i);
                i--;
            }
        }
    }
}
