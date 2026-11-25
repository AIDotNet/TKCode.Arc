// Ported from vscode (src/vs/editor/common/languages/supports.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the module functions createScopedLineTokens and ignoreBracketsInToken are in the static class Supports;
//  - substrings clamp their indices as JavaScript's String.prototype.substring does.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Languages;

/// <summary>Helpers the language supports share.</summary>
internal static class Supports
{
    private const StandardTokenType IgnoreBracketsInTokens =
        StandardTokenType.Comment | StandardTokenType.String | StandardTokenType.RegEx;

    /// <summary>
    /// The run of tokens around <paramref name="offset"/> that have the language of the token at
    /// <paramref name="offset"/> (an embedded language is scoped to its own tokens).
    /// </summary>
    public static ScopedLineTokens CreateScopedLineTokens(LineTokens context, int offset)
    {
        var tokenCount = context.GetCount();
        var tokenIndex = context.FindTokenIndexAtOffset(offset);
        var desiredLanguageId = context.GetLanguageId(tokenIndex);

        var lastTokenIndex = tokenIndex;
        while (lastTokenIndex + 1 < tokenCount && context.GetLanguageId(lastTokenIndex + 1) == desiredLanguageId)
        {
            lastTokenIndex++;
        }

        var firstTokenIndex = tokenIndex;
        while (firstTokenIndex > 0 && context.GetLanguageId(firstTokenIndex - 1) == desiredLanguageId)
        {
            firstTokenIndex--;
        }

        return new ScopedLineTokens(
            context,
            desiredLanguageId,
            firstTokenIndex,
            lastTokenIndex + 1,
            context.GetStartOffset(firstTokenIndex),
            context.GetEndOffset(lastTokenIndex));
    }

    /// <summary>Whether brackets in a token of this type are text, not brackets (comments, strings, regular expressions).</summary>
    public static bool IgnoreBracketsInToken(StandardTokenType standardTokenType)
    {
        return (standardTokenType & IgnoreBracketsInTokens) != 0;
    }
}

/// <summary>The tokens of one line that belong to one language, with offsets relative to the first of them.</summary>
internal sealed class ScopedLineTokens
{
    private readonly LineTokens _actual;
    private readonly int _firstTokenIndex;
    private readonly int _lastTokenIndex;
    private readonly int _lastCharOffset;

    public ScopedLineTokens(
        LineTokens actual,
        string languageId,
        int firstTokenIndex,
        int lastTokenIndex,
        int firstCharOffset,
        int lastCharOffset)
    {
        _actual = actual;
        LanguageId = languageId;
        _firstTokenIndex = firstTokenIndex;
        _lastTokenIndex = lastTokenIndex;
        FirstCharOffset = firstCharOffset;
        _lastCharOffset = lastCharOffset;
        LanguageIdCodec = actual.LanguageIdCodec;
    }

    public ILanguageIdCodec LanguageIdCodec { get; }

    public string LanguageId { get; }

    public int FirstCharOffset { get; }

    public string GetLineContent()
    {
        var actualLineContent = _actual.GetLineContent();
        return SearchHelpers.JsSubstring(actualLineContent, FirstCharOffset, _lastCharOffset).ToString();
    }

    public int GetLineLength()
    {
        return _lastCharOffset - FirstCharOffset;
    }

    public string GetActualLineContentBefore(int offset)
    {
        var actualLineContent = _actual.GetLineContent();
        return SearchHelpers.JsSubstring(actualLineContent, 0, FirstCharOffset + offset).ToString();
    }

    public int GetTokenCount()
    {
        return _lastTokenIndex - _firstTokenIndex;
    }

    public int FindTokenIndexAtOffset(int offset)
    {
        return _actual.FindTokenIndexAtOffset(offset + FirstCharOffset) - _firstTokenIndex;
    }

    public StandardTokenType GetStandardTokenType(int tokenIndex)
    {
        return _actual.GetStandardTokenType(tokenIndex + _firstTokenIndex);
    }

    public IViewLineTokens ToIViewLineTokens()
    {
        return _actual.SliceAndInflate(FirstCharOffset, _lastCharOffset, 0);
    }
}
