// Ported from vscode (src/vs/editor/common/tokens/lineTokens.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the token array is a uint[] (upstream's Uint32Array): [endOffset0, metadata0, endOffset1, metadata1, ...];
//  - reading a token past the end of the array gives 0 instead of failing. Upstream reads `undefined` there, which
//    its bit operations turn into 0; a LineTokens with no tokens (an empty line in the tests) relies on it;
//  - ConvertToEndOffset does nothing for an empty array (upstream writes to index -2, which a typed array ignores);
//  - sliceZeroCopy and getTokensInRange (and TokenArray, TokenInfo, TokenArrayBuilder) are left out until
//    OffsetRange is ported; getStandardTokenTypeAtPosition is left out until the model has tokenization;
//  - the insertions of WithInserted are InsertedToken values, and CreateFromTextAndMetadata takes tuples.

using System.Text;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Tokens;

/// <summary>The tokens of one view line.</summary>
internal interface IViewLineTokens
{
    ILanguageIdCodec LanguageIdCodec { get; }

    bool Equals(IViewLineTokens other);

    int GetCount();

    StandardTokenType GetStandardTokenType(int tokenIndex);

    ColorId GetForeground(int tokenIndex);

    int GetEndOffset(int tokenIndex);

    string GetClassName(int tokenIndex);

    string GetInlineStyle(int tokenIndex, IReadOnlyList<string> colorMap);

    TokenPresentation GetPresentation(int tokenIndex);

    int FindTokenIndexAtOffset(int offset);

    string GetLineContent();

    uint GetMetadata(int tokenIndex);

    string GetLanguageId(int tokenIndex);

    string GetTokenText(int tokenIndex);

    void ForEach(Action<int> callback);
}

/// <summary>A token to insert with <see cref="LineTokens.WithInserted"/>.</summary>
internal readonly record struct InsertedToken(int Offset, string Text, uint TokenMetadata);

/// <summary>The tokens of one line of a model, together with the line's text.</summary>
internal sealed class LineTokens : IViewLineTokens
{
    /// <summary>Metadata of a token with no style: default foreground and background, no font style.</summary>
    public const uint DefaultTokenMetadata =
        ((uint)FontStyle.None << MetadataConsts.FontStyleOffset)
        | ((uint)ColorId.DefaultForeground << MetadataConsts.ForegroundOffset)
        | ((uint)ColorId.DefaultBackground << MetadataConsts.BackgroundOffset);

    private readonly uint[] _tokens;
    private readonly int _tokensCount;
    private readonly string _text;

    public LineTokens(uint[] tokens, string text, ILanguageIdCodec decoder)
    {
        var tokensLength = tokens.Length > 1 ? tokens[tokens.Length - 2] : 0;
        if (tokensLength != text.Length)
        {
            UnexpectedErrors.Report(new InvalidOperationException("Token length and text length do not match!"));
        }
        _tokens = tokens;
        _tokensCount = _tokens.Length >> 1;
        _text = text;
        LanguageIdCodec = decoder;
    }

    public ILanguageIdCodec LanguageIdCodec { get; }

    public static LineTokens CreateEmpty(string lineContent, ILanguageIdCodec decoder)
    {
        var tokens = new uint[2];
        tokens[0] = (uint)lineContent.Length;
        tokens[1] = DefaultTokenMetadata;

        return new LineTokens(tokens, lineContent, decoder);
    }

    public static LineTokens CreateFromTextAndMetadata(IEnumerable<(string Text, uint Metadata)> data, ILanguageIdCodec decoder)
    {
        var offset = 0;
        var fullText = new StringBuilder();
        var tokens = new List<uint>();
        foreach (var (text, metadata) in data)
        {
            tokens.Add((uint)(offset + text.Length));
            tokens.Add(metadata);
            offset += text.Length;
            fullText.Append(text);
        }
        return new LineTokens(tokens.ToArray(), fullText.ToString(), decoder);
    }

