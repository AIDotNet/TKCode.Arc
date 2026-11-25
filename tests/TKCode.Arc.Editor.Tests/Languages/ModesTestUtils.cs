// Ported from vscode (src/vs/editor/test/common/modesTestUtils.ts, commit b57d5c9e), MIT license.
// JsRegExp is not upstream: it turns the JavaScript regular expression literals of the tests into .NET Regex values
// through RegExps.CreateRegExp, so \s, $ and . keep their JavaScript meaning.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Tests;

internal readonly record struct TokenText(string Text, StandardTokenType Type);

internal static class ModesTestUtils
{
    public static ScopedLineTokens CreateFakeScopedLineTokens(IReadOnlyList<TokenText> rawTokens)
    {
        var tokens = new uint[rawTokens.Count << 1];
        var line = "";

        for (int i = 0, len = rawTokens.Count; i < len; i++)
        {
            var rawToken = rawTokens[i];

            var startOffset = line.Length;
            var metadata = (uint)rawToken.Type << MetadataConsts.TokenTypeOffset;

            tokens[i << 1] = (uint)startOffset;
            tokens[(i << 1) + 1] = metadata;
            line += rawToken.Text;
        }

        LineTokens.ConvertToEndOffset(tokens, line.Length);
        return Supports.CreateScopedLineTokens(new LineTokens(tokens, line, new LanguageIdCodec()), 0);
    }
}

/// <summary>A JavaScript regular expression literal <c>/source/flags</c> as a .NET Regex.</summary>
internal static class JsRegExp
{
    public static Regex Of(string source, string flags = "")
    {
        return RegExps.CreateRegExp(source, isRegex: true, new RegExpOptions(
            MatchCase: !flags.Contains('i'),
            Multiline: flags.Contains('m'),
            Global: flags.Contains('g'),
            Unicode: flags.Contains('u')));
    }
}
