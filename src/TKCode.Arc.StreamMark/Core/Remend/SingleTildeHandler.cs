// Ported from vercel/streamdown packages/remend (src/single-tilde-handler.ts), Copyright 2023 Vercel, Inc., Apache-2.0; modified.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamMark.Core.Remend;

/// <summary>
/// 转义夹在单词字符之间的单个 <c>~</c>，避免 remark-gfm（singleTilde: true）把它误读为删除线，
/// 例如 <c>20~25°C</c> → <c>20\~25°C</c>。不转义 <c>~~</c>。
/// </summary>
internal static partial class SingleTildeHandler
{
    // The TS pattern /([\p{L}\p{N}_])~(?!~)(?=[\p{L}\p{N}_])/gu matches whole code
    // points (u flag), so an astral letter or digit around the ~ counts too. .NET
    // classes see UTF-16 units, so this candidate pattern also admits a surrogate
    // on either side and the evaluator checks the full code point. The TS capture
    // of the preceding character only works around missing lookbehind in old
    // Safari; it never blocks a later match, so a lookbehind is equivalent. The
    // lookbehind follows the literal ~ so the engine can scan for ~ directly.
    [GeneratedRegex(@"~(?<=(?:[\p{L}\p{N}_]|[\uDC00-\uDFFF])~)(?!~)(?=[\p{L}\p{N}_]|[\uD800-\uDBFF])")]
    private static partial Regex SingleTildeCandidate();

    private static bool IsWordCategory(UnicodeCategory category) => category is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
        or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
        or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber;

    // Code point ending at index (inclusive) is a letter, number or underscore
    private static bool WordCodePointEndingAt(string text, int index)
    {
        var c = text[index];
        if (char.IsLowSurrogate(c))
        {
            return index > 0 && char.IsHighSurrogate(text[index - 1])
                && IsWordCategory(Rune.GetUnicodeCategory(new Rune(text[index - 1], c)));
        }
        return RemendUtils.IsWordChar(c);
    }

    // Code point starting at index is a letter, number or underscore
    private static bool WordCodePointStartingAt(string text, int index)
    {
        var c = text[index];
        if (char.IsHighSurrogate(c))
        {
            return index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                && IsWordCategory(Rune.GetUnicodeCategory(new Rune(c, text[index + 1])));
        }
        return RemendUtils.IsWordChar(c);
    }

    public static string HandleSingleTildeEscape(string text)
    {
        // Quick check: if there's no ~ in the text, skip processing
        if (string.IsNullOrEmpty(text) || !text.Contains('~'))
        {
            return text;
        }

        return SingleTildeCandidate().Replace(text, match =>
        {
            var tildeOffset = match.Index;
            if (!WordCodePointEndingAt(text, tildeOffset - 1) || !WordCodePointStartingAt(text, tildeOffset + 1))
            {
                return match.Value;
            }

            // Don't escape inside code blocks
            if (RemendUtils.IsInsideCodeBlock(text, tildeOffset))
            {
                return match.Value;
            }

            return "\\~";
        });
    }
}