    /// <summary>Turns an array of [startOffset, metadata] pairs into [endOffset, metadata] pairs in place.</summary>
    public static void ConvertToEndOffset(uint[] tokens, int lineTextLength)
    {
        var tokenCount = tokens.Length >> 1;
        if (tokenCount == 0)
        {
            return;
        }
        var lastTokenIndex = tokenCount - 1;
        for (var tokenIndex = 0; tokenIndex < lastTokenIndex; tokenIndex++)
        {
            tokens[tokenIndex << 1] = tokens[(tokenIndex + 1) << 1];
        }
        tokens[lastTokenIndex << 1] = (uint)lineTextLength;
    }

    public static int FindIndexInTokensArray(uint[] tokens, int desiredIndex)
    {
        if (tokens.Length <= 2)
        {
            return 0;
        }

        var low = 0;
        var high = (tokens.Length >> 1) - 1;

        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            var endOffset = (long)tokens[mid << 1];

            if (endOffset == desiredIndex)
            {
                return mid + 1;
            }
            else if (endOffset < desiredIndex)
            {
                low = mid + 1;
            }
            else if (endOffset > desiredIndex)
            {
                high = mid;
            }
        }

        return low;
    }

    private uint TokenAt(int index)
    {
        return (uint)index < (uint)_tokens.Length ? _tokens[index] : 0;
    }

    public int GetTextLength()
    {
        return _text.Length;
    }

    public bool Equals(IViewLineTokens other)
    {
        if (other is LineTokens lineTokens)
        {
            return SlicedEquals(lineTokens, 0, _tokensCount);
        }
        return false;
    }

    public bool SlicedEquals(LineTokens other, int sliceFromTokenIndex, int sliceTokenCount)
    {
        if (!string.Equals(_text, other._text, StringComparison.Ordinal))
        {
            return false;
        }
        if (_tokensCount != other._tokensCount)
        {
            return false;
        }
        var from = sliceFromTokenIndex << 1;
        var to = from + (sliceTokenCount << 1);
        for (var i = from; i < to; i++)
        {
            if (TokenAt(i) != other.TokenAt(i))
            {
                return false;
            }
        }
        return true;
    }

    public string GetLineContent()
    {
        return _text;
    }

    public int GetCount()
    {
        return _tokensCount;
    }

    public int GetStartOffset(int tokenIndex)
    {
        if (tokenIndex > 0)
        {
            return (int)TokenAt((tokenIndex - 1) << 1);
        }
        return 0;
    }

    public uint GetMetadata(int tokenIndex)
    {
        return TokenAt((tokenIndex << 1) + 1);
    }

    public string GetLanguageId(int tokenIndex)
    {
        var metadata = TokenAt((tokenIndex << 1) + 1);
        var languageId = TokenMetadata.GetLanguageId(metadata);
        return LanguageIdCodec.DecodeLanguageId(languageId);
    }

    public StandardTokenType GetStandardTokenType(int tokenIndex)
    {
        var metadata = TokenAt((tokenIndex << 1) + 1);
        return TokenMetadata.GetTokenType(metadata);
    }

    public ColorId GetForeground(int tokenIndex)
    {
        var metadata = TokenAt((tokenIndex << 1) + 1);
        return TokenMetadata.GetForeground(metadata);
    }

    public string GetClassName(int tokenIndex)
    {
        var metadata = TokenAt((tokenIndex << 1) + 1);
        return TokenMetadata.GetClassNameFromMetadata(metadata);
    }

    public string GetInlineStyle(int tokenIndex, IReadOnlyList<string> colorMap)
    {
        var metadata = TokenAt((tokenIndex << 1) + 1);
        return TokenMetadata.GetInlineStyleFromMetadata(metadata, colorMap);
    }

    public TokenPresentation GetPresentation(int tokenIndex)
    {
        var metadata = TokenAt((tokenIndex << 1) + 1);
        return TokenMetadata.GetPresentationFromMetadata(metadata);
    }

    public int GetEndOffset(int tokenIndex)
    {
        return (int)TokenAt(tokenIndex << 1);
    }

    /// <summary>Find the token containing offset <paramref name="offset"/>.</summary>
    /// <returns>The index of the token containing the offset.</returns>
    public int FindTokenIndexAtOffset(int offset)
    {
        return FindIndexInTokensArray(_tokens, offset);
    }

    public IViewLineTokens Inflate()
    {
        return this;
    }

    public IViewLineTokens SliceAndInflate(int startOffset, int endOffset, int deltaOffset)
    {
        return new SliceLineTokens(this, startOffset, endOffset, deltaOffset);
    }

    /// <summary>A copy with <paramref name="insertTokens"/> inserted. Pure.</summary>
    /// <param name="insertTokens">Must be sorted by offset.</param>
    public LineTokens WithInserted(IReadOnlyList<InsertedToken> insertTokens)
    {
        if (insertTokens.Count == 0)
        {
            return this;
        }

        var nextOriginalTokenIdx = 0;
        var nextInsertTokenIdx = 0;
        var text = new StringBuilder();
        var newTokens = new List<uint>();

        var originalEndOffset = 0;
        while (true)
        {
            var nextOriginalTokenEndOffset = nextOriginalTokenIdx < _tokensCount ? (int)_tokens[nextOriginalTokenIdx << 1] : -1;
            InsertedToken? nextInsertToken = nextInsertTokenIdx < insertTokens.Count ? insertTokens[nextInsertTokenIdx] : null;

            if (nextOriginalTokenEndOffset != -1 && (nextInsertToken is null || nextOriginalTokenEndOffset <= nextInsertToken.Value.Offset))
            {
                // original token ends before next insert token
                text.Append(_text, originalEndOffset, nextOriginalTokenEndOffset - originalEndOffset);
                var metadata = _tokens[(nextOriginalTokenIdx << 1) + 1];
                newTokens.Add((uint)text.Length);
                newTokens.Add(metadata);
                nextOriginalTokenIdx++;
                originalEndOffset = nextOriginalTokenEndOffset;
            }
            else if (nextInsertToken is { } insertToken)
            {
                if (insertToken.Offset > originalEndOffset)
                {
                    // insert token is in the middle of the next token.
                    text.Append(_text, originalEndOffset, insertToken.Offset - originalEndOffset);
                    var metadata = TokenAt((nextOriginalTokenIdx << 1) + 1);
                    newTokens.Add((uint)text.Length);
                    newTokens.Add(metadata);
                    originalEndOffset = insertToken.Offset;
                }

                text.Append(insertToken.Text);
                newTokens.Add((uint)text.Length);
                newTokens.Add(insertToken.TokenMetadata);
                nextInsertTokenIdx++;
            }
            else
            {
                break;
            }
        }

        return new LineTokens(newTokens.ToArray(), text.ToString(), LanguageIdCodec);
    }

    public string GetTokenText(int tokenIndex)
    {
        var startOffset = GetStartOffset(tokenIndex);
        var endOffset = GetEndOffset(tokenIndex);
        return _text.Substring(startOffset, endOffset - startOffset);
    }

    public void ForEach(Action<int> callback)
    {
        var tokenCount = GetCount();
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            callback(tokenIndex);
        }
    }

    public override string ToString()
    {
        var result = new StringBuilder();
        for (var i = 0; i < GetCount(); i++)
        {
            result.Append('[').Append(GetTokenText(i)).Append("]{").Append(GetClassName(i)).Append('}');
        }
        return result.ToString();
    }
}

