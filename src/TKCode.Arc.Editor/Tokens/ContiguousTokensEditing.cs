// Ported from vscode (src/vs/editor/common/tokens/contiguousTokensEditing.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - a line's tokens are a uint[] (upstream keeps a Uint32Array or its ArrayBuffer, to save an object), null when the
//    line has none yet; EmptyLineTokens is upstream's EMPTY_LINE_TOKENS, compared by reference (an empty array, so the
//    shared instance cannot change); toUint32Array has nothing to do and is left out;
//  - the edits change the array in place where upstream does, and return a new one where upstream does.

namespace TKCode.Arc.Editor.Tokens;

/// <summary>Adjusts the encoded tokens of one line ([endOffset, metadata] pairs) to an edit of the line.</summary>
internal static class ContiguousTokensEditing
{
    /// <summary>
    /// The tokens of a line without text (upstream's EMPTY_LINE_TOKENS), compared by reference: an array of its own, not
    /// Array.Empty, so that a tokenizer's empty result is not mistaken for it.
    /// </summary>
#pragma warning disable CA1825 // Avoid zero-length array allocations: the identity matters here.
    public static readonly uint[] EmptyLineTokens = new uint[0];
#pragma warning restore CA1825

    public static uint[]? DeleteBeginning(uint[]? lineTokens, int toChIndex)
    {
        if (lineTokens is null || ReferenceEquals(lineTokens, EmptyLineTokens))
        {
            return lineTokens;
        }

        return Delete(lineTokens, 0, toChIndex);
    }

    public static uint[]? DeleteEnding(uint[]? lineTokens, int fromChIndex)
    {
        if (lineTokens is null || ReferenceEquals(lineTokens, EmptyLineTokens))
        {
            return lineTokens;
        }

        var lineTextLength = (int)lineTokens[^2];
        return Delete(lineTokens, fromChIndex, lineTextLength);
    }

    public static uint[]? Delete(uint[]? lineTokens, int fromChIndex, int toChIndex)
    {
        if (lineTokens is null || ReferenceEquals(lineTokens, EmptyLineTokens) || fromChIndex == toChIndex)
        {
            return lineTokens;
        }

        var tokens = lineTokens;
        var tokensCount = tokens.Length >> 1;

        // special case: deleting everything
        if (fromChIndex == 0 && tokens[^2] == toChIndex)
        {
            return EmptyLineTokens;
        }

        var fromTokenIndex = LineTokens.FindIndexInTokensArray(tokens, fromChIndex);
        var fromTokenStartOffset = fromTokenIndex > 0 ? (int)tokens[(fromTokenIndex - 1) << 1] : 0;
        var fromTokenEndOffset = (int)tokens[fromTokenIndex << 1];

        if (toChIndex < fromTokenEndOffset)
        {
            // the delete range is inside a single token
            var insideDelta = (uint)(toChIndex - fromChIndex);
            for (var i = fromTokenIndex; i < tokensCount; i++)
            {
                tokens[i << 1] -= insideDelta;
            }

            return lineTokens;
        }

        int dest;
        int lastEnd;
        if (fromTokenStartOffset != fromChIndex)
        {
            tokens[fromTokenIndex << 1] = (uint)fromChIndex;
            dest = (fromTokenIndex + 1) << 1;
            lastEnd = fromChIndex;
        }
        else
        {
            dest = fromTokenIndex << 1;
            lastEnd = fromTokenStartOffset;
        }

        var delta = toChIndex - fromChIndex;
        for (var tokenIndex = fromTokenIndex + 1; tokenIndex < tokensCount; tokenIndex++)
        {
            var tokenEndOffset = (int)tokens[tokenIndex << 1] - delta;
            if (tokenEndOffset > lastEnd)
            {
                tokens[dest++] = (uint)tokenEndOffset;
                tokens[dest++] = tokens[(tokenIndex << 1) + 1];
                lastEnd = tokenEndOffset;
            }
        }

        if (dest == tokens.Length)
        {
            // nothing to trim
            return lineTokens;
        }

        var tmp = new uint[dest];
        Array.Copy(tokens, tmp, dest);
        return tmp;
    }

    public static uint[]? Append(uint[]? lineTokens, uint[]? otherTokens)
    {
        if (ReferenceEquals(otherTokens, EmptyLineTokens))
        {
            return lineTokens;
        }

        if (ReferenceEquals(lineTokens, EmptyLineTokens))
        {
            return otherTokens;
        }

        if (lineTokens is null)
        {
            return lineTokens;
        }

        if (otherTokens is null)
        {
            // cannot determine combined line length...
            return null;
        }

        var myTokens = lineTokens;
        var otherTokensCount = otherTokens.Length >> 1;

        var result = new uint[myTokens.Length + otherTokens.Length];
        Array.Copy(myTokens, result, myTokens.Length);
        var dest = myTokens.Length;
        var delta = myTokens[^2];
        for (var i = 0; i < otherTokensCount; i++)
        {
            result[dest++] = otherTokens[i << 1] + delta;
            result[dest++] = otherTokens[(i << 1) + 1];
        }

        return result;
    }

    public static uint[]? Insert(uint[]? lineTokens, int chIndex, int textLength)
    {
        if (lineTokens is null || ReferenceEquals(lineTokens, EmptyLineTokens))
        {
            // nothing to do
            return lineTokens;
        }

        var tokens = lineTokens;
        var tokensCount = tokens.Length >> 1;

        var fromTokenIndex = LineTokens.FindIndexInTokensArray(tokens, chIndex);
        if (fromTokenIndex > 0)
        {
            var fromTokenStartOffset = (int)tokens[(fromTokenIndex - 1) << 1];
            if (fromTokenStartOffset == chIndex)
            {
                fromTokenIndex--;
            }
        }

        for (var tokenIndex = fromTokenIndex; tokenIndex < tokensCount; tokenIndex++)
        {
            tokens[tokenIndex << 1] += (uint)textLength;
        }

        return lineTokens;
    }
}