/// <summary>A view of a range of a <see cref="LineTokens"/>, with its offsets moved by a delta.</summary>
internal sealed class SliceLineTokens : IViewLineTokens
{
    private readonly LineTokens _source;
    private readonly int _startOffset;
    private readonly int _endOffset;
    private readonly int _deltaOffset;

    private readonly int _firstTokenIndex;
    private readonly int _tokensCount;

    public SliceLineTokens(LineTokens source, int startOffset, int endOffset, int deltaOffset)
    {
        _source = source;
        _startOffset = startOffset;
        _endOffset = endOffset;
        _deltaOffset = deltaOffset;
        _firstTokenIndex = source.FindTokenIndexAtOffset(startOffset);
        LanguageIdCodec = source.LanguageIdCodec;

        _tokensCount = 0;
        for (int i = _firstTokenIndex, len = source.GetCount(); i < len; i++)
        {
            var tokenStartOffset = source.GetStartOffset(i);
            if (tokenStartOffset >= endOffset)
            {
                break;
            }
            _tokensCount++;
        }
    }

    public ILanguageIdCodec LanguageIdCodec { get; }

    public uint GetMetadata(int tokenIndex)
    {
        return _source.GetMetadata(_firstTokenIndex + tokenIndex);
    }

    public string GetLanguageId(int tokenIndex)
    {
        return _source.GetLanguageId(_firstTokenIndex + tokenIndex);
    }

    public string GetLineContent()
    {
        return _source.GetLineContent().Substring(_startOffset, _endOffset - _startOffset);
    }

    public bool Equals(IViewLineTokens other)
    {
        if (other is SliceLineTokens slice)
        {
            return _startOffset == slice._startOffset
                && _endOffset == slice._endOffset
                && _deltaOffset == slice._deltaOffset
                && _source.SlicedEquals(slice._source, _firstTokenIndex, _tokensCount);
        }
        return false;
    }

    public int GetCount()
    {
        return _tokensCount;
    }

    public StandardTokenType GetStandardTokenType(int tokenIndex)
    {
        return _source.GetStandardTokenType(_firstTokenIndex + tokenIndex);
    }

    public ColorId GetForeground(int tokenIndex)
    {
        return _source.GetForeground(_firstTokenIndex + tokenIndex);
    }

    public int GetEndOffset(int tokenIndex)
    {
        var tokenEndOffset = _source.GetEndOffset(_firstTokenIndex + tokenIndex);
        return Math.Min(_endOffset, tokenEndOffset) - _startOffset + _deltaOffset;
    }

    public string GetClassName(int tokenIndex)
    {
        return _source.GetClassName(_firstTokenIndex + tokenIndex);
    }

    public string GetInlineStyle(int tokenIndex, IReadOnlyList<string> colorMap)
    {
        return _source.GetInlineStyle(_firstTokenIndex + tokenIndex, colorMap);
    }

    public TokenPresentation GetPresentation(int tokenIndex)
    {
        return _source.GetPresentation(_firstTokenIndex + tokenIndex);
    }

    public int FindTokenIndexAtOffset(int offset)
    {
        return _source.FindTokenIndexAtOffset(offset + _startOffset - _deltaOffset) - _firstTokenIndex;
    }

    public string GetTokenText(int tokenIndex)
    {
        var adjustedTokenIndex = _firstTokenIndex + tokenIndex;
        var tokenStartOffset = _source.GetStartOffset(adjustedTokenIndex);
        var tokenEndOffset = _source.GetEndOffset(adjustedTokenIndex);
        var text = _source.GetTokenText(adjustedTokenIndex);
        if (tokenStartOffset < _startOffset)
        {
            text = text.Substring(_startOffset - tokenStartOffset);
        }
        if (tokenEndOffset > _endOffset)
        {
            text = text.Substring(0, text.Length - (tokenEndOffset - _endOffset));
        }
        return text;
    }

    public void ForEach(Action<int> callback)
    {
        for (var tokenIndex = 0; tokenIndex < GetCount(); tokenIndex++)
        {
            callback(tokenIndex);
        }
    }
}
